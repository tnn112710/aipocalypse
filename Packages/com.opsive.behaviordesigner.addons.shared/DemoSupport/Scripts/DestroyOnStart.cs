/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Demo
{
    using UnityEngine;

    /// <summary>
    /// Destroys the GameObject when the game starts.
    /// </summary>
    public class DestroyOnStart : MonoBehaviour
    {
        /// <summary>
        /// Destroys the GameObject.
        /// </summary>
        private void Start()
        {
            Destroy(gameObject);
        }
    }
}