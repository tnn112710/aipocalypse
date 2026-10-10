using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using UnityEngine;

namespace Rowlan.Biomes_Presets_5
{
    public class TerrainMaterialReplacement : MonoBehaviour, IContentBrowserDropAction
    {
        public Material materialTemplate;

        public void Execute(out bool destroyAfterExecute)
        {
            Debug.Log($"Replacing terrain material with {materialTemplate} for all MicroVerse registered terrains");

            if(materialTemplate != null)
            {
                for (int i = 0; i < MicroVerse.instance.terrains.Length; ++i)
                {
                    Terrain terrain = MicroVerse.instance.terrains[i];
                    terrain.materialTemplate = materialTemplate;
                }
            }

            destroyAfterExecute = true;
        }
    }
}