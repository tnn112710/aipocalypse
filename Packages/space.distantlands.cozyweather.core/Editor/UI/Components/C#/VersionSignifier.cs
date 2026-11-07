//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [UxmlElement]
    public partial class VersionSignifier : VisualElement
    {
        public VersionSignifier()
        {
            Init();
        }

        private Label Label => this.Q<Label>("version-number");

        public void Init(
        )
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Components/UXML/version-signifier.uxml"
            );

            asset.CloneTree(this);

            if (AssetInformation.INSTALLED_VERSION == null || AssetInformation.UAS_VERSION == null)
                return;

            if (AssetInformation.INSTALLED_VERSION > AssetInformation.UAS_VERSION)
            {
                styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/space.distantlands.cozyweather.core/Editor/UI/Stylesheets/console-dev.uss"
                ));
                tooltip = "Development Version";
            }
            else if (AssetInformation.INSTALLED_VERSION == AssetInformation.UAS_VERSION)
            {
                styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/space.distantlands.cozyweather.core/Editor/UI/Stylesheets/console-info.uss"
                ));
                tooltip = "Up to Date";
            }
            else
            {
                styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/space.distantlands.cozyweather.core/Editor/UI/Stylesheets/console-error.uss"
                ));
                tooltip = $"Current Version is v{AssetInformation.UAS_VERSION}";
            }

            Label.text = $"v{AssetInformation.INSTALLED_VERSION}";
        }

    }
}