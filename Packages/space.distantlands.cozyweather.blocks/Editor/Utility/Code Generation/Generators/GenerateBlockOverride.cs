using System.Linq;
using System.Text;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateBlockOverrides : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.blocks/Editor/Utility/Code Generation/Templates/BlockOverrideTemplate.txt";
        private const string OUTPUT_DIR = "Packages/space.distantlands.cozyweather.blocks/Runtime/Data/Overrides";

        public void Generate(LayerData[] data)
        {
            foreach (var layer in data)
            {
                if (!layer.features.Any(f => f != null && f.featureUsage.HasFlag(FeatureUsage.OverrideOnly))) continue;

                if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH))
                    continue;

                StringBuilder classesBuilder = new();

                foreach (var feature in layer.features)
                {
                    if (feature == null || !feature.featureUsage.HasFlag(FeatureUsage.OverrideOnly)) continue;

                    StringBuilder functionOutput = new();
                    StringBuilder captureOutput = new();
                    StringBuilder propertiesBuilder = new();

                    classesBuilder.AppendLine($"    //{feature.name}");
                    classesBuilder.AppendLine($"    [System.Serializable]");
                    classesBuilder.AppendLine($"    public class {layer.name}{feature.name}Override : BlockOverride");
                    classesBuilder.AppendLine("    {");
                    classesBuilder.AppendLine($"        public override string Title => \"{feature.name}\";");
                    classesBuilder.AppendLine($"        public override string Category => \"{layer.name}\";");

                    if (feature.featureUsage.HasFlag(FeatureUsage.ToggleOnly))
                    {
                        classesBuilder.AppendLine($"        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.Use{layer.name}{feature.name};");
                        classesBuilder.AppendLine($"        public override void ToggleFeature() => CozyPreferences.CurrentStyle.Toggle{layer.name}{feature.name}();");
                    }
                    classesBuilder.AppendLine();

                    functionOutput.AppendLine("        public override void ApplyToWeatherSphere(AtmosphereParameters parameters, float weight) {");
                    functionOutput.AppendLine("            if (!active || !FeatureEnabledGlobally) return;");
                    captureOutput.AppendLine("        public override bool CaptureFromParameters(AtmosphereParameters parameters) {");

                    if (feature.variables != null)
                    {
                        foreach (var property in feature.variables)
                        {
                            if (property.TypeValueString != "Float" && property.TypeValueString != "Color" && property.TypeValueString != "Vector3" && property.TypeValueString != "Bool")
                                continue;

                            string varName = GenerateVariableStack.VariableName($"{property.Name}");
                            string paramRefName = GenerateVariableStack.VariableName($"{layer.name}{feature.name}{property.Name}");
                            string propType = property.DeclareCSharpProperty();

                            propertiesBuilder.AppendLine("        /// <summary>");
                            propertiesBuilder.AppendLine($"        /// {property.Tooltip}");
                            propertiesBuilder.AppendLine("        /// </summary>");
                            propertiesBuilder.AppendLine("        [CozySearchable]");
                            propertiesBuilder.AppendLine($"        [Tooltip(\"{property.Tooltip}\")]");
                            string attr = property.AdditionalAttributeProperties == "" ? "" : $", {property.AdditionalAttributeProperties}";
                            propertiesBuilder.AppendLine($"        [BlockProperty(VarType.{property.TypeValueString}, \"{property.Name}\"{attr})]");
                            propertiesBuilder.AppendLine($"        public Block{propType} {varName} = new Block{propType}(default);");

                            captureOutput.AppendLine($"            {varName} = new Block{propType}(parameters.{paramRefName});");
                            functionOutput.AppendLine($"            if (!{varName}.disabled)");
                            functionOutput.AppendLine($"                parameters.{paramRefName}.Lerp({varName}.value, weight);");
                        }
                    }

                    captureOutput.AppendLine($"            return {(propertiesBuilder.Length > 0 ? "true" : "false")};");
                    captureOutput.AppendLine("        }");
                    functionOutput.AppendLine("        }");

                    classesBuilder.Append(propertiesBuilder);
                    classesBuilder.AppendLine();
                    classesBuilder.Append(captureOutput);
                    classesBuilder.AppendLine();
                    classesBuilder.Append(functionOutput);
                    classesBuilder.AppendLine("    }");
                    classesBuilder.AppendLine();
                }

                GenerateVariableStack.Replace(ref fileContent, ("/*__OVERRIDES__*/", classesBuilder.ToString()));
                GenerateVariableStack.WriteFile(fileContent, OUTPUT_DIR, $"{layer.name}Overrides", "cs");
            }
        }
    }
}