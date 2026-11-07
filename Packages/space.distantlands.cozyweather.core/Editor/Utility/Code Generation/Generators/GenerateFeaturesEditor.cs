//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Text;
using System.IO;
using System.Linq;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    public class GenerateFeaturesEditor : IGeneration
    {
        private const string TARGET_PATH = "Packages/space.distantlands.cozyweather.core/Editor/UI/Window/C#/CozyStyleEditor.cs";

        public void Generate(LayerData[] data)
        {
            StringBuilder sb = new();
            sb.AppendLine("// START GENERATED CODE");

            foreach (var layer in data)
            {
                if (!layer.features.Any(x => x.featureUsage.HasFlag(FeatureUsage.ToggleOnly))) continue;
                StringBuilder layerString = new();

                foreach (var feature in layer.features)
                {
                    if (feature == null || !feature.featureUsage.HasFlag(FeatureUsage.ToggleOnly)) continue;
                    
                    layerString.Append($", (\"{feature.name}\", \"{feature.description}\", PerformanceOverhead.{feature.overhead.ToString()})");
                }

                if (layerString.Length > 0)
                {
                    sb.AppendLine($"            #region {layer.name}");
                    sb.AppendLine($"            DrawLayerHeader(serializedObject.FindProperty(\"use{layer.name}\"), \"{layer.name}\"{layerString});");
                    sb.AppendLine("            #endregion");
                }
            }

            string replacement = sb.ToString();
            string startMarker = "// START GENERATED CODE";
            string endMarker = "// END GENERATED CODE";

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