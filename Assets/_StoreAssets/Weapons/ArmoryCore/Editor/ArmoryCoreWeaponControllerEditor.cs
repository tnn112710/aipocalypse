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

        private bool showDefinition = true;
        private bool showSceneSetup = true;
        private bool showInitialization = true;
        private bool showPlayerInput = true;
        private bool showCamera = true;
        private bool showAudio = true;
        private bool showAnimator = true;
        private bool showMuzzleEffects = true;
        private bool showCasings = true;
        private bool showVisualRecoil = true;
        private bool showFireEvents = true;
        private bool showStateEvents = true;
        private bool showAimEvents = true;

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

        private void DrawWeaponPage()
        {
            DrawPageIntro(
                "Weapon configuration",
                "Assign the reusable definition, edit all of its gameplay values inline, and configure this scene instance.");

            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showDefinition,
                    "Weapon definition",
                    "The definition stores firing, ballistic, handling, reliability, compatibility, and sound values."))
            {
                DrawProperty(
                    "definition",
                    "Definition",
                    "Reusable gameplay configuration shared by weapon instances.");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showDefinition);

            if (!serializedObject.isEditingMultipleObjects)
            {
                var definition = serializedObject.FindProperty("definition")?.objectReferenceValue
                    as ArmoryCoreWeaponDefinition;
                if (definition == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign a Weapon Definition to expose all weapon parameters here.",
                        MessageType.Info);
                }
                else
                {
                    EnsureDefinitionEditor(definition);
                    EditorGUILayout.Space(5f);
                    EditorGUILayout.LabelField("WEAPON PARAMETERS", EditorStyles.miniBoldLabel);
                    if (definitionEditor is ArmoryCoreWeaponDefinitionEditor weaponDefinitionEditor)
                        weaponDefinitionEditor.DrawEmbeddedInspector();
                    else
                        definitionEditor?.OnInspectorGUI();
                }
            }

            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showSceneSetup,
                    "Scene references",
                    "Required transforms define projectile origin and aiming direction for this prefab instance."))
            {
                DrawProperty("muzzle", "Muzzle", "Projectile origin; its forward axis must point along the barrel.");
                DrawProperty("aimReference", "Aim Reference", "Optional fallback aiming direction when a sight is not active.");
                DrawProperty("hitMask", "Projectile Hit Mask", "Physics layers that projectiles are allowed to hit.");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showSceneSetup);

            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showInitialization,
                    "Initial state",
                    "Controls automatic initialization and ammunition carried by this instance."))
            {
                DrawProperty("initializeOnAwake", "Initialize On Awake");
                DrawProperty("reserveRounds", "Starting Reserve Rounds");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showInitialization);
        }

        private void DrawControlPage()
        {
            DrawPageIntro(
                "Input & aiming",
                "Choose whether this component reads local input and configure camera aiming behavior.");

            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showPlayerInput,
                    "Player input",
                    "Disable local input when another player, AI, or network system drives the weapon through its public API."))
            {
                DrawProperty("processPlayerInput", "Read Local Player Input");
                var processInput = serializedObject.FindProperty("processPlayerInput");
                if (processInput == null || processInput.hasMultipleDifferentValues || processInput.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        "Controls: LMB fire · RMB aim · R reload · B fire mode · C cycle action · "
                        + "T clear malfunction · F light · Z bipod/stock · mouse wheel zoom.",
                        MessageType.None);
                }
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showPlayerInput);

            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showCamera,
                    "Aim camera & field of view",
                    "The camera may be assigned explicitly or resolved from Camera.main at runtime."))
            {
                DrawProperty("autoFindMainCamera", "Find Main Camera Automatically");
                var autoFind = serializedObject.FindProperty("autoFindMainCamera");
                if (autoFind == null || autoFind.hasMultipleDifferentValues || !autoFind.boolValue)
                    DrawProperty("aimCamera", "Aim Camera");

                DrawProperty("controlCameraFieldOfView", "Control Camera Field Of View");
                var controlFov = serializedObject.FindProperty("controlCameraFieldOfView");
                if (controlFov == null || controlFov.hasMultipleDifferentValues || controlFov.boolValue)
                {
                    DrawProperty("hipFieldOfView", "Hip Field Of View (°)");
                    DrawProperty("aimFieldOfView", "Aim Field Of View (°)");
                    DrawProperty("fieldOfViewSpeed", "Field Of View Transition Speed");
                }
                DrawProperty("compensateSightZero", "Compensate Sight Zero");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showCamera);
        }

        private void DrawEffectsPage()
        {
            DrawPageIntro(
                "Presentation",
                "Configure each optional presentation system independently; unused systems stay compact.");
            DrawAudioSettings();
            DrawAnimatorSettings();
            DrawMuzzleEffectSettings();
            DrawCasingSettings();
            DrawVisualRecoilSettings();
        }

        private void DrawAudioSettings()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showAudio,
                    "Weapon audio",
                    "The Audio Source handles spatial playback; clip selection and ranges are configured in the Weapon Definition."))
            {
                DrawProperty("audioSource", "Audio Source");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showAudio);
        }

        private void DrawAnimatorSettings()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showAnimator,
                    "Animator",
                    "Parameter names are only used when an Animator is assigned."))
            {
                DrawProperty("animator", "Animator");
                var animator = serializedObject.FindProperty("animator");
                if (animator == null || animator.hasMultipleDifferentValues || animator.objectReferenceValue != null)
                {
                    ArmoryCoreDefinitionEditorUtility.DrawSubheading("Trigger parameters");
                    DrawProperty("fireTrigger", "Fire");
                    DrawProperty("reloadTrigger", "Reload");
                    DrawProperty("emptyTrigger", "Empty Trigger");
                    DrawProperty("cycleTrigger", "Cycle Action");
                    DrawProperty("malfunctionTrigger", "Malfunction");
                    ArmoryCoreDefinitionEditorUtility.DrawSubheading("State parameters");
                    DrawProperty("aimingParameter", "Aiming Bool");
                    DrawProperty("fireModeParameter", "Fire Mode Integer");
                }
                else
                {
                    EditorGUILayout.HelpBox("No Animator assigned; animation events are skipped.", MessageType.None);
                }
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showAnimator);
        }

        private void DrawMuzzleEffectSettings()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showMuzzleEffects,
                    "Muzzle flash & light",
                    "Particle and light effects are optional and scale with installed muzzle devices."))
            {
                DrawProperty("muzzleFlash", "Muzzle Flash Particles");
                var flash = serializedObject.FindProperty("muzzleFlash");
                if (flash == null || flash.hasMultipleDifferentValues || flash.objectReferenceValue != null)
                    DrawProperty("muzzleFlashParticles", "Particles Per Shot");

                DrawProperty("muzzleLight", "Muzzle Light");
                var light = serializedObject.FindProperty("muzzleLight");
                if (light == null || light.hasMultipleDifferentValues || light.objectReferenceValue != null)
                    DrawProperty("muzzleLightDuration", "Light Duration (s)");
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showMuzzleEffects);
        }

        private void DrawCasingSettings()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showCasings,
                    "Casing ejection",
                    "Assign both an ejection point and a rigidbody prefab to enable physical casings."))
            {
                DrawProperty("casingEjectionPoint", "Ejection Point");
                DrawProperty("casingPrefab", "Casing Prefab");
                var point = serializedObject.FindProperty("casingEjectionPoint");
                var prefab = serializedObject.FindProperty("casingPrefab");
                var configured = point == null || prefab == null
                    || point.hasMultipleDifferentValues || prefab.hasMultipleDifferentValues
                    || point.objectReferenceValue != null || prefab.objectReferenceValue != null;
                if (configured)
                {
                    DrawProperty("casingEjectionForce", "Ejection Force");
                    DrawProperty("casingEjectionTorque", "Ejection Torque");
                    DrawProperty("casingLifetime", "Casing Lifetime (s)");
                }
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showCasings);
        }

        private void DrawVisualRecoilSettings()
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(
                    ref showVisualRecoil,
                    "Visual recoil",
                    "The target transform receives positional and rotational kick without changing projectile ballistics."))
            {
                DrawProperty("recoilTarget", "Recoil Target");
                var targetProperty = serializedObject.FindProperty("recoilTarget");
                if (targetProperty == null || targetProperty.hasMultipleDifferentValues
                    || targetProperty.objectReferenceValue != null)
                {
                    DrawProperty("visualKickMetersPerDegree", "Kick Distance Per Degree (m)");
                }
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(showVisualRecoil);
        }

        private void DrawAttachmentsPage(ArmoryCoreWeaponController weapon)
        {
            DrawPageIntro(
                "Installed attachments",
                "Choose the active child for each slot. Options are filtered by the Weapon Definition compatibility rules.");

            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox("Attachment selection is available when editing one weapon at a time.", MessageType.Info);
                return;
            }

            DrawSlot(weapon, ArmoryCoreAttachmentSlot.Sights);
            DrawSlot(weapon, ArmoryCoreAttachmentSlot.Barrel);
            DrawSlot(weapon, ArmoryCoreAttachmentSlot.Grip);
            DrawSlot(weapon, ArmoryCoreAttachmentSlot.Stock);
        }

        private static void DrawSlot(ArmoryCoreWeaponController weapon, ArmoryCoreAttachmentSlot slot)
        {
            var title = ObjectNames.NicifyVariableName(slot.ToString());
            var root = weapon.GetAttachmentSlotRoot(slot);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (root == null)
                {
                    EditorGUILayout.LabelField($"No {title} Slot child exists on this weapon.", EditorStyles.wordWrappedMiniLabel);
                    return;
                }
                if (root.childCount == 0)
                {
                    EditorGUILayout.LabelField("The slot exists but contains no attachment prefabs.", EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                var transforms = new List<Transform> { null };
                var labels = new List<string> { "None" };
                var selected = 0;
                for (var index = 0; index < root.childCount; index++)
                {
                    var option = root.GetChild(index);
                    var module = option.GetComponent<ArmoryCoreAttachmentModule>();
                    var definition = module == null ? null : module.Definition;
                    if (definition != null && !weapon.CanInstall(definition))
                        continue;

                    transforms.Add(option);
                    labels.Add(definition == null || string.IsNullOrWhiteSpace(definition.displayName)
                        ? ObjectNames.NicifyVariableName(option.name)
                        : definition.displayName);
                    if (option.gameObject.activeSelf)
                        selected = transforms.Count - 1;
                }

                EditorGUI.BeginChangeCheck();
                var next = EditorGUILayout.Popup("Installed", selected, labels.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    var objects = new UnityEngine.Object[root.childCount];
                    for (var index = 0; index < root.childCount; index++)
                        objects[index] = root.GetChild(index).gameObject;
                    Undo.RecordObjects(objects, $"Change {title} attachment");
                    var nextTransform = transforms[next];
                    var nextModule = nextTransform == null
                        ? null
                        : nextTransform.GetComponent<ArmoryCoreAttachmentModule>();
                    var nextDefinition = nextModule == null ? null : nextModule.Definition;
                    var nextId = nextTransform == null
                        ? null
                        : string.IsNullOrWhiteSpace(nextDefinition?.id)
                            ? nextTransform.name
                            : nextDefinition.id;
                    weapon.SelectAttachment(slot, nextId);
                    EditorUtility.SetDirty(weapon.gameObject);
                }

                if (selected > 0)
                {
                    var active = transforms[selected];
                    var module = active == null ? null : active.GetComponent<ArmoryCoreAttachmentModule>();
                    var definition = module == null ? null : module.Definition;
                    var details = definition == null
                        ? "No Attachment Definition on the active child."
                        : $"{ObjectNames.NicifyVariableName(module.GetType().Name)}  •  "
                          + $"{definition.massKilograms:0.###} kg  •  recoil "
                          + ArmoryCoreDefinitionEditorUtility.FormatModifier(definition.recoilMultiplier);
                    EditorGUILayout.LabelField(details, EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("No attachment installed.", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.Space(2f);
        }

        private void DrawEventsPage()
        {
            DrawPageIntro(
                "Gameplay events",
                "Connect UI, animation, sound, analytics, and other systems without modifying the weapon controller.");

            DrawEventSection(ref showFireEvents, "Firing & ammunition", new[]
            {
                "onShot", "onEmpty", "onAmmoChanged"
            });
            DrawEventSection(ref showStateEvents, "Reload & malfunctions", new[]
            {
                "onReloadStarted", "onReloadCompleted", "onMalfunction", "onMalfunctionCleared"
            });
            DrawEventSection(ref showAimEvents, "Aiming", new[] { "onAimChanged" });
        }

        private void DrawEventSection(ref bool expanded, string title, string[] propertyNames)
        {
            if (ArmoryCoreDefinitionEditorUtility.BeginSection(ref expanded, title))
            {
                for (var index = 0; index < propertyNames.Length; index++)
                    DrawProperty(propertyNames[index], ObjectNames.NicifyVariableName(propertyNames[index]));
            }
            ArmoryCoreDefinitionEditorUtility.EndSection(expanded);
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
