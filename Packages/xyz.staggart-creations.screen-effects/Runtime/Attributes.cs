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

#if UNITY_6000_5_OR_NEWER
#define STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
#endif

using System.Reflection;
using System.Runtime.CompilerServices;
using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

[assembly: InternalsVisibleTo("sc.screeneffects.editor")]
namespace sc.screeneffects.runtime
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum, AllowMultiple = false)]
    public class EffectDocs : HelpURLAttribute
    {
        public EffectDocs(string url) : base(PostProcessingComponent.DOCUMENTATION_SECTION_URL + url)
        {
        }
    }
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PostProcessingVolumeComponentAttribute : Attribute
    {
        public readonly Type renderFeatureType;
        public readonly string shaderName;
        public readonly string docSection;
        
        public PostProcessingVolumeComponentAttribute(string shaderName, Type renderFeatureType, string docSection = null)
        {
            if (!typeof(PostEffectFeature).IsAssignableFrom(renderFeatureType))
            {
                Debug.LogError($"Renderer type {renderFeatureType} must be derived from {typeof(PostEffectFeature)}");
            }
            
            this.shaderName = shaderName;
            this.renderFeatureType = renderFeatureType;
            this.docSection = docSection;
        }

        public static PostProcessingVolumeComponentAttribute GetAttribute(Type type)
        {
            return type.GetCustomAttribute<PostProcessingVolumeComponentAttribute>();
        }
    }

    /// <summary>
    /// Loads a default texture through a GUID when the volume component is first added or reset.
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field)]
    public class VolumeTextureDefaultAttribute : Attribute
    {
        public string guid;
        
        public VolumeTextureDefaultAttribute(string guid)
        {
            this.guid = guid;
        }
    }
    
    //Dummy attributes so they can be added error-free in older Unity versions
#if !STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false, AllowMultiple = false)]
    internal class NoAutoStaticsCleanupAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false, AllowMultiple = false)]
    internal class AutoStaticsCleanupAttribute : Attribute { }
#endif
}

//Dummy attributes in this namespace, so attributes can be added error free in older Unity versions
#if !STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
namespace Unity.Scripting.LifecycleManagement
{
    class DummyClassSCSE { }
}
#endif