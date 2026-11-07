using System;
using UnityEngine;

namespace DistantLands.Cozy.Eclipse
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class EclipseOverridesListAttribute : PropertyAttribute
    {
        public EclipseOverridesListAttribute() : base(true) { }
    }
}
