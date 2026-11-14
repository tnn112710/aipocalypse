using System;
using System.Collections.Generic;
using System.Linq;
using ArmoryCore;
using UnityEditor;
using UnityEngine;
using Graph = ArmoryCoreEditor.ArmoryCoreGraph;
using StepState = ArmoryCoreEditor.ArmoryCoreSetupGUI.StepState;

namespace ArmoryCoreEditor
{
    /// <summary>
    /// The weapon definition as numbered setup steps: type and cartridge, action, shot, magazine, recoil, handling, heat,
    /// sound and attachments. Each step shows scales with real weapons as marks and a picture of what the numbers do.
    /// Shared by the definition inspector, the controller inspector and the Weapon Setup window.
    /// </summary>
    internal sealed class ArmoryCoreWeaponSteps : IDisposable
    {
        private static readonly Color SprayColor = new Color(1f, 0.55f, 0.25f);
        private static readonly Color HeatColor = new Color(1f, 0.45f, 0.2f);

        private static readonly Graph.Spec RateScale = new Graph.Spec(20f, 1200f, "rpm", 1f, "0", true).Captions("slow", "fast").Colors(Graph.Muted, Graph.Bad).Limits(1f)
            .With(Graph.M(40f, "bolt"), Graph.M(60f, "pump"), Graph.M(300f, "pistol"), Graph.M(650f, "rifle"), Graph.M(900f, "SMG"));
        private static readonly Graph.Spec BurstScale = new Graph.Spec(2f, 6f, "shots", 1f, "0").Limits(2f, 20f).With(Graph.M(3f, "standard"));
        private static readonly Graph.Spec CycleScale = new Graph.Spec(0.2f, 2.5f, "s", 1f, "0.00").Captions("quick", "slow").Colors(Graph.Good, Graph.Warn).Limits(0f)
            .With(Graph.M(0.6f, "pump"), Graph.M(1.1f, "bolt"));
        private static readonly Graph.Spec ProjectileScale = new Graph.Spec(1f, 20f, "", 1f, "0").Captions("bullet", "shot").Limits(1f, 100f)
            .With(Graph.M(1f, "slug"), Graph.M(10f, "buckshot"));
        private static readonly Graph.Spec DamageScale = new Graph.Spec(0f, 60f, "", 1f, "0.#").Captions("weak", "strong").Colors(Graph.Warn, Graph.Bad).Limits(0f)
            .With(Graph.M(2f, "pellet"), Graph.M(8f, "pistol"), Graph.M(11f, "rifle"), Graph.M(18f, "magnum"), Graph.M(40f, "AMR"));
        private static readonly Graph.Spec BarrelScale = new Graph.Spec(0.6f, 1.3f, "×", 1f, "0.00").Captions("short barrel", "long").Colors(Graph.Muted, Graph.Info).Limits(0.1f)
            .With(Graph.M(0.85f, "short"), Graph.M(1f, "standard"), Graph.M(1.1f, "long"));
        private static readonly Graph.Spec AccuracyScale = new Graph.Spec(0.5f, 30f, "MOA", 1f, "0.##", true).Captions("precise", "wide").Colors(Graph.Good, Graph.Warn).Limits(0.01f)
            .With(Graph.M(1.5f, "sniper"), Graph.M(5f, "rifle"), Graph.M(8.6f, "SMG"), Graph.M(11f, "pistol"), Graph.M(17f, "buckshot"));
        private static readonly Graph.Spec HipScale = new Graph.Spec(1f, 20f, "×", 1f, "0.#").Captions("as aimed", "wild").Colors(Graph.Good, Graph.Warn).Limits(1f)
            .With(Graph.M(6f, "launcher"), Graph.M(10f, "standard"));
        private static readonly Graph.Spec MovingScale = new Graph.Spec(1f, 5f, "×", 1f, "0.##").Captions("steady", "wild").Colors(Graph.Good, Graph.Warn).Limits(1f)
            .With(Graph.M(2f, "standard"));
        private static readonly Graph.Spec HeatSpreadScale = new Graph.Spec(1f, 5f, "×", 1f, "0.##").Captions("no effect", "wild").Colors(Graph.Good, HeatColor).Limits(1f)
            .With(Graph.M(2.5f, "standard"));

        private static readonly Graph.Spec MagazineScale = new Graph.Spec(1f, 150f, "rounds", 1f, "0", true).Captions("single", "belt").Limits(1f, 1000f)
            .With(Graph.M(5f, "bolt"), Graph.M(8f, "shotgun"), Graph.M(15f, "pistol"), Graph.M(30f, "rifle"), Graph.M(100f, "belt"));
        private static readonly Graph.Spec ReloadScale = new Graph.Spec(0.5f, 8f, "s", 1f, "0.0").Captions("instant", "slow").Colors(Graph.Good, Graph.Warn).Limits(0.01f)
            .With(Graph.M(2.4f, "pistol"), Graph.M(3.25f, "rifle"), Graph.M(6.5f, "MG"));
        private static readonly Graph.Spec PerRoundScale = new Graph.Spec(0.1f, 4f, "s", 1f, "0.00", true).Captions("quick", "slow").Colors(Graph.Good, Graph.Warn).Limits(0.01f)
            .With(Graph.M(0.35f, "rifle"), Graph.M(0.5f, "shotgun"), Graph.M(2.5f, "rocket"));

        private static readonly Graph.Spec VerticalScale = new Graph.Spec(0f, 4f, "°", 1f, "0.##").Captions("none", "violent").Colors(Graph.Muted, Graph.Bad).Limits(0f)
            .With(Graph.M(0.48f, "pistol"), Graph.M(0.64f, "rifle"), Graph.M(1f, "bolt"), Graph.M(1.6f, "shotgun"));
        private static readonly Graph.Spec HorizontalScale = new Graph.Spec(0f, 2f, "°", 1f, "0.##").Captions("straight", "wanders").Colors(Graph.Muted, Graph.Warn).Limits(0f)
            .With(Graph.M(0.27f, "rifle"), Graph.M(0.6f, "launcher"));
        private static readonly Graph.Spec RandomnessScale = new Graph.Spec(0f, 1f, "%", 100f, "0").Captions("repeatable", "random").Limits(0f)
            .With(Graph.M(0.13f, "standard"));
        private static readonly Graph.Spec RecoveryScale = new Graph.Spec(0.5f, 30f, "°/s", 1f, "0.#", true).Captions("stays up", "snaps back").Colors(Graph.Muted, Graph.Good).Limits(0.01f)
            .With(Graph.M(4.5f, "standard"));
        private static readonly Graph.Spec RecoveryDelayScale = new Graph.Spec(0f, 0.5f, "s", 1f, "0.00").Captions("at once", "late").Limits(0.01f)
            .With(Graph.M(0.08f, "standard"));

