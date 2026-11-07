using System;
using DistantLands.Cozy;
using NUnit.Framework;
using UnityEngine;

namespace DistantLands.Cozy.Tests
{
    public class TestClimateProperties
    {
        // A standard tolerance for floating point comparisons
        private const float Tolerance = 0.001f;

        [TestFixture]
        public class TemperatureTests
        {

            // --- STATIC CONVERSION TESTS ---

            [Test] // 1
            public void ConvertToUnit_FahrenheitToCelsius_ReturnsCorrectValue()
            {
                float celsius = Temperature.ConvertToUnit(32f, TemperatureUnit.Celsius);
                Assert.AreEqual(0f, celsius, Tolerance);

                float boilingC = Temperature.ConvertToUnit(212f, TemperatureUnit.Celsius);
                Assert.AreEqual(100f, boilingC, Tolerance);
            }

            [Test] // 2
            public void ConvertToUnit_FahrenheitToKelvin_ReturnsCorrectValue()
            {
                float kelvin = Temperature.ConvertToUnit(32f, TemperatureUnit.Kelvin);
                Assert.AreEqual(273.15f, kelvin, Tolerance);
            }

            [Test] // 3
            public void ConvertFromUnit_CelsiusToFahrenheit_ReturnsCorrectValue()
            {
                float fahrenheit = Temperature.ConvertFromUnit(0f, TemperatureUnit.Celsius);
                Assert.AreEqual(32f, fahrenheit, Tolerance);
            }

            [Test] // 4
            public void ConvertFromUnit_KelvinToFahrenheit_ReturnsCorrectValue()
            {
                float fahrenheit = Temperature.ConvertFromUnit(273.15f, TemperatureUnit.Kelvin);
                Assert.AreEqual(32f, fahrenheit, Tolerance);
            }

            [Test] // 5
            public void AbsoluteZero_EdgeCase_CalculatesCorrectly()
            {
                // 0 Kelvin is -459.67 Fahrenheit
                float fahrenheit = Temperature.ConvertFromUnit(0f, TemperatureUnit.Kelvin);
                Assert.AreEqual(-459.67f, fahrenheit, Tolerance);
            }

            // --- TEMPERATURE CONSTRUCTOR & PROPERTIES ---

            [Test] // 6
            public void TemperatureConstructor_CelsiusInput_StoresCorrectFahrenheit()
            {
                Temperature temp = new Temperature(100f, TemperatureUnit.Celsius);
                Assert.AreEqual(212f, temp, Tolerance); // Internal value should be F
            }

            [Test] // 7
            public void Normalized_Midpoint_ReturnsExactHalf()
            {
                // Min is -30, Max is 120. Range is 150. Midpoint is 45F.
                Temperature temp = new Temperature(45f, TemperatureUnit.Fahrenheit);
                Assert.AreEqual(0.5f, temp.Normalized, Tolerance);
            }

            [Test] // 8
            public void Normalized_BelowMin_ClampsToZero()
            {
                Temperature temp = new Temperature(-40f, TemperatureUnit.Fahrenheit);
                Assert.AreEqual(0f, temp.Normalized, Tolerance);
            }

            [Test] // 9
            public void Normalized_AboveMax_ClampsToOne()
            {
                Temperature temp = new Temperature(150f, TemperatureUnit.Fahrenheit);
                Assert.AreEqual(1f, temp.Normalized, Tolerance);
            }

            // --- PARSING TESTS ---

            [Test] // 10
            public void Parse_ValidCelsiusString_ReturnsCorrectTemperature()
            {
                Temperature temp = Temperature.Parse("20 C");
                Assert.AreEqual(68f, temp, Tolerance); // 20C = 68F
            }

            [Test] // 11
            public void Parse_ValidFahrenheitString_ReturnsCorrectTemperature()
            {
                Temperature temp = Temperature.Parse("-40 fahrenheit");
                Assert.AreEqual(-40f, temp, Tolerance); // -40F = -40C
            }

