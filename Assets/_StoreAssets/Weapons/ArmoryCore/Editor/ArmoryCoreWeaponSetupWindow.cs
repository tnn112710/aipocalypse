using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArmoryCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Graph = ArmoryCoreEditor.ArmoryCoreGraph;
using StepState = ArmoryCoreEditor.ArmoryCoreSetupGUI.StepState;

namespace ArmoryCoreEditor
{
    /// <summary>
    /// Weapon setup in one window: the definition as numbered steps on the left, the model on the right with the muzzle
    /// and the casing ejection port dragged straight on its surface. The Cartridges tab sets up the project's cartridges
    /// and compares their ballistics.
    /// </summary>
    public sealed partial class ArmoryCoreWeaponSetupWindow : EditorWindow
    {
        private const float PanelWidth = 440f;
        private const string Title = "Weapon Setup";
        private const string MuzzleName = "Muzzle";
        private const string EjectionName = "Casing Ejection Point";
        private static readonly string[] AxisNames = { "+Z", "+Y", "+X", "−Z", "−Y", "−X" };
        private static readonly Vector3[] Axes = { Vector3.forward, Vector3.up, Vector3.right, Vector3.back, Vector3.down, Vector3.left };
        private static readonly Color MuzzleColor = new Color(0.3f, 0.85f, 1f);
        private static readonly Color EjectionColor = new Color(1f, 0.65f, 0.2f);


        private static Graph.Tile[] axisTiles;
        private static readonly Graph.Tile[] SideTiles =
        {
            new Graph.Tile("Ejects right", null, "Casings fly to the right of the barrel.") { Glyph = "→", Color = EjectionColor },
            new Graph.Tile("Ejects left", null, "Casings fly to the left of the barrel.") { Glyph = "←", Color = EjectionColor },
        };

        [SerializeField] private GameObject model;
        [SerializeField] private ArmoryCoreWeaponDefinition definition;
        [SerializeField] private Vector3 muzzlePosition;
        [SerializeField] private Vector3 muzzleForward = Vector3.forward;
        [SerializeField] private bool ejection = true;
        [SerializeField] private Vector3 ejectionPosition;
        [SerializeField] private bool ejectRight = true;
        private Vector2 scroll;
        private string status;
        private bool statusError;
        private ArmoryCoreModelViewport viewport;
        private ArmoryCoreWeaponSteps steps;
        private SerializedObject definitionData;
        private int dragged = -1;
        private Vector3 dragAnchor;
        [System.NonSerialized] private ArmoryCoreModelGeometry geometry;
        [System.NonSerialized] private GameObject geometryModel;

