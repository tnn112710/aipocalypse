using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Edit mode: gives each animated gun's left grip the contacts where the hand may turn to meet the arm (HandGrip > Turns,
// HANDS.md → Wrist comfort): the forend hold (rolls round the forend's middle, measured from the gun's meshes) and the
// bolt handle (a pinch). Pistols: the slide only (their default contact is the other hand).
//   All()            every gun prefab (a variant of another gun inherits from it)
//   One(gun)         one gun
//   Measure(gun)     the forend radius it would use, without writing
public static class GripTurnSetup
{
    // Round the forend, flat on the palm (degrees).
    static readonly Vector2 ForendTurn = new(15f, 35f);
    // A pinch on a bolt handle or slide may roll far round the handle, and turn a little flat on the palm.
    static readonly Vector2 BoltTurn = new(90f, 20f);
    // Rig/LeftHand sources (HANDS.md → Weapon animation authoring).
    const int ForendSource = 0, BoltSource = 3;
    static readonly HashSet<string> Pistols = new() { "Pistol", "Glock17", "DesertEagle" };

    public static string All()
    {
        var sb = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Weapons" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponentInChildren<HeldGun>(true) == null || asset.GetComponent<ItemHands>() == null)
                continue;
            // A variant of another gun (AK-74M of the AK47) inherits the turns.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(asset);
            if (source != null && source.GetComponent<ItemHands>() != null)
                continue;
            sb.AppendLine(Write(path, true));
        }
        return sb.ToString();
    }

    public static string One(string gun) => Write(PathOf(gun), true);

    public static string Measure(string gun) => Write(PathOf(gun), false);

    static string PathOf(string gun) => $"Assets/_Game/Content/Weapons/{gun}/{gun}.prefab";

    static string Write(string path, bool save)
    {
        if (Application.isPlaying)
            return "leave Play mode first";
        string gun = System.IO.Path.GetFileNameWithoutExtension(path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            HandGrip grip = root.GetComponent<ItemHands>()?.GetGrip(HandSide.Left);
            MultiParentConstraint constraint = FindLeftConstraint(root);
            if (grip == null)
                return $"{gun}: no left grip";
            // A gun without a left-hand constraint holds its grip still: the grip is the forend contact.
            Transform forend = grip.transform, bolt = null;
            if (constraint != null)
            {
                WeightedTransformArray sources = constraint.data.sourceObjects;
                forend = sources.Count > ForendSource ? sources[ForendSource].transform : null;
                bolt = sources.Count > BoltSource ? sources[BoltSource].transform : null;
            }

            var turns = new List<GripTurn>();
            string report = gun + ":";
            if (forend != null && !Pistols.Contains(gun))
            {
                float palmOffset = new SerializedObject(grip).FindProperty("_palmOffset").floatValue;
                float radius = ForendRadius(root, forend, grip.transform.rotation) + palmOffset;
                turns.Add(new GripTurn { Contact = forend, Radius = radius, Max = ForendTurn });
                report += $" forend {forend.name} radius {radius.ToString("F3", CultureInfo.InvariantCulture)}";
            }
            if (bolt != null && bolt != forend)
            {
                turns.Add(new GripTurn { Contact = bolt, Radius = 0f, Max = BoltTurn });
                report += $" bolt {bolt.name}";
            }
            if (save)
            {
                grip.SetTurns(turns.ToArray());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            return report;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static MultiParentConstraint FindLeftConstraint(GameObject root)
    {
        foreach (MultiParentConstraint c in root.GetComponentsInChildren<MultiParentConstraint>(true))
            if (c.name == "LeftHand")
                return c;
        return null;
    }

    // From the contact to the middle of the part under the palm: the gun's mesh vertices in a thin slice across the part
    // at the contact (grip frame: Z along the part, Y out of the back of the hand), the middle of their extent below it.
    static float ForendRadius(GameObject root, Transform contact, Quaternion hold)
    {
        Quaternion inverse = Quaternion.Inverse(hold);
        float low = float.MaxValue, high = float.MinValue;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            Vector3[] vertices = filter.sharedMesh.vertices;
            Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
            foreach (Vector3 v in vertices)
            {
                Vector3 q = inverse * (toWorld.MultiplyPoint3x4(v) - contact.position);
                if (Mathf.Abs(q.z) > 0.02f || Mathf.Abs(q.x) > 0.06f || q.y > 0.01f || q.y < -0.12f)
                    continue;
                low = Mathf.Min(low, q.y);
                high = Mathf.Max(high, q.y);
            }
        }
        return low > high ? 0.04f : Mathf.Clamp(-(low + high) * 0.5f, 0.015f, 0.08f);
    }
}
