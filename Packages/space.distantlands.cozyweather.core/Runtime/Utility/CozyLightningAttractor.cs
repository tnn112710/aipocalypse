//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DistantLands.Cozy
{
    [AddComponentMenu("Distant Lands/Cozy/Lightning Attractor")]
    public class CozyLightningAttractor : MonoBehaviour
    {
        private static List<CozyLightningAttractor> instances;

        [Tooltip("Call this event when this point is struck. Use this to create lightning strike effects like explosions, start fires, damage players, etc.")]
        public UnityEvent onStrike;
        public delegate void OnStrike();
        public OnStrike onStrikeEvent;

        public static CozyLightningAttractor GetRandomAttractor() => instances != null && instances.Count > 0 ? instances[Random.Range(0, instances.Count - 1)] : null;

        void OnEnable()
        {
            instances ??= new List<CozyLightningAttractor>();

            instances.Add(this);

            for (int i = instances.Count - 1; i >= 0; i--)
            {
                if (instances[i] == null) instances.RemoveAt(i);
            }
        }

        public void Strike()
        {
            onStrike?.Invoke();
            onStrikeEvent?.Invoke();
        }

        void OnDisable()
        {
            instances.Remove(this);
        }
    }
}
