using System;
using System.Collections.Generic;
using ArmoryCore;
using UnityEditor;
using UnityEngine;

namespace ArmoryCoreEditor
{
    [CustomEditor(typeof(ArmoryCoreWeaponController))]
    [CanEditMultipleObjects]
    public sealed class ArmoryCoreWeaponControllerEditor : Editor
    {
        private enum InspectorPage
        {
            Weapon,
            Control,
            Effects,
            Attachments,
            Events
        }

        private const string SelectedPageSessionKey = "ArmoryCore.WeaponControllerEditor.SelectedPage";
        private static readonly string[] PageLabels = { "Weapon", "Control", "Effects", "Mods", "Events" };

        private InspectorPage selectedPage;
        private Editor definitionEditor;
        private ArmoryCoreWeaponDefinition definitionEditorTarget;


        private void OnEnable()
        {
            selectedPage = (InspectorPage)Mathf.Clamp(
                SessionState.GetInt(SelectedPageSessionKey, (int)InspectorPage.Weapon),
                (int)InspectorPage.Weapon,
                (int)InspectorPage.Events);
        }

        private void OnDisable()
        {
            if (definitionEditor != null)
                UnityEngine.Object.DestroyImmediate(definitionEditor);
            definitionEditor = null;
            definitionEditorTarget = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var weapon = (ArmoryCoreWeaponController)target;

            DrawHeader(weapon);
            DrawDiagnostics(weapon);
            if (!serializedObject.isEditingMultipleObjects)
                DrawOverview(weapon);
            if (Application.isPlaying && !serializedObject.isEditingMultipleObjects)
                DrawRuntimePanel(weapon);

            DrawNavigation();
            switch (selectedPage)
            {
                case InspectorPage.Weapon:
                    DrawWeaponPage();
                    break;
                case InspectorPage.Control:
                    DrawControlPage();
                    break;
                case InspectorPage.Effects:
                    DrawEffectsPage();
                    break;
                case InspectorPage.Attachments:
                    DrawAttachmentsPage(weapon);
                    break;
                case InspectorPage.Events:
                    DrawEventsPage();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawHeader(ArmoryCoreWeaponController weapon)
        {
            var title = weapon.Definition == null
                ? "ArmoryCore Weapon"
                : string.IsNullOrWhiteSpace(weapon.Definition.displayName)
                    ? weapon.Definition.name
                    : weapon.Definition.displayName;
            var subtitle = weapon.Definition == null
                ? "Complete weapon setup, behavior, presentation, and integration"
                : $"{ObjectNames.NicifyVariableName(weapon.Definition.action.ToString())}  •  "
                  + $"{ObjectNames.NicifyVariableName(weapon.Definition.reloadType.ToString())}  •  "
                  + "scene controller";
            ArmoryCoreDefinitionEditorUtility.DrawTitle(title, subtitle);
        }

        private void DrawDiagnostics(ArmoryCoreWeaponController weapon)
        {
            if (serializedObject.isEditingMultipleObjects)
                return;

            var issues = new List<string>();
            if (weapon.Definition == null)
                issues.Add("Weapon Definition is not assigned.");
            else if (weapon.Definition.ammunition == null)
                issues.Add("Ammunition is not assigned in the Weapon Definition.");
            if (weapon.Muzzle == null)
                issues.Add("Muzzle transform is not assigned.");
            else if (weapon.Muzzle.localPosition.sqrMagnitude < 0.000001f)
                issues.Add("Muzzle is at the weapon origin. Open the prefab and position it manually.");
            if (weapon.AudioSource == null)
                issues.Add("Audio Source is not assigned (shooting still works silently).");

            var processInput = serializedObject.FindProperty("processPlayerInput");
            var autoFindCamera = serializedObject.FindProperty("autoFindMainCamera");
            var aimCamera = serializedObject.FindProperty("aimCamera");
            if (processInput != null && processInput.boolValue
                && autoFindCamera != null && !autoFindCamera.boolValue
                && aimCamera != null && aimCamera.objectReferenceValue == null)
            {
                issues.Add("Player input is enabled, but no Aim Camera is assigned and automatic lookup is disabled.");
            }

            var hipFov = serializedObject.FindProperty("hipFieldOfView");
            var aimFov = serializedObject.FindProperty("aimFieldOfView");
            if (hipFov != null && aimFov != null && aimFov.floatValue >= hipFov.floatValue)
                issues.Add("Aim Field Of View should normally be lower than Hip Field Of View.");

            var casingPrefab = serializedObject.FindProperty("casingPrefab");
            var ejectionPoint = serializedObject.FindProperty("casingEjectionPoint");
            if (casingPrefab?.objectReferenceValue != null && ejectionPoint?.objectReferenceValue == null)
                issues.Add("A Casing Prefab is assigned without a Casing Ejection Point.");

            if (issues.Count == 0)
                EditorGUILayout.HelpBox("Ready to fire · required configuration is complete.", MessageType.Info);
            else
                EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Warning);
        }

        private static void DrawOverview(ArmoryCoreWeaponController weapon)
        {
            if (weapon.Definition == null)
                return;

            var definition = weapon.Definition;
            var loaded = definition.magazineCapacity + definition.chamberCapacity;
            var damage = definition.damagePerProjectile * definition.projectilesPerShot;
            var activeMods = CountActiveAttachments(weapon);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("AT A GLANCE", EditorStyles.miniBoldLabel);
            if (EditorGUIUtility.currentViewWidth >= 520f)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawOverviewMetrics(definition, loaded, damage, activeMods);
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    ArmoryCoreDefinitionEditorUtility.DrawMetric("Rate", $"{definition.roundsPerMinute:0} rpm");
                    ArmoryCoreDefinitionEditorUtility.DrawMetric("Loaded", $"{loaded} rounds");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    ArmoryCoreDefinitionEditorUtility.DrawMetric("Trigger damage", $"{damage:0.##}");
                    ArmoryCoreDefinitionEditorUtility.DrawMetric("Active mods", activeMods.ToString());
                }
            }
            EditorGUILayout.Space(5f);
        }

        private static void DrawOverviewMetrics(
            ArmoryCoreWeaponDefinition definition,
            int loaded,
            float damage,
            int activeMods)
        {
            ArmoryCoreDefinitionEditorUtility.DrawMetric("Rate", $"{definition.roundsPerMinute:0} rpm");
            ArmoryCoreDefinitionEditorUtility.DrawMetric("Loaded", $"{loaded} rounds");
            ArmoryCoreDefinitionEditorUtility.DrawMetric("Trigger damage", $"{damage:0.##}");
            ArmoryCoreDefinitionEditorUtility.DrawMetric("Active mods", activeMods.ToString());
        }

