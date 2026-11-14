using System;
using System.Text;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEngine;

// First-pass gun stats from real-life figures, anchored on the AK47 asset as tuned by hand: each other gun takes the
// AK's numbers times its real-life ratio to the AKM. Measurable things (fire rate, magazine, loaded weight) are the real
// ones; reload times are real times scaled by the AK's 2 s against a real ~2.5 s. Damage, bullet speed, range and
// recoil use the square root of the real ratio, because the game compresses them (the AK runs at 42 % of its real
// muzzle velocity on a compressed map); a straight ratio would leave .45 rounds at 106 m/s with 6 m of full damage.
// Knockback is momentum, so it takes the straight momentum ratio. Feel values (aim speed, sway, zoom, recoil response,
// spread) stay as they are. Re-run after retuning the AK: AgentScripts/Weapons/RealisticGunStats.cs, entry Apply.
public static class RealisticGunStats
{
    const string Weapons = "Assets/_Game/Content/Weapons/";

    sealed class Real
    {
        public string Gun;
        public float BulletGrams, Velocity, EffectiveRange, LoadedKg, PowderGrams, ReloadSeconds, RoundsPerMinute;
        public int Magazine, Pellets = 1;
    }

    // AKM 7.62x39 PS; MP5A3 9x19 124 gr; M1911A1 .45 ACP 230 gr; 12 gauge 2 3/4" 00 buckshot (9 pellets of 3.5 g) from a
    // Remington 870 (4+1 tube) and a 28" side-by-side. Loaded weights. Powder charges for the free-recoil estimate.
    static readonly Real Akm = new() { Gun = "AK47", BulletGrams = 7.9f, Velocity = 715f, EffectiveRange = 350f, LoadedKg = 3.6f, PowderGrams = 1.6f, ReloadSeconds = 2.5f, Magazine = 30, RoundsPerMinute = 600f };

    static readonly Real[] Others =
    {
        new() { Gun = "MP5", BulletGrams = 8f, Velocity = 400f, EffectiveRange = 200f, LoadedKg = 3f, PowderGrams = 0.4f, ReloadSeconds = 2.5f, Magazine = 30, RoundsPerMinute = 800f },
        new() { Gun = "Pistol", BulletGrams = 14.9f, Velocity = 253f, EffectiveRange = 50f, LoadedKg = 1.2f, PowderGrams = 0.4f, ReloadSeconds = 1.8f, Magazine = 7, RoundsPerMinute = 400f },
        // Per-round reload: the time for one shell.
        new() { Gun = "PumpShotgun", BulletGrams = 3.5f, Pellets = 9, Velocity = 390f, EffectiveRange = 35f, LoadedKg = 3.6f, PowderGrams = 1.8f, ReloadSeconds = 0.6f, Magazine = 5, RoundsPerMinute = 300f },
        new() { Gun = "DoubleBarrel", BulletGrams = 3.5f, Pellets = 9, Velocity = 410f, EffectiveRange = 40f, LoadedKg = 3.4f, PowderGrams = 1.8f, ReloadSeconds = 3f, Magazine = 2, RoundsPerMinute = 300f },

        // Block-outs (GunBlockoutSetup). Semi-automatics' rate is a practical click limit; the hunting rifle's bolt
        // (CycleTime) and per-round reload are its own.
        // Glock 17 9x19 124 gr; Desert Eagle .50 AE 300 gr; Uzi 9x19; P90 5.7x28 SS190; M1 Carbine .30 Carbine; M4 5.56 M855
        // from 14.5"; AK-74M 5.45x39 7N6; FAL 7.62x51 M80; Remington 700 .308 168 gr (4 + 1, loaded one at a time); AA-12
        // 00 buckshot, 8-round box; RPD 7.62x39 from a 100-round drum.
        new() { Gun = "Glock17", BulletGrams = 8f, Velocity = 375f, EffectiveRange = 50f, LoadedKg = 0.9f, PowderGrams = 0.4f, ReloadSeconds = 1.8f, Magazine = 17, RoundsPerMinute = 450f },
        new() { Gun = "DesertEagle", BulletGrams = 19.4f, Velocity = 470f, EffectiveRange = 50f, LoadedKg = 2f, PowderGrams = 1.9f, ReloadSeconds = 2f, Magazine = 7, RoundsPerMinute = 180f },
        new() { Gun = "Uzi", BulletGrams = 8f, Velocity = 400f, EffectiveRange = 150f, LoadedKg = 3.8f, PowderGrams = 0.4f, ReloadSeconds = 2.5f, Magazine = 32, RoundsPerMinute = 600f },
        new() { Gun = "P90", BulletGrams = 2f, Velocity = 715f, EffectiveRange = 200f, LoadedKg = 3f, PowderGrams = 0.4f, ReloadSeconds = 3f, Magazine = 50, RoundsPerMinute = 900f },
        new() { Gun = "M1Carbine", BulletGrams = 7.1f, Velocity = 600f, EffectiveRange = 300f, LoadedKg = 2.6f, PowderGrams = 0.9f, ReloadSeconds = 2f, Magazine = 15, RoundsPerMinute = 450f },
        new() { Gun = "M4", BulletGrams = 4f, Velocity = 880f, EffectiveRange = 500f, LoadedKg = 3.4f, PowderGrams = 1.6f, ReloadSeconds = 2.5f, Magazine = 30, RoundsPerMinute = 800f },
        new() { Gun = "AK74M", BulletGrams = 3.4f, Velocity = 900f, EffectiveRange = 500f, LoadedKg = 3.9f, PowderGrams = 1.45f, ReloadSeconds = 2.5f, Magazine = 30, RoundsPerMinute = 650f },
        new() { Gun = "FAL", BulletGrams = 9.5f, Velocity = 840f, EffectiveRange = 600f, LoadedKg = 4.8f, PowderGrams = 2.9f, ReloadSeconds = 2.8f, Magazine = 20, RoundsPerMinute = 400f },
        new() { Gun = "HuntingRifle", BulletGrams = 10.9f, Velocity = 820f, EffectiveRange = 800f, LoadedKg = 4.3f, PowderGrams = 2.8f, ReloadSeconds = 0.8f, Magazine = 5, RoundsPerMinute = 60f },
        new() { Gun = "AA12", BulletGrams = 3.5f, Pellets = 9, Velocity = 380f, EffectiveRange = 35f, LoadedKg = 5.7f, PowderGrams = 1.8f, ReloadSeconds = 2.5f, Magazine = 8, RoundsPerMinute = 300f },
        new() { Gun = "RPD", BulletGrams = 7.9f, Velocity = 735f, EffectiveRange = 800f, LoadedKg = 9f, PowderGrams = 1.6f, ReloadSeconds = 5f, Magazine = 100, RoundsPerMinute = 700f },
    };

