using System;
using System.Collections.Generic;
using System.Linq;
using ArmoryCore;
using UnityEditor;
using UnityEngine;

namespace ArmoryCoreEditor
{
    internal static class ArmoryCoreDefinitionEditorUtility
    {
        private static readonly Color AccentColor = new Color(0.95f, 0.43f, 0.12f, 1f);
        private static GUIStyle headerTitleStyle;
        private static GUIStyle headerSubtitleStyle;
        private static GUIStyle sectionHeaderStyle;
        private static GUIStyle sectionDescriptionStyle;
        private static GUIStyle metricLabelStyle;
        private static GUIStyle metricValueStyle;

        private static Color SurfaceColor => EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.17f, 0.19f, 1f)
            : new Color(0.90f, 0.91f, 0.93f, 1f);

        private static Color MutedTextColor => EditorGUIUtility.isProSkin
            ? new Color(0.68f, 0.70f, 0.73f, 1f)
            : new Color(0.32f, 0.34f, 0.37f, 1f);

        private static GUIStyle HeaderTitleStyle
        {
            get
            {
                if (headerTitleStyle == null)
                {
                    headerTitleStyle = new GUIStyle(EditorStyles.largeLabel)
                    {
                        fontSize = 17,
                        fontStyle = FontStyle.Bold,
                        margin = new RectOffset(0, 0, 1, 0)
                    };
                }
                return headerTitleStyle;
            }
        }

