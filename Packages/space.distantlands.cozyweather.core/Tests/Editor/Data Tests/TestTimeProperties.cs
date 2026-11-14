using System;
using System.Globalization;
using System.Reflection;
using DistantLands.Cozy;
using NUnit.Framework;
using UnityEngine;

namespace DistantLands.Cozy.Tests
{
    public class TestMeridiemProperties
    {
        private const float Tolerance = 0.0001f;

        public abstract class IsolatedPreferencesTests
        {
            private static readonly FieldInfo InstanceField = typeof(CozyPreferences).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly FieldInfo DaysPerYearField = typeof(CozyPreferences).GetField("daysPerYear", BindingFlags.Static | BindingFlags.NonPublic);
            private CozyPreferences previousPreferences;
            private CozyPreferences testPreferences;
            private int previousDaysPerYear;

            [SetUp]
            public void SetUpPreferences()
            {
                previousPreferences = (CozyPreferences)InstanceField.GetValue(null);
                previousDaysPerYear = (int)DaysPerYearField.GetValue(null);
                testPreferences = ScriptableObject.CreateInstance<CozyPreferences>();
                InstanceField.SetValue(null, testPreferences);
                DaysPerYearField.SetValue(null, 0);
            }

            [TearDown]
            public void RestorePreferences()
            {
                InstanceField.SetValue(null, previousPreferences);
                DaysPerYearField.SetValue(null, previousDaysPerYear);
                UnityEngine.Object.DestroyImmediate(testPreferences);
            }

            protected void SetPreference(string name, object value)
            {
                typeof(CozyPreferences).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(testPreferences, value);
            }
        }

        [TestFixture]
        public class MeridiemTimeTests : IsolatedPreferencesTests
        {
            [Test]
            public void Add_SimpleMinutes_CalculatesCorrectly()
            {
                // TEST 01 - Add 4:59 AM + 0:01 AM
                MeridiemTime result = new MeridiemTime(4, 59, Meridiem.AM) + new MeridiemTimeDelta(0, 1);
                Assert.AreEqual("5:00 AM", result.ToString());
            }

            [Test]
            public void Add_MinuteRollover_BalancesCorrectly()
            {
                // TEST 04 - Balance edge case (59 mins + 2 mins)
                MeridiemTime result = new MeridiemTime(5, 59, Meridiem.AM) + new MeridiemTimeDelta(0, 2);
                Assert.AreEqual("6:01 AM", result.ToString());

                // TEST 18 - Add minutes rollover (midnight implied)
                MeridiemTime result2 = MeridiemTime.From24Hour(0, 59) + new MeridiemTimeDelta(0, 2);
                Assert.AreEqual("1:01 AM", result2.ToString());
            }

            [Test]
            public void Add_WrapToNextMeridiem_FlipsAMtoPM()
            {
                // TEST 05 & 31 - Wrap to next meridiem (11:59 PM + 1 min)
                MeridiemTime resultPM = new MeridiemTime(11, 59, Meridiem.PM) + new MeridiemTimeDelta(0, 1);
                Assert.AreEqual("12:00 AM", resultPM.ToString());

                // TEST 32 - Wrap to next meridiem (11:59 AM + 1 min)
                MeridiemTime resultAM = new MeridiemTime(11, 59, Meridiem.AM) + new MeridiemTimeDelta(0, 1);
                Assert.AreEqual("12:00 PM", resultAM.ToString());
            }

            [Test]
            public void Add_HoursRollover_UpdatesMeridiem()
            {
                // TEST 19 - Add hours rollover
                MeridiemTime result = new MeridiemTime(11, 0, Meridiem.AM) + new MeridiemTimeDelta(2, 0);
                Assert.AreEqual("1:00 PM", result.ToString());

                // TEST 22 - Add across both AM/PM
                MeridiemTime result2 = new MeridiemTime(11, 59, Meridiem.AM) + new MeridiemTimeDelta(1, 2);
                Assert.AreEqual("1:01 PM", result2.ToString());
            }

