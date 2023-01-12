using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Voxelizer : MonoBehaviour {
    public int GridSize = 8;
    public int ThreadCount = 8;
    public ComputeShader ComputeVoxels;
    public Material DebugMaterial;
    RenderTexture output;

    struct AABB {
        public Vector3 min;
        public Vector3 max;
    }
    struct BOX {
        public Vector3 center;
        public AABB bounds;
    }

	void Start () {
        output = new RenderTexture(GridSize, GridSize, 0, RenderTextureFormat.ARGBHalf);
        output.enableRandomWrite = true;
        output.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        output.volumeDepth = GridSize;
        output.autoGenerateMips = false;
        output.Create();

       // doOnce();
    }
    void FixedUpdate() {
        //output.Release();
        //output.Create();
        ComputeVoxels.SetTexture(0, "_Result", output);
        ComputeVoxels.Dispatch(0, GridSize / ThreadCount, GridSize / ThreadCount, GridSize / ThreadCount);

        Collider[] cols = Physics.OverlapBox(transform.position, transform.lossyScale * 0.5f, transform.rotation);
        foreach (Collider c in cols)
        {
            Mesh m = c.GetComponent<MeshFilter>().mesh;
            if (m == null) {
                return;
            }
            /* Vector3 scale = new Vector3(transform.localScale.x / GridSize, transform.localScale.y / GridSize, transform.localScale.z / GridSize);
             Matrix4x4 sim2world = Matrix4x4.TRS(transform.position + new Vector3(-.5f * GridSize * scale.x, -.5f * GridSize * scale.y, -.5f * GridSize * scale.z), Quaternion.identity, scale);
             Matrix4x4 world2sim = sim2world.inverse;

             Vector3 min = world2sim.MultiplyPoint(c.bounds.min);
             Vector3 max = world2sim.MultiplyPoint(c.bounds.max);

             Vector3Int offset;
             Vector3Int ilength;

             offset = new Vector3Int(Mathf.FloorToInt(Mathf.Clamp(min.x, 0, GridSize)), Mathf.FloorToInt(Mathf.Clamp(min.y, 0, GridSize)), Mathf.FloorToInt(Mathf.Clamp(min.z, 0, GridSize)));

             ilength = new Vector3Int(Mathf.CeilToInt(max.x - min.x), Mathf.CeilToInt(max.y - min.y), Mathf.CeilToInt(max.z - min.z));
             ilength.x = Mathf.Max(GetNearPow2(ilength.x), ThreadCount);
             ilength.y = Mathf.Max(GetNearPow2(ilength.y), ThreadCount);
             ilength.z = Mathf.Max(GetNearPow2(ilength.z), ThreadCount);

             Vector3[] vertices = m.vertices;
             int[] triangles = m.triangles;

             Rigidbody rb = c.GetComponent<Rigidbody>();
             Vector3 vel = rb != null ? rb.velocity : Vector3.zero;

             ComputeBuffer input_verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
             ComputeBuffer input_tris = new ComputeBuffer(triangles.Length, sizeof(int));

             input_verts.SetData(vertices);
             input_tris.SetData(triangles);

             ComputeVoxels.SetInt("_NumTriangles", triangles.Length);
             ComputeVoxels.SetVector("_Offset", new Vector3(offset.x, offset.y, offset.z));

             ComputeVoxels.SetMatrix("_Sim2WorldMat", sim2world);
             ComputeVoxels.SetMatrix("_Obj2WorldMat", c.transform.localToWorldMatrix);

             ComputeVoxels.SetBuffer(0, "_Vertices", input_verts);
             ComputeVoxels.SetBuffer(0, "_Triangles", input_tris);
             ComputeVoxels.SetTexture(0, "_Result", output);

             ComputeVoxels.Dispatch(0, ilength.x / ThreadCount, ilength.y / ThreadCount, ilength.z / ThreadCount);

             DebugMaterial.SetTexture("_Voxels", output);

             input_verts.Release();
             input_tris.Release();*/

            Vector3 scale = new Vector3(transform.localScale.x / GridSize, transform.localScale.y / GridSize, transform.localScale.z / GridSize);
            Matrix4x4 sim2world = Matrix4x4.TRS(transform.position + new Vector3(-.5f * GridSize * scale.x, -.5f * GridSize * scale.y, -.5f * GridSize * scale.z), Quaternion.identity, scale);
            Matrix4x4 world2sim = sim2world.inverse;

            int num_triangles = m.triangles.Length / 3;

            Vector3[] vertices = m.vertices;
            int[] triangles = m.triangles;

            ComputeBuffer input_verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            ComputeBuffer input_tris = new ComputeBuffer(triangles.Length, sizeof(int));

            input_verts.SetData(vertices);
            input_tris.SetData(triangles);

            ComputeVoxels.SetInt("_NumTriangles", num_triangles);

            ComputeVoxels.SetMatrix("_Sim2WorldMat", world2sim);
            ComputeVoxels.SetMatrix("_Obj2WorldMat", c.transform.localToWorldMatrix);

            ComputeVoxels.SetBuffer(1, "_Vertices", input_verts);
            ComputeVoxels.SetBuffer(1, "_Triangles", input_tris);
            ComputeVoxels.SetTexture(1, "_Result", output);

            ComputeVoxels.Dispatch(1, num_triangles / 8 + 1, 1 , 1);

            DebugMaterial.SetTexture("_Voxels", output);

            input_verts.Release();
            input_tris.Release();
        }
    }
    int GetNearPow2(float n)
    {
        if (n <= 0)
        {
            return 0;
        }
        var k = Mathf.CeilToInt(Mathf.Log(n, 2));
        return (int)Mathf.Pow(2, k);
    }
    void OnRenderObject()
    {
        DebugMaterial.SetPass(0);
        Vector3 scale = new Vector3(transform.localScale.x / GridSize, transform.localScale.y / GridSize, transform.localScale.z / GridSize);
        DebugMaterial.SetMatrix("_Obj2WorldMat", Matrix4x4.TRS(transform.position + new Vector3(-.5f * GridSize * scale.x, -.5f * GridSize * scale.y, -.5f * GridSize * scale.z), Quaternion.identity, scale));
        DebugMaterial.SetFloat("_VoxelScale", 0.5f * (transform.localScale.x / GridSize));
        Graphics.DrawProcedural(MeshTopology.Points, GridSize * GridSize * GridSize);
    }
    void OnDestroy() {
        output.Release();
    }
}
