//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary
    /// Runs when the project opens to validate the default atmosphere references resource. 
    /// </summary>
    [InitializeOnLoad]
    public class ResetDefaultAtmosphereValues
    {
        static ResetDefaultAtmosphereValues()
        {
            DefaultAtmosphereReferences references = Resources.Load<DefaultAtmosphereReferences>("Default COZY Atmosphere References");
            references.AssignDefaults();
        }
    }
}