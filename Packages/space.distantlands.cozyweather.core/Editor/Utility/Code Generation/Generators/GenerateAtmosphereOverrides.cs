//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Text;
using System.Linq;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateAtmosphereOverrides : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.core/Editor/Utility/Code Generation/Templates/AtmosphereOverrideTemplate.txt";
        private const string OUTPUT_DIR = "Packages/space.distantlands.cozyweather.core/Runtime/Data/Atmosphere Overrides";

        public void Generate(LayerData[] data)
        {
            foreach (var layer in data)
            {
                if (!layer.features.Any(f => f.featureUsage.HasFlag(FeatureUsage.OverrideOnly))) continue;

                if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH))
                    continue;

                StringBuilder classesBuilder = new();

                foreach (var feature in layer.features)
                {
                    if (feature == null || !feature.featureUsage.HasFlag(FeatureUsage.OverrideOnly)) continue;

                    StringBuilder functionOutput = new();
                    StringBuilder copyModelFunctionOutput = new();
                    StringBuilder resetFunctionOutput = new();
                    StringBuilder applyToModelOutput = new();
                    StringBuilder biomeFunctionOutput = new();
                    StringBuilder propertiesBuilder = new();

                    classesBuilder.AppendLine($"    //{feature.name}");
                    classesBuilder.AppendLine($"    [System.Serializable]");
                    classesBuilder.AppendLine($"    public class {layer.name}{feature.name}Override : AtmosphereOverride");
                    classesBuilder.AppendLine("    {");
                    classesBuilder.AppendLine($"        public override string Title => \"{feature.name}\";");
                    classesBuilder.AppendLine($"        public override string Category => \"{layer.name}\";");

                    if (feature.featureUsage.HasFlag(FeatureUsage.ToggleOnly))
                    {
                        classesBuilder.AppendLine($"        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.Use{layer.name}{feature.name};");
                        classesBuilder.AppendLine($"        public override void ToggleFeature() => CozyPreferences.CurrentStyle.Toggle{layer.name}{feature.name}();");
                    }
                    classesBuilder.AppendLine();

                    functionOutput.AppendLine("        public override void ApplyToWeatherSphere(AtmosphereParameters parameters, float time) {");
                    functionOutput.AppendLine("            if (!active) return;");

                    copyModelFunctionOutput.AppendLine("        public override void CopyModel(AtmosphereModel model) {");

                    resetFunctionOutput.AppendLine("        public override void ResetToDefaults() {");
                    
                    applyToModelOutput.AppendLine("        public override void ApplyToModel(AtmosphereModel model) {");
                    applyToModelOutput.AppendLine("             if (!FeatureEnabledGlobally || !active) return;");

                    biomeFunctionOutput.AppendLine("        public override void ApplyAsBiome(AtmosphereParameters parameters, float time, float weight) {");
                    biomeFunctionOutput.AppendLine("            if (!active || weight == 0) return;");

                    foreach (var property in feature.variables)
                    {
                        string varName = GenerateVariableStack.VariableName($"{property.Name}");
                        string paramRefName = GenerateVariableStack.VariableName($"{layer.name}{feature.name}{property.Name}");
                        string propType = property.DeclareCSharpProperty();

                        propertiesBuilder.AppendLine("        /// <summary>");
                        propertiesBuilder.AppendLine($"        /// {property.Tooltip}");
                        propertiesBuilder.AppendLine("        /// </summary>");
                        propertiesBuilder.AppendLine("        [CozySearchable]");
                        propertiesBuilder.AppendLine($"        [Tooltip(\"{property.Tooltip}\")]");
                        string attr = property.AdditionalAttributeProperties == "" ? "" : $", {property.AdditionalAttributeProperties}";
                        propertiesBuilder.AppendLine($"        [AtmosphereProperty(VarType.{property.TypeValueString}, \"{property.Name}\"{attr})]");
                        propertiesBuilder.AppendLine($"        public AtmosphereProperty<{propType}> {varName};");

                        functionOutput.AppendLine($"            {varName}.SetValue(ref parameters.{paramRefName}, time);");
                        
                        applyToModelOutput.AppendLine($"            if ({varName}.overrideValue)");
                        applyToModelOutput.AppendLine($"                model.{paramRefName} = {varName};");
                        
                        copyModelFunctionOutput.AppendLine($"                {varName} = model.{paramRefName};");

                        // Add default value logic here adapted to your VariableData definitions
                        // ...
                        // biomeFunctionOutput
                        biomeFunctionOutput.AppendLine($"            parameters.{paramRefName}.Lerp({varName}.GetValue(parameters.{paramRefName}, time), weight);");
                    }

                    functionOutput.AppendLine("        }");
                    resetFunctionOutput.AppendLine("        }");
                    copyModelFunctionOutput.AppendLine("        }");
                    applyToModelOutput.AppendLine("        }");
                    biomeFunctionOutput.AppendLine("        }");

                    classesBuilder.Append(propertiesBuilder);
                    classesBuilder.AppendLine();
                    classesBuilder.Append(functionOutput);
                    classesBuilder.AppendLine();
                    classesBuilder.Append(resetFunctionOutput);
                    classesBuilder.AppendLine();
                    classesBuilder.Append(copyModelFunctionOutput);
                    classesBuilder.AppendLine();
                    classesBuilder.Append(biomeFunctionOutput);
                    classesBuilder.AppendLine();
                    classesBuilder.Append(applyToModelOutput);
                    classesBuilder.AppendLine("    }");
                    classesBuilder.AppendLine();
                }

                GenerateVariableStack.Replace(ref fileContent, ("/*__OVERRIDES__*/", classesBuilder.ToString()));
                GenerateVariableStack.WriteFile(fileContent, OUTPUT_DIR, $"{layer.name}Overrides", "cs");
            }
        }
    }
}