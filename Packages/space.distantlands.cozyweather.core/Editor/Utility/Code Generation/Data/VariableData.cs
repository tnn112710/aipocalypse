//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Linq;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    [Flags]
    public enum VariableUsage
    {
        None = 0,
    }
    public interface IVariableData
    {
        public string Name { get; }
        public string Tooltip { get; }
        public string AdditionalAttributeProperties { get; }
        public string PresetValue { get; }
        public string DefaultValue { get; }
        public string DeclareHLSLProperty(string propertyDeclaration);
        public string DeclareCSharpProperty();
        public string SetGlobalShaderProperty(string propertyDeclaration);
        public string DisabledValueString { get; }
        public string TypeValueString { get; }
        public string TypeReferenceString { get; }
        public VariableUsage VariableUsage { get; }
    }

    [Serializable]
    public class VariableData<T> : IVariableData where T : IInterpolatedValue<T>, new()
    {
        public string Name => name;
        private readonly string name;
        public string Tooltip => tooltip;
        private readonly string tooltip;
        public string AdditionalAttributeProperties => additionalAttributeProperties;
        public string PresetValue => presetValue;
        private readonly string additionalAttributeProperties;
        public VariableUsage VariableUsage => variableUsage;
        private readonly VariableUsage variableUsage;
        public T disabledValue;
        public string DefaultValue => defaultValue;
        public string defaultValue;
        public string presetValue;

        public VariableData(string name, 
                            string tooltip, 
                            VariableUsage variableUsage = VariableUsage.None,
                            string additionalAttributeProperties = "", 
                            T disabledValue = default, 
                            string presetValue = null, 
                            string defaultValue = ""
                        )
        {
            this.name = name;
            this.tooltip = tooltip;
            this.disabledValue = disabledValue;
            this.variableUsage = variableUsage;
            this.defaultValue = defaultValue;
            this.presetValue = presetValue;
            this.additionalAttributeProperties = additionalAttributeProperties;
        }

        public string DeclareCSharpProperty()
        {
            Type type = typeof(T);
            if (type.IsGenericType)
            {
                string mainName = type.Name[..type.Name.IndexOf('`')];
                string argNames = string.Join(", ", type.GetGenericArguments().Select(a => a.Name));
                return $"{mainName}<{argNames}>";
            }
            return type.Name;
        }
        public virtual string TypeReferenceString => TypeValueString;

        public virtual string TypeValueString
        {
            get
            {
                Type type = typeof(T);
                return type.Name.Replace("Value", "");
            }
        }

        public virtual string DeclareHLSLProperty(string prop)
        {
            return typeof(T).Name switch
            {
                nameof(FloatValue) or nameof(BoolValue) => $"uniform float {prop};",
                nameof(Vector3Value) => $"uniform float3 {prop};",
                nameof(ColorValue) => $"uniform float4 {prop};",
                nameof(Texture2DValue) => $"TEXTURE2D({prop}); SAMPLER(sampler{prop});",
                nameof(Texture3DValue) => $"TEXTURE3D({prop}); SAMPLER(sampler{prop});",
                nameof(CubemapValue) => $"TEXTURECUBE({prop}); SAMPLER(sampler{prop});",
                _ => ""
            };
        }

        public virtual string DisabledValueString
        {
            get => disabledValue.ToString();
        }

        public string SetGlobalShaderProperty(string prop) => typeof(T).Name switch
        {
            nameof(FloatValue) or nameof(BoolValue)
                => $"Shader.SetGlobalFloat(CozyShaderIDs.{prop}, parameters.{prop}.value);",

            "EnumValue"
                => $"Shader.SetGlobalFloat(CozyShaderIDs.{prop}, (int)parameters.{prop}.value);",

            nameof(Vector3Value)
                => $"Shader.SetGlobalVector(CozyShaderIDs.{prop}, parameters.{prop}.value);",

            nameof(ColorValue)
                => $"Shader.SetGlobalColor(CozyShaderIDs.{prop}, parameters.{prop}.value);",

            nameof(Texture2DValue) or nameof(Texture3DValue)
                => $"Shader.SetGlobalTexture(CozyShaderIDs.{prop}, parameters.{prop} == null ? Texture2D.blackTexture : parameters.{prop}.value);",

            nameof(CubemapValue)
                => $"Shader.SetGlobalTexture(CozyShaderIDs.{prop}, parameters.{prop} == null ? CozyUtilities.BlackCubemap : parameters.{prop}.value);",

            _ => ""
        };
    }

    [Serializable]
    public class FloatVariable : VariableData<FloatValue>
    {
        public FloatVariable(string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, FloatValue disabledValue = default, string presetValue = "", string defaultValue = default, string additionalAttributeProperties = "")
            : base(name, tooltip, variableUsage, additionalAttributeProperties: additionalAttributeProperties, disabledValue: disabledValue, presetValue: presetValue, defaultValue: defaultValue) { }

        public override string DisabledValueString => $"{disabledValue.value}f";
    }

    [Serializable]
    public class BoolVariable : VariableData<BoolValue>
    {
        public BoolVariable(string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, BoolValue disabledValue = default, string presetValue = "", string defaultValue = default, string additionalAttributeProperties = "")
            : base(name, tooltip, variableUsage, additionalAttributeProperties: additionalAttributeProperties, disabledValue: disabledValue, presetValue: presetValue, defaultValue: defaultValue) { }

        public override string DisabledValueString => $"{disabledValue.value}".ToLower();
    }

    [Serializable]
    public class Vector3Variable : VariableData<Vector3Value>
    {
        public Vector3Variable(string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, Vector3Value disabledValue = default, string presetValue = "", string defaultValue = default, string additionalAttributeProperties = "")
            : base(name, tooltip, variableUsage, additionalAttributeProperties: additionalAttributeProperties, disabledValue: disabledValue, presetValue: presetValue, defaultValue: defaultValue) { }

        public override string DisabledValueString => $"new Vector3({disabledValue.value.x}f, {disabledValue.value.y}f, {disabledValue.value.z}f)";
    }

    [Serializable]
    public class ColorVariable : VariableData<ColorValue>
    {
        public ColorVariable(string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, ColorValue disabledValue = default, string presetValue = "", string defaultValue = default, string additionalAttributeProperties = "")
            : base(name, tooltip, variableUsage, additionalAttributeProperties: additionalAttributeProperties, disabledValue: disabledValue, presetValue: presetValue, defaultValue: defaultValue) { }

        public override string DisabledValueString => $"new Color({disabledValue.value.r}f, {disabledValue.value.g}f, {disabledValue.value.b}f, {disabledValue.value.a}f)";
    }

    [Serializable]
    public class Texture2DVariable : VariableData<Texture2DValue>
    {
        public Texture2DVariable(string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, Texture2DValue disabledValue = default, string presetValue = "", string defaultValue = default, string additionalAttributeProperties = "")
            : base(name, tooltip, variableUsage, additionalAttributeProperties: additionalAttributeProperties, disabledValue: disabledValue, presetValue: presetValue, defaultValue: defaultValue) { }

        public override string DisabledValueString => $"null";
    }

    [Serializable]
    public class Texture3DVariable : VariableData<Texture3DValue>
    {
        public Texture3DVariable(string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, Texture3DValue disabledValue = default, string presetValue = "", string defaultValue = default, string additionalAttributeProperties = "")
            : base(name, tooltip, variableUsage, additionalAttributeProperties: additionalAttributeProperties, disabledValue: disabledValue, presetValue: presetValue, defaultValue: defaultValue) { }

        public override string DisabledValueString => $"null";
    }

    [Serializable]
    public class CubemapVariable : VariableData<CubemapValue>
    {
        public CubemapVariable(string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, CubemapValue disabledValue = default, string presetValue = "", string defaultValue = default, string additionalAttributeProperties = "")
            : base(name, tooltip, variableUsage, additionalAttributeProperties: additionalAttributeProperties, disabledValue: disabledValue, presetValue: presetValue, defaultValue: defaultValue) { }

        public override string DisabledValueString => $"null";
    }

    [Serializable]
    public class EnumVariable<T> : VariableData<EnumValue<T>>
    {
        public EnumVariable(string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, T disabledValue = default, string presetValue = "", string defaultValue = default, string additionalAttributeProperties = "")
            : base(name, tooltip, variableUsage, additionalAttributeProperties: additionalAttributeProperties, disabledValue: disabledValue, presetValue: presetValue, defaultValue: defaultValue) { }

        public override string DisabledValueString => $"{typeof(T).Name}.{disabledValue.value}";
        public override string DeclareHLSLProperty(string prop) => $"uniform float {prop};";
        public override string TypeValueString => "Enum";
    }

    [Serializable]
    public class ObjectVariable<T> : VariableData<ObjectValue<T>>
    {
        public ObjectVariable(string name, string tooltip, VariableUsage variableUsage = VariableUsage.None, T disabledValue = default, string presetValue = "", string defaultValue = default, string additionalAttributeProperties = "")
            : base(name, tooltip, variableUsage, additionalAttributeProperties: additionalAttributeProperties, disabledValue: disabledValue, presetValue: presetValue, defaultValue: defaultValue) { }

        public override string DisabledValueString => $"null";
        public override string TypeValueString => "Object";
        public override string TypeReferenceString => typeof(T).Name;
    }

}