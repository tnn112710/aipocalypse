using System;
using UnityEngine;

namespace DistantLands.Cozy.Blocks
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class BlockOverridesListAttribute : PropertyAttribute
    {
        public BlockOverridesListAttribute() : base(true) { }
    }
}
