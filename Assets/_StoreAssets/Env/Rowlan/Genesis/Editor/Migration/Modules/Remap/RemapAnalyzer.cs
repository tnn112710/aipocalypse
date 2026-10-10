using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Migration
{
    public class RemapAnalyzer
    {
        private List<PresetItem> filteredPresets;

        private Dictionary<string, AssetData> assetDataMap = new Dictionary<string, AssetData>();

        public RemapAnalyzer(List<PresetItem> filteredPresets)
        {
            this.filteredPresets = filteredPresets;

            Analyze();
        }

        public List<AssetData> GetAssetData()
        {
            return assetDataMap.Values.ToList();
        }

        public void Analyze()
        {
            assetDataMap.Clear();

            List<PresetItem> presets = filteredPresets;

            for (int i = 0; i < presets.Count; i++)
            {
                PresetItem preset = presets[i];

                CheckTextureStamps(preset);
                CheckTreeStamps(preset);
                CheckDetailStamps(preset);
            }
        }

        private void CheckTextureStamps(PresetItem preset)
        {
            GameObject go = preset.content.prefab;

            if (go == null)
                return;

            TextureStamp[] stamps = go.GetComponentsInChildren<TextureStamp>();

            foreach (TextureStamp stamp in stamps)
            {
                if (stamp == null || stamp.layer == null)
                    continue;

                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(stamp.layer, out string guid, out long localId))
                {
                    RegisterAsset(guid, stamp.layer.name);
                }
            }
        }

        private void CheckTreeStamps(PresetItem preset)
        {
#if __MICROVERSE_VEGETATION__
            GameObject go = preset.content.prefab;

            if (go == null)
                return;

            TreeStamp[] stamps = go.GetComponentsInChildren<TreeStamp>();
            foreach (TreeStamp stamp in stamps)
            {
                foreach (TreePrototypeSerializable pt in stamp.prototypes)
                {
                    GameObject prefab = pt.prefab;

                    if (prefab == null)
                        continue;

                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string guid, out long localId))
                    {
                        RegisterAsset(guid, prefab.name);
                    }
                }
            }
#endif
        }

        private void CheckDetailStamps(PresetItem preset)
        {
#if __MICROVERSE_VEGETATION__

            GameObject go = preset.content.prefab;

            if (go == null)
                return;

            DetailStamp[] stamps = go.GetComponentsInChildren<DetailStamp>();
            foreach (DetailStamp stamp in stamps)
            {
                GameObject prototype = stamp.prototype.prototype;

                if (prototype == null)
                    continue;

                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prototype, out string guid, out long localId))
                {
                    RegisterAsset(guid, prototype.name);
                }

            }
#endif
        }

        private void RegisterAsset(string guid, string name)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            AssetData assetData = new AssetData()
            {
                assetPath = assetPath,
                guid = guid,
                name = name,
            };

            assetDataMap.TryAdd(assetPath, assetData);
        }
    }
}