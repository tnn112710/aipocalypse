//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    public struct AtmosphereProperty<T> where T : IInterpolatedValue<T>
    {
        public T constantValue;
        public bool overrideValue;
        public bool interpolateValue;

        [Serializable]
        public struct AtmosphereGradientKey
        {
            public T value;
            public MeridiemTime time;
            public AtmosphereGradientKey(T value, MeridiemTime time)
            {
                this.value = value;
                this.time = time;
            }
        }
        [NonSerialized]
        internal bool dirtyMask;

        public AtmosphereGradientKey[] interpolatedValue;

        public AtmosphereProperty(T value)
        {
            constantValue = value;
            overrideValue = true;
            interpolateValue = true;
            dirtyMask = true;
            interpolatedValue = new AtmosphereGradientKey[1] { new() };
        }

        public AtmosphereProperty(AtmosphereGradientKey[] values)
        {
            constantValue = default;
            overrideValue = true;
            interpolateValue = true;
            dirtyMask = true;
            interpolatedValue = values;
        }

        public T Evaluate(MeridiemTime time)
        {

            if (!interpolateValue || interpolatedValue == null || interpolatedValue.Length == 0)
                return constantValue;

            int count = interpolatedValue.Length;

            if (count == 1)
                return interpolatedValue[0].value;

            float t = time.AsPercentage();
            float t0 = interpolatedValue[0].time.AsPercentage();
            float tLast = interpolatedValue[^1].time.AsPercentage();

            if (t < t0)
            {
                T result = interpolatedValue[^1].value;
                result.Lerp(interpolatedValue[0].value, Mathf.InverseLerp(tLast - 1f, t0, t));
                return result;
            }
            if (t > tLast)
            {
                T result = interpolatedValue[^1].value;
                result.Lerp(interpolatedValue[0].value, Mathf.InverseLerp(tLast, t0 + 1f, t));
                return result;
            }

            for (int i = 0; i < count; i++)
            {
                int nextIndex = (i + 1) % count;
                float t1 = interpolatedValue[nextIndex].time.AsPercentage();
                float tCurr = interpolatedValue[i].time.AsPercentage();

                if (t <= t1)
                {
                    T start = interpolatedValue[i].value;
                    T end = interpolatedValue[nextIndex].value;

                    T result = start;
                    result.Lerp(end, Mathf.InverseLerp(tCurr, t1, t));
                    return result;
                }
            }

            return constantValue;
        }


        public T SetValue(ref T property)
        {
            return SetValue(ref property, CozyWeather.Instance.Time.CurrentTime);
        }

        public T SetValue(ref T property, float time)
        {
            if (overrideValue)
            {
                if (interpolateValue)
                {
                    property = Evaluate(time);
                }
                else
                {
                    property = constantValue;
                }
            }

            return property;
        }

        public T GetValue(T property, float time)
        {
            if (overrideValue)
            {
                if (interpolateValue)
                {
                    property = Evaluate(time);
                }
                else
                {
                    property = constantValue;
                }
            }

            return property;
        }

        public T Lerp(ref T property, T end, float t)
        {
            if (overrideValue)
            {
                property.Lerp(end, t);
            }

            return property;
        }
    }

    public interface IInterpolatedValue<T>
    {
        void Lerp(T end, float t)
        {

        }
    }

    [Serializable]
    public struct FloatValue : IInterpolatedValue<FloatValue>
    {
        public float value;
        public FloatValue(float val)
        {
            value = val;
        }
        public static implicit operator FloatValue(float val)
        {
            return new FloatValue(val);
        }
        public static implicit operator float(FloatValue val)
        {
            return val.value;
        }
        public static bool operator ==(FloatValue val1, FloatValue val2)
        {
            return val1.value == val2.value;
        }
        public static bool operator !=(FloatValue val1, FloatValue val2)
        {
            return val1.value != val2.value;
        }
        public override bool Equals(object o)
        {
            if (o is FloatValue floatValue)
                return value.Equals(floatValue.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(FloatValue end, float t)
        {
            value = Mathf.Lerp(value, end, t);
        }
    }

    [Serializable]
    public struct ColorValue : IInterpolatedValue<ColorValue>
    {
        [ColorUsage(true, true)]
        public Color value;
        public ColorValue(Color val)
        {
            value = val;
        }
        public static implicit operator ColorValue(Color val)
        {
            return new ColorValue(val);
        }
        public static implicit operator Color(ColorValue val)
        {
            return val.value;
        }
        public static bool operator ==(ColorValue val1, ColorValue val2)
        {
            return val1.value == val2.value;
        }
        public static bool operator !=(ColorValue val1, ColorValue val2)
        {
            return val1.value != val2.value;
        }
        public override bool Equals(object o)
        {
            if (o is ColorValue colorValue)
                return value.Equals(colorValue.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(ColorValue end, float t)
        {
            value = Color.Lerp(value, end, t);
        }
    }

    [Serializable]
    public struct Vector4Value : IInterpolatedValue<Vector4Value>
    {
        public Vector4 value;
        public Vector4Value(Vector4 val)
        {
            value = val;
        }
        public static implicit operator Vector4Value(Vector4 val)
        {
            return new Vector4Value(val);
        }
        public static implicit operator Vector2(Vector4Value val)
        {
            return val.value;
        }
        public static implicit operator Vector3(Vector4Value val)
        {
            return val.value;
        }
        public static implicit operator Vector4(Vector4Value val)
        {
            return val.value;
        }
        public static bool operator ==(Vector4Value val1, Vector4Value val2)
        {
            return val1.value == val2.value;
        }
        public static bool operator !=(Vector4Value val1, Vector4Value val2)
        {
            return val1.value != val2.value;
        }
        public override bool Equals(object o)
        {
            if (o is Vector4Value vector4Value)
                return value.Equals(vector4Value.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(Vector4Value end, float t)
        {
            value = Vector4.Lerp(value, end, t);
        }
    }
    [Serializable]
    public struct Vector3Value : IInterpolatedValue<Vector3Value>
    {
        public Vector3 value;
        public Vector3Value(Vector3 val)
        {
            value = val;
        }
        public static implicit operator Vector3Value(Vector3 val)
        {
            return new Vector3Value(val);
        }
        public static implicit operator Vector2(Vector3Value val)
        {
            return val.value;
        }
        public static implicit operator Vector3(Vector3Value val)
        {
            return val.value;
        }
        public static implicit operator Vector4(Vector3Value val)
        {
            return val.value;
        }
        public static bool operator ==(Vector3Value val1, Vector3Value val2)
        {
            return val1.value == val2.value;
        }
        public static bool operator !=(Vector3Value val1, Vector3Value val2)
        {
            return val1.value != val2.value;
        }
        public override bool Equals(object o)
        {
            if (o is Vector3Value vector3Value)
                return value.Equals(vector3Value.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(Vector3Value end, float t)
        {
            value = Vector3.Lerp(value, end, t);
        }
    }

    [Serializable]
    public struct Texture2DValue : IInterpolatedValue<Texture2DValue>
    {
        public Texture2D value;
        public Texture2DValue(Texture2D val)
        {
            value = val;
        }
        public static implicit operator Texture2DValue(Texture2D val)
        {
            return new Texture2DValue(val);
        }
        public static implicit operator Texture2D(Texture2DValue val)
        {
            return val.value;
        }
        public static bool operator ==(Texture2DValue val1, Texture2DValue val2)
        {
            return val1.value == val2.value;
        }
        public static bool operator !=(Texture2DValue val1, Texture2DValue val2)
        {
            return val1.value != val2.value;
        }
        public override bool Equals(object o)
        {
            if (o is Texture2DValue texture2DValue)
                return value.Equals(texture2DValue.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(Texture2DValue end, float t)
        {
            value = t > 0f ? end : value;
        }
    }

    [Serializable]
    public struct RenderTextureValue : IInterpolatedValue<RenderTextureValue>
    {
        public CustomRenderTexture value;
        public RenderTextureValue(CustomRenderTexture val)
        {
            value = val;
        }
        public static implicit operator RenderTextureValue(CustomRenderTexture val)
        {
            return new RenderTextureValue(val);
        }
        public static implicit operator CustomRenderTexture(RenderTextureValue val)
        {
            return val.value;
        }
        public static bool operator ==(RenderTextureValue val1, RenderTextureValue val2)
        {
            return val1.value == val2.value;
        }
        public static bool operator !=(RenderTextureValue val1, RenderTextureValue val2)
        {
            return val1.value != val2.value;
        }
        public override bool Equals(object o)
        {
            if (o is RenderTextureValue renderTextureValue)
                return value.Equals(renderTextureValue.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(RenderTextureValue end, float t)
        {
            value = t > 0f ? end : value;
        }
    }

    [Serializable]
    public struct Texture3DValue : IInterpolatedValue<Texture3DValue>
    {
        public Texture3D value;
        public Texture3DValue(Texture3D val)
        {
            value = val;
        }
        public static implicit operator Texture3DValue(Texture3D val)
        {
            return new Texture3DValue(val);
        }
        public static implicit operator Texture3D(Texture3DValue val)
        {
            return val.value;
        }
        public static bool operator ==(Texture3DValue val1, Texture3DValue val2)
        {
            return val1.value == val2.value;
        }
        public static bool operator !=(Texture3DValue val1, Texture3DValue val2)
        {
            return val1.value != val2.value;
        }
        public override bool Equals(object o)
        {
            if (o is Texture3DValue texture3DValue)
                return value.Equals(texture3DValue.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(Texture3DValue end, float t)
        {
            value = t > 0f ? end : value;
        }
    }

    [Serializable]
    public struct CubemapValue : IInterpolatedValue<CubemapValue>
    {
        public Cubemap value;
        public CubemapValue(Cubemap val)
        {
            value = val;
        }
        public static implicit operator CubemapValue(Cubemap val)
        {
            return new CubemapValue(val);
        }
        public static implicit operator Cubemap(CubemapValue val)
        {
            return val.value;
        }
        public static bool operator ==(CubemapValue val1, CubemapValue val2)
        {
            return val1.value == val2.value;
        }
        public static bool operator !=(CubemapValue val1, CubemapValue val2)
        {
            return val1.value != val2.value;
        }
        public override bool Equals(object o)
        {
            if (o is CubemapValue cubemapValue)
                return value.Equals(cubemapValue.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(CubemapValue end, float t)
        {
            value = t > 0f ? end : value;
        }
    }

    [Serializable]
    public struct BoolValue : IInterpolatedValue<BoolValue>
    {
        public bool value;
        public BoolValue(bool val)
        {
            value = val;
        }
        public static implicit operator BoolValue(bool val)
        {
            return new BoolValue(val);
        }
        public static implicit operator bool(BoolValue val)
        {
            return val.value;
        }
        public static bool operator ==(BoolValue val1, BoolValue val2)
        {
            return val1.value == val2.value;
        }
        public static bool operator !=(BoolValue val1, BoolValue val2)
        {
            return val1.value != val2.value;
        }
        public override bool Equals(object o)
        {
            if (o is BoolValue boolValue)
                return value.Equals(boolValue.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(BoolValue end, float t)
        {
            value = t > 0f ? end : value;
        }
    }

    [Serializable]
    public struct EnumValue<T> : IInterpolatedValue<EnumValue<T>>
    {
        public T value;
        public EnumValue(T val)
        {
            value = val;
        }
        public static implicit operator EnumValue<T>(T val)
        {
            return new EnumValue<T>(val);
        }
        public static implicit operator T(EnumValue<T> val)
        {
            return val.value;
        }
        public static bool operator ==(EnumValue<T> val1, EnumValue<T> val2)
        {
            return EqualityComparer<T>.Default.Equals(val1.value, val2.value);
        }
        public static bool operator !=(EnumValue<T> val1, EnumValue<T> val2)
        {
            return !EqualityComparer<T>.Default.Equals(val1.value, val2.value);
        }
        public override bool Equals(object o)
        {
            if (o is EnumValue<T> enumValue)
                return value.Equals(enumValue.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(EnumValue<T> end, float t)
        {
            value = t > 0f ? end : value;
        }
    }
    [Serializable]
    public struct ObjectValue<T> : IInterpolatedValue<ObjectValue<T>>
    {
        public T value;
        public ObjectValue(T val)
        {
            value = val;
        }
        public static implicit operator ObjectValue<T>(T val)
        {
            return new ObjectValue<T>(val);
        }
        public static implicit operator T(ObjectValue<T> val)
        {
            return val.value;
        }
        public static bool operator ==(ObjectValue<T> val1, ObjectValue<T> val2)
        {
            return EqualityComparer<T>.Default.Equals(val1.value, val2.value);
        }
        public static bool operator !=(ObjectValue<T> val1, ObjectValue<T> val2)
        {
            return !EqualityComparer<T>.Default.Equals(val1.value, val2.value);
        }
        public override bool Equals(object o)
        {
            if (o is ObjectValue<T> objectValue)
                return value.Equals(objectValue.value);
            return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public void Lerp(ObjectValue<T> end, float t)
        {
            value = t > 0f ? end : value;
        }
    }

}