        private static readonly Graph.Spec MassScale = new Graph.Spec(0.3f, 15f, "kg", 1f, "0.##", true).Captions("light", "heavy").Colors(Graph.Good, Graph.Warn).Limits(0.1f)
            .With(Graph.M(1.05f, "pistol"), Graph.M(2.8f, "SMG"), Graph.M(3.6f, "rifle"), Graph.M(8.5f, "MG"));
        private static readonly Graph.Spec AimScale = new Graph.Spec(0.05f, 1f, "s", 1f, "0.00").Captions("snappy", "sluggish").Colors(Graph.Good, Graph.Warn).Limits(0.01f)
            .With(Graph.M(0.17f, "pistol"), Graph.M(0.28f, "rifle"), Graph.M(0.45f, "MG"));

        private static readonly Graph.Spec ReliabilityScale = new Graph.Spec(100f, 20000f, "rounds", 1f, "0", true).Captions("jams", "never jams").Colors(Graph.Bad, Graph.Good).Limits(1f)
            .With(Graph.M(500f, "worn"), Graph.M(2500f, "MG"), Graph.M(3500f, "standard"), Graph.M(10000f, "match"));
        private static readonly Graph.Spec HeatPerShotScale = new Graph.Spec(0f, 3f, "", 1f, "0.##").Captions("cold", "hot").Colors(Graph.Muted, HeatColor).Limits(0f)
            .With(Graph.M(0.3f, "pistol"), Graph.M(0.73f, "rifle"), Graph.M(1.2f, "bolt"));
        private static readonly Graph.Spec CoolingScale = new Graph.Spec(0.1f, 10f, "/s", 1f, "0.##", true).Captions("keeps heat", "cools fast").Colors(HeatColor, Graph.Info).Limits(0.01f)
            .With(Graph.M(2.5f, "standard"));
        private static readonly Graph.Spec PenaltyHeatScale = new Graph.Spec(5f, 300f, "", 1f, "0", true).Captions("early", "late").Colors(Graph.Warn, Graph.Good).Limits(0.01f)
            .With(Graph.M(23f, "pistol"), Graph.M(35f, "standard"));
        private static readonly Graph.Spec CookOffScale = new Graph.Spec(10f, 500f, "", 1f, "0", true).Captions("early", "never").Colors(Graph.Bad, Graph.Good).Limits(0.01f)
            .With(Graph.M(64f, "pistol"), Graph.M(100f, "standard"));
        private static readonly Graph.Spec WearScale = new Graph.Spec(0f, 0.0002f, "% / 1000 rounds", 100000f, "0.##").Captions("lasts", "wears out").Colors(Graph.Good, Graph.Bad).Limits(0f, 1f)
            .With(Graph.M(0.000025f, "standard"));
        private static readonly Graph.Spec FoulingScale = new Graph.Spec(0f, 0.001f, "% / 1000 rounds", 100000f, "0.#").Captions("clean", "dirty").Colors(Graph.Good, Graph.Warn).Limits(0f, 1f)
            .With(Graph.M(0.00015f, "standard"));

        private static readonly Graph.Spec VolumeScale = new Graph.Spec(0f, 1f, "%", 100f, "0").Captions("quiet", "loud").Limits(0f, 1f);
        private static readonly Graph.Spec PitchScale = new Graph.Spec(0f, 0.15f, "%", 100f, "0.#").Captions("identical", "varied").Limits(0f, 0.15f)
            .With(Graph.M(0.025f, "standard"));
        private static readonly Graph.Spec NearScale = new Graph.Spec(0.1f, 20f, "m", 1f, "0.#", true).Captions("near", "far").Limits(0.01f)
            .With(Graph.M(2f, "standard"));
        private static readonly Graph.Spec FarScale = new Graph.Spec(10f, 1000f, "m", 1f, "0", true).Captions("near", "far").Limits(1f)
            .With(Graph.M(250f, "standard"));

        private static Graph.Tile[] actionTiles;
        private static Graph.Tile[] reloadTiles;
        private static readonly ArmoryCoreFireMode[] ModeOrder =
        {
            ArmoryCoreFireMode.Safe, ArmoryCoreFireMode.SemiAutomatic, ArmoryCoreFireMode.Burst, ArmoryCoreFireMode.Automatic
        };
        private static readonly Graph.Tile[] ModeTiles =
        {
            new Graph.Tile("Safe", "trigger locked", "The selector has a safe position.") { Icon = ArmoryCoreMechanismIcons.Safe, Color = Graph.Muted },
            new Graph.Tile("Semi", "one per pull", "Every pull fires one shot.") { Icon = ArmoryCoreMechanismIcons.Semi, Color = Graph.Accent },
            new Graph.Tile("Burst", "N per pull", "A pull fires a short burst of the set length.") { Icon = ArmoryCoreMechanismIcons.Burst, Color = Graph.Warn },
            new Graph.Tile("Auto", "while held", "Fires while the trigger is held, at the cyclic rate.") { Icon = ArmoryCoreMechanismIcons.Auto, Color = Graph.Bad },
        };

        private static List<ArmoryCoreAttachmentDefinition> attachments;

        /// <summary>Prefix of the remembered foldout states, so the inspector and the window keep their own.</summary>
        public string Key = "weapon";
        /// <summary>When set, the cartridge step offers this instead of the inline cartridge settings.</summary>
        public Action<ArmoryCoreAmmunitionDefinition> OpenCartridge;
        /// <summary>Result of the last action in a step, such as an applied type; the caller shows and clears it.</summary>
        public string Status;

        private readonly ArmoryCoreCartridgePicker picker = new ArmoryCoreCartridgePicker();

        static ArmoryCoreWeaponSteps()
        {
            EditorApplication.projectChanged += () => attachments = null;
        }

