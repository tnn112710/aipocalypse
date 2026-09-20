// Stylized Grass Shader © Staggart Creations (http://staggart.xyz)
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
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace sc.stylizedgrass.editor
{
    public static class RendererIntegration
    {
        public enum RenderingAssets
        {
            [InspectorName("None")]
            None,
            [InspectorName("Vegetation Studio (Beyond)")]
            VegetationStudio,
            [InspectorName("GPU Instancer Pro")]
            GPUInstancer,
            [InspectorName("Nature Renderer (Unity 6)")]
            NatureRenderer,
            [InspectorName("Foliage Renderer")]
            FoliageRenderer
        }

        [Serializable]
        public class Integration
        {
            public string name;
            public RenderingAssets asset;
            public int id;
            public string libraryGUID;
            public string instancing_options;
            public bool includeWithPragmas;
            public bool installed;
            
            public Texture2D _thumbnail;
            public Texture2D thumbnail
            {
                get
                {
                    if (!_thumbnail) _thumbnail = DownloadAssetIcon(id);
                
                    return _thumbnail;
                }
                set => _thumbnail = value;
            }

            public Integration(string name, RenderingAssets asset, int id, string guid, bool includeWithPragmas, string instancingOptions)
            {
                this.name = name;
                this.asset = asset;
                this.id = id;
                this.libraryGUID = guid;
                this.includeWithPragmas = includeWithPragmas;
                this.instancing_options = instancingOptions;
                this.installed = IsLibraryPresent(this);
            }
        }
        
        private static readonly Dictionary<int, Texture2D> DownloadedAssetIcons = new();
        private static readonly HashSet<int> AssetIconsBeingDownloaded = new();
        private static Texture2D TemporaryAssetIcon;
        
        public static Texture2D DownloadAssetIcon(int id)
        {
#if UNITY_EDITOR
            if(id == 0) return TemporaryAssetIcon;
            
            if(Application.internetReachability == NetworkReachability.NotReachable) return TemporaryAssetIcon;
            
            if (DownloadedAssetIcons.TryGetValue(id, out Texture2D cachedIcon) && cachedIcon)
            {
                return cachedIcon;
            }

            if (!AssetIconsBeingDownloaded.Contains(id))
            {
                AssetIconsBeingDownloaded.Add(id);
                DownloadAssetIconAsync(id);
            }
#endif

            return TemporaryAssetIcon;
        }
        
        private static async void DownloadAssetIconAsync(int id)
        {
            try
            {
                string url = $"https://api.assetstore.unity3d.com/affiliate/embed/package/{id}/icon";

                using UnityWebRequest request = UnityWebRequest.Get(url);

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await System.Threading.Tasks.Task.Yield();
                }

                AssetIconsBeingDownloaded.Remove(id);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    //Debug.LogWarning($"Failed to download Asset Store icon for package {id}: {request.error}");
                    DownloadedAssetIcons[id] = TemporaryAssetIcon;
                    InternalEditorUtility.RepaintAllViews();
                    
                    return;
                }

                byte[] imageBytes = request.downloadHandler.data;
                Texture2D icon = new Texture2D(160, 160, TextureFormat.RGBA32, false, false);
                icon.LoadImage(imageBytes, true);

                if (icon)
                {
                    DownloadedAssetIcons[id] = icon;
                    InternalEditorUtility.RepaintAllViews();
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to download Asset Store icon for package {id}: {e.Message}");
            }
        }

        private static Integration[] _RenderingIntegrations;
        public static Integration[] RenderingIntegrations
        {
            get
            {
                if (_RenderingIntegrations == null) _RenderingIntegrations = GetAvailableRenderingIntegrations();
                return _RenderingIntegrations;
            }
        }
        
        private static Integration[] GetAvailableRenderingIntegrations()
        {
            Integration[] integrationsArray = new[]
            {
                new Integration("Default Unity", RenderingAssets.None, 0, "", false,string.Empty),
                new Integration("Vegetation Studio (Beyond)", RenderingAssets.VegetationStudio, 0, "a9324aff8d6fb7746847dbf6108e0382", false, "assumeuniformscaling renderinglayer procedural:setupVSPro"),
                new Integration("Nature Renderer 6", RenderingAssets.NatureRenderer, 285950,"ca4c4574fc8ceab448f85800842a6cee", false, "procedural:SetupNatureRenderer"),
                new Integration("GPU Instancer Pro", RenderingAssets.GPUInstancer, 290293, "01c16f02afaf429591046b0d8007c478", true, "procedural:setupGPUI"),
                new Integration("Foliage Renderer", RenderingAssets.FoliageRenderer, 307081, "7f684950130464f4c86c65052b7c92c8", false, "procedural:setupFoliageRenderer forwardadd"),
            };

            for (int i = 0; i < integrationsArray.Length; i++)
            {
                integrationsArray[i].installed = IsLibraryPresent(integrationsArray[i]);
            }

            return integrationsArray;
        }
        
        private static Integration[] _WeatherIntegrations;
        public static Integration[] WeatherIntegrations
        {
            get
            {
                if (_WeatherIntegrations == null) _WeatherIntegrations = GetAvailableWeatherIntegrations();
                return _WeatherIntegrations;
            }
        }
        
        private static Integration[] GetAvailableWeatherIntegrations()
        {
            Integration[] integrationsArray = new[]
            {
                new Integration("Enviro", RenderingAssets.None, 0, "", false,string.Empty),
                new Integration("Vegetation Studio (Beyond)", RenderingAssets.VegetationStudio, 0, "a9324aff8d6fb7746847dbf6108e0382", false, "assumeuniformscaling renderinglayer procedural:setupVSPro"),
                new Integration("Nature Renderer 6", RenderingAssets.NatureRenderer, 285950,"ca4c4574fc8ceab448f85800842a6cee", false, "procedural:SetupNatureRenderer"),
                new Integration("GPU Instancer Pro", RenderingAssets.GPUInstancer, 290293, "01c16f02afaf429591046b0d8007c478", true, "procedural:setupGPUI"),
                new Integration("Foliage Renderer", RenderingAssets.FoliageRenderer, 307081, "7f684950130464f4c86c65052b7c92c8", false, "procedural:setupFoliageRenderer forwardadd"),
            };

            for (int i = 0; i < integrationsArray.Length; i++)
            {
                integrationsArray[i].installed = IsLibraryPresent(integrationsArray[i]);
            }

            return integrationsArray;
        }

        public static Integration GetIntegration(RenderingAssets asset)
        {
            for (int i = 0; i < RenderingIntegrations.Length; i++)
            {
                if (RenderingIntegrations[i].asset == asset) return RenderingIntegrations[i];
            }

            return null;
        }

        public static bool IsLibraryPresent(Integration integration)
        {
            if (integration.asset == RenderingAssets.None) return true;

            string path = AssetDatabase.GUIDToAssetPath(integration.libraryGUID);
            
            if(path == string.Empty) return false;

            return AssetDatabase.LoadAssetAtPath(path, typeof(Object));
        }

        public static Integration GetFirstInstalled()
        {
            for (int i = 0; i < RenderingIntegrations.Length; i++)
            {
                //Always installed anyway
                if (RenderingIntegrations[i].asset == RenderingAssets.None) continue;
                
                if (IsLibraryPresent(RenderingIntegrations[i]))
                {
                    return RenderingIntegrations[i];
                }
            }

            //No third-party assets installed, default to Unity
            return GetIntegration(RenderingAssets.None);
        }
    }
}