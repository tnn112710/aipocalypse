//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    [System.Serializable]
    public class LayerData
    {
        public string name;
        public List<FeatureData> features = new List<FeatureData>();
        public bool hasShader;
        public LayerData(string name, bool hasShader = true)
        {
            this.name = name;
            this.hasShader = hasShader;
            features = new List<FeatureData>();
        }

        public LayerData Has(FeatureData feature)
        {
            features ??= new List<FeatureData>();

            features.Add(feature);
            return this;
        }
    }
}