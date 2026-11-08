using System.Collections.Generic;
using MB13.Aipocalypse.Core.RandomSystem;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MB13.Aipocalypse.Editor.RandomSystem
{
    // Inspector for every WeightedTable<T>: share stripe, entries with their chance, and a seeded test of N picks through
    // WeightedDraw, so NoRepeat and Units show in the results. IMGUI because Odin only hosts IMGUI property drawers.
    [CustomPropertyDrawer(typeof(WeightedTable<>), true)]
    public sealed class WeightedTableDrawer : PropertyDrawer
    {
        const float BarHeight = 3f;
        const float WeightWidth = 56f;
        const float UnitsWidth = 44f;
        const float ChanceWidth = 96f;
        const float Gap = 4f;

        sealed class TestRun
        {
            public int Picks = 10000;
            public int Seed = 1;
            public float[] Shares;
            public float Nothing;
        }

        static readonly string[] Settings =
            { nameof(WeightedTable<int>.NothingWeight), nameof(WeightedTable<int>.Count), nameof(WeightedTable<int>.NoRepeat) };
        static readonly Dictionary<(Object, string), ReorderableList> Lists = new();
        static readonly Dictionary<(Object, string), TestRun> Runs = new();

        static float Line => EditorGUIUtility.singleLineHeight;
        static float Space => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = Line + Space + WeightStripe.Height;
            if (!property.isExpanded)
                return height;
            height += Space + ListFor(property).GetHeight();
            foreach (string field in Settings)
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field)) + Space;
            height += 2 * (Line + Space); // test controls, test summary
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, Line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            row.y += Line + Space;

            Rect body = EditorGUI.IndentedRect(new Rect(position.x, row.y, position.width, position.yMax - row.y));
            ReadTable(property, out string[] labels, out float[] weights, out _);
            float nothing = property.FindPropertyRelative(nameof(WeightedTable<int>.NothingWeight)).floatValue;
            WeightStripe.Draw(new Rect(body.x, body.y, body.width, WeightStripe.Height), labels, weights, nothing);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            float y = body.y + WeightStripe.Height + Space;
            ReorderableList list = ListFor(property);
            list.DoList(new Rect(body.x, y, body.width, list.GetHeight()));
            y += list.GetHeight();

            foreach (string field in Settings)
            {
                SerializedProperty child = property.FindPropertyRelative(field);
                float height = EditorGUI.GetPropertyHeight(child);
                EditorGUI.PropertyField(new Rect(body.x, y, body.width, height), child);
                y += height + Space;
            }

            DrawTest(new Rect(body.x, y, body.width, 2 * Line + Space), property);
            EditorGUI.EndProperty();
        }

        static ReorderableList ListFor(SerializedProperty table)
        {
            var key = (table.serializedObject.targetObject, table.propertyPath);
            SerializedProperty entries = table.FindPropertyRelative(nameof(WeightedTable<int>.Entries));
            if (Lists.TryGetValue(key, out ReorderableList list) && list.serializedProperty.serializedObject == table.serializedObject)
            {
                list.serializedProperty = entries;
                return list;
            }

            string path = table.propertyPath;
            SerializedObject owner = table.serializedObject;
            list = new ReorderableList(owner, entries, true, true, true, true)
            {
                elementHeight = Line + BarHeight + 4f,
                drawHeaderCallback = DrawHeader,
            };
            list.drawElementCallback = (rect, index, _, _) => DrawEntry(rect, owner.FindProperty(path), index);
            Lists[key] = list;
            return list;
        }

        static void DrawHeader(Rect rect)
        {
            rect.xMin += 14f;
            float right = rect.xMax;
            GUI.Label(new Rect(right - ChanceWidth, rect.y, ChanceWidth, rect.height), new GUIContent("Chance", "Weight share · test result"), EditorStyles.miniLabel);
            right -= ChanceWidth + Gap;
            GUI.Label(new Rect(right - UnitsWidth, rect.y, UnitsWidth, rect.height), new GUIContent("Units", "Picks before it runs out until every entry refills (WeightedPicker); 0 = never"), EditorStyles.miniLabel);
            right -= UnitsWidth + Gap;
            GUI.Label(new Rect(right - WeightWidth, rect.y, WeightWidth, rect.height), new GUIContent("Weight", "Relative: chance = weight / total"), EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x, rect.y, right - WeightWidth - rect.x, rect.height), "Value", EditorStyles.miniLabel);
        }

        static void DrawEntry(Rect rect, SerializedProperty table, int index)
        {
            SerializedProperty entries = table.FindPropertyRelative(nameof(WeightedTable<int>.Entries));
            if (index >= entries.arraySize)
                return;
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            ReadTable(table, out _, out float[] weights, out float total);

            float share = total > 0f ? weights[index] / total : 0f;
            Color color = WeightStripe.ColorOf(index);
            if (weights[index] <= 0f)
                color.a = 0.3f;

            var line = new Rect(rect.x, rect.y + 2f, rect.width, Line);
            EditorGUI.DrawRect(new Rect(line.x, line.y + 2f, 4f, Line - 4f), color);
            float right = line.xMax;
            var chance = new Rect(right - ChanceWidth, line.y, ChanceWidth, Line);
            right -= ChanceWidth + Gap;
            var units = new Rect(right - UnitsWidth, line.y, UnitsWidth, Line);
            right -= UnitsWidth + Gap;
            var weight = new Rect(right - WeightWidth, line.y, WeightWidth, Line);
            var value = new Rect(line.x + 8f, line.y, weight.x - Gap - line.x - 8f, Line);

            EditorGUI.PropertyField(value, entry.FindPropertyRelative(nameof(WeightedEntry<int>.Value)), GUIContent.none);
            EditorGUI.PropertyField(weight, entry.FindPropertyRelative(nameof(WeightedEntry<int>.Weight)), GUIContent.none);
            EditorGUI.PropertyField(units, entry.FindPropertyRelative(nameof(WeightedEntry<int>.Units)), GUIContent.none);

            string text = $"{share * 100f:0.0}%";
            if (Runs.TryGetValue((table.serializedObject.targetObject, table.propertyPath), out TestRun run)
                && run.Shares != null && run.Shares.Length == weights.Length)
                text += $" · {run.Shares[index] * 100f:0.0}";
            GUI.Label(chance, text, EditorStyles.miniLabel);

            var bar = new Rect(value.x, line.yMax + 1f, chance.xMax - value.x, BarHeight);
            EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.25f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * share, bar.height), color);
        }

        static void DrawTest(Rect rect, SerializedProperty table)
        {
            var key = (table.serializedObject.targetObject, table.propertyPath);
            if (!Runs.TryGetValue(key, out TestRun run))
                Runs[key] = run = new TestRun();

            var row = new Rect(rect.x, rect.y, rect.width, Line);
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 40f;
            float fieldWidth = Mathf.Min(120f, (row.width - 2f * 60f - 3f * Gap) / 2f);
            run.Picks = Mathf.Clamp(EditorGUI.IntField(new Rect(row.x, row.y, fieldWidth, Line), "Picks", run.Picks), 1, 1_000_000);
            run.Seed = EditorGUI.IntField(new Rect(row.x + fieldWidth + Gap, row.y, fieldWidth, Line), "Seed", run.Seed);
            EditorGUIUtility.labelWidth = labelWidth;
            float buttons = row.x + 2f * (fieldWidth + Gap);
            if (GUI.Button(new Rect(buttons, row.y, 60f, Line), new GUIContent("Test", "Roll the picks with this seed (NoRepeat and Units included)")))
                Run(table, run);
            if (GUI.Button(new Rect(buttons + 60f + Gap, row.y, 60f, Line), "Clear"))
                run.Shares = null;

            row.y += Line + Space;
            ReadTable(table, out _, out float[] weights, out float total);
            float nothing = table.FindPropertyRelative(nameof(WeightedTable<int>.NothingWeight)).floatValue;
            string summary = $"Total weight {total:0.###}";
            if (nothing > 0f && total > 0f)
                summary += $" · nothing {nothing / total * 100f:0.0}%";
            if (run.Shares != null && run.Shares.Length == weights.Length)
                summary += $" · tested {run.Picks} picks: nothing {run.Nothing * 100f:0.0}%";
            GUI.Label(row, summary, EditorStyles.miniLabel);
        }

        static void Run(SerializedProperty table, TestRun run)
        {
            ReadTable(table, out _, out float[] weights, out _);
            SerializedProperty entries = table.FindPropertyRelative(nameof(WeightedTable<int>.Entries));
            var units = new int[weights.Length];
            for (int i = 0; i < units.Length; i++)
                units[i] = entries.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(WeightedEntry<int>.Units)).intValue;
            float nothing = table.FindPropertyRelative(nameof(WeightedTable<int>.NothingWeight)).floatValue;
            bool noRepeat = table.FindPropertyRelative(nameof(WeightedTable<int>.NoRepeat)).boolValue;

            var draw = new WeightedDraw();
            var random = new RandomStream((ulong)run.Seed);
            var counts = new int[weights.Length];
            int none = 0;
            for (int i = 0; i < run.Picks; i++)
            {
                int index = draw.Next(weights, units, nothing, noRepeat, random.Value);
                if (index < 0)
                    none++;
                else
                    counts[index]++;
            }
            run.Shares = new float[weights.Length];
            for (int i = 0; i < counts.Length; i++)
                run.Shares[i] = counts[i] / (float)run.Picks;
            run.Nothing = none / (float)run.Picks;
        }

        // Labels and pickable weights (0 for an unset value) of the entries; total includes the nothing weight.
        static void ReadTable(SerializedProperty table, out string[] labels, out float[] weights, out float total)
        {
            SerializedProperty entries = table.FindPropertyRelative(nameof(WeightedTable<int>.Entries));
            labels = new string[entries.arraySize];
            weights = new float[entries.arraySize];
            total = Mathf.Max(0f, table.FindPropertyRelative(nameof(WeightedTable<int>.NothingWeight)).floatValue);
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty value = entry.FindPropertyRelative(nameof(WeightedEntry<int>.Value));
                bool set = value.propertyType != SerializedPropertyType.ObjectReference || value.objectReferenceValue != null;
                labels[i] = LabelOf(value, i);
                weights[i] = set ? Mathf.Max(0f, entry.FindPropertyRelative(nameof(WeightedEntry<int>.Weight)).floatValue) : 0f;
                total += weights[i];
            }
        }

        static string LabelOf(SerializedProperty value, int index) => value.propertyType switch
        {
            SerializedPropertyType.ObjectReference => value.objectReferenceValue != null ? value.objectReferenceValue.name : $"#{index} (none)",
            SerializedPropertyType.String => string.IsNullOrEmpty(value.stringValue) ? $"#{index}" : value.stringValue,
            SerializedPropertyType.Enum => value.enumValueIndex >= 0 && value.enumValueIndex < value.enumDisplayNames.Length
                ? value.enumDisplayNames[value.enumValueIndex] : $"#{index}",
            SerializedPropertyType.Integer => value.intValue.ToString(),
            SerializedPropertyType.Float => value.floatValue.ToString("0.##"),
            SerializedPropertyType.Boolean => value.boolValue ? "true" : "false",
            _ => $"#{index}",
        };
    }
}
