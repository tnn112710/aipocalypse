using System;
using System.Linq;
using ArmoryCore;
using UnityEditor;
using UnityEngine;

namespace ArmoryCoreEditor
{
    /// <summary>
    /// Weapon types to start from: each one fills the mechanism, feed, shot, recoil, handling and heat of a definition
    /// with the values of a typical weapon of that kind and loads its usual cartridge.
    /// </summary>
    internal static class ArmoryCoreWeaponPresets
    {
        public sealed class Preset
        {
            public string Title;
            public string Glyph;
            public string Cartridge;
            public ArmoryCoreWeaponAction Action;
            public ArmoryCoreReloadType Reload;
            public ArmoryCoreFireMode[] Modes;
            public float Rpm;
            public int Burst = 3;
            public float Cycle = 0.7f;
            public int Magazine;
            public int Chamber = 1;
            public int Projectiles = 1;
            public float Damage;
            public float Moa;
            public float Hip = 10f;
            public float Vertical;
            public float Horizontal;
            public float Randomness = 0.13f;
            public float Recovery = 4.5f;
            public float Mass;
            public float Aim;
            public float Tactical;
            public float Empty;
            public float PerRound;
            public float Heat;
            public float Reliability = 3500f;
        }

        private static readonly ArmoryCoreFireMode[] SemiOnly = { ArmoryCoreFireMode.Safe, ArmoryCoreFireMode.SemiAutomatic };
        private static readonly ArmoryCoreFireMode[] SelectFire = { ArmoryCoreFireMode.Safe, ArmoryCoreFireMode.SemiAutomatic, ArmoryCoreFireMode.Automatic };

        // Values follow the weapons shipped with ArmoryCore, so a new weapon lands next to them.
        public static readonly Preset[] All =
        {
            new Preset
            {
                Title = "Pistol", Glyph = "⌐", Cartridge = "9x19", Action = ArmoryCoreWeaponAction.ClosedBolt, Reload = ArmoryCoreReloadType.DetachableMagazine,
                Modes = SemiOnly, Rpm = 300f, Magazine = 15, Damage = 8f, Moa = 11f, Vertical = 0.48f, Horizontal = 0.2f, Randomness = 0.14f,
                Mass = 1.05f, Aim = 0.17f, Tactical = 2.4f, Empty = 2.9f, PerRound = 0.45f, Heat = 0.3f
            },
            new Preset
            {
                Title = "Submachine gun", Glyph = "═", Cartridge = "9x19", Action = ArmoryCoreWeaponAction.ClosedBolt, Reload = ArmoryCoreReloadType.DetachableMagazine,
                Modes = SelectFire, Rpm = 800f, Magazine = 30, Damage = 7f, Moa = 8.6f, Vertical = 0.56f, Horizontal = 0.25f,
                Mass = 2.8f, Aim = 0.22f, Tactical = 2.5f, Empty = 3.1f, PerRound = 0.35f, Heat = 0.5f
            },
            new Preset
            {
                Title = "Assault rifle", Glyph = "═", Cartridge = "5.56x45", Action = ArmoryCoreWeaponAction.ClosedBolt, Reload = ArmoryCoreReloadType.DetachableMagazine,
                Modes = SelectFire, Rpm = 650f, Magazine = 30, Damage = 10f, Moa = 5.75f, Vertical = 0.64f, Horizontal = 0.27f,
                Mass = 3.6f, Aim = 0.28f, Tactical = 2.665f, Empty = 3.25f, PerRound = 0.35f, Heat = 0.73f
            },
            new Preset
            {
                Title = "Marksman rifle", Glyph = "═", Cartridge = "7.62x51", Action = ArmoryCoreWeaponAction.ClosedBolt, Reload = ArmoryCoreReloadType.DetachableMagazine,
                Modes = SemiOnly, Rpm = 300f, Magazine = 20, Damage = 15f, Moa = 3.5f, Vertical = 0.96f, Horizontal = 0.3f, Randomness = 0.12f,
                Mass = 4.2f, Aim = 0.32f, Tactical = 2.8f, Empty = 3.4f, PerRound = 0.4f, Heat = 0.9f, Reliability = 4000f
            },
            new Preset
            {
                Title = "Bolt-action rifle", Glyph = "━", Cartridge = "7.62x54R", Action = ArmoryCoreWeaponAction.Manual, Reload = ArmoryCoreReloadType.DetachableMagazine,
                Modes = SemiOnly, Rpm = 40f, Cycle = 1.1f, Magazine = 5, Damage = 18f, Moa = 2f, Vertical = 1f, Horizontal = 0.25f, Randomness = 0.1f, Recovery = 4f,
                Mass = 4.3f, Aim = 0.35f, Tactical = 3f, Empty = 3.6f, PerRound = 0.55f, Heat = 1.2f, Reliability = 6000f
            },
            new Preset
            {
                Title = "Shotgun", Glyph = "≡", Cartridge = "12x70", Action = ArmoryCoreWeaponAction.Pump, Reload = ArmoryCoreReloadType.InternalMagazine,
                Modes = SemiOnly, Rpm = 60f, Cycle = 0.6f, Magazine = 8, Projectiles = 10, Damage = 2f, Moa = 17.25f, Vertical = 1.6f, Horizontal = 0.4f, Randomness = 0.2f, Recovery = 4f,
                Mass = 3.4f, Aim = 0.27f, Tactical = 2.8f, Empty = 3.4f, PerRound = 0.5f, Heat = 1.2f, Reliability = 3000f
            },
            new Preset
            {
                Title = "Machine gun", Glyph = "☰", Cartridge = "7.62x51", Action = ArmoryCoreWeaponAction.OpenBolt, Reload = ArmoryCoreReloadType.BeltFed,
                Modes = new[] { ArmoryCoreFireMode.Safe, ArmoryCoreFireMode.Automatic }, Rpm = 650f, Magazine = 100, Damage = 15f, Moa = 6.9f, Vertical = 0.88f, Horizontal = 0.35f, Randomness = 0.14f,
                Mass = 8.5f, Aim = 0.45f, Tactical = 5.5f, Empty = 6.5f, PerRound = 0.35f, Heat = 1f, Reliability = 2500f
            },
            new Preset
            {
                Title = "Rocket launcher", Glyph = "▶", Cartridge = "RG7", Action = ArmoryCoreWeaponAction.ClosedBolt, Reload = ArmoryCoreReloadType.InternalMagazine,
                Modes = SemiOnly, Rpm = 30f, Magazine = 1, Damage = 1f, Moa = 6.9f, Hip = 6f, Vertical = 1.6f, Horizontal = 0.6f, Randomness = 0.2f, Recovery = 3f,
                Mass = 7f, Aim = 0.4f, Tactical = 2.05f, Empty = 2.5f, PerRound = 2.5f, Heat = 1.15f, Reliability = 5000f
            },
        };

