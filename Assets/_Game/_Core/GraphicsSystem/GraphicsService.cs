using System;
using System.Collections.Generic;
using sc.stylizedgrass.runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aipocalypse.Core.GraphicsSystem
{
    // Root service, sole runtime writer of the URP asset's shadow distance: the player's setting, shortened by owner-keyed
    // requests (shortest wins). The asset is saved with the project, so its own value is restored on Dispose.
    // Also owns the player's graphics toggles that are global shader keywords (star twinkle), and keeps Stylized Grass
    // bending running for game cameras.
    public sealed class GraphicsService : IDisposable
    {
        // SkyStarMap.shader compiles its twinkle out without it, so low-end PCs pay nothing when it is off.
        public const string StarTwinkleKeyword = "STAR_TWINKLE";

        readonly Dictionary<object, float> _shadowRequests = new();
        readonly GlobalKeyword _starTwinkleKeyword = GlobalKeyword.Create(StarTwinkleKeyword);
        readonly List<(ScriptableRendererFeature Feature, HideFlags Flags)> _grassFeatures = new();
        UniversalRenderPipelineAsset _pipeline;
        float _savedShadowDistance;
        float _shadowDistance = -1f;
        bool _starTwinkle;

        public GraphicsService()
        {
            StarTwinkle = true;
            ShowGrassFeatures();
            RenderPipelineManager.beginContextRendering += ShowGrassFeatures;
        }

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
            RenderPipelineManager.beginContextRendering -= ShowGrassFeatures;
            foreach ((ScriptableRendererFeature feature, HideFlags flags) in _grassFeatures)
                if (feature != null)
                    feature.hideFlags = flags;
            _grassFeatures.Clear();
        }

        void ShowGrassFeatures(ScriptableRenderContext context, List<Camera> cameras) => ShowGrassFeatures();

        // Stylized Grass 2.1.3 skips every non-Scene-view camera while its render feature has hide flags, and URP 17.6
        // saves renderer features HideInHierarchy, so grass would only bend in the Scene view. Re-checked every frame
        // because URP's OnValidate puts the flags back in the editor.
        void ShowGrassFeatures()
        {
            UniversalRenderPipelineAsset pipeline = Pipeline;
            if (pipeline == null)
                return;
            foreach (ScriptableRendererData data in pipeline.rendererDataList)
            {
                if (data == null)
                    continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature is not GrassRenderFeature || feature.hideFlags == HideFlags.None)
                        continue;
                    if (!_grassFeatures.Exists(f => f.Feature == feature))
                        _grassFeatures.Add((feature, feature.hideFlags));
                    feature.hideFlags = HideFlags.None;
                }
            }
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