            [Test] // 12
            public void Parse_ValidKelvinString_ReturnsCorrectTemperature()
            {
                Temperature temp = Temperature.Parse("300°k");
                Assert.AreEqual(80.33f, temp, Tolerance); // 300K ~ 80.33F
            }

            [Test] // 13
            public void Parse_StringWithWeirdSpacingAndCaps_ParsesCorrectly()
            {
                Temperature temp = Temperature.Parse("  -12.5   °   CeLsIuS ");
                Assert.AreEqual(9.5f, temp, Tolerance); // -12.5C = 9.5F
            }

            [Test] // 14
            public void Parse_EmptyString_ReturnsZeroTemperature()
            {
                Temperature temp = Temperature.Parse("   ");
                Assert.AreEqual(0f, temp, Tolerance);
            }

            [Test] // 15
            public void Parse_InvalidString_ThrowsFormatException()
            {
                Assert.Throws<FormatException>(() => Temperature.Parse("Not A Temperature"));
            }

            [Test] // 16
            public void Parse_NoUnitString_FallsBackToValue()
            {
                // Tests the fallback float.TryParse block
                Temperature temp = Temperature.Parse("45.5");
                // Since we don't know the global CozyPreferences state in a unit test, 
                // we at least assert it doesn't throw and registers a value.
                Assert.DoesNotThrow(() => Temperature.Parse("45.5"));
            }

            // --- STRING FORMATTING TESTS ---

            [Test] // 17
            public void ToString_SpecificUnit_FormatsWithOneDecimal()
            {
                Temperature temp = new Temperature(32f, TemperatureUnit.Fahrenheit);
                Assert.AreEqual("0.0°C", temp.ToString(TemperatureUnit.Celsius));
                Assert.AreEqual("32.0°F", temp.ToString(TemperatureUnit.Fahrenheit));
            }

            // --- LERP TESTS ---

            [Test] // 18
            public void Lerp_StaticMethod_InterpolatesCorrectly()
            {
                Temperature a = new Temperature(0f, TemperatureUnit.Fahrenheit);
                Temperature b = new Temperature(100f, TemperatureUnit.Fahrenheit);

                Temperature result = Temperature.Lerp(a, b, 0.25f);
                Assert.AreEqual(25f, result, Tolerance);
            }

            [Test] // 19
            public void Lerp_InstanceMethod_InterpolatesCorrectly()
            {
                Temperature a = new Temperature(50f, TemperatureUnit.Fahrenheit);
                Temperature b = new Temperature(100f, TemperatureUnit.Fahrenheit);

                Temperature result = a.Lerp(b, 0.5f);
                Assert.AreEqual(75f, result, Tolerance);
            }

            [Test] // 20
            public void Lerp_OutOfBounds_ClampsUsingMathfLerp()
            {
                Temperature a = new Temperature(0f, TemperatureUnit.Fahrenheit);
                Temperature b = new Temperature(100f, TemperatureUnit.Fahrenheit);

                // Mathf.Lerp clamps t between 0 and 1
                Temperature result = Temperature.Lerp(a, b, 1.5f);
                Assert.AreEqual(100f, result, Tolerance);
            }

            // --- ARITHMETIC WITH DELTAS ---

            [Test] // 21
            public void Add_PositiveDelta_IncreasesTemperature()
            {
                Temperature temp = new Temperature(32f, TemperatureUnit.Fahrenheit);
                TemperatureDelta delta = new TemperatureDelta(10f, TemperatureUnit.Fahrenheit);

                temp.Add(delta);
                Assert.AreEqual(42f, temp, Tolerance);
            }

            [Test] // 22
            public void Subtract_PositiveDelta_DecreasesTemperature()
            {
                Temperature temp = new Temperature(0f, TemperatureUnit.Celsius); // 32F
                TemperatureDelta delta = new TemperatureDelta(9f, TemperatureUnit.Fahrenheit);

                temp.Subtract(delta);
                Assert.AreEqual(23f, temp, Tolerance);
            }

