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
using UnityEngine;
using UnityEngine.Rendering;

namespace sc.screeneffects.runtime
{
    [AddComponentMenu("SC Screen Effects/Post Effect Mask")]
    public class PostEffectMask : MonoBehaviour
    {
        public RenderingLayerMask renderingLayerMask = 2;
        public CompareFunction zTest = CompareFunction.Less;
        public CullMode culling = CullMode.Back;
        
        [SerializeField] [HideInInspector]
        private Shader shader;
        [SerializeField] [HideInInspector]
        private Material material;

        public MeshRenderer[] meshRenderers = Array.Empty<MeshRenderer>();

        private void Reset()
        {
            meshRenderers = GetComponents<MeshRenderer>();
        }

        private void OnValidate()
        {
            UpdateMaterial();
        }

        private void Start()
        {
            UpdateMaterial();
        }

        public void UpdateMaterial()
        {
            if (!shader)
            {
                shader = Shader.Find(ShaderNames.PostEffectMask);
                if(!shader) throw new Exception($"[PostEffectMask] Shader {ShaderNames.PostEffectMask} not found. Ensure that it is included in the build and compiles without errors.");
            }
            if (!material)
            {
                material = new Material(shader);
                material.name = "Post Processing Layer Mask";
            }
            
            material.SetInt("_ZTest", (int)zTest);
            material.SetInt("_Cull", (int)culling);
            
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                if (!meshRenderer) continue;
                
                meshRenderer.renderingLayerMask = renderingLayerMask;
                meshRenderer.material = material;
            }
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Edit Rendering Layers")]
        private void OpenLayerEditor()
        {
            UnityEditor.SettingsService.OpenProjectSettings("Project/Tags and Layers");
        }
        #endif
    }
}