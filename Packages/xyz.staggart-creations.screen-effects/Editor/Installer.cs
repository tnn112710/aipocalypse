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
using System.Reflection;
using sc.screeneffects.runtime;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageInfo;
#if XR
using UnityEngine.XR.Management;
#endif

namespace sc.screeneffects.editor
{
    [NoAutoStaticsCleanup]
    public static class Installer
    {
        public class SetupItem
        {
            public MessageType state = MessageType.None;
            public string name;
            public string description;
            public string actionName;
            
            public Action action;

            public SetupItem(string name)
            {
                this.name = name;
            }
            
            public SetupItem(string name, MessageType state)
            {
                this.name = name;
                this.state = state;
            }
            

            public void ExecuteAction()
            {
                action.Invoke();
                action = null;

                if (state == MessageType.Error)
                {
                    m_errorCount--;
                }
                else if (state == MessageType.Warning)
                {
                    m_warningCount--;
                }

                state = MessageType.None;
                
                Installer.Initialize();
            }
        }
        
        public static List<SetupItem> SetupItems = new List<SetupItem>();

        public static int ErrorCount
        {
            get => m_errorCount;
        }
        private static int m_errorCount;
        public static int WarningCount
        {
            get => m_warningCount;
        }
        private static int m_warningCount;
        
        public static bool HasError => ErrorCount > 0;

        public static void OpenWindowIfNeeded()
        {
            if (HasError && Application.isBatchMode == false && BuildPipeline.isBuildingPlayer == false)
            {
                HelpWindow.ShowWindow(true);
            }
        }
        
        private static void AddItem(SetupItem item)
        {
            if (item.state == MessageType.Error) m_errorCount++;
            else if (item.state == MessageType.Warning) m_warningCount++;
            
            SetupItems.Add(item);
        }
            