        private static ArmoryCoreGraph.Tile[] tiles;

        public static ArmoryCoreGraph.Tile[] Tiles
        {
            get
            {
                if (tiles != null)
                    return tiles;
                tiles = new ArmoryCoreGraph.Tile[All.Length];
                for (var i = 0; i < All.Length; i++)
                {
                    var preset = All[i];
                    tiles[i] = new ArmoryCoreGraph.Tile(
                        preset.Title,
                        $"{preset.Mass:0.#} kg · {preset.Rpm:0} rpm",
                        $"{ObjectNames.NicifyVariableName(preset.Action.ToString())}, {ObjectNames.NicifyVariableName(preset.Reload.ToString()).ToLowerInvariant()} "
                        + $"of {preset.Magazine}, {preset.Cartridge} cartridge. Click to fill the definition with these values (Ctrl+Z undoes).")
                    {
                        Glyph = preset.Glyph,
                        Color = ArmoryCoreGraph.Accent
                    };
                }
                return tiles;
            }
        }

        /// <summary>The preset closest to the definition: same mechanism and feed first, then rate, mass and pellets.</summary>
        public static int Closest(ArmoryCoreWeaponDefinition definition)
        {
            if (definition == null)
                return -1;
            var best = -1;
            var bestScore = float.MaxValue;
            for (var i = 0; i < All.Length; i++)
            {
                var preset = All[i];
                var score = (preset.Action == definition.action ? 0f : 1.5f)
                    + (preset.Reload == definition.reloadType ? 0f : 1f)
                    + Mathf.Abs(Mathf.Log(Mathf.Max(1f, definition.roundsPerMinute) / preset.Rpm))
                    + Mathf.Abs(Mathf.Log(Mathf.Max(0.1f, definition.weaponMassKilograms) / preset.Mass))
                    + (preset.Projectiles > 1 == definition.projectilesPerShot > 1 ? 0f : 3f)
                    + (definition.ammunition != null && preset.Title == "Rocket launcher" != definition.ammunition.IsRocket ? 4f : 0f);
                if (score >= bestScore)
                    continue;
                bestScore = score;
                best = i;
            }
            return best;
        }

