using UnityEngine;
using System;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Empty attribute for a list of FX Profiles
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class FXAttribute : PropertyAttribute
    {
        
    }
}