        public static void Initialize()
        {
            SetupItems.Clear();
            m_errorCount = 0;
            m_warningCount = 0;
            
            if (Application.isBatchMode || BuildPipeline.isBuildingPlayer) return;

            bool using2DRenderer = Utilities.Using2DRenderer();
            #if UNITY_6000_4_OR_NEWER
            Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            #else
            Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            #endif
            
            AssetInfo.VersionChecking.CheckUnityVersion();
            SetupItem unityVersion = new SetupItem(AssetInfo.VersionChecking.GetUnityVersion());
            {
                unityVersion.state = MessageType.None;
                unityVersion.description = $"Likely compatible and supported Unity version";
                
                //Too old
                if (AssetInfo.VersionChecking.compatibleVersion == false || AssetInfo.VersionChecking.supportedPatchVersion == false)
                {
                    unityVersion.state = MessageType.Error;
                    unityVersion.description = $"This version of Unity is not compatible and is not subject to support. Update to at least <b>{AssetInfo.VersionChecking.MinRequiredUnityVersion}</b>. Errors and issues need to be resolved locally";
                }
                else
                {
                    //Too broken
                    if (AssetInfo.VersionChecking.unityVersionType != AssetInfo.VersionChecking.UnityVersionType.Release)
                    {
                        unityVersion.state = MessageType.Warning;
                        unityVersion.description = "Alpha/preview versions of Unity are not supported. Shader/script errors or warnings may occur depending on which weekly-version you are using." +
                                                   "\n\n" +
                                                   "After the release version becomes available, compatibility will be verified and an update may follow with the necessary fixes/changes.";
                    }
                    
                    //Too risky
                    if (AssetInfo.VersionChecking.ltsVersion == false)
                    {
                        unityVersion.state = MessageType.Warning;
                        unityVersion.description = "Non-LTS versions of Unity are not recommended nor supported." +
                                                   "\n\n" +
                                                   "They are only updated for a few months, before being abandoned (despite being labeled as \"Supported\")." +
                                                   "\n\n" +
                                                   "Updating to or staying on an LTS version is strongly recommended.";
                    }

                    //Too new
                    if (AssetInfo.VersionChecking.supportedMajorVersion == false)
                    {
                        unityVersion.state = MessageType.Warning;
                        unityVersion.description = $"This version of Unity is not supported. An upgrade version of this asset that does support this may be available. See the documentation for up-to-date information.";
                    }
                }
            }
            AddItem(unityVersion);
            
            AssetInfo.VersionChecking.CheckForUpdate();
            SetupItem assetVersion = new SetupItem($"Asset version ({AssetInfo.INSTALLED_VERSION})");
            {
                //Testing
                //AssetInfo.VersionChecking.compatibleVersion = false;
                //AssetInfo.VersionChecking.alphaVersion = true;
                //AssetInfo.VersionChecking.UPDATE_AVAILABLE = false;
                
                if (AssetInfo.VersionChecking.UPDATE_AVAILABLE)
                {
                    assetVersion.state = MessageType.Info;
                    assetVersion.description = $"An updated version is available (v{AssetInfo.VersionChecking.LATEST_VERSION})" +
                                               "\n\n" +
                                               "Asset can be updated through the Package Manager.";

                    assetVersion.actionName = "Open Package Manager";
                    assetVersion.action = AssetInfo.OpenInPackageManager;
                }
                else
                {
                    assetVersion.state = MessageType.None;
                    assetVersion.description = "Installed version is the latest";
                }
            }
            AddItem(assetVersion);
            
            SetupItem platform = new SetupItem($"Platform ({EditorUserBuildSettings.activeBuildTarget})");
            {
                platform.state = MessageType.None;
                platform.description = "Compatible";

                if (AssetInfo.IsCompatiblePlatform == false)
                {
                    platform.state = MessageType.Warning;
                    platform.description = "This platform is not supported. See the documentation for up-to-date information.";
                }
            }
            AddItem(platform);
            
            SetupItem renderGraph = new SetupItem("Render Graph", AutoSetup.RenderGraphEnabled() ? MessageType.None : MessageType.Error);
            {
                if (renderGraph.state == MessageType.Error)
                {
                    renderGraph.description = "Disabled (Backwards Compatibility mode). All effects will be unavailable!";
                    renderGraph.actionName = "Enable";
                    renderGraph.action = () =>
                    {
                        AutoSetup.SetRenderGraphCompatibilityMode(false);
                    };
                }
                else
                {
                    renderGraph.state = MessageType.None;
                    renderGraph.description = "Enabled";
                }

                AddItem(renderGraph);
            }
            
            SetupItem graphicsAPI = new SetupItem($"Graphics API ({PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget)[0].ToString()})");
            {
                graphicsAPI.state = MessageType.None;
                graphicsAPI.description = $"Compatible";

                if (AssetInfo.IsSupportedGraphicsAPI == false)
                {
                    graphicsAPI.state = MessageType.Error;
                    graphicsAPI.description = "This graphics API is not supported. See the documentation for up-to-date information.";
                }
            }
            AddItem(graphicsAPI);
            
            SetupItem colorSpace = new SetupItem($"Color space ({PlayerSettings.colorSpace.ToString()})");
            {
                if (PlayerSettings.colorSpace == ColorSpace.Linear)
                {
                    colorSpace.state = MessageType.None;
                    colorSpace.description = $"Linear";
                }
                else
                {
                    colorSpace.state = MessageType.Warning;
                    colorSpace.description = $"All content is authored for the Linear color space, colors will not look as advertised.";
                }
            }
            AddItem(colorSpace);
            
            SetupItem renderer2D = new SetupItem("2D Renderer");
            {
                renderer2D.state = MessageType.Warning;
                renderer2D.description = "2D Renderer is partially supported." +
                                         "\n\n" +
                                         "3D effects have limited to no use:" +
                                         "• Fog" +
                                         "• Sunshafts" +
                                         "• Edge Detection (depth/normals)" +
                                         "• Curvature Highlight" +
                                         "\n\n" +
                                         "Feature unavailable:" +
                                         "• Rendering Layer masking" +
                                         "• Distance fading";
                
                if (using2DRenderer)
                {
                    AddItem(renderer2D);
                }
            }
            
            SetupItem cameras = new SetupItem("Cameras");
            {
                cameras.state = MessageType.None;
                cameras.description = "Main camera has post processing enabled";

                List<Camera> invalidCameras = new List<Camera>();

                for (int i = 0; i < allCameras.Length; i++)
                {
                    Camera camera = allCameras[i];
                    
                    if(camera.hideFlags != HideFlags.None) continue;
                    if(camera.cameraType != CameraType.Game) continue;
                    if(camera.targetTexture != null) continue;
                    
                    UniversalAdditionalCameraData cameraData = allCameras[i].GetComponent<UniversalAdditionalCameraData>();
                    if(!cameraData) continue;

                    //Camera stacking in use, post processing can be kept disabled
                    if(cameraData.cameraStack != null && cameraData.cameraStack.Count > 0) continue;
                    
                    if (cameraData.renderPostProcessing == false)
                    {
                        invalidCameras.Add(camera);
                    }
                }

                if (invalidCameras.Count > 0)
                {
                    cameras.state = MessageType.Warning;
                    cameras.description = $"The following cameras do not have post processing enabled";
                    
                    for (int i = 0; i < invalidCameras.Count; i++)
                    {
                        cameras.description += $"\n• {invalidCameras[i].name}";
                    }

                    cameras.actionName = "Enable";
                    cameras.action = () =>
                    {
                        for (int i = 0; i < invalidCameras.Count; i++)
                        {
                            UniversalAdditionalCameraData cameraData = invalidCameras[i].GetComponent<UniversalAdditionalCameraData>();
                            
                            cameraData.renderPostProcessing = true;
                            
                            EditorUtility.SetDirty(cameraData);
                        }
                    };
                }
                
            }
            AddItem(cameras);
            
            SetupItem overlayCameras = new SetupItem("Camera Stacking");
            {
                overlayCameras.state = MessageType.None;
                overlayCameras.description = "Camera stacking is not in use. No action required.";
                
                List<Camera> invalidBaseCameras = new List<Camera>();
                for (int i = 0; i < allCameras.Length; i++)
                {
                    Camera camera = allCameras[i];
                    UniversalAdditionalCameraData baseCameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                    if (!baseCameraData || baseCameraData.renderType != CameraRenderType.Base)
                    {
                        continue;
                    }
                    
                    int stackCount = baseCameraData.cameraStack.Count;
                    bool basePostProcessing = baseCameraData.renderPostProcessing;
                    
                    bool overlayPostProcessing = false;
                    for (int j = 0; j < stackCount; j++)
                    {
                        Camera cameraInStack = baseCameraData.cameraStack[j];
                        UniversalAdditionalCameraData cameraData = cameraInStack.GetComponent<UniversalAdditionalCameraData>();
                        
                        if (cameraData.renderType == CameraRenderType.Overlay)
                        {
                            overlayPostProcessing |= cameraData.renderPostProcessing;
                        }
                    }

                    if (basePostProcessing && overlayPostProcessing)
                    {
                        invalidBaseCameras.Add(camera);
                    }
                }

                if (invalidBaseCameras.Count > 0)
                {
                    overlayCameras.state = MessageType.Error;
                    overlayCameras.description =
                        "Camera stacking is in use. A base camera was detected with Post Processing enabled, but one or more Overlay cameras also have it enabled." +
                        "\n\n" +
                        "This causes effects to be applied twice!" +
                        "\n\n" +
                        "Only Overlay cameras need this enabled, since they render last";

                    overlayCameras.actionName = "Disable on Base, enable on Overlay.";
                    overlayCameras.action = () =>
                    {
                        for (int i = 0; i < invalidBaseCameras.Count; i++)
                        {
                            UniversalAdditionalCameraData baseCameraData = invalidBaseCameras[i].GetComponent<UniversalAdditionalCameraData>();
                            
                            baseCameraData.renderPostProcessing = false;
                            EditorUtility.SetDirty(baseCameraData);
                            
                            int stackCount = baseCameraData.cameraStack.Count;
                            for (int j = 0; j < stackCount; j++)
                            {
                                Camera cameraInStack = baseCameraData.cameraStack[j];
                                UniversalAdditionalCameraData cameraData = cameraInStack.GetComponent<UniversalAdditionalCameraData>();

                                cameraData.renderPostProcessing = true;
                                EditorUtility.SetDirty(cameraData);
                            }
                        }
                    };
                }
            }
            SetupItems.Add(overlayCameras);

            SetupItem renderers = new SetupItem("Renderers");
            {
                renderers.state = MessageType.None;
                renderers.description = "All renderers have post processing enabled";
                
                ScriptableRendererData[] rendererDatas = AutoSetup.GetRenderDataList(UniversalRenderPipeline.asset);
                
                List<UniversalRendererData> invalidRenderers = new List<UniversalRendererData>();
                for (int i = 0; i < rendererDatas.Length; i++)
                {
                    UniversalRendererData rendererData = rendererDatas[i] as UniversalRendererData;

                    if(!rendererData) continue;
                    
                    if (rendererData.GetType() == typeof(UniversalRenderer))
                    {
                        if (rendererData.postProcessData == null)
                        {
                            invalidRenderers.Add(rendererData);
                        }
                    }

                    if (rendererData.GetType() == typeof(Renderer2DData))
                    {
                        Renderer2DData renderer2DData = rendererDatas[i] as Renderer2DData;
                        
                        //Can't access the `postProcessData` field, assume it is enabled
                    }
                }

                if (invalidRenderers.Count > 0)
                {
                    renderers.state = MessageType.Warning;
                    renderers.description = "One or more renderers have Post Processing disabled on them:";

                    foreach (var r in invalidRenderers)
                    {
                        cameras.description += $"\n• {r.name}";
                    }
                    
                    cameras.actionName = "Enable";
                    cameras.action = () =>
                    {
                        var path = System.IO.Path.Combine(UniversalRenderPipelineAsset.packagePath, "Runtime/Data/PostProcessData.asset");
                        PostProcessData defaultData = AssetDatabase.LoadAssetAtPath<PostProcessData>(path);
                        
                        for (int i = 0; i < invalidRenderers.Count; i++)
                        {
                            UniversalRendererData rendererData = invalidRenderers[i];
                            
                            rendererData.postProcessData = defaultData;
                            
                            EditorUtility.SetDirty(rendererData);
                        }
                    };
                }
            }
            SetupItems.Add(renderers);
            
            SetupItem renderingLayers = new SetupItem("Rendering Layer Support");
            {
                renderingLayers.state = MessageType.None;
                renderingLayers.description = "Supported";
                
                if (using2DRenderer)
                {
                    renderingLayers.state = MessageType.Warning;
                    renderingLayers.description = "This functionality is not possible when using the 2D Renderer.";
                }
                else
                {
                    GraphicsDeviceType[] graphicsAPIs = PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget);
                    
                    List<GraphicsDeviceType> unsupportedGraphicsAPIs = new List<GraphicsDeviceType>();
                    for (int i = 0; i < graphicsAPIs.Length; i++)
                    {
                        if (graphicsAPIs[i] == GraphicsDeviceType.OpenGLCore)
                        {
                            unsupportedGraphicsAPIs.Add(graphicsAPIs[i]);
                        }

                        if (graphicsAPIs[i] == GraphicsDeviceType.OpenGLES3)
                        {
                            unsupportedGraphicsAPIs.Add(graphicsAPIs[i]);
                        }
                    }

                    if (unsupportedGraphicsAPIs.Count > 0)
                    {
                        renderingLayers.state = MessageType.Warning;
                        renderingLayers.description = "The following graphics APIs are not supported:";

                        foreach (var api in unsupportedGraphicsAPIs)
                        {
                            renderingLayers.description += $"\n• {api}";
                        }
                    }
                    else
                    {
                        List<UniversalRenderPipelineAsset> unsupportedRenderers = Utilities.GetPipelineAssetsWithRenderingLayersDisabled();

                        if (unsupportedRenderers.Count > 0)
                        {
                            renderingLayers.state = MessageType.Warning;
                            renderingLayers.description =
                                "The following Universal Render Pipeline Assets have Rendering Layers disabled:";

                            foreach (var renderer in unsupportedRenderers)
                            {
                                renderingLayers.description += $"\n• {renderer.name}";
                            }

                            renderingLayers.actionName = "Enable";
                            renderingLayers.action = () =>
                            {
                                foreach (var renderer in unsupportedRenderers)
                                {
                                    Utilities.EnableRenderingLayers(renderer);
                                    EditorUtility.SetDirty(renderer);
                                }
                            };
                        }
                    }
                }
            }
            SetupItems.Add(renderingLayers);

