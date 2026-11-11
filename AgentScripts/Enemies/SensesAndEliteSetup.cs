using System;
using MB13.Aipocalypse.Core.EnemySystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Content for the enemies' senses and the elite tier. Safe to re-run:
// - Data/Enemies/EliteSettings.asset (the shared elite tier) and its eye material (_Shared/EyeGlow/EyeGlow_Elite.mat, the
//   goblin eyes in another colour), linked from every goblin definition (Elite);
// - spotting ranges per kind (the goblins keep the definition's default, the ranged kinds see further);
// - the ranged kinds' Friends mask (the Enemy layer) for covering fire.
// Run: unity command script run --file AgentScripts/Enemies/SensesAndEliteSetup.cs --entry SensesAndEliteSetup.All
public static class SensesAndEliteSetup
{
    const string Enemies = "Assets/_Game/Content/Enemies/";
    const string ElitePath = "Assets/_Game/Data/Enemies/EliteSettings.asset";
    const string EyePath = Enemies + "_Shared/EyeGlow/EyeGlow_Elite.mat";

    public static string All()
    {
        Material eyes = EliteEyes();
        var elite = AssetDatabase.LoadAssetAtPath<EliteSettings>(ElitePath);
        if (elite == null)
        {
            elite = ScriptableObject.CreateInstance<EliteSettings>();
            AssetDatabase.CreateAsset(elite, ElitePath);
        }
        elite.EyeMaterial = eyes;
        Save(elite);

        string log = "";
        foreach ((string kind, float spot) in new[] { ("Goblin", 20f), ("GoblinSpear", 25f), ("GoblinArcher", 30f) })
        {
            var definition = Load<EnemyDefinition>($"{Enemies}{kind}/{kind}.asset");
            definition.Elite = elite;
            definition.SpotRange = spot;
            Save(definition);
            var ranged = AssetDatabase.LoadAssetAtPath<RangedSettings>($"{Enemies}{kind}/{kind}_Ranged.asset");
            if (ranged != null)
            {
                ranged.Friends = LayerMask.GetMask("Enemy");
                Save(ranged);
            }
            log += $"{kind}: elite tier, spots at {spot} m{(ranged != null ? ", holds fire for friends" : "")}\n";
        }
        return log + $"elite: {ElitePath} (chance {elite.Chance}, HP ×{elite.HealthMultiplier}, damage ×{elite.DamageMultiplier}, scale {elite.Scale}), eyes {EyePath}";
    }

    // The goblin eye material in a cold violet-white, so an elite reads at a glance at night.
    static Material EliteEyes()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(EyePath);
        if (material == null)
        {
            material = new Material(Load<Material>(Enemies + "_Shared/EyeGlow/EyeGlow_Goblin.mat"));
            AssetDatabase.CreateAsset(material, EyePath);
        }
        material.SetColor("_Color", new Color(2.2f, 0.9f, 4.5f, 1f));
        Save(material);
        return material;
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("missing " + path);

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