            [Test]
            public void Add_MidnightRoll_BalancesCorrectly()
            {
                // TEST 20 - Midnight roll balance check
                MeridiemTime result = MeridiemTime.From24Hour(23, 59) + new MeridiemTimeDelta(0, 1);
                Assert.AreEqual("12:00 AM", result.ToString());
            }

            [Test]
            public void Add_MillisecondsAndSeconds_RollsOverCorrectly()
            {
                // TEST 16 - Add milliseconds rollover
                MeridiemTime msResult = MeridiemTime.From24Hour(0, 0, 0, 999) + new MeridiemTimeDelta(0, 0, 0, 5);
                Assert.AreEqual("12:00:01:004 AM", msResult.FullString()); // Assumes FullString format

                // TEST 17 - Add seconds rollover
                MeridiemTime secResult = MeridiemTime.From24Hour(0, 0, 59, 500) + new MeridiemTimeDelta(0, 0, 1, 600);
                Assert.AreEqual("12:01:01:100 AM", secResult.FullString());
            }

            [Test]
            public void Add_EqualAMandPMMerge_CalculatesCorrectly()
            {
                // TEST 29 - Equal AM/PM merge check (6 hours + 18 hours = 24 hours)
                MeridiemTime result = new MeridiemTime(6, 0, Meridiem.AM) + new MeridiemTimeDelta(6, 0);
                Assert.AreEqual("12:00 PM", result.ToString());
            }

            // --- SUBTRACTION TESTS ---

            [Test]
            public void Subtract_AcrossMeridiem_YieldsCorrectDifference()
            {
                // TEST 14 - Subtract times across meridiem (13:00 - 11:00 = 2:00)
                MeridiemTimeDelta result = new MeridiemTime(1, 0, Meridiem.PM) - new MeridiemTime(11, 0, Meridiem.AM);
                Assert.AreEqual("02:00:00:000", result.ToString());
            }

            [Test]
            public void Subtract_NegativeTime_ReturnsNegativeDelta()
            {
                // TEST 15 - Negative time subtraction returns a negative duration
                MeridiemTimeDelta result = new MeridiemTime(3, 0, Meridiem.AM) - new MeridiemTime(4, 0, Meridiem.AM);
                Assert.AreEqual(new MeridiemTimeDelta(-1, 0), result);
            }

            [Test]
            public void Subtract_MidnightBoundary_WrapsCorrectly()
            {
                // TEST 33
                MeridiemTime midnight = new MeridiemTime(12, 0, Meridiem.AM) - new MeridiemTimeDelta(0, 1);
                Assert.AreEqual("11:59 PM", midnight.ToString());

                // TEST 34
                MeridiemTime noon = new MeridiemTime(12, 0, Meridiem.PM) - new MeridiemTimeDelta(0, 1);
                Assert.AreEqual("11:59 AM", noon.ToString());
            }

            // --- IMPLICIT CONVERSION TESTS ---

            [Test]
            public void ImplicitConversion_FloatToTime_MapsCorrectly()
            {
                // TEST 06
                Assert.AreEqual("12:00 AM", ((MeridiemTime)0.0f).ToString());

                // TEST 07
                Assert.AreEqual("12:00 PM", ((MeridiemTime)0.5f).ToString());

                // TEST 08
                Assert.AreEqual("6:00 PM", ((MeridiemTime)0.75f).ToString());
            }

            [Test]
            public void ImplicitConversion_DoubleToTime_MapsCorrectly()
            {
                // TEST 09
                Assert.AreEqual("6:00 AM", ((MeridiemTime)0.25d).ToString());
            }

            [Test]
            public void ImplicitConversion_TimeToFloat_MapsCorrectly()
            {
                // TEST 10
                float timeFloat = (float)new MeridiemTime(6, 0, Meridiem.AM);
                Assert.AreEqual(0.25f, timeFloat, Tolerance);
            }

