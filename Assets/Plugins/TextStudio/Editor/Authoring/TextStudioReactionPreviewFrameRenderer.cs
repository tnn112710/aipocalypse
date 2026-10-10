using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace TextStudio.Editor
{
    internal enum TextStudioReactionPreviewAcquisitionStage
    {
        Scene,
        Camera,
        Destination,
        RenderTarget,
        Readback,
        FirstFrame
    }

    /// <summary>Renders a qualified reaction session with native TMP glyph geometry in an isolated scene.</summary>
#if UNITY_6000_7_OR_NEWER
    // Preview resources are disposed explicitly before entering Play Mode and on editor shutdown.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioReactionPreviewFrameRenderer : IDisposable
    {
        sealed class OutgoingLayer : IDisposable
        {
            readonly TextStudioReactionPreviewFrameRenderer _owner;
            readonly GameObject _object;
            readonly TextMeshPro _text;
            readonly TextStudioTMPFrameApplier _applier = new TextStudioTMPFrameApplier();
            readonly GlyphDirtyBits _dirty = new GlyphDirtyBits();
            readonly EvaluatedGlyphState[] _states;

            internal OutgoingLayer(TextStudioReactionPreviewFrameRenderer owner, int slot)
            {
                _owner = owner;
                _object = owner.CreateTextObject("Text Studio Reaction Output " + slot,
                    owner._beforeDisplay, owner._font, -0.05f);
                try
                {
                    _text = _object.GetComponent<TextMeshPro>();
                    _object.GetComponent<MeshRenderer>().sortingOrder = 1;
                    _applier.CacheSourceData(_text.textInfo, owner._session.BeforePlan);
                    _states = new EvaluatedGlyphState[owner._session.BeforePlan.VisibleGlyphCount];
                    _dirty.EnsureCapacity(_states.Length);
                    _object.SetActive(false);
                }
                catch
                {
                    _applier.Clear();
                    UnityEngine.Object.DestroyImmediate(_object);
                    throw;
                }
            }

            internal void Apply(int unit, in TextStudioReactionVisualSample sample,
                float time)
            {
                if (!_object.activeSelf)
                {
                    _object.SetActive(true);
                    _text.ForceMeshUpdate(true, true);
                    _applier.Clear();
                    _applier.CacheSourceData(_text.textInfo, _owner._session.BeforePlan);
                }
                for (int i = 0; i < _states.Length; i++)
                {
                    _states[i] = EvaluatedGlyphState.Identity;
                    _states[i].alpha = 0f;
                }
                if (unit >= 0 && unit < _states.Length && _owner._session.IsOutgoingChanged(unit))
                {
                    _states[unit].positionOffset = sample.LocalOffset;
                    _states[unit].rotation = sample.LocalRotation;
                    _states[unit].scale = sample.LocalScale;
                    _states[unit].alpha = sample.Alpha;
                    _states[unit].BlendColor(sample.Tint, TextStudioColorBlendMode.Set, 1f);
                }
                _dirty.SetAllDirty(_states.Length);
                TextStudioFrame frame = new TextStudioFrame(
                    _owner._session.BeforePlan, null, 0, null, PlacementMode.Linear,
                    _states, _states.Length, _dirty, 1f, DirtyFlags.Animation,
                    deltaTime: 0f, time: time);
                _applier.ApplyFrame(_text, in frame, null, null, true, 1f, false);
            }

            internal void Hide() => _object.SetActive(false);

            public void Dispose()
            {
                try { _applier.Clear(); }
                finally { if (_object != null) UnityEngine.Object.DestroyImmediate(_object); }
            }
        }

        static readonly HashSet<TextStudioReactionPreviewFrameRenderer> s_active =
            new HashSet<TextStudioReactionPreviewFrameRenderer>();

        static TextStudioReactionPreviewFrameRenderer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) DisposeAll();
            };
        }

        static void DisposeAll()
        {
            if (s_active.Count == 0) return;
            TextStudioReactionPreviewFrameRenderer[] renderers =
                new TextStudioReactionPreviewFrameRenderer[s_active.Count];
            s_active.CopyTo(renderers);
            for (int i = 0; i < renderers.Length; i++)
                try { renderers[i].Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
        }

        readonly TextStudioReactionPreviewSession _session;
        readonly TMP_FontAsset _font;
        readonly string _beforeDisplay;
        readonly Scene _scene;
        readonly GameObject _cameraObject;
        readonly GameObject _afterObject;
        readonly Camera _camera;
        readonly RenderTexture _target;
        readonly Texture2D _readback;
        readonly OutgoingLayer[] _layers;
        bool _disposed;

        internal TextStudioReactionPreviewFrameRenderer(
            TextStudioReactionPreviewSession session, int width, int height,
            TMP_FontAsset font = null,
            Action<TextStudioReactionPreviewAcquisitionStage> afterAcquisition = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _font = font != null ? font : TMP_Settings.defaultFontAsset;
            if (_font == null || _font.atlasPopulationMode != AtlasPopulationMode.Static)
            {
                _session.Dispose();
                throw new InvalidOperationException(
                    "Reaction preview needs a static TMP font asset with a stable atlas.");
            }
            _beforeDisplay = Display(session.BeforePlan);
            string afterDisplay = Display(session.AfterPlan);
            if (!FontContains(_font, _beforeDisplay) || !FontContains(_font, afterDisplay))
            {
                _session.Dispose();
                throw new InvalidOperationException(
                    "The static TMP preview font does not contain every sample glyph.");
            }

            try
            {
                _scene = EditorSceneManager.NewPreviewScene();
                if (EditorSceneManager.GetSceneCullingMask(_scene) == 0)
                    throw new InvalidOperationException(
                        "Unity could not allocate an isolated reaction preview scene.");
                afterAcquisition?.Invoke(TextStudioReactionPreviewAcquisitionStage.Scene);
                _cameraObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "Text Studio Reaction Preview Camera", HideFlags.HideAndDontSave,
                    typeof(Camera));
                SceneManager.MoveGameObjectToScene(_cameraObject, _scene);
                afterAcquisition?.Invoke(TextStudioReactionPreviewAcquisitionStage.Camera);
                _camera = _cameraObject.GetComponent<Camera>();
                _camera.scene = _scene;
                _camera.orthographic = true;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color32(31, 34, 40, 255);
                _camera.nearClipPlane = .1f;
                _camera.farClipPlane = 100f;
                _camera.transform.position = new Vector3(0f, 0f, -20f);
                _camera.allowHDR = false;
                _camera.allowMSAA = false;

                _afterObject = CreateTextObject("Text Studio Reaction Destination",
                    afterDisplay, _font, 0f);
                _afterObject.GetComponent<MeshRenderer>().sortingOrder = 0;
                _layers = new OutgoingLayer[_session.OutputCapacity];
                afterAcquisition?.Invoke(TextStudioReactionPreviewAcquisitionStage.Destination);

                int safeWidth = Mathf.Clamp(width, 64, 4096);
                int safeHeight = Mathf.Clamp(height, 64, 4096);
                _target = new RenderTexture(safeWidth, safeHeight, 24,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Text Studio Reaction Preview Frame",
                    hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _target.Create();
                _camera.targetTexture = _target;
                afterAcquisition?.Invoke(TextStudioReactionPreviewAcquisitionStage.RenderTarget);
                _readback = new Texture2D(safeWidth, safeHeight, TextureFormat.RGBA32,
                    false, false)
                {
                    name = "Text Studio Reaction Preview Readback",
                    hideFlags = HideFlags.HideAndDontSave
                };
                afterAcquisition?.Invoke(TextStudioReactionPreviewAcquisitionStage.Readback);

                TextMeshPro destination = _afterObject.GetComponent<TextMeshPro>();
                Bounds beforeBounds = MeasureText(_beforeDisplay, _font);
                Bounds afterBounds = destination.textBounds;
                float aspect = safeWidth / (float)safeHeight;
                float halfWidth = Mathf.Max(beforeBounds.extents.x, afterBounds.extents.x);
                float halfHeight = Mathf.Max(beforeBounds.extents.y, afterBounds.extents.y);
                _camera.orthographicSize = Mathf.Max(1f,
                    Mathf.Max(halfHeight, halfWidth / aspect) * 1.35f);
                s_active.Add(this);
                RenderAt(0f);
                afterAcquisition?.Invoke(TextStudioReactionPreviewAcquisitionStage.FirstFrame);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal RenderTexture Target => _target;
        internal int LiveNativeObjectCount => (_cameraObject != null ? 1 : 0) +
            (_afterObject != null ? 1 : 0) + ActiveLayerCount;
        internal int ActiveLayerCount
        {
            get
            {
                if (_layers == null) return 0;
                int count = 0;
                for (int i = 0; i < _layers.Length; i++)
                    if (_layers[i] != null) count++;
                return count;
            }
        }

        internal void RenderAt(float seconds)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TextStudioReactionPreviewFrameRenderer));
            try
            {
                _session.Seek(seconds);
                for (int slot = 0; slot < _layers.Length; slot++)
                {
                    if (_session.TryGetOutput(slot, out int unit,
                            out TextStudioReactionVisualSample sample))
                    {
                        _layers[slot] ??= new OutgoingLayer(this, slot);
                        _layers[slot].Apply(unit, in sample, seconds);
                    }
                    else
                        _layers[slot]?.Hide();
                }
                _camera.Render();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal Texture2D Readback()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TextStudioReactionPreviewFrameRenderer));
            try
            {
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = _target;
                    _readback.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
                    _readback.Apply(false, false);
                    return _readback;
                }
                finally { RenderTexture.active = previous; }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        GameObject CreateTextObject(string name, string value, TMP_FontAsset font, float z)
        {
            GameObject go = EditorUtility.CreateGameObjectWithHideFlags(name,
                HideFlags.HideAndDontSave, typeof(TextMeshPro));
            try
            {
                SceneManager.MoveGameObjectToScene(go, _scene);
                TextMeshPro text = go.GetComponent<TextMeshPro>();
                text.font = font;
                text.text = value;
                text.fontSize = 4f;
                text.color = TextStudioEffectPreviewUtility.BaseColor;
                text.alignment = TextAlignmentOptions.Center;
                text.richText = false;
                text.enableAutoSizing = false;
                text.rectTransform.sizeDelta = new Vector2(24f, 9f);
                UnityUtils.SetTextMeshProWordWrapping(text, false);
                text.ForceMeshUpdate(true, true);
                go.transform.position = new Vector3(-text.textBounds.center.x,
                    -text.textBounds.center.y, z);
                return go;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(go);
                throw;
            }
        }

        Bounds MeasureText(string value, TMP_FontAsset font)
        {
            GameObject measure = CreateTextObject("Text Studio Reaction Measure",
                value, font, 0f);
            try { return measure.GetComponent<TextMeshPro>().textBounds; }
            finally { UnityEngine.Object.DestroyImmediate(measure); }
        }

        static string Display(TextStudioCompiledPlan plan) =>
            plan == null ? string.Empty :
            !string.IsNullOrEmpty(plan.DisplayText) ? plan.DisplayText : plan.ResolvedText;

        static bool FontContains(TMP_FontAsset font, string value)
        {
            for (int i = 0; i < value.Length; i++)
                if (value[i] != '\n' && value[i] != '\r' && value[i] != '\t' &&
                    !font.HasCharacter(value[i], false, false)) return false;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            s_active.Remove(this);
            Cleanup(() => { if (_camera != null) _camera.targetTexture = null; });
            if (_layers != null)
                for (int i = 0; i < _layers.Length; i++)
                {
                    int index = i;
                    Cleanup(() => _layers[index]?.Dispose());
                }
            Cleanup(() =>
            {
                if (_target == null) return;
                try { _target.Release(); }
                finally { UnityEngine.Object.DestroyImmediate(_target); }
            });
            Cleanup(() => { if (_readback != null) UnityEngine.Object.DestroyImmediate(_readback); });
            Cleanup(() => { if (_afterObject != null) UnityEngine.Object.DestroyImmediate(_afterObject); });
            Cleanup(() => { if (_cameraObject != null) UnityEngine.Object.DestroyImmediate(_cameraObject); });
            Cleanup(() => { if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); });
            Cleanup(_session.Dispose);
        }

        static void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
