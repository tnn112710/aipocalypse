using System.Linq;
using System.Text;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateEclipseOverrides : IGeneration
    {
        private const string TemplatePath = "Packages/space.distantlands.cozyweather.eclipse/Editor/Utility/Code Generation/Templates/EclipseOverrideTemplate.txt";
        private const string OutputDirectory = "Packages/space.distantlands.cozyweather.eclipse/Runtime/Data/Overrides";

        public void Generate(LayerData[] data)
        {
            foreach (LayerData layer in data)
            {
                if (!layer.features.Any(feature =>
                        feature != null && feature.featureUsage.HasFlag(FeatureUsage.OverrideOnly)))
                {
                    continue;
                }

                if (!GenerateVariableStack.ReadFile(out string fileContent, TemplatePath))
                    continue;

                StringBuilder overridesBuilder = new StringBuilder();

                foreach (var feature in layer.features)
                {
                    if (feature == null || !feature.featureUsage.HasFlag(FeatureUsage.OverrideOnly))
                        continue;

                    StringBuilder propertiesBuilder = new StringBuilder();
                    StringBuilder captureBuilder = new StringBuilder();
                    StringBuilder applyBuilder = new StringBuilder();

                    overridesBuilder.AppendLine($"    [System.Serializable]");
                    overridesBuilder.AppendLine($"    public class {layer.name}{feature.name}EclipseOverride : EclipseOverride");
                    overridesBuilder.AppendLine("    {");
                    overridesBuilder.AppendLine($"        public override string Title => \"{feature.name}\";");
                    overridesBuilder.AppendLine($"        public override string Category => \"{layer.name}\";");

                    if (feature.featureUsage.HasFlag(FeatureUsage.ToggleOnly))
                    {
                        overridesBuilder.AppendLine($"        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.Use{layer.name}{feature.name};");
                        overridesBuilder.AppendLine($"        public override void ToggleFeature() => CozyPreferences.CurrentStyle.Toggle{layer.name}{feature.name}();");
                    }

                    captureBuilder.AppendLine("        public override bool CaptureFromParameters(AtmosphereParameters parameters) {");
                    applyBuilder.AppendLine("        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {");
                    applyBuilder.AppendLine("            if (!active || weight <= 0f) return;");

                    if (feature.variables != null)
                    {
                        foreach (var property in feature.variables)
                        {
                            if (property.TypeValueString != "Float" &&
                                property.TypeValueString != "Color" &&
                                property.TypeValueString != "Vector3" &&
                                property.TypeValueString != "Bool")
                            {
                                continue;
                            }

                            string variableName = GenerateVariableStack.VariableName(property.Name);
                            string parameterName = GenerateVariableStack.VariableName($"{layer.name}{feature.name}{property.Name}");
                            string propertyType = property.DeclareCSharpProperty();
                            string eclipseType = $"Eclipse{propertyType}";
                            string attributeArguments = property.AdditionalAttributeProperties == ""
                                ? ""
                                : $", {property.AdditionalAttributeProperties}";

                            propertiesBuilder.AppendLine($"        [Tooltip(\"{property.Tooltip}\")]");
                            propertiesBuilder.AppendLine($"        [EclipseProperty(VarType.{property.TypeValueString}, \"{property.Name}\"{attributeArguments})]");
                            propertiesBuilder.AppendLine($"        public {eclipseType} {variableName} = new {eclipseType}(default);");

                            captureBuilder.AppendLine($"            {variableName} = new {eclipseType}(parameters.{parameterName});");
                            applyBuilder.AppendLine($"            if (!{variableName}.disabled)");
                            applyBuilder.AppendLine($"                parameters.{parameterName}.Lerp({variableName}.value, weight);");
                        }
                    }

                    captureBuilder.AppendLine($"            return {(propertiesBuilder.Length > 0 ? "true" : "false")};");
                    captureBuilder.AppendLine("        }");
                    applyBuilder.AppendLine("        }");

                    overridesBuilder.Append(propertiesBuilder);
                    overridesBuilder.AppendLine();
                    overridesBuilder.Append(captureBuilder);
                    overridesBuilder.AppendLine();
                    overridesBuilder.Append(applyBuilder);
                    overridesBuilder.AppendLine("    }");
                    overridesBuilder.AppendLine();
                }

                GenerateVariableStack.Replace(ref fileContent, ("/*__OVERRIDES__*/", overridesBuilder.ToString()));
                GenerateVariableStack.WriteFile(fileContent, OutputDirectory, $"{layer.name}EclipseOverrides", "cs");
            }
        }
    }
}
