using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    [CreateAssetMenu(fileName = ProjectSetup.Path_SettingsTemplate_FileName, menuName = ProjectSetup.Path_SettingsTemplate_MenuName)]
    [System.Serializable]
    public class PathSettingsTemplate : ScriptableObject, ITemplateSettings
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

        public PathSettings.Shape shape;

        public bool closed;

        /// <summary>
        /// The relative scale compared to the total terrain bounds
        /// </summary>
        [Range(0f, 1f)]
        public float scale = 0.6f;

        [MinMax(-20, 20, true)]
        public Vector2 deformationAngle;

        [Range(0, 5)]
        public int subdivisionIterations;

        [MinMax(0.2f, 0.8f, true)]
        public Vector2 subdivisionRange;

        public string GetName()
        {
            return templateName;
        }
    }
}
