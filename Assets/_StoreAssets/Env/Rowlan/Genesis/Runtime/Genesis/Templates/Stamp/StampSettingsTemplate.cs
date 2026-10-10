using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    [CreateAssetMenu(fileName = ProjectSetup.Stamp_SettingsTemplate_FileName, menuName = ProjectSetup.Stamp_SettingsTemplate_MenuName)]
    [System.Serializable]
    public class StampSettingsTemplate : ScriptableObject, ITemplateSettings
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
        /// The impact the stamp has on the terrain. Larger weight means larger stamp compared to those with lower weight.
        /// </summary>
        public float weight = 1f;

        /// <summary>
        /// The probability at which the prefab is chosen to be instantiated.
        /// This value is relative to all other prefabs.
        /// So 0 doesn't mean it won't be instantiated at all, it means it's less probable
        /// to be instantiated than others which don't have 0.
        /// Ranges from 0 (not probable at all) to 1 (highest probability).
        /// The value is relative. If all a
        /// </summary>
        public float probability = 1;

        /// <summary>
        /// The heightmap combine mode
        /// </summary>
        public HeightStampSettings.FalloffType falloffType = HeightStampSettings.FalloffType.Range;

        [MinMax(0f, 1f, true)]
        public Vector2 positionRangeX;

        [MinMax(0f, 1f, true)]
        public Vector2 positionRangeY;

        [MinMax(0f, 1f, true)]
        public Vector2 positionRangeZ;

        [MinMax(0f, 360f, true)]
        public Vector2 rotationRange;

        [MinMax(0f, 1f, true)]
        public Vector2 scaleRangeWidth;

        [MinMax(0f, 1f, true)]
        public Vector2 scaleRangeHeight;

        public string GetName()
        {
            return templateName;
        }
    }
}
