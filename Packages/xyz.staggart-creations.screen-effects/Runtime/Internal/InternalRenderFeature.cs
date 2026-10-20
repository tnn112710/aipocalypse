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

using System.Reflection;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
#endif
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.runtime.Internal
{
    //Need to reference this class into the UnityEngine.Rendering.Universal.Runtime assembly so that the internal RequireRenderingLayers method can be overriden
    public class InternalRenderFeature : ScriptableRendererFeature
    {
        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }
        
        public virtual bool RequireRenderingLayers() 
        {
            return false;
        }

        internal override bool RequireRenderingLayers(bool isDeferred, bool needsGBufferAccurateNormals, out RenderingLayerUtils.Event atEvent, out RenderingLayerUtils.MaskSize maskSize) 
        {
            //Opt to use the opaque event, as a normal-prepass is otherwise executed
            atEvent = RenderingLayerUtils.Event.Opaque;
            maskSize = RenderingLayerUtils.MaskSize.Bits8;
            
            var requiresRenderingLayers = RequireRenderingLayers();
            
            //if(requiresRenderingLayers) Debug.Log($"{this.GetType()} requires Rendering Layers...");
            
            return requiresRenderingLayers;
        }

        private const string m_RequiresRenderingLayerFieldName = "m_RequiresRenderingLayer";
        private static FieldInfo _m_RequiresRenderingLayer;
        public static FieldInfo m_RequiresRenderingLayer => _m_RequiresRenderingLayer ??= typeof(UniversalRenderer).GetField(m_RequiresRenderingLayerFieldName, BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Boolean in the UniversalRenderer class that's set if any feature returns true from RequireRenderingLayers()
        /// </summary>
        /// <returns></returns>
        public static bool RequiresRenderingLayers(ScriptableRenderer renderer)
        {
            #if DEBUG
            if (m_RequiresRenderingLayer == null)
            {
                Debug.LogError($"The private field {m_RequiresRenderingLayerFieldName} could not be found in the UniversalRenderer class. This means internal changes were made to the UniversalRenderer class. Please report this to the developer of this asset.");
                return false;
            }
            #endif
            
            return (bool)m_RequiresRenderingLayer.GetValue(renderer);
        }
        
        public static void SetCameraOpaqueTexture(UniversalResourceData resourceData, TextureHandle opaqueTexture)
        {
            resourceData.cameraOpaqueTexture = opaqueTexture;
        }

        public static void SetCameraOcclusionTexture(UniversalResourceData resourceData, TextureHandle occlusionTexture)
        {
            resourceData.ssaoTexture = occlusionTexture;
        }
        
        public static void CopyIntoOpaqueTexture(RenderGraph renderGraph, UniversalResourceData resourceData, TextureHandle texture)
        {
            renderGraph.AddCopyPass(texture, resourceData.cameraOpaqueTexture);
        }
    }
}