            [TestCase("en-US")]
            [TestCase("fi-FI")]
            public void ImplicitConversion_TimeToDateTime_MapsCorrectly(string cultureName)
            {
                var previousCulture = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    DateTime dt = (DateTime)new MeridiemTime(9, 30, Meridiem.AM);
                    Assert.AreEqual(new DateTime(1, 1, 1, 9, 30, 0), dt);
                }
                finally
                {
                    CultureInfo.CurrentCulture = previousCulture;
                }
            }

            // --- STRING FORMATTING TESTS ---

            [Test]
            public void Formatting_Noon_IsCorrect()
            {
                // TEST 21
                Assert.AreEqual("12:00 PM", new MeridiemTime(12, 0, Meridiem.PM).ToString());
            }

            [Test]
            public void Formatting_FullString_IncludesAllUnits()
            {
                // TEST 12
                Assert.AreEqual("3:04:05:067 AM", new MeridiemTime(3, 4, 5, 67, Meridiem.AM).FullString());
            }

            [Test]
            public void Formatting_FullMilitaryTime_FormatsCorrectly()
            {
                // TEST 13
                Assert.AreEqual("15:04:05:067", new MeridiemTime(3, 4, 5, 67, Meridiem.PM).FullMilitaryTime());
            }

            [TestCase(MeridiemTime.Unit.Standard, "7:45 PM")]
            [TestCase(MeridiemTime.Unit.Military, "19:45")]
            public void Formatting_ToStringOverride_UsesTimePreference(MeridiemTime.Unit unit, string expected)
            {
                SetPreference("timeUnit", unit);
                Assert.AreEqual(expected, new MeridiemTime(7, 45, Meridiem.PM).ToString());
            }

            [Test]
            public void Formatting_HourlyLoops_SpotCheck()
            {
                // Covers TEST 02 and TEST 03 logic without a massive loop
                Assert.AreEqual("12:00 AM", new MeridiemTime(12, 0, Meridiem.AM).ToString());
                Assert.AreEqual("00:00", new MeridiemTime(12, 0, Meridiem.AM).MilitaryTime());

                Assert.AreEqual("12:00 PM", new MeridiemTime(12, 0, Meridiem.PM).ToString());
                Assert.AreEqual("12:00", new MeridiemTime(12, 0, Meridiem.PM).MilitaryTime());

                Assert.AreEqual("1:00 PM", MeridiemTime.From24Hour(13, 0).ToString());
                Assert.AreEqual("13:00", MeridiemTime.From24Hour(13, 0).MilitaryTime());

                Assert.AreEqual("12:00 AM", MeridiemTime.From24Hour(24, 0).ToString());
                Assert.AreEqual("00:00", MeridiemTime.From24Hour(24, 0).MilitaryTime()); // Assuming 24:00 wraps to 00:00
            }

            // --- EDGE CASES & LARGE/NEGATIVE INPUTS ---

            [Test]
            public void Input_HiddenSecondsOrMilliseconds_ToStringTruncates()
            {
                // TEST 23 - Time with seconds only
                Assert.AreEqual("12:00 AM", MeridiemTime.From24Hour(0, 0, 59, 0).ToString());

                // TEST 24 - Time with milliseconds only
                Assert.AreEqual("12:00 AM", MeridiemTime.From24Hour(0, 0, 0, 999).ToString());
            }

            [Test]
            public void Input_GreaterThan12Hours_ParsesAsMilitary()
            {
                // TEST 25
                Assert.AreEqual("1:00 PM", MeridiemTime.From24Hour(13, 0).ToString());
                // TEST 26
                Assert.AreEqual("12:00 AM", MeridiemTime.From24Hour(24, 0).ToString());
            }

            [Test]
            public void Input_ExcessivelyLargeValues_RollsOverSafely()
            {
                // TEST 28 - Large values test: 100h, 1000m, 1000s, 10000ms
                // 10000ms = +10s. 1010s = +16m 50s. 1016m 50s = +16h 56m 50s.
                // Total = 116h 56m 50s. 116 % 24 = 20h. 20:56 = 8:56 PM.
                MeridiemTime largeTime = MeridiemTime.From24Hour(100, 1000, 1000, 10000);
                Assert.AreEqual("8:56 PM", largeTime.ToString());
            }
        }

