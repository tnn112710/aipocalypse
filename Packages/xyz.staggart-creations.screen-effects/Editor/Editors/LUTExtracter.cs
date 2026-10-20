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
using UnityEngine;
using sc.screeneffects.runtime;
using UnityEditor;

namespace sc.screeneffects.editor
{
    public class LUTExtracter : Editor
    {
        //Cached in- and outputs
        public static string InputPath
        {
            get { return EditorPrefs.GetString("LUT_INPUT_PATH", ""); }
            set { EditorPrefs.SetString("LUT_INPUT_PATH", value); }
        }

        public static string OutputPath
        {
            get { return EditorPrefs.GetString("LUT_OUTPUT_PATH", ""); }
            set { EditorPrefs.SetString("LUT_OUTPUT_PATH", value); }
        }

        public static void ExtractLUT(Texture2D screenshot, Texture2D targetLUT)
        {
            if (!screenshot || !targetLUT) return;
            
            Color[] texels = screenshot.GetPixels(0, 0, targetLUT.width, targetLUT.height);

            //Create new LUT
            Texture2D newLUT = new Texture2D(targetLUT.width, targetLUT.height, TextureFormat.RGBA32, false, true);

            newLUT.SetPixels(texels);
            newLUT.Apply();

            byte[] bytes = newLUT.EncodeToPNG();

            //Save new LUT
            string filePath = AssetDatabase.GetAssetPath(targetLUT);
            System.IO.File.WriteAllBytes(filePath, bytes);

            //AssetDatabase.Refresh();
            //AssetDatabase.SaveAssets();
            newLUT = (Texture2D)AssetDatabase.LoadAssetAtPath(filePath, typeof(Texture2D));
            EditorUtility.CopySerialized(newLUT, targetLUT);
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
        }
    }
}