    static float Momentum(Real gun) => gun.BulletGrams * 0.001f * gun.Velocity;

    // Free recoil energy (J): bullet(s) plus powder gas (leaving at about 1.2 km/s) against the loaded gun.
    static float RecoilEnergy(Real gun)
    {
        float impulse = Momentum(gun) * gun.Pellets + gun.PowderGrams * 0.001f * 1200f;
        float speed = impulse / gun.LoadedKg;
        return 0.5f * gun.LoadedKg * speed * speed;
    }

    public static string Apply()
    {
        var ak = Load(Akm.Gun);
        var report = new StringBuilder();
        foreach (Real real in Others)
        {
            GunDefinition gun = Load(real.Gun);
            float damage = Mathf.Sqrt(Momentum(real) / Momentum(Akm));
            float speed = Mathf.Sqrt(real.Velocity / Akm.Velocity);
            float range = Mathf.Sqrt(real.EffectiveRange / Akm.EffectiveRange);
            float recoil = Mathf.Sqrt(RecoilEnergy(real) / RecoilEnergy(Akm));

            var so = new SerializedObject(gun);
            so.FindProperty("_fire.Damage").floatValue = Mathf.Round(ak.Fire.Damage * damage);
            so.FindProperty("_fire.Knockback").floatValue = Round2(ak.Fire.Knockback * Momentum(real) / Momentum(Akm));
            so.FindProperty("_fire.Pellets").intValue = real.Pellets;
            so.FindProperty("_fire.RoundsPerMinute").floatValue = real.RoundsPerMinute;
            so.FindProperty("_fire.Falloff.Start").floatValue = Mathf.Round(ak.Fire.Falloff.Start * range);
            so.FindProperty("_fire.Falloff.End").floatValue = Mathf.Round(ak.Fire.Falloff.End * range);
            so.FindProperty("_fire.StaminaPerShot").floatValue = Round2(ak.Fire.StaminaPerShot * recoil);
            so.FindProperty("_bullet.Speed").floatValue = Mathf.Round(ak.Bullet.Speed * speed);
            so.FindProperty("_bullet.Range").floatValue = Mathf.Round(ak.Bullet.Range * range);
            so.FindProperty("_magazine.Size").intValue = real.Magazine;
            so.FindProperty("_magazine.ReloadTime").floatValue = Round2(real.ReloadSeconds * ak.Magazine.ReloadTime / Akm.ReloadSeconds);
            so.FindProperty("_weight").floatValue = real.LoadedKg;

            // Recoil: this gun's own pattern (sideways vs up, model kick) is kept; its size follows the free recoil energy.
            float targetVertical = ak.Recoil.Hip.Vertical * recoil;
            float scale = gun.Recoil.Hip.Vertical > 0f ? targetVertical / gun.Recoil.Hip.Vertical : 1f;
            ScaleKick(so.FindProperty("_recoil.Hip"), scale);
            ScaleKick(so.FindProperty("_recoil.Aim"), scale);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gun);
            AssetDatabase.SaveAssetIfDirty(gun);

            report.AppendLine($"{real.Gun}: damage {gun.Fire.Damage}x{gun.Fire.Pellets}, knockback {gun.Fire.Knockback}, {gun.Fire.RoundsPerMinute} rpm, " +
                $"mag {gun.Magazine.Size}, reload {gun.Magazine.ReloadTime} s, speed {gun.Bullet.Speed} m/s, range {gun.Bullet.Range} m, " +
                $"falloff {gun.Fire.Falloff.Start}-{gun.Fire.Falloff.End} m, recoil up {gun.Recoil.Hip.Vertical:0.0} deg (x{scale:0.00}), " +
                $"stamina {gun.Fire.StaminaPerShot}, weight {gun.Weight} kg (free recoil {RecoilEnergy(real):0.0} J vs AK {RecoilEnergy(Akm):0.0} J)");
        }
        return report.ToString();
    }

    static GunDefinition Load(string gun) =>
        AssetDatabase.LoadAssetAtPath<GunDefinition>($"{Weapons}{gun}/{gun}.asset") ?? throw new Exception("missing gun " + gun);

    static float Round2(float value) => Mathf.Round(value * 100f) / 100f;

    static void ScaleKick(SerializedProperty values, float scale)
    {
        foreach (string field in new[] { "Vertical", "Horizontal", "Roll", "NoisePitch", "NoiseYaw", "CameraShake", "GunPitch", "GunYaw", "GunRoll", "KickBack" })
        {
            SerializedProperty property = values.FindPropertyRelative(field);
            property.floatValue = Mathf.Round(property.floatValue * scale * 1000f) / 1000f;
        }
    }
}
