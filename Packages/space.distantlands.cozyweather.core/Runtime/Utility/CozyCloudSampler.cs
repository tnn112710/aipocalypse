// Distant Lands | 2026
// COZY: Stylized Weather 4
// All code included in this file is protected under the Unity Asset Store Eula

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DistantLands.Cozy
{
    public struct CloudParams
    {
        public float bottom, top, invRange;
        public float uvScale1;
        public float uvScale2;
        public float uvScale4;
        public float2 wind1;
        public float2 wind2;
        public float coverage;
        public float nimbusCoverage;
        public float noiseScale;
        public float noiseAmount;
        public float densityScale;
        public float time;
        public bool useNimbus;
    }


    public struct CloudData
    {
        [ReadOnly] public NativeArray<float> mainR, mainG;
        public int mainW, mainH;

        [ReadOnly] public NativeArray<float> nimbusP;
        public int nimbusW, nimbusH;

        [ReadOnly] public NativeArray<float> rampLut;
        [ReadOnly] public NativeArray<float> nimbusRampLut;

        [ReadOnly] public NativeArray<byte> noise;
        public int3 noiseSize;
    }

    public static class CloudMath
    {
        const float Eps = 0.001f;

        public static float Density(in CloudParams p, in CloudData d, float3 pos)
        {
            if (pos.y < p.bottom || pos.y > p.top) return 0f;
            float h = (pos.y - p.bottom) * p.invRange;

            float hv = Lut(d.rampLut, h);

            float k = 0.0005f * (1f - math.sin(h * math.PI));
            float2 baseUV = pos.xz * 0.000005f + new float2(0.64f, 0.25f) * k;

            if (p.useNimbus)
            {
                float nRamp = Lut(d.nimbusRampLut, h);
                float nPlace = Bilinear(d.nimbusP, d.nimbusW, d.nimbusH, baseUV * p.uvScale4 + p.wind2);
                hv = math.lerp(hv, nRamp, math.saturate(-nPlace + 2f * p.nimbusCoverage));
            }

            float cH = p.coverage * hv;
            if (cH * 10f <= Eps) return 0f;

            float s1 = Bilinear(d.mainG, d.mainW, d.mainH, baseUV * p.uvScale1 + p.wind1);
            float s2 = Bilinear(d.mainR, d.mainW, d.mainH, baseUV * p.uvScale2 + p.wind2);

            float placement = (s1 + s2 - 2f + 2f * cH) * 5f;
            if (placement <= Eps) return 0f;

            float3 np = pos * p.noiseScale + new float3(p.time * 0.05f, p.time * 0.01f, 0f);
            float noise = (1f - Trilinear(d.noise, d.noiseSize, np)) * p.noiseAmount;

            return math.saturate(placement - noise) * p.densityScale;
        }

        static int Wrap(int i, int n)
        {
            int m = i % n;
            return m < 0 ? m + n : m;
        }

        static float Lut(NativeArray<float> lut, float t)
        {
            float f = math.saturate(t) * 255f;
            int i = (int)f;
            int j = math.min(i + 1, 255);
            return math.lerp(lut[i], lut[j], f - i);
        }

        static float Bilinear(NativeArray<float> a, int w, int h, float2 uv)
        {
            float x = uv.x * w - 0.5f;
            float y = uv.y * h - 0.5f;
            float fx = math.floor(x), fy = math.floor(y);
            float tx = x - fx, ty = y - fy;

            int x0 = Wrap((int)fx, w); int x1 = x0 + 1 == w ? 0 : x0 + 1;
            int y0 = Wrap((int)fy, h); int y1 = y0 + 1 == h ? 0 : y0 + 1;
            int r0 = y0 * w, r1 = y1 * w;

            return math.lerp(
                math.lerp(a[r0 + x0], a[r0 + x1], tx),
                math.lerp(a[r1 + x0], a[r1 + x1], tx),
                ty);
        }

        static float Trilinear(NativeArray<byte> a, int3 s, float3 uvw)
        {
            float3 p = uvw * (float3)s - 0.5f;
            float3 f = math.floor(p);
            float3 t = p - f;

            int x0 = Wrap((int)f.x, s.x); int x1 = x0 + 1 == s.x ? 0 : x0 + 1;
            int y0 = Wrap((int)f.y, s.y); int y1 = y0 + 1 == s.y ? 0 : y0 + 1;
            int z0 = Wrap((int)f.z, s.z); int z1 = z0 + 1 == s.z ? 0 : z0 + 1;

            int stride = s.x * s.y;
            int b0 = z0 * stride, b1 = z1 * stride;
            int r00 = b0 + y0 * s.x, r01 = b0 + y1 * s.x;
            int r10 = b1 + y0 * s.x, r11 = b1 + y1 * s.x;

            float c00 = math.lerp(a[r00 + x0], a[r00 + x1], t.x);
            float c01 = math.lerp(a[r01 + x0], a[r01 + x1], t.x);
            float c10 = math.lerp(a[r10 + x0], a[r10 + x1], t.x);
            float c11 = math.lerp(a[r11 + x0], a[r11 + x1], t.x);

            return math.lerp(math.lerp(c00, c01, t.y), math.lerp(c10, c11, t.y), t.z) * (1f / 255f);
        }
    }

    [BurstCompile]
    public struct CloudDensityJob : IJobParallelFor
    {
        public CloudParams parameters;
        public CloudData data;
        [ReadOnly] public NativeArray<float3> positions;
        [WriteOnly] public NativeArray<float> results;

        public void Execute(int i) => results[i] = CloudMath.Density(parameters, data, positions[i]);
    }

    public class CozyCloudSampler : MonoBehaviour
    {
        [Tooltip("Density above this counts as 'inside a cloud'.")]
        public float insideThreshold = 0.0001f;
        public bool useCumulonimbus = false;

        const int PlacementMip = 3;

        static readonly int MainTextureID = Shader.PropertyToID("CZY_Clouds_Volumetric_MainTexture");
        static readonly int RampID = Shader.PropertyToID("CZY_Clouds_Volumetric_Ramp");
        static readonly int NoiseID = Shader.PropertyToID("CZY_Clouds_Volumetric_NoiseTexture");
        static readonly int CumulonimbusMainTextureID = Shader.PropertyToID("CZY_Clouds_Cumulonimbus_MainTexture");
        static readonly int CumulonimbusRampID = Shader.PropertyToID("CZY_Clouds_Cumulonimbus_Ramp");

        static readonly int HeightID = Shader.PropertyToID("CZY_Clouds_Volumetric_Height");
        static readonly int PlacementHeightID = Shader.PropertyToID("CZY_Clouds_Volumetric_PlacementHeight");
        static readonly int WindStrengthID = Shader.PropertyToID("CZY_Clouds_Volumetric_WindStrength");
        static readonly int PlacementScale1ID = Shader.PropertyToID("CZY_Clouds_Volumetric_PlacementScale1");
        static readonly int PlacementScale2ID = Shader.PropertyToID("CZY_Clouds_Volumetric_PlacementScale2");
        static readonly int NoiseScaleID = Shader.PropertyToID("CZY_Clouds_Volumetric_NoiseScale");
        static readonly int NoiseAmountID = Shader.PropertyToID("CZY_Clouds_Volumetric_NoiseAmount");
        static readonly int DensityID = Shader.PropertyToID("CZY_Clouds_Volumetric_Density");
        static readonly int CumulusCoverageID = Shader.PropertyToID("CZY_Base_Weather_CumulusCoverage");
        static readonly int CumulonimbusCoverageID = Shader.PropertyToID("CZY_Base_Weather_CumulonimbusCoverage");
        static readonly int WindOffsetID = Shader.PropertyToID("CZY_Base_Weather_CloudWindOffset");

        private CloudParams cloudParams;
        private CloudData cloudData;
        private bool hasData;
        private int preparedFrame = -1;
        private bool warned;

        private Texture mainTex;
        private Texture rampTex;
        private Texture noiseTex;
        private Texture nimbusMainTex;
        private Texture nimbusRampTex;
        private bool builtWithNimbus;

        private void OnDisable()
        {
            DisposeData();
        }

        public bool IsInCloud(Vector3 worldPos)
        {
            return GetDensity(worldPos) > insideThreshold;
        }

        /// <summary>
        /// Gets the density of the volumetric clouds at a particular point.
        /// </summary>
        public float GetDensity(Vector3 worldPos)
        {
            if (!Prepare())
                return 0f;

            return CloudMath.Density(cloudParams, cloudData, worldPos);
        }

        private JobHandle activeJobs;

        public JobHandle ScheduleDensity(
            NativeArray<float3> positions,
            NativeArray<float> results,
            JobHandle dependency = default,
            int batchSize = 64)
        {
            if (!Prepare())
                throw new InvalidOperationException("Cloud density data is unavailable.");

            var combined = JobHandle.CombineDependencies(dependency, activeJobs);
            activeJobs = new CloudDensityJob
            {
                parameters = cloudParams,
                data = cloudData,
                positions = positions,
                results = results
            }.Schedule(positions.Length, batchSize, combined);

            return activeJobs;
        }

        bool Prepare()
        {
            if (preparedFrame == Time.frameCount) return hasData;
            preparedFrame = Time.frameCount;

            var main = Shader.GetGlobalTexture(MainTextureID);
            var ramp = Shader.GetGlobalTexture(RampID);
            var noise = Shader.GetGlobalTexture(NoiseID);
            var nMain = useCumulonimbus ? Shader.GetGlobalTexture(CumulonimbusMainTextureID) : null;
            var nRamp = useCumulonimbus ? Shader.GetGlobalTexture(CumulonimbusRampID) : null;

            bool changed = main != mainTex || ramp != rampTex || noise != noiseTex ||
                           nMain != nimbusMainTex || nRamp != nimbusRampTex;

            if (changed || !hasData)
            {
                if (changed) warned = false;
                mainTex = main; rampTex = ramp; noiseTex = noise;
                nimbusMainTex = nMain; nimbusRampTex = nRamp;
                Rebuild();
            }

            if (!hasData) return false;
            ReadParams();
            return true;
        }

        void ReadParams()
        {
            float height = Shader.GetGlobalFloat(HeightID);
            float place = Shader.GetGlobalFloat(PlacementHeightID);
            float windStrength = Shader.GetGlobalFloat(WindStrengthID);
            float scale1 = Shader.GetGlobalFloat(PlacementScale1ID);
            float scale2 = Shader.GetGlobalFloat(PlacementScale2ID);
            float cov = Shader.GetGlobalFloat(CumulusCoverageID);
            Vector4 w = Shader.GetGlobalVector(WindOffsetID);
            float2 wind = new float2(w.x, w.z) * windStrength;

            float oneMinus = (-0.1f + cov * 1.1f) - 1f; // Remap(cov, 0, 1, -0.1, 1) - 1

            cloudParams.bottom = place - height;
            cloudParams.top = place + height;
            cloudParams.invRange = 1f / (2f * height);
            cloudParams.uvScale1 = 5f * scale1;
            cloudParams.uvScale2 = 10f * scale2;
            cloudParams.uvScale4 = scale2;
            cloudParams.wind1 = wind * 0.0051f;
            cloudParams.wind2 = wind * 0.001f;
            cloudParams.coverage = 1.2f - oneMinus * oneMinus;
            cloudParams.nimbusCoverage = Shader.GetGlobalFloat(CumulonimbusCoverageID);
            cloudParams.noiseScale = 0.0002f * Shader.GetGlobalFloat(NoiseScaleID);
            cloudParams.noiseAmount = Shader.GetGlobalFloat(NoiseAmountID);
            cloudParams.densityScale = Shader.GetGlobalFloat(DensityID) * 0.01f;
            cloudParams.time = Time.timeSinceLevelLoad;
            cloudParams.useNimbus = builtWithNimbus;
        }

        void Rebuild()
        {
            DisposeData();

            var main = mainTex as Texture2D;
            var ramp = rampTex as Texture2D;
            var noise = noiseTex as Texture3D;
            if (main == null || ramp == null || noise == null) return;

            if (!main.isReadable || !ramp.isReadable || !noise.isReadable)
            {
                Warn("Cloud textures must have Read/Write enabled (main, ramp, noise).");
                return;
            }

            var nMain = nimbusMainTex as Texture2D;
            var nRamp = nimbusRampTex as Texture2D;
            builtWithNimbus = useCumulonimbus && nMain != null && nRamp != null &&
                               nMain.isReadable && nRamp.isReadable;

            // Placement texture: only mip 3 is ever sampled
            {
                int mip = Mathf.Min(PlacementMip, main.mipmapCount - 1);
                Color[] px = main.GetPixels(mip);
                cloudData.mainW = Mathf.Max(1, main.width >> mip);
                cloudData.mainH = Mathf.Max(1, main.height >> mip);
                cloudData.mainR = Channel(px, 0);
                cloudData.mainG = Channel(px, 1);
            }

            cloudData.rampLut = BuildLut(ramp);

            // Nimbus arrays are always allocated (1-element dummies when unused) so jobs never see
            // an uninitialized NativeArray.
            if (builtWithNimbus)
            {
                int mip = Mathf.Min(PlacementMip, nMain.mipmapCount - 1);
                Color[] px = nMain.GetPixels(mip);
                cloudData.nimbusW = Mathf.Max(1, nMain.width >> mip);
                cloudData.nimbusH = Mathf.Max(1, nMain.height >> mip);
                cloudData.nimbusP = Channel(px, 0);
                cloudData.nimbusRampLut = BuildLut(nRamp);
            }
            else
            {
                cloudData.nimbusW = cloudData.nimbusH = 1;
                cloudData.nimbusP = new NativeArray<float>(1, Allocator.Persistent);
                cloudData.nimbusRampLut = new NativeArray<float>(256, Allocator.Persistent);
            }

            // 3D noise: G channel only, quantized to 8-bit
            {
                Color[] px = noise.GetPixels();
                var arr = new NativeArray<byte>(px.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < px.Length; i++) arr[i] = (byte)(px[i].g * 255f + 0.5f);
                cloudData.noise = arr;
                cloudData.noiseSize = new int3(noise.width, noise.height, noise.depth);
            }

            hasData = true;
        }

        static NativeArray<float> Channel(Color[] px, int ch)
        {
            var arr = new NativeArray<float>(px.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < px.Length; i++) arr[i] = px[i][ch];
            return arr;
        }

        static NativeArray<float> BuildLut(Texture2D ramp)
        {
            var lut = new NativeArray<float>(256, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < 256; i++) lut[i] = ramp.GetPixelBilinear(i / 255f, 0f).r;
            return lut;
        }

        void DisposeData()
        {
            activeJobs.Complete();
            if (cloudData.mainR.IsCreated) cloudData.mainR.Dispose();
            if (cloudData.mainG.IsCreated) cloudData.mainG.Dispose();
            if (cloudData.nimbusP.IsCreated) cloudData.nimbusP.Dispose();
            if (cloudData.rampLut.IsCreated) cloudData.rampLut.Dispose();
            if (cloudData.nimbusRampLut.IsCreated) cloudData.nimbusRampLut.Dispose();
            if (cloudData.noise.IsCreated) cloudData.noise.Dispose();
            cloudData = default;
            hasData = false;
        }

        void Warn(string msg)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[CozyCloudSampler] " + msg, this);
        }
    }

}
