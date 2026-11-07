//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Text;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateHLSL : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.core/Editor/Utility/Code Generation/Templates/HLSLTemplate.txt";
        private const string OUTPUT_PATH = "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes";

        public void Generate(LayerData[] data)
        {
            if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH))
                return;

            foreach (var layer in data)
            {
                string layerContent = fileContent;

                StringBuilder hlslBuilder = new();

                int count = 0;

                foreach (var feature in layer.features)
                {
                    if (!((feature.featureUsage.HasFlag(FeatureUsage.ToggleOnly) && layer.hasShader) || feature.featureUsage.HasFlag(FeatureUsage.HLSLOnly)))
                        continue;

                    hlslBuilder.AppendLine();
                    hlslBuilder.AppendLine($"// {layer.name} {feature.name}");

                    if (feature.featureUsage.HasFlag(FeatureUsage.ToggleOnly))
                        hlslBuilder.AppendLine($"#pragma shader_feature _ CZY_USE_{layer.name.ToUpper()}{feature.name.ToUpper()}");

                    if (feature.featureUsage.HasFlag(FeatureUsage.HLSLOnly))
                    {
                        foreach (var property in feature.variables)
                        {
                            hlslBuilder.AppendLine(property.DeclareHLSLProperty($"CZY_{layer.name}_{feature.name}_{property.Name}"));
                        }

                        hlslBuilder.AppendLine();
                    }
                    count++;
                }

                if (count == 0) continue;

                GenerateVariableStack.Replace(ref layerContent, ("/*__SHADERVARS__*/", hlslBuilder.ToString()));
                GenerateVariableStack.Replace(ref layerContent, ("/*__FEATURENAME__*/", $"COZY{layer.name}Variables"));

                GenerateVariableStack.WriteFile(layerContent, OUTPUT_PATH, $"Cozy{layer.name}Variables", "hlsl");
            }
        }
    }
}