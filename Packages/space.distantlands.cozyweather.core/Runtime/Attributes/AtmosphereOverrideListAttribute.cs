//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Custom property attribute for atmosphere overrides. Used for the drawer to recognize a list of overrides before drawing. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class AtmosphereOverridesListAttribute : PropertyAttribute
    {
        public AtmosphereOverridesListAttribute() : base(true) { }
    }
}