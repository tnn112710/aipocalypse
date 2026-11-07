//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Text;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateAtmosphereParameters : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.core/Editor/Utility/Code Generation/Templates/AtmosphereParametersTemplate.txt";
        private const string OUTPUT_PATH = "Packages/space.distantlands.cozyweather.core/Runtime/Data/Parameters";
        
        public void Generate(LayerData[] data)
        {
            if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH))
                return;

            StringBuilder variableSectionBuilder = new();
            StringBuilder resetSectionBuilder = new();
            StringBuilder copySectionBuilder = new();
            StringBuilder blendSectionBuilder = new();

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
                        variableSectionBuilder.AppendLine($"        public {property.DeclareCSharpProperty()} {varName};");

                        //Create reset function body
                        if (property.DisabledValueString != "")
                        {
                            resetSectionBuilder.AppendLine($"            {varName}.value = {property.DisabledValueString};");
                        }

                        //Create copy function body
                        copySectionBuilder.AppendLine($"            {varName}.value = parameters.{varName}.value;");

                        //Create blend function body
                        blendSectionBuilder.AppendLine($"             {varName}.Lerp(parameters.{varName}, weight);");
                    }

                    variableSectionBuilder.AppendLine();
                }
                variableSectionBuilder.AppendLine("        #endregion");
                variableSectionBuilder.AppendLine();
            }

            GenerateVariableStack.Replace(ref fileContent, ("/*__VARIABLES__*/", variableSectionBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__RESET_FUNCTION__*/", resetSectionBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__COPY_FUNCTION__*/", copySectionBuilder.ToString()));
            GenerateVariableStack.Replace(ref fileContent, ("/*__BLEND_FUNCTION__*/", blendSectionBuilder.ToString()));

            GenerateVariableStack.WriteFile(fileContent, OUTPUT_PATH, "AtmosphereParameters", "cs");

        }
    }
}