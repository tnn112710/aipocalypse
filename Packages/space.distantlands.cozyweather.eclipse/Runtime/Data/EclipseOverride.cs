using UnityEngine;

namespace DistantLands.Cozy.Eclipse
{
    [System.Serializable]
    public struct EclipseFloatValue
    {
        public bool disabled;
        public float value;

        public EclipseFloatValue(float value)
        {
            disabled = false;
            this.value = value;
        }
    }

    [System.Serializable]
    public struct EclipseColorValue
    {
        public bool disabled;
        [ColorUsage(true, true)]
        public Color value;

        public EclipseColorValue(Color value)
        {
            disabled = false;
            this.value = value;
        }
    }

    [System.Serializable]
    public struct EclipseVector3Value
    {
        public bool disabled;
        public Vector3 value;

        public EclipseVector3Value(Vector3 value)
        {
            disabled = false;
            this.value = value;
        }
    }

    [System.Serializable]
    public struct EclipseBoolValue
    {
        public bool disabled;
        public bool value;

        public EclipseBoolValue(bool value)
        {
            disabled = false;
            this.value = value;
        }
    }

    public sealed class EclipsePropertyAttribute : PropertyAttribute
    {
        public readonly VarType varType;
        public readonly string fieldTitle;
        public float minFloat;
        public float maxFloat;

        public EclipsePropertyAttribute(VarType varType, string title)
        {
            this.varType = varType;
            fieldTitle = title;
        }
    }

    [System.Serializable]
    public class EclipseOverride
    {
        public bool active = true;
        public virtual string Title => "Eclipse Override";
        public virtual string Category => "";
        public virtual bool FeatureEnabledGlobally => true;
        public virtual void ToggleFeature() { }

        public virtual bool CaptureFromParameters(AtmosphereParameters parameters)
        {
            return false;
        }

        public virtual void ApplyToAtmosphere(AtmosphereParameters parameters, float weight)
        {
        }
    }
}
