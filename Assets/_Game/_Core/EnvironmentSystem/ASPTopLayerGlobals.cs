using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Sets the global multipliers the ANGRY MESH Stylized Pack shaders apply to every material's top layer (grass caps).
    // Unset they are 0 and no top layer shows; the pack's ASPGlobalTopLayer would set them from Update every frame.
    [ExecuteAlways]
    public sealed class ASPTopLayerGlobals : MonoBehaviour
    {
        static readonly int IntensityId = Shader.PropertyToID("ASPP_TopLayerIntensity");
        static readonly int OffsetId = Shader.PropertyToID("ASPP_TopLayerOffset");
        static readonly int ContrastId = Shader.PropertyToID("ASPP_TopLayerContrast");
        static readonly int HeightStartId = Shader.PropertyToID("ASPT_TopLayerHeightStart");
        static readonly int HeightFadeId = Shader.PropertyToID("ASPT_TopLayerHeightFade");

        [SerializeField, Range(0f, 1f)] float _intensity = 1f;
        [SerializeField, Range(0f, 2f)] float _offset = 1f;
        [SerializeField, Range(0f, 2f)] float _contrast = 1f;
        [SerializeField, Tooltip("World height where the top layer starts; far below the terrain = everywhere")] float _heightStart = -5000f;
        [SerializeField, Min(0.01f)] float _heightFade = 1f;

        void OnEnable() => Apply();

        void OnValidate() => Apply();

        void Apply()
        {
            Shader.SetGlobalFloat(IntensityId, _intensity);
            Shader.SetGlobalFloat(OffsetId, _offset);
            Shader.SetGlobalFloat(ContrastId, _contrast);
            Shader.SetGlobalFloat(HeightStartId, _heightStart);
            Shader.SetGlobalFloat(HeightFadeId, _heightFade);
        }
    }
}
