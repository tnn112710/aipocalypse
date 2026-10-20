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

#if !UNITY_6000_0_OR_NEWER
#define INCOMPATIBLE_VERSION
#endif
#if !UNITY_6000_0_OR_NEWER || UNITY_7000_0_OR_NEWER
#define UNSUPPORTED_MAJOR_VERSION
#endif
#if UNITY_6000_1 || UNITY_6000_2
#define NON_LTS_VERSION
#endif

#if !UNITY_6000_0_OR_NEWER
#error [SC Screen Effects] Imported in a version older than Unity 6, all present script and shader compile errors are valid and not something to simply be fixed. Upgrade the project to Unity 6 to resolve them. Alternatively use the older SC Post Effects asset, which is designed for Unity 2022.3 and older.
#endif

using System;
using System.Collections.Generic;
using System.Net;
using sc.screeneffects.runtime;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace sc.screeneffects.editor
{
    [NoAutoStaticsCleanup]
    public static partial class AssetInfo
    {
        //Asset specifics
        public const string ASSET_NAME = "SC Screen Effects";
        public const string ASSET_ID = "377136";
        public const string ASSET_ABRV = "SCPE";
        public const string CHANGELOG_GUID = "53954c715fb313645a046cf9b03109dc";
        public const string PACKAGE_NAME = "xyz.staggart-creations.screen-effects";

        public const string INSTALLED_VERSION = "3.0.2";
        
        private static readonly Dictionary<string, int> REQUIRED_PATCH_VERSIONS = new Dictionary<string, int>()
        {
            { "6000.0", 22 },
            { "6000.1", 3 },
            { "6000.2", 15 }
        };
        
        public const string DOC_URL = PostProcessingComponent.DOCUMENTATION_URL;
        public const string FORUM_URL = "https://discussions.unity.com/t/688846/99999";
        public const string DISCORD_INVITE_URL = "https://staggart.xyz/support/discord/";
        
        //Sorry, as much as I hate to intrude on an entire project, this is the only way in Unity to track importing or updating an asset
        public class ImportOrUpdateAsset : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromPath)
            {
                for (int i = 0; i < importedAssets.Length; i++)
                {
                    OnPreProcessAsset(importedAssets[i]);
                }
            }
            
            private static void OnPreProcessAsset(string m_assetPath)
            {
                //Importing/updating the asset
                if (m_assetPath.EndsWith(PACKAGE_NAME + "/Editor/AssetInfo.cs"))
                {
                    Installer.Initialize();
                    Installer.OpenWindowIfNeeded();
                }
            }
        }
        
        public static bool IsCompatiblePlatform
        {
            get
            {
                BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
                bool compatible = true;

                //test
                //target = BuildTarget.iOS;

                compatible &= target != BuildTarget.iOS;
                compatible &= target != BuildTarget.Android;
                compatible &= target != BuildTarget.Switch;
#if UNITY_6000_2_OR_NEWER
				compatible &= target != BuildTarget.Switch2;
#endif
                compatible &= target != BuildTarget.tvOS;
                compatible &= target != BuildTarget.VisionOS;

                return compatible;
            }
        }
        
        public static bool IsSupportedGraphicsAPI
        {
            get
            {
                bool compatible = true;

                GraphicsDeviceType graphicsAPI = PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget)[0];
                
                //test
                //target = BuildTarget.OpenGLES3;

                compatible &= graphicsAPI != GraphicsDeviceType.OpenGLCore;
                compatible &= graphicsAPI != GraphicsDeviceType.OpenGLES3;

                return compatible;
            }
        }
        
        public static void OpenInPackageManager()
        {
            Application.OpenURL("com.unity3d.kharma:content/" + ASSET_ID);
        }

        public static void OpenReviewsPage()
        {
            Application.OpenURL($"https://assetstore.unity.com/packages/slug/{ASSET_ID}?aid=1011l7Uk8&pubref=scpe_editor#reviews");
        }
        
        public static void OpenForumPage()
        {
            Application.OpenURL(FORUM_URL);
        }
        
        [NoAutoStaticsCleanup]
        public static partial class VersionChecking
        {
            public static string MinRequiredUnityVersion = "undefined";

            public enum UnityVersionType
            {
                Release,
                Beta,
                Alpha
            }
            public static UnityVersionType unityVersionType;
            
            [InitializeOnLoadMethod]
            static void Initialize()
            {
                if (CHECK_PERFORMED == false)
                {
                    CheckForUpdate(false);
                    CHECK_PERFORMED = true;
                }
            }
            
            [AutoStaticsCleanup]
            private static bool CHECK_PERFORMED
            {
                get => SessionState.GetBool(ASSET_ABRV + "_VERSION_CHECK_PERFORMED", false);
                set => SessionState.SetBool(ASSET_ABRV + "_VERSION_CHECK_PERFORMED", value);
            }
            
            [AutoStaticsCleanup]
            public static string LATEST_VERSION
            {
                get => SessionState.GetString(ASSET_ABRV + "_LATEST_VERSION", INSTALLED_VERSION);
                set => SessionState.SetString(ASSET_ABRV + "_LATEST_VERSION", value);
            }
            
            [AutoStaticsCleanup]
            public static bool UPDATE_AVAILABLE
            {
                get => SessionState.GetBool(ASSET_ABRV + "_UPDATE_AVAILABLE", false);
                set => SessionState.SetBool(ASSET_ABRV + "_UPDATE_AVAILABLE", value);
            }
            
            public static string GetUnityVersion()
            {
                string version = UnityEditorInternal.InternalEditorUtility.GetFullUnityVersion();
                
                //Remove GUID in parenthesis 
                return version.Substring(0, version.LastIndexOf(" ("));
            }
            
            public static bool supportedMajorVersion = true;
            public static bool supportedPatchVersion = true;
            public static bool compatibleVersion = true;
            public static bool ltsVersion = true;
            
            private static void ParseUnityVersion(string versionString, out int major, out int minor, out int patch, out UnityVersionType type)
            {
                var match = System.Text.RegularExpressions.Regex.Match(versionString, @"^(\d+)\.(\d+)\.(\d+)");
                if (match.Success)
                {
                    major = int.Parse(match.Groups[1].Value);
                    minor = int.Parse(match.Groups[2].Value);
                    patch = int.Parse(match.Groups[3].Value);
                    
                    if (versionString.Contains("b"))
                    {
                        type = UnityVersionType.Beta;
                    }
                    else if (versionString.Contains("a"))
                    {
                        type = UnityVersionType.Alpha;
                    }
                    else
                    {
                        type = UnityVersionType.Release;
                    }
                }
                else
                {
                    throw new FormatException($"Invalid Unity version format: {versionString}.");
                }
            }
            
            public static void CheckUnityVersion()
            {
                string unityVersion = GetUnityVersion();
                
                #if INCOMPATIBLE_VERSION
                compatibleVersion = false;
                #endif
                
                #if UNSUPPORTED_MAJOR_VERSION
                supportedMajorVersion = false;
                #endif
                
                #if NON_LTS_VERSION
				ltsVersion = false;
                #endif
                
                ParseUnityVersion(unityVersion, out int major, out int minor, out int patch, out unityVersionType);

                //Get the minimum required patch version for the current Unity version (eg. 6000.1)
                if (REQUIRED_PATCH_VERSIONS.TryGetValue($"{major}.{minor}", out int minPatchVersion))
                {
                    supportedPatchVersion = patch >= minPatchVersion;
                }
                else
                {
                    //None found, current Unity version likely unknown
                    supportedPatchVersion = true;
                }

                MinRequiredUnityVersion = $"{major}.{minor}.{minPatchVersion}";
            }

            public static string apiResult;
            private static bool showPopup;
            
            public enum QueryStatus
            {
                Fetching,
                Completed,
                Failed
            }
            public static QueryStatus queryStatus = QueryStatus.Completed;
            
            private static DateTime lastUpdateCheckTime;
            
            public static void CheckForUpdate(bool showPopup = false)
            {
                //Offline
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    LATEST_VERSION = INSTALLED_VERSION;
                    return;
                }
                
                //Prevent frequent calls from checking the latest version
                DateTime now = DateTime.Now;
                if (now.Subtract(lastUpdateCheckTime).TotalSeconds < 1) return;
                lastUpdateCheckTime = now;
                
                VersionChecking.showPopup = showPopup;

                queryStatus = QueryStatus.Fetching;

                var url = $"https://api.assetstore.unity3d.com/package/latest-version/{ASSET_ID}";

                using (System.Net.WebClient webClient = new System.Net.WebClient())
                {
                    webClient.DownloadStringCompleted += OnRetrievedAPIContent;
                    webClient.DownloadStringAsync(new System.Uri(url), apiResult);
                }
            }
            
            private class AssetStoreItem
            {
                public string name;
                public string version;
            }

            private static void OnRetrievedAPIContent(object sender, DownloadStringCompletedEventArgs e)
            {
                if (e.Error == null && !e.Cancelled)
                {
                    string result = e.Result;

                    AssetStoreItem asset = (AssetStoreItem)JsonUtility.FromJson(result, typeof(AssetStoreItem));

                    LATEST_VERSION = asset.version;
                    UPDATE_AVAILABLE = new Version(LATEST_VERSION) > new Version(INSTALLED_VERSION);
                    //UPDATE_AVAILABLE = true;
                    
#if SCPE_DEV
                    Debug.Log("<b>Asset store API</b> Update available = " + UPDATE_AVAILABLE + " (Installed:" + INSTALLED_VERSION + ") (Remote:" + LATEST_VERSION + ")");
#endif

                    queryStatus = QueryStatus.Completed;

                    if (showPopup)
                    {
                        if (UPDATE_AVAILABLE)
                        {
                            if (EditorUtility.DisplayDialog(ASSET_NAME + ", version " + INSTALLED_VERSION, "An updated version is available: " + LATEST_VERSION, "Open Package Manager", "Close"))
                            {
                                OpenInPackageManager();
                            }
                        }
                        else
                        {
                            if (EditorUtility.DisplayDialog(ASSET_NAME + ", version " + INSTALLED_VERSION, "Installed version is up-to-date!", "Close")) { }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[" + ASSET_NAME + "] Contacting update server failed: " + e.Error.Message);
                    queryStatus = QueryStatus.Failed;
                }
            }
        }
    }
}