        [TestFixture]
        public class MeridiemDateTests : IsolatedPreferencesTests
        {
            [TestCase(CozyPreferences.DateFormat.MDY, "7/5/2026")]
            [TestCase(CozyPreferences.DateFormat.DMY, "5/7/2026")]
            [TestCase(CozyPreferences.DateFormat.YMD, "2026/7/5")]
            public void ToStringFunction(CozyPreferences.DateFormat format, string expected)
            {
                SetPreference("dateFormat", format);
                MeridiemDate date = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual(expected, date.ToString());
            }

            [Test]
            public void AddDays_AdjustsDateCorrectly()
            {
                MeridiemDate date1 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("7/7/2026", (date1 + 2).ToString());

                MeridiemDate date2 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("7/5/2026", (date2 + 0).ToString());

                MeridiemDate date3 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("8/6/2026", (date3 + 32).ToString());

                MeridiemDate date4 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("7/5/2027", (date4 + 365).ToString());
            }

            [Test]
            public void SubtractDays_AdjustsDateCorrectly()
            {
                MeridiemDate date1 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("7/3/2026", (date1 - 2).ToString());

                MeridiemDate date2 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("7/5/2026", (date2 - 0).ToString());

                MeridiemDate date3 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("6/30/2026", (date3 - 5).ToString());

                MeridiemDate date4 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("7/5/2025", (date4 - 365).ToString());
            }

            [Test]
            public void SubtractDaysAcrossMonthBoundary_CalculatesCorrectly()
            {
                MeridiemDate date1 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("6/1/2026", (date1 - 34).ToString());
            }

            [Test]
            public void SubtractDaysAcrossYears_CalculatesCorrectly()
            {
                MeridiemDate date1 = new MeridiemDate(7, 5, 2026);
                Assert.AreEqual("6/1/2020", (date1 - 2224).ToString());
            }
            [Test]
            public void DateComparison_LessThan()
            {
                MeridiemDate date1 = new MeridiemDate(7, 5, 2026);
                MeridiemDate date2 = new MeridiemDate(1, 2, 2027);
                var test = date1 < date2;
                Assert.AreEqual(true, test);
            }
            [Test]
            public void DateComparison_MoreThan()
            {
                MeridiemDate date1 = new MeridiemDate(7, 5, 2026);
                MeridiemDate date2 = new MeridiemDate(1, 2, 2027);
                var test = date1 > date2;
                Assert.AreEqual(false, test);
            }
            [Test]
            public void DateComparison_Equal()
            {
                MeridiemDate date1 = new MeridiemDate(7, 5, 2026);
                MeridiemDate date2 = new MeridiemDate(1, 2, 2027);
                var test = date1 == date2;
                Assert.AreEqual(false, test);

                MeridiemDate date3 = new MeridiemDate(7, 5, 2026);
                MeridiemDate date4 = new MeridiemDate(7, 5, 2026);
                var test2 = date3 == date4;
                Assert.AreEqual(true, test2);
            }

            #region Constructors & Basic Properties

            [Test]
            public void Constructor_SetsYearMonthDayCorrectly()
            {
                var date = new MeridiemDate(5, 10, 2024);
                Assert.AreEqual(2024, date.year);
                Assert.AreEqual(5, date.month);
                Assert.AreEqual(10, date.day);
            }

            [Test]
            public void Yearless_ReturnsDateWithYearZero()
            {
                var date = new MeridiemDate(5, 10, 2024).Yearless;
                Assert.AreEqual(0, date.year);
                Assert.AreEqual(5, date.month);
                Assert.AreEqual(10, date.day);
            }

            [Test]
            public void AbsoluteDay_CalculatesCorrectlyForFirstDay()
            {
                var date = new MeridiemDate(1, 1, 0);
                Assert.AreEqual(0, date.AbsoluteDay);
            }

