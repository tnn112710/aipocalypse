//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Visual Effect Graph FX", order = 361)]
    public class VisualEffectGraphFX : ReferenceFX<CozyVFXInstance>
    {
        public CozyVFXInstance prefab;
        internal override CozyVFXInstance Prefab => prefab;
        public int layer;
        [Range(0, 1)]
        public float targetWeight = 1;

        public override void UpdateReference(float weight)
        {
            instance.Play(weight, targetWeight);
        }

        public override void GetDefaultInstance()
        {
            instance.gameObject.layer = layer;
        }

        public override void StopReference()
        {
            instance.Play(0, targetWeight);
        }
    }
}
