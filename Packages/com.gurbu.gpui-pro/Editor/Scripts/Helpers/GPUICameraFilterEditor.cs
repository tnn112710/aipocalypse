// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using UnityEditor;
using UnityEngine;

namespace GPUInstancerPro
{
    [CustomEditor(typeof(GPUICameraFilter))]
    public class GPUICameraFilterEditor : GPUIEditor
    {
        public override string GetTitleText()
        {
            return "GPUI Camera Filter";
        }

        public override string GetWikiURLParams()
        {
            return "title=GPU_Instancer_Pro:GettingStarted#GPUI_Camera_Filter";
        }

        [MenuItem("Tools/GPU Instancer Pro/Utilities/Add Camera Filter", validate = false, priority = 195)]
        public static GPUICameraFilter ToolbarAddCameraFilter()
        {
            GameObject go = new GameObject("GPUI Camera Filter");
            GPUICameraFilter cameraFilter = go.AddComponent<GPUICameraFilter>();

            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Add Camera Filter");

            return cameraFilter;
        }
    }
}
