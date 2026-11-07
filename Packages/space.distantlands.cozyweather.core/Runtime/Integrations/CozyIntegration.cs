//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Create a managed integration with COZY 4. See https://distant-lands.gitbook.io/cozy-weather-4/extending-cozy/build-an-integration
    /// </summary>
    [Serializable]
    public abstract class CozyIntegration
    {
        /// <summary>
        /// Checks whether the package this integration is for is installed in the project
        /// </summary>
        public abstract bool PackageInstalled { get; }
        /// <summary>
        /// Checks whether the integration is installed. If the integration makes no changes to third party packages, simply return true always. 
        /// Otherwise, ensure that the changes are made and then return true.
        /// </summary>
        public abstract bool IntegrationInstalled { get; }
        /// <summary>
        /// Runs all checks to see if the integration is functioning properly at runtime. Use this function to check if the needed components are available in the scene. 
        /// Use the error message to explain how to resolve the issue.
        /// </summary>
        public abstract StatusType CheckStatus(out string message);

        public StatusType CheckStatus()
        {
            return CheckStatus(out _);
        }
        /// <summary>
        /// Is the integration installed, enabled and active
        /// </summary>
        public bool ActiveAndEnabled => PackageInstalled && enabled;
        internal bool enabled = true;
        /// <summary>
        /// Is the integration enabled in the config
        /// </summary>
        public bool Enabled
        {
            get => enabled;
            set
            {
                if (value != enabled)
                {
                    if (value)
                        Initialize();
                    else
                        Deinitialize();
                }
                enabled = value;
            }
        }

        /// <summary>
        /// Toggle whether the integration is active or not
        /// </summary>
        /// <param name="enabled">Applies the integration when true</param>
        public void SetEnabled(bool enabled) => Enabled = enabled;

        /// <summary>
        /// Runs when the game starts
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Runs when the game closes
        /// </summary>
        public abstract void Deinitialize();

        /// <summary>
        /// Apply the integration. Make shader changes, add scripting defines, etc.
        /// </summary>
        public abstract void ApplyIntegration();

        /// <summary>
        /// Integrations should NEVER make changes that cannot be revoked. Remove any changes made in the ApplyIntegration method
        /// </summary>
        public abstract void UnapplyIntegration();
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class CozyIntegrationAttribute : Attribute
    {
        readonly string name;
        readonly string iconPath;
        readonly string packageLink;

        readonly bool displayToggle;
        readonly bool displayApplyButton;

        public CozyIntegrationAttribute(bool displayToggle, bool displayApplyButton, string name, string iconPath, string packageLink)
        {
            this.displayToggle = displayToggle;
            this.displayApplyButton = displayApplyButton;
            this.name = name;
            this.iconPath = iconPath;
            this.packageLink = packageLink;
        }

        public string Name
        {
            get { return name; }
        }

        public string IconPath
        {
            get { return iconPath; }
        }

        public string PackageLink
        {
            get { return packageLink; }
        }
        public bool DisplayToggle
        {
            get { return displayToggle; }
        }
        public bool DisplayApplyButton
        {
            get { return displayApplyButton; }
        }
    }

}