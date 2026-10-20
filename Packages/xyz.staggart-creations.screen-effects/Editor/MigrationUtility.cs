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
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;
using sc.screeneffects.runtime;
using Unity.Scripting.LifecycleManagement;

namespace sc.screeneffects.editor
{
    [NoAutoStaticsCleanup]
    public static class MigrationUtility
    {
        private const string WINDOW_MENU_PATH = "Window/" + AssetInfo.ASSET_NAME + "/Migration Utility";
        private const string OLD_NAMESPACE = "SCPE";
        
        [MenuItem(WINDOW_MENU_PATH, false, 1003)]
        public static void OpenMigrationWindow()
        {
            MigrationWindow.Open();
        }

        #region Type Mapping Table
        public struct EffectTypeMapping
        {
            public string v2TypeName;
            public Type v3Type;
            public string v2RendererTypeName;
            public Type v3RendererType;
            public Dictionary<string, string> propertyAliases;

            public EffectTypeMapping(string v2TypeName, Type v3Type, string v2RendererTypeName, Type v3RendererType, Dictionary<string, string> propertyAliases = null)
            {
                this.v2TypeName = v2TypeName;
                this.v3Type = v3Type;
                this.v2RendererTypeName = v2RendererTypeName;
                this.v3RendererType = v3RendererType;
                this.propertyAliases = propertyAliases ?? new Dictionary<string, string>();
            }
        }

        private static List<EffectTypeMapping> s_Mappings;
        private static Dictionary<string, EffectTypeMapping> s_V2NameToMapping;
        private static Dictionary<string, EffectTypeMapping> s_V2RendererNameToMapping;

        public static List<EffectTypeMapping> Mappings
        {
            get
            {
                if (s_Mappings == null) InitializeMappings();
                return s_Mappings;
            }
        }

