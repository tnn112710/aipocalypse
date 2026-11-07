using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DistantLands.Cozy
{
    public static class FlareOcclusion
    {
        public static Camera TargetCamera;
        const int PatchSize = 8;

        class Entry { public Vector2 uv; public float visibility = 1f; public bool inFlight; public int lastFrame; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => s_Entries.Clear();

        static readonly Dictionary<EntityId, Entry> s_Entries = new();
        static readonly List<EntityId> s_Stale = new();

        public static float GetFlareOcclusion(Vector3 screenPoint, EntityId key)
        {
            if (TargetCamera == null || !SystemInfo.supportsAsyncGPUReadback)
            {
                return 1f;
            }
            if (screenPoint.z <= 0f)
            {
                return 0f;
            }

            Entry e;
            if (!s_Entries.TryGetValue(key, out e))
            {
                e = new Entry();
                s_Entries.Add(key, e);
            }

            e.lastFrame = Time.frameCount;
            e.uv = new Vector2(screenPoint.x / TargetCamera.pixelWidth,
                               screenPoint.y / TargetCamera.pixelHeight);
            return e.visibility;
        }

        internal static void IssueReadbacks(CommandBuffer cmd, Texture tex)
        {
            s_Stale.Clear();
            foreach (var kv in s_Entries)
                if (Time.frameCount - kv.Value.lastFrame > 5) s_Stale.Add(kv.Key);
            foreach (EntityId k in s_Stale) s_Entries.Remove(k);

            foreach (var e in s_Entries.Values)
            {
                if (e.inFlight) continue;
                int x = Mathf.Clamp((int)(e.uv.x * tex.width) - PatchSize / 2, 0, tex.width - PatchSize);
                int y = Mathf.Clamp((int)(e.uv.y * tex.height) - PatchSize / 2, 0, tex.height - PatchSize);

                e.inFlight = true;
                var entry = e;
                cmd.RequestAsyncReadback(tex, 0, x, PatchSize, y, PatchSize, 0, 1,
                    GraphicsFormat.R32G32B32A32_SFloat, req =>
                    {
                        entry.inFlight = false;
                        if (req.hasError) return;
                        var px = req.GetData<Vector4>();
                        float sum = 0f;
                        for (int i = 0; i < px.Length; i++) sum += px[i].w;
                        float raw = 1f - Mathf.Clamp01(sum / px.Length);
                        float t = 1f - Mathf.Exp(-30.0f * Time.deltaTime);
                        entry.visibility = Mathf.Lerp(entry.visibility, raw, t);
                    });
            }
        }
    }
}