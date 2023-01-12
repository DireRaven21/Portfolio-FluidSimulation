using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GBFTexDriver : MonoBehaviour {
    const int READ = 0;
    const int WRITE = 1;

    public int N = 64;
    public int NumThreads = 8;
    public float TimeStep = 0.1f;
    public int Iterations = 20;
    public float Viscosity = 1f;
    public float VorticyModifier = 1f;
    public float AmbientTemp = 0.1f;
    public float TemperatureDecay = 0.05f;
    public float DensityDecay = 0.05f;
    public float Mass = 0.001f;

    Vector3 pos;
    Vector3 scale;

    public enum EnumDrawMode
    {
        Debug,
        Volumetric
    };

    public EnumDrawMode DrawMode;

    public ComputeShader ComputeVoxels;
    public ComputeShader Sources;
    public ComputeShader Advect;
    public ComputeShader Diffuse;
    public ComputeShader Vorticity;
    public ComputeShader ExternalForces;
    public ComputeShader CalcDivergence;
    public ComputeShader Projection;
    public ComputeShader Draw;

    Material mat;

    RenderTexture tex;
    RenderTexture[] pressure;
    RenderTexture[] velocity;
    RenderTexture[] density;
    RenderTexture[] temperature;
    RenderTexture divergence;
    RenderTexture staticboundaries;
    RenderTexture boundaries;
    RenderTexture phi_n_hat;
    RenderTexture phi_n1_hat;
    RenderTexture vorticity;

    Bounds AABB;
    Matrix4x4 world2sim;

    //bool simulate = true;
    void Awake()
    {
        pos = transform.position;
        scale = transform.localScale;

        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;

        Camera.main.depthTextureMode = DepthTextureMode.Depth;
    }
    // Use this for initialization
    void Start()
    {
        //int size = N * N * N;
        tex = new RenderTexture(N, N, 24);
        tex.enableRandomWrite = true;
        tex.autoGenerateMips = false;
        tex.filterMode = FilterMode.Point;
        tex.Create();

        RenderTextureDescriptor scalardesc = new RenderTextureDescriptor(N, N, RenderTextureFormat.RHalf, 0);
        scalardesc.enableRandomWrite = true;
        scalardesc.autoGenerateMips = false;
        scalardesc.sRGB = false;
        scalardesc.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        scalardesc.volumeDepth = N;

        RenderTextureDescriptor vectordesc = new RenderTextureDescriptor(N, N, RenderTextureFormat.ARGBHalf, 0);
        vectordesc.enableRandomWrite = true;
        vectordesc.autoGenerateMips = false;
        vectordesc.sRGB = false;
        vectordesc.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        vectordesc.volumeDepth = N;

        pressure = new RenderTexture[2];
        pressure[READ] = new RenderTexture(scalardesc);
        pressure[READ].Create();

        pressure[WRITE] = new RenderTexture(scalardesc);
        pressure[WRITE].Create();

        velocity = new RenderTexture[2];
        velocity[READ] = new RenderTexture(vectordesc);
        velocity[READ].Create();

        velocity[WRITE] = new RenderTexture(vectordesc);
        velocity[WRITE].Create();

        density = new RenderTexture[2];
        density[READ] = new RenderTexture(scalardesc);
        density[READ].Create();

        density[WRITE] = new RenderTexture(scalardesc);
        density[WRITE].Create();

        temperature = new RenderTexture[2];
        temperature[READ] = new RenderTexture(scalardesc);
        temperature[READ].Create();

        temperature[WRITE] = new RenderTexture(scalardesc);
        temperature[WRITE].Create();

        divergence = new RenderTexture(scalardesc);
        divergence.Create();

        staticboundaries = new RenderTexture(vectordesc);
        staticboundaries.Create();

        boundaries = new RenderTexture(vectordesc);
        boundaries.Create();

        phi_n_hat = new RenderTexture(scalardesc);
        phi_n_hat.Create();

        phi_n1_hat = new RenderTexture(scalardesc);
        phi_n1_hat.Create();

        vorticity = new RenderTexture(vectordesc);
        vorticity.Create();

        mat = GetComponent<MeshRenderer>().material;
        mat.mainTexture = tex;

        Vector3 scale = new Vector3(transform.localScale.x / N, transform.localScale.y / N, transform.localScale.z / N);
        world2sim = Matrix4x4.TRS(transform.position + new Vector3(-.5f * N * scale.x, -.5f * N * scale.y, -.5f * N * scale.z), Quaternion.identity, scale).inverse;

        AABB = new Bounds(transform.position, transform.lossyScale);

        setupEnclosedBoundaries();
        VoxelizeBondaries(staticboundaries, true);

    }
    void Update() {
        transform.position = pos;
        transform.rotation = Quaternion.identity;
        transform.localScale = scale;
    }
    void FixedUpdate()
    {
        clearBoundary();
        VoxelizeBondaries(boundaries);

        advectMacCormack(density, DensityDecay);
        advectMacCormack(temperature, TemperatureDecay, AmbientTemp);

        GBFSource[] sources = FindObjectsOfType<GBFSource>();
        foreach (GBFSource s in sources)
        {
            if (AABB.Contains(s.transform.position))
            {
                addSources(density, s.transform.position, s.DensityRate);
                addSources(temperature, s.transform.position, s.TemperatureRate);
            }
        }

        advectVel(velocity[READ], velocity[WRITE]);
        
        applyForces();
        applyVorticyConfinement();
        project();

        if (DrawMode == EnumDrawMode.Debug)
        {
            debugDraw();
        }
        else
        {
            setMaterialProperties();
        }
    }
    void swapBuffers(RenderTexture[] buffer)
    {
        RenderTexture tmp = buffer[READ];
        buffer[READ] = buffer[WRITE];
        buffer[WRITE] = tmp;
    }
    void setupEnclosedBoundaries() {
        ComputeVoxels.SetTexture(1, "_Result", staticboundaries);
        ComputeVoxels.Dispatch(1, N / 8, N / 8, N / 8);
    }
    void clearBoundary() {
        ComputeVoxels.SetTexture(0, "_Static", staticboundaries);
        ComputeVoxels.SetTexture(0, "_Result", boundaries);
        ComputeVoxels.Dispatch(0, N / 8, N / 8, N / 8);
    }
    void VoxelizeBondaries(RenderTexture res, bool only_static = false)
    {
        Collider[] cols = Physics.OverlapBox(transform.position, transform.lossyScale * 0.5f, transform.rotation);
        foreach (Collider c in cols)
        {
            Rigidbody rb = c.transform.GetComponent<Rigidbody>();
            if (only_static ^ rb == null) {
                continue;
            }

            Mesh m = c.GetComponent<MeshFilter>().mesh;
            if (m == null)
            {
                continue;
            }

            Vector3 vel = rb != null ? rb.velocity : Vector3.zero;
            
            int num_triangles = m.triangles.Length / 3;

            Vector3[] vertices = m.vertices;
            int[] triangles = m.triangles;

            ComputeBuffer input_verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            ComputeBuffer input_tris = new ComputeBuffer(triangles.Length, sizeof(int));

            input_verts.SetData(vertices);
            input_tris.SetData(triangles);

            ComputeVoxels.SetInt("_NumTriangles", num_triangles);
            ComputeVoxels.SetVector("_Velocity", vel);

            ComputeVoxels.SetMatrix("_Matrix", world2sim * c.transform.localToWorldMatrix);

            ComputeVoxels.SetBuffer(2, "_Vertices", input_verts);
            ComputeVoxels.SetBuffer(2, "_Triangles", input_tris);
            ComputeVoxels.SetTexture(2, "_Result", res);

            ComputeVoxels.Dispatch(2, num_triangles / 8 + 1, 1, 1);

            input_verts.Release();
            input_tris.Release();
        }
    }
    void addSources(RenderTexture[] b, Vector3 pos, float a, float radius = 0.1f)
    {
        Sources.SetFloat("DT", TimeStep);
        Sources.SetVector("Center", world2sim.MultiplyPoint(pos));
        Sources.SetFloat("Ammount", a);
        Sources.SetFloat("Radius", radius);
        Sources.SetTexture(0, "DensRead", b[READ]);
        Sources.SetTexture(0, "DensWrite", b[WRITE]);

        Sources.Dispatch(0, N / NumThreads, N / NumThreads, N / NumThreads);
        swapBuffers(b);
    }
    void advect(RenderTexture[] b, float decay = 0f, float min = 0f)
    {
        Advect.SetFloat("DT", TimeStep);
        Advect.SetFloat("SCL", 1);
        Advect.SetFloat("Decay", decay);
        Advect.SetTexture(1, "Vel", velocity[READ]);
        Advect.SetTexture(1, "Boundaries", boundaries);
        Advect.SetTexture(1, "ScalarQtyRead", b[READ]);
        Advect.SetTexture(1, "ScalarQtyWrite", b[WRITE]);

        Advect.Dispatch(1, N / NumThreads, N / NumThreads, N / NumThreads);
        swapBuffers(b);
    }
    void advect(RenderTexture read, RenderTexture write, float scale = 1.0f)
    {
        Advect.SetFloat("DT", TimeStep);
        Advect.SetFloat("SCL", scale);

        Advect.SetTexture(1, "Vel", velocity[READ]);
        Advect.SetTexture(1, "Boundaries", boundaries);
        Advect.SetTexture(1, "ScalarQtyRead", read);
        Advect.SetTexture(1, "ScalarQtyWrite", write);

        Advect.Dispatch(1, N / NumThreads, N / NumThreads, N / NumThreads);
    }
    void advectMacCormack(RenderTexture[] b, float decay = 0f, float min = 0f)
    {
        advect(b[READ], phi_n1_hat);
        advect(phi_n1_hat, phi_n_hat, -1.0f);

        Advect.SetFloat("DT", TimeStep);
        Advect.SetFloat("Decay", decay);
        Advect.SetFloat("Min", min);
        Advect.SetTexture(2, "Boundaries", boundaries);
        Advect.SetTexture(2, "Vel", velocity[READ]);
        Advect.SetTexture(2, "phi_n_hat_1f", phi_n_hat);
        Advect.SetTexture(2, "phi_n1_hat_1f", phi_n1_hat);
        Advect.SetTexture(2, "ScalarQtyRead", b[READ]);
        Advect.SetTexture(2, "ScalarQtyWrite", b[WRITE]);

        Advect.Dispatch(2, N / NumThreads, N / NumThreads, N / NumThreads);
        swapBuffers(b);
    }
    void advectVel(RenderTexture read, RenderTexture write, float scl = 1.0f)
    {
        Advect.SetFloat("DT", TimeStep);
        Advect.SetFloat("SCL", scl);

        Advect.SetTexture(0, "Boundaries", boundaries);
        Advect.SetTexture(0, "Vel", velocity[READ]);
        Advect.SetTexture(0, "VectorQtyRead", read);
        Advect.SetTexture(0, "VectorQtyWrite", write);

        Advect.Dispatch(0, N / NumThreads, N / NumThreads, N / NumThreads);
        swapBuffers(velocity);
    }
    void applyVorticyConfinement()
    {
        Vorticity.SetTexture(0, "Boundaries", boundaries);
        Vorticity.SetTexture(0, "Vel", velocity[READ]);
        Vorticity.SetTexture(0, "Vorticy", vorticity);
        Vorticity.Dispatch(0, N / NumThreads, N / NumThreads, N / NumThreads);

        Vorticity.SetFloat("DT", TimeStep);
        Vorticity.SetFloat("Epsilon", VorticyModifier);
        Vorticity.SetTexture(1, "Boundaries", boundaries);
        Vorticity.SetTexture(1, "Vorticy", vorticity);
        Vorticity.SetTexture(1, "Vel", velocity[READ]);
        Vorticity.SetTexture(1, "VelWrite", velocity[WRITE]);
        Vorticity.Dispatch(1, N / NumThreads, N / NumThreads, N / NumThreads);
        swapBuffers(velocity);
    }
    void applyForces()
    {
        ExternalForces.SetFloat("Ambient", AmbientTemp);
        ExternalForces.SetFloat("DT", TimeStep);
        ExternalForces.SetTexture(0, "VelRead", velocity[READ]);
        ExternalForces.SetTexture(0, "VelWrite", velocity[WRITE]);
        ExternalForces.SetTexture(0, "Temp", temperature[READ]);
        ExternalForces.SetTexture(0, "Dens", density[READ]);
        ExternalForces.Dispatch(0, N / NumThreads, N / NumThreads, N / NumThreads);
        swapBuffers(velocity);
    }
    void project()
    {
        CalcDivergence.SetFloat("SCL", 0.5f);
        CalcDivergence.SetTexture(0, "Boundaries", boundaries);
        CalcDivergence.SetTexture(0, "Vel", velocity[READ]);
        CalcDivergence.SetTexture(0, "Div", divergence);
        CalcDivergence.SetTexture(0, "P", pressure[WRITE]);
        CalcDivergence.Dispatch(0, N / NumThreads, N / NumThreads, N / NumThreads);
        swapBuffers(pressure);

        Diffuse.SetTexture(0, "Boundaries", boundaries);
        Diffuse.SetTexture(0, "ConstField", divergence);
        for (int i = 0; i < Iterations; i++)
        {
            Diffuse.SetTexture(0, "XRead", pressure[READ]);
            Diffuse.SetTexture(0, "XWrite", pressure[WRITE]);

            Diffuse.Dispatch(0, N / NumThreads, N / NumThreads, N / NumThreads);
            swapBuffers(pressure);
        }

        Projection.SetFloat("SCL", 0.5f);
        Projection.SetTexture(0, "Boundaries", boundaries);
        Projection.SetTexture(0, "VelRead", velocity[READ]);
        Projection.SetTexture(0, "VelWrite", velocity[WRITE]);
        Projection.SetTexture(0, "Pressure", pressure[READ]);
        Projection.Dispatch(0, N / NumThreads, N / NumThreads, N / NumThreads);
        swapBuffers(velocity);
    }
    void debugDraw()
    {
        Draw.SetTexture(0, "Dens", density[READ]);
        Draw.SetTexture(0, "Temp", temperature[READ]);
        Draw.SetTexture(0, "Vel", velocity[READ]);
        Draw.SetTexture(0, "Boundaries", boundaries);
        Draw.SetTexture(0, "Result", tex);
        Draw.Dispatch(0, N / NumThreads, N / NumThreads, 1);
    }
    void setMaterialProperties()
    {
        Vector3 ray_x = Camera.main.ScreenPointToRay(new Vector3(Screen.width, 0)).direction;
        Vector3 ray_y = Camera.main.ScreenPointToRay(new Vector3(0, Screen.height)).direction;
        Vector3 ray_0 = Camera.main.ScreenPointToRay(new Vector3(0, 0)).direction;
        Vector3 ray_2 = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2)).direction;

        float Angle = Vector3.Angle(ray_x, ray_2);

        float Hipo = Camera.main.farClipPlane / Mathf.Cos(Mathf.PI * Angle / 180);
        Vector3 screen_center = transform.position + Hipo * ray_0;

        Vector3 X_Vector = Hipo * ray_x - Hipo * ray_0;
        Vector3 Y_Vector = Hipo * ray_y - Hipo * ray_0;

        mat.SetVector("_Translate", transform.localPosition);
        mat.SetVector("_Scale", transform.localScale);
        mat.SetVector("_Size", new Vector3(N, N, N));
        mat.SetVector("_CamCenter", screen_center);
        mat.SetVector("_CamX", X_Vector);
        mat.SetVector("_CamY", Y_Vector);
        mat.SetTexture("_Density", density[READ]);
    }
    void OnDestroy()
    {
        pressure[READ].Release();
        pressure[WRITE].Release();

        velocity[READ].Release();
        velocity[WRITE].Release();

        density[READ].Release();
        density[WRITE].Release();

        temperature[READ].Release();
        temperature[WRITE].Release();

        divergence.Release();
        boundaries.Release();

        phi_n_hat.Release();
        phi_n1_hat.Release();
    }
}
