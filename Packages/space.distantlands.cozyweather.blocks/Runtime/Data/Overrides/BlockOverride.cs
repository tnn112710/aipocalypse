
using UnityEngine;

namespace DistantLands.Cozy.Blocks
{
    [System.Serializable]
    public struct BlockFloatValue
    {
        public bool disabled;
        public float value;

        public BlockFloatValue(float value)
        {
            disabled = false;
            this.value = value;
        }
    }

    [System.Serializable]
    public struct BlockColorValue
    {
        public bool disabled;
        [ColorUsage(true, true)]
        public Color value;

        public BlockColorValue(Color value)
        {
            disabled = false;
            this.value = value;
        }
    }

    [System.Serializable]
    public struct BlockVector3Value
    {
        public bool disabled;
        public Vector3 value;

        public BlockVector3Value(Vector3 value)
        {
            disabled = false;
            this.value = value;
        }
    }

    [System.Serializable]
    public struct BlockBoolValue
    {
        public bool disabled;
        public bool value;

        public BlockBoolValue(bool value)
        {
            disabled = false;
            this.value = value;
        }
    }

    public sealed class BlockPropertyAttribute : PropertyAttribute
    {
        public readonly VarType varType;
        public readonly string fieldTitle;
        public float minFloat;
        public float maxFloat;

        public BlockPropertyAttribute(VarType varType, string title)
        {
            this.varType = varType;
            fieldTitle = title;
        }
    }

    [System.Serializable]
    public class BlockOverride
    {
        public bool active = true;
        public virtual string Title => "Block Override";
        public virtual string Category => "";
        public virtual bool FeatureEnabledGlobally => true;
        public virtual void ToggleFeature() { }

        public virtual bool CaptureFromParameters(AtmosphereParameters parameters)
        {
            return false;
        }

        public virtual void ApplyToWeatherSphere(AtmosphereParameters parameters, float weight)
        {

        }
    }
}