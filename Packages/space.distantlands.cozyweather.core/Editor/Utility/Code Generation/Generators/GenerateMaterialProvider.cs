//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using System.Text;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateMaterialProvider : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.core/Editor/Utility/Code Generation/Templates/MaterialProviderTemplate.txt";
        private const string OUTPUT_PATH = "Packages/space.distantlands.cozyweather.core/Runtime/Utility";

        public void Generate(LayerData[] data)
        {
            if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH))
                return;

            StringBuilder materialsBuilder = new();
            StringBuilder keywordsBuilder = new();
            HashSet<string> generatedMaterials = new();

            foreach (LayerData layer in data)
            {
                if (!layer.hasShader) continue;

                AppendMaterialProperty(materialsBuilder, generatedMaterials, layer.name);

                foreach (FeatureData feature in layer.features)
                {
                    if (feature == null || !feature.featureUsage.HasFlag(FeatureUsage.ToggleOnly))
                        continue;

                    string materialName = string.IsNullOrEmpty(feature.shaderName) ? layer.name : feature.shaderName;
                    AppendMaterialProperty(materialsBuilder, generatedMaterials, materialName);
                    keywordsBuilder.AppendLine($"            if ({materialName}Material != null) {materialName}Material.SetKeyword(CozyShaderIDs.use{layer.name}{feature.name}, CozyPreferences.CurrentStyle.Use{layer.name}{feature.name});");
                }
            }

            GenerateVariableStack.Replace(ref fileContent, ("/*__MATERIALS__*/", materialsBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__KEYWORDS__*/", keywordsBuilder.ToString()));
            GenerateVariableStack.WriteFile(fileContent, OUTPUT_PATH, "CozyMaterialProvider", "cs");
        }

        private static void AppendMaterialProperty(StringBuilder builder, HashSet<string> generatedMaterials, string materialName)
        {
            if (!generatedMaterials.Add(materialName)) return;

            string fieldName = char.ToLowerInvariant(materialName[0]) + materialName.Substring(1);
            builder.AppendLine($"        public static Material {materialName}Material");
            builder.AppendLine("        {");
            builder.AppendLine("            get");
            builder.AppendLine("            {");
            builder.AppendLine($"                if ({fieldName}Material == null)");
            builder.AppendLine($"                    {fieldName}Material = Resources.Load<Material>(\"Materials/COZY {materialName}\");");
            builder.AppendLine($"                return {fieldName}Material;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine($"        private static Material {fieldName}Material;");
        }
    }
}
