using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    [CreateAssetMenu(fileName = ProjectSetup.Layer_SettingsTemplate_FileName, menuName = ProjectSetup.Layer_SettingsTemplate_MenuName)]
    [System.Serializable]
    public class LayerSettingsTemplate : ScriptableObject, ITemplateSettings
    {
        /// <summary>
        /// The name which will be displayed in the prefab template grid of the inspector
        /// </summary>
        public string templateName;

        /// <summary>
        /// Whether the prefab is used or not
        /// </summary>
        public bool active = true;

        /// <summary>
        /// The probability at which the prefab is chosen to be instantiated.
        /// This value is relative to all other prefabs.
        /// So 0 doesn't mean it won't be instantiated at all, it means it's less probable
        /// to be instantiated than others which don't have 0.
        /// Ranges from 0 (not probable at all) to 1 (highest probability).
        /// The value is relative. If all a
        /// </summary>
        public float probability = 1;

        public bool slopeEnabled = true;

        public Vector2 slopeRange;

        public string GetName()
        {
            return templateName;
        }
    }
}
