//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("COZY/Disable Rendering")]
    public class CozyCameraRendering : MonoBehaviour
    {
        [Tooltip("Disable COZY rendering on this camera.")]
        public bool excludeFromCozyRendering = true;
    }
}
