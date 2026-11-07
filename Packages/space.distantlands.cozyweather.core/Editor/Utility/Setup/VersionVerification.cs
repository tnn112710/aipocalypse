//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace DistantLands.Cozy.EditorScripts
{
    [InitializeOnLoad]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    internal static class VersionChecking
    {
        private const string TargetPackage = "space.distantlands.cozyweather.core";

        private static ListRequest _listRequest;
        private static UnityWebRequest _webRequest;

        private static string _installedVersionStr;
        private static string _remoteVersionStr;

        public static int VersionStatus
        {
            get => SessionState.GetInt("CZY_VERSION_STATUS", -2);
        }

        static VersionChecking()
        {
            if (SessionState.GetInt("CZY_VERSION_CHECK_RUN", 0) == 0)
            {
                EditorApplication.delayCall += CheckForUpdate;
            }
        }

        public static string LatestVersion
        {
            get => SessionState.GetString("CZY_VERSION", "NotInitialized");
            set => SessionState.SetString("CZY_VERSION", value);
        }

        public static string InstalledVersion
        {
            get => SessionState.GetString("CZY_INSTALLED_VERSION", "NotInitialized");
            set => SessionState.SetString("CZY_INSTALLED_VERSION", value);
        }

        [Serializable]
        private class AssetStoreItem
        {
            public string name;
            public string version;
        }

        public static void CheckForUpdate()
        {
            SessionState.SetInt("CZY_VERSION_CHECK_RUN", 1);

            _installedVersionStr = null;
            _remoteVersionStr = null;

            _listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);

            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                string url = $"https://api.assetstore.unity3d.com/package/latest-version/{AssetInformation.ASSET_ID}";
                _webRequest = UnityWebRequest.Get(url);
                _webRequest.SendWebRequest();
            }
            else
            {
                Debug.LogWarning("[Version Check] No internet connection available to fetch remote version.");
            }

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (_listRequest != null && _listRequest.IsCompleted)
            {
                if (_listRequest.Status == StatusCode.Success)
                {
                    foreach (var package in _listRequest.Result)
                    {
                        if (package.name == TargetPackage)
                        {
                            _installedVersionStr = package.version;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(_installedVersionStr))
                    {
                        Debug.LogWarning($"[Version Check] Package '{TargetPackage}' is not installed in this project.");
                        _installedVersionStr = "0.0.0";
                    }
                }
                else
                {
                    Debug.LogError($"[Version Check] Failed to list packages: {_listRequest.Error?.message}");
                    _installedVersionStr = "0.0.0";
                }

                _listRequest = null; // Mark task done
            }

            if (_webRequest != null && _webRequest.isDone)
            {
                if (_webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = _webRequest.downloadHandler.text;
                        AssetStoreItem asset = JsonUtility.FromJson<AssetStoreItem>(json);
                        if (asset != null && !string.IsNullOrEmpty(asset.version))
                        {
                            _remoteVersionStr = asset.version;
                            LatestVersion = asset.version;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Version Check] Failed to parse Asset Store response: {ex.Message}");
                        _remoteVersionStr = "0.0.0";
                    }
                }
                else
                {
                    Debug.LogWarning($"[Version Check] Failed to fetch remote version: {_webRequest.error}");
                    _remoteVersionStr = "0.0.0";
                }

                _webRequest.Dispose();
                _webRequest = null; // Mark task done
            }

            bool isWaitingOnUpm = _listRequest != null;
            bool isWaitingOnWeb = _webRequest != null;

            if (isWaitingOnUpm || isWaitingOnWeb)
            {
                return;
            }

            EditorApplication.update -= OnEditorUpdate;
            CompareVersions();
        }

        private static void CompareVersions()
        {
            if (string.IsNullOrEmpty(_installedVersionStr) || string.IsNullOrEmpty(_remoteVersionStr))
            {
                return;
            }

            if (Version.TryParse(_installedVersionStr, out Version installed) &&
                Version.TryParse(_remoteVersionStr, out Version remote))
            {
                InstalledVersion = _installedVersionStr;
                LatestVersion = _remoteVersionStr;

                switch (remote.CompareTo(installed))
                {
                    case < 0:
                        SessionState.SetInt("CZY_VERSION_STATUS", 1);
                        Debug.Log($"<b>[COZY Weather]</b> Developer beta installed! Installed: v{installed} | Latest: v{remote}");
                        break;
                    case > 0:
                        SessionState.SetInt("CZY_VERSION_STATUS", -1);
                        Debug.Log($"<b>[COZY Weather]</b> Update available! Installed: v{installed} | Latest: v{remote}");
                        break;
                    default:
                        SessionState.SetInt("CZY_VERSION_STATUS", 0);
                        Debug.Log($"[COZY Weather] Up to date (v{installed}).");
                        break;
                }
            }
            else
            {
                Debug.LogError($"[Version Check] Could not safely parse version strings. Local: {_installedVersionStr}, Remote: {_remoteVersionStr}");
            }
        }
    }
}