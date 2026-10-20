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
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Net;
using System.Reflection;
using sc.screeneffects.runtime;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace sc.screeneffects.editor
{
    public static class PostEffectsEditor
    {
        [MenuItem("CONTEXT/UniversalRendererData/Delete All Post Effects")]
        public static void DeleteAllEffects(MenuCommand cmd)
        {
            UniversalRendererData renderer = (UniversalRendererData)cmd.context;

            int removed = 0;
            foreach (var feature in renderer.rendererFeatures.ToList())
            {
                if (feature is PostEffectFeature)
                {
                    renderer.rendererFeatures.Remove(feature);

                    removed++;
                }
            }
            
            MethodInfo validateInfo = typeof(ScriptableRendererData).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            validateInfo.Invoke(renderer, null);
            
            string path = AssetDatabase.GetAssetPath(renderer);

            //Also clear any render features with no script attached
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object subAsset in subAssets)
            {
                if (subAsset)
                {
                    //if (AssetDatabase.IsSubAsset(subAsset))
                    {
                        SerializedObject so = new SerializedObject(subAsset);
                        SerializedProperty scriptProp = so.FindProperty("m_Script");
                        if (scriptProp != null && scriptProp.objectReferenceValue == null)
                        {
                            AssetDatabase.RemoveObjectFromAsset(subAsset);
                            removed++;
                        }
                    }
                }
            }
            
            if(removed > 0) EditorUtility.SetDirty(renderer);
        }
    }

    public class StackEditor : EditorWindow
    {
        [MenuItem("CONTEXT/UniversalRendererData/Edit Post Effects Stack")]
        public static void EditRenderer(MenuCommand cmd)
        {
            UniversalRendererData renderer = (UniversalRendererData)cmd.context;
            StackEditor window = CreateWindow<StackEditor>();
            window.Initialize(renderer);
            window.ShowUtility();
        }

        [SerializeField]
        private UniversalRendererData renderer;
        
        private ReorderableList effectsList;
        private Editor rendererEditor;
        private SerializedObject serializedObject;
        private SerializedProperty effects;

        private void OnFocus()
        {
            //Initialize(renderer);
        }

        private void Initialize(UniversalRendererData m_renderer)
        {
            if (!m_renderer) throw new Exception("No renderer selected");

            this.renderer = m_renderer;
            
            rendererEditor = Editor.CreateEditor(renderer);

            serializedObject = rendererEditor.serializedObject;
            
            effects = serializedObject.FindProperty("m_RendererFeatures");

            effectsList = new ReorderableList(serializedObject, effects);
            
            effectsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Post Effects");
            effectsList.drawElementCallback = DrawElementCallback;
            effectsList.headerHeight = 0;
            effectsList.displayAdd = false;
            effectsList.displayRemove = true;
            effectsList.draggable = true;
            effectsList.elementHeight = 20;
            effectsList.onRemoveCallback = OnRemoveCallback;
            effectsList.onChangedCallback = OnChangedCallback;
        }

        private void OnChangedCallback(ReorderableList list)
        {
            ValidateRenderFeatures();
        }

        private void OnRemoveCallback(ReorderableList list)
        {
            int index = list.index;
            effects.DeleteArrayElementAtIndex(index);
        }

        private void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty effect = effects.GetArrayElementAtIndex(index);
            if (effect == null)
                return;
            
            ScriptableRendererFeature rendererFeature = effect.objectReferenceValue as ScriptableRendererFeature;
            SerializedProperty nameProp = effect.FindPropertyRelative("name");
            SerializedProperty enabledProp = effect.FindPropertyRelative("m_Active");

            rect.height = EditorGUIUtility.singleLineHeight;
            rect.y += 1;
            
            Rect toggleRect = new Rect(rect.x, rect.y, 15, rect.height);
            rect.x += 15;

            //EditorGUI.PropertyField(toggleRect, enabledProp);
            
            if (nameProp == null)
            {
                GUI.Label(rect, rendererFeature.name);
                return;
            }
            
            GUI.Label(rect, nameProp.stringValue);
        }

        private void OnGUI()
        {
            if (!renderer)
            {
                EditorGUILayout.HelpBox("No renderer selected", MessageType.Error);
                return;
            }

            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.ObjectField("Editing:", renderer, typeof(UniversalRendererData), true);
            }
            
            serializedObject.Update();
            
            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.LabelField("Post Processing Stack", EditorStyles.boldLabel);
            effectsList.DoLayoutList();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                
            }
        }

        private void ValidateRenderFeatures()
        {
            //Onvalidate will call ValidateRendererFeatures and update m_RendererPassMap
            MethodInfo validateInfo = typeof(ScriptableRendererData).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            validateInfo.Invoke(renderer, null);
        }
    }
}//namespace