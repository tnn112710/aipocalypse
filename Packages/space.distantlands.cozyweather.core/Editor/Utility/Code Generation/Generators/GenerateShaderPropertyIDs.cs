//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Text;
using System.Collections.Generic;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateShaderPropertyIDs : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.core/Editor/Utility/Code Generation/Templates/ShaderIDsTemplate.txt";
        private const string OUTPUT_PATH = "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Utility";

        public void Generate(LayerData[] data)
        {
            if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH))
                return;

            StringBuilder propertiesBuilder = new();
            StringBuilder grabIdsBuilder = new();
            HashSet<string> generatedShaders = new();

            foreach (var layer in data)
            {
                if (layer.hasShader)
                    AppendShaderProperty(propertiesBuilder, generatedShaders, layer.name);

                grabIdsBuilder.AppendLine($"            // {layer.name}");

                foreach (var feature in layer.features)
                {
                    if (feature == null) continue;

                    grabIdsBuilder.AppendLine($"            // {feature.name}");

                    if (layer.hasShader && feature.featureUsage.HasFlag(FeatureUsage.ToggleOnly))
                    {
                        string shaderName = string.IsNullOrEmpty(feature.shaderName) ? layer.name : feature.shaderName;
                        AppendShaderProperty(propertiesBuilder, generatedShaders, shaderName);
                        propertiesBuilder.AppendLine($"        public static LocalKeyword use{layer.name}{feature.name};");
                        grabIdsBuilder.AppendLine($"            use{layer.name}{feature.name} = new LocalKeyword({shaderName}Shader, \"CZY_USE_{layer.name.ToUpperInvariant()}{feature.name.ToUpperInvariant()}\");");
                    }

                    if (feature.featureUsage.HasFlag(FeatureUsage.HLSLOnly))
                        foreach (var property in feature.variables)
                        {
                            string varName = GenerateVariableStack.VariableName($"{layer.name}{feature.name}{property.Name}");
                            propertiesBuilder.AppendLine($"        public static int {varName};");
                            grabIdsBuilder.AppendLine($"            {varName} = Shader.PropertyToID(\"CZY_{layer.name}_{feature.name}_{property.Name}\");");
                        }

                    grabIdsBuilder.AppendLine();
                }
                grabIdsBuilder.AppendLine();
            }

            GenerateVariableStack.Replace(ref fileContent, ("/*__PROPERTIES__*/", propertiesBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__GRAB_IDS__*/", grabIdsBuilder.ToString()));
            GenerateVariableStack.WriteFile(fileContent, OUTPUT_PATH, "CozyShaderIDs", "cs");
        }

        private static void AppendShaderProperty(StringBuilder builder, HashSet<string> generatedShaders, string shaderName)
        {
            if (!generatedShaders.Add(shaderName)) return;

            string fieldName = shaderName.ToLowerInvariant();
            builder.AppendLine($"        public static Shader {shaderName}Shader");
            builder.AppendLine("        {");
            builder.AppendLine("            get");
            builder.AppendLine("            {");
            builder.AppendLine($"                if ({fieldName}Shader == null)");
            builder.AppendLine($"                    {fieldName}Shader = (Resources.Load(\"Materials/COZY {shaderName}\") as Material).shader;");
            builder.AppendLine($"                return {fieldName}Shader;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine($"        private static Shader {fieldName}Shader;");
        }
    }
}