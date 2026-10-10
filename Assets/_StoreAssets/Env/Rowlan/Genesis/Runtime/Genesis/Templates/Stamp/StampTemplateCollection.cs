using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    [CreateAssetMenu(fileName = ProjectSetup.Stamp_TemplateCollection_FileName, menuName = ProjectSetup.Stamp_TemplateCollection_MenuName)]
    [System.Serializable]
    public class StampTemplateCollection : ScriptableObject, ITemplateCollection
    {
        /// <summary>
        /// Information about the valid objects on a drop target
        /// </summary>
        public string presetHelpText;

        /// <summary>
        /// Collection of various settings templates
        /// </summary>
        [SerializeField]
        public StampSettingsTemplate[] templates = new StampSettingsTemplate[0];

        public ITemplateSettings[] GetTemplates()
        {
            return templates;
        }

        public string GetPresetHelpText()
        {
            return presetHelpText;
        }
    }
}