        private static Graph.Tile[] ActionTiles => actionTiles ??= new[]
        {
            new Graph.Tile("Closed bolt", "rifles, pistols", "Fires from a chambered round; the next round is chambered after each shot.") { Icon = ArmoryCoreMechanismIcons.ClosedBolt, IconAspect = 2f },
            new Graph.Tile("Open bolt", "SMG, machine gun", "The bolt rests open and strips a round from the magazine on every shot: nothing is chambered.") { Icon = ArmoryCoreMechanismIcons.OpenBolt, IconAspect = 2f },
            new Graph.Tile("Manual", "bolt action", "The action is cycled by hand after every shot (C).") { Icon = ArmoryCoreMechanismIcons.Manual, IconAspect = 2f },
            new Graph.Tile("Pump", "pump shotgun", "The fore-end is pumped after every shot; it cycles by itself.") { Icon = ArmoryCoreMechanismIcons.Pump, IconAspect = 2f },
            new Graph.Tile("Break action", "hinged barrel", "Opened and closed by hand after every shot.") { Icon = ArmoryCoreMechanismIcons.BreakAction, IconAspect = 2f },
        };

        private static Graph.Tile[] ReloadTiles => reloadTiles ??= new[]
        {
            new Graph.Tile("Magazine", "detachable", "The whole magazine is replaced: a tactical reload keeps the chambered round.") { Icon = ArmoryCoreMechanismIcons.DetachableMagazine, Color = Graph.Info },
            new Graph.Tile("Internal", "round by round", "Rounds are loaded one at a time into a fixed magazine or tube.") { Icon = ArmoryCoreMechanismIcons.InternalMagazine, Color = Graph.Info },
            new Graph.Tile("Single round", "one at a time", "One round is loaded per reload.") { Icon = ArmoryCoreMechanismIcons.SingleRound, Color = Graph.Info },
            new Graph.Tile("Belt", "belt-fed", "A belt or box of linked rounds is replaced.") { Icon = ArmoryCoreMechanismIcons.Belt, Color = Graph.Info },
        };

