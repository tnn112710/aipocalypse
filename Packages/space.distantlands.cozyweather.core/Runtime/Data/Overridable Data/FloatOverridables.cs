//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    public class MinFloatOverridable : Overridable<float>
    {
        public float min;

        public override float Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = Mathf.Max(value, min);
            }
        }

        public MinFloatOverridable(float _value, float min, bool _overrideValue) : base(_value, _overrideValue)
        {
            this.min = min;
        }
    }
    
    [Serializable]
    public class MaxFloatOverridable : Overridable<float>
    {
        public float max;

        public override float Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = Mathf.Min(value, max);
            }
        }

        public MaxFloatOverridable(float _value, float max, bool _overrideValue) : base(_value, _overrideValue)
        {
            this.max = max;
        }
    }
    [Serializable]
    public class FloatRangeOverridable : Overridable<float>
    {
        public float min;
        public float max;

        public override float Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = Mathf.Clamp(value, min, max);
            }
        }

        public FloatRangeOverridable(float _value, float min, float max, bool _overrideValue) : base(_value, _overrideValue)
        {
            this.min = min;
            this.max = max;
        }
    }
}