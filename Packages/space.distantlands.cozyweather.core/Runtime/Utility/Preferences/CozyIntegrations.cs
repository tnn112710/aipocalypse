//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    public class CozyIntegrations : ScriptableObject
    {
        [SerializeReference]
        public List<CozyIntegration> integrations = new();
        public static List<CozyIntegration> Integrations => Instance.integrations;

        public static void Initialize() => Instance.OnInitialize();

        private void OnInitialize()
        {
            foreach (var integration in integrations)
            {
                if (integration == null) continue;

                integration.Deinitialize();
                integration.Initialize();
            }
        }
        public static void Deinitialize() => Instance.OnDeinitialize();

        private void OnDeinitialize()
        {
            foreach (var integration in integrations)
            {
                if (integration == null) continue;
                integration.Deinitialize();
            }
        }

        //Boilerplate
        private static CozyIntegrations _instance;
        public static CozyIntegrations Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CozyIntegrations>("COZY Integrations");

                    if (_instance == null)
                    {
                        Debug.LogError("Settings asset 'COZY Integrations' not found in any Resources folder. Please create one.");
                    }
                }
                return _instance;
            }
        }
        public static bool Setup => _instance != null;

#if UNITY_EDITOR
        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(Instance);
        }
#endif
    }
}