        private static List<ArmoryCoreAttachmentDefinition> Attachments => attachments ??= AssetDatabase
            .FindAssets("t:" + nameof(ArmoryCoreAttachmentDefinition))
            .Select(guid => AssetDatabase.LoadAssetAtPath<ArmoryCoreAttachmentDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(attachment => attachment)
            .OrderBy(attachment => attachment.slot)
            .ThenBy(attachment => Name(attachment))
            .ToList();

        public static string Name(ArmoryCoreWeaponDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
        }

        private static string Name(ArmoryCoreAttachmentDefinition attachment)
        {
            return string.IsNullOrWhiteSpace(attachment.displayName) ? attachment.name : attachment.displayName;
        }

        private static ArmoryCoreWeaponDefinition Target(SerializedObject data) => (ArmoryCoreWeaponDefinition)data.targetObject;

        private bool Step(string id, int number, string title, StepState state, string summary, string tooltip)
        {
            return ArmoryCoreSetupGUI.Step(Key + ".step." + id, number, title, state, summary, tooltip);
        }

        /// <summary>Every step of the definition in order; returns the number of the next step.</summary>
        public int DrawAll(SerializedObject data, int number = 1)
        {
            DrawType(data, number++);
            DrawAction(data, number++);
            DrawShot(data, number++);
            DrawMagazine(data, number++);
            DrawRecoil(data, number++);
            DrawHandling(data, number++);
            DrawReliability(data, number++);
            DrawSound(data, number++);
            DrawIdentity(data, number++);
            return number;
        }

        // ---------- 1. Type & cartridge ----------

        public void DrawType(SerializedObject data, int number)
        {
            var definition = Target(data);
            var preset = ArmoryCoreWeaponPresets.Closest(definition);
            var cartridge = definition.ammunition;
            var summary = (preset >= 0 ? ArmoryCoreWeaponPresets.All[preset].Title : "custom") + " · "
                + (cartridge ? ArmoryCoreCartridgeLibrary.Name(cartridge) : "no cartridge");
            if (!Step("type", number, "Type & cartridge", cartridge ? StepState.Done : StepState.Current, summary, "What to start from and what the weapon fires"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                var picked = Graph.Choice(preset, ArmoryCoreWeaponPresets.Tiles, 2, 40f);
                if (picked != preset && picked >= 0)
                    ApplyPreset(data, picked);
                using (new EditorGUILayout.HorizontalScope())
                {
                    Graph.Hint("The highlighted type is the closest to the current values. Picking a type rewrites mechanism, feed, shot, recoil, handling and heat; Ctrl+Z undoes.");
                    if (preset >= 0 && ArmoryCoreSetupGUI.MiniButton("Reset", $"Fill the definition with the values of «{ArmoryCoreWeaponPresets.All[preset].Title}» again", 48f))
                        ApplyPreset(data, preset);
                }
                EditorGUILayout.LabelField("Cartridge", EditorStyles.miniBoldLabel);
                picker.Open = OpenCartridge;
                picker.Draw(data.FindProperty("ammunition"), definition.muzzleVelocityMultiplier);
            }
        }

        private void ApplyPreset(SerializedObject data, int index)
        {
            var preset = ArmoryCoreWeaponPresets.All[index];
            ArmoryCoreWeaponPresets.Apply(data, preset);
            GUI.FocusControl(null);
            Status = $"Type «{preset.Title}»: mechanism, fire modes, feed, shot, recoil, handling, heat and the {preset.Cartridge} cartridge are filled in. Ctrl+Z undoes.";
        }

        // ---------- 2. Action & fire modes ----------

        public void DrawAction(SerializedObject data, int number)
        {
            var definition = Target(data);
            var modes = data.FindProperty("fireModes");
            var anyMode = modes.arraySize > 0 && ModeOrder.Skip(1).Any(mode => Contains(modes, mode));
            var summary = $"{ObjectNames.NicifyVariableName(definition.action.ToString()).ToLowerInvariant()} · {ModeNames(modes)} · {definition.roundsPerMinute:0} rpm";
            if (!Step("action", number, "Action & fire modes", anyMode ? StepState.Done : StepState.Current, summary, "How the weapon cycles, which fire modes it has and how fast it fires"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                if (Graph.Choice(data.FindProperty("action"), ActionTiles, 2, 40f))
                    ArmoryCoreWeaponPresets.SyncMechanismTags(data);
                EditorGUILayout.LabelField(new GUIContent("Fire modes", "Positions of the selector; B cycles them in play."), EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var i = 0; i < ModeOrder.Length; i++)
                    {
                        var on = Contains(modes, ModeOrder[i]);
                        if (Graph.ToggleTile(on, ModeTiles[i], 36f) != on)
                            SetMode(modes, ModeOrder[i], !on);
                    }
                }
                if (!anyMode)
                    Graph.Line("Without Semi, Burst or Auto the weapon cannot fire.", Graph.Bad);
                Graph.Scale(data, "roundsPerMinute", "Rate of fire", "Cyclic rate: rounds per minute with the trigger held, or the fastest the trigger can be pulled.", RateScale);
                if (Contains(modes, ArmoryCoreFireMode.Burst))
                    Graph.Scale(data, "burstSize", "Burst length", "Shots fired by one pull in burst mode.", BurstScale);
                var action = (ArmoryCoreWeaponAction)data.FindProperty("action").enumValueIndex;
                if (action == ArmoryCoreWeaponAction.Manual || action == ArmoryCoreWeaponAction.Pump || action == ArmoryCoreWeaponAction.BreakAction)
                    Graph.Scale(data, "actionCycleTime", "Action cycle", "Time it takes to work the action after a shot.", CycleScale);
                var interval = 60f / Mathf.Max(1f, definition.roundsPerMinute);
                Graph.Hint($"One shot every <b>{interval * 1000f:0} ms</b>." + (Contains(modes, ArmoryCoreFireMode.Automatic)
                    ? $" A held trigger empties {definition.magazineCapacity + definition.chamberCapacity} rounds in <b>{(definition.magazineCapacity + definition.chamberCapacity - 1) * interval:0.#} s</b>."
                    : ""));
            }
        }

        private static bool Contains(SerializedProperty modes, ArmoryCoreFireMode mode)
        {
            for (var i = 0; i < modes.arraySize; i++)
            {
                if (modes.GetArrayElementAtIndex(i).enumValueIndex == (int)mode)
                    return true;
            }
            return false;
        }

        private static void SetMode(SerializedProperty modes, ArmoryCoreFireMode mode, bool on)
        {
            var next = ModeOrder.Where(value => value == mode ? on : Contains(modes, value)).ToArray();
            ArmoryCoreWeaponPresets.SetModes(modes, next);
        }

        private static string ModeNames(SerializedProperty modes)
        {
            var names = new List<string>();
            if (Contains(modes, ArmoryCoreFireMode.SemiAutomatic))
                names.Add("semi");
            if (Contains(modes, ArmoryCoreFireMode.Burst))
                names.Add("burst");
            if (Contains(modes, ArmoryCoreFireMode.Automatic))
                names.Add("auto");
            return names.Count == 0 ? "cannot fire" : string.Join("/", names);
        }

        // ---------- 3. Shot & accuracy ----------

        public void DrawShot(SerializedObject data, int number)
        {
            var definition = Target(data);
            var summary = (definition.projectilesPerShot > 1 ? $"{definition.projectilesPerShot} × " : "") + $"{definition.damagePerProjectile:0.#} dmg · {definition.accuracyMinutesOfAngle:0.#} MOA";
            if (!Step("shot", number, "Shot & accuracy", StepState.Done, summary, "Projectiles, damage, barrel length and dispersion"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                Graph.Scale(data, "projectilesPerShot", "Projectiles per shot", "Bullets or pellets fired at once. For buckshot the cartridge describes one pellet.", ProjectileScale);
                Graph.Scale(data, "damagePerProjectile", "Damage per projectile", "Damage of a hit at full muzzle energy; it falls off with the energy the projectile keeps.", DamageScale);
                Graph.Scale(data, "muzzleVelocityMultiplier", "Barrel length", "Short barrels burn less powder: the projectile leaves slower than the cartridge table says; long barrels add speed.", BarrelScale);
                Graph.Scale(data, "accuracyMinutesOfAngle", "Accuracy (aimed)", "Diameter of the dispersion cone in minutes of angle when aiming. 1 MOA ≈ 2.9 cm at 100 m.", AccuracyScale);
                Graph.Scale(data, "hipFireSpreadMultiplier", "From the hip", "How much wider the cone is when firing without aiming.", HipScale);
                Graph.Scale(data, "movingSpreadMultiplier", "While moving", "Extra widening at full movement speed.", MovingScale);
                Graph.Scale(data, "heatSpreadMultiplier", "When hot", "Extra widening once the heat reaches the accuracy-penalty threshold.", HeatSpreadScale);
                DrawGroup(definition);
                var cartridge = definition.ammunition;
                if (cartridge)
                {
                    var speed = cartridge.muzzleVelocityMetersPerSecond * definition.muzzleVelocityMultiplier;
                    Graph.Hint($"The {(cartridge.IsRocket ? "rocket" : "bullet")} leaves at <b>{speed:0} m/s</b> "
                        + $"({cartridge.KineticEnergy(speed) * definition.projectilesPerShot:0} J per shot). Attachments multiply spread and speed on top.");
                }
                else
                {
                    Graph.Hint("Pick a cartridge in step 1 to see the muzzle velocity and energy.");
                }
            }
        }

        /// <summary>Where shots land at 100 m: aimed, from the hip and from the hip on the move, next to a torso for scale.</summary>
        private static void DrawGroup(ArmoryCoreWeaponDefinition definition)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(140f, 118f, GUILayout.ExpandWidth(true)));
            Graph.Panel(rect);
            const float MoaAt100 = 0.02909f;
            var aimed = definition.accuracyMinutesOfAngle * MoaAt100;
            var hip = aimed * definition.hipFireSpreadMultiplier;
            var moving = hip * definition.movingSpreadMultiplier;
            var area = new Rect(rect.x + 8f, rect.y + 20f, Mathf.Min(rect.height - 28f, rect.width * 0.5f), rect.height - 28f);
            var scale = area.height / Mathf.Max(0.7f, moving);
            var center = area.center;
            Graph.Disc(center, Mathf.Max(1.5f, moving * scale * 0.5f), new Color(Graph.Bad.r, Graph.Bad.g, Graph.Bad.b, 0.18f));
            Graph.Disc(center, Mathf.Max(1.5f, hip * scale * 0.5f), new Color(Graph.Warn.r, Graph.Warn.g, Graph.Warn.b, 0.3f));
            var torso = new Vector2(0.45f, 0.6f) * scale;
            Graph.Frame(new Rect(center.x - torso.x * 0.5f, center.y - torso.y * 0.5f, torso.x, torso.y), new Color(Graph.Ink.r, Graph.Ink.g, Graph.Ink.b, 0.5f));
            Graph.Disc(center, Mathf.Max(1.5f, aimed * scale * 0.5f), Graph.Good);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), "Group at 100 m, torso for scale", EditorStyles.miniBoldLabel);
            var text = new Rect(area.xMax + 12f, area.y + 4f, rect.xMax - area.xMax - 16f, 16f);
            Legend(ref text, Graph.Good, $"aimed  ⌀ {Size(aimed)}");
            Legend(ref text, Graph.Warn, $"from the hip  ⌀ {Size(hip)}");
            Legend(ref text, Graph.Bad, $"hip, moving  ⌀ {Size(moving)}");
            if (definition.projectilesPerShot > 1)
                Legend(ref text, Graph.Muted, $"{definition.projectilesPerShot} pellets per shot");
        }

