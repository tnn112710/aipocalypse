//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Text;
using System.Linq;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateOverrideSearchProvider : IGeneration
    {
        private const string TEMPLATE_PATH = "Packages/space.distantlands.cozyweather.core/Editor/Utility/Code Generation/Templates/AtmosphereOverrideSearchProvider.txt";
        private const string OUTPUT_DIR = "Packages/space.distantlands.cozyweather.core/Editor";

        public void Generate(LayerData[] data)
        {
            StringBuilder inspectorCode = new();

            if (!GenerateVariableStack.ReadFile(out string fileContent, TEMPLATE_PATH)) return;

            foreach (var layer in data)
            {
                if (layer.features.Any(x => x.featureUsage.HasFlag(FeatureUsage.OverrideOnly)))
                    inspectorCode.AppendLine($"                entries.Add(new SearchTreeGroupEntry(new GUIContent(\"{GenerateVariableStack.DisplayName(layer.name)}\"), 1));");

                foreach (var feature in layer.features)
                {
                    if (feature == null || !feature.featureUsage.HasFlag(FeatureUsage.OverrideOnly)) continue;

                    inspectorCode.AppendLine($"            if (!overridesList.Any(x => x is {layer.name + feature.name}Override))");
                    inspectorCode.AppendLine($"                entries.Add(new SearchTreeEntry(new GUIContent(\"    {GenerateVariableStack.DisplayName(feature.name)}\"))" + "{ userData = new " + $"{layer.name + feature.name}Override" + "(), level=2});");
                }
            }

            GenerateVariableStack.Replace(ref fileContent, ("/*__OVERRIDES__*/", inspectorCode.ToString()));
            GenerateVariableStack.WriteFile(fileContent, OUTPUT_DIR, $"AtmosphereOverrideSearchProvider", "cs");
        }
    }
}