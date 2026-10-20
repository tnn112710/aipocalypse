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

using System.Collections;
using System.Collections.Generic;
using sc.screeneffects.runtime;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace sc.screeneffects.editor
{
    public partial class LUTExtracterWindow : EditorWindow
    {
        private readonly bool useAssetInput = true;

        [AutoStaticsCleanup]
        public static Texture2D inputTexture;
        [AutoStaticsCleanup]
        public static Texture2D OutputLUT;

        private const int width = 275;
        private const int height = 250;

        public static void ShowWindow()
        {
            EditorWindow editorWindow = GetWindow<LUTExtracterWindow>(false, " LUT Extracter", true);
            
            editorWindow.autoRepaintOnSceneChange = true;

            //Open cursor position
            Vector2 mousePosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            editorWindow.position = new Rect(mousePosition.x - (width * 1.5f), mousePosition.y - (height / 2f), width, height);

            editorWindow.minSize = new Vector2(width, height);
            editorWindow.maxSize = new Vector2(width, height);

            editorWindow.Show();
        }

        private void OnEnable()
        {
            OutputLUT = (Texture2D)AssetDatabase.LoadAssetAtPath(LUTExtracter.OutputPath, typeof(Texture2D));
            inputTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(LUTExtracter.InputPath, typeof(Texture2D));
        }

        private void OnDisable()
        {
            //Save paths of current texture fields
            LUTExtracter.InputPath = AssetDatabase.GetAssetPath(inputTexture);
            LUTExtracter.OutputPath = AssetDatabase.GetAssetPath(OutputLUT);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Capture scene screenshot", null,
                    "Captures a 720p screenshot from the scene-view and inserts a neutral LUT in the bottom-left corner\n\n" +
                    "This file is saved to the Assets folder and automatically assigned as the input screenshot below\n\n" +
                    "You can make any color adjustments to this screenshot. Once saved, the now modified LUT will be extracted and copied into the target LUT file")))
                {
                    LUTUtilities.CaptureScreenshotWithLUT();
                }
                GUILayout.FlexibleSpace();

            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {

                GUILayout.Label("Input screenshot (8-bit depth)", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (useAssetInput)
                    {
                        inputTexture = (Texture2D)EditorGUILayout.ObjectField(inputTexture, typeof(Texture2D), true);
                    }
                    /*
                    else
                    {
                        EditorGUILayout.TextField(LUTExtracter.InputPath, SCPE.SCPE_GUI.PathField);
                        if (GUILayout.Button("...", GUILayout.Width(50f)))
                        {
                            LUTExtracter.InputPath = EditorUtility.OpenFilePanel("Screenshot destination folder", LUTExtracter.InputPath, "");
                        }
                    }
                    */
                    if (GUILayout.Button("Open", GUILayout.Width(50f)))
                    {
                        if (inputTexture) AssetDatabase.OpenAsset(inputTexture);
                    }
                }

                ValidateImportSettings(inputTexture, false);
                EditorGUILayout.Space();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string lutLabel = (OutputLUT != null) ? " (" + OutputLUT.width + "x" + OutputLUT.height + ")" : string.Empty;
                GUILayout.Label("Output LUT" + lutLabel, EditorStyles.boldLabel);
                OutputLUT = (Texture2D)EditorGUILayout.ObjectField(OutputLUT, typeof(Texture2D), true);

                ValidateImportSettings(OutputLUT, true);
                EditorGUILayout.Space();
            }

            if (inputTexture == null || OutputLUT == null) EditorGUILayout.HelpBox("Empty input or output", MessageType.Warning);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledGroupScope(inputTexture == null || OutputLUT == null))
                {
                    if (GUILayout.Button(new GUIContent(" Extract", EditorGUIUtility.IconContent("LookDevObjRotation").image), GUILayout.Width(150f)))
                    {
                        LUTExtracter.ExtractLUT(inputTexture, OutputLUT);
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        public static bool ValidateImportSettings(Texture2D tex, bool isLUT)
        {
            if (!tex) return false;

            bool valid = false;

            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));

            if (importer != null) // Fails when using a non-persistent texture
            {
                valid = isLUT ? LUTUtilities.IsValidLUT(importer) : LUTUtilities.IsValidScreenshot(importer);

                if (!valid)
                {
                    EditorGUILayout.HelpBox("Invalid import settings.", MessageType.Warning);

                    GUILayout.Space(-32);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Fix", GUILayout.Width(60)))
                        {
                            LUTUtilities.SetSSImportSettings(importer);
                            valid = true;
                            AssetDatabase.Refresh();
                        }
                        GUILayout.Space(8);
                    }
                    GUILayout.Space(11);
                }
            }

            return valid;
        }
    }
}