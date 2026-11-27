using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class OverlayFX : MonoBehaviour {
    private const float PARTICLE_SIZE_MULTIPLIER = 0.15f;

    private static readonly int _localBoundsMinimumId = Shader.PropertyToID("_LocalBoundsMinimum");
    private static readonly int _localBoundsMaximumId = Shader.PropertyToID("_LocalBoundsMaximum");
    private static readonly int _overlayPositionXId = Shader.PropertyToID("_OverlayPositionX");
    private static readonly int _overlayPositionYId = Shader.PropertyToID("_OverlayPositionY");
    private static readonly int _overlayPositionZId = Shader.PropertyToID("_OverlayPositionZ");

    public Material overlayMaterial;
    public Renderer targetRenderer;
    public List<ParticleSystem> particleSystems = new();

    [HideInInspector]
    public Vector3 rendererTrueForward;
#if UNITY_EDITOR
    public bool livePreviewSync = true;
#endif
    private readonly Dictionary<ParticleSystem, ParticleFrame> _particleFrames = new();
    private readonly List<ParticleFrame> _orderedFrames = new();
    private readonly Dictionary<ParticleSystem, float> _lastAutoSize = new();
    private readonly Dictionary<ParticleSystem, MotionScale> _motionScales = new();

    private sealed class MotionScale {
        public Vector4 Source;
        public Vector4 Applied;
        public ParticleSystemScalingMode ScalingMode;
        public ParticleSystemSimulationSpace VelocitySpace;
        public float StartSpeed;
        public float AppliedStartSpeed;
    }

    private sealed class ParticleFrame {
        public ParticleSystem Particles;
        public Quaternion LocalRotation;
        public Quaternion RelativeRotation;
        public Quaternion AppliedLocalRotation;
        public int Depth;
    }

    [NonSerialized] private Material _injectedMaterial;
    [NonSerialized] private Material _sourceMaterial;
    [NonSerialized] private Renderer _injectedRenderer;
    [NonSerialized] private Renderer _overlayRenderer;
    [NonSerialized] private Mesh _posedMesh;
    [NonSerialized] private Mesh _posedSourceMesh;
    [NonSerialized] private int _posedFrame = -1;
    [NonSerialized] private double _posedEditorTime = -1;
    private readonly List<Vector3> _posedVertices = new();

#if UNITY_EDITOR
    private static bool _editorTransition;
    private bool _savingScene;

    [InitializeOnLoadMethod]
    private static void RegisterEditorLifecycle() {
        AssemblyReloadEvents.beforeAssemblyReload -= ReleaseAllOverlays;
        AssemblyReloadEvents.beforeAssemblyReload += ReleaseAllOverlays;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void ReleaseAllOverlays() {
        _editorTransition = true;
        foreach (var overlay in Resources.FindObjectsOfTypeAll<OverlayFX>()) {
            if (!overlay.gameObject.scene.IsValid()) continue;
            overlay.ReleaseOverlay();
            overlay.RestoreOrientation();
            overlay.RestoreParticleMotion();
            overlay.RecordRestoredParticleSettings();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state) {
        _editorTransition = state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode;
        if (_editorTransition) {
            ReleaseAllOverlays();
        } else {
            EditorApplication.delayCall += () => {
                foreach (var overlay in Resources.FindObjectsOfTypeAll<OverlayFX>()) {
                    if (overlay.gameObject.scene.IsValid() && overlay.isActiveAndEnabled) overlay.RefreshOverlay();
                }
            };
        }
    }
#endif

    private void OnEnable() {
#if UNITY_EDITOR
        EditorApplication.update += RefreshInEditor;
        EditorSceneManager.sceneSaving += RestoreBeforeSave;
        EditorSceneManager.sceneSaved += RefreshAfterSave;
        TryBindPreviewParent();
#endif
        RefreshOverlay();
    }

#if UNITY_EDITOR
    private bool TryBindPreviewParent() {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ||
            _editorTransition || _savingScene || transform.parent == null) return false;

        Renderer parentRenderer = transform.parent.GetComponent<SkinnedMeshRenderer>();
        if (parentRenderer == null) parentRenderer = transform.parent.GetComponent<MeshRenderer>();
        if (parentRenderer == null || parentRenderer == targetRenderer) return false;

        Undo.RecordObject(this, "Assign overlay parent renderer");
        targetRenderer = parentRenderer;
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        return true;
    }

    private void OnTransformParentChanged() {
        if (isActiveAndEnabled && TryBindPreviewParent()) RefreshOverlay();
    }

    private void RefreshInEditor() {
        if (!Application.isPlaying && isActiveAndEnabled) {
            RefreshOverlay();
        }
    }

    private void RestoreBeforeSave(Scene scene, string path) {
        if (!Application.isPlaying && gameObject.scene == scene) {
            _savingScene = true;
            ReleaseOverlay();
            RestoreOrientation();
            RestoreParticleMotion();
            RecordRestoredParticleSettings();
        }
    }

    private void RefreshAfterSave(Scene scene) {
        if (gameObject.scene == scene) _savingScene = false;
        if (!Application.isPlaying && isActiveAndEnabled && gameObject.scene == scene) {
            RefreshOverlay();
        }
    }
#endif

#if UNITY_EDITOR
    private void RecordRestoredParticleSettings() {
        if (Application.isPlaying || particleSystems == null) return;
        foreach (var particles in particleSystems) {
            if (particles == null) continue;
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles.transform);
        }
    }
#endif

    private void LateUpdate() => RefreshOverlay();

    private void OnDrawGizmosSelected() {
        if (isActiveAndEnabled) {
            RefreshOverlay();
        }
    }

    private void OnDisable() {
#if UNITY_EDITOR
        EditorApplication.update -= RefreshInEditor;
        EditorSceneManager.sceneSaving -= RestoreBeforeSave;
        EditorSceneManager.sceneSaved -= RefreshAfterSave;
#endif
        ReleaseOverlay();
        RestoreOrientation();
        RestoreParticleMotion();
    }

    private void OnDestroy() => OnDisable();

    private void RestoreOrientation() {
        foreach (var frame in _orderedFrames) {
            if (frame.Particles != null) frame.Particles.transform.localRotation = frame.LocalRotation;
        }
        _particleFrames.Clear();
        _orderedFrames.Clear();
    }

    public void BindTargetRenderer(Renderer renderer) {
        if (targetRenderer != renderer) {
            ReleaseOverlay();
        }

        targetRenderer = renderer;
        if (isActiveAndEnabled) {
            RefreshOverlay();
        }
    }

    public void ClearTargetRenderer() {
        ReleaseOverlay();
        RestoreOrientation();
        RestoreParticleMotion();
        targetRenderer = null;
    }

    private void RefreshOverlay() {
#if UNITY_EDITOR
        if (_editorTransition || _savingScene || (!Application.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)) return;
#endif
        if (_injectedRenderer != targetRenderer || _sourceMaterial != overlayMaterial ||
            (_injectedMaterial != null && overlayMaterial != null && _injectedMaterial.shader != overlayMaterial.shader)) {
            ReleaseOverlay();
        }

        if (targetRenderer == null || overlayMaterial == null) {
            ReleaseOverlay();
            RestoreOrientation();
            RestoreParticleMotion();
            return;
        }

        if (_injectedMaterial == null) {
            _injectedRenderer = targetRenderer;
            _sourceMaterial = overlayMaterial;
            _injectedMaterial = new Material(overlayMaterial) {
                name = overlayMaterial.name + (Application.isPlaying ? " (Runtime)" : " (Preview)"),
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        EnsureOverlayAttached();

#if UNITY_EDITOR
        if (!Application.isPlaying && livePreviewSync) {
            _injectedMaterial.CopyPropertiesFromMaterial(overlayMaterial);
        }
#endif
        UpdateCoordinates();
        SyncParticleSystems();
    }

    private void EnsureOverlayAttached() {
        var materials = new List<Material>(targetRenderer.sharedMaterials);
        Mesh mesh = GetTargetMesh();
        int subMeshCount = mesh != null ? mesh.subMeshCount : materials.Count;
        bool changed = false;
        // Empty extra passes render the missing-material shader. Actual submesh slots are preserved.
        for (int slot = materials.Count - 1; slot >= subMeshCount; slot--) {
            if (materials[slot] == null) {
                materials.RemoveAt(slot);
                changed = true;
            }
        }
        if (mesh != null && mesh.subMeshCount > 1) {
            changed |= materials.RemoveAll(material => material == _injectedMaterial) > 0;
            if (changed) targetRenderer.sharedMaterials = materials.ToArray();
            EnsureOverlayRenderer(mesh);
            return;
        }
        ReleaseOverlayRenderer();
        if (!materials.Contains(_injectedMaterial)) {
            materials.Add(_injectedMaterial);
            changed = true;
        }
        if (changed) targetRenderer.sharedMaterials = materials.ToArray();
    }

    private void EnsureOverlayRenderer(Mesh mesh) {
        // Additional material slots only cover the final submesh; use one overlay pass per submesh.
        if (_overlayRenderer == null) {
            var surface = new GameObject("Overlay FX Surface") { hideFlags = HideFlags.HideAndDontSave };
            surface.transform.SetParent(targetRenderer.transform, false);
            if (targetRenderer is SkinnedMeshRenderer) {
                _overlayRenderer = surface.AddComponent<SkinnedMeshRenderer>();
            } else {
                surface.AddComponent<MeshFilter>();
                _overlayRenderer = surface.AddComponent<MeshRenderer>();
            }
            _overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        _overlayRenderer.gameObject.layer = targetRenderer.gameObject.layer;
        _overlayRenderer.enabled = targetRenderer.enabled;
        _overlayRenderer.forceRenderingOff = targetRenderer.forceRenderingOff;
        _overlayRenderer.sortingLayerID = targetRenderer.sortingLayerID;
        _overlayRenderer.sortingOrder = targetRenderer.sortingOrder;
        _overlayRenderer.renderingLayerMask = targetRenderer.renderingLayerMask;
        _overlayRenderer.lightProbeUsage = targetRenderer.lightProbeUsage;
        _overlayRenderer.reflectionProbeUsage = targetRenderer.reflectionProbeUsage;
        _overlayRenderer.probeAnchor = targetRenderer.probeAnchor;
        if (_overlayRenderer is SkinnedMeshRenderer overlaySkin && targetRenderer is SkinnedMeshRenderer targetSkin) {
            overlaySkin.sharedMesh = mesh;
            overlaySkin.bones = targetSkin.bones;
            overlaySkin.rootBone = targetSkin.rootBone;
            overlaySkin.localBounds = targetSkin.localBounds;
            overlaySkin.updateWhenOffscreen = targetSkin.updateWhenOffscreen;
            overlaySkin.quality = targetSkin.quality;
            for (int shape = 0; shape < mesh.blendShapeCount; shape++) overlaySkin.SetBlendShapeWeight(shape, targetSkin.GetBlendShapeWeight(shape));
        } else {
            _overlayRenderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            if (targetRenderer is MeshRenderer targetMesh) ((MeshRenderer)_overlayRenderer).additionalVertexStreams = targetMesh.additionalVertexStreams;
        }
        var overlayMaterials = _overlayRenderer.sharedMaterials;
        if (overlayMaterials.Length != mesh.subMeshCount || Array.Exists(overlayMaterials, m => m != _injectedMaterial)) {
            overlayMaterials = new Material[mesh.subMeshCount];
            for (int slot = 0; slot < overlayMaterials.Length; slot++) overlayMaterials[slot] = _injectedMaterial;
            _overlayRenderer.sharedMaterials = overlayMaterials;
        }
    }

    private void ReleaseOverlayRenderer() {
        if (_overlayRenderer == null) return;
        _overlayRenderer.enabled = false;
        var surface = _overlayRenderer.gameObject;
        _overlayRenderer.sharedMaterials = Array.Empty<Material>();
        _overlayRenderer = null;
        if (Application.isPlaying) Destroy(surface);
        else DestroyImmediate(surface);
    }

    private void ReleaseOverlay() {
        ReleaseOverlayRenderer();
        if (_posedMesh != null) {
            if (Application.isPlaying) Destroy(_posedMesh);
            else DestroyImmediate(_posedMesh);
        }
        _posedMesh = null;
        _posedSourceMesh = null;
        _posedVertices.Clear();
        _posedFrame = -1;
        _posedEditorTime = -1;
        if (_injectedMaterial != null) {
            if (_injectedRenderer != null) {
                var materials = new List<Material>(_injectedRenderer.sharedMaterials);
                bool changed = false;
                for (int slot = materials.Count - 1; slot >= 0; slot--) {
                    if (materials[slot] != _injectedMaterial) {
                        continue;
                    }
                    if (slot == materials.Count - 1) {
                        materials.RemoveAt(slot);
                    } else {
                        // Preserve other materials' submesh indices if the list was changed externally.
                        materials[slot] = null;
                    }
                    changed = true;
                }
                if (changed) {
                    _injectedRenderer.sharedMaterials = materials.ToArray();
                }
            }

            if (Application.isPlaying) {
                Destroy(_injectedMaterial);
            } else {
                DestroyImmediate(_injectedMaterial);
            }
        }

        _injectedMaterial = null;
        _injectedRenderer = null;
        _sourceMaterial = null;
    }

    private static Quaternion GetOverlayRotation() => Quaternion.identity;

    private void UpdateCoordinates() {
        Matrix4x4 worldToOverlay = Matrix4x4.TRS(targetRenderer.transform.position,
            GetOverlayRotation(), Vector3.one).inverse;

        // The shader's object space can differ from Transform space for skinned/batched renderers.
        var shaderRenderer = _overlayRenderer != null ? _overlayRenderer : targetRenderer;
        Matrix4x4 objectToOverlay = worldToOverlay * shaderRenderer.localToWorldMatrix;
        _injectedMaterial.SetVector(_overlayPositionXId, objectToOverlay.GetRow(0));
        _injectedMaterial.SetVector(_overlayPositionYId, objectToOverlay.GetRow(1));
        _injectedMaterial.SetVector(_overlayPositionZId, objectToOverlay.GetRow(2));

        // Skinned bind-pose bounds can use different axes from the rendered pose.
        Mesh mesh = GetTargetMesh();
        Bounds bounds;
        if (targetRenderer is SkinnedMeshRenderer skinned && mesh != null) {
            bounds = GetPosedBounds(skinned, worldToOverlay);
        } else if (mesh != null) {
            bounds = TransformBounds(mesh.bounds, worldToOverlay * targetRenderer.transform.localToWorldMatrix);
        } else {
            bounds = TransformBounds(targetRenderer.localBounds, objectToOverlay);
        }
        _injectedMaterial.SetVector(_localBoundsMinimumId, bounds.min);
        _injectedMaterial.SetVector(_localBoundsMaximumId, bounds.max);
    }

    private Bounds GetPosedBounds(SkinnedMeshRenderer skinned, Matrix4x4 worldToOverlay) {
        bool refresh = _posedMesh == null || _posedSourceMesh != skinned.sharedMesh;
        if (Application.isPlaying) refresh |= _posedFrame != Time.frameCount;
#if UNITY_EDITOR
        else refresh |= EditorApplication.timeSinceStartup - _posedEditorTime >= 0.05;
#endif
        if (_posedMesh == null) _posedMesh = new Mesh { name = "Overlay FX Posed Bounds", hideFlags = HideFlags.HideAndDontSave };
        if (refresh) {
            skinned.BakeMesh(_posedMesh, false);
            _posedMesh.GetVertices(_posedVertices);
            _posedSourceMesh = skinned.sharedMesh;
            _posedFrame = Time.frameCount;
#if UNITY_EDITOR
            _posedEditorTime = EditorApplication.timeSinceStartup;
#endif
        }
        Matrix4x4 posedToOverlay = worldToOverlay * skinned.transform.localToWorldMatrix;
        if (_posedVertices.Count == 0) return new Bounds(posedToOverlay.MultiplyPoint3x4(Vector3.zero), Vector3.zero);
        var bounds = new Bounds(posedToOverlay.MultiplyPoint3x4(_posedVertices[0]), Vector3.zero);
        for (int vertex = 1; vertex < _posedVertices.Count; vertex++) bounds.Encapsulate(posedToOverlay.MultiplyPoint3x4(_posedVertices[vertex]));
        return bounds;
    }

    private Mesh GetTargetMesh() {
        if (targetRenderer is SkinnedMeshRenderer skinned) {
            return skinned.sharedMesh;
        }
        if (targetRenderer.TryGetComponent(out MeshFilter meshFilter)) {
            return meshFilter.sharedMesh;
        }
        return null;
    }

    private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix) {
        Vector3 x = matrix.MultiplyVector(new Vector3(bounds.extents.x, 0f, 0f));
        Vector3 y = matrix.MultiplyVector(new Vector3(0f, bounds.extents.y, 0f));
        Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, bounds.extents.z));
        Vector3 extents = new Vector3(
            Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
            Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        return new Bounds(matrix.MultiplyPoint3x4(bounds.center), extents * 2f);
    }

    private void SyncParticleSystems() {
        if (particleSystems == null || particleSystems.Count == 0) {
            RestoreOrientation();
            RestoreParticleMotion();
            return;
        }

        NormalizeParticleFrames();

        Mesh mesh = GetTargetMesh();
        Vector3 worldSize = targetRenderer.bounds.size;
        if (mesh != null) {
            Matrix4x4 localToWorld = targetRenderer.transform.localToWorldMatrix;
            worldSize = new Vector3(
                localToWorld.MultiplyVector(Vector3.right * mesh.bounds.size.x).magnitude,
                localToWorld.MultiplyVector(Vector3.up * mesh.bounds.size.y).magnitude,
                localToWorld.MultiplyVector(Vector3.forward * mesh.bounds.size.z).magnitude);
        }
        float modelSize = Mathf.Max(worldSize.x, worldSize.y, worldSize.z);
        float scale = modelSize * PARTICLE_SIZE_MULTIPLIER;
        foreach (var particles in particleSystems) {
            if (particles == null) {
                continue;
            }
            var shape = particles.shape;
            shape.enabled = true;
            if (targetRenderer is MeshRenderer meshRenderer) {
                shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                shape.meshRenderer = meshRenderer;
            } else if (targetRenderer is SkinnedMeshRenderer skinnedRenderer) {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = skinnedRenderer;
            }
            var main = particles.main;
            if (!_lastAutoSize.TryGetValue(particles, out float lastSize) ||
                Mathf.Approximately(main.startSizeMultiplier, lastSize)) {
                main.startSizeMultiplier = scale;
                _lastAutoSize[particles] = scale;
            }
            ScaleParticleMotion(particles, modelSize);
        }
    }

    private void NormalizeParticleFrames() {
        for (int i = _orderedFrames.Count - 1; i >= 0; i--) {
            var frame = _orderedFrames[i];
            if (frame.Particles != null && particleSystems.Contains(frame.Particles)) continue;
            if (frame.Particles != null) frame.Particles.transform.localRotation = frame.LocalRotation;
            RestoreParticleMotion(frame.Particles);
            _particleFrames.Remove(frame.Particles);
            _orderedFrames.RemoveAt(i);
        }
        // Cache every authored relative rotation before changing any parent particle transform.
        foreach (var particles in particleSystems) {
            if (particles == null || _particleFrames.ContainsKey(particles)) continue;
            var particleTransform = particles.transform;
            if (targetRenderer.transform == particleTransform || targetRenderer.transform.IsChildOf(particleTransform)) continue;
            int depth = 0;
            for (var parent = particleTransform; parent != null && parent != transform; parent = parent.parent) {
                depth++;
            }
            var frame = new ParticleFrame {
                Particles = particles, LocalRotation = particleTransform.localRotation,
                AppliedLocalRotation = particleTransform.localRotation, Depth = depth
            };
            _particleFrames.Add(particles, frame);
            _orderedFrames.Add(frame);
        }
        _orderedFrames.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var frame in _orderedFrames) {
            if (Quaternion.Angle(frame.Particles.transform.localRotation, frame.AppliedLocalRotation) > 0.01f) {
                frame.LocalRotation = frame.Particles.transform.localRotation;
            }
            Quaternion relative = Quaternion.identity;
            var particleTransform = frame.Particles.transform;
            if (particleTransform.IsChildOf(transform)) {
                for (var parent = particleTransform; parent != transform; parent = parent.parent) {
                    var parentParticles = parent.GetComponent<ParticleSystem>();
                    relative = (parentParticles != null && _particleFrames.TryGetValue(parentParticles, out var parentFrame) ?
                        parentFrame.LocalRotation : parent.localRotation) * relative;
                }
            } else {
                relative = frame.LocalRotation;
            }
            frame.RelativeRotation = relative;
        }
        Quaternion rotation = GetOverlayRotation();
        foreach (var frame in _orderedFrames) {
            frame.Particles.transform.rotation = rotation * frame.RelativeRotation;
            frame.AppliedLocalRotation = frame.Particles.transform.localRotation;
        }
    }

    private void ScaleParticleMotion(ParticleSystem particles, float modelSize) {
        var velocity = particles.velocityOverLifetime;
        var main = particles.main;
        Vector4 current = new Vector4(velocity.xMultiplier, velocity.yMultiplier,
            velocity.zMultiplier, velocity.radialMultiplier);
        if (!_motionScales.TryGetValue(particles, out var state)) {
            state = new MotionScale {
                Source = current, Applied = current, ScalingMode = main.scalingMode,
                VelocitySpace = velocity.space,
                StartSpeed = main.startSpeedMultiplier, AppliedStartSpeed = main.startSpeedMultiplier
            };
            _motionScales.Add(particles, state);
        }
        if (!Mathf.Approximately(main.startSpeedMultiplier, state.AppliedStartSpeed)) state.StartSpeed = main.startSpeedMultiplier;
        for (int axis = 0; axis < 4; axis++) {
            if (!Mathf.Approximately(current[axis], state.Applied[axis])) {
                state.Source[axis] = current[axis];
            }
        }
        // Linear travel uses model lengths per second; angular orbit curves keep their authored rate.
        state.Applied = state.Source * modelSize;
        // Renderer shapes interpret local velocity in the mesh renderer's imported frame.
        // World velocity keeps the authored XYZ curves in the shared overlay frame.
        velocity.space = ParticleSystemSimulationSpace.World;
        // Emission follows the renderer's scale; size and travel are already scaled explicitly.
        main.scalingMode = ParticleSystemScalingMode.Shape;
        state.AppliedStartSpeed = state.StartSpeed * modelSize;
        main.startSpeedMultiplier = state.AppliedStartSpeed;
        velocity.xMultiplier = state.Applied.x;
        velocity.yMultiplier = state.Applied.y;
        velocity.zMultiplier = state.Applied.z;
        velocity.radialMultiplier = state.Applied.w;
    }

    private void RestoreParticleMotion() {
        foreach (var particles in new List<ParticleSystem>(_motionScales.Keys)) RestoreParticleMotion(particles);
    }

    private void RestoreParticleMotion(ParticleSystem particles) {
        if (!_motionScales.TryGetValue(particles, out var state)) return;
        if (particles != null) {
            var velocity = particles.velocityOverLifetime;
            var main = particles.main;
            if (velocity.space == ParticleSystemSimulationSpace.World) velocity.space = state.VelocitySpace;
            if (main.scalingMode == ParticleSystemScalingMode.Shape) main.scalingMode = state.ScalingMode;
            if (Mathf.Approximately(main.startSpeedMultiplier, state.AppliedStartSpeed)) main.startSpeedMultiplier = state.StartSpeed;
            if (Mathf.Approximately(velocity.xMultiplier, state.Applied.x)) velocity.xMultiplier = state.Source.x;
            if (Mathf.Approximately(velocity.yMultiplier, state.Applied.y)) velocity.yMultiplier = state.Source.y;
            if (Mathf.Approximately(velocity.zMultiplier, state.Applied.z)) velocity.zMultiplier = state.Source.z;
            if (Mathf.Approximately(velocity.radialMultiplier, state.Applied.w)) velocity.radialMultiplier = state.Source.w;
        }
        _motionScales.Remove(particles);
    }

    public enum GradientAxis { X, Y, Z }
    public enum UvDirection { X, Y, Z }
    public static class ShaderKeywordController {
        private static readonly string[] _gradientAxisKeywords = { "_GRADIENT_AXIS_X", "_GRADIENT_AXIS_Y", "_GRADIENT_AXIS_Z" };
        private static readonly string[] _uvDirectionKeywords = { "_UV_DIRECTION_X", "_UV_DIRECTION_Y", "_UV_DIRECTION_Z" };

        public static void SetGradientAxis(Material material, GradientAxis axis) {
            SetKeyword(material, _gradientAxisKeywords, (int)axis);
            material.SetFloat("_GRADIENT_AXIS", (int)axis);
        }

        public static void SetUvDirection(Material material, UvDirection direction) {
            SetKeyword(material, _uvDirectionKeywords, (int)direction);
            material.SetFloat("_UV_DIRECTION", (int)direction);
        }

        private static void SetKeyword(Material material, string[] keywords, int selected) {
            for (int i = 0; i < keywords.Length; i++) {
                if (i == selected) material.EnableKeyword(keywords[i]);
                else material.DisableKeyword(keywords[i]);
            }
        }
    }
}
