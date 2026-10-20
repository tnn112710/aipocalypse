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

using System.Collections.Generic;
using sc.screeneffects.runtime;
using UnityEditor;
using UnityEngine;

namespace sc.screeneffects.editor
{
    [FilePath("ProjectSettings/SC Screen Effects.asset", FilePathAttribute.Location.ProjectFolder)]
    public class PostProcessingProjectSettings : ScriptableSingleton<PostProcessingProjectSettings>
    {
        public const string ProjectSettingsPath = "Project/Graphics/SC Screen Effects";
        
        public bool includeAllShadersInBuild = true;

        public bool HasProjectSettingsSetup()
        {
            Object[] assets = PlayerSettings.GetPreloadedAssets();
            
            foreach (Object asset in assets)
            {
                if (asset is PostProcessingResources)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        public void SetupInPlayerSettings()
        {
            Object[] assets = PlayerSettings.GetPreloadedAssets();
            
            List<Object> newAssets = new List<Object>(assets);

            PostProcessingResources resources = PostProcessingResources.FindInEditor();

            if (resources)
            {
                newAssets.Add(resources);

                PlayerSettings.SetPreloadedAssets(newAssets.ToArray());
            }
        }
        
        internal void Save() { Save(true); }
        private void OnDisable() { Save(); }

        [SettingsProvider]
        public static SettingsProvider ProjectSettingsProvider()
        {
            var provider = new SettingsProvider(ProjectSettingsPath, SettingsScope.Project)
            {
                label = AssetInfo.ASSET_NAME,
                //activateHandler = (searchContext, rootElement) => { RefreshLightmapInfo(); },

                guiHandler = (searchContent) =>
                {
                    instance.includeAllShadersInBuild = EditorGUILayout.Toggle("Include all shaders", instance.includeAllShadersInBuild);
                    if (instance.includeAllShadersInBuild)
                    {
                        EditorGUILayout.HelpBox("This will include all shaders in the build, including those not used on any volume profiles." +
                                                "\n\n" +
                                                "Keep this enabled to make creating and controlling effects 100% through script possible in a build.", MessageType.Info);

                    }
                    else
                    {
                        EditorGUILayout.HelpBox("If disabled, you'd need to make sure that you have a reference to any required effect shaders, otherwise they will be stripped from builds." +
                                                "\n\n" +
                                                "Only do this to save on shader build size and for exact control!", MessageType.Warning);
                        
                    }
                    EditorGUILayout.Space();
                }

            };
            
            return provider;
        }
    }
}