using MB13.Aipocalypse.Core.SpawnSystem;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Editor.RandomSystem
{
    // SpawnTable with its weight stripe on top (shares before day phase, day range and alive caps rule entries out).
    [CustomEditor(typeof(SpawnTable))]
    public sealed class SpawnTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var table = (SpawnTable)target;
            int count = table.Entries.Count;
            var labels = new string[count];
            var weights = new float[count];
            for (int i = 0; i < count; i++)
            {
                SpawnTableEntry entry = table.Entries[i];
                labels[i] = entry.Card != null ? entry.Card.name : $"#{i} (none)";
                weights[i] = entry.Card != null ? Mathf.Max(0f, entry.Weight) : 0f;
            }
            Rect stripe = EditorGUILayout.GetControlRect(false, WeightStripe.Height);
            WeightStripe.Draw(stripe, labels, weights, table.NothingWeight);
            EditorGUILayout.Space(2f);
            DrawDefaultInspector();
        }
    }
}
