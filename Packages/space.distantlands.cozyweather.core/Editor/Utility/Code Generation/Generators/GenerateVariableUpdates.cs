//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Text;
using System.IO;
using System.Linq;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateVariableUpdates : IGeneration
    {
        private const string TARGET_PATH = "Packages/space.distantlands.cozyweather.core/Runtime/Modules/CozyAtmosphereModule.cs";

        public void Generate(LayerData[] data)
        {
            StringBuilder sb = new();
            sb.AppendLine("// START GENERATED VARIABLE UPDATE");

            foreach (var layer in data)
            {
                if (!layer.features.Any(feature => feature.featureUsage.HasFlag(FeatureUsage.OverrideOnly) && feature.featureUsage.HasFlag(FeatureUsage.HLSLOnly)))
                    continue;

                sb.AppendLine($"            #region {layer.name}");

                sb.AppendLine($"            if (currentStyle.Use{layer.name})");
                sb.AppendLine("            {");

                foreach (var feature in layer.features)
                {
                    if (feature == null) continue;

                    if (!(feature.featureUsage.HasFlag(FeatureUsage.OverrideOnly) && feature.featureUsage.HasFlag(FeatureUsage.HLSLOnly))) continue;

                    sb.AppendLine($"            // {feature.name}");
                    sb.AppendLine($"            if (currentStyle.Use{layer.name}{feature.name})");
                    sb.AppendLine("            {");

                    foreach (var property in feature.variables)
                    {
                        string varName = GenerateVariableStack.VariableName($"{layer.name}{feature.name}{property.Name}");

                        string shaderSetter = property.SetGlobalShaderProperty(varName);

                        if (!string.IsNullOrEmpty(shaderSetter))
                            sb.AppendLine($"            if (parameters.{varName}.value != lastFrameParameters.{varName}.value) {shaderSetter}");
                    }
                    sb.AppendLine("            }");
                    sb.AppendLine();
                }

                sb.AppendLine("            }");
                sb.AppendLine("            #endregion");
                sb.AppendLine();
            }

            string replacement = sb.ToString();
            string startMarker = "// START GENERATED VARIABLE UPDATE";
            string endMarker = "// END GENERATED VARIABLE UPDATE";

            if (File.Exists(TARGET_PATH))
            {
                string content = File.ReadAllText(TARGET_PATH);
                int start = content.IndexOf(startMarker);
                int end = content.IndexOf(endMarker, start + startMarker.Length);

                if (start != -1 && end != -1)
                {
                    string newContent = content.Substring(0, start) + replacement + content.Substring(end);
                    File.WriteAllText(TARGET_PATH, newContent);
                }
            }
        }
    }
}