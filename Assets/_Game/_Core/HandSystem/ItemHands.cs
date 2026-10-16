using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.HandSystem
{
    // Where the hands hold an item, and its hand actions (reload, clear jam…). An action moves the item's own parts
    // (mag, bolt, a shell) over its 0..1 progress and switches which grip each hand holds; hands on a moving part's grip
    // ride along. The item's view (HeldGun…) plays actions by name and sets their progress; FirstPersonHands places the hands.
    [DisallowMultipleComponent]
    public sealed class ItemHands : MonoBehaviour
    {
        // Names the views play. Others are allowed, for new item kinds.
        public static class Actions
        {
            public const string Reload = "Reload";
            // Reload from an empty magazine (racks the bolt); falls back to Reload.
            public const string ReloadEmpty = "ReloadEmpty";
            // Per-round reload: one round going in; progress restarts each round.
            public const string LoadRound = "LoadRound";
            public const string ClearJam = "ClearJam";
            // Pump / bolt worked after a shot.
            public const string Cycle = "Cycle";

            public static readonly string[] All = { Reload, ReloadEmpty, LoadRound, ClearJam, Cycle };
        }

        [Serializable]
        public struct HandKey
        {
            [Range(0f, 1f)] public float Time;
            public HandSide Side;
            [Tooltip("Grip the hand moves to; none = back to its default grip")] public HandGrip Grip;
            [Min(0f), Tooltip("Seconds to get there; 0 = the item's default blend")] public float Blend;
        }

        [Serializable]
        public struct PartKey
        {
            [Range(0f, 1f)] public float Time;
            [Tooltip("Offset from the part's rest position, in its parent's space")] public Vector3 Position;
            [Tooltip("Rotation on top of the part's rest rotation")] public Vector3 Euler;
        }

        [Serializable]
        public sealed class PartTrack
        {
            [Tooltip("Moved by the keys. The track starts and ends at rest unless keys sit at 0 / 1")] public Transform Part;
            [ListDrawerSettings(ShowFoldout = false)] public PartKey[] Keys = Array.Empty<PartKey>();
        }

        [Serializable]
        public struct ShowSpan
        {
            [Tooltip("Shown only between From and To while the action plays (a shell in the hand)")] public GameObject Target;
            [Range(0f, 1f)] public float From;
            [Range(0f, 1f)] public float To;
        }

        [Serializable]
        public sealed class HandAction
        {
            [ValueDropdown(nameof(Names), AppendNextDrawer = true)] public string Name;
            [ListDrawerSettings(ShowFoldout = false)] public HandKey[] Hands = Array.Empty<HandKey>();
            [ListDrawerSettings(ShowFoldout = false)] public PartTrack[] Parts = Array.Empty<PartTrack>();
            [ListDrawerSettings(ShowFoldout = false)] public ShowSpan[] Show = Array.Empty<ShowSpan>();

            static IEnumerable<string> Names => Actions.All;
        }

        struct Rest
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Active;
        }

        [SerializeField] HandGrip _right;
        [SerializeField, Tooltip("None = this hand stays off screen (one-handed item)")] HandGrip _left;
        [SerializeField, Min(0.1f), Tooltip("Hand size for this item; the hand's grip point stays on its contact")] float _handScale = 1f;
        [SerializeField, Min(0f), Tooltip("Seconds a hand takes to move between grips, unless a key says otherwise")]
        float _blend = 0.12f;
        [SerializeField, ListDrawerSettings(ShowFoldout = true, ListElementLabelName = "Name")] HandAction[] _actions = Array.Empty<HandAction>();

        readonly Dictionary<UnityEngine.Object, Rest> _rest = new();
        HandAction _playing;
        float _progress;

        public bool IsPlaying => _playing != null;
        public string Playing => _playing?.Name;
        public float DefaultBlend => _blend;
        public float HandScale => _handScale;

        void Awake() => CaptureRest();

        public bool Has(string action) => Find(action) != null;

        // Starts an action at progress 0. False (and back to the default grips) when the item has no such action.
        public bool Play(string action)
        {
            HandAction next = Find(action);
            if (_playing != null && _playing != next)
                ResetParts(_playing);
            _playing = next;
            _progress = 0f;
            if (next != null)
                Evaluate(next, 0f);
            return next != null;
        }

        public void Stop()
        {
            if (_playing == null)
                return;
            ResetParts(_playing);
            _playing = null;
            _progress = 0f;
        }

        public void SetProgress(float progress)
        {
            if (_playing == null)
                return;
            _progress = Mathf.Clamp01(progress);
            Evaluate(_playing, _progress);
        }

        public HandGrip GetDefault(HandSide side) => side == HandSide.Left ? _left : _right;

        // The grip this hand holds now, and how long it takes to get there when it changes.
        public HandGrip GetGrip(HandSide side, out float blend)
        {
            blend = _blend;
            if (_playing == null)
                return GetDefault(side);
            return GripAt(_playing, side, _progress, out blend);
        }

        HandGrip GripAt(HandAction action, HandSide side, float progress, out float blend)
        {
            blend = _blend;
            HandGrip grip = GetDefault(side);
            float latest = -1f;
            foreach (HandKey key in action.Hands)
            {
                if (key.Side != side || key.Time > progress || key.Time < latest)
                    continue;
                latest = key.Time;
                grip = key.Grip != null ? key.Grip : GetDefault(side);
                blend = key.Blend > 0f ? key.Blend : _blend;
            }
            return grip;
        }

        HandAction Find(string action)
        {
            if (string.IsNullOrEmpty(action))
                return null;
            foreach (HandAction candidate in _actions)
            {
                if (candidate != null && candidate.Name == action)
                    return candidate;
            }
            return null;
        }

        void CaptureRest()
        {
            foreach (HandAction action in _actions)
            {
                foreach (PartTrack track in action.Parts)
                {
                    if (track.Part != null && !_rest.ContainsKey(track.Part))
                        _rest.Add(track.Part, new Rest { Position = track.Part.localPosition, Rotation = track.Part.localRotation });
                }
                foreach (ShowSpan span in action.Show)
                {
                    if (span.Target != null && !_rest.ContainsKey(span.Target))
                        _rest.Add(span.Target, new Rest { Active = span.Target.activeSelf });
                }
            }
        }

        void Evaluate(HandAction action, float progress)
        {
            foreach (PartTrack track in action.Parts)
            {
                if (track.Part == null || !_rest.TryGetValue(track.Part, out Rest rest))
                    continue;
                Sample(track.Keys, progress, out Vector3 position, out Quaternion rotation);
                track.Part.SetLocalPositionAndRotation(rest.Position + position, rest.Rotation * rotation);
            }
            foreach (ShowSpan span in action.Show)
            {
                if (span.Target == null)
                    continue;
                bool shown = progress >= span.From && progress < span.To;
                if (span.Target.activeSelf != shown)
                    span.Target.SetActive(shown);
            }
        }

        void ResetParts(HandAction action)
        {
            foreach (PartTrack track in action.Parts)
            {
                if (track.Part != null && _rest.TryGetValue(track.Part, out Rest rest))
                    track.Part.SetLocalPositionAndRotation(rest.Position, rest.Rotation);
            }
            foreach (ShowSpan span in action.Show)
            {
                if (span.Target != null && _rest.TryGetValue(span.Target, out Rest rest))
                    span.Target.SetActive(rest.Active);
            }
        }

        // Smoothstep between keys, with implicit rest keys at 0 and 1 when the track has none there.
        static void Sample(PartKey[] keys, float t, out Vector3 position, out Quaternion rotation)
        {
            PartKey from = default;
            PartKey to = new() { Time = 1f };
            foreach (PartKey key in keys)
            {
                if (key.Time <= t && key.Time >= from.Time)
                    from = key;
                if (key.Time > t && key.Time <= to.Time)
                    to = key;
            }
            float span = to.Time - from.Time;
            float w = span > 0f ? Mathf.SmoothStep(0f, 1f, (t - from.Time) / span) : 1f;
            position = Vector3.Lerp(from.Position, to.Position, w);
            rotation = Quaternion.Slerp(Quaternion.Euler(from.Euler), Quaternion.Euler(to.Euler), w);
        }

#if UNITY_EDITOR
        // Edit-mode scrubbing: poses the parts and ghost hands at a point of an action. Parts go back to rest when the
        // preview ends or the prefab is saved or closed.
        [FoldoutGroup("Preview"), ShowInInspector, ValueDropdown(nameof(PreviewNames)), OnValueChanged(nameof(UpdatePreview))]
        string _previewAction;
        [FoldoutGroup("Preview"), ShowInInspector, Range(0f, 1f), OnValueChanged(nameof(UpdatePreview))]
        float _previewTime;
        bool _previewing;

        IEnumerable<string> PreviewNames()
        {
            yield return "(default grips)";
            foreach (HandAction action in _actions)
                yield return action.Name;
        }

        [FoldoutGroup("Preview"), Button("Show hands"), HideIf(nameof(_previewing))]
        void UpdatePreview()
        {
            if (!_previewing)
            {
                if (Application.isPlaying)
                    return;
                _previewing = true;
                _rest.Clear();
                CaptureRest();
                UnityEditor.SceneManagement.PrefabStage.prefabSaving += OnPrefabSaving;
                UnityEditor.SceneManagement.PrefabStage.prefabStageClosing += OnPrefabStageClosing;
                foreach (HandSide side in new[] { HandSide.Left, HandSide.Right })
                {
                    HandSide s = side;
                    HandPreview.Show(this, s, () => PreviewGrip(s));
                }
            }

            HandAction action = Find(_previewAction);
            foreach (HandAction other in _actions)
            {
                if (other != action)
                    ResetParts(other);
            }
            if (action != null)
                Evaluate(action, _previewTime);
            UnityEditor.SceneView.RepaintAll();
        }

        HandGrip PreviewGrip(HandSide side)
        {
            HandAction action = Find(_previewAction);
            return action != null ? GripAt(action, side, _previewTime, out _) : GetDefault(side);
        }

        // Fits every grip of this item (defaults and action grips) to its meshes. Fits use the parts' rest poses.
        [FoldoutGroup("Preview"), Button("Bake fit for all grips")]
        void BakeAll()
        {
            bool previewing = _previewing;
            EndPreview();
            foreach (HandGrip grip in GetComponentsInChildren<HandGrip>(true))
            {
                UnityEditor.Undo.RecordObject(grip, "Bake hand fit");
                HandFit.Bake(grip);
                UnityEditor.EditorUtility.SetDirty(grip);
            }
            if (previewing)
                UpdatePreview();
        }

        [FoldoutGroup("Preview"), Button("End preview"), ShowIf(nameof(_previewing))]
        void EndPreview()
        {
            if (!_previewing)
                return;
            _previewing = false;
            UnityEditor.SceneManagement.PrefabStage.prefabSaving -= OnPrefabSaving;
            UnityEditor.SceneManagement.PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            if (this == null)
                return;
            foreach (HandAction action in _actions)
                ResetParts(action);
            HandPreview.Hide(this);
        }

        void OnPrefabSaving(GameObject _) => EndPreview();
        void OnPrefabStageClosing(UnityEditor.SceneManagement.PrefabStage _) => EndPreview();

        void OnDestroy()
        {
            if (!Application.isPlaying)
                EndPreview();
        }
#endif
    }
}
