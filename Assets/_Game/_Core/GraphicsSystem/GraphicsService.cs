using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aipocalypse.Core.GraphicsSystem
{
    // Root service, sole runtime writer of the URP asset's shadow distance: the player's setting, shortened by owner-keyed
    // requests (shortest wins). The asset is saved with the project, so its own value is restored on Dispose.
    // Also owns the player's graphics toggles that are global shader keywords (star twinkle).
    public sealed class GraphicsService : IDisposable
    {
        // SkyStarMap.shader compiles its twinkle out without it, so low-end PCs pay nothing when it is off.
        public const string StarTwinkleKeyword = "STAR_TWINKLE";

        readonly Dictionary<object, float> _shadowRequests = new();
        readonly GlobalKeyword _starTwinkleKeyword = GlobalKeyword.Create(StarTwinkleKeyword);
        UniversalRenderPipelineAsset _pipeline;
        float _savedShadowDistance;
        float _shadowDistance = -1f;
        bool _starTwinkle;

        public GraphicsService() => StarTwinkle = true;

        public bool StarTwinkle
        {
            get => _starTwinkle;
            set
            {
                _starTwinkle = value;
                Shader.SetKeyword(_starTwinkleKeyword, value);
            }
        }

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
            Shader.SetKeyword(_starTwinkleKeyword, false);
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
