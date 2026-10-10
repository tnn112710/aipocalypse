using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    [CreateAssetMenu(fileName = ProjectSetup.Layer_TemplateCollection_FileName, menuName = ProjectSetup.Layer_TemplateCollection_MenuName)]
    [System.Serializable]
    public class LayerTemplateCollection : ScriptableObject, ITemplateCollection
    {
        /// <summary>
        /// Information about the valid objects on a drop target
        /// </summary>
        public string presetHelpText;

        /// <summary>
        /// Collection of various settings templates
        /// </summary>
        [SerializeField]
        public LayerSettingsTemplate[] templates = new LayerSettingsTemplate[0];

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