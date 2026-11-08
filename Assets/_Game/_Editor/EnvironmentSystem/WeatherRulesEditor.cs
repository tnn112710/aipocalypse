using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Editor.RandomSystem;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Editor.EnvironmentSystem
{
    // WeatherRules as three grids (how often per period, how long, wind), each weight with its share, plus a seeded test
    // that runs the real WeatherPlanner over many days and shows the share of time each weather gets.
    [CustomEditor(typeof(WeatherRules))]
    public sealed class WeatherRulesEditor : UnityEditor.Editor
    {
        const float NameWidth = 104f;
        const float BarHeight = 3f;
        const float Gap = 3f;

        static readonly SkyWeather[] Skies = (SkyWeather[])Enum.GetValues(typeof(SkyWeather));
        static readonly WeatherPeriod[] Periods = (WeatherPeriod[])Enum.GetValues(typeof(WeatherPeriod));
        static readonly WindLevel[] Winds = (WindLevel[])Enum.GetValues(typeof(WindLevel));
        // Filled in Styles(): NicifyVariableName may not run while Unity creates the editor.
        static string[] SkyNames;
        static string[] PeriodNames;
        static string[] WindNames;
        static readonly string[] Settings =
        {
            nameof(WeatherRules.Enabled), nameof(WeatherRules.NightStart), nameof(WeatherRules.NightEnd), nameof(WeatherRules.DurationJitter), nameof(WeatherRules.ShortestHours), nameof(WeatherRules.PlanHours),
        };

        static GUIStyle _section;
        static GUIStyle _note;
        static GUIStyle _share;
        static GUIStyle _header;

        int _days = 1000;
        int _seed = 1;
        int _bloodMoonEvery = 8;
        int _eclipseEvery = 16;
        Result _result;

        sealed class Result
        {
            public readonly float[,] Shares = new float[Periods.Length, Skies.Length];
            public readonly float[] Rain = new float[Periods.Length];
            public readonly float[] Windy = new float[Periods.Length];
            public readonly float[] Forbidden = new float[Periods.Length];
            public float ChangesPerDay;
            public float RainSpellsPerDay;
            public int Days;
        }

        static float Line => EditorGUIUtility.singleLineHeight;

        public override void OnInspectorGUI()
        {
            Styles();
            var rules = (WeatherRules)target;
            serializedObject.Update();
            foreach (string field in Settings)
                EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
            StepHours(serializedObject.FindProperty(nameof(WeatherRules.StepHours)));
            serializedObject.ApplyModifiedProperties();

            if (!HasEveryRow(rules))
            {
                EditorGUILayout.HelpBox("Some sky weathers have no rule (never picked, no wind or duration).", MessageType.Warning);
                if (GUILayout.Button("Add the missing rows"))
                    AddMissingRows(rules);
                return;
            }
            FixDurationRows(rules);

            DrawOften(rules);
            DrawLong(rules);
            DrawWind(rules);
            DrawTest(rules);
        }

        // Vector2 as "min – max" game hours.
        static void StepHours(SerializedProperty property)
        {
            Rect line = EditorGUILayout.GetControlRect();
            Rect field = EditorGUI.PrefixLabel(line, new GUIContent("Step Hours", property.tooltip));
            Vector2 value = property.vector2Value;
            float half = (field.width - 16f) * 0.5f;
            EditorGUI.BeginChangeCheck();
            value.x = EditorGUI.FloatField(new Rect(field.x, field.y, half, field.height), value.x);
            GUI.Label(new Rect(field.x + half, field.y, 16f, field.height), "–", EditorStyles.centeredGreyMiniLabel);
            value.y = EditorGUI.FloatField(new Rect(field.xMax - half, field.y, half, field.height), value.y);
            if (EditorGUI.EndChangeCheck())
                property.vector2Value = new Vector2(Mathf.Max(0.05f, value.x), Mathf.Max(0.05f, value.y));
        }

        void DrawOften(WeatherRules rules)
        {
            Section("How often", "Weight per period: chance = weight / the column's total. 0 = never in that period, not even "
                + "on the way to another weather. Night runs from Night Start to Night End; a Blood Moon is a night, an eclipse a daytime.");
            HeaderRow(PeriodNames);
            var totals = new float[Periods.Length];
            for (int p = 0; p < Periods.Length; p++)
                foreach (SkyWeather sky in Skies)
                    totals[p] += rules.Weight(Periods[p], sky);

            for (int s = 0; s < Skies.Length; s++)
            {
                WeatherRules.SkyRule rule = rules.Rule(Skies[s]);
                Rect[] cells = Row(s, SkyNames[s], Periods.Length);
                for (int p = 0; p < Periods.Length; p++)
                {
                    float weight = rule.Weight(Periods[p]);
                    float edited = Cell(cells[p], weight, totals[p] > 0f ? Mathf.Max(0f, weight) / totals[p] : 0f, s);
                    if (!Mathf.Approximately(edited, weight))
                        Edit(rules, () => rule.SetWeight(Periods[p], Mathf.Max(0f, edited)));
                }
            }

            EditorGUILayout.Space(4f);
            for (int p = 0; p < Periods.Length; p++)
            {
                Rect line = EditorGUILayout.GetControlRect(false, WeightStripe.Height);
                GUI.Label(new Rect(line.x, line.y, NameWidth, line.height), PeriodNames[p], EditorStyles.miniBoldLabel);
                var weights = new float[Skies.Length];
                for (int s = 0; s < Skies.Length; s++)
                    weights[s] = rules.Weight(Periods[p], Skies[s]);
                WeightStripe.Draw(new Rect(line.x + NameWidth, line.y, line.width - NameWidth, line.height), SkyNames, weights, 0f);
            }
        }

        void DrawLong(WeatherRules rules)
        {
            Section("How long", "Weight per duration, in game hours (± Duration Jitter). The in-between steps on the way to a far "
                + "weather don't count. One game hour lasts the clock settings' Seconds Per Game Hour.");
            int count = rules.DurationHours.Count;

            Rect header = EditorGUILayout.GetControlRect(false, Line);
            Rect[] heads = Split(header, count + 1);
            GUI.Label(new Rect(header.x, header.y, NameWidth, Line), "Hours", _header);
            for (int d = 0; d < count; d++)
            {
                float hours = rules.DurationHours[d];
                float edited = EditorGUI.DelayedFloatField(heads[d], hours);
                if (!Mathf.Approximately(edited, hours))
                    Edit(rules, () => rules.DurationHours[d] = Mathf.Max(0.25f, edited));
            }
            GUI.Label(heads[count], new GUIContent("Average", "Average rolled duration, in game hours"), _header);

            for (int s = 0; s < Skies.Length; s++)
            {
                WeatherRules.SkyRule rule = rules.Rule(Skies[s]);
                Rect[] cells = Row(s, SkyNames[s], count + 1);
                float total = 0f, sum = 0f;
                for (int d = 0; d < count; d++)
                {
                    total += rule.DurationWeight(d);
                    sum += rule.DurationWeight(d) * rules.DurationHours[d];
                }
                for (int d = 0; d < count; d++)
                {
                    float weight = rule.Durations[d];
                    float edited = Cell(cells[d], weight, total > 0f ? rule.DurationWeight(d) / total : 0f, s);
                    if (!Mathf.Approximately(edited, weight))
                        Edit(rules, () => rule.Durations[d] = Mathf.Max(0f, edited));
                }
                GUI.Label(cells[count], total > 0f ? $"{sum / total:0.0} h" : "none", _share);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(NameWidth);
                if (GUILayout.Button(new GUIContent("Add duration", "A new column, weight 0 for every weather"), EditorStyles.miniButtonLeft))
                    Edit(rules, () =>
                    {
                        float last = count > 0 ? rules.DurationHours[count - 1] : 0f;
                        rules.DurationHours.Add(last + 2f);
                        foreach (WeatherRules.SkyRule rule in rules.Skies.Values)
                            rule.Durations.Add(0f);
                    });
                using (new EditorGUI.DisabledScope(count <= 1))
                    if (GUILayout.Button(new GUIContent("Remove last", "Drops the last column and its weights"), EditorStyles.miniButtonRight))
                        Edit(rules, () =>
                        {
                            rules.DurationHours.RemoveAt(count - 1);
                            foreach (WeatherRules.SkyRule rule in rules.Skies.Values)
                                rule.Durations.RemoveAt(count - 1);
                        });
            }
        }

        void DrawWind(WeatherRules rules)
        {
            Section("Wind", "Weight per wind level, rolled when a weather starts (a forced one keeps its own). The wind moves one "
                + "level per step (Step Hours), so calm to gale passes through breezy and windy.");
            HeaderRow(WindNames);
            for (int s = 0; s < Skies.Length; s++)
            {
                WeatherRules.SkyRule rule = rules.Rule(Skies[s]);
                Rect[] cells = Row(s, SkyNames[s], Winds.Length);
                float total = 0f;
                foreach (WindLevel wind in Winds)
                    total += Mathf.Max(0f, rule.WindWeight(wind));
                for (int w = 0; w < Winds.Length; w++)
                {
                    float weight = rule.WindWeight(Winds[w]);
                    float edited = Cell(cells[w], weight, total > 0f ? Mathf.Max(0f, weight) / total : 0f, s);
                    if (!Mathf.Approximately(edited, weight))
                        Edit(rules, () => rule.SetWindWeight(Winds[w], Mathf.Max(0f, edited)));
                }
            }
        }

        void DrawTest(WeatherRules rules)
        {
            Section("Test", "Runs the planner (the game's own code) over many days with a seed: the share of time each weather "
                + "gets, which differs from the weights because of the in-between steps and the durations.");
            using (new EditorGUILayout.HorizontalScope())
            {
                float label = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 36f;
                _days = Mathf.Clamp(EditorGUILayout.IntField("Days", _days), 1, 20000);
                _seed = EditorGUILayout.IntField("Seed", _seed);
                EditorGUIUtility.labelWidth = label;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                float label = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 120f;
                _bloodMoonEvery = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Blood Moon every", "Every Nth night is a Blood Moon in the test; 0 = none"), _bloodMoonEvery));
                _eclipseEvery = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Eclipse every", "Every Nth day is an eclipse in the test; 0 = none"), _eclipseEvery));
                EditorGUIUtility.labelWidth = label;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Test", EditorStyles.miniButtonLeft))
                    _result = Simulate(rules, _days, _seed, _bloodMoonEvery, _eclipseEvery);
                if (GUILayout.Button("Clear", EditorStyles.miniButtonRight))
                    _result = null;
            }
            if (_result == null)
                return;

            HeaderRow(PeriodNames);
            for (int s = 0; s < Skies.Length; s++)
            {
                Rect[] cells = Row(s, SkyNames[s], Periods.Length);
                for (int p = 0; p < Periods.Length; p++)
                    ShareCell(cells[p], _result.Shares[p, s], s);
            }
            ResultRow("Rain", _result.Rain, Color.white);
            ResultRow("Windy or gale", _result.Windy, Color.white);
            ResultRow("Not allowed", _result.Forbidden, new Color(1f, 0.45f, 0.4f));
            EditorGUILayout.LabelField($"{_result.Days} days: {_result.ChangesPerDay:0.0} sky changes a day (steps included), "
                + $"a rain spell every {(_result.RainSpellsPerDay > 0f ? 1f / _result.RainSpellsPerDay : 0f):0.0} days", _note);
        }

        static Result Simulate(WeatherRules rules, int days, int seed, int bloodMoonEvery, int eclipseEvery)
        {
            var planner = new WeatherPlanner(rules, new RandomStream((ulong)seed));
            for (int day = 1; day <= days; day++)
            {
                if (bloodMoonEvery > 0 && day % bloodMoonEvery == 0)
                    planner.MarkBloodMoon(day);
                if (eclipseEvery > 0 && (day + eclipseEvery / 2) % eclipseEvery == 0)
                    planner.MarkEclipse(day);
            }
            planner.Restart(0d, SkyWeather.PartlyCloudy, WindLevel.Breezy);
            planner.Extend(days * 24d);

            var result = new Result { Days = days };
            var hours = new float[Periods.Length];
            int changes = 0, rainSpells = 0;
            SkyWeather previous = SkyWeather.PartlyCloudy;
            foreach (WeatherSpell spell in planner.Plan)
            {
                if (spell.Start >= days * 24d)
                    break;
                if (spell.Sky != previous)
                {
                    changes++;
                    if (IsRain(spell.Sky) && !IsRain(previous))
                        rainSpells++;
                    previous = spell.Sky;
                }
                for (double t = spell.Start; t < spell.End - 1e-6; t += 0.25d)
                {
                    float slice = (float)Math.Min(0.25d, spell.End - t);
                    double middle = t + slice * 0.5d;
                    int p = (int)planner.PeriodAt(middle);
                    hours[p] += slice;
                    result.Shares[p, (int)spell.Sky] += slice;
                    if (IsRain(spell.Sky))
                        result.Rain[p] += slice;
                    if (spell.Wind >= WindLevel.Windy)
                        result.Windy[p] += slice;
                    if (!planner.Allows(spell.Sky, middle))
                        result.Forbidden[p] += slice;
                }
            }
            for (int p = 0; p < Periods.Length; p++)
            {
                if (hours[p] <= 0f)
                    continue;
                for (int s = 0; s < Skies.Length; s++)
                    result.Shares[p, s] /= hours[p];
                result.Rain[p] /= hours[p];
                result.Windy[p] /= hours[p];
                result.Forbidden[p] /= hours[p];
            }
            result.ChangesPerDay = changes / (float)days;
            result.RainSpellsPerDay = rainSpells / (float)days;
            return result;
        }

        static bool IsRain(SkyWeather sky) => sky is SkyWeather.LightRain or SkyWeather.MediumRain or SkyWeather.HeavyRain;

        static bool HasEveryRow(WeatherRules rules)
        {
            foreach (SkyWeather sky in Skies)
                if (rules.Rule(sky) == null)
                    return false;
            return true;
        }

        static void AddMissingRows(WeatherRules rules) => Edit(rules, () =>
        {
            foreach (SkyWeather sky in Skies)
                if (rules.Rule(sky) == null)
                    rules.Skies[sky] = new WeatherRules.SkyRule { Breezy = 1f };
        });

        // Every rule's Durations has one weight per DurationHours entry.
        static void FixDurationRows(WeatherRules rules)
        {
            int count = rules.DurationHours.Count;
            bool fits = true;
            foreach (WeatherRules.SkyRule rule in rules.Skies.Values)
                fits &= rule.Durations.Count == count;
            if (fits)
                return;
            Edit(rules, () =>
            {
                foreach (WeatherRules.SkyRule rule in rules.Skies.Values)
                {
                    while (rule.Durations.Count < count)
                        rule.Durations.Add(0f);
                    if (rule.Durations.Count > count)
                        rule.Durations.RemoveRange(count, rule.Durations.Count - count);
                }
            });
        }

        static void Edit(WeatherRules rules, Action change)
        {
            Undo.RecordObject(rules, "Edit weather rules");
            change();
            EditorUtility.SetDirty(rules);
        }

        static void Section(string title, string note)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(title, _section);
            EditorGUILayout.LabelField(note, _note);
            EditorGUILayout.Space(2f);
        }

        static void HeaderRow(string[] names)
        {
            Rect line = EditorGUILayout.GetControlRect(false, Line);
            Rect[] cells = Split(line, names.Length);
            for (int i = 0; i < names.Length; i++)
                GUI.Label(cells[i], names[i], _header);
        }

        // A row's name (with its colour) and the rects of its cells.
        static Rect[] Row(int index, string name, int cells)
        {
            Rect line = EditorGUILayout.GetControlRect(false, Line + BarHeight + 2f);
            EditorGUI.DrawRect(new Rect(line.x, line.y + 3f, 4f, Line - 6f), WeightStripe.ColorOf(index));
            GUI.Label(new Rect(line.x + 8f, line.y, NameWidth - 8f, Line), name);
            return Split(line, cells);
        }

        static Rect[] Split(Rect line, int count)
        {
            var cells = new Rect[count];
            float width = (line.width - NameWidth) / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
                cells[i] = new Rect(line.x + NameWidth + i * width, line.y, width - Gap, line.height);
            return cells;
        }

        // A weight field with its share next to it and a share bar under both.
        static float Cell(Rect cell, float weight, float share, int colorIndex)
        {
            float fieldWidth = Mathf.Min(48f, cell.width * 0.5f);
            float edited = EditorGUI.DelayedFloatField(new Rect(cell.x, cell.y, fieldWidth, Line), weight);
            GUI.Label(new Rect(cell.x + fieldWidth, cell.y, cell.width - fieldWidth, Line), share > 0f ? $"{share * 100f:0.#}%" : "–", _share);
            Bar(cell, share, colorIndex);
            return edited;
        }

        static void ShareCell(Rect cell, float share, int colorIndex)
        {
            GUI.Label(new Rect(cell.x, cell.y, cell.width, Line), share > 0f ? $"{share * 100f:0.0}%" : "–", _share);
            Bar(cell, share, colorIndex);
        }

        static void Bar(Rect cell, float share, int colorIndex)
        {
            var bar = new Rect(cell.x, cell.y + Line + 1f, cell.width, BarHeight);
            EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.25f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(share), bar.height), WeightStripe.ColorOf(colorIndex));
        }

        static void ResultRow(string name, float[] values, Color color)
        {
            Rect line = EditorGUILayout.GetControlRect(false, Line);
            GUI.Label(new Rect(line.x + 8f, line.y, NameWidth - 8f, Line), name, EditorStyles.miniBoldLabel);
            Rect[] cells = Split(line, values.Length);
            Color old = GUI.contentColor;
            for (int i = 0; i < values.Length; i++)
            {
                GUI.contentColor = values[i] > 0f ? color : old;
                GUI.Label(cells[i], $"{values[i] * 100f:0.0}%", _share);
            }
            GUI.contentColor = old;
        }

        static void Styles()
        {
            SkyNames ??= Array.ConvertAll(Skies, s => ObjectNames.NicifyVariableName(s.ToString()));
            PeriodNames ??= Array.ConvertAll(Periods, p => ObjectNames.NicifyVariableName(p.ToString()));
            WindNames ??= Array.ConvertAll(Winds, w => w.ToString());
            _section ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _note ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = EditorStyles.centeredGreyMiniLabel.normal.textColor } };
            _share ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(4, 0, 0, 0) };
            _header ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft };
        }
    }
}