            [Test]
            public void AbsoluteDay_CalculatesCorrectlyAcrossYears()
            {
                var date = new MeridiemDate(1, 1, 1); // Start of year 1
                Assert.AreEqual(365, date.AbsoluteDay);
            }

            [Test]
            public void AbsoluteDayInYear_IgnoresYearValue()
            {
                var date1 = new MeridiemDate(2, 1, 0);
                var date2 = new MeridiemDate(2, 1, 100);
                Assert.AreEqual(31, date1.AbsoluteDayInYear);
                Assert.AreEqual(31, date2.AbsoluteDayInYear);
            }

            #endregion

            #region Comparisons

            [Test]
            public void Operator_Equality_ReturnsTrueForSameDates()
            {
                var d1 = new MeridiemDate(1, 1, 2024);
                var d2 = new MeridiemDate(1, 1, 2024);
                Assert.IsTrue(d1 == d2);
            }

            [Test]
            public void Operator_Greater_ComparesYears()
            {
                var d1 = new MeridiemDate(1, 1, 2025);
                var d2 = new MeridiemDate(1, 1, 2024);
                Assert.IsTrue(d1 > d2);
            }

            [Test]
            public void Operator_Greater_ComparesMonthsIfYearsEqual()
            {
                var d1 = new MeridiemDate(2, 1, 2024);
                var d2 = new MeridiemDate(1, 1, 2024);
                Assert.IsTrue(d1 > d2);
            }

            [Test]
            public void Operator_Less_ComparesDaysIfMonthYearEqual()
            {
                var d1 = new MeridiemDate(1, 1, 2024);
                var d2 = new MeridiemDate(1, 2, 2024);
                Assert.IsTrue(d1 < d2);
            }

            [Test]
            public void Equals_ReturnsFalseForNullOrDifferentType()
            {
                var date = new MeridiemDate(1, 1, 2024);
                Assert.IsFalse(date.Equals(null));
                Assert.IsFalse(date.Equals("not a date"));
            }

            #endregion

            #region Normalization (ToBalanced)

            [Test]
            public void ToBalanced_HandlesDayOverflow()
            {
                var date = new MeridiemDate(1, 35, 2024); // Jan 35th
                var balanced = date.ToBalanced();
                Assert.AreEqual(2, balanced.month);
                Assert.AreEqual(4, balanced.day);
            }

            [Test]
            public void ToBalanced_HandlesMonthOverflow()
            {
                var date = new MeridiemDate(13, 1, 2024);
                var balanced = date.ToBalanced();
                Assert.AreEqual(1, balanced.month);
                Assert.AreEqual(2025, balanced.year);
            }

            [Test]
            public void ToBalanced_HandlesNegativeDays()
            {
                var date = new MeridiemDate(2, 0, 2024);
                var balanced = date.ToBalanced();
                Assert.AreEqual(1, balanced.month);
                Assert.AreEqual(31, balanced.day);
            }

            [Test]
            public void ToBalanced_HandlesNegativeMonths()
            {
                var date = new MeridiemDate(0, 1, 2024);
                var balanced = date.ToBalanced();
                Assert.AreEqual(12, balanced.month);
                Assert.AreEqual(2023, balanced.year);
            }

            [Test]
            public void ToBalanced_ComplexMultiStepNormalization()
            {
                // Adding enough days to skip multiple months and years
                var date = new MeridiemDate(1, 400, 2024);
                var balanced = date.ToBalanced();
                Assert.IsTrue(balanced.year > 2024);
                Assert.AreEqual(balanced.AbsoluteDay, date.AbsoluteDay);
            }

            #endregion

            #region Arithmetic

            [Test]
            public void Operator_PlusDelta_AdjustsDate()
            {
                var date = new MeridiemDate(1, 1, 2024);
                var result = date + 5;
                Assert.AreEqual(6, result.day);
            }

