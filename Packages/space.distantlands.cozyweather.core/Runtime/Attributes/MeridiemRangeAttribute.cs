using UnityEngine;
using System;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Attribute for a time that fits within a preset range
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class MeridiemRangeAttribute : PropertyAttribute
    {
        public float min;
        public float max;

        public MeridiemRangeAttribute(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
    }
}