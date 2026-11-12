//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Handles FX Profile playing
    /// </summary>
    public class CozyVFXProvider : IVFXProvider, IVFXBlockingProvider
    {
        public RenderTexture FXBlockingTexture => fxBlockingTexture;
        private RenderTexture fxBlockingTexture;
        private Camera fxBlockingCamera;

        private Transform fxParent;
        public Transform FXParent => fxParent;

        public void Initialize()
        {
            if (fxParent == null)
                fxParent = new GameObject("COZY FX").transform;

            if (!CozyPreferences.RenderFXBlocking) return;

            fxBlockingTexture = Resources.Load("COZY Resources/FX Blocking Texture") as RenderTexture;

            if (fxBlockingTexture == null)
            {
                fxBlockingTexture = new RenderTexture(CozyPreferences.FXBlockingResolution, CozyPreferences.FXBlockingResolution, 1, RenderTextureFormat.Depth)
                {
                    name = "COZY_FXBlocking",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            if (fxBlockingCamera == null)
            {
                if (CozyPreferences.FXBlockingRendererIndex == -1)
                    return;

                GameObject camObj = new GameObject("FX Blocking Camera");
                camObj.transform.SetParent(fxParent);
                camObj.transform.localPosition = new Vector3(0, CozyPreferences.FXRenderHeight * 0.5f, 0);
                camObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                fxBlockingCamera = camObj.AddComponent<Camera>();
                camObj.AddComponent<CozyCameraRendering>();
                UniversalAdditionalCameraData additionalCameraData = camObj.AddComponent<UniversalAdditionalCameraData>();
                fxBlockingCamera.cullingMask = CozyPreferences.FXBlockLayerMask;
                fxBlockingCamera.clearFlags = CameraClearFlags.SolidColor;
                fxBlockingCamera.backgroundColor = Color.white;
                fxBlockingCamera.orthographic = true;
                fxBlockingCamera.orthographicSize = CozyPreferences.FXRenderDistance * 0.5f;
                fxBlockingCamera.nearClipPlane = 0.1f;
                fxBlockingCamera.farClipPlane = CozyPreferences.FXRenderHeight;
                fxBlockingCamera.depthTextureMode = DepthTextureMode.Depth;
                fxBlockingCamera.targetTexture = fxBlockingTexture;

                if (GraphicsSettings.currentRenderPipeline != null && (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset).GetRenderer(CozyPreferences.FXBlockingRendererIndex) != null)
                    additionalCameraData.SetRenderer(CozyPreferences.FXBlockingRendererIndex);
                additionalCameraData.requiresColorOption = CameraOverrideOption.Off;
                additionalCameraData.requiresDepthOption = CameraOverrideOption.On;
                additionalCameraData.renderShadows = false;
                additionalCameraData.renderPostProcessing = false;
            }

        }

        public void Deinitialize()
        {
            if (Application.isPlaying)
            {
                if (fxParent)
                    Object.Destroy(fxParent.gameObject);

                if (fxBlockingTexture != null && fxBlockingTexture.IsCreated())
                {
                    fxBlockingTexture.Release();
                    Shader.SetGlobalTexture("CZY_FXBlockDepthTex", Texture2D.blackTexture);
                }
            }
        }

        public void Center()
        {
            if (Application.isPlaying && fxParent == null) Initialize();

            fxParent.position = CozyTrackPoint.Position;
        }

        public void UpdateVFXBlocking()
        {
            // Early exit if not enabled
            if (!Application.isPlaying || !CozyPreferences.RenderFXBlocking)
            {
                return;
            }

            if (fxBlockingCamera == null || fxBlockingTexture == null) Initialize();

            if (fxBlockingTexture != null)
                Shader.SetGlobalTexture("CZY_FXBlockDepthTex", fxBlockingTexture);
            else
                Shader.SetGlobalTexture("CZY_FXBlockDepthTex", Texture2D.blackTexture);
            Shader.SetGlobalFloat("CZY_DepthTextureScale", CozyPreferences.FXRenderDistance / 2);
            Shader.SetGlobalFloat("CZY_DepthTextureHeight", CozyPreferences.FXRenderHeight / 2);
            Shader.SetGlobalFloat("CZY_RenderDistance", CozyPreferences.FXRenderDistance / 2);
        }

        public bool GetFXParent<T>(out Transform parent)
        {
            parent = null;

            if (fxParent == null)
            {
                Initialize();
            }

            for (int i = 0; i < fxParent.childCount; i++)
            {
                if (fxParent.GetChild(i).name == typeof(T).Name)
                {
                    parent = fxParent.GetChild(i);
                    return true;
                }
            }

            Transform newParent = new GameObject(typeof(T).Name).transform;
            newParent.parent = fxParent;
            newParent.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            parent = newParent;

            return true;
        }

        public T GetFXRuntimeRef<T>(string name) where T : Component
        {
            if (this == null)
                return null;

            if (GetFXParent<T>(out Transform parent))
            {
                foreach (var component in parent.GetComponentsInChildren<T>())
                {
                    if (component != null && component.transform.name == name)
                        return component;
                }
            }

            return null;
        }
    }
}