            SetupItem shaderInclusion = new SetupItem("Project Settings");
            {
                shaderInclusion.state = MessageType.None;
                shaderInclusion.description = "Shaders configured to be included in build.";

                if (PostProcessingProjectSettings.instance.HasProjectSettingsSetup() == false)
                {
                    shaderInclusion.state = MessageType.Error;
                    shaderInclusion.description = "Shaders are not configured to be included in the build. Script-based effects will not work.";

                    //Weird fix needed here, otherwise the installer doesn't pop up on a fresh import
                    m_errorCount++;
                    
                    shaderInclusion.actionName = "Fix";
                    shaderInclusion.action = () =>
                    {
                        PostProcessingProjectSettings.instance.SetupInPlayerSettings();
                    };
                }
            }
            SetupItems.Add(shaderInclusion);
            
            #if UNITY_6000_6_OR_NEWER
            SetupItem onTileRendering = new SetupItem("On Tile Rendering");
            {
                onTileRendering.state = MessageType.None;
                onTileRendering.description = "On Tile Rendering is disabled";

                UniversalRendererData defaultRenderer = AutoSetup.GetDefaultRenderer() as UniversalRendererData;
                if (defaultRenderer && Utilities.OnTileRenderingEnabled(defaultRenderer))
                {
                    onTileRendering.state = MessageType.Error;
                    onTileRendering.description = "On Tile Rendering is enabled on the default renderer." +
                                                  "\n\n" +
                                                  "This is strictly unsupported and must be disabled.";
                    
                    onTileRendering.actionName = "Disable";
                    onTileRendering.action = () =>
                    {
                        defaultRenderer.tileOnlyMode = false;
                        EditorUtility.SetDirty(defaultRenderer);
                    };
                }
            }
            AddItem(onTileRendering);
            #endif
            
