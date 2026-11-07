//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Linq;
using System.Text;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateFeatures : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.core/Editor/Utility/Code Generation/Templates/StyleTemplate.txt";
        private const string OUTPUT_PATH = "Packages/space.distantlands.cozyweather.core/Runtime/Utility/Preferences";

        public void Generate(LayerData[] data)
        {
            if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH))
                return;

            StringBuilder featuresBuilder = new();

            foreach (var layer in data)
            {
                if (!layer.features.Any(x => x.featureUsage.HasFlag(FeatureUsage.ToggleOnly))) continue;
                featuresBuilder.AppendLine();
                featuresBuilder.AppendLine($"        public bool Use{layer.name} => use{layer.name};");
                featuresBuilder.AppendLine($"        [SerializeField]");
                featuresBuilder.AppendLine($"        private bool use{layer.name};");
                featuresBuilder.AppendLine("");

                foreach (var feature in layer.features)
                {
                    if (feature != null && feature.featureUsage.HasFlag(FeatureUsage.ToggleOnly))
                    {
                        featuresBuilder.AppendLine($"        public bool Use{layer.name}{feature.name} => use{layer.name} && use{layer.name}{feature.name};");
                        featuresBuilder.AppendLine($"        [SerializeField]");
                        featuresBuilder.AppendLine($"        private bool use{layer.name}{feature.name};");
                        featuresBuilder.AppendLine($"        public void Toggle{layer.name}{feature.name}() " + "{");
                        featuresBuilder.AppendLine($"            use{layer.name}{feature.name} = !use{layer.name}{feature.name};");
                        featuresBuilder.AppendLine($"            if (CozyWeather.Instance) CozyWeather.Instance.ResetQuality();");
                        featuresBuilder.AppendLine("         }");
                    }
                }
            }

            GenerateVariableStack.Replace(ref fileContent, ("/*__FEATURES__*/", featuresBuilder.ToString()));
            GenerateVariableStack.WriteFile(fileContent, OUTPUT_PATH, "CozyStyle", "cs");
        }
    }
}