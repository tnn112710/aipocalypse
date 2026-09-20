using System;
using System.Collections.Generic;
using UnityEngine;

namespace GPUInstancerPro
{
#if !UNITY_6000_3_0 && !GPUIPRO_NO_HELPURL
    [HelpURL("https://wiki.gurbu.com/index.php?title=GPU_Instancer_Pro:GettingStarted#GPUI_Camera_Filter")]
#endif
    [DefaultExecutionOrder(-2000)]
    [ExecuteInEditMode]
    public class GPUICameraFilter : MonoBehaviour
    {
        public GPUICameraIgnoreMode ignoreMode = GPUICameraIgnoreMode.Always;
        public GPUICameraTypeMask ignoredCameraTypes;
        public LayerMask ignoredCameraLayers;
        public List<Camera> ignoredCameras;

        private static bool HasCameraFilter;
        private static GPUICameraFilter Instance;

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(GPUIConstants.LOG_PREFIX + "Only one GPUICameraFilter can be active at a time.", this);

                enabled = false;
                return;
            }

            Instance = this;
            HasCameraFilter = true;

            ignoredCameras ??= new List<Camera>();
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
                HasCameraFilter = false;
            }
            else if (HasCameraFilter && Instance == null)
                HasCameraFilter = false;
        }

        public static bool IsCameraTypeIgnored(GPUICameraTypeMask ignoredCameraTypes, CameraType cameraType)
        {
            return (ignoredCameraTypes & (GPUICameraTypeMask)(1 << (int)cameraType)) != 0;
        }

        public static bool IsCameraIgnored(Camera camera, CameraType cameraType)
        {
            if (!HasCameraFilter
                || (Application.isPlaying && !Instance.IsIgnoreInPlayMode)
                || (!Application.isPlaying && !Instance.IsIgnoreInEditMode)
                )
                return false;
            return IsCameraTypeIgnored(Instance.ignoredCameraTypes, cameraType) || Instance.ignoredCameras.Contains(camera) || GPUIUtility.IsInLayer(Instance.ignoredCameraLayers, camera.gameObject.layer);
        }

        public static bool IsCameraIgnored(Camera camera)
        {
            return IsCameraIgnored(camera, camera.cameraType);
        }

        public bool IsIgnoreInEditMode => ignoreMode == GPUICameraIgnoreMode.Always || ignoreMode == GPUICameraIgnoreMode.EditMode;
        public bool IsIgnoreInPlayMode => ignoreMode == GPUICameraIgnoreMode.Always || ignoreMode == GPUICameraIgnoreMode.PlayMode;
    }

    public enum GPUICameraIgnoreMode
    {
        EditMode,
        PlayMode,
        Always
    }

    [Flags]
    public enum GPUICameraTypeMask
    {
        None = 0,
        Game = 1 << CameraType.Game,
        SceneView = 1 << CameraType.SceneView,
        Preview = 1 << CameraType.Preview,
        VR = 1 << CameraType.VR,
        Reflection = 1 << CameraType.Reflection,
        All = ~0
    }
}