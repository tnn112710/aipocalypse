using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.Ambience.Legacy
{
#if USING_LEGACY

    using UnityEngine.Rendering.PostProcessing;

    public class PostProcessingSetupEditor
    {
        private AmbienceModule editor;
        private PostProcessingSetup setup;

        public PostProcessingSetupEditor(AmbienceModule editor)
        {
            this.editor = editor;

            Camera camera = GameObjectUtils.GetMainCamera();

            setup = ScriptableObject.CreateInstance<PostProcessingSetup>();

            setup.camera = camera;

            if( setup.camera)
            {
                setup.postProcessLayer = camera.GetComponent<PostProcessLayer>();
                setup.postProcessVolume = Object.FindObjectOfType<PostProcessVolume>();

                if (setup.postProcessVolume != null)
                {
                    setup.postProcessProfile = setup.postProcessVolume.profile;
                }
            }
        }


        public void OnInspectorGUI()
        {
            setup.camera = EditorGUILayout.ObjectField("Camera", setup.camera, typeof(Camera), true) as Camera;
            if(!setup.camera)
            {
                EditorGUILayout.HelpBox("Camera required", MessageType.Error);
            }

            if (setup.camera)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    setup.postProcessLayer = EditorGUILayout.ObjectField("PostProcess Layer", setup.postProcessLayer, typeof(PostProcessLayer), true) as PostProcessLayer;

                    if (!setup.postProcessLayer)
                    {
                        if (GUILayout.Button("Add", GUILayout.Width(60)))
                        {
                            PostProcessLayer postProcessLayer = setup.camera.gameObject.GetComponent<PostProcessLayer>();

                            if(postProcessLayer == null)
                            {
                                postProcessLayer = setup.camera.gameObject.AddComponent<PostProcessLayer>();

                                // temporal anti aliasing
                                postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.TemporalAntialiasing;
                                postProcessLayer.volumeLayer = int.MaxValue; // TODO: don't use everything

                            }

                            // assign
                            setup.postProcessLayer = postProcessLayer;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    setup.postProcessVolume = EditorGUILayout.ObjectField("PostProcess Volume", setup.postProcessVolume, typeof(PostProcessVolume), true) as PostProcessVolume;

                    if (!setup.postProcessVolume)
                    {
                        if (GUILayout.Button("Add", GUILayout.Width(60)))
                        {
                            GameObject go = new GameObject("PostProcess Volume");

                            PostProcessVolume volume = go.AddComponent<PostProcessVolume>();

                            volume.isGlobal = true;

                            // assign
                            setup.postProcessVolume = volume;

                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (setup.postProcessVolume)
                {
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Profile", GUIStyles.BoxTitleStyle);

                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUI.BeginChangeCheck();
                        {
                            setup.postProcessProfile = EditorGUILayout.ObjectField("PostProcess Profile", setup.postProcessProfile, typeof(PostProcessProfile), true) as PostProcessProfile;
                        }
                        if( EditorGUI.EndChangeCheck())
                        {
                            setup.postProcessVolume.sharedProfile = setup.postProcessProfile; // important to use sharedProfile, otherwise the profile will be reset when the user enters play mode

                            EditorUtility.SetDirty(setup.postProcessVolume);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
#else
    public class PostProcessingSetupEditor
    {
        public PostProcessingSetupEditor(AmbienceModule editor)
        {
        }
    }
#endif
}