        public static ArmoryCoreAmmunitionDefinition FindCartridge(string name)
        {
            return ArmoryCoreCartridgeLibrary.All.FirstOrDefault(a => string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase))
                ?? ArmoryCoreCartridgeLibrary.All.FirstOrDefault(a => string.Equals(a.id, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Writes the preset through <paramref name="data"/>, so it is one undo step with the rest of the inspector.</summary>
        public static void Apply(SerializedObject data, Preset preset)
        {
            Set(data, "action", (int)preset.Action);
            Set(data, "reloadType", (int)preset.Reload);
            SetModes(data.FindProperty("fireModes"), preset.Modes);
            Set(data, "roundsPerMinute", preset.Rpm);
            Set(data, "burstSize", preset.Burst);
            Set(data, "actionCycleTime", preset.Cycle);
            Set(data, "magazineCapacity", preset.Magazine);
            Set(data, "chamberCapacity", preset.Chamber);
            Set(data, "projectilesPerShot", preset.Projectiles);
            Set(data, "damagePerProjectile", preset.Damage);
            Set(data, "muzzleVelocityMultiplier", 1f);
            Set(data, "accuracyMinutesOfAngle", preset.Moa);
            Set(data, "hipFireSpreadMultiplier", preset.Hip);
            Set(data, "verticalRecoilDegrees", preset.Vertical);
            Set(data, "horizontalRecoilDegrees", preset.Horizontal);
            Set(data, "recoilRandomness", preset.Randomness);
            Set(data, "recoilRecoveryDegreesPerSecond", preset.Recovery);
            Set(data, "weaponMassKilograms", preset.Mass);
            Set(data, "aimDownSightsTime", preset.Aim);
            Set(data, "tacticalReloadTime", preset.Tactical);
            Set(data, "emptyReloadTime", preset.Empty);
            Set(data, "perRoundReloadTime", preset.PerRound);
            Set(data, "heatPerShot", preset.Heat);
            Set(data, "meanRoundsBetweenFailures", preset.Reliability);
            var cartridge = FindCartridge(preset.Cartridge);
            if (cartridge != null)
                data.FindProperty("ammunition").objectReferenceValue = cartridge;
            SyncMechanismTags(data);
        }

        private static void Set(SerializedObject data, string name, float value)
        {
            var property = data.FindProperty(name);
            if (property == null)
                return;
            if (property.propertyType == SerializedPropertyType.Integer)
                property.intValue = Mathf.RoundToInt(value);
            else if (property.propertyType == SerializedPropertyType.Enum)
                property.enumValueIndex = Mathf.RoundToInt(value);
            else
                property.floatValue = value;
        }

        public static void SetModes(SerializedProperty modes, ArmoryCoreFireMode[] values)
        {
            if (modes == null)
                return;
            modes.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                modes.GetArrayElementAtIndex(i).enumValueIndex = (int)values[i];
        }

        /// <summary>
        /// The shipped definitions tag themselves with their action and feed (attachments may require them). When the
        /// definition already carries such tags, they follow the current action and feed.
        /// </summary>
        public static void SyncMechanismTags(SerializedObject data)
        {
            var tags = data.FindProperty("weaponTags");
            if (tags == null)
                return;
            var mechanism = Enum.GetNames(typeof(ArmoryCoreWeaponAction)).Concat(Enum.GetNames(typeof(ArmoryCoreReloadType))).ToArray();
            var tagged = false;
            for (var i = tags.arraySize - 1; i >= 0; i--)
            {
                if (!mechanism.Contains(tags.GetArrayElementAtIndex(i).stringValue))
                    continue;
                tags.DeleteArrayElementAtIndex(i);
                tagged = true;
            }
            if (!tagged)
                return;
            var action = (ArmoryCoreWeaponAction)data.FindProperty("action").enumValueIndex;
            var reload = (ArmoryCoreReloadType)data.FindProperty("reloadType").enumValueIndex;
            tags.InsertArrayElementAtIndex(0);
            tags.GetArrayElementAtIndex(0).stringValue = action.ToString();
            tags.InsertArrayElementAtIndex(1);
            tags.GetArrayElementAtIndex(1).stringValue = reload.ToString();
        }
    }
}
