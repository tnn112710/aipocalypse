//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    public class ColorOverridable : Overridable<Color>
    {
        public bool useHDR;
        public bool useAlpha;
        public override Color Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
            }
        }

        public ColorOverridable(Color value, bool overrideState = false) : base(value, overrideState)
        {
            useHDR = false;
            useAlpha = true;
        }
        public ColorOverridable(Color value, bool useHDR, bool useAlpha, bool overrideState = false) : base(value, overrideState)
        {
            this.useHDR = useHDR;
            this.useAlpha = useAlpha;
        }
    }

}