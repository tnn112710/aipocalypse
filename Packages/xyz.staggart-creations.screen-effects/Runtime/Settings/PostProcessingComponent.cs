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
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace sc.screeneffects.runtime
{
    [VolumeComponentMenu("SC Screen Effects/")]
    [SupportedOnRenderPipeline(typeof(RenderPipelineAsset))]
    public partial class PostProcessingComponent : VolumeComponent, IPostProcessComponent
    {
        public const string DOCUMENTATION_URL = "https://staggart.xyz/unity/sc-screen-effects/scpe-docs-v3/";
        public const string DOCUMENTATION_SECTION_URL = DOCUMENTATION_URL + "?section=";
        
        [SerializeField]
        public Shader shader;
        internal Volume quickVolume;

        [ResourcePath(ShaderNames.DepthNormals, SearchType.ShaderName)]
        public Shader normalsShader;
        [ResourcePath(ShaderNames.Blur, SearchType.ShaderName)]
        public Shader blurShader;
        
        public bool IsActive() => this.active;
        
        private void Reset()
        {
            //Base class
            if (this.GetType() == typeof(PostProcessingComponent)) return;
            
            #if UNITY_EDITOR
            //if(Application.isPlaying == false) intensity.Override(1f);
            #endif
            
            SerializeShader();

            LoadDefaultTextures();
        }
        
        public void LoadDefaultTextures()
        {
            #if UNITY_EDITOR
            var fields = this.GetType().GetFields( BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var field in fields)
            {
                var volumeTextureAttribute = field.GetCustomAttribute<VolumeTextureDefaultAttribute>();
                
                if (volumeTextureAttribute != null)
                {
                    Type fieldType = field.FieldType;
                    if (fieldType != typeof(TextureParameter) && fieldType != typeof(Texture2DParameter) && fieldType != typeof(Texture3DParameter))
                    {
                        Debug.LogError($"The {typeof(VolumeTextureDefaultAttribute)} attribute can only be used on fields of type TextureParameter. Field {field.Name} in class {this.GetType().Name} is not a TextureParameter.");
                        continue;
                    }
                    
                    VolumeParameter textureParameter = (VolumeParameter)field.GetValue(this);
                    
                    //Debug.Log($"[SC Screen Effects] Loading default texture for {field.Name} in class {this.GetType().Name} of type: {fieldType.Name}");
                    
                    var valueProperty = textureParameter.GetType().GetProperty("value");
                    
                    //Auto assign default texture
                    if (valueProperty.GetValue(textureParameter, null) == null)
                    {
                        string texturePath = UnityEditor.AssetDatabase.GUIDToAssetPath(volumeTextureAttribute.guid);

                        if (texturePath == string.Empty)
                        {
                            Debug.LogWarning($"Could not find the default texture for parameter {field.Name} in class {this.GetType().Name} with GUID: {volumeTextureAttribute.guid}. Was the file deleted or not imported?");
                            continue;
                        }
                        
                        //Debug.Log($"[SC Screen Effects] Loading default texture for {field.Name} in class {this.GetType().Name} from path: {texturePath}");
                        
                        textureParameter.overrideState = true;
                        
                        Object loadedTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(texturePath);
                        valueProperty.SetValue(textureParameter, loadedTexture);
                    }
                }
            }
            #endif
        }

        public Shader FindShader()
        {
            //Base class
            if (this.GetType() == typeof(PostProcessingComponent)) return null;
            
            PostProcessingVolumeComponentAttribute componentAttribute = PostProcessingVolumeComponentAttribute.GetAttribute(this.GetType());

            if (componentAttribute == null)
            {
                throw new Exception("Missing PostProcessingVolumeComponent attribute on " + this.GetType());
            }
                
            shader = Shader.Find(componentAttribute.shaderName);
            
            if (!shader)
            {
                throw new Exception("Missing shader: " + componentAttribute.shaderName);
            }

            return shader;
        }
        
        protected virtual bool SerializeShader()
        {
            //Base class
            if (this.GetType() == typeof(PostProcessingComponent)) return false;
            
            bool wasSerialized = !shader;
            
            shader = FindShader();

            return wasSerialized;
        }

        internal virtual void WarnWhenMissingResources() { }

        internal void ThrowWarningExceptionIfNull(Texture3DParameter parameter)
        {
            if(parameter.value == null) throw new WarningException($"The post processing effect {this.name} requires a texture, but it is missing. Be sure to override the parameter and assign it.");
        }
        
        internal void ThrowWarningExceptionIfNull(TextureParameter parameter)
        {
            if(parameter.value == null) throw new WarningException($"The post processing effect {this.name} requires a texture, but it is missing. Be sure to override the parameter and assign it.");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            Dispose();
        }

        /// <summary>
        /// Creates a hidden volume with a virtual profile that has this effect active on it.
        /// Use this volume to set up and control script-based effects at runtime.
        /// Dispose of the volume by using <see cref="DisposeEffect"/>
        /// </summary>
        /// <param name="addRenderer">Adds a PostProcessingVolumeRenderer component, disable if using a Render Feature already!</param>
        /// <param name="overriden">Override all parameters</param>
        /// <param name="layer">Layer the volume gets put on, should correspond to the camera's volume layer (not culling layer!)</param>
        /// <param name="priority">Priority the settings have over other volumes, default is +1</param>
        /// <returns></returns>
        public static T Create<T>(bool addRenderer = true, bool overriden = false, int layer = 0, int priority = 1) where T : PostProcessingComponent
        {
            var settings = ScriptableObject.CreateInstance<T>();
            
            #if DEBUG
            settings.name = $"[SC Screen Effects] {settings.GetType().Name}";
            #endif
            
            //Debug.Log($"Created {settings.GetType().Name} effect");
            
            settings.SerializeShader();
            settings.quickVolume = QuickVolume.Create(layer, priority, overriden, settings);

            if (addRenderer)
            {
                PostProcessingVolumeRenderer renderer = settings.quickVolume.gameObject.AddComponent<PostProcessingVolumeRenderer>();
                renderer.volume = settings.quickVolume;
                renderer.volume.sharedProfile = settings.quickVolume.profile;
                renderer.GetEffectsFromVolume();
            }
            
            return settings;
        }

        /// <summary>
        /// Disposes the resources allocated by <see cref="Create{T}"/>
        /// </summary>
        public void Dispose()
        {
            Dispose(this);
        }
        
        /// <summary>
        /// Disposes an effect's hidden volume, if created through <see cref="Create{T}"/>
        /// </summary>
        /// <param name="effect"></param>
        public static void Dispose(PostProcessingComponent effect)
        {
            if (effect.quickVolume == null) return;
            
            QuickVolume.Destroy(effect.quickVolume);
        }
    }
}