        private static GUIStyle HeaderSubtitleStyle
        {
            get
            {
                if (headerSubtitleStyle == null)
                {
                    headerSubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        margin = new RectOffset(0, 0, 0, 2)
                    };
                    headerSubtitleStyle.normal.textColor = MutedTextColor;
                }
                return headerSubtitleStyle;
            }
        }

        private static GUIStyle SectionHeaderStyle
        {
            get
            {
                if (sectionHeaderStyle == null)
                {
                    sectionHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader)
                    {
                        fontStyle = FontStyle.Bold,
                        fixedHeight = 24f,
                        padding = new RectOffset(16, 5, 4, 2)
                    };
                }
                return sectionHeaderStyle;
            }
        }

        private static GUIStyle SectionDescriptionStyle
        {
            get
            {
                if (sectionDescriptionStyle == null)
                {
                    sectionDescriptionStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        margin = new RectOffset(3, 3, 2, 6)
                    };
                    sectionDescriptionStyle.normal.textColor = MutedTextColor;
                }
                return sectionDescriptionStyle;
            }
        }

        private static GUIStyle MetricLabelStyle
        {
            get
            {
                if (metricLabelStyle == null)
                {
                    metricLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };
                    metricLabelStyle.normal.textColor = MutedTextColor;
                }
                return metricLabelStyle;
            }
        }

        private static GUIStyle MetricValueStyle
        {
            get
            {
                if (metricValueStyle == null)
                {
                    metricValueStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 13,
                        fontStyle = FontStyle.Bold
                    };
                }
                return metricValueStyle;
            }
        }

        public static void DrawTitle(string title, string subtitle)
        {
            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var accent = GUILayoutUtility.GetRect(0f, 3f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(accent, AccentColor);
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(title, HeaderTitleStyle);
                EditorGUILayout.LabelField(subtitle, HeaderSubtitleStyle);
            }
            EditorGUILayout.Space(3f);
        }

        public static void DrawWeaponHeader(ArmoryCoreWeaponDefinition definition, bool editingMultiple, int count)
        {
            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var accent = GUILayoutUtility.GetRect(0f, 4f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(accent, AccentColor);
                EditorGUILayout.Space(3f);

                var title = editingMultiple
                    ? $"{count} Weapon Definitions"
                    : string.IsNullOrWhiteSpace(definition.displayName)
                        ? definition.name
                        : definition.displayName;
                EditorGUILayout.LabelField(title, HeaderTitleStyle);

                var subtitle = editingMultiple
                    ? "Batch editing shared weapon configuration"
                    : $"{ObjectNames.NicifyVariableName(definition.action.ToString())}  •  "
                      + $"{ObjectNames.NicifyVariableName(definition.reloadType.ToString())}  •  "
                      + FormatFireModes(definition.fireModes);
                EditorGUILayout.LabelField(subtitle, HeaderSubtitleStyle);
            }
            EditorGUILayout.Space(4f);
        }

        public static bool BeginSection(ref bool expanded, string label, string description = null)
        {
            EditorGUILayout.Space(2f);
            var header = GUILayoutUtility.GetRect(0f, 27f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(header, SurfaceColor);
            EditorGUI.DrawRect(new Rect(header.x, header.y, 3f, header.height), AccentColor);
            expanded = EditorGUI.Foldout(
                new Rect(header.x + 5f, header.y + 1f, header.width - 7f, header.height - 2f),
                expanded,
                label,
                true,
                SectionHeaderStyle);
            if (expanded)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (!string.IsNullOrWhiteSpace(description))
                    EditorGUILayout.LabelField(description, SectionDescriptionStyle);
                EditorGUI.indentLevel++;
            }
            return expanded;
        }

        public static void EndSection(bool expanded)
        {
            if (expanded)
            {
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2f);
                EditorGUILayout.EndVertical();
            }
        }

        public static void DrawProperty(SerializedObject serialized, string name)
        {
            var property = serialized.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        public static void DrawProperty(SerializedObject serialized, string name, GUIContent label)
        {
            var property = serialized.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, label, true);
        }

        public static void DrawSubheading(string label)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        public static void DrawMetric(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.MinWidth(92f),
                       GUILayout.MinHeight(48f),
                       GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField(label.ToUpperInvariant(), MetricLabelStyle);
                EditorGUILayout.LabelField(value, MetricValueStyle);
            }
        }

        public static void DrawIdentityTools(UnityEngine.Object[] targets)
        {
            if (GUILayout.Button("Fill missing identity from asset name"))
            {
                Undo.RecordObjects(targets, "Fill ArmoryCore identity");
                for (var index = 0; index < targets.Length; index++)
                {
                    var serialized = new SerializedObject(targets[index]);
                    var id = serialized.FindProperty("id");
                    var displayName = serialized.FindProperty("displayName");
                    if (id != null && string.IsNullOrWhiteSpace(id.stringValue))
                        id.stringValue = targets[index].name;
                    if (displayName != null && string.IsNullOrWhiteSpace(displayName.stringValue))
                        displayName.stringValue = ObjectNames.NicifyVariableName(targets[index].name);
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(targets[index]);
                }
            }
        }

        public static string FormatModifier(float multiplier)
        {
            var percent = (multiplier - 1f) * 100f;
            if (Mathf.Abs(percent) < 0.05f)
                return "No change";
            return percent > 0f ? $"+{percent:0.#}%" : $"{percent:0.#}%";
        }

        private static string FormatFireModes(ArmoryCoreFireMode[] modes)
        {
            if (modes == null || modes.Length == 0)
                return "No fire modes";

            var labels = new string[modes.Length];
            for (var index = 0; index < modes.Length; index++)
            {
                labels[index] = modes[index] == ArmoryCoreFireMode.SemiAutomatic
                    ? "Semi"
                    : modes[index] == ArmoryCoreFireMode.Automatic
                        ? "Auto"
                        : modes[index].ToString();
            }
            return string.Join(" / ", labels);
        }
    }

    /// <summary>
    /// The weapon definition as numbered setup steps with scales, tiles and pictures, the same way the Weapon Setup window
    /// shows it. Several definitions at once are edited as plain grouped fields.
    /// </summary>
    [CustomEditor(typeof(ArmoryCoreWeaponDefinition))]
    [CanEditMultipleObjects]
    public sealed class ArmoryCoreWeaponDefinitionEditor : Editor
    {
        private static readonly string[][] Groups =
        {
            new[] { "Identity", "id", "displayName", "weaponTags" },
            new[] { "Mechanism", "action", "reloadType", "fireModes", "roundsPerMinute", "burstSize", "actionCycleTime" },
            new[] { "Feeding", "ammunition", "magazineCapacity", "chamberCapacity", "spawnWithFullMagazine", "spawnWithChamberLoaded" },
            new[] { "Shot", "projectilesPerShot", "damagePerProjectile", "muzzleVelocityMultiplier", "accuracyMinutesOfAngle",
                "hipFireSpreadMultiplier", "movingSpreadMultiplier", "heatSpreadMultiplier" },
            new[] { "Recoil", "verticalRecoilDegrees", "horizontalRecoilDegrees", "recoilRandomness", "recoilRecoveryDegreesPerSecond", "recoilRecoveryDelay" },
            new[] { "Handling", "weaponMassKilograms", "aimDownSightsTime", "tacticalReloadTime", "emptyReloadTime", "perRoundReloadTime" },
            new[] { "Reliability and heat", "meanRoundsBetweenFailures", "heatPerShot", "heatDissipationPerSecond", "heatForAccuracyPenalty",
                "heatForCookOff", "durabilityLossPerShot", "foulingPerShot" },
            new[] { "Compatibility", "compatibleAttachmentIds" },
            new[] { "Audio", "shotSound", "suppressedShotSound", "reloadSound", "emptySound", "shotVolume", "reloadVolume", "emptyVolume",
                "shotPitchVariation", "audioMinDistanceMeters", "audioMaxDistanceMeters" },
        };

        private readonly bool[] expanded = { true, true, true, true, false, false, false, false, false };
        private ArmoryCoreWeaponSteps steps;

        private void OnEnable()
        {
            steps = new ArmoryCoreWeaponSteps { Key = "weapon.inspector" };
        }

        private void OnDisable()
        {
            steps?.Dispose();
            steps = null;
        }

        public override void OnInspectorGUI()
        {
            DrawInspector(false);
        }

        /// <summary>The steps without the title, for the controller inspector.</summary>
        public void DrawEmbeddedInspector()
        {
            DrawInspector(true);
        }

        private void DrawInspector(bool embedded)
        {
            serializedObject.Update();
            var definition = (ArmoryCoreWeaponDefinition)target;
            if (serializedObject.isEditingMultipleObjects)
            {
                ArmoryCoreDefinitionEditorUtility.DrawWeaponHeader(definition, true, targets.Length);
                DrawGroups();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            if (!embedded)
                ArmoryCoreDefinitionEditorUtility.DrawWeaponHeader(definition, false, 1);
            DrawToolbar(definition);
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                steps.DrawAll(serializedObject);
            serializedObject.ApplyModifiedProperties();

            ArmoryCoreSetupGUI.Issues(
                "weapon.inspector.issues",
                ArmoryCoreWeaponSteps.Validate(definition),
                "Ready to fire · nothing to fix.");
            steps.Status = ArmoryCoreSetupGUI.Status(steps.Status);
        }

        private static void DrawToolbar(ArmoryCoreWeaponDefinition definition)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var fires = definition.ammunition != null && definition.fireModes != null
                    && definition.fireModes.Any(mode => mode != ArmoryCoreFireMode.Safe);
                ArmoryCoreSetupGUI.Badge(fires ? "✓ fires" : "— cannot fire", fires ? ArmoryCoreGraph.Good : ArmoryCoreGraph.Muted);
                ArmoryCoreSetupGUI.Badge(definition.ammunition ? "✓ cartridge" : "— cartridge", definition.ammunition ? ArmoryCoreGraph.Good : ArmoryCoreGraph.Muted);
                ArmoryCoreSetupGUI.Badge(definition.shotSound ? "✓ sound" : "— sound", definition.shotSound ? ArmoryCoreGraph.Good : ArmoryCoreGraph.Muted);
                GUILayout.FlexibleSpace();
                if (ArmoryCoreSetupGUI.MiniButton("Weapon Setup…", "Open the definition in the Weapon Setup window: muzzle on the 3D model, cartridges and ballistics", 104f))
                    ArmoryCoreWeaponSetupWindow.Open(definition);
                ArmoryCoreGraph.NumbersToggle();
            }
            GUILayout.Space(2f);
        }

        private void DrawGroups()
        {
            for (var group = 0; group < Groups.Length; group++)
            {
                if (ArmoryCoreDefinitionEditorUtility.BeginSection(ref expanded[group], Groups[group][0]))
                {
                    for (var index = 1; index < Groups[group].Length; index++)
                        ArmoryCoreDefinitionEditorUtility.DrawProperty(serializedObject, Groups[group][index]);
                }
                ArmoryCoreDefinitionEditorUtility.EndSection(expanded[group]);
            }
        }
    }

    /// <summary>
    /// The attachment definition as numbered setup steps: slot and identity, weight, recoil and accuracy, muzzle signature
    /// and optic, then what it changes on a weapon. Several definitions at once are edited as plain grouped fields.
    /// </summary>
    [CustomEditor(typeof(ArmoryCoreAttachmentDefinition))]
    [CanEditMultipleObjects]
    public sealed class ArmoryCoreAttachmentDefinitionEditor : Editor
    {
        private static readonly ArmoryCoreGraph.Spec MassScale = new ArmoryCoreGraph.Spec(0.02f, 3f, "kg", 1f, "0.###", true).Captions("light", "heavy").Colors(ArmoryCoreGraph.Good, ArmoryCoreGraph.Warn).Limits(0f)
            .With(ArmoryCoreGraph.M(0.14f, "grip"), ArmoryCoreGraph.M(0.32f, "sight"), ArmoryCoreGraph.M(0.45f, "suppressor"), ArmoryCoreGraph.M(1.5f, "launcher"));
        private static readonly ArmoryCoreGraph.Spec RecoilScale = Multiplier(0.4f, 1.6f).With(ArmoryCoreGraph.M(0.6f, "grip"), ArmoryCoreGraph.M(0.9f, "scope"), ArmoryCoreGraph.M(1f, "no change"));
        private static readonly ArmoryCoreGraph.Spec SpreadScale = Multiplier(0.5f, 1.5f).With(ArmoryCoreGraph.M(0.8f, "grip"), ArmoryCoreGraph.M(1f, "no change"));
        private static readonly ArmoryCoreGraph.Spec VelocityScale = Multiplier(0.8f, 1.2f, true).With(ArmoryCoreGraph.M(0.9f, "suppressor"), ArmoryCoreGraph.M(1f, "no change"));
        private static readonly ArmoryCoreGraph.Spec AimScale = Multiplier(0.8f, 1.3f).With(ArmoryCoreGraph.M(1f, "no change"), ArmoryCoreGraph.M(1.1f, "heavy optic"));
        private static readonly ArmoryCoreGraph.Spec ReloadScale = Multiplier(0.6f, 1.5f).With(ArmoryCoreGraph.M(0.85f, "magwell"), ArmoryCoreGraph.M(1f, "no change"));
        private static readonly ArmoryCoreGraph.Spec ReliabilityScale = Multiplier(0.5f, 1.5f, true).With(ArmoryCoreGraph.M(0.88f, "suppressor"), ArmoryCoreGraph.M(1f, "no change"));
        private static readonly ArmoryCoreGraph.Spec SoundScale = new ArmoryCoreGraph.Spec(0f, 2f, "×", 1f, "0.##").Captions("silent", "louder").Colors(ArmoryCoreGraph.Good, ArmoryCoreGraph.Bad).Limits(0f, 2f)
            .With(ArmoryCoreGraph.M(0.28f, "suppressor"), ArmoryCoreGraph.M(1f, "no change"), ArmoryCoreGraph.M(1.3f, "brake"));
        private static readonly ArmoryCoreGraph.Spec FlashScale = new ArmoryCoreGraph.Spec(0f, 2f, "×", 1f, "0.##").Captions("no flash", "bigger").Colors(ArmoryCoreGraph.Good, ArmoryCoreGraph.Warn).Limits(0f, 2f)
            .With(ArmoryCoreGraph.M(0.12f, "suppressor"), ArmoryCoreGraph.M(0.4f, "flash hider"), ArmoryCoreGraph.M(1f, "no change"));
        private static readonly ArmoryCoreGraph.Spec MagnificationScale = new ArmoryCoreGraph.Spec(1f, 25f, "×", 1f, "0.#", true).Captions("red dot", "long range").Limits(1f)
            .With(ArmoryCoreGraph.M(1f, "1×"), ArmoryCoreGraph.M(4f, "4×"), ArmoryCoreGraph.M(8f, "8×"), ArmoryCoreGraph.M(12f, "12×"));
        private static readonly ArmoryCoreGraph.Spec ZeroScale = new ArmoryCoreGraph.Spec(25f, 1000f, "m", 1f, "0", true).Captions("close", "far").Limits(1f)
            .With(ArmoryCoreGraph.M(50f, "pistol"), ArmoryCoreGraph.M(100f, "standard"), ArmoryCoreGraph.M(300f, "rifle"));
        /// <summary>Field of view the controller aims with by default, which sights narrow by their magnification.</summary>
        private const float AimFieldOfView = 42f;

        private static readonly ArmoryCoreGraph.Tile[] SlotTiles =
        {
            new ArmoryCoreGraph.Tile("Sights", "optics, red dots", "Aimed through; may magnify.") { Glyph = "⌖" },
            new ArmoryCoreGraph.Tile("Barrel", "suppressors, brakes", "On the muzzle; may move the muzzle point.") { Glyph = "▬" },
            new ArmoryCoreGraph.Tile("Grip", "grips, bipods", "Under the handguard.") { Glyph = "┃" },
            new ArmoryCoreGraph.Tile("Stock", "stocks", "At the shoulder.") { Glyph = "◣" },
            new ArmoryCoreGraph.Tile("Magazine", "magazines", "In the magazine well.") { Glyph = "▮" },
            new ArmoryCoreGraph.Tile("Generic", "anything else", "No fixed place on the weapon.") { Glyph = "◇" },
        };

        private static List<ArmoryCoreWeaponDefinition> weapons;
        private bool showIdentity = true;
        private bool showPhysical = true;
        private bool showSignature;
        private bool showOptic = true;

        static ArmoryCoreAttachmentDefinitionEditor()
        {
            EditorApplication.projectChanged += () => weapons = null;
        }

        /// <summary>A multiplier around 1: below it reads as better, above as worse, unless <paramref name="higherIsBetter"/>.</summary>
        private static ArmoryCoreGraph.Spec Multiplier(float min, float max, bool higherIsBetter = false)
        {
            return new ArmoryCoreGraph.Spec(min, max, "×", 1f, "0.###")
                .Captions(higherIsBetter ? "worse" : "better", higherIsBetter ? "better" : "worse")
                .Colors(higherIsBetter ? ArmoryCoreGraph.Bad : ArmoryCoreGraph.Good, higherIsBetter ? ArmoryCoreGraph.Good : ArmoryCoreGraph.Bad)
                .Limits(0.01f);
        }

        private static List<ArmoryCoreWeaponDefinition> Weapons => weapons ??= AssetDatabase
            .FindAssets("t:" + nameof(ArmoryCoreWeaponDefinition))
            .Select(guid => AssetDatabase.LoadAssetAtPath<ArmoryCoreWeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(weapon => weapon)
            .ToList();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var definition = (ArmoryCoreAttachmentDefinition)target;
            var title = string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
            if (serializedObject.isEditingMultipleObjects)
            {
                ArmoryCoreDefinitionEditorUtility.DrawTitle($"{targets.Length} Attachment Definitions", "Batch editing shared attachment settings");
                DrawGroups();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            ArmoryCoreDefinitionEditorUtility.DrawTitle(title, ObjectNames.NicifyVariableName(definition.slot.ToString()) + " attachment · " + Summary(definition));
            DrawToolbar(definition);
            DrawPicture(definition);
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                var number = 1;
                DrawIdentity(definition, number++);
                DrawHandling(definition, number++);
                DrawRecoil(definition, number++);
                DrawSignature(definition, number++);
                if (definition.slot == ArmoryCoreAttachmentSlot.Sights)
                    DrawOptic(definition, number++);
                DrawEffect(definition, number);
            }
            serializedObject.ApplyModifiedProperties();
            ArmoryCoreSetupGUI.Issues("attachment.inspector.issues", Validate(definition), "Ready for use · nothing to fix.");
        }

        private static string Summary(ArmoryCoreAttachmentDefinition definition)
        {
            var parts = new List<string> { $"{definition.massKilograms:0.###} kg" };
            if (definition.slot == ArmoryCoreAttachmentSlot.Sights)
                parts.Add(definition.GameplayMaximumMagnification > definition.GameplayMinimumMagnification
                    ? $"{definition.GameplayMinimumMagnification:0.#}–{definition.GameplayMaximumMagnification:0.#}×"
                    : $"{definition.GameplayMaximumMagnification:0.#}×");
            if (definition.suppressesMuzzleBlast)
                parts.Add("suppressed");
            if (Mathf.Abs(definition.recoilMultiplier - 1f) > 0.005f)
                parts.Add("recoil " + ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.recoilMultiplier));
            return string.Join(" · ", parts);
        }

        private static void DrawToolbar(ArmoryCoreAttachmentDefinition definition)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var named = !string.IsNullOrWhiteSpace(definition.id) && !string.IsNullOrWhiteSpace(definition.displayName);
                ArmoryCoreSetupGUI.Badge(named ? "✓ identity" : "— identity", named ? ArmoryCoreGraph.Good : ArmoryCoreGraph.Muted);
                var users = Weapons.Count(weapon => Allows(weapon, definition));
                ArmoryCoreSetupGUI.Badge(users > 0 ? $"✓ fits {users} weapons" : "— fits no weapon", users > 0 ? ArmoryCoreGraph.Good : ArmoryCoreGraph.Muted);
                GUILayout.FlexibleSpace();
                var prefab = ArmoryCoreHoverPreview.PrefabOf(definition);
                using (new EditorGUI.DisabledScope(!prefab))
                {
                    if (ArmoryCoreSetupGUI.MiniButton("Prefab", prefab ? "Ping the module prefab that carries this definition" : "No module prefab carries this definition", 52f))
                        EditorGUIUtility.PingObject(prefab);
                }
                ArmoryCoreGraph.NumbersToggle();
            }
            GUILayout.Space(2f);
        }

        /// <summary>Weapons whose definition accepts the attachment: the same rules as ArmoryCoreWeaponController.CanInstall.</summary>
        private static bool Allows(ArmoryCoreWeaponDefinition weapon, ArmoryCoreAttachmentDefinition attachment)
        {
            var ids = weapon.compatibleAttachmentIds;
            if (ids != null && ids.Length > 0 && !ids.Any(id => string.Equals(id, attachment.id, StringComparison.OrdinalIgnoreCase)))
                return false;
            var tags = weapon.weaponTags ?? Array.Empty<string>();
            return (attachment.requiredWeaponTags ?? Array.Empty<string>())
                .All(tag => tags.Any(owned => string.Equals(owned, tag, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>The module's model next to its key numbers.</summary>
        private static void DrawPicture(ArmoryCoreAttachmentDefinition definition)
        {
            var prefab = ArmoryCoreHoverPreview.PrefabOf(definition);
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(140f, 96f, GUILayout.ExpandWidth(true)));
            ArmoryCoreGraph.Panel(rect);
            var image = new Rect(rect.x + 6f, rect.y + 6f, rect.height - 12f, rect.height - 12f);
            EditorGUI.DrawRect(image, ArmoryCoreGraph.TrackColor);
            var preview = prefab ? AssetPreview.GetAssetPreview(prefab) : null;
            if (preview)
                GUI.DrawTexture(image, preview, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(image, prefab ? "loading…" : "no prefab", EditorStyles.centeredGreyMiniLabel);
            if (prefab && !preview && AssetPreview.IsLoadingAssetPreview(prefab.GetInstanceID()))
                EditorWindow.mouseOverWindow?.Repaint();
            var text = new Rect(image.xMax + 10f, rect.y + 8f, rect.xMax - image.xMax - 14f, 16f);
            GUI.Label(text, ObjectNames.NicifyVariableName(definition.slot.ToString()), EditorStyles.miniBoldLabel);
            foreach (var line in Changes(definition).Take(4))
            {
                text.y += 17f;
                GUI.Label(text, line, EditorStyles.miniLabel);
            }
        }

        private static IEnumerable<string> Changes(ArmoryCoreAttachmentDefinition definition)
        {
            yield return $"+{definition.massKilograms:0.###} kg";
            if (definition.slot == ArmoryCoreAttachmentSlot.Sights && definition.GameplayMaximumMagnification > 1f)
                yield return $"magnifies {definition.GameplayMinimumMagnification:0.#}–{definition.GameplayMaximumMagnification:0.#}×, zero {definition.defaultZeroDistanceMeters:0} m";
            if (definition.suppressesMuzzleBlast)
                yield return $"suppressed · sound ×{definition.soundPressureMultiplier:0.##}";
            var names = new[] { "recoil", "spread", "muzzle velocity", "aim time", "reload time", "reliability" };
            var values = new[] { definition.recoilMultiplier, definition.spreadMultiplier, definition.muzzleVelocityMultiplier,
                definition.aimTimeMultiplier, definition.reloadTimeMultiplier, definition.reliabilityMultiplier };
            for (var i = 0; i < names.Length; i++)
            {
                if (Mathf.Abs(values[i] - 1f) >= 0.005f)
                    yield return names[i] + " " + ArmoryCoreDefinitionEditorUtility.FormatModifier(values[i]);
            }
        }

        // ---------- Steps ----------

        private static bool Step(string id, int number, string title, ArmoryCoreSetupGUI.StepState state, string summary, string tooltip)
        {
            return ArmoryCoreSetupGUI.Step("attachment.inspector.step." + id, number, title, state, summary, tooltip);
        }

        private void DrawIdentity(ArmoryCoreAttachmentDefinition definition, int number)
        {
            var named = !string.IsNullOrWhiteSpace(definition.id) && !string.IsNullOrWhiteSpace(definition.displayName);
            var tags = definition.requiredWeaponTags == null || definition.requiredWeaponTags.Length == 0
                ? "no tags needed"
                : "needs " + string.Join(", ", definition.requiredWeaponTags);
            if (!Step("identity", number, "Slot & identity", named ? ArmoryCoreSetupGUI.StepState.Done : ArmoryCoreSetupGUI.StepState.Current,
                    $"{ObjectNames.NicifyVariableName(definition.slot.ToString()).ToLowerInvariant()} · {tags}", "Where it goes, its IDs and the weapon tags it needs"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                ArmoryCoreGraph.Choice(serializedObject.FindProperty("slot"), SlotTiles, 3, 36f);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("id"), new GUIContent("Attachment ID", "Weapons list this ID to allow the attachment."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Display name", "The player-facing name."));
                if (!named && ArmoryCoreSetupGUI.Button("Fill the empty ID and name from the asset name"))
                {
                    if (string.IsNullOrWhiteSpace(serializedObject.FindProperty("id").stringValue))
                        serializedObject.FindProperty("id").stringValue = definition.name;
                    if (string.IsNullOrWhiteSpace(serializedObject.FindProperty("displayName").stringValue))
                        serializedObject.FindProperty("displayName").stringValue = ObjectNames.NicifyVariableName(definition.name);
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredWeaponTags"), new GUIContent("Required weapon tags", "Every tag here must be on the weapon definition."), true);
                var users = Weapons.Where(weapon => Allows(weapon, definition)).ToList();
                ArmoryCoreGraph.Hint(users.Count == 0
                    ? "No weapon definition of the project accepts it yet: add its ID to a weapon's attachments (Weapon, «Identity & attachments»)."
                    : $"Fits <b>{users.Count}</b> weapon definitions: " + string.Join(", ", users.Take(6).Select(ArmoryCoreWeaponSteps.Name)) + (users.Count > 6 ? $" and {users.Count - 6} more." : "."));
            }
        }

        private void DrawHandling(ArmoryCoreAttachmentDefinition definition, int number)
        {
            if (!Step("handling", number, "Weight & handling", ArmoryCoreSetupGUI.StepState.Done,
                    $"{definition.massKilograms:0.###} kg · aim {ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.aimTimeMultiplier).ToLowerInvariant()}",
                    "Added mass and how it changes aiming, reloading and reliability"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                ArmoryCoreGraph.Scale(serializedObject, "massKilograms", "Mass", "Added to the weapon's mass.", MassScale);
                ArmoryCoreGraph.Scale(serializedObject, "aimTimeMultiplier", "Aim time", "Multiplies the time to aim down the sights.", AimScale);
                ArmoryCoreGraph.Scale(serializedObject, "reloadTimeMultiplier", "Reload time", "Multiplies every reload time.", ReloadScale);
                ArmoryCoreGraph.Scale(serializedObject, "reliabilityMultiplier", "Reliability", "Multiplies the rounds between stoppages.", ReliabilityScale);
            }
        }

        private void DrawRecoil(ArmoryCoreAttachmentDefinition definition, int number)
        {
            var summary = $"recoil {ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.recoilMultiplier).ToLowerInvariant()} · spread {ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.spreadMultiplier).ToLowerInvariant()}";
            if (!Step("recoil", number, "Recoil & accuracy", ArmoryCoreSetupGUI.StepState.Done, summary, "Kick, dispersion and muzzle velocity"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                ArmoryCoreGraph.Scale(serializedObject, "recoilMultiplier", "Recoil", "Multiplies the vertical and horizontal kick.", RecoilScale);
                ArmoryCoreGraph.Scale(serializedObject, "spreadMultiplier", "Spread", "Multiplies the dispersion cone.", SpreadScale);
                ArmoryCoreGraph.Scale(serializedObject, "muzzleVelocityMultiplier", "Muzzle velocity", "Multiplies the speed projectiles leave with.", VelocityScale);
            }
        }

        private void DrawSignature(ArmoryCoreAttachmentDefinition definition, int number)
        {
            var changes = definition.suppressesMuzzleBlast || Mathf.Abs(definition.soundPressureMultiplier - 1f) > 0.005f || Mathf.Abs(definition.muzzleFlashMultiplier - 1f) > 0.005f;
            var summary = definition.suppressesMuzzleBlast ? $"suppressed · sound ×{definition.soundPressureMultiplier:0.##}" : changes ? $"sound ×{definition.soundPressureMultiplier:0.##} · flash ×{definition.muzzleFlashMultiplier:0.##}" : "unchanged";
            if (!Step("signature", number, "Sound & flash", changes ? ArmoryCoreSetupGUI.StepState.Done : ArmoryCoreSetupGUI.StepState.Optional, summary, "How loud the shot is and how big the muzzle flash"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                ArmoryCoreGraph.ToggleTile(serializedObject.FindProperty("suppressesMuzzleBlast"), new ArmoryCoreGraph.Tile("Suppressor", "plays the suppressed shot",
                    "The weapon plays its suppressed shot sound while this is installed.") { Glyph = "≋", Color = ArmoryCoreGraph.Info }, 34f);
                ArmoryCoreGraph.Scale(serializedObject, "soundPressureMultiplier", "Loudness", "Multiplies the sound pressure of the shot.", SoundScale);
                ArmoryCoreGraph.Scale(serializedObject, "muzzleFlashMultiplier", "Muzzle flash", "Multiplies the flash particles and light.", FlashScale);
                if (definition.suppressesMuzzleBlast && definition.soundPressureMultiplier >= 1f)
                    ArmoryCoreGraph.Line("It suppresses the blast but does not make the shot quieter.", ArmoryCoreGraph.Warn);
            }
        }

        private void DrawOptic(ArmoryCoreAttachmentDefinition definition, int number)
        {
            var summary = definition.IsReflexSight ? "reflex sight · 1×" : $"{definition.GameplayMinimumMagnification:0.#}–{definition.GameplayMaximumMagnification:0.#}× · zero {definition.defaultZeroDistanceMeters:0} m";
            if (!Step("optic", number, "Optic", ArmoryCoreSetupGUI.StepState.Done, summary, "Magnification range and zero distance"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                ArmoryCoreGraph.Scale(serializedObject, "minimumMagnification", "Lowest magnification", "Magnification at the lowest zoom step.", MagnificationScale);
                ArmoryCoreGraph.Scale(serializedObject, "maximumMagnification", "Highest magnification", "Magnification at the highest zoom step; the wheel steps between them.", MagnificationScale);
                ArmoryCoreGraph.Scale(serializedObject, "defaultZeroDistanceMeters", "Zero distance", "Distance at which the shot meets the line of sight.", ZeroScale);
                if (definition.IsReflexSight)
                    ArmoryCoreGraph.Line("This ID is one of the reflex sights: it always aims at 1×, whatever is set here.", ArmoryCoreGraph.Warn);
                DrawSightPicture(definition);
            }
        }

        /// <summary>A person at 100 m through the sight at its lowest and highest magnification.</summary>
        private static void DrawSightPicture(ArmoryCoreAttachmentDefinition definition)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(140f, 124f, GUILayout.ExpandWidth(true)));
            ArmoryCoreGraph.Panel(rect);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), "A person at 100 m through the sight", EditorStyles.miniBoldLabel);
            var radius = Mathf.Min(rect.height - 34f, rect.width * 0.25f - 8f) * 0.5f;
            var low = definition.GameplayMinimumMagnification;
            var high = definition.GameplayMaximumMagnification;
            Scope(new Vector2(rect.x + rect.width * 0.25f, rect.y + 20f + radius), radius, low);
            Scope(new Vector2(rect.x + rect.width * 0.7f, rect.y + 20f + radius), radius, high);
        }

        private static void Scope(Vector2 center, float radius, float magnification)
        {
            ArmoryCoreGraph.Disc(center, radius + 2f, ArmoryCoreGraph.Ink * new Color(1f, 1f, 1f, 0.5f));
            ArmoryCoreGraph.Disc(center, radius, new Color(0.42f, 0.5f, 0.38f));
            // The sight's radius covers half the aim field of view narrowed by the magnification.
            var halfView = Mathf.Tan(AimFieldOfView / magnification * 0.5f * Mathf.Deg2Rad) * 100f;
            var pixelsPerMeter = radius / Mathf.Max(0.01f, halfView);
            var person = new Vector2(0.45f, 1.75f) * pixelsPerMeter;
            var body = new Rect(center.x - person.x * 0.5f, center.y - person.y * 0.45f, person.x, person.y);
            var visible = Mathf.Min(body.height, radius * 2f);
            ArmoryCoreGraph.FillRect(new Rect(body.x, Mathf.Max(body.y, center.y - radius), body.width, Mathf.Min(body.yMax, center.y + radius) - Mathf.Max(body.y, center.y - radius)), new Color(0.15f, 0.16f, 0.17f));
            if (visible > 3f)
                ArmoryCoreGraph.Disc(new Vector2(center.x, Mathf.Max(body.y, center.y - radius)), Mathf.Min(person.x * 0.45f, radius), new Color(0.15f, 0.16f, 0.17f));
            ArmoryCoreGraph.FillRect(new Rect(center.x - radius, center.y, radius * 2f, 1f), Color.black);
            ArmoryCoreGraph.FillRect(new Rect(center.x, center.y - radius, 1f, radius * 2f), Color.black);
            GUI.Label(new Rect(center.x - radius, center.y + radius + 2f, radius * 2f, 14f), $"{magnification:0.#}×", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawEffect(ArmoryCoreAttachmentDefinition definition, int number)
        {
            if (!Step("effect", number, "What it changes", ArmoryCoreSetupGUI.StepState.Done, $"{Changes(definition).Count()} changes", "Every modifier against the bare weapon"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                Modifier("Recoil", definition.recoilMultiplier, false);
                Modifier("Spread", definition.spreadMultiplier, false);
                Modifier("Muzzle velocity", definition.muzzleVelocityMultiplier, true);
                Modifier("Aim time", definition.aimTimeMultiplier, false);
                Modifier("Reload time", definition.reloadTimeMultiplier, false);
                Modifier("Reliability", definition.reliabilityMultiplier, true);
                Modifier("Loudness", definition.soundPressureMultiplier, false);
                Modifier("Muzzle flash", definition.muzzleFlashMultiplier, false);
                ArmoryCoreGraph.Hint($"Adds <b>{definition.massKilograms:0.###} kg</b>. The bar is half full when nothing changes; green is better, red worse.");
            }
        }

        private static void Modifier(string title, float multiplier, bool higherIsBetter)
        {
            var better = higherIsBetter ? multiplier > 1.001f : multiplier < 0.999f;
            var worse = higherIsBetter ? multiplier < 0.999f : multiplier > 1.001f;
            ArmoryCoreGraph.Bar(title, Mathf.Clamp01(multiplier * 0.5f), better ? ArmoryCoreGraph.Good : worse ? ArmoryCoreGraph.Bad : ArmoryCoreGraph.Muted,
                ArmoryCoreDefinitionEditorUtility.FormatModifier(multiplier));
        }

        private static List<string> Validate(ArmoryCoreAttachmentDefinition definition)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(definition.id))
                issues.Add("The attachment ID is empty.");
            if (string.IsNullOrWhiteSpace(definition.displayName))
                issues.Add("The display name is empty.");
            if (definition.slot == ArmoryCoreAttachmentSlot.Sights && definition.maximumMagnification < definition.minimumMagnification)
                issues.Add("The highest magnification is below the lowest.");
            if (definition.suppressesMuzzleBlast && definition.soundPressureMultiplier >= 1f)
                issues.Add("It suppresses the muzzle blast but does not reduce the loudness.");
            return issues;
        }

        // ---------- Several at once ----------

        private void DrawGroups()
        {
            DrawSection(ref showIdentity, "Identity and compatibility", new[] { "id", "displayName", "slot", "requiredWeaponTags" });
            DrawSection(ref showPhysical, "Physical and handling", new[]
            {
                "massKilograms", "recoilMultiplier", "spreadMultiplier",
                "muzzleVelocityMultiplier", "reloadTimeMultiplier", "aimTimeMultiplier",
                "reliabilityMultiplier"
            });
            DrawSection(ref showSignature, "Muzzle signature", new[] { "suppressesMuzzleBlast", "soundPressureMultiplier", "muzzleFlashMultiplier" });
            DrawSection(ref showOptic, "Optic", new[] { "minimumMagnification", "maximumMagnification", "defaultZeroDistanceMeters" });
        }

        private void DrawSection(ref bool expanded, string label, string[] properties)
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(ref expanded, label))
            {
                for (var index = 0; index < properties.Length; index++)
                    ArmoryCoreDefinitionEditorUtility.DrawProperty(serializedObject, properties[index]);
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(expanded);
        }
    }
}
