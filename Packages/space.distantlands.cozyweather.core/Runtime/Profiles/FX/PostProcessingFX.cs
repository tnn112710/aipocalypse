//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Post Processing FX", order = 361)]
    public class PostProcessingFX : ReferenceFX<Volume>
    {
        public int layer;
        public float priority = 100;
        public VolumeProfile effectSettings;

        public override void GetDefaultInstance()
        {
            instance.profile = effectSettings;
            instance.weight = 0;
            instance.priority = priority;
            instance.isGlobal = true;
            instance.gameObject.layer = layer;
        }

        public override void UpdateReference(float weight)
        {
            instance.weight = Easing.Evaluate(weight, TransitionTimeModifier);
        }

        public override void StopReference()
        {
            instance.weight = 0;
        }
    }
}
