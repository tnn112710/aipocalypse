using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis.Ambience.Hdrp
{
    public class PostProcessingSetupEditor
    {
        private AmbienceModule editor;

        public PostProcessingSetupEditor(AmbienceModule editor)
        {
            this.editor = editor;
        }

#if USING_HDRP
        public void OnInspectorGUI()
        {

            if( GUILayout.Button("Hdrp"))
            {

                Debug.Log("Hdrp");
            }
        }
#endif

    }
}