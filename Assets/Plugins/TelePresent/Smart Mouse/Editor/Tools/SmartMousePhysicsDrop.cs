/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 2021.2.
#if UNITY_2021_2_OR_NEWER
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.SceneManagement.PrefabStageUtility;
#else
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif

namespace TelePresent.SmartMouse
{
    [InitializeOnLoad]
    internal static partial class SmartMousePhysicsDrop
    {
        const double MaxSimSeconds = 30.0;
        const float LinearRestThreshold = 0.02f;
        const float AngularRestThreshold = 0.1f;
        const int RestStepsRequired = 5;

        const float SimulationSpeed = 3;

        const float GroundProbeLift = 0.5f;
        const float FallSlack = 1f;
        const float NoGroundFallLimit = 50f;

        const string ActiveKey = "SmartMouse_PhysicsDrop_Active";
        const string ModeKey = "SmartMouse_PhysicsDrop_OriginalMode";
        const string ModeChangedKey = "SmartMouse_PhysicsDrop_ModeChanged";

        class Body
        {
            public GameObject GameObject;
            public Rigidbody Rigidbody;
            public bool IsRoot;
            public Body RootBody;
            public bool GroundFound;
            public float FloorY;
            public float Height;
            public bool AddedRigidbody;
            public Collider AddedCollider;
            public readonly List<MeshCollider> MadeConvex = new List<MeshCollider>();
            public bool OrigIsKinematic;
            public bool OrigUseGravity;
            public bool OrigDetectCollisions;
            public CollisionDetectionMode OrigCollisionMode;
            public Vector3 OrigPosition;
            public Quaternion OrigRotation;
            public Vector3 OrigLinearVelocity;
            public Vector3 OrigAngularVelocity;
            public bool OrigSleeping;
            public int RestSteps;
        }

        class FrozenBody
        {
            public Rigidbody Rigidbody;
            public bool OrigIsKinematic;
            public Vector3 OrigLinearVelocity;
            public Vector3 OrigAngularVelocity;
            public bool OrigSleeping;
        }

        static readonly List<Body> _bodies = new List<Body>();
        static readonly List<FrozenBody> _frozenBodies = new List<FrozenBody>();
        static readonly List<PhysicsScene> _simulationScenes = new List<PhysicsScene>();
        static readonly List<MeshCollider> _temporaryGroundColliders = new List<MeshCollider>();
        static GameObject[] _roots;
        static bool _running;
        static bool _manualSimulationModeChanged;
        static int _stepCount;
        static int _maxSteps;
        static double _lastTickTime;
        static float _accumulator;

#if UNITY_2022_2_OR_NEWER
        static SimulationMode _originalSimulationMode = SimulationMode.FixedUpdate;
#else
        private static bool _originalAutoSimulation = true;
#endif

        static SmartMousePhysicsDrop()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.wantsToQuit += OnWantsToQuit;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
            SmartMouseSettings.EnabledChanged += OnEnabledChanged;

            AssemblyReloadEvents.beforeAssemblyReload += () => { if (_running) Stop(); };

            if (SessionState.GetBool(ActiveKey, false))
                RestoreAfterInterruptedRun();
        }

        [SmartMouseContextMenuItem(22)]
        public static void RegisterPhysicsMenu(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                menu.AddDisabledItem(new GUIContent("Selection/Settle With Physics", "Available in Edit Mode only."));
            else if (SmartMouseCompat.EditableSceneSelection(topLevelOnly: true).Length > 0)
                menu.AddItem(new GUIContent("Selection/Settle With Physics"), false, () => Settle(Selection.gameObjects));
            else
                menu.AddDisabledItem(new GUIContent("Selection/Settle With Physics"));
        }

        public static void Settle(GameObject[] selection)
        {
            if (_running) Stop();

            if (selection == null || selection.Length == 0) return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Smart Mouse: 'Settle With Physics' is available in Edit Mode only.");
                return;
            }

            GameObject[] sceneRoots = GetTopLevelSceneObjects(selection);
            if (sceneRoots.Length == 0) return;

            sceneRoots = System.Array.FindAll(sceneRoots, obj => !SmartMouseUtility.IsUIElement(obj));
            if (sceneRoots.Length == 0)
            {
                Debug.LogWarning("Smart Mouse: 'Settle With Physics' works on scene objects; UI elements belong to their canvas, so there is nothing to simulate.");
                return;
            }

