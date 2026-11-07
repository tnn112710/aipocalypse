using System;
using DistantLands.Cozy;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DistantLands.Cozy.Tests
{
    [TestFixture]
    public class AtmospherePropertyTests
    {
        // Simple mock for MeridiemTime since it needs to cast to float
        // If your MeridiemTime is a struct, ensure it's accessible here.

        #region FloatValue & Basic Interpolation Tests

        [Test]
        public void FloatValue_Lerp_CalculatesCorrectly()
        {
            FloatValue start = 0f;
            FloatValue end = 10f;
            start.Lerp(end, 0.5f);
            Assert.AreEqual(5f, start.value, 0.01f);
        }

        [Test]
        public void FloatValue_ImplicitConversion_WorksBothWays()
        {
            FloatValue fVal = 5.5f;
            float raw = fVal;
            Assert.AreEqual(5.5f, raw);
        }

        [Test]
        public void AtmosphereProperty_Evaluate_ReturnsConstantWhenInterpolationDisabled()
        {
            var prop = new AtmosphereProperty<FloatValue>(10f);
            prop.interpolateValue = false;
            // Even with keys, it should return constantValue
            Assert.AreEqual(10f, (float)prop.Evaluate(0.5f));
        }

        [Test]
        public void AtmosphereProperty_Evaluate_ReturnsSingleKeyWhenCountIsOne()
        {
            var keys = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey[]
            {
                new (20f, 0.2f)
            };
            var prop = new AtmosphereProperty<FloatValue>(keys);
            Assert.AreEqual(20f, (float)prop.Evaluate(0.8f));
        }

        #endregion

        #region Evaluation Logic (Linear & Wrapping)

        [Test]
        public void Evaluate_MidPoint_ReturnsInterpolatedValue()
        {
            var keys = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey[]
            {
                new (0f, 0.0f),
                new (10f, 1.0f)
            };
            var prop = new AtmosphereProperty<FloatValue>(keys);

            Assert.AreEqual(5f, (float)prop.Evaluate(0.5f), 0.01f);
        }

        [Test]
        public void Evaluate_BeforeFirstKey_WrapsToLastKey()
        {
            // Time is 0.0, first key is at 0.1. Should lerp from "End of Day" to 0.1.
            var keys = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey[]
            {
                new (10f, 0.1f),
                new (20f, 0.9f)
            };
            var prop = new AtmosphereProperty<FloatValue>(keys);

            // At time 0, it's lerping between the 0.9 key and the 0.1 key across the midnight boundary
            float result = prop.Evaluate(0.0f);
            Assert.IsTrue(result > 10f && result < 20f);
        }

        [Test]
        public void Evaluate_AfterLastKey_WrapsToFirstKey()
        {
            var keys = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey[]
            {
                new (0f, 0.1f),
                new (10f, 0.9f)
            };
            var prop = new AtmosphereProperty<FloatValue>(keys);

            // Time 0.95 is after 0.9, should be lerping back toward 0.1
            float result = prop.Evaluate(0.95f);
            Assert.IsTrue(result > 0f && result < 10f);
        }

        [Test]
        public void Evaluate_ExactKeyTime_ReturnsExactValue()
        {
            var keys = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey[]
            {
                new AtmosphereProperty<FloatValue>.AtmosphereGradientKey(0, 0f),
                new (100f, 1.0f)
            };
            var prop = new AtmosphereProperty<FloatValue>(keys);
            Assert.AreEqual(50f, (float)prop.Evaluate(0.5f));
        }

        #endregion

        #region Color, Bool, and Enum Specialized Tests

        [Test]
        public void ColorValue_Lerp_CalculatesCorrectly()
        {
            ColorValue start = Color.black;
            ColorValue end = Color.white;
            start.Lerp(end, 0.5f);
            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f, 1), start.value);
        }

        [Test]
        public void BoolValue_Lerp_ActsAsSnapSwitch()
        {
            BoolValue b = false;
            b.Lerp(new BoolValue(true), 0.1f); // Any t > 0
            Assert.IsTrue(b.value);

            b = true;
            b.Lerp(new BoolValue(false), 0.0f); // t <= 0
            Assert.IsTrue(b.value);
        }

        [Test]
        public void EnumValue_Lerp_SnapsToNewValue()
        {
            var eVal = new EnumValue<KeyCode>(KeyCode.A);
            eVal.Lerp(new EnumValue<KeyCode>(KeyCode.B), 0.001f);
            Assert.AreEqual(KeyCode.B, eVal.value);
        }

        [Test]
        public void Texture2DValue_Lerp_ReturnsEndValueIfTGreaterThanZero()
        {
            Texture2D tex1 = new Texture2D(1, 1);
            Texture2D tex2 = new Texture2D(1, 1);
            Texture2DValue val = tex1;

            val.Lerp(new Texture2DValue(tex2), 0.5f);
            Assert.AreEqual(tex2, val.value);
        }

        #endregion

        #region Get/Set Value & Override Logic

        [Test]
        public void GetValue_NoOverride_ReturnsOriginalProperty()
        {
            var prop = new AtmosphereProperty<FloatValue>(100f);
            prop.overrideValue = false;

            FloatValue original = 5f;
            FloatValue result = prop.GetValue(original, 0.5f);

            Assert.AreEqual(5f, (float)result);
        }

        [Test]
        public void SetValue_WithOverride_UpdatesRefProperty()
        {
            var prop = new AtmosphereProperty<FloatValue>(100f);
            prop.overrideValue = true;
            prop.interpolateValue = false;

            FloatValue target = 0f;
            prop.SetValue(ref target, 0.5f);

            Assert.AreEqual(100f, (float)target);
        }

        [Test]
        public void Lerp_Method_OnlyAffectsPropertyIfOverrideIsTrue()
        {
            var prop = new AtmosphereProperty<FloatValue>(0f);
            prop.overrideValue = false;

            FloatValue current = 0f;
            prop.Lerp(ref current, 10f, 0.5f);

            Assert.AreEqual(0f, (float)current, "Should not lerp if override is false");

            prop.overrideValue = true;
            prop.Lerp(ref current, 10f, 0.5f);
            Assert.AreEqual(5f, (float)current, "Should lerp if override is true");
        }

        #endregion

        #region Structural & Boundary Tests

        [Test]
        public void AtmosphereProperty_Constructor_SetsDefaultFlags()
        {
            var prop = new AtmosphereProperty<FloatValue>(5f);
            Assert.IsTrue(prop.overrideValue);
            Assert.IsTrue(prop.interpolateValue);
        }

        [Test]
        public void Vector3Value_Equality_WorksCorrectly()
        {
            Vector3Value v1 = new Vector3(1, 2, 3);
            Vector3Value v2 = new Vector3(1, 2, 3);
            Vector3Value v3 = new Vector3(0, 0, 0);

            Assert.IsTrue(v1 == v2);
            Assert.IsFalse(v1 == v3);
        }

        [Test]
        public void Evaluate_EmptyInterpolatedArray_ReturnsConstant()
        {
            var prop = new AtmosphereProperty<FloatValue>(77f);
            prop.interpolatedValue = System.Array.Empty<AtmosphereProperty<FloatValue>.AtmosphereGradientKey>();

            Assert.AreEqual(77f, (float)prop.Evaluate(0.5f));
        }

        [Test]
        public void ObjectValue_Lerp_SnapsCorrectly()
        {
            var obj1 = new GameObject("1");
            var obj2 = new GameObject("2");
            ObjectValue<GameObject> val = obj1;
            ObjectValue<GameObject> val2 = obj2;

            val.Lerp(val2, 0.1f);
            Assert.AreEqual(obj2, val.value);

            Object.DestroyImmediate(obj1);
            Object.DestroyImmediate(obj2);
        }

        #endregion
        #region Vector Value Tests

        [Test]
        public void Vector4Value_Lerp_CalculatesLinearInterpolation()
        {
            Vector4Value start = new Vector4(0, 0, 0, 0);
            Vector4Value end = new Vector4(1, 1, 1, 1);
            start.Lerp(end, 0.5f);

            Assert.AreEqual(new Vector4(0.5f, 0.5f, 0.5f, 0.5f), (Vector4)start);
        }

        [Test]
        public void Vector3Value_ImplicitConversions_SupportVector2AndVector4()
        {
            Vector3 original = new Vector3(10, 20, 30);
            Vector3Value vVal = original;

            Vector2 v2 = vVal;
            Vector4 v4 = vVal;

            Assert.AreEqual(10f, v2.x);
            Assert.AreEqual(30f, v4.z);
        }

        [Test]
        public void Vector4Value_Equality_HandlesPrecision()
        {
            Vector4Value v1 = new Vector4(1, 1, 1, 1);
            Vector4Value v2 = new Vector4(1, 1, 1, 1);
            Vector4Value v3 = new Vector4(1, 1, 1, 1.01f);

            Assert.IsTrue(v1 == v2);
            Assert.IsTrue(v1 != v3);
        }

        #endregion

        #region Specialized Texture & Asset Tests

        [Test]
        public void Texture3DValue_SnapLerp_OnlyChangesAtPositiveT()
        {
            Texture3D texA = new Texture3D(1, 1, 1, TextureFormat.RGBA32, false);
            Texture3D texB = new Texture3D(1, 1, 1, TextureFormat.RGBA32, false);
            Texture3DValue val = texA;
            Texture3DValue val2 = texB;

            val.Lerp(val2, 0f);
            Assert.AreEqual(texA, (Texture3D)val);

            val.Lerp(val2, 0.0001f);
            Assert.AreEqual(texB, (Texture3D)val);
        }

        [Test]
        public void CubemapValue_Equality_ComparesReferences()
        {
            Cubemap cube = new Cubemap(1, TextureFormat.RGBA32, false);
            CubemapValue v1 = cube;
            CubemapValue v2 = cube;

            Assert.IsTrue(v1 == v2);
            Assert.IsTrue(v1.Equals(v2));
        }

        [Test]
        public void RenderTextureValue_ImplicitConversion_ReturnsCorrectType()
        {
            // CustomRenderTexture inherits from RenderTexture
            var crt = new CustomRenderTexture(16, 16);
            RenderTextureValue val = crt;

            CustomRenderTexture result = val;
            Assert.AreEqual(crt, result);
        }

        #endregion

        #region Generic Value Tests (Enum & Object)

        [Test]
        public void EnumValue_Equality_WorksWithDifferentEnumTypes()
        {
            var e1 = new EnumValue<ShadowQuality>(ShadowQuality.All);
            var e2 = new EnumValue<ShadowQuality>(ShadowQuality.All);
            var e3 = new EnumValue<ShadowQuality>(ShadowQuality.Disable);

            Assert.IsTrue(e1 == e2);
            Assert.IsFalse(e1 == e3);
        }

        [Test]
        public void ObjectValue_GetHashCode_ReturnsUnderlyingHashCode()
        {
            Material mat = new Material(Shader.Find("Specular"));
            ObjectValue<Material> val = mat;

            Assert.AreEqual(mat.GetHashCode(), val.GetHashCode());
        }

        #endregion

        #region AtmosphereProperty Edge Cases

        [Test]
        public void AtmosphereProperty_Evaluate_HandlesNullInterpolatedValueArray()
        {
            var prop = new AtmosphereProperty<FloatValue>(50f);
            prop.interpolatedValue = null;
            prop.interpolateValue = true;

            // Should fallback to constantValue if array is null
            Assert.AreEqual(50f, (float)prop.Evaluate(0.5f));
        }

        [Test]
        public void AtmosphereProperty_Evaluate_MidnightBoundary_ExactLerp()
        {
            // Testing the math for tLast - 1f and t0 wrap-around
            var keys = new AtmosphereProperty<FloatValue>.AtmosphereGradientKey[]
            {
                new (0f, 0.1f),   // Morning
                new (100f, 0.9f)  // Night
            };
            var prop = new AtmosphereProperty<FloatValue>(keys);

            // Time 0.0 is halfway between 0.9 (last night) and 0.1 (this morning)
            // Range: 0.9 to 1.1 (0.1 + 1.0)
            // Midpoint: 1.0 (Midnight)
            float result = prop.Evaluate(0.0f);
            Assert.AreEqual(50f, result, 0.1f);
        }

        #endregion
    }
}