            #if XR
            SetupItem xrRendering = new SetupItem("XR Rendering");
            {
                List<XRLoader> multiPassLoaders = AutoSetup.GetXRMultiPassLoaders();
                
                if (multiPassLoaders.Count == 0)
                {
                    xrRendering.state = MessageType.None;
                    xrRendering.description = "Single Pass Instanced is used";
                }
                else
                {
                    xrRendering.description = $"Multipass renderers: {string.Join(", ", multiPassLoaders.Select(x => x.name))}";
                    
                    xrRendering.description += $"\n\nMulti-pass rendering is not supported, and obsolete.";

                    xrRendering.actionName = "Fix";
                    xrRendering.action = () =>
                    {
                        foreach (XRLoader loader in multiPassLoaders)
                        {
                            AutoSetup.SetLoaderRenderModeToSinglePassInstanced(loader);
                        }
                    };
                }
            }
            SetupItems.Add(xrRendering);
            #endif
            
            /*
            //Test formatting
            SetupItem error = new SetupItem("Error");
            {
                error.state = MessageType.Error;
                error.description = "An error has occured";

                error.actionName = "Fix";
                error.action = () =>
                {

                };
                AddItem(error);
            }

            SetupItem warnings = new SetupItem("Warning");
            {
                warnings.state = MessageType.Warning;
                warnings.description = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book.";

                warnings.actionName = "Enable";
                warnings.action = () =>
                {

                };
                AddItem(warnings);
            }
            */
            