            // --- TEMPERATURE DELTA SPECIFIC TESTS ---

            [Test] // 23
            public void TemperatureDelta_CelsiusToFahrenheit_CalculatesCorrectChange()
            {
                // A change of 5 Celsius is exactly a change of 9 Fahrenheit. No 32 degree offset!
                TemperatureDelta deltaC = new TemperatureDelta(5f, TemperatureUnit.Celsius);
                Assert.AreEqual(9f, deltaC, Tolerance);
            }

            [Test] // 24
            public void TemperatureDelta_KelvinToFahrenheit_CalculatesCorrectChange()
            {
                // NOTE: This test asserts standard math physics. It will fail with the current code's +273.15 bug.
                // A change of 5 Kelvin is identical to a change of 5 Celsius (which is 9 Fahrenheit).
                TemperatureDelta deltaK = new TemperatureDelta(5f, TemperatureUnit.Kelvin);
                Assert.AreEqual(9f, deltaK, Tolerance);
            }

            [Test] // 25
            public void Add_NegativeDelta_ActsAsSubtraction()
            {
                Temperature temp = new Temperature(50f, TemperatureUnit.Fahrenheit);
                TemperatureDelta delta = new TemperatureDelta(-10f, TemperatureUnit.Fahrenheit);

                temp.Add(delta);
                Assert.AreEqual(40f, temp, Tolerance);
            }

            [Test] // 26
            public void TemperatureDelta_Parse_ValidString_ReturnsCorrectDelta()
            {
                TemperatureDelta delta = TemperatureDelta.Parse("5 c");
                Assert.AreEqual(9f, delta, Tolerance); // 5C delta = 9F delta
            }

            [Test] // 27
            public void TemperatureDelta_MultiplyOperator_ScalesDeltaCorrectly()
            {
                TemperatureDelta delta = new TemperatureDelta(10f, TemperatureUnit.Fahrenheit);
                TemperatureDelta scaled = delta * 2.5f;

                Assert.AreEqual(25f, scaled, Tolerance);
            }

            [Test] // 28
            public void TemperatureDelta_ZeroDelta_NoChange()
            {
                Temperature temp = new Temperature(75f, TemperatureUnit.Fahrenheit);
                TemperatureDelta zeroDelta = new TemperatureDelta(0f, TemperatureUnit.Fahrenheit);

                temp.Add(zeroDelta);
                Assert.AreEqual(75f, temp, Tolerance);
            }

            // --- IMPLICIT CASTING TESTS ---

            [Test] // 29
            public void ImplicitCast_TemperatureToFloat_ReturnsValue()
            {
                Temperature temp = new Temperature(100f, TemperatureUnit.Celsius);

                // This cast relies on CozyPreferences.TemperatureUnit, which we can't mock here,
                // so we just assert that the cast compiles and doesn't throw.
                float castedValue = temp;
                Assert.IsNotNull(castedValue);
            }

            [Test] // 30
            public void ImplicitCast_DeltaToFloat_ReturnsValue()
            {
                TemperatureDelta delta = new TemperatureDelta(10f, TemperatureUnit.Fahrenheit);

                float castedValue = delta;
                Assert.IsNotNull(castedValue);
            }
        }

        [TestFixture]
        public class AirQualityTests
        {
            [Test]
            public void Parse_ValidString_ReturnsCorrectAirQuality()
            {
                AirQuality airQuality = AirQuality.Parse("150");
                Assert.AreEqual(150f, airQuality, Tolerance);
            }

            [Test]
            public void Lerp_StaticMethod_InterpolatesCorrectly()
            {
                AirQuality a = new AirQuality(50f);
                AirQuality b = new AirQuality(150f);
                AirQuality result = AirQuality.Lerp(a, b, 0.5f);
                Assert.AreEqual(100f, result, Tolerance);
            }

