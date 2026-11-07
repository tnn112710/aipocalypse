//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    [AddComponentMenu("Distant Lands/Cozy/Track Point")]
    public class CozyTrackPoint : MonoBehaviour
    {
        public static CozyTrackPoint Instance;

        public static Vector3 Position
        {
            get
            {
                if (Instance)
                    return Instance.transform.position;
                
                if (Camera.main)
                    return Camera.main.transform.position;

                return Vector3.zero;
            }
        }

        void OnEnable()
        {
            if (Instance != null)
            {
                Debug.LogWarning($"Only one COZY Track Point may be used in your scene at one time. Using {Instance.name} as the pivot point");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}