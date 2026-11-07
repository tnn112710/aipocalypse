//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Text;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateAtmosphereModel : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.core/Editor/Utility/Code Generation/Templates/AtmosphereModelTemplate.txt";
        private const string OUTPUT_PATH = "Packages/space.distantlands.cozyweather.core/Runtime/Data/Parameters";

        public void Generate(LayerData[] data)
        {
            if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH))
                return;

            StringBuilder variableSectionBuilder = new();
            StringBuilder resetSectionBuilder = new();
            StringBuilder updateSectionBuilder = new();
            StringBuilder updateSectionByProfileBuilder = new();
            StringBuilder applySectionBuilder = new();
            StringBuilder applyMaxWeightSectionBuilder = new();

            foreach (var layer in data)
            {
                variableSectionBuilder.AppendLine($"        #region {layer.name}");
                foreach (var feature in layer.features)
                {
                    if (feature == null || !feature.featureUsage.HasFlag(FeatureUsage.OverrideOnly)) continue;
                    variableSectionBuilder.AppendLine($"        // {feature.name}");
                    foreach (var property in feature.variables)
                    {
                        string varName = GenerateVariableStack.VariableName($"{layer.name}{feature.name}{property.Name}");

                        //Create variable declaration body
                        variableSectionBuilder.AppendLine("        /// <summary>");
                        variableSectionBuilder.AppendLine($"        /// {property.Tooltip}");
                        variableSectionBuilder.AppendLine("        /// </summary>");
                        variableSectionBuilder.AppendLine($"        [Tooltip(\"{property.Tooltip}\")]");
                        variableSectionBuilder.AppendLine($"        public AtmosphereProperty<{property.DeclareCSharpProperty()}> {varName};");

                        if (!string.IsNullOrEmpty(property.PresetValue))
                        {
                            updateSectionBuilder.AppendLine($"              {varName} = {property.PresetValue};");
                            updateSectionByProfileBuilder.AppendLine($"              {varName} = {property.PresetValue};");
                        }

                        if (!string.IsNullOrEmpty(property.DefaultValue))
                            resetSectionBuilder.AppendLine($"               {varName} = Constant<{property.DeclareCSharpProperty()}>(defaultReferences.default{layer.name}{feature.name}{property.Name});");
                        else if (!string.IsNullOrEmpty(property.DisabledValueString)) 
                            resetSectionBuilder.AppendLine($"               {varName} = Disabled<{property.DeclareCSharpProperty()}>({property.DisabledValueString});");


                        applySectionBuilder.AppendLine($"            {varName}.Lerp(ref parameters.{varName}, {varName}.GetValue(parameters.{varName}, time), weight);");
                        applyMaxWeightSectionBuilder.AppendLine($"                  parameters.{varName} = {varName}.GetValue(parameters.{varName}, time);");

                    }

                    variableSectionBuilder.AppendLine();
                }
                variableSectionBuilder.AppendLine("        #endregion");
                variableSectionBuilder.AppendLine();
            }

            GenerateVariableStack.Replace(ref fileContent, ("/*__VARIABLES__*/", variableSectionBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__RESET_FUNCTION__*/", resetSectionBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__UPDATE_FUNCTION__*/", updateSectionBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__UPDATE_BY_PROFILE_FUNCTION__*/", updateSectionByProfileBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__APPLY_FUNCTION__*/", applySectionBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__APPLY_FUNCTION_WEIGHT_1__*/", applyMaxWeightSectionBuilder.ToString()));

            GenerateVariableStack.WriteFile(fileContent, OUTPUT_PATH, "AtmosphereModel", "cs");

        }
    }
}