            [Test]
            public void Operator_MinusInt_AdjustsDate()
            {
                var date = new MeridiemDate(1, 10, 2024);
                var result = date - 10;
                Assert.AreEqual(12, result.month);
                Assert.AreEqual(2023, result.year);
                Assert.AreEqual(31, result.day);
            }

            [Test]
            public void Operator_PlusInt_AddsDays()
            {
                var date = new MeridiemDate(1, 1, 2024);
                var result = date + 10;
                Assert.AreEqual(11, result.day);
            }

            [Test]
            public void Operator_MinusInt_SubtractsDays()
            {
                var date = new MeridiemDate(1, 10, 2024);
                var result = date - 5;
                Assert.AreEqual(5, result.day);
            }

            [Test]
            public void ImplicitConversion_ToInt_ReturnsAbsoluteDay()
            {
                var date = new MeridiemDate(1, 10, 0);
                int absDay = date;
                Assert.AreEqual(9, absDay);
            }

            #endregion

            #region Absolute Day Constructor

            [Test]
            public void Constructor_Int_HandlesZero()
            {
                var date = new MeridiemDate(0);
                Assert.AreEqual(0, date.year);
                Assert.AreEqual(1, date.month);
                Assert.AreEqual(1, date.day);
            }

            [Test]
            public void Constructor_Int_HandlesNegativeAbsoluteDays()
            {
                var date = new MeridiemDate(-1);
                Assert.AreEqual(-1, date.year);
                Assert.AreEqual(12, date.month);
                Assert.AreEqual(31, date.day);
            }

            [Test]
            public void Constructor_Int_HandlesLargeValues()
            {
                var date = new MeridiemDate(400); // 1 year (360) + 40 days
                Assert.AreEqual(1, date.year);
                Assert.AreEqual(2, date.month);
                Assert.AreEqual(5, date.day);
            }

            #endregion

            #region Helper Methods & Metadata

            [Test]
            public void MonthName_ReturnsCorrectStringFromPreferences()
            {
                var date = new MeridiemDate(3, 1, 0);
                Assert.AreEqual("March", date.MonthName);
            }

            [Test]
            public void DaysInMonth_ReturnsCorrectValue()
            {
                var date = new MeridiemDate(1, 1, 0);
                Assert.AreEqual(31, date.DaysInMonth);
            }

            [Test]
            public void YearPercentage_CalculatesCorrectFraction()
            {
                var date = new MeridiemDate(7, 1, 0);
                Assert.AreEqual(0.5f, date.YearPercentage, 0.01f);
            }

            [Test]
            public void Weekday_CalculatesCorrectEnum()
            {
                var date = new MeridiemDate(0); // Absolute Day 0
                Assert.AreEqual(CozyPreferences.StartingWeekday, date.Weekday);
            }

            [TestCase(DayOfWeek.Sunday)]
            [TestCase(DayOfWeek.Monday)]
            [TestCase(DayOfWeek.Tuesday)]
            [TestCase(DayOfWeek.Wednesday)]
            [TestCase(DayOfWeek.Thursday)]
            [TestCase(DayOfWeek.Friday)]
            [TestCase(DayOfWeek.Saturday)]
            public void IsWeekend_IdentifiesSaturdayAndSunday(DayOfWeek startingWeekday)
            {
                SetPreference("startingWeekday", startingWeekday);
                var sat = new MeridiemDate(((int)DayOfWeek.Saturday - (int)startingWeekday + 7) % 7);
                var sun = new MeridiemDate(((int)DayOfWeek.Sunday - (int)startingWeekday + 7) % 7);
                var mon = new MeridiemDate(((int)DayOfWeek.Monday - (int)startingWeekday + 7) % 7);

                Assert.AreEqual(DayOfWeek.Saturday, sat.Weekday);
                Assert.AreEqual(DayOfWeek.Sunday, sun.Weekday);
                Assert.AreEqual(DayOfWeek.Monday, mon.Weekday);
                Assert.IsTrue(sat.IsWeekend);
                Assert.IsTrue(sun.IsWeekend);
                Assert.IsFalse(mon.IsWeekend);
            }

            #endregion

        }
    }
}