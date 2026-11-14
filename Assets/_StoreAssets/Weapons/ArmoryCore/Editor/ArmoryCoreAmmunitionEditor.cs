using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArmoryCore;
using UnityEditor;
using UnityEngine;
using Graph = ArmoryCoreEditor.ArmoryCoreGraph;

namespace ArmoryCoreEditor
{
    /// <summary>Real cartridges to start from, and the project's own cartridges, found once per project change.</summary>
    internal static class ArmoryCoreCartridgeLibrary
    {
        public sealed class Template
        {
            public readonly string Title;
            public readonly string File;
            public readonly float Grams;
            public readonly float Millimeters;
            public readonly float Length;
            public readonly ArmoryCoreBulletNose Nose;
            public readonly float Velocity;
            public readonly float Drag;
            public readonly float Penetration;

            public Template(string title, string file, float grams, float millimeters, float length, ArmoryCoreBulletNose nose, float velocity, float drag, float penetration)
            {
                Title = title;
                File = file;
                Grams = grams;
                Millimeters = millimeters;
                Length = length;
                Nose = nose;
                Velocity = velocity;
                Drag = drag;
                Penetration = penetration;
            }
        }

        // Typical factory loads; drag coefficients chosen so the velocity at 100 m matches published tables.
        public static readonly Template[] Templates =
        {
            new Template(".22 LR, lead round nose", ".22 LR", 2.6f, 5.7f, 11f, ArmoryCoreBulletNose.RoundNose, 330f, 0.30f, 0.4f),
            new Template("9x19 mm Parabellum FMJ", "9x19 FMJ", 8.04f, 9.01f, 15.5f, ArmoryCoreBulletNose.RoundNose, 360f, 0.36f, 1f),
            new Template("9x19 mm JHP", "9x19 JHP", 8.04f, 9.01f, 15f, ArmoryCoreBulletNose.HollowPoint, 360f, 0.38f, 0.6f),
            new Template(".45 ACP FMJ", ".45 ACP", 14.9f, 11.48f, 17f, ArmoryCoreBulletNose.RoundNose, 255f, 0.30f, 0.9f),
            new Template("5.45x39 mm 7N6, steel core", "5.45x39 7N6", 3.42f, 5.6f, 25.5f, ArmoryCoreBulletNose.Spitzer, 880f, 0.26f, 1.4f),
            new Template("5.56x45 mm NATO M855", "5.56x45 M855", 4f, 5.7f, 23f, ArmoryCoreBulletNose.Spitzer, 940f, 0.27f, 1.4f),
            new Template("7.62x39 mm PS, steel core", "7.62x39 PS", 7.9f, 7.91f, 26.8f, ArmoryCoreBulletNose.Spitzer, 715f, 0.30f, 1.4f),
            new Template("7.62x54R LPS", "7.62x54R LPS", 9.6f, 7.92f, 32f, ArmoryCoreBulletNose.Spitzer, 830f, 0.27f, 1.5f),
            new Template("12.7x108 mm B-32, armour piercing", "12.7x108 B-32", 48.2f, 12.98f, 62f, ArmoryCoreBulletNose.Spitzer, 840f, 0.25f, 2.5f),
            new Template("12 gauge Brenneke slug", "12 gauge slug", 31.5f, 18.5f, 20f, ArmoryCoreBulletNose.FlatNose, 430f, 0.60f, 0.8f),
        };

        private const string DefaultFolder = "Assets/ArmoryCore/Definitions/Ammunition";
        private static List<ArmoryCoreAmmunitionDefinition> cartridges;
        private static List<ArmoryCoreWeaponDefinition> weapons;

        static ArmoryCoreCartridgeLibrary()
        {
            EditorApplication.projectChanged += Forget;
        }

        public static void Forget()
        {
            cartridges = null;
            weapons = null;
        }

        /// <summary>Every cartridge of the project: bullets by calibre and weight, then rockets.</summary>
        public static List<ArmoryCoreAmmunitionDefinition> All => cartridges ??= Load<ArmoryCoreAmmunitionDefinition>()
            .OrderBy(a => a.IsRocket)
            .ThenBy(a => a.projectileDiameterMillimeters)
            .ThenBy(a => a.projectileMassGrams)
            .ToList();

        private static List<ArmoryCoreWeaponDefinition> Weapons => weapons ??= Load<ArmoryCoreWeaponDefinition>();

