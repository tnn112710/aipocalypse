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
using System.Reflection;
using sc.screeneffects.runtime.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.runtime
{
    public class PostEffectFeature : InternalRenderFeature
    {
        [SerializeField]
        public RenderFeatureSettings settings = new RenderFeatureSettings();

        private object volumeSettings;

        protected T GetVolumeSettings<T>() where T : PostProcessingComponent
        {
            return volumeSettings as T;
        }

        protected bool GetBlendedVolumeSettings<T>() where T : PostProcessingComponent
        {
            volumeSettings = VolumeManager.instance.stack.GetComponent<T>();
            
            return volumeSettings != null;
        }
        
        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }
        
        //Internal: https://discussions.unity.com/t/unable-to-set-rendering-layer-mask-texture-requirement-for-custom-render-feature/1719030
        public override bool RequireRenderingLayers()
        {
            return settings.UseRenderingLayers;
        }

        void OnValidate()
        {
            settings.OnValidate();
        }
    }
    
    //Specialized version for specific settings type
    public class PostEffectFeature<T> : PostEffectFeature where T : PostProcessingComponent
    {
        public T volumeSettings => GetVolumeSettings<T>();

        /// <summary>
        /// Gets the blended volume settings for this feature's settings type
        /// </summary>
        public bool GetBlendedVolumeSettings()
        {
            return GetBlendedVolumeSettings<T>();
        }
    }
}