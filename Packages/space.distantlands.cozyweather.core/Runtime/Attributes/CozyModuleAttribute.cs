using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Required for any class deriving from CozyModule.cs
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CozyModuleAttribute : Attribute
    {
        /// <summary>
        /// Required for any class deriving from CozyModule.cs
        /// </summary>
        /// <param name="name">The name of the COZY Module</param>
        /// <param name="icon">The name of the icon. Must be placed in a Resources/COZY Icons folder (for example, Assets/Resources/COZY Icons/MyModule).</param>
        /// <param name="tooltip">The tooltip/description for the module.</param>
        /// <param name="category">
        /// The title for the category that this module is in in the dropdown. This is case sensitive!
        /// Base COZY modules are in these categories, however you can create your own when needed:<br/>
        ///     - Atmosphere<br/>
        ///     - Time<br/>
        ///     - Ecosystem<br/>
        ///     - Integration<br/>
        ///     - Utility<br/>
        /// </param>
        public CozyModuleAttribute(string name = "New COZY Module", string icon = "", string tooltip = "", string category = "Other", bool biomeCompatible = false,  int priority = 0)
        {
            this.name = name;
            this.icon = icon;
            this.tooltip = tooltip;
            this.category = category;
            this.biomeCompatible = biomeCompatible;
        }

        /// <summary>
        /// The name of the COZY Module
        /// </summary>
        public string name;

        /// <summary>
        /// The name of the icon. Must be placed in a Resources/COZY Icons folder (for example, Assets/Resources/COZY Icons/MyModule).
        /// </summary>
        public string icon;

        /// <summary>
        /// The tooltip/description for the module.
        /// </summary>
        public string tooltip;

        /// <summary>
        /// The title for the category that this module is in in the dropdown. This is case sensitive!
        /// Base COZY modules are in these categories, however you can create your own when needed:<br/>
        ///     - Atmosphere<br/>
        ///     - Time<br/>
        ///     - Ecosystem<br/>
        ///     - Integration<br/>
        ///     - Utility<br/>
        /// </summary>
        public string category;

        /// <summary>
        /// Set the priority for executing logic on this module.
        /// </summary>
        public int priority = 0;

        /// <summary>
        /// Determines if this module can be added to biomes
        /// </summary>
        public bool biomeCompatible;

        /// <summary>
        /// Loads an icon based on a key from Resources/COZY Icons/
        /// </summary>
        public static Sprite LoadIcon(string icon)
        {
            return Resources.Load<Sprite>($"COZY Icons/{icon}");
        }
    }
}