        private static List<T> Load<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset)
                .ToList();
        }

        public static int WeaponsUsing(ArmoryCoreAmmunitionDefinition ammunition)
        {
            return Weapons.Count(weapon => weapon && weapon.ammunition == ammunition);
        }

        public static string Name(ArmoryCoreAmmunitionDefinition ammunition)
        {
            return string.IsNullOrWhiteSpace(ammunition.displayName) ? ammunition.name : ammunition.displayName;
        }

        public static string Summary(ArmoryCoreAmmunitionDefinition ammunition)
        {
            return ammunition.IsRocket
                ? $"⌀ {ammunition.projectileDiameterMillimeters:0} mm · {ammunition.projectileMassGrams * 0.001f:0.##} kg · {ammunition.muzzleVelocityMetersPerSecond:0} m/s"
                : $"⌀ {ammunition.projectileDiameterMillimeters:0.##} mm · {ammunition.projectileMassGrams:0.#} g · {ammunition.muzzleVelocityMetersPerSecond:0} m/s · "
                  + $"{ammunition.KineticEnergy(ammunition.muzzleVelocityMetersPerSecond):0} J";
        }

        public static bool Matches(ArmoryCoreAmmunitionDefinition ammunition, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;
            var text = (Name(ammunition) + " " + ammunition.id + " " + ammunition.name).ToLowerInvariant();
            foreach (var word in search.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!text.Contains(word))
                    return false;
            }
            return true;
        }

        public static string FolderOf(ArmoryCoreAmmunitionDefinition ammunition)
        {
            var path = ammunition ? AssetDatabase.GetAssetPath(ammunition) : null;
            if (!string.IsNullOrEmpty(path))
                return Path.GetDirectoryName(path).Replace('\\', '/');
            return AssetDatabase.IsValidFolder(DefaultFolder) ? DefaultFolder : "Assets";
        }

        /// <summary>Saves a new cartridge; <paramref name="source"/> is an existing cartridge to copy, otherwise the template or the defaults are used.</summary>
        public static ArmoryCoreAmmunitionDefinition Create(Template template, ArmoryCoreAmmunitionDefinition source, string folder)
        {
            var fileName = template != null ? template.File : source ? source.name + " copy" : "Ammunition";
            var path = EditorUtility.SaveFilePanelInProject(
                "New cartridge",
                fileName,
                "asset",
                "Save the cartridge in the project.",
                folder);
            if (string.IsNullOrEmpty(path))
                return null;

            var ammunition = source ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<ArmoryCoreAmmunitionDefinition>();
            ammunition.id = Path.GetFileNameWithoutExtension(path);
            if (template != null)
            {
                ammunition.displayName = template.Title;
                ammunition.projectileType = ArmoryCoreProjectileType.Bullet;
                ammunition.projectileMassGrams = template.Grams;
                ammunition.projectileDiameterMillimeters = template.Millimeters;
                ammunition.bulletLengthMillimeters = template.Length;
                ammunition.noseShape = template.Nose;
                ammunition.muzzleVelocityMetersPerSecond = template.Velocity;
                ammunition.aerodynamicDragCoefficient = template.Drag;
                ammunition.penetrationMultiplier = template.Penetration;
                ammunition.gravityMultiplier = 1f;
                ammunition.maximumRangeMeters = template.Velocity < 500f ? 450f : template.Grams > 30f ? 2000f : 1200f;
                ammunition.maximumFlightTime = 2.5f * ammunition.maximumRangeMeters / template.Velocity;
            }
            else
            {
                ammunition.displayName = source ? Name(source) + " (copy)" : ObjectNames.NicifyVariableName(ammunition.id);
            }

            AssetDatabase.CreateAsset(ammunition, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            Forget();
            EditorGUIUtility.PingObject(ammunition);
            return ammunition;
        }
    }

    /// <summary>Flight of a cartridge sampled at even distances, integrated the same way as the runtime projectiles.</summary>
    internal static class ArmoryCoreFlight
    {
        public struct Sample
        {
            public float Distance;
            public float Speed;
            public float Drop;
            public float Time;
        }

        /// <summary>Level shot in still air; the result ends early when the projectile stops, times out or falls below its lethal speed.</summary>
        public static Sample[] Simulate(ArmoryCoreAmmunitionDefinition ammunition, float launchSpeed, float range, int count = 61)
        {
            var samples = new Sample[count];
            var filled = 1;
            samples[0] = new Sample { Speed = launchSpeed };
            var position = Vector2.zero;
            var velocity = new Vector2(Mathf.Max(1f, launchSpeed), 0f);
            var gravity = Mathf.Abs(Physics.gravity.y) * ammunition.gravityMultiplier;
            var air = ArmoryCoreBallisticsEnvironment.AirDensity;
            var dragFactor = 0.5f * air * ammunition.aerodynamicDragCoefficient * ammunition.CrossSectionSquareMeters
                / ammunition.ProjectileMassKilograms;
            var step = Mathf.Max(0.001f, ammunition.maximumFlightTime / 4000f);
            var time = 0f;
            var maximumSpeed = ammunition.MaximumSpeedMetersPerSecond;

            while (filled < count && time < ammunition.maximumFlightTime)
            {
                var speed = velocity.magnitude;
                var forward = speed > 0.0001f ? velocity / speed : Vector2.right;
                if (ammunition.IsRocket
                    && ammunition.motorAccelerationMetersPerSecondSquared > 0f
                    && time >= ammunition.motorIgnitionDelay
                    && time < ammunition.motorIgnitionDelay + ammunition.motorBurnTime)
                {
                    velocity += forward * ammunition.motorAccelerationMetersPerSecondSquared * step;
                    if (velocity.sqrMagnitude > maximumSpeed * maximumSpeed)
                        velocity = velocity.normalized * maximumSpeed;
                    speed = velocity.magnitude;
                }
                velocity -= forward * Mathf.Min(dragFactor * speed * speed * step, speed);
                velocity.y -= gravity * step;

                var previous = position;
                position += velocity * step;
                time += step;
                while (filled < count && position.x >= range * filled / (count - 1))
                {
                    var target = range * filled / (count - 1);
                    var share = Mathf.InverseLerp(previous.x, position.x, target);
                    samples[filled++] = new Sample
                    {
                        Distance = target,
                        Speed = velocity.magnitude,
                        Drop = -Mathf.Lerp(previous.y, position.y, share),
                        Time = time - step * (1f - share)
                    };
                }
                if (position.x >= ammunition.maximumRangeMeters
                    || !ammunition.IsRocket && velocity.magnitude < ammunition.minimumLethalVelocity
                    || velocity.x <= 0f)
                    break;
            }
            Array.Resize(ref samples, filled);
            return samples;
        }

        /// <summary>The sample at <paramref name="distance"/>, or false when the projectile does not get that far.</summary>
        public static bool At(Sample[] samples, float distance, out Sample sample)
        {
            sample = default;
            for (var i = 1; i < samples.Length; i++)
            {
                if (samples[i].Distance < distance - 0.01f)
                    continue;
                var a = samples[i - 1];
                var b = samples[i];
                var share = Mathf.InverseLerp(a.Distance, b.Distance, distance);
                sample = new Sample
                {
                    Distance = distance,
                    Speed = Mathf.Lerp(a.Speed, b.Speed, share),
                    Drop = Mathf.Lerp(a.Drop, b.Drop, share),
                    Time = Mathf.Lerp(a.Time, b.Time, share)
                };
                return true;
            }
            return false;
        }
    }

    /// <summary>Graphical editor of a cartridge: silhouette, scales with real cartridges as marks, flight curve and terminal effect.</summary>
    [CustomEditor(typeof(ArmoryCoreAmmunitionDefinition))]
    [CanEditMultipleObjects]
    public sealed class ArmoryCoreAmmunitionDefinitionEditor : Editor
    {
        private static readonly Color Lead = new Color(0.62f, 0.60f, 0.58f);
        private static readonly Color Copper = new Color(0.85f, 0.52f, 0.30f);
        private static readonly Color Olive = new Color(0.42f, 0.48f, 0.32f);
        private static readonly Color SpeedColor = new Color(0.3f, 0.7f, 1f);
        private static readonly Color DropColor = new Color(1f, 0.55f, 0.25f);

        // Bullets
        private static readonly Graph.Spec MassScale = new Graph.Spec(1.5f, 60f, "g", 1f, "0.##", true).Captions("light", "heavy").Limits(0.01f)
            .With(Graph.M(4f, "5.56"), Graph.M(8f, "9x19"), Graph.M(15f, ".45"), Graph.M(42f, ".50 BMG"));
        private static readonly Graph.Spec CalibreScale = new Graph.Spec(4f, 20f, "mm", 1f, "0.##", true).Captions("small", "large").Limits(0.01f)
            .With(Graph.M(5.7f, "5.56"), Graph.M(7.92f, "7.62"), Graph.M(9.03f, "9"), Graph.M(11.5f, ".45"), Graph.M(12.95f, "12.7"), Graph.M(18.5f, "12 ga"));
        private static readonly Graph.Spec LengthScale = new Graph.Spec(5f, 70f, "mm", 1f, "0.#", true).Captions("short", "long").Limits(0.5f)
            .With(Graph.M(15.5f, "9x19"), Graph.M(23f, "5.56"), Graph.M(32f, "7.62x54R"), Graph.M(62f, "12.7x108"));
        private static readonly Graph.Spec VelocityScale = new Graph.Spec(150f, 1200f, "m/s", 1f, "0").Captions("subsonic", "rifle").Colors(Graph.Muted, Graph.Bad).Limits(1f)
            .With(Graph.M(265f, ".45"), Graph.M(370f, "9x19"), Graph.M(715f, "7.62x39"), Graph.M(900f, "5.56"));
        private static readonly Graph.Spec DragScale = new Graph.Spec(0.15f, 0.65f, "", 1f, "0.00").Captions("streamlined", "blunt").Colors(Graph.Good, Graph.Warn).Limits(0.01f, 2f)
            .With(Graph.M(0.29f, "spitzer"), Graph.M(0.36f, "round nose"), Graph.M(0.47f, "shot"));
        private static readonly Graph.Spec RangeScale = new Graph.Spec(50f, 3000f, "m", 1f, "0", true).Captions("close", "long").Limits(1f)
            .With(Graph.M(450f, "pistol"), Graph.M(1200f, "rifle"), Graph.M(2000f, "heavy MG"));
        private static readonly Graph.Spec LethalScale = new Graph.Spec(10f, 300f, "m/s", 1f, "0", true).Captions("keeps hitting", "stops early").Limits(0.01f);
        private static readonly Graph.Spec RetainedScale = new Graph.Spec(0f, 1f, "%", 100f, "0").Captions("falls off", "keeps damage").Colors(Graph.Muted, Graph.Bad)
            .With(Graph.M(0.12f, "standard"));
        private static readonly Graph.Spec PenetrationScale = new Graph.Spec(0.2f, 4f, "×", 1f, "0.##", true).Captions("soft", "armour piercing").Colors(Lead, new Color(0.55f, 0.75f, 1f))
            .Limits(0f).With(Graph.M(0.5f, "lead"), Graph.M(1f, "FMJ"), Graph.M(1.5f, "steel core"), Graph.M(2.5f, "AP"));
        private static readonly Graph.Spec RicochetScale = new Graph.Spec(0f, 1f, "%", 100f, "0").Captions("stops", "keeps energy").With(Graph.M(0.55f, "standard"));

        // Shared
        private static readonly Graph.Spec GravityScale = new Graph.Spec(0f, 2f, "×", 1f, "0.##").Captions("none", "heavy").Colors(Graph.Muted, DropColor)
            .With(Graph.M(0f, "off"), Graph.M(0.3f, "rocket"), Graph.M(1f, "real"));
        private static readonly Graph.Spec FlightTimeScale = new Graph.Spec(0.5f, 20f, "s", 1f, "0.##", true).Captions("short", "long").Limits(0.01f);
        private static readonly Graph.Spec SegmentScale = new Graph.Spec(0.1f, 10f, "m", 1f, "0.##", true).Captions("precise", "fast").Colors(Graph.Good, Graph.Warn).Limits(0.01f)
            .With(Graph.M(2f, "default"));
        private static readonly Graph.Spec SubstepScale = new Graph.Spec(1f, 64f, "", 1f, "0").Captions("cheap", "precise").Colors(Graph.Warn, Graph.Good).Limits(1f)
            .With(Graph.M(24f, "default"));

        // Rockets
        private static readonly Graph.Spec RocketMassScale = new Graph.Spec(300f, 15000f, "kg", 0.001f, "0.##", true).Captions("light", "heavy").Limits(0.01f)
            .With(Graph.M(1400f, "RG18"), Graph.M(2250f, "RG7"), Graph.M(8500f, "RG28"));
        private static readonly Graph.Spec RocketCalibreScale = new Graph.Spec(30f, 160f, "mm", 1f, "0", true).Captions("small", "large").Limits(1f)
            .With(Graph.M(64f, "RG18"), Graph.M(84f, "RG84"), Graph.M(125f, "RG28"));
        private static readonly Graph.Spec RocketLengthScale = new Graph.Spec(0.2f, 2f, "m", 1f, "0.##").Captions("short", "long").Limits(0.05f).With(Graph.M(0.9f, "default"));
        private static readonly Graph.Spec LaunchScale = new Graph.Spec(30f, 400f, "m/s", 1f, "0").Captions("slow", "fast").Colors(Graph.Muted, Graph.Bad).Limits(1f)
            .With(Graph.M(115f, "RG7"), Graph.M(200f, "RG84"));
        private static readonly Graph.Spec MotorScale = new Graph.Spec(0f, 400f, "m/s²", 1f, "0").Captions("no motor", "strong").Colors(Graph.Muted, Graph.Accent).With(Graph.M(180f, "default"));
        private static readonly Graph.Spec IgnitionScale = new Graph.Spec(0f, 0.5f, "s", 1f, "0.##").Captions("at once", "late").Limits(0f).With(Graph.M(0.08f, "default"));
        private static readonly Graph.Spec BurnScale = new Graph.Spec(0f, 3f, "s", 1f, "0.##").Captions("short", "long").Limits(0f).With(Graph.M(1f, "default"));
        private static readonly Graph.Spec TopSpeedScale = new Graph.Spec(50f, 600f, "m/s", 1f, "0").Captions("slow", "fast").Colors(Graph.Muted, Graph.Bad).Limits(1f)
            .With(Graph.M(295f, "default"));
        private static readonly Graph.Spec ArmingScale = new Graph.Spec(0f, 30f, "m", 1f, "0.#").Captions("at once", "far").Limits(0f).With(Graph.M(5f, "default"));
        private static readonly Graph.Spec BlastRadiusScale = new Graph.Spec(0.5f, 20f, "m", 1f, "0.#", true).Captions("small", "large").Colors(Graph.Warn, Graph.Bad).Limits(0.1f)
            .With(Graph.M(5f, "default"));
        private static readonly Graph.Spec BlastDamageScale = new Graph.Spec(0f, 1000f, "", 1f, "0").Captions("weak", "strong").Colors(Graph.Warn, Graph.Bad).Limits(0f)
            .With(Graph.M(250f, "default"));
        private static readonly Graph.Spec EdgeScale = new Graph.Spec(0f, 1f, "%", 100f, "0").Captions("only centre", "even").With(Graph.M(0.1f, "default"));
        private static readonly Graph.Spec ForceScale = new Graph.Spec(0f, 3000f, "N·s", 1f, "0").Captions("none", "strong").Limits(0f).With(Graph.M(600f, "default"));
        private static readonly Graph.Spec SmokeScale = new Graph.Spec(0f, 5f, "s", 1f, "0.#").Captions("none", "long").Limits(0f).With(Graph.M(1.6f, "default"));

        private static Graph.Tile[] typeTiles;
        private static Graph.Tile[] noseTiles;

        /// <summary>Inside another inspector: no title, diagnostics or identity tools of its own.</summary>
        public bool Embedded;
        /// <summary>Barrel multiplier of the weapon that fires the cartridge, applied to the flight preview.</summary>
        public float VelocityMultiplier = 1f;

        private bool showIdentity = true;
        private bool showProjectile = true;
        private bool showFlight = true;
        private bool showTerminal = true;
        private bool showRocketMotor = true;
        private bool showWarhead = true;
        private bool showSimulation;
        private bool showEffect = true;

        private static Graph.Tile[] TypeTiles => typeTiles ??= new[]
        {
            new Graph.Tile("Bullet", "inert, ballistic", "Simulated as an inert ballistic projectile with drag, gravity, penetration and ricochet.")
                { Icon = (r, c) => DrawBullet(r, ArmoryCoreBulletNose.Spitzer, 0.35f, c) },
            new Graph.Tile("Rocket", "motor and warhead", "Visible, motor-driven projectile that explodes on impact.")
                { Icon = (r, c) => DrawRocket(r, c) },
        };

        private static Graph.Tile[] NoseTiles => noseTiles ??= new[]
        {
            new Graph.Tile("Round nose", "pistol", "Rounded jacketed nose: moderate drag, typical of pistol bullets.")
                { Icon = (r, c) => DrawBullet(r, ArmoryCoreBulletNose.RoundNose, 0.45f, c) },
            new Graph.Tile("Spitzer", "rifle", "Long pointed nose: the least drag, typical of rifle bullets.")
                { Icon = (r, c) => DrawBullet(r, ArmoryCoreBulletNose.Spitzer, 0.3f, c) },
            new Graph.Tile("Hollow point", "expanding", "Cavity in the nose: expands in soft targets, usually paired with lower penetration.")
                { Icon = (r, c) => DrawBullet(r, ArmoryCoreBulletNose.HollowPoint, 0.45f, c) },
            new Graph.Tile("Flat nose", "slug, wadcutter", "Flat meplat: more drag, typical of slugs and cast bullets.")
                { Icon = (r, c) => DrawBullet(r, ArmoryCoreBulletNose.FlatNose, 0.45f, c) },
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (serializedObject.isEditingMultipleObjects)
            {
                DrawMultipleObjects();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var definition = (ArmoryCoreAmmunitionDefinition)target;
            if (!Embedded)
            {
                ArmoryCoreDefinitionEditorUtility.DrawTitle(
                    ArmoryCoreCartridgeLibrary.Name(definition),
                    (definition.IsRocket ? "Rocket · " : "Cartridge · ") + ArmoryCoreCartridgeLibrary.Summary(definition));
                DrawDiagnostics(definition);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(definition.IsRocket ? "ROCKET" : "BULLET", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                Graph.NumbersToggle();
            }
            DrawSilhouette(definition);

            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                DrawIdentity();
                DrawProjectile(definition);
                DrawFlight(definition);
                if (definition.IsRocket)
                {
                    DrawRocketMotor();
                    DrawWarhead();
                }
                else
                {
                    DrawTerminal();
                }
                DrawSimulation();
                DrawEffect(definition);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ---------- Sections ----------

        private void DrawIdentity()
        {
            if (Section(ref showIdentity, "Identity", "Stable IDs are used by compatibility checks; the display name is safe to change."))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("id"), new GUIContent("Cartridge ID", "A stable unique identifier."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Display Name", "The player-facing cartridge name."));
                EditorGUILayout.LabelField("Projectile", EditorStyles.miniBoldLabel);
                Graph.Choice(serializedObject.FindProperty("projectileType"), TypeTiles, 2, 40f);
                if (!Embedded)
                    ArmoryCoreDefinitionEditorUtility.DrawIdentityTools(targets);
            }
            EndSection(showIdentity);
        }

        private void DrawProjectile(ArmoryCoreAmmunitionDefinition definition)
        {
            var summary = definition.IsRocket
                ? $"{definition.projectileMassGrams * 0.001f:0.##} kg · ⌀ {definition.projectileDiameterMillimeters:0} mm"
                : $"{definition.projectileMassGrams:0.##} g · ⌀ {definition.projectileDiameterMillimeters:0.##} mm";
            if (Section(ref showProjectile, definition.IsRocket ? "Rocket" : "Bullet", summary))
            {
                if (definition.IsRocket)
                {
                    Graph.Scale(serializedObject, "projectileMassGrams", "Rocket mass", "Mass of the rocket in flight. With the speed it gives the energy and the drag loss.", RocketMassScale);
                    Graph.Scale(serializedObject, "projectileDiameterMillimeters", "Calibre", "Body diameter: its frontal area sets the air drag.", RocketCalibreScale);
                    Graph.Scale(serializedObject, "rocketLengthMeters", "Rocket length", "Length of the visible rocket.", RocketLengthScale);
                    Graph.Scale(serializedObject, "smokeTrailTime", "Smoke trail", "How long the smoke trail stays visible.", SmokeScale);
                }
                else
                {
                    Graph.Scale(serializedObject, "projectileMassGrams", "Bullet mass", "Mass of one bullet. With the speed it gives the energy.", MassScale);
                    Graph.Scale(serializedObject, "projectileDiameterMillimeters", "Calibre", "Bullet diameter: its frontal area sets the air drag and the sectional density.", CalibreScale);
                    DrawBulletLength(definition);
                    EditorGUILayout.LabelField("Nose shape", EditorStyles.miniBoldLabel);
                    Graph.Choice(serializedObject.FindProperty("noseShape"), NoseTiles, 2, 40f);
                }
            }
            EndSection(showProjectile);
        }

        private void DrawBulletLength(ArmoryCoreAmmunitionDefinition definition)
        {
            var length = serializedObject.FindProperty("bulletLengthMillimeters");
            var estimated = length.floatValue <= 0f;
            using (new EditorGUI.DisabledScope(estimated))
            {
                EditorGUI.BeginChangeCheck();
                var next = Graph.Scale(
                    new GUIContent("Bullet length", "Length of the bullet itself, without the case."),
                    definition.BulletLengthMillimeters,
                    LengthScale);
                if (EditorGUI.EndChangeCheck())
                    length.floatValue = next;
            }
            var auto = Graph.ToggleTile(estimated, new Graph.Tile(
                "Estimate from mass",
                "jacketed lead bullet of this mass and calibre",
                "Turn off to type the length of a steel-core or monolithic bullet.") { Glyph = "≈", Color = Graph.Info }, 34f);
            if (auto != estimated)
                length.floatValue = auto ? 0f : Mathf.Round(definition.EstimatedBulletLengthMillimeters * 10f) / 10f;
        }

        private void DrawFlight(ArmoryCoreAmmunitionDefinition definition)
        {
            var velocity = definition.muzzleVelocityMetersPerSecond;
            if (Section(ref showFlight, "Shot & flight", $"{velocity:0} m/s · {definition.KineticEnergy(velocity):0} J"))
            {
                if (definition.IsRocket)
                {
                    Graph.Scale(serializedObject, "muzzleVelocityMetersPerSecond", "Launch speed", "Speed the rocket leaves the launcher with, before the sustainer motor ignites.", LaunchScale);
                }
                else
                {
                    Graph.Scale(serializedObject, "muzzleVelocityMetersPerSecond", "Muzzle velocity", "Speed of the bullet at the muzzle. Weapons scale it with their barrel multiplier.", VelocityScale);
                }
                Graph.Scale(serializedObject, "aerodynamicDragCoefficient", "Air drag", "Drag coefficient: the larger it is, the faster the projectile loses speed.", DragScale);
                Graph.Scale(serializedObject, "gravityMultiplier", "Gravity", "1 is real gravity; 0 flies straight. Rockets often use less to stand for lift and thrust.", GravityScale);
                Graph.Scale(serializedObject, "maximumRangeMeters", "Maximum range", "The projectile disappears after travelling this far.", RangeScale);
                Graph.Scale(serializedObject, "maximumFlightTime", "Maximum flight time", "The projectile disappears after flying this long.", FlightTimeScale);
                if (!definition.IsRocket)
                    Graph.Scale(serializedObject, "minimumLethalVelocity", "Minimum lethal speed", "Slower bullets are removed: they no longer hit anything.", LethalScale);
                serializedObject.ApplyModifiedProperties();
                DrawTrajectory(definition, velocity * VelocityMultiplier);
            }
            EndSection(showFlight);
        }

        private void DrawTerminal()
        {
            if (Section(ref showTerminal, "Terminal ballistics", "Damage falloff, penetration and ricochet."))
            {
                Graph.Scale(serializedObject, "penetrationMultiplier", "Penetration", "Divides the energy a ballistic material takes per millimetre. Soft lead below 1, steel and hardened cores above.", PenetrationScale);
                Graph.Scale(serializedObject, "retainedDamageAtMinimumVelocity", "Damage at lethal speed", "Share of the weapon damage still dealt when the bullet has slowed to the minimum lethal speed.", RetainedScale);
                Graph.Scale(serializedObject, "ricochetEnergyRetention", "Ricochet energy", "Share of the energy a ricocheting bullet keeps.", RicochetScale);
            }
            EndSection(showTerminal);
        }

        private void DrawRocketMotor()
        {
            if (Section(ref showRocketMotor, "Rocket motor", "Sustainer motor that accelerates the rocket after launch."))
            {
                Graph.Scale(serializedObject, "motorAccelerationMetersPerSecondSquared", "Thrust", "Net acceleration produced by the sustainer motor while it burns.", MotorScale);
                Graph.Scale(serializedObject, "motorIgnitionDelay", "Ignition delay", "Time after launch before the sustainer motor ignites.", IgnitionScale);
                Graph.Scale(serializedObject, "motorBurnTime", "Burn time", "How long the motor pushes.", BurnScale);
                Graph.Scale(serializedObject, "maximumRocketSpeedMetersPerSecond", "Top speed", "The motor never pushes the rocket past this speed.", TopSpeedScale);
            }
            EndSection(showRocketMotor);
        }

        private void DrawWarhead()
        {
            if (Section(ref showWarhead, "Warhead", "Fuze arming and the explosion."))
            {
                Graph.Scale(serializedObject, "armingDistanceMeters", "Arming distance", "Distance the rocket must travel before the fuze arms. Earlier impacts do not detonate.", ArmingScale);
                Graph.Scale(serializedObject, "explosionRadiusMeters", "Blast radius", "Radius of the explosion.", BlastRadiusScale);
                Graph.Scale(serializedObject, "explosionDamage", "Blast damage", "Damage at the centre of the explosion. Weapon damage is added to the directly hit target.", BlastDamageScale);
                Graph.Scale(serializedObject, "explosionDamageAtEdge", "Damage at the edge", "Share of the centre damage left at the blast radius.", EdgeScale);
                Graph.Scale(serializedObject, "explosionForce", "Blast impulse", "Impulse applied to rigidbodies at the centre of the explosion.", ForceScale);
                using (new EditorGUILayout.HorizontalScope())
                {
                    Graph.ToggleTile(serializedObject.FindProperty("explosionAffectsOwner"), new Graph.Tile("Hurts the owner", "inside the radius", "The shooter takes blast damage too.") { Glyph = "!", Color = Graph.Bad }, 34f);
                    Graph.ToggleTile(serializedObject.FindProperty("explodeOnTimeout"), new Graph.Tile("Air burst", "when range runs out", "Detonate in the air when the flight time or range runs out instead of disappearing.") { Glyph = "✸", Color = Graph.Warn }, 34f);
                }
            }
            EndSection(showWarhead);
        }

        private void DrawSimulation()
        {
            if (Section(ref showSimulation, "Simulation quality", "Raycast segment length and the substep budget per physics frame."))
            {
                Graph.Scale(serializedObject, "maximumSegmentLengthMeters", "Segment length", "Longest raycast per substep. Shorter segments follow the curve better.", SegmentScale);
                Graph.Scale(serializedObject, "maximumSubstepsPerFrame", "Substeps per frame", "Upper limit of substeps in one physics frame.", SubstepScale);
            }
            EndSection(showSimulation);
        }

        private void DrawEffect(ArmoryCoreAmmunitionDefinition definition)
        {
            if (Section(ref showEffect, "What you get", null))
            {
                var speed = definition.muzzleVelocityMetersPerSecond * VelocityMultiplier;
                var energy = definition.KineticEnergy(speed);
                var momentum = definition.ProjectileMassKilograms * speed;
                if (definition.IsRocket)
                {
                    var flight = ArmoryCoreFlight.Simulate(definition, speed, 100f, 41);
                    var reaches = ArmoryCoreFlight.At(flight, 100f, out var at100);
                    Graph.Bar("Top speed", definition.MaximumSpeedMetersPerSecond / 600f, Graph.Bad, $"{definition.MaximumSpeedMetersPerSecond:0} m/s");
                    Graph.Bar("Time to 100 m", reaches ? at100.Time / 2f : 1f, Graph.Info, reaches ? $"{at100.Time:0.00} s" : "does not reach");
                    Graph.Bar("Blast radius", definition.explosionRadiusMeters / 20f, Graph.Warn, $"{definition.explosionRadiusMeters:0.#} m");
                    Graph.Bar("Blast damage", definition.explosionDamage / 1000f, Graph.Bad, $"{definition.explosionDamage:0} · {definition.explosionDamageAtEdge * 100f:0}% at edge");
                    Graph.Bar("Launch impulse", momentum / 1000f, Graph.Muted, $"{momentum:0} kg·m/s");
                }
                else
                {
                    var flight = ArmoryCoreFlight.Simulate(definition, speed, 100f, 41);
                    var reaches = ArmoryCoreFlight.At(flight, 100f, out var at100);
                    var kept = reaches ? definition.KineticEnergy(at100.Speed) / Mathf.Max(0.001f, energy) : 0f;
                    Graph.Bar("Muzzle energy", energy / 6000f, Graph.Bad, $"{energy:0} J");
                    Graph.Bar("Energy kept at 100 m", kept, Graph.Warn, reaches ? $"{kept * 100f:0}%" : "does not reach");
                    Graph.Bar("Recoil impulse", momentum / 20f, Graph.Warn, $"{momentum:0.##} kg·m/s", "Momentum of the bullet: what the shooter's shoulder takes, before the powder gas.");
                    Graph.Bar("Sectional density", definition.SectionalDensity / 350f, Graph.Accent, $"{definition.SectionalDensity:0} kg/m²", "Mass per frontal area: denser bullets go deeper.");
                    Graph.Bar("Ballistic coefficient", definition.BallisticCoefficient / 1200f, Graph.Info, $"{definition.BallisticCoefficient:0} kg/m²", "m / (Cd·A): how well the bullet carries through the air.");
                    Graph.Bar("Penetration", definition.penetrationMultiplier / 3f, Graph.Good, $"×{definition.penetrationMultiplier:0.##}");
                    // Reference: 9x19 mm FMJ, 8 g at 370 m/s.
                    const float ReferenceEnergy = 547.6f;
                    Graph.Hint($"<b>{energy / ReferenceEnergy:0.#}×</b> the energy of a 9x19 mm FMJ (548 J). Weapon damage is scaled by the energy left on impact.");
                }
                if (!Mathf.Approximately(VelocityMultiplier, 1f))
                    Graph.Hint($"Shown for this weapon's barrel: ×{VelocityMultiplier:0.##} muzzle velocity, {speed:0} m/s.");
            }
            EndSection(showEffect);
        }

        private void DrawMultipleObjects()
        {
            ArmoryCoreDefinitionEditorUtility.DrawTitle($"{targets.Length} Ammunition Definitions", "Batch editing shared cartridge settings");
            DrawProperties(ref showIdentity, "Identity", "id", "displayName", "projectileType");
            DrawProperties(ref showProjectile, "Projectile", "projectileMassGrams", "projectileDiameterMillimeters", "noseShape", "bulletLengthMillimeters");
            DrawProperties(ref showFlight, "External ballistics", "muzzleVelocityMetersPerSecond", "aerodynamicDragCoefficient",
                "gravityMultiplier", "maximumFlightTime", "maximumRangeMeters", "minimumLethalVelocity");
            DrawProperties(ref showTerminal, "Terminal ballistics", "retainedDamageAtMinimumVelocity", "penetrationMultiplier", "ricochetEnergyRetention");
            DrawProperties(ref showRocketMotor, "Rocket motor", "motorAccelerationMetersPerSecondSquared", "motorIgnitionDelay",
                "motorBurnTime", "maximumRocketSpeedMetersPerSecond", "rocketLengthMeters", "smokeTrailTime");
            DrawProperties(ref showWarhead, "Warhead", "armingDistanceMeters", "explosionRadiusMeters", "explosionDamage",
                "explosionDamageAtEdge", "explosionForce", "explosionAffectsOwner", "explodeOnTimeout");
            DrawProperties(ref showSimulation, "Simulation quality", "maximumSegmentLengthMeters", "maximumSubstepsPerFrame");
        }

        private void DrawProperties(ref bool expanded, string title, params string[] properties)
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(ref expanded, title))
            {
                foreach (var property in properties)
                    ArmoryCoreDefinitionEditorUtility.DrawProperty(serializedObject, property);
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(expanded);
        }

        private static bool Section(ref bool expanded, string title, string description)
        {
            var open = ArmoryCoreDefinitionEditorUtility.BeginSection(ref expanded, title, description);
            if (open)
                EditorGUI.indentLevel--;
            return open;
        }

        private static void EndSection(bool expanded)
        {
            if (expanded)
                EditorGUI.indentLevel++;
            ArmoryCoreDefinitionEditorUtility.EndSection(expanded);
        }

        private static void DrawDiagnostics(ArmoryCoreAmmunitionDefinition definition)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(definition.id))
                issues.Add("ID is empty.");
            if (string.IsNullOrWhiteSpace(definition.displayName))
                issues.Add("Display Name is empty.");
            if (definition.maximumSegmentLengthMeters * definition.maximumSubstepsPerFrame
                < definition.MaximumSpeedMetersPerSecond * (1f / 30f))
            {
                issues.Add("At 30 FPS, the substep budget may be too low for the configured muzzle velocity.");
            }
            if (!definition.IsRocket && definition.minimumLethalVelocity >= definition.muzzleVelocityMetersPerSecond)
                issues.Add("Minimum lethal speed is not below the muzzle velocity: bullets vanish at once.");

            EditorGUILayout.HelpBox(
                issues.Count == 0 ? "Definition is ready for use." : string.Join("\n", issues),
                issues.Count == 0 ? MessageType.Info : MessageType.Warning);
        }

        // ---------- Pictures ----------

        /// <summary>The projectile seen from the side at true proportions, nose to the right.</summary>
        private static void DrawSilhouette(ArmoryCoreAmmunitionDefinition definition)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(140f, 64f, GUILayout.ExpandWidth(true)));
            Graph.Panel(rect);
            var length = definition.IsRocket ? definition.rocketLengthMeters * 1000f : definition.BulletLengthMillimeters;
            var diameter = definition.projectileDiameterMillimeters;
            var scale = Mathf.Min((rect.width - 170f) / Mathf.Max(0.1f, length), (rect.height - 20f) / Mathf.Max(0.1f, diameter));
            var body = new Rect(rect.x + 20f, rect.center.y - diameter * scale * 0.5f, length * scale, diameter * scale);
            if (definition.IsRocket)
            {
                DrawRocket(body, Olive, true);
            }
            else
            {
                var hardness = Mathf.InverseLerp(0.5f, 2.5f, definition.penetrationMultiplier);
                var noseShare = Mathf.Clamp(length / Mathf.Max(0.1f, diameter), 1f, 5f) > 2.5f ? 0.4f : 0.5f;
                DrawBullet(body, definition.noseShape, noseShare, Color.Lerp(Copper, Lead, hardness * 0.6f), true);
            }
            var text = new Rect(body.xMax + 10f, rect.y + 10f, rect.xMax - body.xMax - 14f, 16f);
            GUI.Label(text, definition.IsRocket
                ? $"⌀ {diameter:0} mm × {length * 0.001f:0.##} m"
                : $"⌀ {diameter:0.##} × {length:0.#} mm" + (definition.bulletLengthMillimeters > 0f ? "" : " (est.)"), EditorStyles.miniBoldLabel);
            text.y += 18f;
            GUI.Label(text, definition.IsRocket
                ? $"{definition.projectileMassGrams * 0.001f:0.##} kg"
                : $"{definition.projectileMassGrams:0.##} g · {definition.projectileMassGrams * 15.4324f:0} gr", EditorStyles.miniLabel);
        }

        /// <summary>Bullet silhouette in <paramref name="rect"/>: body, then the nose over the last <paramref name="noseShare"/> of the length.</summary>
        public static void DrawBullet(Rect rect, ArmoryCoreBulletNose nose, float noseShare, Color color, bool full = false)
        {
            if (!full)
            {
                var h = rect.height * 0.34f;
                rect = new Rect(rect.x, rect.center.y - h * 0.5f, rect.width, h);
            }
            var r = rect.height * 0.5f;
            var cy = rect.center.y;
            var noseStart = rect.xMax - rect.width * noseShare;
            var length = rect.xMax - noseStart;
            // A hollow point is a round nose cut where it narrows to the cavity rim, so the outline stays convex.
            var reach = nose == ArmoryCoreBulletNose.HollowPoint ? Mathf.Sqrt((1f - 0.45f * 0.45f) / 0.92f) : 1f;
            const int Steps = 14;
            var top = new List<Vector2> { new Vector2(rect.x, rect.y) };
            var bottom = new List<Vector2> { new Vector2(rect.x, rect.yMax) };
            for (var i = 0; i <= Steps; i++)
            {
                var t = i / (float)Steps;
                var half = nose == ArmoryCoreBulletNose.Spitzer ? r * Mathf.Pow(1f - t, 0.62f)
                    : nose == ArmoryCoreBulletNose.FlatNose ? r * Mathf.Lerp(1f, 0.55f, t)
                    : r * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t * reach * reach * 0.92f));
                var x = noseStart + length * t;
                top.Add(new Vector2(x, cy - half));
                if (half > 0.01f)
                    bottom.Add(new Vector2(x, cy + half));
            }
            bottom.Reverse();
            top.AddRange(bottom);
            Graph.Fill(color, top.ToArray());
            Graph.Fill(Graph.Shade(color, 0.7f), new Rect(rect.x, rect.y, Mathf.Min(2f, rect.width * 0.1f), rect.height));
            if (nose == ArmoryCoreBulletNose.HollowPoint)
                Graph.Fill(Graph.Shade(color, 0.35f), new Rect(rect.xMax - length * 0.25f, cy - r * 0.3f, length * 0.25f, r * 0.6f));
        }

        /// <summary>Rocket silhouette: warhead at full calibre with an ogive nose, narrow motor tube and tail fins.</summary>
        public static void DrawRocket(Rect rect, Color color, bool full = false)
        {
            if (!full)
            {
                var h = rect.height * 0.4f;
                rect = new Rect(rect.x, rect.center.y - h * 0.5f, rect.width, h);
            }
            var r = rect.height * 0.5f;
            var cy = rect.center.y;
            var warheadStart = rect.x + rect.width * 0.55f;
            var noseStart = rect.x + rect.width * 0.82f;
            var tube = r * 0.45f;
            var finEnd = rect.x + rect.width * 0.18f;
            var taperStart = warheadStart - r * 0.6f;
            Graph.Fill(Graph.Shade(color, 0.85f), new Rect(rect.x, cy - tube, taperStart - rect.x + r * 0.3f, tube * 2f));
            Graph.Fill(Graph.Shade(color, 0.7f),
                new Vector2(rect.x, cy - tube), new Vector2(rect.x, rect.y), new Vector2(finEnd, cy - tube));
            Graph.Fill(Graph.Shade(color, 0.7f),
                new Vector2(rect.x, cy + tube), new Vector2(finEnd, cy + tube), new Vector2(rect.x, rect.yMax));

            // Taper, warhead and nose as one convex outline: no seams between the parts.
            const int Steps = 12;
            var length = rect.xMax - noseStart;
            var top = new List<Vector2> { new Vector2(taperStart, cy - tube), new Vector2(warheadStart, rect.y) };
            var bottom = new List<Vector2> { new Vector2(taperStart, cy + tube), new Vector2(warheadStart, rect.yMax) };
            for (var i = 0; i <= Steps; i++)
            {
                var t = i / (float)Steps;
                var half = r * Mathf.Pow(1f - t, 0.6f);
                top.Add(new Vector2(noseStart + length * t, cy - half));
                if (half > 0.01f)
                    bottom.Add(new Vector2(noseStart + length * t, cy + half));
            }
            bottom.Reverse();
            top.AddRange(bottom);
            Graph.Fill(color, top.ToArray());
        }

        /// <summary>Speed and drop over the first metres of a level shot.</summary>
        private static void DrawTrajectory(ArmoryCoreAmmunitionDefinition definition, float launchSpeed)
        {
            var range = Mathf.Min(definition.maximumRangeMeters, definition.IsRocket ? 600f : 400f);
            var flight = ArmoryCoreFlight.Simulate(definition, launchSpeed, range);
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(140f, 140f, GUILayout.ExpandWidth(true)));
            Graph.Panel(rect);
            var graph = new Rect(rect.x + 8f, rect.y + 20f, rect.width - 16f, rect.height - 44f);
            for (var i = 1; i < 6; i++)
                Graph.FillRect(new Rect(graph.x + graph.width * i / 6f, graph.y, 1f, graph.height), Graph.GridColor);
            var topSpeed = 1f;
            var maximumDrop = 0.05f;
            foreach (var sample in flight)
            {
                topSpeed = Mathf.Max(topSpeed, sample.Speed);
                maximumDrop = Mathf.Max(maximumDrop, sample.Drop);
            }
            var speed = new Vector2[flight.Length];
            var drop = new Vector2[flight.Length];
            for (var i = 0; i < flight.Length; i++)
            {
                var x = graph.x + graph.width * flight[i].Distance / range;
                speed[i] = new Vector2(x, Mathf.Lerp(graph.yMax, graph.y, flight[i].Speed / topSpeed));
                drop[i] = new Vector2(x, Mathf.Lerp(graph.y, graph.yMax, Mathf.Clamp01(flight[i].Drop / maximumDrop)));
            }
            Graph.Polyline(speed, SpeedColor, 2f);
            Graph.Polyline(drop, DropColor, 2f);
            if (flight.Length > 0 && flight[flight.Length - 1].Distance < range - 0.5f)
            {
                var end = graph.x + graph.width * flight[flight.Length - 1].Distance / range;
                Graph.FillRect(new Rect(end, graph.y, 1f, graph.height), Graph.Bad);
            }
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f),
                $"speed (blue, up to {topSpeed:0} m/s) and drop (orange, down to {maximumDrop * 100f:0} cm) over {range:0} m", EditorStyles.miniLabel);
            var near = Mathf.Round(range / 8f / 25f) * 25f;
            var far = Mathf.Round(range * 0.5f / 50f) * 50f;
            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 22f, rect.width - 12f, 16f), Row(definition, flight, near) + "   ·   " + Row(definition, flight, far), EditorStyles.miniLabel);
        }

        private static string Row(ArmoryCoreAmmunitionDefinition definition, ArmoryCoreFlight.Sample[] flight, float distance)
        {
            if (!ArmoryCoreFlight.At(flight, distance, out var sample))
                return $"{distance:0} m: does not reach";
            return definition.IsRocket
                ? $"{distance:0} m: {sample.Speed:0} m/s, {sample.Time:0.00} s, ↓{sample.Drop:0.##} m"
                : $"{distance:0} m: {sample.Speed:0} m/s, {definition.KineticEnergy(sample.Speed):0} J, ↓{sample.Drop * 100f:0.#} cm";
        }
    }

    /// <summary>Cartridge choice as tiles, a new cartridge from real templates, and the chosen cartridge's editor inline.</summary>
    internal sealed class ArmoryCoreCartridgePicker : IDisposable
    {
        private const int CollapsedCount = 8;
        private string search = "";
        private bool showAll;
        private bool showSettings;
        private Editor cartridgeEditor;
        /// <summary>When set, a button hands the cartridge to this instead of showing its settings inline.</summary>
        public Action<ArmoryCoreAmmunitionDefinition> Open;

        public void Draw(SerializedProperty property, float velocityMultiplier)
        {
            if (property == null)
                return;
            if (property.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(property, new GUIContent("Ammunition Definition", "Ballistic data used by every projectile."));
                return;
            }

            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                var current = property.objectReferenceValue as ArmoryCoreAmmunitionDefinition;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Cartridge", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
                    search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                    if (GUILayout.Button(showAll ? "Fewer" : "All", EditorStyles.miniButton, GUILayout.Width(44f)))
                        showAll = !showAll;
                }

                var matching = ArmoryCoreCartridgeLibrary.All.Where(a => ArmoryCoreCartridgeLibrary.Matches(a, search)).ToList();
                var visible = showAll || matching.Count <= CollapsedCount ? matching : matching.Take(CollapsedCount).ToList();
                if (current && !visible.Contains(current) && matching.Contains(current))
                    visible[visible.Count - 1] = current;
                var tiles = visible.Select(Tile).ToList();
                tiles.Add(new ArmoryCoreGraph.Tile("New cartridge…", "from a real cartridge or a copy", "Create a cartridge asset and assign it.")
                    { Glyph = "+", Color = ArmoryCoreGraph.Muted });

                var index = visible.IndexOf(current);
                var picked = ArmoryCoreGraph.Choice(index, tiles, 2, 40f);
                if (picked != index)
                {
                    if (picked < visible.Count)
                        property.objectReferenceValue = visible[picked];
                    else
                        ShowCreateMenu(property, current);
                }
                if (visible.Count < matching.Count)
                    ArmoryCoreGraph.Hint($"{matching.Count - visible.Count} more: search by name or calibre, or press All.");
                else if (matching.Count == 0)
                    ArmoryCoreGraph.Hint(string.IsNullOrWhiteSpace(search) ? "The project has no cartridges yet." : "No cartridge matches the search.");

                EditorGUILayout.PropertyField(property, new GUIContent("Ammunition Definition", "Ballistic data used by every projectile."));
                current = property.objectReferenceValue as ArmoryCoreAmmunitionDefinition;
                if (!current)
                    return;

                var users = ArmoryCoreCartridgeLibrary.WeaponsUsing(current);
                if (Open != null)
                {
                    if (GUILayout.Button(new GUIContent($"Set up cartridge «{ArmoryCoreCartridgeLibrary.Name(current)}» →",
                            "Open the cartridge in the Cartridges tab: mass, calibre, bullet, speed and a comparison with the others")))
                        Open(current);
                    return;
                }
                if (!ArmoryCoreDefinitionEditorUtility.BeginSection(ref showSettings, "Cartridge settings", ArmoryCoreCartridgeLibrary.Summary(current)))
                {
                    ArmoryCoreDefinitionEditorUtility.EndSection(false);
                    return;
                }
                ArmoryCoreGraph.Line(
                    users > 1 ? $"Shared asset: changes apply to all {users} weapon definitions that fire this cartridge." : "Changes are saved in the cartridge asset.",
                    users > 1 ? ArmoryCoreGraph.Warn : ArmoryCoreGraph.Muted);
                Editor.CreateCachedEditor(current, typeof(ArmoryCoreAmmunitionDefinitionEditor), ref cartridgeEditor);
                var editor = (ArmoryCoreAmmunitionDefinitionEditor)cartridgeEditor;
                editor.Embedded = true;
                editor.VelocityMultiplier = velocityMultiplier;
                editor.OnInspectorGUI();
                ArmoryCoreDefinitionEditorUtility.EndSection(true);
            }
        }

        private static ArmoryCoreGraph.Tile Tile(ArmoryCoreAmmunitionDefinition ammunition)
        {
            return new ArmoryCoreGraph.Tile(
                ArmoryCoreCartridgeLibrary.Name(ammunition),
                ArmoryCoreCartridgeLibrary.Summary(ammunition),
                AssetDatabase.GetAssetPath(ammunition))
            {
                Color = ArmoryCoreGraph.Accent,
                Icon = ammunition.IsRocket
                    ? (Action<Rect, Color>)((r, c) => ArmoryCoreAmmunitionDefinitionEditor.DrawRocket(r, c))
                    : (r, c) => ArmoryCoreAmmunitionDefinitionEditor.DrawBullet(r, ammunition.noseShape, 0.4f, c)
            };
        }

        /// <summary>The menu runs after this event: it reopens the property by path on its owner.</summary>
        private static void ShowCreateMenu(SerializedProperty property, ArmoryCoreAmmunitionDefinition current)
        {
            var owner = property.serializedObject.targetObject;
            var path = property.propertyPath;
            var folder = ArmoryCoreCartridgeLibrary.FolderOf(current);

            void Assign(ArmoryCoreAmmunitionDefinition made)
            {
                if (!made || !owner)
                    return;
                var data = new SerializedObject(owner);
                data.Update();
                var target = data.FindProperty(path);
                if (target == null)
                    return;
                target.objectReferenceValue = made;
                data.ApplyModifiedProperties();
            }

            var menu = new GenericMenu();
            foreach (var template in ArmoryCoreCartridgeLibrary.Templates)
            {
                var chosen = template;
                menu.AddItem(new GUIContent("Real cartridge/" + template.Title), false,
                    () => Assign(ArmoryCoreCartridgeLibrary.Create(chosen, null, folder)));
            }
            if (current)
            {
                menu.AddItem(new GUIContent("Copy of " + ArmoryCoreCartridgeLibrary.Name(current)), false,
                    () => Assign(ArmoryCoreCartridgeLibrary.Create(null, current, folder)));
            }
            menu.AddItem(new GUIContent("Empty cartridge"), false, () => Assign(ArmoryCoreCartridgeLibrary.Create(null, null, folder)));
            menu.ShowAsContext();
        }

        public void Dispose()
        {
            if (cartridgeEditor)
                UnityEngine.Object.DestroyImmediate(cartridgeEditor);
            cartridgeEditor = null;
        }
    }
}
