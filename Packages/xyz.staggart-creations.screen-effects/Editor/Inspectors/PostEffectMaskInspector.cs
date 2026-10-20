// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using System;
using System.Collections.Generic;
using sc.screeneffects.runtime;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(PostEffectMask))]
    public partial class PostEffectMaskInspector : Editor
    {
        private PostEffectMask component;
        [NoAutoStaticsCleanup]
        private static List<UniversalRenderPipelineAsset> unsupportedRenderers;
        
        private void OnEnable()
        {
            component = target as PostEffectMask;
            
            unsupportedRenderers = Utilities.GetPipelineAssetsWithRenderingLayersDisabled();
        }

        public override void OnInspectorGUI()
        {
            UI.DrawHeader();
            
            base.OnInspectorGUI();

            if (component.renderingLayerMask > 0 && unsupportedRenderers.Count == 0)
            {
                UI.DrawRenderingLayerSetupError();
            }
            
            UI.DrawFooter();
        }
    }
}