        private static void InitializeMappings()
        {
            s_Mappings = new List<EffectTypeMapping>
            {
                new EffectTypeMapping(OLD_NAMESPACE + ".BlackBars", typeof(BlackBars), OLD_NAMESPACE + ".BlackBarsRenderer", typeof(BlackBarsRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Blur", typeof(Blur), OLD_NAMESPACE + ".BlurRenderer", typeof(BlurRenderer),
                    new Dictionary<string, string> { { "amount", "radius" } }),
                new EffectTypeMapping(OLD_NAMESPACE + ".ColorSplit", typeof(ColorSplit), OLD_NAMESPACE + ".ColorSplitRenderer", typeof(ColorSplitRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Colorize", typeof(Colorize), OLD_NAMESPACE + ".ColorizeRenderer", typeof(ColorizeRenderer),
                    new Dictionary<string, string> { { "mode", "blendMode" }, { "colorRamp", "colorGradient" } }),
                new EffectTypeMapping(OLD_NAMESPACE + ".Danger", typeof(Condition), OLD_NAMESPACE + ".DangerRenderer", typeof(ConditionRenderer),
                    new Dictionary<string, string> { { "size", "falloff" } }),
                new EffectTypeMapping(OLD_NAMESPACE + ".CurvatureHighlight", typeof(CurvatureHighlight), OLD_NAMESPACE + ".CurvatureHighlightRenderer", typeof(CurvatureHighlightRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Curvature", typeof(CurvatureHighlight), OLD_NAMESPACE + ".CurvatureRenderer", typeof(CurvatureHighlightRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Dithering", typeof(Dithering), OLD_NAMESPACE + ".DitheringRenderer", typeof(DitheringRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".DoubleVision", typeof(DoubleVision), OLD_NAMESPACE + ".DoubleVisionRenderer", typeof(DoubleVisionRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".EdgeDetection", typeof(EdgeDetection), OLD_NAMESPACE + ".EdgeDetectionRenderer", typeof(EdgeDetectionRenderer),
                    new Dictionary<string, string> { { "sensitivityDepth", "depthInfluence" }, { "sensitivityNormals", "normalInfluence" }, { "lumThreshold", "colorInfluence" } }),
                new EffectTypeMapping(OLD_NAMESPACE + ".Fog", typeof(Fog), OLD_NAMESPACE + ".FogRenderer", typeof(FogRenderer),
                    new Dictionary<string, string> { { "fogStartDistance", "startDistance" }, { "fogEndDistance", "endDistance" } }),
                new EffectTypeMapping(OLD_NAMESPACE + ".Gradient", typeof(sc.screeneffects.runtime.Gradient), OLD_NAMESPACE + ".GradientRenderer", typeof(GradientRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".HueShift3D", typeof(HueShift3D), OLD_NAMESPACE + ".HueShift3DRenderer", typeof(HueShift3DRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Kaleidoscope", typeof(Kaleidoscope), OLD_NAMESPACE + ".KaleidoscopeRenderer", typeof(KaleidoscopeRenderer),
                    new Dictionary<string, string> { { "splits", "radialSplits" } }),
                new EffectTypeMapping(OLD_NAMESPACE + ".Kuwahara", typeof(Kuwahara), OLD_NAMESPACE + ".KuwaharaRenderer", typeof(KuwaharaRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".LUT", typeof(LUT), OLD_NAMESPACE + ".LUTRenderer", typeof(LUTRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".LightStreaks", typeof(LightStreaks), OLD_NAMESPACE + ".LightStreaksRenderer", typeof(LightStreaksRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Mosaic", typeof(Mosaic), OLD_NAMESPACE + ".MosaicRenderer", typeof(MosaicRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Overlay", typeof(Overlay), OLD_NAMESPACE + ".OverlayRenderer", typeof(OverlayRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Pixelize", typeof(Pixelize), OLD_NAMESPACE + ".PixelizeRenderer", typeof(PixelizeRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Posterize", typeof(Posterize), OLD_NAMESPACE + ".PosterizeRenderer", typeof(PosterizeRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".RadialBlur", typeof(RadialBlur), OLD_NAMESPACE + ".RadialBlurRenderer", typeof(RadialBlurRenderer),
                    new Dictionary<string, string>{ { "amount", "intensity "}}),
                new EffectTypeMapping(OLD_NAMESPACE + ".Refraction", typeof(Refraction), OLD_NAMESPACE + ".RefractionRenderer", typeof(RefractionRenderer),
                    new Dictionary<string, string> { { "amount", "intensity "},{ "refractionTex", "normalMap" } }),
                new EffectTypeMapping(OLD_NAMESPACE + ".Ripples", typeof(Ripples), OLD_NAMESPACE + ".RipplesRenderer", typeof(RipplesRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Scanlines", typeof(Scanlines), OLD_NAMESPACE + ".ScanlinesRenderer", typeof(ScanlinesRenderer),
                    new Dictionary<string, string> { { "amount", "linesManual" } }),
                new EffectTypeMapping(OLD_NAMESPACE + ".Sharpen", typeof(Sharpen), OLD_NAMESPACE + ".SharpenRenderer", typeof(SharpenRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Sketch", typeof(Sketch), OLD_NAMESPACE + ".SketchRenderer", typeof(SketchRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".Sunshafts", typeof(Sunshafts), OLD_NAMESPACE + ".SunshaftsRenderer", typeof(SunshaftsRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".TiltShift", typeof(TiltShift), OLD_NAMESPACE + ".TiltShiftRenderer", typeof(TiltShiftRenderer), 
                    new Dictionary<string, string>{ { "amount", "intensity "}}),
                new EffectTypeMapping(OLD_NAMESPACE + ".Transition", typeof(Transition), OLD_NAMESPACE + ".TransitionRenderer", typeof(TransitionRenderer)),
                new EffectTypeMapping(OLD_NAMESPACE + ".TubeDistortion", typeof(TubeDistortion), OLD_NAMESPACE + ".TubeDistortionRenderer", typeof(TubeDistortionRenderer))
            };

            s_V2NameToMapping = new Dictionary<string, EffectTypeMapping>(StringComparer.OrdinalIgnoreCase);
            s_V2RendererNameToMapping = new Dictionary<string, EffectTypeMapping>(StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in s_Mappings)
            {
                s_V2NameToMapping[mapping.v2TypeName] = mapping;
                string shortName = mapping.v2TypeName.Contains(".") ? mapping.v2TypeName.Substring(mapping.v2TypeName.LastIndexOf('.') + 1) : mapping.v2TypeName;
                s_V2NameToMapping[shortName] = mapping;

                s_V2RendererNameToMapping[mapping.v2RendererTypeName] = mapping;
                string shortRendererName = mapping.v2RendererTypeName.Contains(".") ? mapping.v2RendererTypeName.Substring(mapping.v2RendererTypeName.LastIndexOf('.') + 1) : mapping.v2RendererTypeName;
                s_V2RendererNameToMapping[shortRendererName] = mapping;
            }
        }

        public static bool IsV3VolumeComponent(Type type)
        {
            if (type == null) return false;
            if (type.Namespace == typeof(BlackBars).Namespace) return true;
            if (s_Mappings == null) InitializeMappings();
            return s_Mappings.Any(m => m.v3Type == type);
        }

        public static bool IsV3VolumeComponent(VolumeComponent component)
        {
            if (component == null) return false;
            return IsV3VolumeComponent(component.GetType());
        }

        public static bool IsV3RendererFeature(Type type)
        {
            if (type == null) return false;
            if (type.Namespace == typeof(BlackBarsRenderer).Namespace) return true;
            if (s_Mappings == null) InitializeMappings();
            return s_Mappings.Any(m => m.v3RendererType == type);
        }

        public static bool IsV3RendererFeature(ScriptableRendererFeature feature)
        {
            if (feature == null) return false;
            return IsV3RendererFeature(feature.GetType());
        }

        public static bool TryGetVolumeComponentMapping(Type v2Type, out EffectTypeMapping mapping)
        {
            if (v2Type == null || IsV3VolumeComponent(v2Type))
            {
                mapping = default;
                return false;
            }
            return TryGetVolumeComponentMapping(v2Type.FullName, out mapping) || (v2Type.Namespace == OLD_NAMESPACE && TryGetVolumeComponentMapping(v2Type.Name, out mapping));
        }

        public static bool TryGetVolumeComponentMapping(string v2TypeName, out EffectTypeMapping mapping)
        {
            if (string.IsNullOrEmpty(v2TypeName))
            {
                mapping = default;
                return false;
            }
            if (s_V2NameToMapping == null) InitializeMappings();
            return s_V2NameToMapping.TryGetValue(v2TypeName, out mapping);
        }

        public static bool TryGetRendererFeatureMapping(Type v2Type, out EffectTypeMapping mapping)
        {
            if (v2Type == null || IsV3RendererFeature(v2Type))
            {
                mapping = default;
                return false;
            }
            return TryGetRendererFeatureMapping(v2Type.FullName, out mapping) || (v2Type.Namespace == OLD_NAMESPACE && TryGetRendererFeatureMapping(v2Type.Name, out mapping));
        }

        public static bool TryGetRendererFeatureMapping(string v2TypeName, out EffectTypeMapping mapping)
        {
            if (string.IsNullOrEmpty(v2TypeName))
            {
                mapping = default;
                return false;
            }
            if (s_V2RendererNameToMapping == null) InitializeMappings();
            return s_V2RendererNameToMapping.TryGetValue(v2TypeName, out mapping);
        }

        public static bool IsV2VolumeComponent(VolumeComponent component)
        {
            if (component == null) return false;
            Type type = component.GetType();

            if (IsV3VolumeComponent(type)) return false;
            if (type.Namespace == OLD_NAMESPACE) return true;
            return TryGetVolumeComponentMapping(type, out _);
        }

        public static bool IsV2RendererFeature(ScriptableRendererFeature feature)
        {
            if (feature == null) return false;
            Type type = feature.GetType();
            
            if (IsV3RendererFeature(type)) return false;
            if (type.Namespace == OLD_NAMESPACE) return true;
            return TryGetRendererFeatureMapping(type, out _);
        }
        #endregion

        #region Serialized Property Copying
        public static int CopySerializedProperties(SerializedObject sourceObj, SerializedObject destObj, Dictionary<string, string> customAliases = null)
        {
            if (sourceObj == null || destObj == null) return 0;

            sourceObj.Update();
            destObj.Update();

            int propertiesCopied = 0;
            SerializedProperty srcProp = sourceObj.GetIterator();

            // Build lookup of FormerlySerializedAs attributes on the destination type
            Type destType = destObj.targetObject != null ? destObj.targetObject.GetType() : null;
            Dictionary<string, string> formerlySerializedLookup = GetFormerlySerializedAsMap(destType);

            if (srcProp.NextVisible(true))
            {
                do
                {
                    if (srcProp.name == "m_Script") continue;

                    string targetPropName = srcProp.name;

                    // 1. Check custom aliases
                    if (customAliases != null && customAliases.TryGetValue(srcProp.name, out string aliasName))
                    {
                        targetPropName = aliasName;
                    }
                    // 2. Check FormerlySerializedAs attribute mappings
                    else if (formerlySerializedLookup != null && formerlySerializedLookup.TryGetValue(srcProp.name, out string formerlyMappedName))
                    {
                        targetPropName = formerlyMappedName;
                    }

                    SerializedProperty dstProp = destObj.FindProperty(targetPropName);
                    if (dstProp != null)
                    {
                        if (CopyPropertyValue(srcProp, dstProp))
                        {
                            propertiesCopied++;
                        }
                    }
                }
                while (srcProp.NextVisible(false));
            }

            destObj.ApplyModifiedProperties();
            return propertiesCopied;
        }

        private static Dictionary<string, string> GetFormerlySerializedAsMap(Type type)
        {
            if (type == null) return null;

            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (FieldInfo field in fields)
            {
                var attrs = field.GetCustomAttributes<FormerlySerializedAsAttribute>(true);
                foreach (var attr in attrs)
                {
                    if (!string.IsNullOrEmpty(attr.oldName) && !map.ContainsKey(attr.oldName))
                    {
                        map[attr.oldName] = field.Name;
                    }
                }
            }

            return map;
        }

        public static bool CopyPropertyValue(SerializedProperty source, SerializedProperty dest)
        {
            if (source == null || dest == null) return false;

            if (source.propertyType == dest.propertyType)
            {
                switch (source.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        dest.longValue = source.longValue;
                        return true;
                    case SerializedPropertyType.Boolean:
                        dest.boolValue = source.boolValue;
                        return true;
                    case SerializedPropertyType.Float:
                        dest.doubleValue = source.doubleValue;
                        return true;
                    case SerializedPropertyType.String:
                        dest.stringValue = source.stringValue;
                        return true;
                    case SerializedPropertyType.Color:
                        dest.colorValue = source.colorValue;
                        return true;
                    case SerializedPropertyType.ObjectReference:
                        dest.objectReferenceValue = source.objectReferenceValue;
                        return true;
                    case SerializedPropertyType.LayerMask:
                        dest.intValue = source.intValue;
                        return true;
                    case SerializedPropertyType.Enum:
                        if (source.enumNames.Length > 0 && dest.enumNames.Length > 0)
                        {
                            string sourceEnumName = source.enumValueIndex >= 0 && source.enumValueIndex < source.enumNames.Length
                                ? source.enumNames[source.enumValueIndex] : null;
                            if (!string.IsNullOrEmpty(sourceEnumName))
                            {
                                int targetIndex = Array.IndexOf(dest.enumNames, sourceEnumName);
                                if (targetIndex >= 0)
                                {
                                    dest.enumValueIndex = targetIndex;
                                    return true;
                                }
                            }
                        }
                        dest.intValue = source.intValue;
                        return true;
                    case SerializedPropertyType.Vector2:
                        dest.vector2Value = source.vector2Value;
                        return true;
                    case SerializedPropertyType.Vector3:
                        dest.vector3Value = source.vector3Value;
                        return true;
                    case SerializedPropertyType.Vector4:
                        dest.vector4Value = source.vector4Value;
                        return true;
                    case SerializedPropertyType.Rect:
                        dest.rectValue = source.rectValue;
                        return true;
                    case SerializedPropertyType.ArraySize:
                        dest.intValue = source.intValue;
                        return true;
                    case SerializedPropertyType.AnimationCurve:
                        dest.animationCurveValue = source.animationCurveValue;
                        return true;
                    case SerializedPropertyType.Bounds:
                        dest.boundsValue = source.boundsValue;
                        return true;
                    case SerializedPropertyType.Vector2Int:
                        dest.vector2IntValue = source.vector2IntValue;
                        return true;
                    case SerializedPropertyType.Vector3Int:
                        dest.vector3IntValue = source.vector3IntValue;
                        return true;
                    case SerializedPropertyType.RectInt:
                        dest.rectIntValue = source.rectIntValue;
                        return true;
                    case SerializedPropertyType.BoundsInt:
                        dest.boundsIntValue = source.boundsIntValue;
                        return true;
                    case SerializedPropertyType.ManagedReference:
                        dest.managedReferenceValue = source.managedReferenceValue;
                        return true;
                    case SerializedPropertyType.Generic:
                        return CopyGenericChildren(source, dest);
                }
            }
            else
            {
                // Compatible cross-type conversions
                if (source.propertyType == SerializedPropertyType.Float && dest.propertyType == SerializedPropertyType.Integer)
                {
                    dest.intValue = Mathf.RoundToInt((float)source.doubleValue);
                    return true;
                }
                if (source.propertyType == SerializedPropertyType.Integer && dest.propertyType == SerializedPropertyType.Float)
                {
                    dest.doubleValue = source.longValue;
                    return true;
                }
                if (source.propertyType == SerializedPropertyType.Enum && dest.propertyType == SerializedPropertyType.Integer)
                {
                    dest.intValue = source.intValue;
                    return true;
                }
                if (source.propertyType == SerializedPropertyType.Integer && dest.propertyType == SerializedPropertyType.Enum)
                {
                    dest.intValue = source.intValue;
                    return true;
                }
            }

            return false;
        }

        private static bool CopyGenericChildren(SerializedProperty sourceParent, SerializedProperty destParent)
        {
            if (sourceParent.isArray && destParent.isArray)
            {
                destParent.arraySize = sourceParent.arraySize;
                for (int i = 0; i < sourceParent.arraySize; i++)
                {
                    CopyPropertyValue(sourceParent.GetArrayElementAtIndex(i), destParent.GetArrayElementAtIndex(i));
                }
                return true;
            }

            SerializedProperty srcChild = sourceParent.Copy();
            SerializedProperty endProperty = sourceParent.GetEndProperty();
            bool hasChildren = srcChild.NextVisible(true);
            bool copiedAny = false;

            while (hasChildren && !SerializedProperty.EqualContents(srcChild, endProperty))
            {
                SerializedProperty dstChild = destParent.FindPropertyRelative(srcChild.name);
                if (dstChild != null)
                {
                    if (CopyPropertyValue(srcChild, dstChild))
                    {
                        copiedAny = true;
                    }
                }
                hasChildren = srcChild.NextVisible(false);
            }

            return copiedAny;
        }
        #endregion

        #region Discovery
        
        private static bool InstanceNotNull(SerializedProperty prop)
        {
#if UNITY_6000_4_OR_NEWER
            return prop.objectReferenceEntityIdValue != null;
#else
            return prop.objectReferenceInstanceIDValue != 0;
#endif
        }

        public static List<VolumeProfile> FindVolumeProfilesWithV2Components(out List<string> warnings)
        {
            warnings = new List<string>();
            List<VolumeProfile> result = new List<VolumeProfile>();

            URPDefaultVolumeProfileSettings defaultVolumeSettings =
                GraphicsSettings.GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>();
            
            string[] guids = AssetDatabase.FindAssets("t:VolumeProfile");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null) continue;

                //Don't bother attempting to migrate the default profile
                if(profile == defaultVolumeSettings.volumeProfile) continue;
                
                bool hasV2 = false;
                bool hasMissing = false;

                SerializedObject serializedProfile = new SerializedObject(profile);
                SerializedProperty componentsProp = serializedProfile.FindProperty("components");

                if (componentsProp != null && componentsProp.isArray)
                {
                    for (int i = 0; i < componentsProp.arraySize; i++)
                    {
                        SerializedProperty compElement = componentsProp.GetArrayElementAtIndex(i);
                        VolumeComponent comp = compElement.objectReferenceValue as VolumeComponent;

                        if (compElement.objectReferenceValue == null && InstanceNotNull(compElement))
                        {
                            hasMissing = true;
                        }
                        else if (comp != null && IsV2VolumeComponent(comp))
                        {
                            hasV2 = true;
                        }
                    }
                }
                else if (profile.components != null)
                {
                    foreach (var comp in profile.components)
                    {
                        if (comp == null)
                        {
                            hasMissing = true;
                        }
                        else if (IsV2VolumeComponent(comp))
                        {
                            hasV2 = true;
                        }
                    }
                }

                if (hasMissing)
                {
                    warnings.Add($"Volume Profile \"{profile.name}\" contains missing Volume Component script(s). If SCPE v2 was already deleted, missing components cannot be automatically deserialized.");
                }

                if (hasV2)
                {
                    result.Add(profile);
                }
            }

            return result;
        }

        public static List<ScriptableRendererData> FindRenderersWithV2Features(out List<string> warnings)
        {
            warnings = new List<string>();
            List<ScriptableRendererData> result = new List<ScriptableRendererData>();

            string[] guids = AssetDatabase.FindAssets("t:ScriptableRendererData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null) continue;

                bool hasV2 = false;
                bool hasMissing = false;

                SerializedObject serializedRenderer = new SerializedObject(rendererData);
                SerializedProperty featuresProp = serializedRenderer.FindProperty("m_RendererFeatures");

                if (featuresProp != null && featuresProp.isArray)
                {
                    for (int i = 0; i < featuresProp.arraySize; i++)
                    {
                        SerializedProperty featElement = featuresProp.GetArrayElementAtIndex(i);
                        ScriptableRendererFeature feature = featElement.objectReferenceValue as ScriptableRendererFeature;
                        
                        if (featElement.objectReferenceValue == null && InstanceNotNull(featElement))
                        {
                            hasMissing = true;
                        }
                        else if (feature != null && IsV2RendererFeature(feature))
                        {
                            hasV2 = true;
                        }
                    }
                }
                else if (rendererData.rendererFeatures != null)
                {
                    foreach (var feature in rendererData.rendererFeatures)
                    {
                        if (feature == null)
                        {
                            hasMissing = true;
                        }
                        else if (IsV2RendererFeature(feature))
                        {
                            hasV2 = true;
                        }
                    }
                }

                if (hasMissing)
                {
                    warnings.Add($"Renderer Data \"{rendererData.name}\" contains missing Render Feature script(s). If SCPE v2 was already deleted, missing features cannot be automatically deserialized.");
                }

                if (hasV2)
                {
                    result.Add(rendererData);
                }
            }

            return result;
        }
        #endregion

        #region Migration Execution
        [MenuItem("CONTEXT/VolumeProfile/[SC Screen Effects] Migrate Effects to v3")]
        public static void MigrateVolumeInstance(MenuCommand cmd)
        {
            VolumeProfile profile = cmd.context as VolumeProfile;

            List<string> logMessages = new List<string>();
            MigrateVolumeProfile(profile, logMessages);

            foreach (var msg in logMessages)
            {
                Debug.Log(msg);
            }
        }
        
        public static bool MigrateVolumeProfile(VolumeProfile profile, List<string> logMessages)
        {
            if (profile == null) return false;

            string profileName = profile.name;
            string assetPath = AssetDatabase.GetAssetPath(profile);

            Undo.RegisterCompleteObjectUndo(profile, $"Migrate SCPE Volume Profile: {profileName}");

            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty componentsProp = serializedProfile.FindProperty("components");

            if (componentsProp == null || !componentsProp.isArray)
            {
                logMessages.Add($"[Error] Could not find 'components' property on Volume Profile \"{profileName}\".");
                return false;
            }

            serializedProfile.Update();

            bool modified = false;
            int componentCount = componentsProp.arraySize;

            for (int i = 0; i < componentCount; i++)
            {
                SerializedProperty compElement = componentsProp.GetArrayElementAtIndex(i);
                VolumeComponent oldComp = compElement.objectReferenceValue as VolumeComponent;

                if (oldComp == null)
                {
                    if (InstanceNotNull(compElement))
                    {
                        logMessages.Add($"[Warning] Profile \"{profileName}\" has missing component at index {i} (v2 already deleted). Cannot automatically migrate missing script.");
                    }
                    continue;
                }

                Type oldType = oldComp.GetType();
                if (!IsV2VolumeComponent(oldComp)) continue;

                if (!TryGetVolumeComponentMapping(oldType, out EffectTypeMapping mapping))
                {
                    logMessages.Add($"[Warning] No v3 mapping found for v2 component type \"{oldType.FullName}\" in profile \"{profileName}\".");
                    continue;
                }

                try
                {
                    // Create v3 instance
                    VolumeComponent newComp = (VolumeComponent)ScriptableObject.CreateInstance(mapping.v3Type);
                    newComp.name = mapping.v3Type.Name;
                    newComp.active = oldComp.active;
                    newComp.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;

                    // Add as sub-asset to Volume Profile asset
                    AssetDatabase.AddObjectToAsset(newComp, profile);
                    Undo.RegisterCreatedObjectUndo(newComp, "Create v3 Volume Component");

                    // Copy serialized settings
                    SerializedObject oldObj = new SerializedObject(oldComp);
                    SerializedObject newObj = new SerializedObject(newComp);
                    CopySerializedProperties(oldObj, newObj, mapping.propertyAliases);

                    // Replace object reference in profile
                    compElement.objectReferenceValue = newComp;

                    // Destroy old sub-asset
                    Undo.DestroyObjectImmediate(oldComp);

                    modified = true;
                    logMessages.Add($"[Success] Migrated \"{oldType.Name}\" to \"{mapping.v3Type.Name}\" in profile \"{profileName}\".");
                }
                catch (Exception ex)
                {
                    logMessages.Add($"[Error] Failed migrating \"{oldType.Name}\" in profile \"{profileName}\": {ex.Message}");
                    Debug.LogException(ex);
                }
            }

            if (modified)
            {
                serializedProfile.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            return modified;
        }

        [MenuItem("CONTEXT/ScriptableRendererData/[SC Screen Effects] Migrate Render Features to v3")]
        public static void MigrateRendererInstance(MenuCommand cmd)
        {
            ScriptableRendererData rendererData = cmd.context as ScriptableRendererData;
            
            List<string> logMessages = new List<string>();
            MigrateRendererData(rendererData, logMessages);

            foreach (var msg in logMessages)
            {
                Debug.Log(msg);
            }
        }
        
        public static bool MigrateRendererData(ScriptableRendererData rendererData, List<string> logMessages)
        {
            if (rendererData == null) return false;

            //Breaks the inspector UI otherwise after this process
            if(Selection.activeObject == rendererData) Selection.activeObject = null;
            
            string rendererName = rendererData.name;
            Undo.RegisterCompleteObjectUndo(rendererData, $"Migrate SCPE Render Features: {rendererName}");

            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty featuresProp = serializedRenderer.FindProperty("m_RendererFeatures");

            if (featuresProp == null || !featuresProp.isArray)
            {
                logMessages.Add($"[Error] Could not find 'm_RendererFeatures' on renderer \"{rendererName}\".");
                return false;
            }

            serializedRenderer.Update();

            bool modified = false;
            int featureCount = featuresProp.arraySize;

            for (int i = 0; i < featureCount; i++)
            {
                SerializedProperty featElement = featuresProp.GetArrayElementAtIndex(i);
                ScriptableRendererFeature oldFeature = featElement.objectReferenceValue as ScriptableRendererFeature;

                if (oldFeature == null)
                {
                    if (InstanceNotNull(featElement))
                    {
                        logMessages.Add($"[Warning] Renderer \"{rendererName}\" has missing feature at index {i} (v2 already deleted). Cannot automatically migrate missing script.");
                    }
                    continue;
                }

                Type oldType = oldFeature.GetType();
                if (!IsV2RendererFeature(oldFeature)) continue;

                if (!TryGetRendererFeatureMapping(oldType, out EffectTypeMapping mapping))
                {
                    logMessages.Add($"[Warning] No v3 mapping found for v2 render feature \"{oldType.FullName}\" in renderer \"{rendererName}\".");
                    continue;
                }

                try
                {
                    // Create v3 instance
                    ScriptableRendererFeature newFeature = (ScriptableRendererFeature)ScriptableRendererFeature.CreateInstance(mapping.v3RendererType.ToString());
                    newFeature.name = "[SC Screen Effects] " + mapping.v3Type.Name;
                    newFeature.SetActive(oldFeature.isActive);

                    // Add as sub-asset
                    AssetDatabase.AddObjectToAsset(newFeature, rendererData);
                    Undo.RegisterCreatedObjectUndo(newFeature, "Create v3 Render Feature");

                    // Copy serialized settings
                    SerializedObject oldObj = new SerializedObject(oldFeature);
                    SerializedObject newObj = new SerializedObject(newFeature);
                    CopySerializedProperties(oldObj, newObj, mapping.propertyAliases);

                    // Replace reference in renderer
                    featElement.objectReferenceValue = newFeature;

                    // Destroy old feature
                    Undo.DestroyObjectImmediate(oldFeature);

                    modified = true;
                    logMessages.Add($"[Success] Migrated render feature \"{oldType.Name}\" to \"{mapping.v3RendererType.Name}\" in renderer \"{rendererName}\".");
                }
                catch (Exception ex)
                {
                    logMessages.Add($"[Error] Failed migrating render feature \"{oldType.Name}\" in renderer \"{rendererName}\": {ex.Message}");
                    Debug.LogException(ex);
                }
            }

            if (modified)
            {
                serializedRenderer.ApplyModifiedProperties();

                // Call OnValidate on renderer data to refresh internal pass map
                MethodInfo validateInfo = typeof(ScriptableRendererData).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
                validateInfo?.Invoke(rendererData, null);

                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
            }

            return modified;
        }
        #endregion

        #region Editor Window
        public class MigrationWindow : EditorWindow
        {
            private List<VolumeProfile> m_VolumeProfiles = new List<VolumeProfile>();
            private List<ScriptableRendererData> m_Renderers = new List<ScriptableRendererData>();
            private List<string> m_Messages = new List<string>();

            private Vector2 m_ProfilesScrollPos;
            private Vector2 m_RenderersScrollPos;
            private Vector2 m_MessagesScrollPos;

            private const int WINDOW_WIDTH = 520;
            private const int WINDOW_HEIGHT = 680;

            public static void Open()
            {
                MigrationWindow window = GetWindow<MigrationWindow>(true, "SC Screen Effects v3 Migration Tool", true);
                window.minSize = new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);
                window.Show();
                window.Refresh();
            }

            private void OnEnable()
            {
                Refresh();
            }

            private void OnFocus()
            {
                Refresh();
            }

            public void Refresh()
            {
                m_Messages.Clear();

                m_VolumeProfiles = FindVolumeProfilesWithV2Components(out List<string> profileWarnings);
                m_Messages.AddRange(profileWarnings);

                m_Renderers = FindRenderersWithV2Features(out List<string> rendererWarnings);
                m_Messages.AddRange(rendererWarnings);

                Repaint();
            }

            private void OnGUI()
            {
                DrawHeader();

                EditorGUILayout.Space(8);

                DrawVolumeProfilesSection();

                EditorGUILayout.Space(12);

                DrawRenderersSection();

                EditorGUILayout.Space(12);

                DrawMessagesSection();
            }

            private void DrawHeader()
            {
                EditorGUILayout.HelpBox(
                    "This tool migrates SCPE v2 Volume Components and Render Features to SC Screen Effects v3.\n\n" +
                    "• Recommended: Keep SCPE v2 installed while running this migration.\n" +
                    "• Always make a project backup or version control commit before migrating.\n" +
                    "• Modifies Volume Profile and ScriptableRendererData assets in place.",
                    MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent(" Refresh", EditorGUIUtility.IconContent("d_RotateTool").image), GUILayout.Width(100), GUILayout.Height(24)))
                    {
                        Refresh();
                    }
                }
            }

            private void DrawVolumeProfilesSection()
            {
                EditorGUILayout.LabelField($"SCPE Volume Profiles ({m_VolumeProfiles.Count})", EditorStyles.boldLabel);

                float listHeight = Mathf.Clamp(m_VolumeProfiles.Count * 22f + 10f, 40f, 130f);
                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ProfilesScrollPos, EditorStyles.helpBox, GUILayout.Height(listHeight)))
                {
                    m_ProfilesScrollPos = scrollView.scrollPosition;

                    if (m_VolumeProfiles.Count == 0)
                    {
                        EditorGUILayout.LabelField("No Volume Profiles containing SCPE v2 components found.", EditorStyles.miniLabel);
                    }
                    else
                    {
                        foreach (var profile in m_VolumeProfiles)
                        {
                            using (new EditorGUI.DisabledGroupScope(true))
                            {
                                EditorGUILayout.ObjectField(profile, typeof(VolumeProfile), false);
                            }
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledGroupScope(m_VolumeProfiles.Count == 0))
                    {
                        if (GUILayout.Button("Migrate all profiles", GUILayout.Width(180), GUILayout.Height(26)))
                        {
                            MigrateAllProfiles();
                        }
                    }
                }
            }

            private void DrawRenderersSection()
            {
                EditorGUILayout.LabelField($"SCPE Renderers ({m_Renderers.Count})", EditorStyles.boldLabel);

                float listHeight = Mathf.Clamp(m_Renderers.Count * 22f + 10f, 40f, 130f);
                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_RenderersScrollPos, EditorStyles.helpBox, GUILayout.Height(listHeight)))
                {
                    m_RenderersScrollPos = scrollView.scrollPosition;

                    if (m_Renderers.Count == 0)
                    {
                        EditorGUILayout.LabelField("No Renderers containing SCPE v2 features found.", EditorStyles.miniLabel);
                    }
                    else
                    {
                        foreach (var renderer in m_Renderers)
                        {
                            using (new EditorGUI.DisabledGroupScope(true))
                            {
                                EditorGUILayout.ObjectField(renderer, typeof(ScriptableRendererData), false);
                            }
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledGroupScope(m_Renderers.Count == 0))
                    {
                        if (GUILayout.Button("Migrate render features", GUILayout.Width(180), GUILayout.Height(26)))
                        {
                            MigrateAllRenderFeatures();
                        }
                    }
                }
            }

            private void DrawMessagesSection()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Messages ({m_Messages.Count})", EditorStyles.boldLabel);
                    if (m_Messages.Count > 0 && GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        m_Messages.Clear();
                    }
                }

                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_MessagesScrollPos, EditorStyles.helpBox, GUILayout.Height(110)))
                {
                    m_MessagesScrollPos = scrollView.scrollPosition;

                    if (m_Messages.Count == 0)
                    {
                        EditorGUILayout.LabelField("No messages.", EditorStyles.miniLabel);
                    }
                    else
                    {
                        foreach (string msg in m_Messages)
                        {
                            MessageType msgType = MessageType.Info;
                            if (msg.StartsWith("[Error]")) msgType = MessageType.Error;
                            else if (msg.StartsWith("[Warning]")) msgType = MessageType.Warning;

                            EditorGUILayout.HelpBox(msg, msgType);
                        }
                    }
                }
            }

            private void MigrateAllProfiles()
            {
                if (!EditorUtility.DisplayDialog("Migrate SCPE Volume Profiles",
                    $"Are you sure you want to migrate {m_VolumeProfiles.Count} Volume Profile(s)?\n\nThis will replace SCPE v2 components with SCSE v3 components.",
                    "Migrate", "Cancel"))
                {
                    return;
                }

                try
                {
                    AssetDatabase.StartAssetEditing();

                    List<VolumeProfile> profilesToMigrate = new List<VolumeProfile>(m_VolumeProfiles);
                    int migratedCount = 0;

                    for (int i = 0; i < profilesToMigrate.Count; i++)
                    {
                        VolumeProfile profile = profilesToMigrate[i];
                        if (profile == null) continue;

                        EditorUtility.DisplayProgressBar("Migrating SCPE Volume Profiles", $"Processing {profile.name} ({i + 1}/{profilesToMigrate.Count})...", (float)i / profilesToMigrate.Count);

                        if (MigrateVolumeProfile(profile, m_Messages))
                        {
                            migratedCount++;
                        }
                    }

                    m_Messages.Add($"[Success] Migration completed. Successfully migrated {migratedCount} Volume Profile(s).");
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Refresh();
                }
            }

            private void MigrateAllRenderFeatures()
            {
                if (!EditorUtility.DisplayDialog("Migrate SCPE Render Features",
                    $"Are you sure you want to migrate {m_Renderers.Count} Renderer(s)?\n\nThis will replace SCPE v2 Render Features with SCSE v3 Render Features.",
                    "Migrate", "Cancel"))
                {
                    return;
                }

                try
                {
                    AssetDatabase.StartAssetEditing();

                    List<ScriptableRendererData> renderersToMigrate = new List<ScriptableRendererData>(m_Renderers);
                    int migratedCount = 0;

                    for (int i = 0; i < renderersToMigrate.Count; i++)
                    {
                        ScriptableRendererData renderer = renderersToMigrate[i];
                        if (renderer == null) continue;

                        EditorUtility.DisplayProgressBar("Migrating SCPE Render Features", $"Processing {renderer.name} ({i + 1}/{renderersToMigrate.Count})...", (float)i / renderersToMigrate.Count);

                        if (MigrateRendererData(renderer, m_Messages))
                        {
                            migratedCount++;
                        }
                    }

                    m_Messages.Add($"[Success] Migration completed. Successfully migrated {migratedCount} Renderer(s).");
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Refresh();
                }
            }
        }
        #endregion
    }
}