        private static void Legend(ref Rect rect, Color color, string text)
        {
            Graph.Disc(new Vector2(rect.x + 5f, rect.center.y), 4f, color);
            GUI.Label(new Rect(rect.x + 14f, rect.y, rect.width - 14f, rect.height), text, EditorStyles.miniLabel);
            rect.y += 18f;
        }

        private static string Size(float meters) => meters < 1f ? $"{meters * 100f:0} cm" : $"{meters:0.#} m";

        // ---------- 4. Magazine & reload ----------

        public void DrawMagazine(SerializedObject data, int number)
        {
            var definition = Target(data);
            var reload = (ArmoryCoreReloadType)data.FindProperty("reloadType").enumValueIndex;
            var perRound = reload == ArmoryCoreReloadType.InternalMagazine || reload == ArmoryCoreReloadType.SingleRound;
            var summary = $"{definition.magazineCapacity}{(definition.chamberCapacity > 0 ? "+1" : "")} rounds · "
                + (perRound ? $"{definition.perRoundReloadTime:0.##} s per round" : $"{definition.tacticalReloadTime:0.#}/{definition.emptyReloadTime:0.#} s");
            if (!Step("magazine", number, "Magazine & reload", StepState.Done, summary, "Feed, capacity, the chamber and reload times"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                if (Graph.Choice(data.FindProperty("reloadType"), ReloadTiles, 2, 36f))
                    ArmoryCoreWeaponPresets.SyncMechanismTags(data);
                Graph.Scale(data, "magazineCapacity", reload == ArmoryCoreReloadType.BeltFed ? "Belt length" : "Capacity", "Rounds in a full magazine, tube or belt.", MagazineScale);
                var chamber = data.FindProperty("chamberCapacity");
                using (new EditorGUILayout.HorizontalScope())
                {
                    var chambered = chamber.intValue > 0;
                    var nextChambered = Graph.ToggleTile(chambered, new Graph.Tile("Chamber", "one more round", "The weapon holds a round in the chamber on top of the magazine (+1). Open-bolt weapons never keep one chambered.") { Glyph = "+1" }, 36f);
                    if (nextChambered != chambered)
                        chamber.intValue = nextChambered ? 1 : 0;
                    Graph.ToggleTile(data.FindProperty("spawnWithFullMagazine"), new Graph.Tile("Starts full", "full magazine", "The weapon spawns with a full magazine.") { Glyph = "▮", Color = Graph.Good }, 36f);
                    using (new EditorGUI.DisabledScope(chamber.intValue == 0))
                        Graph.ToggleTile(data.FindProperty("spawnWithChamberLoaded"), new Graph.Tile("Starts chambered", "ready to fire", "A round is already in the chamber when the weapon spawns.") { Glyph = "•", Color = Graph.Good }, 36f);
                }
                if (perRound)
                {
                    Graph.Scale(data, "perRoundReloadTime", "Per round", "Time to load one round.", PerRoundScale);
                }
                else
                {
                    Graph.Scale(data, "tacticalReloadTime", "Tactical reload", "Reload while rounds are left: the chambered round stays.", ReloadScale);
                    Graph.Scale(data, "emptyReloadTime", "Empty reload", "Reload of an empty weapon, including chambering the first round.", ReloadScale);
                }
                var rounds = definition.magazineCapacity + definition.chamberCapacity;
                var interval = 60f / Mathf.Max(1f, definition.roundsPerMinute);
                var refill = perRound ? definition.perRoundReloadTime * definition.magazineCapacity : definition.emptyReloadTime;
                var cycle = rounds * interval + refill;
                Graph.Hint($"<b>{rounds}</b> rounds empty in {Mathf.Max(0, rounds - 1) * interval:0.#} s; refilling takes {refill:0.#} s. "
                    + $"Sustained fire with reloads: <b>{rounds / Mathf.Max(0.01f, cycle) * 60f:0} rounds/min</b>.");
            }
        }

        // ---------- 5. Recoil ----------

        public void DrawRecoil(SerializedObject data, int number)
        {
            var definition = Target(data);
            var summary = $"{definition.verticalRecoilDegrees:0.##}° up · ±{definition.horizontalRecoilDegrees:0.##}°";
            if (!Step("recoil", number, "Recoil", StepState.Done, summary, "Kick per shot and how quickly the aim recovers"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                Graph.Scale(data, "verticalRecoilDegrees", "Vertical kick", "Degrees the aim climbs per shot.", VerticalScale);
                Graph.Scale(data, "horizontalRecoilDegrees", "Horizontal kick", "Largest sideways jump per shot, left or right at random.", HorizontalScale);
                Graph.Scale(data, "recoilRandomness", "Randomness", "How much the vertical kick varies from shot to shot.", RandomnessScale);
                Graph.Scale(data, "recoilRecoveryDegreesPerSecond", "Recovery speed", "How fast the aim settles back after the recovery delay.", RecoveryScale);
                Graph.Scale(data, "recoilRecoveryDelay", "Recovery delay", "Time after a shot before the aim starts to settle.", RecoveryDelayScale);
                DrawSpray(definition);
                var cartridge = definition.ammunition;
                if (cartridge && !cartridge.IsRocket)
                {
                    var speed = cartridge.muzzleVelocityMetersPerSecond * definition.muzzleVelocityMultiplier;
                    var impulse = cartridge.ProjectileMassKilograms * speed * definition.projectilesPerShot;
                    // Powder gas leaves the muzzle faster than the bullet: about half the bullet's momentum again.
                    var velocity = impulse * 1.5f / Mathf.Max(0.1f, definition.weaponMassKilograms);
                    var energy = 0.5f * definition.weaponMassKilograms * velocity * velocity;
                    Graph.Bar("Free recoil speed", velocity / 8f, velocity > 4f ? Graph.Bad : velocity > 2f ? Graph.Warn : Graph.Good, $"{velocity:0.0} m/s",
                        "Speed of the weapon pushed back by one shot, bullet and powder gas, without a shooter: a heavier weapon kicks slower.");
                    Graph.Bar("Recoil energy", energy / 40f, Graph.Warn, $"{energy:0.#} J", "Energy the shoulder or hands take per shot.");
                    Graph.Hint("Kick degrees are what the player feels; the bars are the physics of this cartridge and weight, to keep the two plausible together.");
                }
            }
        }

        /// <summary>Where a held trigger walks the aim over a full magazine, with the same kick and recovery rules as play.</summary>
        private static void DrawSpray(ArmoryCoreWeaponDefinition definition)
        {
            var rounds = Mathf.Clamp(definition.magazineCapacity + definition.chamberCapacity, 1, 100);
            var interval = 60f / Mathf.Max(1f, definition.roundsPerMinute);
            if (definition.action == ArmoryCoreWeaponAction.Manual || definition.action == ArmoryCoreWeaponAction.Pump || definition.action == ArmoryCoreWeaponAction.BreakAction)
                interval = Mathf.Max(interval, definition.actionCycleTime);
            var random = new System.Random(20261021);
            var offset = Vector2.zero;
            var points = new Vector2[rounds];
            var peak = 0f;
            var drift = 0f;
            for (var i = 0; i < rounds; i++)
            {
                if (i > 0 && interval > definition.recoilRecoveryDelay)
                    offset = Vector2.MoveTowards(offset, Vector2.zero, definition.recoilRecoveryDegreesPerSecond * (interval - definition.recoilRecoveryDelay));
                var vertical = definition.verticalRecoilDegrees * Mathf.Lerp(1f - definition.recoilRandomness, 1f + definition.recoilRandomness, (float)random.NextDouble());
                var horizontal = definition.horizontalRecoilDegrees * Mathf.Lerp(-1f, 1f, (float)random.NextDouble());
                offset += new Vector2(horizontal, vertical);
                points[i] = offset;
                peak = Mathf.Max(peak, offset.y);
                drift = Mathf.Max(drift, Mathf.Abs(offset.x));
            }

            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(140f, 150f, GUILayout.ExpandWidth(true)));
            Graph.Panel(rect);
            var plot = new Rect(rect.x + 10f, rect.y + 22f, rect.width - 20f, rect.height - 44f);
            var height = Mathf.Max(1f, peak * 1.1f);
            var width = Mathf.Max(0.5f, drift * 1.2f);
            var scale = Mathf.Min(plot.width / (2f * width), plot.height / height);
            var origin = new Vector2(plot.center.x, plot.yMax);
            var step = height > 8f ? 2f : height > 3f ? 1f : 0.5f;
            for (var degrees = step; degrees <= height; degrees += step)
                Graph.FillRect(new Rect(plot.x, origin.y - degrees * scale, plot.width, 1f), Graph.GridColor);
            Graph.FillRect(new Rect(origin.x, plot.y, 1f, plot.height), Graph.GridColor);
            var screen = new Vector2[rounds + 1];
            screen[0] = origin;
            for (var i = 0; i < rounds; i++)
                screen[i + 1] = origin + new Vector2(points[i].x, -points[i].y) * scale;
            Graph.Polyline(screen, new Color(Graph.Muted.r, Graph.Muted.g, Graph.Muted.b, 0.45f), 1f);
            for (var i = 0; i < rounds; i++)
                Graph.Disc(screen[i + 1], 3f, Color.Lerp(Graph.Warn, SprayColor, i / Mathf.Max(1f, rounds - 1f)));
            Graph.Disc(origin, 4f, Graph.Good);

            var auto = definition.Supports(ArmoryCoreFireMode.Automatic);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f),
                $"{(auto ? "Held trigger" : "Fastest trigger pulls")}: {rounds} shots at {60f / interval:0} rpm, without attachments", EditorStyles.miniBoldLabel);
            var settle = points[rounds - 1].magnitude / Mathf.Max(0.01f, definition.recoilRecoveryDegreesPerSecond) + definition.recoilRecoveryDelay;
            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width - 12f, 16f),
                $"climbs up to {peak:0.#}°  ·  drifts ±{drift:0.#}°  ·  settles back in {settle:0.#} s  ·  grid {step:0.#}°", EditorStyles.miniLabel);
        }

        // ---------- 6. Handling ----------

        public void DrawHandling(SerializedObject data, int number)
        {
            var definition = Target(data);
            if (!Step("handling", number, "Weight & aiming", StepState.Done, $"{definition.weaponMassKilograms:0.##} kg · aim {definition.aimDownSightsTime:0.00} s", "Mass of the bare weapon and time to aim down the sights"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                Graph.Scale(data, "weaponMassKilograms", "Mass", "Mass of the bare weapon; attachments add their own.", MassScale);
                Graph.Scale(data, "aimDownSightsTime", "Aim time", "Time to raise the sights to the eye; sights and stocks multiply it.", AimScale);
                Graph.Hint("Attachments add their mass and multiply aim and reload times; the effective values appear in the controller's live state in Play Mode.");
            }
        }

        // ---------- 7. Reliability & heat ----------

        public void DrawReliability(SerializedObject data, int number)
        {
            var definition = Target(data);
            var summary = $"1 jam / {definition.meanRoundsBetweenFailures:0} · heat {definition.heatPerShot:0.##}/shot";
            if (!Step("reliability", number, "Reliability & heat", StepState.Done, summary, "Stoppages, heat build-up, cook-off and wear"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                Graph.Scale(data, "meanRoundsBetweenFailures", "Rounds between stoppages", "Average rounds between malfunctions of a clean, cool weapon at full durability.", ReliabilityScale);
                Graph.Scale(data, "heatPerShot", "Heat per shot", "Heat added by every shot.", HeatPerShotScale);
                Graph.Scale(data, "heatDissipationPerSecond", "Cooling", "Heat lost per second.", CoolingScale);
                Graph.Scale(data, "heatForAccuracyPenalty", "Hot from", "Heat at which the spread reaches its full heat penalty and stoppages become three times as likely.", PenaltyHeatScale);
                Graph.Scale(data, "heatForCookOff", "Cook-off from", "Above this heat a chambered round may fire by itself.", CookOffScale);
                Graph.Scale(data, "durabilityLossPerShot", "Wear", "Durability lost per shot; a worn weapon jams more often.", WearScale);
                Graph.Scale(data, "foulingPerShot", "Fouling", "Dirt added per shot until the weapon is serviced; fouling makes stoppages more likely.", FoulingScale);
                DrawHeat(definition);
                var jam = Mathf.Max(1f, definition.meanRoundsBetweenFailures);
                Graph.Hint($"A clean, cool weapon jams about once in <b>{jam:0}</b> rounds; when hot once in <b>{jam / 3f:0}</b>.");
                if (definition.heatForCookOff <= definition.heatForAccuracyPenalty)
                    Graph.Line("Cook-off is not above the hot threshold: rounds may cook off before the weapon even counts as hot.", Graph.Warn);
            }
        }

        /// <summary>Heat over continuous fire with reloads between magazines, against the hot and cook-off thresholds.</summary>
        private static void DrawHeat(ArmoryCoreWeaponDefinition definition)
        {
            var rounds = Mathf.Max(1, definition.magazineCapacity + definition.chamberCapacity);
            var interval = 60f / Mathf.Max(1f, definition.roundsPerMinute);
            var perRound = definition.reloadType == ArmoryCoreReloadType.InternalMagazine || definition.reloadType == ArmoryCoreReloadType.SingleRound;
            var refill = perRound ? definition.perRoundReloadTime * definition.magazineCapacity : definition.emptyReloadTime;
            var duration = Mathf.Clamp(3f * (rounds * interval + refill), 5f, 60f);
            const int Samples = 200;
            var samples = new float[Samples];
            var heat = 0f;
            var fired = 0;
            var inMagazine = rounds;
            var nextShot = 0f;
            var hotAt = -1;
            var cookAt = -1;
            const float Tick = 0.005f;
            for (var t = 0f; t < duration; t += Tick)
            {
                heat = Mathf.Max(0f, heat - definition.heatDissipationPerSecond * Tick);
                if (t >= nextShot)
                {
                    heat += definition.heatPerShot;
                    fired++;
                    inMagazine--;
                    nextShot = t + interval;
                    if (inMagazine == 0)
                    {
                        nextShot += refill;
                        inMagazine = rounds;
                    }
                    if (hotAt < 0 && heat >= definition.heatForAccuracyPenalty)
                        hotAt = fired;
                    if (cookAt < 0 && heat >= definition.heatForCookOff)
                        cookAt = fired;
                }
                var index = Mathf.Min(Samples - 1, (int)(t / duration * Samples));
                samples[index] = Mathf.Max(samples[index], heat);
            }

            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(140f, 120f, GUILayout.ExpandWidth(true)));
            Graph.Panel(rect);
            var plot = new Rect(rect.x + 8f, rect.y + 20f, rect.width - 16f, rect.height - 42f);
            var top = Mathf.Max(definition.heatForCookOff * 1.1f, samples.Max() * 1.05f, 1f);
            float Y(float value) => Mathf.Lerp(plot.yMax, plot.y, Mathf.Clamp01(value / top));
            Graph.FillRect(new Rect(plot.x, Y(definition.heatForAccuracyPenalty), plot.width, 1f), Graph.Warn);
            Graph.FillRect(new Rect(plot.x, Y(definition.heatForCookOff), plot.width, 1f), Graph.Bad);
            GUI.Label(new Rect(plot.xMax - 80f, Y(definition.heatForAccuracyPenalty) - 14f, 80f, 14f), "hot", RightHint);
            GUI.Label(new Rect(plot.xMax - 80f, Y(definition.heatForCookOff) - 14f, 80f, 14f), "cook-off", RightHint);
            var line = new Vector2[Samples];
            for (var i = 0; i < Samples; i++)
                line[i] = new Vector2(plot.x + plot.width * i / (Samples - 1f), Y(samples[i]));
            Graph.Polyline(line, HeatColor, 1.5f);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), $"Heat in {duration:0} s of continuous fire with reloads", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width - 12f, 16f),
                (hotAt > 0 ? $"hot after {hotAt} rounds" : "never gets hot") + "  ·  " + (cookAt > 0 ? $"cook-off after {cookAt} rounds" : "no cook-off"), EditorStyles.miniLabel);
        }

        private static GUIStyle hintRight;

        private static GUIStyle RightHint
        {
            get
            {
                if (hintRight != null)
                    return hintRight;
                hintRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerRight };
                hintRight.normal.textColor = Graph.Muted;
                return hintRight;
            }
        }

        // ---------- 8. Sound ----------

        public void DrawSound(SerializedObject data, int number)
        {
            var definition = Target(data);
            var clips = new List<string>();
            if (definition.shotSound)
                clips.Add("shot");
            if (definition.suppressedShotSound)
                clips.Add("suppressed");
            if (definition.reloadSound)
                clips.Add("reload");
            if (definition.emptySound)
                clips.Add("empty");
            var state = definition.shotSound ? StepState.Done : StepState.Current;
            if (!Step("sound", number, "Sound", state, clips.Count == 0 ? "silent" : string.Join(" · ", clips), "Clips, their mix and how far they carry"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                Clip(data, "shotSound", "Shot", "Played on every shot.");
                Clip(data, "suppressedShotSound", "Suppressed shot", "Played instead of the shot when a suppressor is installed.");
                Clip(data, "reloadSound", "Reload", "Played when a reload starts.");
                Clip(data, "emptySound", "Empty trigger", "Played when the trigger is pulled with nothing to fire.");
                Graph.Scale(data, "shotVolume", "Shot volume", null, VolumeScale);
                Graph.Scale(data, "reloadVolume", "Reload volume", null, VolumeScale);
                Graph.Scale(data, "emptyVolume", "Empty-trigger volume", null, VolumeScale);
                Graph.Scale(data, "shotPitchVariation", "Pitch variation", "Random pitch change per shot, so a held trigger does not sound like a loop.", PitchScale);
                Graph.Scale(data, "audioMinDistanceMeters", "Full volume within", "Distance up to which the shot plays at full volume.", NearScale);
                Graph.Scale(data, "audioMaxDistanceMeters", "Audible up to", "Distance at which the shot fades out.", FarScale);
            }
        }

        private static void Clip(SerializedObject data, string name, string label, string tooltip)
        {
            var property = data.FindProperty(name);
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
        }

        // ---------- 9. Identity & attachments ----------

        public void DrawIdentity(SerializedObject data, int number)
        {
            var definition = Target(data);
            var allowed = definition.compatibleAttachmentIds == null ? 0 : definition.compatibleAttachmentIds.Length;
            var named = !string.IsNullOrWhiteSpace(definition.id) && !string.IsNullOrWhiteSpace(definition.displayName);
            var summary = (named ? definition.id : "no ID") + " · " + (allowed == 0 ? "any matching attachment" : $"{allowed} attachments");
            if (!Step("identity", number, "Identity & attachments", named ? StepState.Done : StepState.Current, summary, "IDs, tags and which attachments fit"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                EditorGUILayout.PropertyField(data.FindProperty("id"), new GUIContent("Weapon ID", "A stable unique identifier used by save data and compatibility rules."));
                EditorGUILayout.PropertyField(data.FindProperty("displayName"), new GUIContent("Display name", "The player-facing weapon name."));
                if ((!named) && ArmoryCoreSetupGUI.Button("Fill the empty ID and name from the asset name"))
                {
                    if (string.IsNullOrWhiteSpace(data.FindProperty("id").stringValue))
                        data.FindProperty("id").stringValue = definition.name;
                    if (string.IsNullOrWhiteSpace(data.FindProperty("displayName").stringValue))
                        data.FindProperty("displayName").stringValue = ObjectNames.NicifyVariableName(definition.name);
                }
                EditorGUILayout.PropertyField(data.FindProperty("weaponTags"), new GUIContent("Tags", "Attachments list the tags a weapon needs to accept them."), true);
                DrawAttachments(data, definition);
            }
        }

        private void DrawAttachments(SerializedObject data, ArmoryCoreWeaponDefinition definition)
        {
            var list = data.FindProperty("compatibleAttachmentIds");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < list.arraySize; i++)
                ids.Add(list.GetArrayElementAtIndex(i).stringValue);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Attachments", "Only attachments switched on here can be installed. With none switched on, every attachment whose tags match fits."), EditorStyles.miniBoldLabel);
                using (new EditorGUI.DisabledScope(list.arraySize == 0))
                {
                    if (ArmoryCoreSetupGUI.MiniButton("Allow any", "Clear the list: every attachment whose tags match the weapon fits", 70f))
                        list.arraySize = 0;
                }
            }
            if (Attachments.Count == 0)
            {
                Graph.Hint("The project has no attachment definitions.");
                return;
            }
            Graph.Hint(list.arraySize == 0
                ? "The list is empty, so <b>every attachment whose tags match</b> fits. Switch some on to allow only those."
                : $"Only the <b>{list.arraySize}</b> switched-on attachments fit, if their tags match.");
            var tags = definition.weaponTags ?? Array.Empty<string>();
            foreach (var group in Attachments.GroupBy(attachment => attachment.slot))
            {
                var members = group.ToList();
                var on = members.Count(attachment => ids.Contains(attachment.id));
                var key = Key + ".attachments." + group.Key;
                var open = ArmoryCoreSetupGUI.Expanded(key, false);
                var next = EditorGUILayout.Foldout(open, $"{ObjectNames.NicifyVariableName(group.Key.ToString())}  ·  {(list.arraySize == 0 ? "any" : on + " of " + members.Count)}", true);
                if (next != open)
                    ArmoryCoreSetupGUI.SetExpanded(key, next);
                if (!next)
                    continue;
                for (var start = 0; start < members.Count; start += 2)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (var index = start; index < start + 2; index++)
                        {
                            if (index >= members.Count)
                            {
                                GUILayoutUtility.GetRect(40f, 26f, GUILayout.ExpandWidth(true));
                                continue;
                            }
                            var attachment = members[index];
                            var missing = (attachment.requiredWeaponTags ?? Array.Empty<string>())
                                .Where(tag => !tags.Any(owned => string.Equals(owned, tag, StringComparison.OrdinalIgnoreCase)))
                                .ToArray();
                            var allowedHere = ids.Contains(attachment.id);
                            var tile = new Graph.Tile(Name(attachment), null, missing.Length == 0
                                ? $"{attachment.id} · {attachment.massKilograms:0.###} kg"
                                : $"{attachment.id}: the weapon lacks the tag(s) {string.Join(", ", missing)}, so it never fits.")
                            {
                                Color = missing.Length == 0 ? Graph.Accent : Graph.Muted,
                                Preview = ArmoryCoreHoverPreview.PrefabOf(attachment)
                            };
                            using (new EditorGUI.DisabledScope(missing.Length > 0 && !allowedHere))
                            {
                                if (Graph.ToggleTile(allowedHere, tile, 26f) != allowedHere)
                                    SetAllowed(list, attachment.id, !allowedHere);
                            }
                        }
                    }
                }
            }
            ArmoryCoreHoverPreview.Draw();
        }

        private static void SetAllowed(SerializedProperty list, string id, bool allowed)
        {
            for (var i = list.arraySize - 1; i >= 0; i--)
            {
                if (string.Equals(list.GetArrayElementAtIndex(i).stringValue, id, StringComparison.OrdinalIgnoreCase))
                    list.DeleteArrayElementAtIndex(i);
            }
            if (!allowed)
                return;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = id;
        }

        // ---------- Diagnostics ----------

        public static List<string> Validate(ArmoryCoreWeaponDefinition definition)
        {
            var issues = new List<string>();
            if (definition == null)
                return issues;
            if (definition.ammunition == null)
                issues.Add("No cartridge: pick one in «Type & cartridge».");
            if (definition.fireModes == null || !definition.fireModes.Any(mode => mode != ArmoryCoreFireMode.Safe))
                issues.Add("No fire mode that shoots: switch on Semi, Burst or Auto.");
            else if (definition.fireModes.Distinct().Count() != definition.fireModes.Length)
                issues.Add("Fire modes contain duplicates.");
            if (string.IsNullOrWhiteSpace(definition.id))
                issues.Add("The weapon ID is empty.");
            if (string.IsNullOrWhiteSpace(definition.displayName))
                issues.Add("The display name is empty.");
            if (definition.spawnWithChamberLoaded && definition.chamberCapacity == 0)
                issues.Add("«Starts chambered» has no effect without a chamber.");
            if (definition.heatForCookOff <= definition.heatForAccuracyPenalty)
                issues.Add("Cook-off heat should be above the hot threshold.");
            if (definition.shotSound == null)
                issues.Add("No shot sound: the weapon fires silently.");
            return issues;
        }

        public void Dispose()
        {
            picker.Dispose();
        }
    }
}
