// Spline Spawner © Staggart Creations (http://staggart.xyz)
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
using sc.splines.spawner.runtime;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace sc.splines.spawner.editor
{
    public class AssetInfo
    {
        public const int ASSET_ID = 305974;
        public const string ASSET_NAME = "Spline Spawner";
        public const string ASSET_ABRV = "SP";
        public const string VERSION = "1.2.2";
        
        public const string DOC_URL = "https://staggart.xyz/support/documentation/spline-spawner/";
        public const string FORUM_URL = "https://discussions.unity.com/t/1659478";
        public const string DISCORD_INVITE_URL = "https://discord.gg/GNjEaJc8gw";

        public static void OpenInPackageManager()
        {
            Application.OpenURL("com.unity3d.kharma:content/" + ASSET_ID);
        }
        
        public static void OpenReviewsPage()
        {
            Application.OpenURL($"https://assetstore.unity.com/packages/slug/{ASSET_ID}?aid=1011l7Uk8&pubref=sp-editor#reviews");
        }
        
        internal static class VersionChecking
        {
            public static bool UPDATE_AVAILABLE
            {
                get => SessionState.GetBool(ASSET_ABRV + "_UPDATE_AVAILABLE", false);
                set => SessionState.SetBool(ASSET_ABRV + "_UPDATE_AVAILABLE", value);
            }
            
            public static string LATEST_VERSION_AVAILABLE
            {
                get => SessionState.GetString(ASSET_ABRV + "_LATEST_VERSION", VERSION);
                set => SessionState.SetString(ASSET_ABRV + "_LATEST_VERSION", value);
            }
            
            [NoAutoStaticsCleanup]
            public static string latestVersion = VERSION;
            [NoAutoStaticsCleanup]
            private static string apiResult;

            [NoAutoStaticsCleanup]
            private static DateTime lastUpdateCheckTime;
            public static void CheckForUpdate()
            {
                //Default, in case of a fail
                UPDATE_AVAILABLE = false;
                
                //Offline
                if (Application.internetReachability == NetworkReachability.NotReachable) return;
                                
                //Prevent frequent calls from checking the latest version
                DateTime now = DateTime.Now;
                if (now.Subtract(lastUpdateCheckTime).TotalSeconds < 1) return;
                lastUpdateCheckTime = now;
                
                //Debug.Log("Checking for version update");
                
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

            private static void OnRetrievedAPIContent(object sender, System.Net.DownloadStringCompletedEventArgs e)
            {
                if (e.Error == null && !e.Cancelled)
                {
                    string result = e.Result;

                    AssetStoreItem asset = (AssetStoreItem)JsonUtility.FromJson(result, typeof(AssetStoreItem));

                    latestVersion = asset.version;

                    Version remoteVersion = new Version(asset.version);
                    Version installedVersion = new Version(VERSION);

                    UPDATE_AVAILABLE = remoteVersion > installedVersion;
                    LATEST_VERSION_AVAILABLE = asset.version;

                    if (UPDATE_AVAILABLE)
                    {
                        //Debug.Log($"[{asset.name} v{installedVersion}] New version ({asset.version}) is available");
                    }
                }
            }
        }
    }
}