        private static int CountActiveAttachments(ArmoryCoreWeaponController weapon)
        {
            var count = 0;
            var slots = new[]
            {
                ArmoryCoreAttachmentSlot.Sights,
                ArmoryCoreAttachmentSlot.Barrel,
                ArmoryCoreAttachmentSlot.Grip,
                ArmoryCoreAttachmentSlot.Stock
            };
            for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                var root = weapon.GetAttachmentSlotRoot(slots[slotIndex]);
                if (root == null)
                    continue;
                for (var childIndex = 0; childIndex < root.childCount; childIndex++)
                {
                    if (root.GetChild(childIndex).gameObject.activeSelf)
                        count++;
                }
            }
            return count;
        }

        private void DrawNavigation()
        {
            EditorGUILayout.LabelField("CONFIGURATION", EditorStyles.miniBoldLabel);
            var nextPage = (InspectorPage)GUILayout.Toolbar(
                (int)selectedPage,
                PageLabels,
                GUILayout.Height(26f));
            if (nextPage != selectedPage)
            {
                selectedPage = nextPage;
                SessionState.SetInt(SelectedPageSessionKey, (int)selectedPage);
            }
            EditorGUILayout.Space(3f);
        }

        private static void DrawPageIntro(string title, string description)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2f);
        }

        private static readonly ArmoryCoreGraph.Spec HipFovScale = new ArmoryCoreGraph.Spec(20f, 120f, "°", 1f, "0").Captions("narrow", "wide").Limits(20f, 120f)
            .With(ArmoryCoreGraph.M(60f, "standard"), ArmoryCoreGraph.M(90f, "wide"));
        private static readonly ArmoryCoreGraph.Spec AimFovScale = new ArmoryCoreGraph.Spec(5f, 100f, "°", 1f, "0").Captions("zoomed", "wide").Limits(5f, 100f)
            .With(ArmoryCoreGraph.M(42f, "standard"));
        private static readonly ArmoryCoreGraph.Spec FovSpeedScale = new ArmoryCoreGraph.Spec(1f, 40f, "", 1f, "0.#", true).Captions("smooth", "instant").Limits(0.01f)
            .With(ArmoryCoreGraph.M(14f, "standard"));
        internal static readonly ArmoryCoreGraph.Spec FlashScale = new ArmoryCoreGraph.Spec(0f, 20f, "particles", 1f, "0").Captions("none", "big").Limits(0f).With(ArmoryCoreGraph.M(4f, "standard"));
        internal static readonly ArmoryCoreGraph.Spec LightScale = new ArmoryCoreGraph.Spec(0f, 0.2f, "ms", 1000f, "0").Captions("blink", "long").Limits(0f).With(ArmoryCoreGraph.M(0.045f, "standard"));
        internal static readonly ArmoryCoreGraph.Spec EjectForceScale = new ArmoryCoreGraph.Spec(0f, 8f, "N·s", 1f, "0.#").Captions("drops", "flies").Limits(0f).With(ArmoryCoreGraph.M(2.4f, "standard"));
        internal static readonly ArmoryCoreGraph.Spec EjectSpinScale = new ArmoryCoreGraph.Spec(0f, 4f, "N·m·s", 1f, "0.#").Captions("flat", "tumbles").Limits(0f).With(ArmoryCoreGraph.M(1.2f, "standard"));
        internal static readonly ArmoryCoreGraph.Spec CasingLifeScale = new ArmoryCoreGraph.Spec(1f, 60f, "s", 1f, "0", true).Captions("short", "long").Limits(0.01f).With(ArmoryCoreGraph.M(8f, "standard"));
        internal static readonly ArmoryCoreGraph.Spec KickScale = new ArmoryCoreGraph.Spec(0f, 0.01f, "mm/°", 1000f, "0.#").Captions("none", "strong").Limits(0f).With(ArmoryCoreGraph.M(0.003f, "standard"));
        private static readonly ArmoryCoreGraph.Spec ReserveScale = new ArmoryCoreGraph.Spec(0f, 600f, "rounds", 1f, "0").Captions("none", "plenty").Limits(0f)
            .With(ArmoryCoreGraph.M(120f, "standard"));

        private void DrawWeaponPage()
        {
            DrawPageIntro(
                "Weapon configuration",
                "The definition as setup steps, then the scene references of this weapon object. Weapon Setup… places the muzzle on the 3D model.");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawProperty("definition", "Definition", "Reusable gameplay configuration shared by weapon instances.");
                    using (new EditorGUI.DisabledScope(serializedObject.isEditingMultipleObjects))
                    {
                        if (ArmoryCoreSetupGUI.MiniButton("Weapon Setup…", "Open this weapon in the Weapon Setup window: muzzle and ejection port on the 3D model, cartridges and ballistics", 104f))
                            ArmoryCoreWeaponSetupWindow.Open(((ArmoryCoreWeaponController)target).gameObject);
                    }
                }
            }

            if (serializedObject.isEditingMultipleObjects)
            {
                DrawSceneSetup(1, null);
                return;
            }

            var definition = serializedObject.FindProperty("definition")?.objectReferenceValue as ArmoryCoreWeaponDefinition;
            if (definition == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Weapon Definition to set up the weapon here, or open Weapon Setup… to create one from a weapon type.",
                    MessageType.Info);
            }
            else
            {
                EnsureDefinitionEditor(definition);
                EditorGUILayout.Space(3f);
                if (definitionEditor is ArmoryCoreWeaponDefinitionEditor weaponDefinitionEditor)
                    weaponDefinitionEditor.DrawEmbeddedInspector();
                else
                    definitionEditor?.OnInspectorGUI();
                EditorGUILayout.Space(3f);
            }
            DrawSceneSetup(10, (ArmoryCoreWeaponController)target);
        }

        private void DrawSceneSetup(int number, ArmoryCoreWeaponController weapon)
        {
            var muzzle = serializedObject.FindProperty("muzzle");
            var placed = muzzle.objectReferenceValue is Transform point && point.localPosition.sqrMagnitude > 0.000001f;
            var muzzleState = muzzle.hasMultipleDifferentValues || placed ? ArmoryCoreSetupGUI.StepState.Done : ArmoryCoreSetupGUI.StepState.Current;
            var muzzleSummary = muzzle.hasMultipleDifferentValues ? "—" : placed ? "muzzle placed" : muzzle.objectReferenceValue ? "muzzle at the origin" : "no muzzle";
            if (ArmoryCoreSetupGUI.Step("weapon.controller.step.scene", number, "Muzzle & aim", muzzleState, muzzleSummary, "Where projectiles leave and what they can hit"))
            {
                using (ArmoryCoreSetupGUI.CardBody())
                {
                    DrawProperty("muzzle", "Muzzle", "Projectile origin; its forward axis must point along the barrel.");
                    DrawProperty("aimReference", "Aim reference", "Optional fallback aiming direction when a sight is not active.");
                    DrawProperty("hitMask", "Projectile hit mask", "Physics layers that projectiles are allowed to hit.");
                    if (weapon != null && ArmoryCoreSetupGUI.Button("Place the muzzle on the 3D model…", true, "Drag the muzzle and the ejection port on the model in the Weapon Setup window"))
                        ArmoryCoreWeaponSetupWindow.Open(weapon.gameObject);
                }
            }

            var reserve = serializedObject.FindProperty("reserveRounds");
            if (ArmoryCoreSetupGUI.Step("weapon.controller.step.state", number + 1, "Initial state", ArmoryCoreSetupGUI.StepState.Done,
                    reserve.hasMultipleDifferentValues ? "—" : $"{reserve.intValue} in reserve", "Automatic initialization and the ammunition this object carries"))
            {
                using (ArmoryCoreSetupGUI.CardBody())
                {
                    ArmoryCoreGraph.ToggleTile(serializedObject.FindProperty("initializeOnAwake"), new ArmoryCoreGraph.Tile(
                        "Initialize on Awake", "ready when the scene starts", "Loads the magazine and chamber and installs attachments in Awake. Turn off when your code calls InitializeState().")
                        { Glyph = "▶", Color = ArmoryCoreGraph.Good }, 34f);
                    if (reserve.hasMultipleDifferentValues)
                        DrawProperty("reserveRounds", "Starting reserve rounds");
                    else
                        ArmoryCoreGraph.Scale(serializedObject, "reserveRounds", "Starting reserve", "Rounds carried for reloads.", ReserveScale);
                }
            }
        }

        // ---------- Shared step helpers ----------

        private bool Step(string id, int number, string title, ArmoryCoreSetupGUI.StepState state, string summary, string tooltip = null)
        {
            return ArmoryCoreSetupGUI.Step("weapon.controller.step." + id, number, title, state, summary, tooltip);
        }

        /// <summary>A scale for one object; the plain field when several objects disagree on the value.</summary>
        private void Scale(string name, string title, string tooltip, ArmoryCoreGraph.Spec spec)
        {
            var property = serializedObject.FindProperty(name);
            if (property == null)
                return;
            if (property.hasMultipleDifferentValues)
                EditorGUILayout.PropertyField(property, new GUIContent(title, tooltip));
            else
                ArmoryCoreGraph.Scale(serializedObject, name, title, tooltip, spec);
        }

        private void Toggle(string name, ArmoryCoreGraph.Tile tile, float height = 34f)
        {
            var property = serializedObject.FindProperty(name);
            if (property == null)
                return;
            if (property.hasMultipleDifferentValues)
                EditorGUILayout.PropertyField(property, new GUIContent(tile.Title, tile.Tooltip));
            else
                ArmoryCoreGraph.ToggleTile(property, tile, height);
        }

        /// <summary>Two tiles standing for false and true of a flag.</summary>
        private void Either(string name, ArmoryCoreGraph.Tile whenFalse, ArmoryCoreGraph.Tile whenTrue, float height = 36f)
        {
            var property = serializedObject.FindProperty(name);
            if (property == null)
                return;
            if (property.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(ObjectNames.NicifyVariableName(name)));
                return;
            }
            var picked = ArmoryCoreGraph.Choice(property.boolValue ? 1 : 0, new[] { whenFalse, whenTrue }, 2, height);
            property.boolValue = picked == 1;
        }

        private bool Assigned(string name)
        {
            var property = serializedObject.FindProperty(name);
            return property != null && (property.hasMultipleDifferentValues || property.objectReferenceValue != null);
        }

        private bool Flag(string name)
        {
            var property = serializedObject.FindProperty(name);
            return property != null && (property.hasMultipleDifferentValues || property.boolValue);
        }

        // ---------- Control ----------

        private void DrawControlPage()
        {
            DrawPageIntro(
                "Input & aiming",
                "Who drives the weapon and how aiming changes the camera.");

            var input = Flag("processPlayerInput");
            if (Step("input", 1, "Who fires", ArmoryCoreSetupGUI.StepState.Done, input ? "local player" : "your code or AI",
                    "Local input, or the public API driven by a player, AI or network script"))
            {
                using (ArmoryCoreSetupGUI.CardBody())
                {
                    Either("processPlayerInput",
                        new ArmoryCoreGraph.Tile("Code or AI", "public API", "Another script calls SetTriggerHeld, StartReload, SetAiming… Use it for AI, network and your own player controller.") { Glyph = "{ }", Color = ArmoryCoreGraph.Info },
                        new ArmoryCoreGraph.Tile("Local player", "keyboard and mouse", "The weapon reads the input itself. Enable it only on the locally controlled weapon.") { Glyph = "⌨", Color = ArmoryCoreGraph.Accent });
                    ArmoryCoreGraph.Hint(input
                        ? "LMB fire · RMB aim · R reload · B fire mode · C cycle action · T clear malfunction · F light · Z bipod/stock · wheel zoom."
                        : "Nothing is read from the keyboard: drive the weapon through its public methods.");
                }
            }

            var autoFind = Flag("autoFindMainCamera");
            var cameraSummary = (autoFind ? "main camera" : Assigned("aimCamera") ? "assigned camera" : "no camera")
                + (Flag("controlCameraFieldOfView") ? $" · {serializedObject.FindProperty("hipFieldOfView").floatValue:0}°→{serializedObject.FindProperty("aimFieldOfView").floatValue:0}°" : "");
            var cameraState = !autoFind && !Assigned("aimCamera") && input ? ArmoryCoreSetupGUI.StepState.Current : ArmoryCoreSetupGUI.StepState.Done;
            if (Step("camera", 2, "Aim camera & field of view", cameraState, cameraSummary, "The camera aimed through and how much aiming zooms"))
            {
                using (ArmoryCoreSetupGUI.CardBody())
                {
                    Either("autoFindMainCamera",
                        new ArmoryCoreGraph.Tile("Assigned camera", "set below", "Aim through the camera assigned below.") { Glyph = "◎", Color = ArmoryCoreGraph.Info },
                        new ArmoryCoreGraph.Tile("Main camera", "Camera.main at runtime", "Find the camera tagged MainCamera when the weapon starts.") { Glyph = "◉", Color = ArmoryCoreGraph.Info });
                    var find = serializedObject.FindProperty("autoFindMainCamera");
                    if (find.hasMultipleDifferentValues || !find.boolValue)
                        DrawProperty("aimCamera", "Aim camera");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Toggle("controlCameraFieldOfView", new ArmoryCoreGraph.Tile("Zoom when aiming", "changes the field of view", "The weapon narrows the camera's field of view while aiming and restores it after.") { Glyph = "⊕" });
                        Toggle("compensateSightZero", new ArmoryCoreGraph.Tile("Sight zero", "hits where the sight points", "Aims the shot up so it meets the line of sight at the sight's zero distance.") { Glyph = "⌖", Color = ArmoryCoreGraph.Good });
                    }
                    if (Flag("controlCameraFieldOfView"))
                    {
                        Scale("hipFieldOfView", "Hip field of view", "Vertical field of view without aiming.", HipFovScale);
                        Scale("aimFieldOfView", "Aim field of view", "Vertical field of view while aiming; sights with magnification narrow it further.", AimFovScale);
                        Scale("fieldOfViewSpeed", "Zoom speed", "How quickly the field of view follows.", FovSpeedScale);
                        if (!serializedObject.isEditingMultipleObjects)
                            DrawFieldOfView(serializedObject.FindProperty("hipFieldOfView").floatValue, serializedObject.FindProperty("aimFieldOfView").floatValue);
                    }
                }
            }
        }

        /// <summary>The hip and aim cones from the eye, and how many times aiming magnifies.</summary>
        private static void DrawFieldOfView(float hip, float aim)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(140f, 110f, GUILayout.ExpandWidth(true)));
            ArmoryCoreGraph.Panel(rect);
            var eye = new Vector2(rect.x + 18f, rect.center.y);
            // Both views are sectors of one radius, sized so the wider one fits: their angles compare true.
            var widest = Mathf.Clamp(Mathf.Max(hip, aim), 1f, 170f) * 0.5f * Mathf.Deg2Rad;
            var radius = Mathf.Min(rect.width * 0.55f, 200f, (rect.height * 0.5f - 6f) / Mathf.Sin(widest));
            void Cone(float degrees, Color color)
            {
                var half = Mathf.Clamp(degrees, 1f, 170f) * 0.5f * Mathf.Deg2Rad;
                const int Steps = 16;
                var fan = new Vector2[Steps + 2];
                fan[0] = eye;
                for (var i = 0; i <= Steps; i++)
                {
                    var angle = Mathf.Lerp(-half, half, i / (float)Steps);
                    fan[i + 1] = eye + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                ArmoryCoreGraph.Fill(new Color(color.r, color.g, color.b, 0.3f), fan);
                ArmoryCoreGraph.Line(eye, fan[1], color, 1.5f);
                ArmoryCoreGraph.Line(eye, fan[fan.Length - 1], color, 1.5f);
            }
            Cone(hip, ArmoryCoreGraph.Muted);
            Cone(aim, ArmoryCoreGraph.Accent);
            var reach = radius;
            ArmoryCoreGraph.Disc(eye, 4f, ArmoryCoreGraph.Ink);
            var zoom = Mathf.Tan(hip * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.001f, Mathf.Tan(aim * 0.5f * Mathf.Deg2Rad));
            var text = new Rect(eye.x + reach + 12f, rect.y + 14f, rect.xMax - eye.x - reach - 16f, 16f);
            GUI.Label(text, $"hip {hip:0}°", EditorStyles.miniLabel);
            text.y += 18f;
            GUI.Label(text, $"aimed {aim:0}°", EditorStyles.miniBoldLabel);
            text.y += 18f;
            GUI.Label(text, zoom >= 1f ? $"×{zoom:0.##} closer" : "wider when aiming!", EditorStyles.miniLabel);
        }

        // ---------- Effects ----------

        private void DrawEffectsPage()
        {
            DrawPageIntro(
                "Presentation",
                "Sound, animation, flash, casings and kick of this weapon object. Every system is optional.");

            var audio = Assigned("audioSource");
            if (Step("audio", 1, "Sound source", audio ? ArmoryCoreSetupGUI.StepState.Done : ArmoryCoreSetupGUI.StepState.Current, audio ? "assigned" : "silent",
                    "The Audio Source that plays the clips of the definition"))
            {
                using (ArmoryCoreSetupGUI.CardBody())
                {
                    DrawProperty("audioSource", "Audio source");
                    ArmoryCoreGraph.Hint("Clips, volumes and audible range are set in the definition (Weapon tab, «Sound»); the source is made 3D on start.");
                    if (!audio && !serializedObject.isEditingMultipleObjects && ArmoryCoreSetupGUI.Button("Add an Audio Source to this object"))
                    {
                        var weapon = (ArmoryCoreWeaponController)target;
                        var source = weapon.GetComponent<AudioSource>() ? weapon.GetComponent<AudioSource>() : Undo.AddComponent<AudioSource>(weapon.gameObject);
                        source.playOnAwake = false;
                        source.spatialBlend = 1f;
                        serializedObject.FindProperty("audioSource").objectReferenceValue = source;
                    }
                }
            }

            var animated = Assigned("animator");
            if (Step("animator", 2, "Animator", animated ? ArmoryCoreSetupGUI.StepState.Done : ArmoryCoreSetupGUI.StepState.Optional, animated ? "triggers and states" : "none",
                    "Animator parameters set on shots, reloads and aiming"))
            {
                using (ArmoryCoreSetupGUI.CardBody())
                {
                    DrawProperty("animator", "Animator");
                    if (animated)
                    {
                        EditorGUILayout.LabelField("Triggers", EditorStyles.miniBoldLabel);
                        DrawProperty("fireTrigger", "Fire");
                        DrawProperty("reloadTrigger", "Reload");
                        DrawProperty("emptyTrigger", "Empty trigger");
                        DrawProperty("cycleTrigger", "Cycle action");
                        DrawProperty("malfunctionTrigger", "Malfunction");
                        EditorGUILayout.LabelField("States", EditorStyles.miniBoldLabel);
                        DrawProperty("aimingParameter", "Aiming (bool)");
                        DrawProperty("fireModeParameter", "Fire mode (int)");
                    }
                    else
                    {
                        ArmoryCoreGraph.Hint("Without an Animator the weapon still fires; no animation parameters are set.");
                    }
                }
            }

            var flash = Assigned("muzzleFlash");
            var light = Assigned("muzzleLight");
            if (Step("flash", 3, "Muzzle flash & light", flash || light ? ArmoryCoreSetupGUI.StepState.Done : ArmoryCoreSetupGUI.StepState.Optional,
                    flash && light ? "particles · light" : flash ? "particles" : light ? "light" : "none", "Particles and a short light at the muzzle; muzzle devices scale them"))
            {
                using (ArmoryCoreSetupGUI.CardBody())
                {
                    DrawProperty("muzzleFlash", "Flash particles", "Particle system emitted at the muzzle on every shot.");
                    if (flash)
                        Scale("muzzleFlashParticles", "Particles per shot", null, FlashScale);
                    DrawProperty("muzzleLight", "Flash light", "Light switched on for a moment on every shot.");
                    if (light)
                        Scale("muzzleLightDuration", "Light duration", null, LightScale);
                    ArmoryCoreGraph.Hint("Suppressors and flash hiders multiply both.");
                }
            }

            var port = Assigned("casingEjectionPoint");
            var casing = Assigned("casingPrefab");
            var casingState = port && casing ? ArmoryCoreSetupGUI.StepState.Done : port || casing ? ArmoryCoreSetupGUI.StepState.Current : ArmoryCoreSetupGUI.StepState.Optional;
            if (Step("casings", 4, "Casing ejection", casingState, port && casing ? "ejects casings" : port || casing ? "incomplete" : "none",
                    "Physical casings thrown out of the ejection port"))
            {
                using (ArmoryCoreSetupGUI.CardBody())
                {
                    DrawProperty("casingEjectionPoint", "Ejection port", "Casings fly along its right axis and a little up.");
                    DrawProperty("casingPrefab", "Casing prefab", "Rigidbody prefab of a spent casing.");
                    if (port || casing)
                    {
                        Scale("casingEjectionForce", "Ejection impulse", "Impulse that throws the casing out of the port.", EjectForceScale);
                        Scale("casingEjectionTorque", "Spin", "Random spin given to the casing.", EjectSpinScale);
                        Scale("casingLifetime", "Casing lifetime", "Seconds before a casing is removed.", CasingLifeScale);
                    }
                    if (port != casing)
                        ArmoryCoreGraph.Line(port ? "No casing prefab: nothing flies out." : "No ejection port: casings have nowhere to fly from.", ArmoryCoreGraph.Warn);
                    if (!serializedObject.isEditingMultipleObjects && ArmoryCoreSetupGUI.Button("Place the ejection port on the 3D model…"))
                        ArmoryCoreWeaponSetupWindow.Open(((ArmoryCoreWeaponController)target).gameObject);
                }
            }

            var kick = Assigned("recoilTarget");
            if (Step("kick", 5, "Visual kick", kick ? ArmoryCoreSetupGUI.StepState.Done : ArmoryCoreSetupGUI.StepState.Optional, kick ? "kicks the model" : "none",
                    "Pushes and tilts a transform with the recoil, without changing where shots go"))
            {
                using (ArmoryCoreSetupGUI.CardBody())
                {
                    DrawProperty("recoilTarget", "Kick target", "Transform pushed back and tilted by recoil; usually the visible model.");
                    if (kick)
                        Scale("visualKickMetersPerDegree", "Kick distance", "How far the target moves back per degree of recoil.", KickScale);
                }
            }
        }

        // ---------- Mods ----------

        private static readonly ArmoryCoreAttachmentSlot[] Slots =
        {
            ArmoryCoreAttachmentSlot.Sights, ArmoryCoreAttachmentSlot.Barrel, ArmoryCoreAttachmentSlot.Grip, ArmoryCoreAttachmentSlot.Stock
        };

        private static readonly string[] SlotGlyphs = { "⌖", "▬", "┃", "◣" };

        private void DrawAttachmentsPage(ArmoryCoreWeaponController weapon)
        {
            DrawPageIntro(
                "Installed attachments",
                "Pick what is installed in each slot. Only attachments the definition allows are offered.");

            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox("Attachment selection is available when editing one weapon at a time.", MessageType.Info);
                return;
            }

            for (var i = 0; i < Slots.Length; i++)
                DrawSlot(weapon, Slots[i], i + 1);
            DrawAttachmentEffect(weapon, Slots.Length + 1);
        }

        private void DrawSlot(ArmoryCoreWeaponController weapon, ArmoryCoreAttachmentSlot slot, int number)
        {
            var title = ObjectNames.NicifyVariableName(slot.ToString());
            var root = weapon.GetAttachmentSlotRoot(slot);
            var options = new List<Transform> { null };
            var hidden = 0;
            var selected = 0;
            if (root != null)
            {
                for (var index = 0; index < root.childCount; index++)
                {
                    var option = root.GetChild(index);
                    var definition = ModuleDefinition(option);
                    if (definition != null && !weapon.CanInstall(definition))
                    {
                        hidden++;
                        continue;
                    }
                    options.Add(option);
                    if (option.gameObject.activeSelf)
                        selected = options.Count - 1;
                }
            }

            var state = root == null || options.Count == 1 ? ArmoryCoreSetupGUI.StepState.Locked
                : selected > 0 ? ArmoryCoreSetupGUI.StepState.Done
                : ArmoryCoreSetupGUI.StepState.Optional;
            var summary = root == null ? "no slot" : options.Count == 1 ? "nothing fits" : selected > 0 ? OptionName(options[selected]) : "empty";
            if (!Step("slot." + slot, number, title, state, summary, $"What is installed in the {title.ToLowerInvariant()} slot"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                if (root == null)
                {
                    ArmoryCoreGraph.Hint($"The weapon has no «{title}» slot object, so nothing can be installed here.");
                    return;
                }
                var glyph = SlotGlyphs[Array.IndexOf(Slots, slot)];
                var tiles = new List<ArmoryCoreGraph.Tile>
                {
                    new ArmoryCoreGraph.Tile("None", "bare weapon", "Remove whatever is installed in this slot.") { Glyph = "○", Color = ArmoryCoreGraph.Muted }
                };
                for (var i = 1; i < options.Count; i++)
                {
                    var definition = ModuleDefinition(options[i]);
                    tiles.Add(new ArmoryCoreGraph.Tile(OptionName(options[i]), Effect(definition), definition == null ? "No Attachment Definition on this object." : definition.id)
                    {
                        Glyph = glyph,
                        Color = ArmoryCoreGraph.Accent,
                        Preview = ArmoryCoreHoverPreview.PrefabOf(options[i], definition)
                    });
                }
                var picked = ArmoryCoreGraph.Choice(selected, tiles, 2, 38f);
                ArmoryCoreHoverPreview.Draw();
                if (picked != selected)
                    Install(weapon, slot, root, options[picked], title);
                if (hidden > 0)
                    ArmoryCoreGraph.Hint($"{hidden} more in the slot do not fit this definition (its attachment list or tags); allow them on the Weapon tab, «Identity & attachments».");
            }
        }

        private static ArmoryCoreAttachmentDefinition ModuleDefinition(Transform option)
        {
            var module = option == null ? null : option.GetComponent<ArmoryCoreAttachmentModule>();
            return module == null ? null : module.Definition;
        }

        private static string OptionName(Transform option)
        {
            var definition = ModuleDefinition(option);
            return definition == null || string.IsNullOrWhiteSpace(definition.displayName)
                ? ObjectNames.NicifyVariableName(option.name)
                : definition.displayName;
        }

        /// <summary>Mass and the two changes that matter most, as a tile caption.</summary>
        private static string Effect(ArmoryCoreAttachmentDefinition definition)
        {
            if (definition == null)
                return "no definition";
            var parts = new List<string>();
            if (definition.slot == ArmoryCoreAttachmentSlot.Sights && definition.GameplayMaximumMagnification > 1f)
                parts.Add($"{definition.GameplayMaximumMagnification:0.#}×");
            if (definition.suppressesMuzzleBlast)
                parts.Add("suppressed");
            void Add(string name, float multiplier)
            {
                if (Mathf.Abs(multiplier - 1f) >= 0.02f)
                    parts.Add(name + " " + ArmoryCoreDefinitionEditorUtility.FormatModifier(multiplier));
            }
            Add("recoil", definition.recoilMultiplier);
            Add("spread", definition.spreadMultiplier);
            Add("aim", definition.aimTimeMultiplier);
            Add("velocity", definition.muzzleVelocityMultiplier);
            if (parts.Count == 0)
                parts.Add("no stat change");
            return $"{definition.massKilograms:0.##} kg · " + string.Join(" · ", parts.GetRange(0, Mathf.Min(2, parts.Count)));
        }

        private static void Install(ArmoryCoreWeaponController weapon, ArmoryCoreAttachmentSlot slot, Transform root, Transform option, string title)
        {
            var objects = new UnityEngine.Object[root.childCount];
            for (var index = 0; index < root.childCount; index++)
                objects[index] = root.GetChild(index).gameObject;
            Undo.RecordObjects(objects, $"Change {title} attachment");
            var definition = ModuleDefinition(option);
            var id = option == null ? null : string.IsNullOrWhiteSpace(definition?.id) ? option.name : definition.id;
            weapon.SelectAttachment(slot, id);
            foreach (var changed in objects)
                ArmoryCoreSetupGUI.MarkChanged(changed);
        }

        /// <summary>The weapon with its installed attachments against the bare weapon.</summary>
        private void DrawAttachmentEffect(ArmoryCoreWeaponController weapon, int number)
        {
            var definition = weapon.Definition;
            var modules = new List<ArmoryCoreAttachmentModule>();
            foreach (var slot in Slots)
            {
                var root = weapon.GetAttachmentSlotRoot(slot);
                if (root == null)
                    continue;
                for (var index = 0; index < root.childCount; index++)
                {
                    var child = root.GetChild(index);
                    var module = child.gameObject.activeSelf ? child.GetComponent<ArmoryCoreAttachmentModule>() : null;
                    if (module != null)
                        modules.Add(module);
                }
            }
            var state = definition == null ? ArmoryCoreSetupGUI.StepState.Locked : ArmoryCoreSetupGUI.StepState.Done;
            if (!Step("mods.effect", number, "What the attachments change", state, definition == null ? "no definition" : $"{modules.Count} installed",
                    "Effective values with the installed attachments against the bare weapon"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                if (definition == null)
                {
                    ArmoryCoreGraph.Hint("Assign a definition on the Weapon tab first.");
                    return;
                }
                var bare = ArmoryCoreWeaponRuntimeStats.Build(definition, new List<ArmoryCoreAttachmentModule>());
                var with = ArmoryCoreWeaponRuntimeStats.Build(definition, modules);
                Compare("Mass", bare.MassKilograms, with.MassKilograms, "kg", false);
                Compare("Rate of fire", bare.RoundsPerMinute, with.RoundsPerMinute, "rpm", true);
                Compare("Muzzle velocity", bare.MuzzleVelocityMetersPerSecond, with.MuzzleVelocityMetersPerSecond, "m/s", true);
                Compare("Spread", bare.AccuracyMinutesOfAngle, with.AccuracyMinutesOfAngle, "MOA", false);
                Compare("Vertical recoil", bare.VerticalRecoilDegrees, with.VerticalRecoilDegrees, "°", false);
                Compare("Aim time", bare.AimTimeSeconds, with.AimTimeSeconds, "s", false);
                Compare("Empty reload", bare.EmptyReloadSeconds, with.EmptyReloadSeconds, "s", false);
                Compare("Rounds between stoppages", bare.MeanRoundsBetweenFailures, with.MeanRoundsBetweenFailures, "", true);
                Compare("Shot loudness", bare.SoundPressureMultiplier, with.SoundPressureMultiplier, "×", false);
                if (with.Magnification > 1f)
                    ArmoryCoreGraph.Hint($"Sight: <b>{with.Magnification:0.#}×</b>, zeroed at {with.ZeroDistanceMeters:0} m.");
                if (with.Suppressed)
                    ArmoryCoreGraph.Hint("The shot is suppressed: the definition's suppressed shot sound plays.");
            }
        }

        /// <summary>A bar of the effective value: green when the attachments make it better, red when worse, half full when unchanged.</summary>
        private static void Compare(string title, float bare, float with, string unit, bool higherIsBetter)
        {
            var ratio = with / Mathf.Max(0.0001f, bare);
            var better = higherIsBetter ? ratio > 1.001f : ratio < 0.999f;
            var worse = higherIsBetter ? ratio < 0.999f : ratio > 1.001f;
            var color = better ? ArmoryCoreGraph.Good : worse ? ArmoryCoreGraph.Bad : ArmoryCoreGraph.Muted;
            var change = better || worse ? "  (" + ArmoryCoreDefinitionEditorUtility.FormatModifier(ratio) + ")" : "";
            var format = with >= 100f ? "0" : with >= 10f ? "0.#" : "0.##";
            var suffix = unit.Length > 0 ? " " + unit : "";
            ArmoryCoreGraph.Bar(title, Mathf.Clamp01(ratio * 0.5f), color, with.ToString(format) + suffix + change,
                $"Bare weapon: {bare.ToString(format)}{suffix}. The bar is half full when nothing changes.");
        }

        // ---------- Events ----------

        private void DrawEventsPage()
        {
            DrawPageIntro(
                "Gameplay events",
                "Connect UI, animation, sound and other systems without changing the weapon controller.");

            DrawEventStep(1, "fire", "Firing & ammunition", "Every shot, an empty trigger pull and every change of the round count", new[]
            {
                ("onShot", "Shot"), ("onEmpty", "Empty trigger"), ("onAmmoChanged", "Ammo changed (in weapon, reserve)")
            });
            DrawEventStep(2, "state", "Reload & malfunctions", "Start and end of a reload, a stoppage and its clearing", new[]
            {
                ("onReloadStarted", "Reload started"), ("onReloadCompleted", "Reload completed"), ("onMalfunction", "Malfunction"), ("onMalfunctionCleared", "Malfunction cleared")
            });
            DrawEventStep(3, "aim", "Aiming", "Raising and lowering the sights", new[] { ("onAimChanged", "Aim changed (is aiming)") });
            ArmoryCoreGraph.Hint("Code can also subscribe to the ShotAttempted and RecoilApplied C# events of ArmoryCoreWeaponController.");
        }

        private void DrawEventStep(int number, string id, string title, string tooltip, (string name, string label)[] events)
        {
            var listeners = 0;
            foreach (var (name, _) in events)
                listeners += Listeners(name);
            var state = listeners > 0 ? ArmoryCoreSetupGUI.StepState.Done : ArmoryCoreSetupGUI.StepState.Optional;
            if (!Step("events." + id, number, title, state, listeners == 0 ? "not connected" : listeners == 1 ? "1 listener" : $"{listeners} listeners", tooltip))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                foreach (var (name, label) in events)
                    DrawProperty(name, label);
            }
        }

        private int Listeners(string name)
        {
            var calls = serializedObject.FindProperty(name + ".m_PersistentCalls.m_Calls");
            return calls == null || calls.hasMultipleDifferentValues ? 0 : calls.arraySize;
        }

        private void DrawProperty(string propertyName, string label, string tooltip = null)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private void EnsureDefinitionEditor(ArmoryCoreWeaponDefinition definition)
        {
            if (definitionEditor != null && definitionEditorTarget == definition)
                return;

            if (definitionEditor != null)
                UnityEngine.Object.DestroyImmediate(definitionEditor);
            definitionEditorTarget = definition;
            definitionEditor = definition == null
                ? null
                : Editor.CreateEditor(definition, typeof(ArmoryCoreWeaponDefinitionEditor));
        }

        private static void DrawRuntimePanel(ArmoryCoreWeaponController weapon)
        {
            EditorGUILayout.LabelField("LIVE STATE", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("State", $"{weapon.State} / {weapon.Malfunction}");
                EditorGUILayout.LabelField("Fire mode", ObjectNames.NicifyVariableName(weapon.FireMode.ToString()));
                EditorGUILayout.LabelField("Ammunition", $"{weapon.TotalRoundsInWeapon} loaded · {weapon.ReserveRounds} reserve");
                EditorGUILayout.LabelField(
                    "Effective stats",
                    $"{weapon.Stats.RoundsPerMinute:0} RPM · {weapon.Stats.MuzzleVelocityMetersPerSecond:0} m/s · "
                    + $"{weapon.Stats.AccuracyMinutesOfAngle:0.00} MOA · {weapon.Stats.Magnification:0.##}x");
                DrawRuntimeBar("Heat", Mathf.Clamp01(weapon.Heat / 100f), $"{weapon.Heat:0.0}");
                DrawRuntimeBar("Durability", weapon.Durability, $"{weapon.Durability:P0}");
                DrawRuntimeBar("Fouling", weapon.Fouling, $"{weapon.Fouling:P1}");
            }
            RepaintPlayingInspector();
            EditorGUILayout.Space(4f);
        }

        private static void DrawRuntimeBar(string label, float value, string text)
        {
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
            EditorGUI.ProgressBar(rect, value, text);
        }

        private static void RepaintPlayingInspector()
        {
            if (Application.isPlaying && Event.current.type == EventType.Repaint)
                EditorApplication.QueuePlayerLoopUpdate();
        }

        private void OnSceneGUI()
        {
            if (target is not ArmoryCoreWeaponController weapon || weapon.Muzzle == null)
                return;
            Handles.color = new Color(1f, 0.35f, 0.08f, 0.9f);
            var size = HandleUtility.GetHandleSize(weapon.Muzzle.position) * 0.35f;
            Handles.ArrowHandleCap(
                0,
                weapon.Muzzle.position,
                Quaternion.LookRotation(weapon.Muzzle.forward),
                size,
                EventType.Repaint);
            Handles.Label(weapon.Muzzle.position, "  Muzzle");

            var aimReferenceProperty = serializedObject.FindProperty("aimReference");
            var aimReference = aimReferenceProperty?.objectReferenceValue as Transform;
            if (aimReference == null)
                return;
            Handles.color = new Color(0.15f, 0.7f, 1f, 0.9f);
            size = HandleUtility.GetHandleSize(aimReference.position) * 0.3f;
            Handles.ArrowHandleCap(
                0,
                aimReference.position,
                Quaternion.LookRotation(aimReference.forward),
                size,
                EventType.Repaint);
            Handles.Label(aimReference.position, "  Aim Reference");
        }
    }

    [CustomEditor(typeof(ArmoryCoreAttachmentModule), true)]
    [CanEditMultipleObjects]
    public sealed class ArmoryCoreAttachmentModuleEditor : Editor
    {
        private bool showSetup = true;
        private bool showTuning = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var module = (ArmoryCoreAttachmentModule)target;

            DrawTitle(module);
            if (!serializedObject.isEditingMultipleObjects)
                DrawDiagnostics(module);
            DrawSetupTools(module);

            showSetup = EditorGUILayout.BeginFoldoutHeaderGroup(showSetup, "Module setup");
            if (showSetup)
            {
                EditorGUI.indentLevel++;
                DrawProperty("definition");
                DrawProperty("muzzlePoint");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2f);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            showTuning = EditorGUILayout.BeginFoldoutHeaderGroup(showTuning, GetTuningTitle(module));
            if (showTuning)
            {
                EditorGUI.indentLevel++;
                DrawModuleTuning(module);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2f);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();
            if (serializedObject.isEditingMultipleObjects)
                return;

            DrawEffectiveModule(module);
            DrawRuntimeControls(module);
        }

        private static void DrawTitle(ArmoryCoreAttachmentModule module)
        {
            var title = module.Definition == null
                ? ObjectNames.NicifyVariableName(module.GetType().Name)
                : string.IsNullOrWhiteSpace(module.Definition.displayName)
                    ? module.Definition.name
                    : module.Definition.displayName;
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(title, EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                $"{ObjectNames.NicifyVariableName(module.GetType().Name)} · prefab-level behavior and overrides",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(3f);
        }

        private void DrawDiagnostics(ArmoryCoreAttachmentModule module)
        {
            var issues = new List<string>();
            if (module.Definition == null)
            {
                issues.Add("Attachment Definition is not assigned.");
            }
            else
            {
                var expectedSlot = ExpectedSlot(module);
                if (expectedSlot.HasValue && module.Definition.slot != expectedSlot.Value)
                {
                    issues.Add(
                        $"This module normally uses the {expectedSlot.Value} slot, but the definition uses "
                        + $"{module.Definition.slot}.");
                }
            }

            if (module is ArmoryCoreSightModule
                && serializedObject.FindProperty("aimPoint")?.objectReferenceValue == null)
            {
                issues.Add("Aim Point is not assigned.");
            }
            if (module is ArmoryCoreSuppressorModule && module.MuzzlePoint == null)
                issues.Add("Muzzle Point is not assigned; the weapon muzzle will be used instead.");
            if (module is ArmoryCoreFlashlightModule)
            {
                var lights = serializedObject.FindProperty("lights");
                if (lights == null || lights.arraySize == 0)
                    issues.Add("No child lights are assigned.");
            }

            EditorGUILayout.HelpBox(
                issues.Count == 0 ? "Module is configured." : string.Join("\n", issues),
                issues.Count == 0 ? MessageType.Info : MessageType.Warning);
        }

        private void DrawSetupTools(ArmoryCoreAttachmentModule module)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-assign child references"))
            {
                for (var index = 0; index < targets.Length; index++)
                    AutoAssign((ArmoryCoreAttachmentModule)targets[index]);
                serializedObject.Update();
            }

            using (new EditorGUI.DisabledScope(
                       serializedObject.isEditingMultipleObjects || module.Definition == null))
            {
                if (GUILayout.Button("Select definition"))
                {
                    Selection.activeObject = module.Definition;
                    EditorGUIUtility.PingObject(module.Definition);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3f);
        }

        private void DrawModuleTuning(ArmoryCoreAttachmentModule module)
        {
            if (module is ArmoryCoreSightModule)
            {
                DrawProperty("aimPoint");
                DrawProperty("useDefinitionMagnification");
                var useDefinition = serializedObject.FindProperty("useDefinitionMagnification");
                if (useDefinition == null || useDefinition.hasMultipleDifferentValues || !useDefinition.boolValue)
                {
                    DrawProperty("minimumMagnification");
                    DrawProperty("maximumMagnification");
                }
                else if (module.Definition != null)
                {
                    EditorGUILayout.HelpBox(
                        $"Magnification comes from the definition: "
                        + $"{module.Definition.GameplayMinimumMagnification:0.##}x – "
                        + $"{module.Definition.GameplayMaximumMagnification:0.##}x.",
                        MessageType.None);
                }
                DrawProperty("currentMagnification");
                DrawProperty("zeroDistanceMeters");
                DrawProperty("zoomSteps");
                DrawProperty("aimTimeMultiplier");
                return;
            }

            if (module is ArmoryCoreSuppressorModule)
            {
                DrawProperty("additionalSoundMultiplier");
                DrawProperty("additionalRecoilMultiplier");
                DrawProperty("additionalFlashMultiplier");
                DrawProperty("suppressedShotOverride");
                return;
            }

            if (module is ArmoryCoreGripModule)
            {
                DrawProperty("verticalRecoilMultiplier");
                DrawProperty("horizontalRecoilMultiplier");
                DrawProperty("spreadMultiplier");
                DrawProperty("supportsDeployment");
                var deployable = serializedObject.FindProperty("supportsDeployment");
                if (deployable == null || deployable.hasMultipleDifferentValues || deployable.boolValue)
                {
                    DrawProperty("deployed");
                    DrawProperty("deployedRecoilMultiplier");
                    DrawProperty("deployedSpreadMultiplier");
                }
                return;
            }

            if (module is ArmoryCoreStockModule)
            {
                DrawProperty("foldable");
                var foldable = serializedObject.FindProperty("foldable");
                if (foldable == null || foldable.hasMultipleDifferentValues || foldable.boolValue)
                    DrawProperty("folded");
                DrawProperty("recoilMultiplier");
                DrawProperty("aimTimeMultiplier");
                if (foldable == null || foldable.hasMultipleDifferentValues || foldable.boolValue)
                {
                    DrawProperty("foldedRecoilMultiplier");
                    DrawProperty("foldedAimTimeMultiplier");
                }
                return;
            }

            if (module is ArmoryCoreFlashlightModule)
            {
                DrawProperty("lights");
                DrawProperty("startEnabled");
                return;
            }

            DrawPropertiesExcluding(serializedObject, "m_Script", "definition", "muzzlePoint");
        }

        private void DrawProperty(string name)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        private static string GetTuningTitle(ArmoryCoreAttachmentModule module)
        {
            if (module is ArmoryCoreSightModule)
                return "Sight tuning";
            if (module is ArmoryCoreSuppressorModule)
                return "Suppressor tuning";
            if (module is ArmoryCoreGripModule)
                return "Grip and deployment";
            if (module is ArmoryCoreStockModule)
                return "Stock tuning";
            if (module is ArmoryCoreFlashlightModule)
                return "Flashlight tuning";
            return "Module tuning";
        }

        private static ArmoryCoreAttachmentSlot? ExpectedSlot(ArmoryCoreAttachmentModule module)
        {
            if (module is ArmoryCoreSightModule)
                return ArmoryCoreAttachmentSlot.Sights;
            if (module is ArmoryCoreSuppressorModule)
                return ArmoryCoreAttachmentSlot.Barrel;
            if (module is ArmoryCoreGripModule)
                return ArmoryCoreAttachmentSlot.Grip;
            if (module is ArmoryCoreStockModule)
                return ArmoryCoreAttachmentSlot.Stock;
            return null;
        }

        private static void AutoAssign(ArmoryCoreAttachmentModule module)
        {
            Undo.RecordObject(module, "Auto-assign attachment references");
            var serialized = new SerializedObject(module);
            AssignIfEmpty(serialized, "muzzlePoint", FindTransform(module.transform, "Muzzle"));

            if (module is ArmoryCoreSightModule)
            {
                var aimPoint = FindTransform(module.transform, "Aim Point")
                    ?? FindTransform(module.transform, "AimPoint")
                    ?? FindTransform(module.transform, "Sight Aim");
                AssignIfEmpty(serialized, "aimPoint", aimPoint);
            }

            if (module is ArmoryCoreFlashlightModule)
            {
                var lights = serialized.FindProperty("lights");
                if (lights != null && lights.arraySize == 0)
                {
                    var childLights = module.GetComponentsInChildren<Light>(true);
                    lights.arraySize = childLights.Length;
                    for (var index = 0; index < childLights.Length; index++)
                        lights.GetArrayElementAtIndex(index).objectReferenceValue = childLights[index];
                }
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(module);
        }

        private static void AssignIfEmpty(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(name);
            if (property != null && property.objectReferenceValue == null && value != null)
                property.objectReferenceValue = value;
        }

        private static Transform FindTransform(Transform root, string name)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                if (string.Equals(transforms[index].name, name, StringComparison.OrdinalIgnoreCase))
                    return transforms[index];
            }
            return null;
        }

        private static void DrawEffectiveModule(ArmoryCoreAttachmentModule module)
        {
            if (module.Definition == null)
                return;

            var definition = module.Definition;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Effective module preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Type", ObjectNames.NicifyVariableName(module.GetType().Name));
                EditorGUILayout.LabelField("Slot", definition.slot.ToString());
                EditorGUILayout.LabelField("Recoil",
                    $"×{definition.recoilMultiplier * module.VerticalRecoilMultiplier:0.###} vertical · "
                    + $"×{definition.recoilMultiplier * module.HorizontalRecoilMultiplier:0.###} horizontal");
                EditorGUILayout.LabelField("Spread",
                    $"×{definition.spreadMultiplier * module.SpreadMultiplier:0.###}");
                if (definition.suppressesMuzzleBlast || module.ForcesSuppressedShot)
                {
                    EditorGUILayout.LabelField("Sound",
                        $"×{definition.soundPressureMultiplier * module.SoundPressureMultiplier:0.###}");
                }
                if (module.OverridesOptic)
                {
                    EditorGUILayout.LabelField("Optic",
                        $"{module.CurrentMagnification:0.##}x @ {module.CurrentZeroDistanceMeters:0} m");
                }
            }
        }

        private static void DrawRuntimeControls(ArmoryCoreAttachmentModule module)
        {
            if (!Application.isPlaying)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Runtime controls", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (module is ArmoryCoreSightModule sight && GUILayout.Button("Cycle zoom"))
                    sight.CycleMagnification();
                if (module is ArmoryCoreGripModule grip && GUILayout.Button("Toggle deployment"))
                    grip.ToggleDeployed();
                if (module is ArmoryCoreStockModule stock && GUILayout.Button("Toggle fold"))
                    stock.ToggleFolded();
                if (module is ArmoryCoreFlashlightModule flashlight && GUILayout.Button("Toggle light"))
                    flashlight.ToggleLight();
            }
        }

        private void OnSceneGUI()
        {
            if (target is not ArmoryCoreAttachmentModule module)
                return;

            if (module.MuzzlePoint != null)
            {
                Handles.color = new Color(1f, 0.35f, 0.08f, 0.9f);
                DrawDirectionHandle(module.MuzzlePoint, "  Module Muzzle");
            }
            if (module.AimPoint != null)
            {
                Handles.color = new Color(0.15f, 0.7f, 1f, 0.9f);
                DrawDirectionHandle(module.AimPoint, "  Aim Point");
            }
        }

        private static void DrawDirectionHandle(Transform point, string label)
        {
            var size = HandleUtility.GetHandleSize(point.position) * 0.3f;
            Handles.ArrowHandleCap(
                0,
                point.position,
                Quaternion.LookRotation(point.forward),
                size,
                EventType.Repaint);
            Handles.Label(point.position, label);
        }
    }
}
