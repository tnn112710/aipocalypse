using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Aipocalypse.Core.GraphicsSystem
{
    // Root service, sole runtime writer of the shared URP asset's shadow distance. ShadowDistance is the player's setting (a
    // graphics menu later; the asset's own value until then); systems request a shorter one keyed by owner (the environment's
    // night distance) and the shortest wins. The asset is saved with the project, so its own value comes back on Dispose.
    public sealed class GraphicsService : IDisposable
    {
        readonly Dictionary<object, float> _shadowRequests = new();
        UniversalRenderPipelineAsset _pipeline;
        float _savedShadowDistance;
        float _shadowDistance = -1f;

        public float ShadowDistance
        {
            get => _shadowDistance >= 0f ? _shadowDistance : Pipeline != null ? _savedShadowDistance : 0f;
            set
            {
                _shadowDistance = Mathf.Max(0f, value);
                ApplyShadowDistance();
            }
        }

        UniversalRenderPipelineAsset Pipeline
        {
            get
            {
                if (_pipeline == null && UniversalRenderPipeline.asset != null)
                {
                    _pipeline = UniversalRenderPipeline.asset;
                    _savedShadowDistance = _pipeline.shadowDistance;
                }
                return _pipeline;
            }
        }

        public void RequestShadowDistance(object owner, float distance)
        {
            if (owner == null)
                return;
            _shadowRequests[owner] = Mathf.Max(0f, distance);
            ApplyShadowDistance();
        }

        public void ReleaseShadowDistance(object owner)
        {
            if (owner != null && _shadowRequests.Remove(owner))
                ApplyShadowDistance();
        }

        public void Dispose()
        {
            _shadowRequests.Clear();
            if (_pipeline != null)
                _pipeline.shadowDistance = _savedShadowDistance;
        }

        void ApplyShadowDistance()
        {
            UniversalRenderPipelineAsset pipeline = Pipeline;
            if (pipeline == null)
                return;
            float distance = ShadowDistance;
            foreach (float request in _shadowRequests.Values)
                distance = Mathf.Min(distance, request);
            if (pipeline.shadowDistance != distance)
                pipeline.shadowDistance = distance;
        }
    }
}