        [MenuItem("Tools/ArmoryCore/Weapon Setup", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<ArmoryCoreWeaponSetupWindow>(Title);
            if (!window.model && Selection.activeGameObject)
                window.SelectModel(Selection.activeGameObject);
        }

        [MenuItem("GameObject/ArmoryCore/Set Up Weapon", false, 20)]
        private static void OpenSelection()
        {
            Open(Selection.activeGameObject);
        }

        public static void Open(GameObject target)
        {
            var window = GetWindow<ArmoryCoreWeaponSetupWindow>(Title);
            if (target)
                window.SelectModel(target);
            window.Show();
            window.Focus();
        }

        /// <summary>Opens the definition together with a weapon that uses it: the selection, a scene object or the prefab of the same name.</summary>
        public static void Open(ArmoryCoreWeaponDefinition weapon)
        {
            var window = GetWindow<ArmoryCoreWeaponSetupWindow>(Title);
            var user = FindUser(weapon);
            if (user)
                window.SelectModel(user);
            window.definition = weapon;
            window.Show();
            window.Focus();
        }

        private static GameObject FindUser(ArmoryCoreWeaponDefinition weapon)
        {
            if (!weapon)
                return null;
            var selected = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<ArmoryCoreWeaponController>() : null;
            if (selected && selected.Definition == weapon)
                return selected.gameObject;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var staged = stage ? stage.prefabContentsRoot.GetComponent<ArmoryCoreWeaponController>() : null;
            if (staged && staged.Definition == weapon)
                return staged.gameObject;
            var inScene = FindObjectsByType<ArmoryCoreWeaponController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(controller => controller.Definition == weapon);
            if (inScene)
                return inScene.gameObject;
            foreach (var guid in AssetDatabase.FindAssets(weapon.name + " t:Prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var controller = prefab ? prefab.GetComponent<ArmoryCoreWeaponController>() : null;
                if (controller && controller.Definition == weapon)
                    return prefab;
            }
            return null;
        }

        private void OnEnable()
        {
            minSize = new Vector2(960f, 620f);
            viewport = new ArmoryCoreModelViewport();
            steps = new ArmoryCoreWeaponSteps { Key = "weapon.window", OpenCartridge = OpenCartridge };
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            viewport?.Dispose();
            viewport = null;
            steps?.Dispose();
            steps = null;
            if (ammunitionEditor)
                DestroyImmediate(ammunitionEditor);
        }

        private void OnSelectionChange()
        {
            var selected = Selection.activeGameObject;
            var controller = selected ? selected.GetComponentInParent<ArmoryCoreWeaponController>() : null;
            if (controller && controller.gameObject != model)
            {
                SelectModel(controller.gameObject);
                Repaint();
            }
        }

        private bool Editable => model && !EditorUtility.IsPersistent(model) && !EditorApplication.isPlaying;
        private ArmoryCoreWeaponController Controller => model ? model.GetComponent<ArmoryCoreWeaponController>() : null;

        private ArmoryCoreModelGeometry Geometry
        {
            get
            {
                if (geometry == null || geometryModel != model)
                {
                    geometry = ArmoryCoreModelGeometry.Build(model);
                    geometryModel = model;
                }
                return geometry;
            }
        }

        private static Graph.Tile[] AxisTiles
        {
            get
            {
                if (axisTiles != null)
                    return axisTiles;
                axisTiles = new Graph.Tile[AxisNames.Length];
                for (var i = 0; i < axisTiles.Length; i++)
                    axisTiles[i] = new Graph.Tile(AxisNames[i], null, "The barrel points along the model's local " + AxisNames[i] + " axis.") { Color = MuzzleColor };
                return axisTiles;
            }
        }

        private static GameObject RootOf(GameObject selected)
        {
            var controller = selected ? selected.GetComponentInParent<ArmoryCoreWeaponController>() : null;
            return controller ? controller.gameObject : selected;
        }

        private void SelectModel(GameObject selected)
        {
            model = RootOf(selected);
            status = null;
            geometry = null;
            if (!model)
                return;
            var controller = Controller;
            if (controller && controller.Definition)
                definition = controller.Definition;
            ReadLayout();
        }

        private void SetStatus(string text, bool error = false)
        {
            status = text;
            statusError = error;
        }

        // ---------- Layout of the model ----------

        private Vector3 Side
        {
            get
            {
                var forward = muzzleForward.normalized;
                var up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.9f ? Vector3.back : Vector3.up;
                return Vector3.Cross(up, forward).normalized;
            }
        }

        private Vector3 Up => Vector3.Cross(muzzleForward.normalized, Side).normalized;

        /// <summary>Reads the muzzle and ejection port of a configured weapon; guesses whatever is missing from the model.</summary>
        private void ReadLayout()
        {
            var root = model.transform;
            var controller = Controller;
            var muzzle = controller ? controller.Muzzle : FindChild(root, MuzzleName);
            if (muzzle)
            {
                muzzlePosition = root.InverseTransformPoint(muzzle.position);
                muzzleForward = root.InverseTransformDirection(muzzle.forward).normalized;
            }
            else
            {
                GuessMuzzle();
            }
            var port = EjectionPort(controller);
            if (port)
            {
                ejection = true;
                ejectionPosition = root.InverseTransformPoint(port.position);
                ejectRight = Vector3.Dot(root.InverseTransformDirection(port.right), Side) >= 0f;
            }
            else
            {
                // A configured weapon without a port has no casings; a new one gets a guessed port.
                ejection = !controller;
                GuessEjection();
            }
        }

        private void GuessMuzzle()
        {
            var shape = Geometry;
            if (shape.IsEmpty)
                return;
            shape.GuessMuzzle(out muzzlePosition, out muzzleForward);
        }

        /// <summary>The port sits on top of the receiver, a little behind the middle of the weapon.</summary>
        private void GuessEjection()
        {
            var shape = Geometry;
            if (shape.IsEmpty)
                return;
            var bounds = shape.Bounds;
            var forward = muzzleForward.normalized;
            var length = Mathf.Abs(Vector3.Dot(bounds.size, forward));
            var height = Mathf.Abs(Vector3.Dot(bounds.size, Up));
            var start = bounds.center + forward * (length * 0.05f) + Up * (height * 0.15f);
            var ray = new Ray(start + Side * bounds.size.magnitude, -Side);
            ejectionPosition = shape.RaycastThrough(ray, out var inside) ? inside : start;
        }

        private static Transform FindChild(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child != root && child.name == name);
        }

        private static Transform EjectionPort(ArmoryCoreWeaponController controller)
        {
            return controller ? new SerializedObject(controller).FindProperty("casingEjectionPoint").objectReferenceValue as Transform : null;
        }

        // ---------- Window ----------

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(PanelWidth)))
                    DrawPanel();
                using (new EditorGUILayout.VerticalScope())
                    DrawViewport();
            }
        }

        private void DrawPanel()
        {
            var wide = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            using (ArmoryCoreSetupGUI.LabelWidth(PanelWidth))
            {
                DrawHeader();
                var nextTab = GUILayout.Toolbar(tab, new[] { "Weapon", "Cartridges" }, GUILayout.Height(24f));
                if (nextTab != tab)
                {
                    tab = nextTab;
                    GUI.FocusControl(null);
                }
                if (tab == 1)
                {
                    cartridgeScroll = EditorGUILayout.BeginScrollView(cartridgeScroll);
                    GUILayout.Space(4f);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                        DrawCartridgesTab();
                    GUILayout.Space(8f);
                    EditorGUILayout.EndScrollView();
                    DrawCartridgeFooter();
                }
                else
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    GUILayout.Space(4f);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                        DrawSteps();
                    GUILayout.Space(8f);
                    EditorGUILayout.EndScrollView();
                    DrawFooter();
                }
            }
            EditorGUIUtility.wideMode = wide;
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                var next = ArmoryCoreSetupGUI.ObjectWithSelection("Model", model, true,
                    "Root of the weapon: a scene object, the root of a prefab in Prefab Mode, or a model asset.", RootOf);
                if (EditorGUI.EndChangeCheck() && next != model)
                    SelectModel(next);
                if (!model)
                {
                    Graph.Hint("Drag a weapon prefab or model here, or select one in the Hierarchy.");
                }
                else if (EditorUtility.IsPersistent(model))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Graph.Hint("A project asset: a prefab opens in Prefab Mode, a model is placed in the scene.");
                        if (ArmoryCoreSetupGUI.MiniButton("Open for editing", null, 110f))
                            OpenModelForEditing();
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    definition = (ArmoryCoreWeaponDefinition)EditorGUILayout.ObjectField(
                        new GUIContent("Definition", "The weapon definition: mechanism, cartridge, shot, recoil, handling, heat and sound."),
                        definition, typeof(ArmoryCoreWeaponDefinition), false);
                    if (ArmoryCoreSetupGUI.MiniButton("Create…", "A new definition from a weapon type, or a copy of the current one", 64f))
                        ShowCreateMenu();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    var controller = Controller;
                    var ready = controller && controller.Muzzle && controller.Definition && controller.Definition.ammunition;
                    ArmoryCoreSetupGUI.Badge(ready ? "✓ fires" : "— not set up", ready ? Graph.Good : Graph.Muted);
                    ArmoryCoreSetupGUI.Badge(definition && definition.ammunition ? "✓ cartridge" : "— cartridge", definition && definition.ammunition ? Graph.Good : Graph.Muted);
                    if (controller && definition && controller.Definition != definition)
                        ArmoryCoreSetupGUI.Badge("model uses another definition", Graph.Warn);
                    GUILayout.FlexibleSpace();
                    Graph.NumbersToggle();
                }
            }
        }

        private void OpenModelForEditing()
        {
            var path = AssetDatabase.GetAssetPath(model);
            if (path.EndsWith(".prefab"))
            {
                var stage = PrefabStageUtility.OpenPrefab(path);
                if (stage)
                    SelectModel(stage.prefabContentsRoot);
                return;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            Undo.RegisterCreatedObjectUndo(instance, "Place weapon model");
            Selection.activeGameObject = instance;
            SelectModel(instance);
        }

        private void ShowCreateMenu()
        {
            var menu = new GenericMenu();
            for (var i = 0; i < ArmoryCoreWeaponPresets.All.Length; i++)
            {
                var preset = ArmoryCoreWeaponPresets.All[i];
                menu.AddItem(new GUIContent("From type/" + preset.Title), false, () => CreateDefinition(preset, null));
            }
            if (definition)
                menu.AddItem(new GUIContent("Copy of " + ArmoryCoreWeaponSteps.Name(definition)), false, () => CreateDefinition(null, definition));
            menu.ShowAsContext();
        }

        private void CreateDefinition(ArmoryCoreWeaponPresets.Preset preset, ArmoryCoreWeaponDefinition source)
        {
            var folder = definition ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(definition)).Replace('\\', '/')
                : AssetDatabase.IsValidFolder("Assets/ArmoryCore/Definitions/Weapons") ? "Assets/ArmoryCore/Definitions/Weapons" : "Assets";
            var name = model ? model.name : source ? source.name + " copy" : "Weapon";
            var path = EditorUtility.SaveFilePanelInProject("New weapon definition", name, "asset", "Save the weapon definition in the project.", folder);
            if (string.IsNullOrEmpty(path))
                return;
            var created = source ? Instantiate(source) : CreateInstance<ArmoryCoreWeaponDefinition>();
            AssetDatabase.CreateAsset(created, path);
            var data = new SerializedObject(created);
            data.FindProperty("id").stringValue = Path.GetFileNameWithoutExtension(path);
            data.FindProperty("displayName").stringValue = ObjectNames.NicifyVariableName(Path.GetFileNameWithoutExtension(path));
            if (preset != null)
            {
                var tags = data.FindProperty("weaponTags");
                tags.arraySize = 2;
                tags.GetArrayElementAtIndex(0).stringValue = preset.Action.ToString();
                tags.GetArrayElementAtIndex(1).stringValue = preset.Reload.ToString();
                ArmoryCoreWeaponPresets.Apply(data, preset);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            definition = created;
            EditorGUIUtility.PingObject(created);
            SetStatus(preset != null
                ? $"Definition created as «{preset.Title}». Press the button below to give it to the model."
                : "Definition copied. Press the button below to give it to the model.");
        }

        // ---------- Weapon steps ----------

        private void DrawSteps()
        {
            if (definition && (definitionData == null || definitionData.targetObject != definition))
                definitionData = new SerializedObject(definition);
            if (!definition)
                definitionData = null;
            definitionData?.Update();

            var number = 1;
            if (definitionData != null)
                steps.DrawType(definitionData, number);
            else if (ArmoryCoreSetupGUI.Step("weapon.window.step.type", number, "Type & cartridge", StepState.Locked, "no definition", null))
                using (ArmoryCoreSetupGUI.CardBody())
                    Graph.Hint("The definition holds the weapon's mechanism, cartridge, shot, recoil, handling and sound. Pick one in the header or press «Create…» to start from a weapon type.");
            number++;
            DrawBarrelStep(number++);
            if (definitionData != null)
            {
                steps.DrawAction(definitionData, number++);
                steps.DrawShot(definitionData, number++);
                steps.DrawMagazine(definitionData, number++);
                steps.DrawRecoil(definitionData, number++);
                steps.DrawHandling(definitionData, number++);
                steps.DrawReliability(definitionData, number++);
                steps.DrawSound(definitionData, number++);
            }
            DrawEffectsStep(number++);
            if (definitionData != null)
            {
                steps.DrawIdentity(definitionData, number);
                definitionData.ApplyModifiedProperties();
            }
            if (!string.IsNullOrEmpty(steps.Status))
            {
                SetStatus(steps.Status);
                steps.Status = null;
            }
        }

        private void DrawBarrelStep(int number)
        {
            var state = !model ? StepState.Locked : Controller && Controller.Muzzle ? StepState.Done : StepState.Current;
            if (!ArmoryCoreSetupGUI.Step("weapon.window.step.barrel", number, "Barrel & muzzle", state, model ? AxisSummary() : "no model", "Where the muzzle is and where the barrel points"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                if (!model)
                {
                    Graph.Hint("Pick a model in the header first.");
                    return;
                }
                Graph.Hint("Drag the muzzle (blue) and the ejection port (orange) straight on the 3D model on the right.");
                EditorGUILayout.LabelField("The barrel points along", EditorStyles.miniBoldLabel);
                var axis = System.Array.FindIndex(Axes, value => Vector3.Dot(value, muzzleForward.normalized) > 0.999f);
                var next = Graph.Choice(axis, AxisTiles, 6, 24f);
                using (new EditorGUI.DisabledScope(!Editable))
                {
                    if (next != axis)
                    {
                        muzzleForward = Axes[next];
                        ApplyMarkers();
                    }
                    if (ArmoryCoreSetupGUI.Button("Guess the muzzle and the port from the model", !Geometry.IsEmpty,
                        "The barrel runs along the longest side; the muzzle is the thinner end, the port is on top of the receiver"))
                    {
                        GuessMuzzle();
                        GuessEjection();
                        ApplyMarkers();
                    }
                    if (Graph.ShowNumbers)
                    {
                        EditorGUI.BeginChangeCheck();
                        muzzlePosition = EditorGUILayout.Vector3Field("Muzzle, m", muzzlePosition);
                        muzzleForward = EditorGUILayout.Vector3Field("Barrel direction", muzzleForward);
                        if (EditorGUI.EndChangeCheck() && muzzleForward.sqrMagnitude > 1e-6f)
                            ApplyMarkers();
                    }
                }
                if (definition && definition.ammunition)
                {
                    var speed = definition.ammunition.muzzleVelocityMetersPerSecond * definition.muzzleVelocityMultiplier;
                    Graph.Hint($"Projectiles leave the muzzle along the barrel at <b>{speed:0} m/s</b>; a barrel attachment with its own muzzle point takes over while it is installed.");
                }
            }
        }

        private string AxisSummary()
        {
            var axis = System.Array.FindIndex(Axes, value => Vector3.Dot(value, muzzleForward.normalized) > 0.999f);
            return axis >= 0 ? "barrel " + AxisNames[axis] : "barrel at an angle";
        }

        private void DrawEffectsStep(int number)
        {
            var controller = Controller;
            var data = controller ? new SerializedObject(controller) : null;
            var effects = new List<string>();
            if (data != null)
            {
                if (data.FindProperty("muzzleFlash").objectReferenceValue)
                    effects.Add("flash");
                if (data.FindProperty("muzzleLight").objectReferenceValue)
                    effects.Add("light");
                if (data.FindProperty("casingPrefab").objectReferenceValue)
                    effects.Add("casings");
                if (data.FindProperty("recoilTarget").objectReferenceValue)
                    effects.Add("kick");
            }
            var state = !controller ? StepState.Locked : StepState.Optional;
            var summary = !controller ? "after the weapon is set up" : effects.Count == 0 ? "none" : string.Join(" · ", effects);
            if (!ArmoryCoreSetupGUI.Step("weapon.window.step.effects", number, "Muzzle flash & casings", state, summary, "Flash, light, casing ejection and visual kick of this weapon object"))
                return;
            using (ArmoryCoreSetupGUI.CardBody())
            {
                if (!controller)
                {
                    Graph.Hint("These settings live on the weapon object. Press «Set up weapon» below first.");
                    return;
                }
                using (new EditorGUI.DisabledScope(!Editable))
                {
                    data.Update();
                    EditorGUILayout.PropertyField(data.FindProperty("muzzleFlash"), new GUIContent("Flash particles", "Particle system emitted at the muzzle on every shot."));
                    if (data.FindProperty("muzzleFlash").objectReferenceValue)
                        Graph.Scale(data, "muzzleFlashParticles", "Particles per shot", null, ArmoryCoreWeaponControllerEditor.FlashScale);
                    EditorGUILayout.PropertyField(data.FindProperty("muzzleLight"), new GUIContent("Flash light", "Light switched on for a moment on every shot."));
                    if (data.FindProperty("muzzleLight").objectReferenceValue)
                        Graph.Scale(data, "muzzleLightDuration", "Light duration", null, ArmoryCoreWeaponControllerEditor.LightScale);

                    GUILayout.Space(2f);
                    var nextEjection = Graph.ToggleTile(ejection, new Graph.Tile("Casing ejection", "port on the receiver",
                        "Spent casings fly out of the ejection port (orange marker). Needs a casing prefab with a Rigidbody.") { Glyph = "⇗", Color = EjectionColor }, 34f);
                    if (nextEjection != ejection)
                    {
                        ejection = nextEjection;
                        ApplyEjectionToggle(data);
                    }
                    if (ejection)
                    {
                        var side = Graph.Choice(ejectRight ? 0 : 1, SideTiles, 2, 24f);
                        if (side != (ejectRight ? 0 : 1))
                        {
                            ejectRight = side == 0;
                            ApplyMarkers();
                        }
                        EditorGUILayout.PropertyField(data.FindProperty("casingPrefab"), new GUIContent("Casing prefab", "Rigidbody prefab of a spent casing."));
                        Graph.Scale(data, "casingEjectionForce", "Ejection impulse", "Impulse that throws the casing out of the port.", ArmoryCoreWeaponControllerEditor.EjectForceScale);
                        Graph.Scale(data, "casingEjectionTorque", "Spin", "Random spin given to the casing.", ArmoryCoreWeaponControllerEditor.EjectSpinScale);
                        Graph.Scale(data, "casingLifetime", "Casing lifetime", "Seconds before a casing is removed.", ArmoryCoreWeaponControllerEditor.CasingLifeScale);
                        if (!data.FindProperty("casingPrefab").objectReferenceValue)
                            Graph.Line("No casing prefab: the port is placed, but nothing flies out.", Graph.Warn);
                    }

                    GUILayout.Space(2f);
                    EditorGUILayout.PropertyField(data.FindProperty("recoilTarget"), new GUIContent("Kick target", "Transform pushed back and tilted by recoil; usually the visible model."));
                    if (data.FindProperty("recoilTarget").objectReferenceValue)
                        Graph.Scale(data, "visualKickMetersPerDegree", "Kick distance", "How far the target moves back per degree of recoil.", ArmoryCoreWeaponControllerEditor.KickScale);
                    if (data.ApplyModifiedProperties())
                        ArmoryCoreSetupGUI.MarkChanged(controller);
                }
            }
        }

        private void ApplyEjectionToggle(SerializedObject data)
        {
            var port = data.FindProperty("casingEjectionPoint");
            if (!ejection)
            {
                port.objectReferenceValue = null;
                return;
            }
            if (!port.objectReferenceValue)
                port.objectReferenceValue = EnsureChild(model.transform, EjectionName);
            data.ApplyModifiedProperties();
            ApplyMarkers();
        }

        // ---------- Footer ----------

        private List<string> Blocking()
        {
            var issues = new List<string>();
            if (EditorApplication.isPlaying)
                issues.Add("Leave Play Mode to change the weapon.");
            if (!model)
                issues.Add("Pick a model in the header.");
            else if (EditorUtility.IsPersistent(model))
                issues.Add("The model is a project asset: press «Open for editing».");
            if (!definition)
                issues.Add("Pick or create a definition in the header.");
            if (muzzleForward.sqrMagnitude < 1e-6f)
                issues.Add("The barrel direction is zero.");
            return issues;
        }

        private void DrawFooter()
        {
            var line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(line, new Color(0f, 0f, 0f, 0.35f));
            GUILayout.Space(3f);
            var blocking = Blocking();
            var issues = blocking.Concat(ArmoryCoreWeaponSteps.Validate(definition)).ToList();
            var configured = Controller;
            ArmoryCoreSetupGUI.Issues("weapon.window.issues", issues, configured ? "The weapon is set up. Try a shot in Play Mode." : "Ready to set up");
            if (ArmoryCoreSetupGUI.PrimaryButton(configured ? "Update weapon" : "Set up weapon", blocking.Count == 0, 30f,
                    "Adds ArmoryCoreWeaponController and an AudioSource, places the muzzle and the ejection port and assigns the definition. One Ctrl+Z undoes it."))
            {
                try
                {
                    Configure();
                    SetStatus("Done. Enter Play Mode and fire: projectiles leave the muzzle along the barrel.");
                }
                catch (System.Exception exception)
                {
                    SetStatus(exception.Message, true);
                }
            }
            status = ArmoryCoreSetupGUI.Status(status, statusError);
            GUILayout.Space(3f);
        }

        private void Configure()
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set up ArmoryCore weapon");
            var controller = Controller ? Controller : Undo.AddComponent<ArmoryCoreWeaponController>(model);
            var audio = model.GetComponent<AudioSource>() ? model.GetComponent<AudioSource>() : Undo.AddComponent<AudioSource>(model);
            Undo.RecordObject(audio, "Set up ArmoryCore weapon");
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            ArmoryCoreSetupGUI.MarkChanged(audio);

            var data = new SerializedObject(controller);
            data.Update();
            var muzzle = data.FindProperty("muzzle").objectReferenceValue as Transform;
            if (!muzzle)
                muzzle = EnsureChild(model.transform, MuzzleName);
            data.FindProperty("definition").objectReferenceValue = definition;
            data.FindProperty("muzzle").objectReferenceValue = muzzle;
            data.FindProperty("audioSource").objectReferenceValue = audio;
            var port = data.FindProperty("casingEjectionPoint");
            if (ejection && !port.objectReferenceValue)
                port.objectReferenceValue = EnsureChild(model.transform, EjectionName);
            if (!ejection)
                port.objectReferenceValue = null;
            data.ApplyModifiedProperties();
            ApplyMarkers();
            ArmoryCoreSetupGUI.MarkChanged(controller);
            Undo.CollapseUndoOperations(group);
        }

        private static Transform EnsureChild(Transform root, string name)
        {
            var existing = FindChild(root, name);
            if (existing)
                return existing;
            var child = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(child.gameObject, "Create " + name);
            child.SetParent(root, false);
            return child;
        }

        /// <summary>Moves the muzzle and the port of a configured weapon to the markers; the muzzle keeps its roll and its rest pose follows.</summary>
        private void ApplyMarkers()
        {
            var controller = Controller;
            if (!controller || !Editable || muzzleForward.sqrMagnitude < 1e-6f)
                return;
            var root = model.transform;
            var data = new SerializedObject(controller);
            var muzzle = data.FindProperty("muzzle").objectReferenceValue as Transform;
            if (muzzle)
            {
                Undo.RecordObject(muzzle, "Move muzzle");
                var forward = root.TransformDirection(muzzleForward.normalized);
                muzzle.SetPositionAndRotation(root.TransformPoint(muzzlePosition), Quaternion.FromToRotation(muzzle.forward, forward) * muzzle.rotation);
                ArmoryCoreSetupGUI.MarkChanged(muzzle);
                data.FindProperty("baseMuzzleLocalPosition").vector3Value = muzzle.localPosition;
                data.FindProperty("baseMuzzleLocalRotation").quaternionValue = muzzle.localRotation;
                data.FindProperty("baseMuzzlePoseConfigured").boolValue = true;
            }
            var port = data.FindProperty("casingEjectionPoint").objectReferenceValue as Transform;
            if (port && ejection)
            {
                Undo.RecordObject(port, "Move ejection port");
                var forward = root.TransformDirection(muzzleForward.normalized);
                var up = root.TransformDirection(Up);
                // Casings fly along the port's right axis and a little up.
                port.SetPositionAndRotation(root.TransformPoint(ejectionPosition), Quaternion.LookRotation(ejectRight ? forward : -forward, up));
                ArmoryCoreSetupGUI.MarkChanged(port);
            }
            if (data.ApplyModifiedProperties())
                ArmoryCoreSetupGUI.MarkChanged(controller);
        }

        // ---------- 3D view ----------

        private void DrawViewport()
        {
            if (tab == 1)
            {
                DrawBallistics();
                return;
            }
            EditorGUILayout.LabelField("3D model", EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetRect(200f, 2000f, 250f, 2000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            viewport ??= new ArmoryCoreModelViewport();
            DrawMarkers(rect, false);
            viewport.Draw(rect, model, Repaint);
            if (!model)
                return;
            DrawViewButtons(rect);
            DrawMarkers(rect, true);
        }

        private void DrawViewButtons(Rect rect)
        {
            var labels = new[] { new GUIContent("Side", "Look at the weapon from the side, barrel to the right"), new GUIContent("3D", "Oblique view") };
            var x = rect.xMax - 8f;
            for (var i = labels.Length - 1; i >= 0; i--)
            {
                var width = Mathf.Ceil(EditorStyles.miniButton.CalcSize(labels[i]).x) + 8f;
                x -= width;
                if (!GUI.Button(new Rect(x, rect.y + 36f, width, 18f), labels[i], i == 0 ? EditorStyles.miniButtonLeft : EditorStyles.miniButtonRight))
                    continue;
                var forward = muzzleForward.normalized;
                viewport.LookAlong(i == 0 ? Side : (Side + Up * 0.5f - forward * 0.3f).normalized, forward);
                Repaint();
            }
        }

        private int MarkerCount => ejection ? 2 : 1;

        private Vector3 MarkerLocal(int marker) => marker == 0 ? muzzlePosition : ejectionPosition;

        private void SetMarker(int marker, Vector3 local)
        {
            if (marker == 0)
                muzzlePosition = local;
            else
                ejectionPosition = local;
            ApplyMarkers();
        }

        /// <summary>Before the viewport the markers take the mouse; after it they are drawn over the model.</summary>
        private void DrawMarkers(Rect rect, bool draw)
        {
            if (!model)
                return;
            var e = Event.current;
            var root = model.transform;
            if (draw)
            {
                if (e.type != EventType.Repaint)
                    return;
                var muzzle = root.TransformPoint(muzzlePosition);
                var reach = Mathf.Max(0.15f, viewport.Bounds.size.magnitude * 0.45f);
                viewport.Arrow(rect, muzzle, muzzle + root.TransformDirection(muzzleForward.normalized) * reach, MuzzleColor, "Shot");
                viewport.Point(rect, muzzle, MuzzleColor, "Muzzle");
                if (ejection)
                {
                    var port = root.TransformPoint(ejectionPosition);
                    var side = root.TransformDirection((ejectRight ? Side : -Side) + Up * 0.4f).normalized;
                    viewport.Arrow(rect, port, port + side * reach * 0.25f, EjectionColor, null);
                    viewport.Point(rect, port, EjectionColor, "Ejection port");
                }
                GUI.Label(new Rect(rect.x + 12f, rect.yMax - 42f, rect.width - 24f, 18f),
                    Editable ? "Markers slide on the model   ·   Shift — free   ·   RMB — orbit" : "Open the model for editing to move the markers", EditorStyles.whiteMiniLabel);
                return;
            }
            if (!Editable || e.alt)
                return;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                for (var i = 0; i < MarkerCount; i++)
                {
                    var world = root.TransformPoint(MarkerLocal(i));
                    if (Vector2.Distance(e.mousePosition, viewport.Project(rect, world)) > 12f)
                        continue;
                    dragged = i;
                    dragAnchor = world;
                    e.Use();
                    break;
                }
            }
            else if (e.type == EventType.MouseDrag && dragged >= 0)
            {
                // The marker rides the model: it sits in the middle of the part under the cursor. Shift lets it go free.
                var pick = viewport.PickRay(rect, e.mousePosition);
                var toModel = root.worldToLocalMatrix;
                var ray = new Ray(toModel.MultiplyPoint3x4(pick.origin), toModel.MultiplyVector(pick.direction).normalized);
                if (!e.shift && !Geometry.IsEmpty)
                {
                    if (Geometry.RaycastThrough(ray, out var local))
                    {
                        SetMarker(dragged, local);
                        dragAnchor = root.TransformPoint(local);
                        Repaint();
                    }
                }
                else if (viewport.DragPoint(rect, dragAnchor, e.mousePosition, out var world))
                {
                    SetMarker(dragged, root.InverseTransformPoint(world));
                    Repaint();
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && dragged >= 0)
            {
                dragged = -1;
                e.Use();
            }
        }
    }
}
