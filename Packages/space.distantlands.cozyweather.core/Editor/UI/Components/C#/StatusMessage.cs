//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine.UIElements;


namespace DistantLands.Cozy.EditorScripts
{
    [UxmlElement]
    public partial class StatusMessage : VisualElement
    {
        public StatusMessage()
        {
            Init();
        }
        public void Init()
        {
            RegisterCallback<AttachToPanelEvent>(e =>
                {
                    Clear();

                    AddToClassList("statusMessage");

                    VisualElement icon = new VisualElement
                    {
                        name = "icon"
                    };
                    Add(icon);

                    Label versionLabel = new Label
                    {
                        name = "versionLabel"
                    };
                    versionLabel.text = VersionChecking.InstalledVersion;
                    Add(versionLabel);

                    RegisterCallback<ClickEvent>(evt =>
                    {
                        CozyProjectSetup.ShowSetupWizard();
                    });

                    var projectSetupStatus = CozyProjectSetup.CheckForSetup();
                    if (projectSetupStatus == StatusType.Fail || projectSetupStatus == StatusType.Warning)
                    {
                        AddToClassList("warning");
                        icon.tooltip = "Project is not fully setup for use with COZY 4. Click here to run setup checks.";
                    }

                    switch (VersionChecking.VersionStatus)
                    {
                        case -2:
                            versionLabel.style.display = DisplayStyle.None;
                            break;
                        case -1:
                            AddToClassList("legacy");
                            break;
                        case 0:
                            AddToClassList("upToDate");
                            break;
                        case 1:
                            AddToClassList("dev");
                            break;
                    }
                });
        }
    }
}