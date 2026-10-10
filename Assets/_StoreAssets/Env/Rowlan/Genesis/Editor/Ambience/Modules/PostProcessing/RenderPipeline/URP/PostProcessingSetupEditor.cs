using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis.Ambience.Urp
{
    public class PostProcessingSetupEditor
    {
        private AmbienceModule editor;

        public PostProcessingSetupEditor(AmbienceModule editor)
        {
            this.editor = editor;
        }

#if USING_URP
        public void OnInspectorGUI()
        {

            if( GUILayout.Button("Urp"))
            {

                Debug.Log("urp");
            }
        }
#endif

    }
}