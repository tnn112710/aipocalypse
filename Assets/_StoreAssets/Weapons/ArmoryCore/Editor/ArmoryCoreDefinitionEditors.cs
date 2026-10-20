using System;
using System.Collections.Generic;
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

    [CustomEditor(typeof(ArmoryCoreWeaponDefinition))]
    [CanEditMultipleObjects]
    public sealed class ArmoryCoreWeaponDefinitionEditor : Editor
    {
        private enum ConfigurationPage
        {
            Setup,
            Ballistics,
            Handling,
            Systems
        }

        private const string SelectedPageSessionKey = "ArmoryCore.WeaponDefinitionEditor.SelectedPage";
        private static readonly string[] PageLabels = { "Setup", "Ballistics", "Handling", "Systems" };
        private ConfigurationPage selectedPage;
        private bool showIdentity = true;
        private bool showMechanism = true;
        private bool showFeeding = true;
        private bool showShot = true;
        private bool showRecoil;
        private bool showHandling;
        private bool showReliability;
        private bool showCompatibility;
        private bool showAudio;

        private void OnEnable()
        {
            selectedPage = (ConfigurationPage)Mathf.Clamp(
                SessionState.GetInt(SelectedPageSessionKey, (int)ConfigurationPage.Setup),
                (int)ConfigurationPage.Setup,
                (int)ConfigurationPage.Systems);
        }

        public override void OnInspectorGUI()
        {
            DrawInspector(false);
        }

        public void DrawEmbeddedInspector()
        {
            DrawInspector(true);
        }

        private void DrawInspector(bool embedded)
        {
            serializedObject.Update();
            var definition = (ArmoryCoreWeaponDefinition)target;
            if (!embedded)
            {
                ArmoryCoreDefinitionEditorUtility.DrawWeaponHeader(
                    definition,
                    serializedObject.isEditingMultipleObjects,
                    targets.Length);

                if (!serializedObject.isEditingMultipleObjects)
                {
                    DrawDiagnostics(definition);
                    DrawPerformanceOverview(definition);
                }
            }

            DrawNavigation();
            switch (selectedPage)
            {
                case ConfigurationPage.Setup:
                    DrawPageIntro("Core setup", "Identity, operating mechanism, fire modes, and ammunition feed.");
                    DrawIdentity();
                    DrawMechanism(definition);
                    DrawFeeding();
                    break;
                case ConfigurationPage.Ballistics:
                    DrawPageIntro("Ballistics", "Projectile output, dispersion, recoil, and recovery behavior.");
                    DrawShot();
                    DrawRecoil();
                    break;
                case ConfigurationPage.Handling:
                    DrawPageIntro("Handling", "Weapon weight, aiming speed, and reload timings.");
                    DrawHandling(definition);
                    break;
                case ConfigurationPage.Systems:
                    DrawPageIntro("Supporting systems", "Heat, reliability, attachment compatibility, and audio presentation.");
                    DrawReliability();
                    DrawCompatibility();
                    DrawAudio();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawNavigation()
        {
            EditorGUILayout.LabelField("CONFIGURATION", EditorStyles.miniBoldLabel);
            var nextPage = (ConfigurationPage)GUILayout.Toolbar(
                (int)selectedPage,
                PageLabels,
                GUILayout.Height(25f));
            if (nextPage != selectedPage)
            {
                selectedPage = nextPage;
                SessionState.SetInt(SelectedPageSessionKey, (int)selectedPage);
            }
            EditorGUILayout.Space(2f);
        }

        private static void DrawPageIntro(string title, string description)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(1f);
        }

        private void DrawIdentity()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showIdentity,
                    "Identity",
                    "Stable IDs are used by compatibility checks; the display name is safe to change."))
            {
                DrawProperty("id", "Weapon ID", "A stable unique identifier used by definitions and compatibility rules.");
                DrawProperty("displayName", "Display Name", "The player-facing weapon name.");
                DrawProperty("weaponTags", "Weapon Tags", "Tags required by compatible attachments.");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showIdentity);
        }

        private void DrawMechanism(ArmoryCoreWeaponDefinition definition)
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showMechanism,
                    "Mechanism & fire modes",
                    "Controls the firing cycle and which trigger modes are available."))
            {
                DrawProperty("action", "Action");
                DrawProperty("reloadType", "Reload Type");
                DrawFireModes();
                DrawProperty("roundsPerMinute", "Cyclic Rate (RPM)", "The theoretical number of rounds fired per minute.");
                if (serializedObject.isEditingMultipleObjects || definition.Supports(ArmoryCoreFireMode.Burst))
                    DrawProperty("burstSize", "Burst Size", "Number of shots fired by one burst trigger pull.");
                if (serializedObject.isEditingMultipleObjects
                    || definition.action == ArmoryCoreWeaponAction.Manual
                    || definition.action == ArmoryCoreWeaponAction.Pump
                    || definition.action == ArmoryCoreWeaponAction.BreakAction)
                {
                    DrawProperty("actionCycleTime", "Action Cycle Time (s)", "Delay required to cycle a manual action.");
                }
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showMechanism);
        }

        private void DrawFeeding()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showFeeding,
                    "Ammunition & feeding",
                    "Defines the cartridge, loaded capacity, and initial state."))
            {
                DrawProperty("ammunition", "Ammunition Definition", "Ballistic data used by every projectile.");
                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Capacity");
                DrawProperty("magazineCapacity", "Magazine Capacity");
                DrawProperty("chamberCapacity", "Chamber Capacity");

                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Initial state");
                DrawProperty("spawnWithFullMagazine", "Spawn With Full Magazine");
                var chamber = serializedObject.FindProperty("chamberCapacity");
                using (new EditorGUI.DisabledScope(
                           chamber != null && !chamber.hasMultipleDifferentValues && chamber.intValue == 0))
                {
                    DrawProperty("spawnWithChamberLoaded", "Spawn With Chamber Loaded");
                }
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showFeeding);
        }

        private void DrawShot()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showShot,
                    "Projectile & accuracy",
                    "Base damage and velocity are applied before attachment modifiers."))
            {
                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Projectile output");
                DrawProperty("projectilesPerShot", "Projectiles Per Shot");
                DrawProperty("damagePerProjectile", "Damage Per Projectile");
                DrawProperty("muzzleVelocityMultiplier", "Muzzle Velocity Multiplier");

                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Dispersion");
                DrawProperty("accuracyMinutesOfAngle", "Base Accuracy (MOA)");
                DrawProperty("hipFireSpreadMultiplier", "Hip-fire Spread Multiplier");
                DrawProperty("movingSpreadMultiplier", "Moving Spread Multiplier");
                DrawProperty("heatSpreadMultiplier", "Heat Spread Multiplier");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showShot);
        }

        private void DrawRecoil()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showRecoil,
                    "Recoil & recovery",
                    "Kick is expressed in degrees; recovery starts after the configured delay."))
            {
                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Kick");
                DrawProperty("verticalRecoilDegrees", "Vertical Recoil (°)");
                DrawProperty("horizontalRecoilDegrees", "Horizontal Recoil (°)");
                DrawProperty("recoilRandomness", "Recoil Randomness");

                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Recovery");
                DrawProperty("recoilRecoveryDegreesPerSecond", "Recovery Speed (°/s)");
                DrawProperty("recoilRecoveryDelay", "Recovery Delay (s)");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showRecoil);
        }

        private void DrawHandling(ArmoryCoreWeaponDefinition definition)
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showHandling,
                    "Weight, aim & reload",
                    "Timings are base values before attachment and runtime modifiers."))
            {
                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Movement & aim");
                DrawProperty("weaponMassKilograms", "Weapon Mass (kg)");
                DrawProperty("aimDownSightsTime", "Aim-down-sights Time (s)");

                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Reload timing");
                if (serializedObject.isEditingMultipleObjects
                    || definition.reloadType == ArmoryCoreReloadType.DetachableMagazine
                    || definition.reloadType == ArmoryCoreReloadType.BeltFed)
                {
                    DrawProperty("tacticalReloadTime", "Tactical Reload Time (s)");
                    DrawProperty("emptyReloadTime", "Empty Reload Time (s)");
                }
                if (serializedObject.isEditingMultipleObjects
                    || definition.reloadType == ArmoryCoreReloadType.InternalMagazine
                    || definition.reloadType == ArmoryCoreReloadType.SingleRound)
                {
                    DrawProperty("perRoundReloadTime", "Per-round Reload Time (s)");
                }
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showHandling);
        }

        private void DrawReliability()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showReliability,
                    "Reliability & heat",
                    "Heat and fouling accumulate per shot and influence accuracy and stoppages."))
            {
                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Reliability");
                DrawProperty("meanRoundsBetweenFailures", "Mean Rounds Between Failures");
                DrawProperty("durabilityLossPerShot", "Durability Loss Per Shot");
                DrawProperty("foulingPerShot", "Fouling Per Shot");

                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Heat");
                DrawProperty("heatPerShot", "Heat Per Shot");
                DrawProperty("heatDissipationPerSecond", "Heat Dissipation / Second");
                DrawProperty("heatForAccuracyPenalty", "Accuracy Penalty Threshold");
                DrawProperty("heatForCookOff", "Cook-off Threshold");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showReliability);
        }

        private void DrawCompatibility()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showCompatibility,
                    "Attachment compatibility",
                    "Only attachment IDs in this allow-list can be installed on the weapon."))
            {
                DrawProperty("compatibleAttachmentIds", "Compatible Attachment IDs");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showCompatibility);
        }

        private void DrawAudio()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showAudio,
                    "Audio",
                    "Assign source clips and tune their mix and 3D audible range."))
            {
                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Clips");
                DrawProperty("shotSound", "Shot");
                DrawProperty("suppressedShotSound", "Suppressed Shot");
                DrawProperty("reloadSound", "Reload");
                DrawProperty("emptySound", "Empty Trigger");

                ArmoryCoreDefinitionEditorUtility.DrawSubheading("Mix");
                DrawProperty("shotVolume", "Shot Volume");
                DrawProperty("reloadVolume", "Reload Volume");
                DrawProperty("emptyVolume", "Empty-trigger Volume");
                DrawProperty("shotPitchVariation", "Shot Pitch Variation");

                ArmoryCoreDefinitionEditorUtility.DrawSubheading("3D falloff");
                DrawProperty("audioMinDistanceMeters", "Minimum Distance (m)");
                DrawProperty("audioMaxDistanceMeters", "Maximum Distance (m)");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showAudio);
        }

        private void DrawProperty(string name, string label, string tooltip = null)
        {
            ArmoryCoreDefinitionEditorUtility.DrawProperty(
                serializedObject,
                name,
                new GUIContent(label, tooltip));
        }

        private void DrawFireModes()
        {
            var property = serializedObject.FindProperty("fireModes");
            if (property == null)
                return;
            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.PropertyField(property, new GUIContent("Fire Modes"), true);
                return;
            }

            EditorGUILayout.LabelField(new GUIContent("Fire Modes", "Click to enable the modes available to this weapon."));
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawFireModeToggle(property, ArmoryCoreFireMode.Safe, "Safe");
                DrawFireModeToggle(property, ArmoryCoreFireMode.SemiAutomatic, "Semi");
                DrawFireModeToggle(property, ArmoryCoreFireMode.Burst, "Burst");
                DrawFireModeToggle(property, ArmoryCoreFireMode.Automatic, "Auto");
            }
        }

        private static void DrawFireModeToggle(SerializedProperty property, ArmoryCoreFireMode mode, string label)
        {
            var enabled = ContainsEnumValue(property, (int)mode);
            var next = GUILayout.Toggle(
                enabled,
                new GUIContent(label, $"Enable {ObjectNames.NicifyVariableName(mode.ToString())} mode"),
                EditorStyles.miniButton);
            if (next == enabled)
                return;

            if (next)
            {
                var newIndex = property.arraySize;
                property.arraySize = newIndex + 1;
                var element = property.GetArrayElementAtIndex(newIndex);
                element.enumValueIndex = (int)mode;
                SortEnumArray(property);
                return;
            }

            for (var index = property.arraySize - 1; index >= 0; index--)
            {
                if (property.GetArrayElementAtIndex(index).enumValueIndex == (int)mode)
                    property.DeleteArrayElementAtIndex(index);
            }
        }

        private static bool ContainsEnumValue(SerializedProperty property, int value)
        {
            for (var index = 0; index < property.arraySize; index++)
            {
                if (property.GetArrayElementAtIndex(index).enumValueIndex == value)
                    return true;
            }
            return false;
        }

        private static void SortEnumArray(SerializedProperty property)
        {
            for (var left = 0; left < property.arraySize - 1; left++)
            {
                for (var right = left + 1; right < property.arraySize; right++)
                {
                    var leftProperty = property.GetArrayElementAtIndex(left);
                    var rightProperty = property.GetArrayElementAtIndex(right);
                    if (leftProperty.enumValueIndex <= rightProperty.enumValueIndex)
                        continue;
                    var value = leftProperty.enumValueIndex;
                    leftProperty.enumValueIndex = rightProperty.enumValueIndex;
                    rightProperty.enumValueIndex = value;
                }
            }
        }

        private static void DrawDiagnostics(ArmoryCoreWeaponDefinition definition)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(definition.id))
                issues.Add("ID is empty.");
            if (string.IsNullOrWhiteSpace(definition.displayName))
                issues.Add("Display Name is empty.");
            if (definition.ammunition == null)
                issues.Add("Ammunition is not assigned.");
            if (definition.fireModes == null || definition.fireModes.Length == 0)
            {
                issues.Add("At least one fire mode is required.");
            }
            else
            {
                var modes = new HashSet<ArmoryCoreFireMode>();
                for (var index = 0; index < definition.fireModes.Length; index++)
                {
                    if (!modes.Add(definition.fireModes[index]))
                    {
                        issues.Add("Fire Modes contains duplicate values.");
                        break;
                    }
                }
            }
            if (definition.spawnWithChamberLoaded && definition.chamberCapacity == 0)
                issues.Add("Spawn With Chamber Loaded has no effect when Chamber Capacity is zero.");
            if (definition.heatForCookOff <= definition.heatForAccuracyPenalty)
                issues.Add("Cook-off heat should normally be above the accuracy-penalty threshold.");
            if (definition.shotSound == null)
                issues.Add("Shot Sound is not assigned.");

            EditorGUILayout.HelpBox(
                issues.Count == 0 ? "Ready for use · no configuration issues found." : string.Join("\n", issues),
                issues.Count == 0 ? MessageType.Info : MessageType.Warning);
        }

        private static void DrawPerformanceOverview(ArmoryCoreWeaponDefinition definition)
        {
            var loadedRounds = definition.magazineCapacity + definition.chamberCapacity;
            var damage = definition.damagePerProjectile * definition.projectilesPerShot;
            var velocity = definition.ammunition == null
                ? 0f
                : definition.ammunition.muzzleVelocityMetersPerSecond * definition.muzzleVelocityMultiplier;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("AT A GLANCE", EditorStyles.miniBoldLabel);
            if (EditorGUIUtility.currentViewWidth >= 520f)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawOverviewMetrics(definition, loadedRounds, damage, velocity);
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    ArmoryCoreDefinitionEditorUtility.DrawMetric("Rate", $"{definition.roundsPerMinute:0} rpm");
                    ArmoryCoreDefinitionEditorUtility.DrawMetric("Loaded", $"{loadedRounds} rounds");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    ArmoryCoreDefinitionEditorUtility.DrawMetric("Trigger damage", $"{damage:0.##}");
                    ArmoryCoreDefinitionEditorUtility.DrawMetric(
                        "Velocity",
                        definition.ammunition == null ? "No ammo" : $"{velocity:0} m/s");
                }
            }
            EditorGUILayout.Space(5f);
        }

        private static void DrawOverviewMetrics(
            ArmoryCoreWeaponDefinition definition,
            int loadedRounds,
            float damage,
            float velocity)
        {
            ArmoryCoreDefinitionEditorUtility.DrawMetric("Rate", $"{definition.roundsPerMinute:0} rpm");
            ArmoryCoreDefinitionEditorUtility.DrawMetric("Loaded", $"{loadedRounds} rounds");
            ArmoryCoreDefinitionEditorUtility.DrawMetric("Trigger damage", $"{damage:0.##}");
            ArmoryCoreDefinitionEditorUtility.DrawMetric(
                "Velocity",
                definition.ammunition == null ? "No ammo" : $"{velocity:0} m/s");
        }
    }

    [CustomEditor(typeof(ArmoryCoreAttachmentDefinition))]
    [CanEditMultipleObjects]
    public sealed class ArmoryCoreAttachmentDefinitionEditor : Editor
    {
        private bool showIdentity = true;
        private bool showPhysical = true;
        private bool showSignature;
        private bool showOptic = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var definition = (ArmoryCoreAttachmentDefinition)target;
            var title = string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.name
                : definition.displayName;
            ArmoryCoreDefinitionEditorUtility.DrawTitle(
                title,
                "Attachment compatibility and effective stat modifiers");

            if (!serializedObject.isEditingMultipleObjects)
                DrawDiagnostics(definition);

            if (ArmoryCoreDefinitionEditorUtility.BeginSection(ref showIdentity, "Identity and compatibility"))
            {
                ArmoryCoreDefinitionEditorUtility.DrawProperty(serializedObject, "id");
                ArmoryCoreDefinitionEditorUtility.DrawProperty(serializedObject, "displayName");
                ArmoryCoreDefinitionEditorUtility.DrawProperty(serializedObject, "slot");
                ArmoryCoreDefinitionEditorUtility.DrawProperty(serializedObject, "requiredWeaponTags");
                ArmoryCoreDefinitionEditorUtility.DrawIdentityTools(targets);
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showIdentity);

            DrawSection(ref showPhysical, "Physical and handling", new[]
            {
                "massKilograms", "recoilMultiplier", "spreadMultiplier",
                "muzzleVelocityMultiplier", "reloadTimeMultiplier", "aimTimeMultiplier",
                "reliabilityMultiplier"
            });
            DrawSection(ref showSignature, "Muzzle signature", new[]
            {
                "suppressesMuzzleBlast", "soundPressureMultiplier", "muzzleFlashMultiplier"
            });

            if (serializedObject.isEditingMultipleObjects || definition.slot == ArmoryCoreAttachmentSlot.Sights)
            {
                DrawSection(ref showOptic, "Optic", new[]
                {
                    "minimumMagnification", "maximumMagnification", "defaultZeroDistanceMeters"
                });
            }

            serializedObject.ApplyModifiedProperties();
            if (!serializedObject.isEditingMultipleObjects)
                DrawModifierSummary(definition);
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

        private static void DrawDiagnostics(ArmoryCoreAttachmentDefinition definition)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(definition.id))
                issues.Add("ID is empty.");
            if (string.IsNullOrWhiteSpace(definition.displayName))
                issues.Add("Display Name is empty.");
            if (definition.slot == ArmoryCoreAttachmentSlot.Sights
                && definition.maximumMagnification < definition.minimumMagnification)
            {
                issues.Add("Maximum Magnification must be greater than or equal to Minimum Magnification.");
            }
            if (definition.suppressesMuzzleBlast && definition.soundPressureMultiplier >= 1f)
                issues.Add("This attachment suppresses muzzle blast but does not reduce sound pressure.");

            EditorGUILayout.HelpBox(
                issues.Count == 0 ? "Definition is ready for use." : string.Join("\n", issues),
                issues.Count == 0 ? MessageType.Info : MessageType.Warning);
        }

        private static void DrawModifierSummary(ArmoryCoreAttachmentDefinition definition)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Modifier preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Mass", $"+{definition.massKilograms:0.###} kg");
                EditorGUILayout.LabelField("Recoil",
                    ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.recoilMultiplier));
                EditorGUILayout.LabelField("Spread",
                    ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.spreadMultiplier));
                EditorGUILayout.LabelField("Muzzle velocity",
                    ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.muzzleVelocityMultiplier));
                EditorGUILayout.LabelField("Reload time",
                    ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.reloadTimeMultiplier));
                EditorGUILayout.LabelField("Aim time",
                    ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.aimTimeMultiplier));
                if (definition.slot == ArmoryCoreAttachmentSlot.Sights)
                {
                    EditorGUILayout.LabelField("Magnification",
                        $"{definition.GameplayMinimumMagnification:0.##}x – "
                        + $"{definition.GameplayMaximumMagnification:0.##}x");
                    EditorGUILayout.LabelField("Default zero",
                        $"{definition.defaultZeroDistanceMeters:0.#} m");
                }
            }
        }
    }

    [CustomEditor(typeof(ArmoryCoreAmmunitionDefinition))]
    [CanEditMultipleObjects]
    public sealed class ArmoryCoreAmmunitionDefinitionEditor : Editor
    {
        private bool showIdentity = true;
        private bool showExternal = true;
        private bool showTerminal = true;
        private bool showSimulation;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var definition = (ArmoryCoreAmmunitionDefinition)target;
            var title = string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.name
                : definition.displayName;
            ArmoryCoreDefinitionEditorUtility.DrawTitle(
                title,
                "Projectile dimensions, external ballistics, terminal behavior, and simulation quality");

            if (!serializedObject.isEditingMultipleObjects)
                DrawDiagnostics(definition);

            if (ArmoryCoreDefinitionEditorUtility.BeginSection(ref showIdentity, "Identity"))
            {
                ArmoryCoreDefinitionEditorUtility.DrawProperty(serializedObject, "id");
                ArmoryCoreDefinitionEditorUtility.DrawProperty(serializedObject, "displayName");
                ArmoryCoreDefinitionEditorUtility.DrawIdentityTools(targets);
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showIdentity);

            DrawSection(ref showExternal, "External ballistics", new[]
            {
                "projectileMassGrams", "projectileDiameterMillimeters",
                "muzzleVelocityMetersPerSecond", "aerodynamicDragCoefficient",
                "gravityMultiplier", "maximumFlightTime", "maximumRangeMeters",
                "minimumLethalVelocity"
            });
            DrawSection(ref showTerminal, "Terminal ballistics", new[]
            {
                "retainedDamageAtMinimumVelocity", "penetrationMultiplier",
                "ricochetEnergyRetention"
            });
            DrawSection(ref showSimulation, "Simulation quality", new[]
            {
                "maximumSegmentLengthMeters", "maximumSubstepsPerFrame"
            });

            serializedObject.ApplyModifiedProperties();
            if (!serializedObject.isEditingMultipleObjects)
                DrawCalculatedSummary(definition);
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

        private static void DrawDiagnostics(ArmoryCoreAmmunitionDefinition definition)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(definition.id))
                issues.Add("ID is empty.");
            if (string.IsNullOrWhiteSpace(definition.displayName))
                issues.Add("Display Name is empty.");
            if (definition.maximumSegmentLengthMeters * definition.maximumSubstepsPerFrame
                < definition.muzzleVelocityMetersPerSecond * (1f / 30f))
            {
                issues.Add("At 30 FPS, the substep budget may be too low for the configured muzzle velocity.");
            }

            EditorGUILayout.HelpBox(
                issues.Count == 0 ? "Definition is ready for use." : string.Join("\n", issues),
                issues.Count == 0 ? MessageType.Info : MessageType.Warning);
        }

        private static void DrawCalculatedSummary(ArmoryCoreAmmunitionDefinition definition)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Calculated preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var velocity = definition.muzzleVelocityMetersPerSecond;
                var momentum = definition.ProjectileMassKilograms * velocity;
                EditorGUILayout.LabelField("Projectile mass",
                    $"{definition.projectileMassGrams:0.###} g · {definition.ProjectileMassKilograms:0.######} kg");
                EditorGUILayout.LabelField("Cross-section",
                    $"{definition.CrossSectionSquareMeters * 1000000f:0.###} mm²");
                EditorGUILayout.LabelField("Muzzle energy", $"{definition.KineticEnergy(velocity):0} J");
                EditorGUILayout.LabelField("Muzzle momentum", $"{momentum:0.###} kg·m/s");
            }
        }
    }
}
