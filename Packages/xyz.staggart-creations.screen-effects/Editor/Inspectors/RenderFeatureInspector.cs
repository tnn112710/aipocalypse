// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using System;
using System.Collections.Generic;
using sc.screeneffects.runtime;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(PostEffectFeature), true)]
    [NoAutoStaticsCleanup]
    public partial class RenderFeatureInspector : Editor
    {
        PostEffectFeature feature;
        [AutoStaticsCleanup]
        private static List<UniversalRenderPipelineAsset> unsupportedRenderers;
        
        public override void OnInspectorGUI()
        {
            EditorGUILayout.Separator();
            
            UI.DrawComponentHeader<PostEffectFeature>(false);
            
            base.OnInspectorGUI();
        
            feature = target as PostEffectFeature;

            if (feature.settings.renderingLayerMask > 0)
            {
                unsupportedRenderers = Utilities.GetPipelineAssetsWithRenderingLayersDisabled();
                if (feature.settings.renderingLayerMask > 0 && unsupportedRenderers.Count > 0)
                {
                    UI.DrawRenderingLayerSetupError();
                }
            }
            UI.DrawFooter();
        }
        
        public static void OpenPopup(ScriptableRendererFeature instance)
        {
            PopupWindow.OpenWindow(instance);
        }

        private class PopupWindow : EditorWindow
        {
            private const float width = 400f;
            private const float height = 225f;
            
            private Vector2 scrollPos;
            private Editor editor;

            [SerializeField]
            private ScriptableRendererFeature instance;
            
            public static void OpenWindow(ScriptableRendererFeature instance)
            {
                var currentWindow = GetWindow<PopupWindow>();
                if(currentWindow) currentWindow.Close();
                
                var window = PopupWindow.CreateWindow<PopupWindow>();
                window.titleContent = new GUIContent("Edit Render Feature");

                window.minSize = new Vector2(width, height);
                window.maxSize = new Vector2(width, 800);
                
                Vector2 mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                window.position = new Rect(mousePos.x - (width * 0.5f), mousePos.y - (height * 0.5f), 0, 0);
                
                if(!instance) Debug.LogError("No instance provided");
                
                window.instance = instance;
                window.ShowUtility();
            }

            private void OnGUI()
            {
                if(!editor) editor = Editor.CreateEditor(instance);
                editor.OnInspectorGUI();
            }
        }
    }
}