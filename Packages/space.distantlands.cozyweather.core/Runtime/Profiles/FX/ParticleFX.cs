//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Particle FX", order = 361)]
    public class ShurikenFX : ReferenceFX<CozyShurikenInstance>
    {
        public CozyShurikenInstance prefab;
        internal override CozyShurikenInstance Prefab => prefab;
        public int layer;
        [Range(0, 1)]
        public float targetWeight = 1;

        public override void UpdateReference(float weight)
        {
            instance.Play(weight);
        }

        public override void GetDefaultInstance()
        {
            instance.gameObject.layer = layer;
        }

        public override void StopReference()
        {
            instance.Play(0);
        }

    }

}
