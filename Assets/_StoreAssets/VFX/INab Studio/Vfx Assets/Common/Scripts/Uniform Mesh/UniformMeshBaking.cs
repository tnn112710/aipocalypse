using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using INab.CommonVFX;
using UnityEngine.VFX.Utility;
using System.Linq;


namespace INab.VFXAssets
{
    /// <summary>
    /// Sampling information structure
    /// - coord is a barycentric coordinate
    /// - index is an index of triangle
    /// </summary>
    [Serializable]
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct BarycentricTriangleSampling
    {
        public Vector2 coord;
        public uint index;
    }

    [Serializable]
    public class UniformMeshBaker
    {
        public static string GraphicsBufferName = "UniformMeshBuffer";
        public int SampleCount
        {
            get => Mathf.Min((int)(_sampleCount * Mathf.Pow(SampleCountMultiplier, 2)), 100000);
        }

        [Tooltip("Amount of points from which particles would be spawned.")]
        [SerializeField]
        private int _sampleCount = 2048;

        [Range(0.01f, 10f), SerializeField]
        [Tooltip("Multiply sample count by this value to control density of the particles. Keep this as low as possible.")]
        public float SampleCountMultiplier = 1f;

        [SerializeField]
        public BarycentricTriangleSampling[] m_BakedSampling;

        [NonSerialized]
        public GraphicsBuffer m_Buffer;

        // New Freature - baking buffer per submesh.
        [SerializeField] public bool UsePerSubmeshBaking = false;
        [SerializeField] public int SubmeshIndex = 0;

        private void ComputeBakedSampling(VisualEffect visualEffect, Mesh mesh)
        {
            if (visualEffect == null)
            {
                Debug.LogWarning("UniformBaker expects a VisualEffect on the shared game object.");
                return;
            }

            if (!visualEffect.HasGraphicsBuffer(GraphicsBufferName))
            {
                //Debug.LogWarningFormat("Graphics Buffer property '{0}' is invalid.", GraphicsBufferName);
                return;
            }

            var meshData = UniformMeshSamplingHelper.ComputeDataCache(mesh, UsePerSubmeshBaking, SubmeshIndex);

            _sampleCount = meshData.triangles.Length;

            // Baked indices already address the mesh index buffer directly, so no extra offset is needed.
            if (visualEffect.HasUInt("Start Triangle Index")) visualEffect.SetUInt("Start Triangle Index", 0);

            int sampleCount = SampleCount;
            if (sampleCount < 1 || meshData.triangles.Length < 1)
            {
                m_BakedSampling = Array.Empty<BarycentricTriangleSampling>();
                return;
            }

            var rand = new System.Random(123); // use random number as seed
            m_BakedSampling = new BarycentricTriangleSampling[sampleCount];
            for (int i = 0; i < sampleCount; ++i)
            {
                var sampling = UniformMeshSamplingHelper.GetNextSampling(meshData, rand);
                sampling.index = meshData.triangleIndices[sampling.index];
                m_BakedSampling[i] = sampling;
            }
        }
        private void UpdateGraphicsBuffer()
        {
            if (m_BakedSampling == null) return;

            if (m_Buffer != null)
            {
                m_Buffer.Release();
                m_Buffer = null;
            }

            if (m_BakedSampling.Length < 1) return;

            if (SampleCount != m_BakedSampling.Length)
            {
                //Debug.LogErrorFormat("The length of baked data mismatches with sample count : {0} vs {1}", SampleCount, m_BakedSampling.Length);
                return;
            }

            m_Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_BakedSampling.Length, Marshal.SizeOf(typeof(BarycentricTriangleSampling)));
            m_Buffer.SetData(m_BakedSampling);
        }
        private void BindGraphicsBuffer(VisualEffect vfx)
        {
            if (vfx == null) return;
            if (vfx.HasGraphicsBuffer(GraphicsBufferName)) vfx.SetGraphicsBuffer(GraphicsBufferName, m_Buffer);
        }

