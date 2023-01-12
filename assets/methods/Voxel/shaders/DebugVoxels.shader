// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "GBF/DebugVoxels"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geo
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			#define N 64

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float alpha : TEXCOORD0;
			};
			
			float4x4 _Obj2WorldMat;
			Texture3D<half4> _Voxels;

			float _VoxelScale;

			v2f vert (uint id : SV_VertexID, uint inst : SV_InstanceID)
			{
				float4 vertex;
				vertex.x = id % N;
				vertex.y = (id / N) % N;
				vertex.z = id / (N * N);
				vertex.w = 1;
			
				v2f o;
				o.vertex = mul(_Obj2WorldMat, vertex);
				o.alpha = _Voxels[uint3(vertex.x, vertex.y, vertex.z)].a;
				return o;
			}
			[maxvertexcount(36)]
			void geo(point v2f inputs[1], inout TriangleStream<v2f> TriStream)
			{
				if (inputs[0].alpha != 1) {
					TriStream.RestartStrip();
					return;
				}

				v2f output1 = (v2f)0;
				v2f output2 = (v2f)0;
				v2f output3 = (v2f)0;

				float3 pos[8] = {
					float3(-1.f, -1.f, -1.f), //front lower left - 0
					float3(-1.f, -1.f,  1.f), //back lower left - 1
					float3(-1.f,  1.f, -1.f), //front upper left - 2
					float3(-1.f,  1.f,  1.f), //back upper left - 3
					float3(1.f, -1.f, -1.f), //front lower right - 4
					float3(1.f, -1.f,  1.f), //back lower right - 5
					float3(1.f,  1.f, -1.f), //front upper right - 6
					float3(1.f,  1.f,  1.f), //back upper right - 7
				};

				int3 index[12] = {
					int3(0, 2, 6),//front
					int3(0, 4, 6),
					int3(2, 3, 7),//top
					int3(2, 6, 7),
					int3(3, 1, 5),//back
					int3(3, 7, 5),
					int3(1, 0, 4),//bottom
					int3(1, 5, 4),
					int3(1, 3, 2),//left
					int3(1, 0, 2),
					int3(4, 6, 7),//right
					int3(4, 5, 7)
				};
				for (int i = 0; i < 12; i++) {
					output1.vertex = inputs[0].vertex;
					output1.alpha = inputs[0].alpha;

					output2.vertex = inputs[0].vertex;
					output2.alpha = inputs[0].alpha;

					output3.vertex = inputs[0].vertex;
					output3.alpha = inputs[0].alpha;

					output1.vertex.x += pos[index[i].x].x * _VoxelScale;
					output1.vertex.y += pos[index[i].x].y * _VoxelScale;
					output1.vertex.z += pos[index[i].x].z * _VoxelScale;
					output1.vertex = mul(UNITY_MATRIX_VP, output1.vertex);

					output2.vertex.x += pos[index[i].y].x * _VoxelScale;
					output2.vertex.y += pos[index[i].y].y * _VoxelScale;
					output2.vertex.z += pos[index[i].y].z * _VoxelScale;
					output2.vertex = mul(UNITY_MATRIX_VP, output2.vertex);

					output3.vertex.x += pos[index[i].z].x * _VoxelScale;
					output3.vertex.y += pos[index[i].z].y * _VoxelScale;
					output3.vertex.z += pos[index[i].z].z * _VoxelScale;
					output3.vertex = mul(UNITY_MATRIX_VP, output3.vertex);

					TriStream.Append(output1);
					TriStream.Append(output2);
					TriStream.Append(output3);
				}
				TriStream.RestartStrip();
			}
			fixed4 frag (v2f i) : SV_Target
			{
				return float4(1, 0, 0, 1);
			}
			ENDCG
		}
	}
}