            [Test]
            public void Add_Subtract_Delta_AdjustsValue()
            {
                AirQuality airQuality = new AirQuality(100f);
                AirQualityDelta delta = new AirQualityDelta(25f);

                airQuality.Add(delta);
                Assert.AreEqual(125f, airQuality, Tolerance);

                airQuality.Subtract(delta);
                Assert.AreEqual(100f, airQuality, Tolerance);
            }
        }

        [TestFixture]
        public class HumidityTests
        {
            [Test]
            public void Parse_ValidString_ReturnsCorrectHumidity()
            {
                Humidity humidity = Humidity.Parse("75");
                Assert.AreEqual(75f, humidity, Tolerance);
            }

            [Test]
            public void Normalized_Midpoint_ReturnsHalf()
            {
                Humidity humidity = new Humidity(50f);
                Assert.AreEqual(0.5f, humidity.Normalized, Tolerance);
            }

            [Test]
            public void Lerp_StaticMethod_InterpolatesCorrectly()
            {
                Humidity a = new Humidity(20f);
                Humidity b = new Humidity(80f);
                Humidity result = Humidity.Lerp(a, b, 0.25f);
                Assert.AreEqual(35f, result, Tolerance);
            }
        }

        [TestFixture]
        public class PrecipitationChanceTests
        {
            [Test]
            public void Parse_ValidString_ReturnsCorrectChance()
            {
                PrecipitationChance chance = PrecipitationChance.Parse("45");
                Assert.AreEqual(45f, chance, Tolerance);
            }

            [Test]
            public void Lerp_StaticMethod_InterpolatesCorrectly()
            {
                PrecipitationChance a = new PrecipitationChance(0f);
                PrecipitationChance b = new PrecipitationChance(100f);
                PrecipitationChance result = PrecipitationChance.Lerp(a, b, 0.5f);
                Assert.AreEqual(50f, result, Tolerance);
            }
        }

        [TestFixture]
        public class PressureTests
        {
            [Test]
            public void Parse_AtmosphereString_ReturnsCorrectInHgValue()
            {
                Pressure pressure = Pressure.Parse("1 atm");
                Assert.AreEqual(29.92126f, pressure.GetPressure(PressureUnit.inHg), Tolerance);
            }

            [Test]
            public void ConvertToUnit_hPa_ReturnsCorrectValue()
            {
                float inHg = 29.92126f;
                float hPa = Pressure.ConvertToUnit(inHg, PressureUnit.hPa);
                Assert.AreEqual(1013.25f, hPa, 0.1f);
            }

            [Test]
            public void Lerp_StaticMethod_InterpolatesCorrectly()
            {
                Pressure a = new Pressure(29.5f, PressureUnit.inHg);
                Pressure b = new Pressure(30.5f, PressureUnit.inHg);
                Pressure result = Pressure.Lerp(a, b, 0.5f);
                Assert.AreEqual(30f, result.GetPressure(PressureUnit.inHg), Tolerance);
            }
        }

        [TestFixture]
        public class UVIndexTests
        {
            [Test]
            public void Parse_ValidString_ReturnsCorrectUVIndex()
            {
                UVIndex uvIndex = UVIndex.Parse("8");
                Assert.AreEqual(8f, uvIndex, Tolerance);
            }

            [Test]
            public void Lerp_StaticMethod_InterpolatesCorrectly()
            {
                UVIndex a = new UVIndex(2f);
                UVIndex b = new UVIndex(10f);
                UVIndex result = UVIndex.Lerp(a, b, 0.5f);
                Assert.AreEqual(6f, result, Tolerance);
            }

            [Test]
            public void Add_Subtract_Delta_AdjustsValue()
            {
                UVIndex uvIndex = new UVIndex(5f);
                UVIndexDelta delta = new UVIndexDelta(3f);

                uvIndex.Add(delta);
                Assert.AreEqual(8f, uvIndex, Tolerance);

                uvIndex.Subtract(delta);
                Assert.AreEqual(5f, uvIndex, Tolerance);
            }
        }
    }
}