            //Sort to display errors first, then warnings.
            SetupItems = SetupItems.OrderBy(o=> (o.state == MessageType.Info || o.state
                == MessageType.None)).ToList();
        }
        
        public static UnityEditor.PackageManager.PackageInfo GetURPPackage()
        {
            string packageName = "com.unity.render-pipelines.universal";
            bool upmDisabled = Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "-noUpm", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "noUpm", StringComparison.OrdinalIgnoreCase));

            if (upmDisabled)
            {
                Debug.LogWarning($"Failed to retrieve package {packageName} from Package Manager. Editor was launched using the -noUpm command line argument.");
                return null;
            }
            
            SearchRequest request = Client.Search(packageName);
                
            while (request.Status == StatusCode.InProgress) { /* Waiting... */ }

            if (request.Status == StatusCode.Failure)
            {
                Debug.LogError("Failed to retrieve URP package from Package Manager...");
                return null;
            }

            return request.Result[0];
        }
        
        public static class Shaders
        {
            public static Shader[] GetAll()
            {
                PostProcessingResources resources = PostProcessingResources.FindInEditor();

                if (!resources) return null;
                
                List<Shader> shaders = new List<Shader>();
                for (int v = 0; v < resources.shaders.Length; v++)
                {
                    Shader shader = resources.shaders[v];
                    
                    if(shader) shaders.Add(shader);
                }

                return shaders.ToArray();
            }
            
            public static ShaderMessage[] GetErrorMessages(Shader shader)
            {
                ShaderMessage[] messages = Array.Empty<ShaderMessage>();

                int n = ShaderUtil.GetShaderMessageCount(shader);

                if (n < 1) return messages;
            
                messages = ShaderUtil.GetShaderMessages(shader);
            
                //Filter for errors
                messages = messages.Where(x => x.severity == ShaderCompilerMessageSeverity.Error).ToArray();

                return messages;
            }
        }
    }
}