            if (IsTwoDimensionalOnly(sceneRoots))
            {
                Debug.LogWarning("Smart Mouse: 'Settle With Physics' uses 3D physics; the selection only has 2D physics components, so there is nothing to simulate.");
                return;
            }

            _bodies.Clear();
            _frozenBodies.Clear();
            _simulationScenes.Clear();
            _temporaryGroundColliders.Clear();
            LastStopReason = SettleStopReason.None;

            int undoGroup = -1;
            try
            {
                var active = new HashSet<Rigidbody>();
                foreach (GameObject obj in sceneRoots)
                {
                    PhysicsScene physicsScene = obj.scene.GetPhysicsScene();
                    if (!_simulationScenes.Contains(physicsScene))
                        _simulationScenes.Add(physicsScene);

                    Body root = NewBody(obj, null);
                    // Register before any temporary mutation so cleanup still covers a partial setup.
                    _bodies.Add(root);
                    EnablePhysicsFor(root);
                    active.Add(root.Rigidbody);


                    foreach (Rigidbody childRb in obj.GetComponentsInChildren<Rigidbody>(false))
                    {
                        if (childRb == null || childRb == root.Rigidbody || childRb.isKinematic) continue;
                        Body child = NewBody(childRb.gameObject, root);
                        _bodies.Add(child);
                        EnablePhysicsFor(child);
                        active.Add(childRb);
                    }
                }

                if (_bodies.Count == 0)
                {
                    _simulationScenes.Clear();
                    return;
                }

                _roots = sceneRoots;
                PrepareGround();
                FreezeOtherBodies(active);

                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Settle With Physics");
                foreach (Body body in _bodies)
                    if (body.GameObject != null)
                        Undo.RegisterCompleteObjectUndo(body.GameObject.transform, "Settle With Physics");

                // Collapse now so later editor actions cannot fold into this undo group.
                Undo.CollapseUndoOperations(undoGroup);
                Undo.IncrementCurrentGroup();

                BeginManualSimulation();

                _stepCount = 0;
                _accumulator = 0f;
                _lastTickTime = EditorApplication.timeSinceStartup;
                _maxSteps = Mathf.CeilToInt((float)MaxSimSeconds / Mathf.Max(0.0001f, Time.fixedDeltaTime));
                _running = true;
                SessionState.SetBool(ActiveKey, true);

                EditorApplication.update += Tick;
                SceneView.duringSceneGui += OnSceneGUI;
                Undo.undoRedoPerformed += OnUndoRedoDuringRun;
            }
            catch (System.Exception e)
            {
                if (undoGroup >= 0)
                    Undo.CollapseUndoOperations(undoGroup);

                Debug.LogError($"Smart Mouse: could not start edit-mode physics. All temporary changes were reverted. {e.Message}");
                CleanupAfterRun(true);
            }
        }

        static Body NewBody(GameObject obj, Body rootBody)
        {
            return new Body
            {
                GameObject = obj,
                IsRoot = rootBody == null,
                RootBody = rootBody,
                OrigPosition = obj.transform.position,
                OrigRotation = obj.transform.rotation
            };
        }

        static void EnablePhysicsFor(Body body)
        {
            GameObject obj = body.GameObject;

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = obj.AddComponent<Rigidbody>();
                body.Rigidbody = rb;
                body.AddedRigidbody = true;
            }
            else
            {
                body.Rigidbody = rb;
                body.OrigIsKinematic = rb.isKinematic;
                body.OrigUseGravity = rb.useGravity;
                body.OrigDetectCollisions = rb.detectCollisions;
                body.OrigCollisionMode = rb.collisionDetectionMode;
                body.OrigLinearVelocity = LinearVelocity(rb);
                body.OrigAngularVelocity = rb.angularVelocity;
                body.OrigSleeping = rb.IsSleeping();
            }

            // Only a root gets a stand-in box; a jointed part without a collider simply has none.
            if (body.IsRoot && !HasUsableCollider(obj, rb))
                EnsureCollider(body);

            // Every non-convex MeshCollider in this compound body must be convex while the Rigidbody is dynamic.
            foreach (MeshCollider meshCollider in obj.GetComponentsInChildren<MeshCollider>(false))
            {
                if (meshCollider == null || meshCollider.convex || meshCollider.attachedRigidbody != rb) continue;
                body.MadeConvex.Add(meshCollider);
                meshCollider.convex = true;
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.WakeUp();
        }

        static void EnsureCollider(Body body)
        {
            GameObject obj = body.GameObject;
            BoxCollider box = obj.AddComponent<BoxCollider>();
            body.AddedCollider = box;

            MeshFilter rootFilter = obj.GetComponent<MeshFilter>();
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(false);
            if (rootFilter != null && rootFilter.sharedMesh != null &&
                renderers.Length == 1 && renderers[0].gameObject == obj)
            {
                box.center = rootFilter.sharedMesh.bounds.center;
                box.size = rootFilter.sharedMesh.bounds.size;
                return;
            }

            if (renderers.Length > 0)
            {
                Transform t = obj.transform;
                UnityEngine.Bounds localBounds = default;
                bool initialized = false;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled) continue;
                    EncapsulateWorldBounds(t, renderer.bounds, ref localBounds, ref initialized);
                }

                if (initialized)
                {
                    box.center = localBounds.center;
                    box.size = localBounds.size;
                }
            }

        }

        static bool HasUsableCollider(GameObject obj, Rigidbody rb)
        {
            foreach (Collider collider in obj.GetComponentsInChildren<Collider>(false))
                if (collider != null && collider.enabled && !collider.isTrigger && collider.attachedRigidbody == rb)
                    return true;
            return false;
        }

        static void EncapsulateWorldBounds(Transform root, UnityEngine.Bounds worldBounds,
            ref UnityEngine.Bounds localBounds, ref bool initialized)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 world = new Vector3(x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Vector3 local = root.InverseTransformPoint(world);
                if (!initialized)
                {
                    localBounds = new UnityEngine.Bounds(local, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(local);
                }
            }
        }
        
        static void PrepareGround()
        {
            SmartMouseUtility.EnsureBVH();
            foreach (Body body in _bodies)
            {
                if (!body.IsRoot) continue;
                GameObject obj = body.GameObject;
                Vector3 lowest = SnapAndAlignUtility.GetLowestPoint(obj);
                body.Height = Mathf.Max(0.1f, SnapAndAlignUtility.GetWorldBounds(new[] { obj }).size.y);
                if (!ProbeGround(body, lowest + Vector3.up * GroundProbeLift, Mathf.Infinity))
                    body.FloorY = lowest.y - NoGroundFallLimit;
            }

            foreach (Body body in _bodies)
            {
                if (body.IsRoot || body.RootBody == null) continue;
                body.Height = body.RootBody.Height;
                body.FloorY = body.RootBody.FloorY;
                body.GroundFound = body.RootBody.GroundFound;
            }
        }

        static bool ProbeGround(Body body, Vector3 origin, float maxDistance)
        {
            if (!SnapAndAlignUtility.TryGetClosestHit(origin, Vector3.down, out Vector3 hit, out _, out GameObject ground, _roots, maxDistance))
                return false;
            body.GroundFound = true;
            body.FloorY = hit.y - (2f * body.Height + FallSlack);
            EnsureGroundCollider(ground);
            return true;
        }

        static void EnsureGroundCollider(GameObject ground)
        {
            if (ground == null || !ground.activeInHierarchy) return;
            foreach (Collider existing in ground.GetComponents<Collider>())
                if (existing != null && existing.enabled && !existing.isTrigger)
                    return;
            if (ground.GetComponentInParent<Rigidbody>() != null) return;
            MeshFilter filter = ground.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;

            MeshCollider temp = ground.AddComponent<MeshCollider>();
            temp.sharedMesh = filter.sharedMesh;
            temp.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInInspector;
            _temporaryGroundColliders.Add(temp);
        }

        static Body FindFallenBody()
        {
            foreach (Body body in _bodies)
            {
                if (body.Rigidbody == null || body.Rigidbody.position.y >= body.FloorY) continue;
                if (!ProbeGround(body, body.Rigidbody.position + Vector3.up * GroundProbeLift, NoGroundFallLimit))
                    return body;
            }
            return null;
        }

        static void FreezeOtherBodies(HashSet<Rigidbody> active)
        {
            Rigidbody[] all = SmartMouseCompat.FindAll<Rigidbody>();
            foreach (Rigidbody rb in all)
            {
                if (rb == null || rb.isKinematic || active.Contains(rb) ||
                    !IsInSimulationScene(rb.gameObject))
                    continue;

                var frozen = new FrozenBody
                {
                    Rigidbody = rb,
                    OrigIsKinematic = rb.isKinematic,
                    OrigLinearVelocity = LinearVelocity(rb),
                    OrigAngularVelocity = rb.angularVelocity,
                    OrigSleeping = rb.IsSleeping()
                };
                _frozenBodies.Add(frozen);
                rb.isKinematic = true;
            }
        }

        static void BeginManualSimulation()
        {
            // Set before changing the global mode so a domain reload can still restore it.
            SessionState.SetBool(ActiveKey, true);
#if UNITY_2022_2_OR_NEWER
            _originalSimulationMode = Physics.simulationMode;
            _manualSimulationModeChanged = _originalSimulationMode != SimulationMode.Script;
            SessionState.SetInt(ModeKey, (int)_originalSimulationMode);
            SessionState.SetBool(ModeChangedKey, _manualSimulationModeChanged);
            if (_manualSimulationModeChanged)
                Physics.simulationMode = SimulationMode.Script;
#else
            _originalAutoSimulation = Physics.autoSimulation;
            _manualSimulationModeChanged = _originalAutoSimulation;
            SessionState.SetInt(ModeKey, _originalAutoSimulation ? 1 : 0);
            SessionState.SetBool(ModeChangedKey, _manualSimulationModeChanged);
            if (_manualSimulationModeChanged)
                Physics.autoSimulation = false;
#endif
        }

        internal enum SettleStopReason { None, Rest, Timeout, FellThrough, Error, Interrupted }

        internal static SettleStopReason LastStopReason { get; private set; }

        static float FixedStep => Mathf.Max(0.0001f, Time.fixedDeltaTime);

        static void Tick()
        {
            if (!_running)
            {
                EditorApplication.update -= Tick;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float fixedStep = FixedStep;
            _accumulator += (float)(now - _lastTickTime) * SimulationSpeed;
            _lastTickTime = now;
            _accumulator = Mathf.Min(_accumulator, fixedStep * 4f);

            while (_running && _accumulator >= fixedStep)
            {
                _accumulator -= fixedStep;
                if (!StepOnce()) return;
            }

            SceneView.RepaintAll();
        }

        // One fixed step of every simulated scene. False once the run has ended.
        static bool StepOnce()
        {
            float fixedStep = FixedStep;
            try
            {
                foreach (PhysicsScene physicsScene in _simulationScenes)
                    if (physicsScene.IsValid()) physicsScene.Simulate(fixedStep);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Smart Mouse: physics simulation step failed. Original transforms were restored. {e.Message}");
                LastStopReason = SettleStopReason.Error;
                StopInternal(true);
                return false;
            }

            _stepCount++;
            Body fallen = FindFallenBody();
            if (fallen != null)
            {
                Debug.LogWarning($"Smart Mouse: '{fallen.GameObject.name}' fell far below where it started" +
                    (fallen.GroundFound
                        ? ": it left the surface found under it and nothing else caught it."
                        : ": no collider or mesh surface was found under it.") +
                    " The simulation was stopped and the original transforms were restored.");
                LastStopReason = SettleStopReason.FellThrough;
                StopInternal(true);
                return false;
            }

            if (AllAtRest())
            {
                LastStopReason = SettleStopReason.Rest;
                StopInternal(false);
                return false;
            }

            if (_stepCount >= _maxSteps)
            {
                Debug.LogWarning("Smart Mouse: physics did not fully settle before the safety timeout; kept the current pose. Press Undo to revert.");
                LastStopReason = SettleStopReason.Timeout;
                StopInternal(false);
                return false;
            }
            return true;
        }

        static bool AllAtRest()
        {
            bool allAtRest = true;
            foreach (Body body in _bodies)
            {
                if (body.GameObject == null || body.Rigidbody == null) continue;

                bool atRest = body.Rigidbody.IsSleeping() ||
                              (LinearVelocity(body.Rigidbody).magnitude < LinearRestThreshold &&
                               body.Rigidbody.angularVelocity.magnitude < AngularRestThreshold);

                body.RestSteps = atRest ? body.RestSteps + 1 : 0;
                if (body.RestSteps < RestStepsRequired)
                    allAtRest = false;
            }
            return allAtRest;
        }

        public static void Stop()
        {
            if (_running) LastStopReason = SettleStopReason.Interrupted;
            StopInternal(false);
        }

        static void OnUndoRedoDuringRun()
        {
            if (!_running) return;
            LastStopReason = SettleStopReason.Interrupted;
            StopInternal(false);
        }

        static void StopInternal(bool restoreTransforms)
        {
            if (!_running && _bodies.Count == 0 && _frozenBodies.Count == 0 &&
                _temporaryGroundColliders.Count == 0 && !_manualSimulationModeChanged)
                return;

            _running = false;
            EditorApplication.update -= Tick;
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedoDuringRun;

            CleanupAfterRun(restoreTransforms);
        }

        static void CleanupAfterRun(bool restoreTransforms)
        {
            _running = false;
            EditorApplication.update -= Tick;
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedoDuringRun;

            try
            {
                foreach (Body body in _bodies)
                {
                    if (body.GameObject == null) continue;

                    if (body.AddedRigidbody && body.Rigidbody != null)
                    {
                        TryCleanup(() => Object.DestroyImmediate(body.Rigidbody));
                    }
                    else if (body.Rigidbody != null)
                    {
                        TryCleanup(() =>
                        {
                            body.Rigidbody.isKinematic = body.OrigIsKinematic;
                            body.Rigidbody.useGravity = body.OrigUseGravity;
                            body.Rigidbody.detectCollisions = body.OrigDetectCollisions;
                            body.Rigidbody.collisionDetectionMode = body.OrigCollisionMode;
                        });
                    }

                    if (body.AddedCollider != null)
                        TryCleanup(() => Object.DestroyImmediate(body.AddedCollider));

                    foreach (MeshCollider meshCollider in body.MadeConvex)
                        if (meshCollider != null)
                            TryCleanup(() => meshCollider.convex = false);

                    if (restoreTransforms && body.GameObject != null)
                        TryCleanup(() =>
                            body.GameObject.transform.SetPositionAndRotation(body.OrigPosition, body.OrigRotation));

                    // A transform teleport can wake a Rigidbody, so velocity and sleep are restored last.
                    if (!body.AddedRigidbody && body.Rigidbody != null)
                    {
                        TryCleanup(() =>
                        {
                            SetLinearVelocity(body.Rigidbody, body.OrigLinearVelocity);
                            body.Rigidbody.angularVelocity = body.OrigAngularVelocity;
                            RestoreSleepState(body.Rigidbody, body.OrigSleeping);
                        });
                    }

                    if (body.GameObject != null)
                        TryCleanup(() => EditorUtility.SetDirty(body.GameObject));
                }

                foreach (FrozenBody frozen in _frozenBodies)
                {
                    Rigidbody rb = frozen.Rigidbody;
                    if (rb == null) continue;

                    TryCleanup(() =>
                    {
                        rb.isKinematic = frozen.OrigIsKinematic;
                        SetLinearVelocity(rb, frozen.OrigLinearVelocity);
                        rb.angularVelocity = frozen.OrigAngularVelocity;
                        RestoreSleepState(rb, frozen.OrigSleeping);
                    });
                }

                foreach (MeshCollider temp in _temporaryGroundColliders)
                    if (temp != null)
                        TryCleanup(() => Object.DestroyImmediate(temp));
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _bodies.Clear();
                _frozenBodies.Clear();
                _simulationScenes.Clear();
                _temporaryGroundColliders.Clear();
                _roots = null;

                bool modeRestored = false;
                try
                {
                    RestoreSimulationMode();
                    modeRestored = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Smart Mouse: could not restore the editor physics simulation mode: {e.Message}");
                }
                finally
                {
                    if (modeRestored || !_manualSimulationModeChanged)
                    {
                        SessionState.EraseBool(ActiveKey);
                        SessionState.EraseInt(ModeKey);
                        SessionState.EraseBool(ModeChangedKey);
                    }
                    else
                    {
                        SessionState.SetBool(ActiveKey, true);
                    }
                }
                SceneView.RepaintAll();
            }
        }

        static void TryCleanup(System.Action cleanup)
        {
            try
            {
                cleanup();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        static void RestoreSimulationMode()
        {
#if UNITY_2022_2_OR_NEWER
            if (_manualSimulationModeChanged)
                Physics.simulationMode = _originalSimulationMode;
#else
            if (_manualSimulationModeChanged)
                Physics.autoSimulation = _originalAutoSimulation;
#endif
            _manualSimulationModeChanged = false;
        }

        static void RestoreAfterInterruptedRun()
        {
            bool modeChanged = SessionState.GetBool(ModeChangedKey, false);
#if UNITY_2022_2_OR_NEWER
            if (modeChanged)
                Physics.simulationMode = (SimulationMode)SessionState.GetInt(ModeKey, (int)SimulationMode.FixedUpdate);
#else
            if (modeChanged)
                Physics.autoSimulation = SessionState.GetInt(ModeKey, 1) != 0;
#endif
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseInt(ModeKey);
            SessionState.EraseBool(ModeChangedKey);
            Debug.LogWarning("Smart Mouse: a prior edit-mode physics run ended before its cleanup callback completed. The physics simulation mode was restored. If Unity itself terminated during the run, reload affected scenes without saving before continuing.");
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && _running)
                Stop();
        }

        static void OnEnabledChanged()
        {
            if (!SmartMouseSettings.IsSmartMouseEnabled && _running)
            {
                LastStopReason = SettleStopReason.Interrupted;
                StopInternal(true);
            }
        }

        static bool OnWantsToQuit()
        {
            if (_running) Stop();
            return true;
        }

        static void OnSceneSaving(Scene scene, string path)
        {
            if (_running) Stop();
        }

        static void OnPrefabSaving(GameObject prefabRoot)
        {
            if (_running) Stop();
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!_running) return;

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(8, 8, 150, 28));
            if (GUILayout.Button("Stop Physics", GUILayout.Height(24)))
                Stop();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        static Vector3 LinearVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        static void SetLinearVelocity(Rigidbody rb, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = velocity;
#else
            rb.velocity = velocity;
#endif
        }

        static void RestoreSleepState(Rigidbody rb, bool wasSleeping)
        {
            if (wasSleeping)
                rb.Sleep();
            else
                rb.WakeUp();
        }

        static GameObject[] GetTopLevelSceneObjects(GameObject[] selection)
        {
            var selected = new HashSet<GameObject>();
            var orderedSelected = new List<GameObject>(selection.Length);
            foreach (GameObject obj in selection)
            {
                if (!SmartMouseCompat.IsEditableSceneObject(obj))
                    continue;
                // The drop preview passes the editable check while detached from its scene, and
                // GetPhysicsScene throws on an invalid scene.
                if (!obj.scene.IsValid())
                    continue;

                PhysicsScene physicsScene = obj.scene.GetPhysicsScene();
                if (physicsScene.IsValid() && selected.Add(obj))
                    orderedSelected.Add(obj);
            }

            var roots = new List<GameObject>(selected.Count);
            foreach (GameObject obj in orderedSelected)
            {
                bool ancestorSelected = false;
                for (Transform parent = obj.transform.parent; parent != null; parent = parent.parent)
                {
                    if (!selected.Contains(parent.gameObject)) continue;
                    ancestorSelected = true;
                    break;
                }

                if (!ancestorSelected) roots.Add(obj);
            }

            if (roots.Count == 0 && selection.Length > 0)
                Debug.LogWarning("Smart Mouse: 'Settle With Physics' requires scene objects; prefab assets and unloaded objects were ignored.");

            return roots.ToArray();
        }

        static bool IsInSimulationScene(GameObject obj)
        {
            if (obj == null || !obj.scene.IsValid() || !obj.scene.isLoaded) return false;
            PhysicsScene physicsScene = obj.scene.GetPhysicsScene();
            return physicsScene.IsValid() && _simulationScenes.Contains(physicsScene);
        }

        static bool IsTwoDimensionalOnly(GameObject[] selection)
        {
            foreach (GameObject obj in selection)
            {
                if (obj == null) continue;

                bool has3D = obj.GetComponentInChildren<Collider>(false) != null ||
                             obj.GetComponentInChildren<MeshFilter>(false) != null ||
                             obj.GetComponentInChildren<SkinnedMeshRenderer>(false) != null;
                if (has3D) return false;

                bool has2D = obj.GetComponentInChildren<Rigidbody2D>(false) != null ||
                             obj.GetComponentInChildren<Collider2D>(false) != null ||
                             obj.GetComponentInChildren<SpriteRenderer>(false) != null;
                if (!has2D) return false;
            }
            return true;
        }
    }
}
