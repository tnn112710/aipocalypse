//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    [Flags]
    public enum FeatureUsage
    {
        /// <summary>
        /// Only appears as toggle in the features window
        /// </summary>
        ToggleOnly = 1,
        /// <summary>
        /// Only appears as an override on the atmosphere profile
        /// </summary>
        OverrideOnly = 2,
        /// <summary>
        /// Sends variables to the shader
        /// </summary>
        HLSLOnly = 4,
        /// <summary>
        /// Appears in the features window as a toggle and as an override on the atmosphere profile
        /// </summary>
        ToggleAndCreateOverrides = ToggleOnly | OverrideOnly,
        /// <summary>
        /// Appears in the features window as a toggle and sends variables to the shader
        /// </summary>
        ToggleAndHLSL = ToggleOnly | HLSLOnly,
        All = ToggleOnly | OverrideOnly | HLSLOnly,
    }
    [Serializable]
    public class FeatureData
    {
        public string name;
        public string description;
        public string shaderName;

        public FeatureUsage featureUsage;
        public List<IVariableData> variables = new();
        public PerformanceOverhead overhead;
        public FeatureData(string name,
            string description = "",
            FeatureUsage featureUsage = FeatureUsage.All,
            PerformanceOverhead overhead = PerformanceOverhead.Lightweight)
        {
            this.name = name;
            this.description = description;
            this.featureUsage = featureUsage;
            this.overhead = overhead;
            variables = new List<IVariableData>();
        }

        public FeatureData With(IVariableData variable)
        {
            variables ??= new List<IVariableData>();

            variables.Add(variable);
            return this;
        }

        public FeatureData UseShader(string shaderName)
        {
            this.shaderName = shaderName;
            return this;
        }

    }

    public static class FeatureDataFactory
    {
        public static FeatureData WithColor(this FeatureData feature, string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, string additionalAttributeProperties = "", ColorValue disabledValue = default, string presetValue = "", string defaultValue = "")
            => feature.With(new ColorVariable(name, tooltip, variableUsage, disabledValue, presetValue, defaultValue, additionalAttributeProperties));

        public static FeatureData WithFloat(this FeatureData feature, string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, string additionalAttributeProperties = "", FloatValue disabledValue = default, string presetValue = "", string defaultValue = "")
            => feature.With(new FloatVariable(name, tooltip, variableUsage, disabledValue, presetValue, defaultValue, additionalAttributeProperties));

        public static FeatureData WithVector3(this FeatureData feature, string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, string additionalAttributeProperties = "", Vector3Value disabledValue = default, string presetValue = "", string defaultValue = "")
            => feature.With(new Vector3Variable(name, tooltip, variableUsage, disabledValue, presetValue, defaultValue, additionalAttributeProperties));

        public static FeatureData WithTexture2D(this FeatureData feature, string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, string additionalAttributeProperties = "", Texture2DValue disabledValue = default, string presetValue = "", string defaultValue = "")
            => feature.With(new Texture2DVariable(name, tooltip, variableUsage, disabledValue, presetValue, defaultValue, additionalAttributeProperties));

        public static FeatureData WithTexture3D(this FeatureData feature, string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, string additionalAttributeProperties = "", Texture3DValue disabledValue = default, string presetValue = "", string defaultValue = "")
            => feature.With(new Texture3DVariable(name, tooltip, variableUsage, disabledValue, presetValue, defaultValue, additionalAttributeProperties));

        public static FeatureData WithCubemap(this FeatureData feature, string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, string additionalAttributeProperties = "", CubemapValue disabledValue = default, string presetValue = "", string defaultValue = "")
            => feature.With(new CubemapVariable(name, tooltip, variableUsage, disabledValue, presetValue, defaultValue, additionalAttributeProperties));

        public static FeatureData WithBool(this FeatureData feature, string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, string additionalAttributeProperties = "", BoolValue disabledValue = default, string presetValue = "", string defaultValue = "")
            => feature.With(new BoolVariable(name, tooltip, variableUsage, disabledValue, presetValue, defaultValue, additionalAttributeProperties));

        public static FeatureData WithEnum<T>(this FeatureData feature, string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, string additionalAttributeProperties = "", EnumValue<T> disabledValue = default, string presetValue = "", string defaultValue = "")
            => feature.With(new EnumVariable<T>(name, tooltip, variableUsage, disabledValue, presetValue, defaultValue, additionalAttributeProperties));

        public static FeatureData WithObject<T>(this FeatureData feature, string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, string additionalAttributeProperties = "", ObjectValue<T> disabledValue = default, string presetValue = "", string defaultValue = "")
            => feature.With(new ObjectVariable<T>(name, tooltip, variableUsage, disabledValue, presetValue, defaultValue, additionalAttributeProperties));
    }
}