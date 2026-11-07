//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Text;
using UnityEditor;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateDefaultReferences : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.core/Editor/Utility/Code Generation/Templates/DefaultReferencesTemplate.txt";
        private const string OUTPUT_PATH = "Packages/space.distantlands.cozyweather.core/Runtime/Profiles";

        public void Generate(LayerData[] data)
        {
            if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH))
                return;

            StringBuilder variableSectionBuilder = new();
            StringBuilder assignmentSectionBuilder = new();

            foreach (var layer in data)
            {
                foreach (var feature in layer.features)
                {
                    if (feature == null || !feature.featureUsage.HasFlag(FeatureUsage.OverrideOnly)) continue;
                    foreach (var property in feature.variables)
                    {
                        if (string.IsNullOrEmpty(property.DefaultValue)) continue;
                        string varName = $"default{layer.name}{feature.name}{property.Name}";

                        variableSectionBuilder.AppendLine($"        public {property.TypeReferenceString} {varName};");

                        assignmentSectionBuilder.AppendLine($"          {varName} = ({property.TypeReferenceString})AssetDatabase.LoadAssetAtPath(\"{property.DefaultValue}\", typeof({property.TypeReferenceString}));");
                    }
                }
            }

            GenerateVariableStack.Replace(ref fileContent, ("/*__VARIABLES__*/", variableSectionBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__ASSIGNMENT__*/", assignmentSectionBuilder.ToString()));

            GenerateVariableStack.WriteFile(fileContent, OUTPUT_PATH, "AtmosphereDefaultReferences", "cs");

            AssetDatabase.Refresh();

            DefaultAtmosphereReferences asset = ScriptableObject.CreateInstance<DefaultAtmosphereReferences>();
            string path = "Packages/space.distantlands.cozyweather.core/Content/Resources/Default COZY Atmosphere References.asset";
            AssetDatabase.CreateAsset(asset, path);

            asset.AssignDefaults();
            EditorUtility.SetDirty(asset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }
    }
}