//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    public class CozyBiomeModuleEditor : CozyModuleEditor
    {
        public CozyBiome biome;
        public CozyBiomeEditor biomeEditor;

        public virtual VisualElement DisplayBiomeUI()
        {
            return DisplayUI();
        }

        public override void RemoveModule()
        {
            CozyModule mod = (CozyModule)target;
            mod.System.RemoveModule(target.GetType());
            if (mod.System is CozyBiome)
                biomeEditor.RemoveModule();
            else
                weatherEditor.RemoveModule();
        }
    }
}