        public void Update(VisualEffect visualEffect, Renderer renderer)
        {
            if (m_BakedSampling == null || m_BakedSampling.Length < 1)
            {
                Bake(visualEffect, renderer);
            }
            else if (m_Buffer == null)
            {
                Bake(visualEffect, renderer);
            }

            // NEW NEW NEW

            // What to test



            // What didnt work

            //Bake(visualEffect, renderer);
            //visualEffect.SetGraphicsBuffer(GraphicsBufferName, m_Buffer);

        }
        public void OnDisable()
        {
            if (m_Buffer != null)
            {
                m_Buffer.Release();
                m_Buffer = null;
            }
        }
        public void Bake(VisualEffect visualEffect, Renderer renderer)
        {
            ComputeBakedSampling(visualEffect, UniformMeshSamplingHelper.RendererToMesh(renderer));
            UpdateGraphicsBuffer();
            BindGraphicsBuffer(visualEffect);
        }
        public void SetGraphicsBuffer(VisualEffect visualEffect)
        {
            UpdateGraphicsBuffer();
            BindGraphicsBuffer(visualEffect);
        }

    }

    public static class MeshSetup
    {
        public static string MeshProperty = "Mesh Renderer";
        public static string SkinnedMeshProperty = "Skinned Renderer";
        public static string UseSkinnedMeshProperty = "Use Skinned Mesh";

        public static void SetupPropertyBinder(VFXPropertyBinder propertyBinder, Transform transform)
        {
            var lossScaleBinders = propertyBinder.GetPropertyBinders<VFXLossyTransformBinder>();

            VFXLossyTransformBinder lossyTransformBinder;

            if (lossScaleBinders.Count() == 0)
            {
                lossyTransformBinder = propertyBinder.AddPropertyBinder<VFXLossyTransformBinder>();
            }
            else
            {
                lossyTransformBinder = lossScaleBinders.First();
            }

            if (transform)
            {
                lossyTransformBinder.Target = transform;
            }
        }


        public static void SetupRenderer(Renderer renderer, VisualEffect visualEffect)
        {
            if (visualEffect.visualEffectAsset == null)
            {
                return;
            }

            if (renderer == null) return;

            bool useSkinnedMesh = false;

            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                visualEffect.SetSkinnedMeshRenderer(SkinnedMeshProperty, skinnedMeshRenderer);
                useSkinnedMesh = true;
            }
            else
            {
                visualEffect.SetMesh(MeshProperty, UniformMeshSamplingHelper.RendererToMesh(renderer));
            }
            visualEffect.SetBool(UseSkinnedMeshProperty, useSkinnedMesh);

        }

    }

    /// <summary>
    /// Cache of mesh data
    /// Contains raw attributes extracted from a readable Mesh
    /// </summary>
    public class RawMeshData
    {
        public struct Vertex
        {
            public Vector3 position;
            public Color color;
            public Vector3 normal;
            public Vector4 tangent;

            public static Vertex operator +(Vertex a, Vertex b)
            {
                var r = new Vertex()
                {
                    position = a.position + b.position,
                    color = a.color + b.color,
                    normal = a.normal + b.normal,
                    tangent = a.tangent + b.tangent,
                };

                return r;
            }

            public static Vertex operator *(float a, Vertex b)
            {
                var r = new Vertex()
                {
                    position = a * b.position,
                    color = a * b.color,
                    normal = a * b.normal,
                    tangent = a * b.tangent,
                };

                return r;
            }
        };

        public struct Triangle
        {
            public uint a, b, c;
        };

        public Vertex[] vertices;
        public Triangle[] triangles;
        public double[] accumulatedTriangleArea;

        /// <summary>
        /// Triangle index inside the mesh index buffer for each entry of <see cref="triangles"/>.
        /// This is what the VFX graph uses to look the triangle up, so it stays valid for any
        /// combination of sub-meshes and Mesh LODs.
        /// </summary>
        public uint[] triangleIndices;
    }

    static public class UniformMeshSamplingHelper
    {
        public static Mesh RendererToMesh(Renderer meshRenderer)
        {
            if (meshRenderer == null) return null;

            if (meshRenderer is SkinnedMeshRenderer skinnedMeshRenderer) return skinnedMeshRenderer.sharedMesh;

            var filter = meshRenderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        /// <summary>
        /// Extract and compute the accumulative sum of triangle area needed for uniform sampling
        /// </summary>
        public static RawMeshData ComputeDataCache(Mesh input, bool useSubMesh, int submeshIndex)
        {
            var meshData = new RawMeshData()
            {
                vertices = Array.Empty<RawMeshData.Vertex>(),
                triangles = Array.Empty<RawMeshData.Triangle>(),
                triangleIndices = Array.Empty<uint>(),
                accumulatedTriangleArea = Array.Empty<double>(),
            };

            if (input == null || !input.isReadable || input.subMeshCount < 1) return meshData;

            // Vertices are kept in full mesh space so triangle indices stay absolute for every sub-mesh.
            var positions = input.vertices;
            var normals = input.normals;
            var tangents = input.tangents;
            var colors = input.colors;

            int vertexCount = positions.Length;
            if (vertexCount < 3) return meshData;

            bool hasNormals = normals.Length == vertexCount;
            bool hasTangents = tangents.Length == vertexCount;
            bool hasColors = colors.Length == vertexCount;

            meshData.vertices = new RawMeshData.Vertex[vertexCount];
            for (int i = 0; i < vertexCount; ++i)
            {
                meshData.vertices[i] = new RawMeshData.Vertex()
                {
                    position = positions[i],
                    color = hasColors ? colors[i] : Color.white,
                    normal = hasNormals ? normals[i] : Vector3.up,
                    tangent = hasTangents ? tangents[i] : Vector4.one,
                };
            }

            int firstSubMesh = 0;
            int lastSubMesh = input.subMeshCount - 1;
            if (useSubMesh) firstSubMesh = lastSubMesh = Mathf.Clamp(submeshIndex, 0, input.subMeshCount - 1);

            var triangles = new List<RawMeshData.Triangle>();
            var triangleIndices = new List<uint>();

            for (int subMesh = firstSubMesh; subMesh <= lastSubMesh; ++subMesh)
            {
                if (input.GetTopology(subMesh) != MeshTopology.Triangles) continue;
                AppendSubMeshTriangles(input, subMesh, vertexCount, triangles, triangleIndices);
            }

            meshData.triangles = triangles.ToArray();
            meshData.triangleIndices = triangleIndices.ToArray();

            if (meshData.triangles.Length >= 1)
            {
                meshData.accumulatedTriangleArea = new double[meshData.triangles.Length];
                meshData.accumulatedTriangleArea[0] = ComputeTriangleArea(meshData, 0);
                for (uint i = 1; i < meshData.triangles.Length; ++i)
                {
                    meshData.accumulatedTriangleArea[i] = meshData.accumulatedTriangleArea[i - 1] + ComputeTriangleArea(meshData, i);
                }
            }

            return meshData;
        }

        /// <summary>
        /// Collects the renderable triangles of a sub-mesh together with their index buffer location.
        /// When the mesh was imported with "Generate Mesh LODs", only LOD0 is sampled, because the
        /// remaining LOD levels are simplified copies of the same surface.
        /// </summary>
        static void AppendSubMeshTriangles(Mesh mesh, int subMesh, int vertexCount, List<RawMeshData.Triangle> triangles, List<uint> triangleIndices)
        {
#if UNITY_6000_3_OR_NEWER
            bool hasMeshLods = mesh.lodCount > 1;

            int[] indices = hasMeshLods ? mesh.GetIndices(subMesh, 0, true) : mesh.GetIndices(subMesh, true);
            uint indexStart = hasMeshLods ? mesh.GetIndexStart(subMesh, 0) : mesh.GetIndexStart(subMesh);
            uint indexCount = hasMeshLods ? mesh.GetIndexCount(subMesh, 0) : mesh.GetIndexCount(subMesh);
#else
            int[] indices = mesh.GetIndices(subMesh, true);
            uint indexStart = mesh.GetIndexStart(subMesh);
            uint indexCount = mesh.GetIndexCount(subMesh);
#endif

            int usableIndices = Mathf.Min(indices.Length, (int)indexCount);
            uint firstTriangle = indexStart / 3;

            for (int i = 0; i + 2 < usableIndices; i += 3)
            {
                int a = indices[i + 0];
                int b = indices[i + 1];
                int c = indices[i + 2];

                if ((uint)a >= (uint)vertexCount || (uint)b >= (uint)vertexCount || (uint)c >= (uint)vertexCount) continue;

                triangles.Add(new RawMeshData.Triangle() { a = (uint)a, b = (uint)b, c = (uint)c });
                triangleIndices.Add(firstTriangle + (uint)(i / 3));
            }
        }

        /// <summary>
        /// Compute interpolated vertices with triangle index and barycentric coordinates
        /// </summary>
        public static RawMeshData.Vertex GetInterpolatedVertex(RawMeshData meshData, BarycentricTriangleSampling sampling)
        {
            var triangle = meshData.triangles[sampling.index];
            var u = sampling.coord.x;
            var v = sampling.coord.y;
            var w = 1.0f - u - v;

            var A = meshData.vertices[triangle.a];
            var B = meshData.vertices[triangle.b];
            var C = meshData.vertices[triangle.c];

            var r = u * A + v * B + w * C;

            r.normal = r.normal.normalized;
            var tangent = new Vector3(r.tangent.x, r.tangent.y, r.tangent.z).normalized;
            r.tangent = new Vector4(tangent.x, tangent.y, tangent.z, r.tangent.w > 0.0f ? 1.0f : -1.0f);

            return r;
        }

        /// <summary>
        /// Return a new uniform sampled position using the accumulated triangle area
        /// </summary>
        public static BarycentricTriangleSampling GetNextSampling(RawMeshData meshData, System.Random rand)
        {
            if (meshData.accumulatedTriangleArea.Length < 1) return new BarycentricTriangleSampling();

            var areaPosition = rand.NextDouble() * meshData.accumulatedTriangleArea.Last();
            uint areaIndex = FindIndexOfArea(meshData, areaPosition);

            var randUV = new Vector2((float)rand.NextDouble(), (float)rand.NextDouble());

            //http://inis.jinr.ru/sl/vol1/CMC/Graphics_Gems_1,ed_A.Glassner.pdf
            //p24 uniform distribution from two numbers in triangle generating barycentric coordinate
            //Alternatively, we can use "A Low-Distortion Map Between Triangle and Square" https://hal.archives-ouvertes.fr/hal-02073696v1/document
            float s = randUV.x;
            float t = Mathf.Sqrt(randUV.y);
            float u = 1.0f - t;
            float v = (1 - s) * t;
            float w = s * t; //Not stored, recomputed using 1 - u - v

            return new BarycentricTriangleSampling
            {
                coord = new Vector2(u, v),
                index = areaIndex
            };
        }

        static double ComputeTriangleArea(RawMeshData meshData, uint triangleIndex)
        {
            var t = meshData.triangles[triangleIndex];
            var A = meshData.vertices[t.a].position;
            var B = meshData.vertices[t.b].position;
            var C = meshData.vertices[t.c].position;
            return 0.5f * Vector3.Cross(B - A, C - A).magnitude;
        }

        static uint FindIndexOfArea(RawMeshData meshData, double area)
        {
            var accumulatedArea = meshData.accumulatedTriangleArea;

            int min = 0;
            int max = accumulatedArea.Length - 1;

            // Lower bound: first triangle whose accumulated area reaches the sampled position.
            while (min < max)
            {
                int mid = min + ((max - min) >> 1);
                if (accumulatedArea[mid] < area) min = mid + 1;
                else max = mid;
            }

            return (uint)min;
        }
    }
}