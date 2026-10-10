using TMPro;
using UnityEngine;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>A training encounter submits real semantic requests to native bounded emitters.</summary>
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioCombatLesson : MonoBehaviour
    {
        [SerializeField] GameObject _authoredExamples;
        [SerializeField] TextStudioEmitter _world, _projected;
        [SerializeField] GameObject _worldHost, _worldCalmHost, _projectedHost, _projectedCalmHost;
        [SerializeField] TextStudioEmissionPreset _damage, _separateDamage, _critical, _heal, _status, _combo, _loot;
        [SerializeField] Transform _target;
        [SerializeField] RectTransform _dummy;
        [SerializeField] TMP_Text _outputLabel, _densityLabel, _mergeLabel, _stackLabel, _overflowLabel, _followLabel, _moveLabel, _readout, _comboReadout;
        int _density = 1, _sequence, _emitted, _merged, _released, _rejected;
        float _timer, _travelTime;
        bool _projectedOutput, _aggregate = true, _moving, _statusDirty = true;
        TextStudioEmissionHandle _manual;
        public TextStudioEmitter SelectedEmitter => _projectedOutput ? _projected : _world;
        public int EmittedCount => _emitted;
        public int MergedCount => _merged;
        public int ReleasedCount => _released;
        public int RejectedCount => _rejected;
        public TextStudioEmissionHandle ManualStatus => _manual;
        public Transform Target => _target;
        public void Configure(TextStudioEmitter world, TextStudioEmitter projected, GameObject worldHost, GameObject worldCalmHost,
            GameObject projectedHost, GameObject projectedCalmHost, TextStudioEmissionPreset[] presets, Transform target, RectTransform dummy,
            TMP_Text output, TMP_Text density, TMP_Text merge, TMP_Text stack, TMP_Text overflow, TMP_Text follow, TMP_Text move, TMP_Text readout, TMP_Text comboReadout)
        {
            _world = world; _projected = projected; _worldHost = worldHost; _worldCalmHost = worldCalmHost; _projectedHost = projectedHost; _projectedCalmHost = projectedCalmHost;
            _damage = presets[0]; _separateDamage = presets[1]; _critical = presets[2]; _heal = presets[3]; _status = presets[4]; _combo = presets[5]; _loot = presets[6];
            _target = target; _dummy = dummy; _outputLabel = output; _densityLabel = density; _mergeLabel = merge; _stackLabel = stack; _overflowLabel = overflow;
            _followLabel = follow; _moveLabel = move; _readout = readout; _comboReadout = comboReadout; ResetLesson();
        }
        void OnEnable()
        {
            if (Application.isPlaying && _authoredExamples != null) _authoredExamples.SetActive(false);
            if (_world == null) return;
            _world.Emitted += Emitted; _projected.Emitted += Emitted; _world.Merged += Merged; _projected.Merged += Merged;
            _world.Released += Released; _projected.Released += Released; ResetLesson();
        }
        void OnDisable()
        {
            if (_world == null) return;
            _world.Emitted -= Emitted; _projected.Emitted -= Emitted; _world.Merged -= Merged; _projected.Merged -= Merged;
            _world.Released -= Released; _projected.Released -= Released; _world.ReleaseAll(); _projected.ReleaseAll();
        }
        void Update()
        {
            if (_world == null || !Application.isPlaying) return;
            if (_moving)
            {
                _travelTime += Time.unscaledDeltaTime;
                _dummy.anchoredPosition = new Vector2(Mathf.Sin(_travelTime * 1.2f) * 65, 0);
            }
            if (_density > 0)
            {
                _timer += Time.unscaledDeltaTime;
                float interval = _density == 1 ? 1.1f : .18f;
                if (_timer >= interval) { _timer = 0; Damage(); }
            }
            if (_statusDirty) Refresh();
        }
        void Emitted(TextStudioEmissionHandle handle) { _emitted++; _statusDirty = true; }
        void Merged(TextStudioEmissionHandle handle) { _merged++; _statusDirty = true; }
        void Released(TextStudioEmissionHandle handle) { _released++; _statusDirty = true; }
        void Accepted(TextStudioEmissionHandle handle) { if (!handle.IsValid) _rejected++; _statusDirty = true; }
        public void Damage() { _sequence++; Accepted(SelectedEmitter.EmitValue(_aggregate ? _damage : _separateDamage, 24 + _sequence % 17, _target)); }
        public void Critical() { _sequence++; Accepted(SelectedEmitter.EmitValue(_critical, 180 + _sequence % 80, _target)); }
        public void Heal() { Accepted(SelectedEmitter.EmitValue(_heal, 40, _target)); }
        public void Status()
        {
            if (_manual.IsValid) _manual.Dismiss();
            _manual = SelectedEmitter.EmitText(_status, "SHIELDED", _target); Accepted(_manual);
        }
        public void DismissStatus() { _manual.Dismiss(); }
        public void Combo() { _sequence++; Accepted(SelectedEmitter.EmitValue(_combo, 2 + _sequence % 7, _target)); _comboReadout.SetSampleText("COMBO " + (2 + _sequence % 7)); }
        public void Loot() { Accepted(SelectedEmitter.EmitText(_loot, "+25 GOLD", _target)); }
        public void Burst() { for (int i = 0; i < 24; i++) Damage(); }
        public void Clear() { _world.ReleaseAll(); _projected.ReleaseAll(); _statusDirty = true; }
        public void ToggleOutput() { Clear(); _projectedOutput = !_projectedOutput; Refresh(); }
        public void NextDensity()
        {
            Clear(); _density = (_density + 1) % 3; _timer = 0; ApplyHosts(); Refresh();
        }
        public void ToggleMerge() { _aggregate = !_aggregate; Refresh(); }
        public void ToggleStack() { _world.StackMatchingAnchors = _projected.StackMatchingAnchors = !_world.StackMatchingAnchors; Refresh(); }
        public void ToggleOverflow()
        {
            _world.OverflowPolicy = _projected.OverflowPolicy = _world.OverflowPolicy == TextStudioEmissionOverflowPolicy.RecycleOldest ? TextStudioEmissionOverflowPolicy.RejectNew : TextStudioEmissionOverflowPolicy.RecycleOldest; Refresh();
        }
        public void ToggleFollow() { _world.FollowTargets = _projected.FollowTargets = !_world.FollowTargets; Refresh(); }
        public void ToggleTravel() { _moving = !_moving; Refresh(); }
        public void InspectEmitter() { TextStudioLessonActions.Inspect(SelectedEmitter); }
        void ApplyHosts()
        {
            _world.DefaultHostPrefab = _density == 0 ? _worldCalmHost : _worldHost;
            _projected.DefaultHostPrefab = _density == 0 ? _projectedCalmHost : _projectedHost;
            if (Application.isPlaying) { _world.Prewarm(); _projected.Prewarm(); }
        }
        public void ResetLesson()
        {
            Clear(); _density = 1; _sequence = _emitted = _merged = _released = _rejected = 0;
            _timer = _travelTime = 0; _projectedOutput = _moving = false; _aggregate = true; _dummy.anchoredPosition = Vector2.zero;
            _world.StackMatchingAnchors = _projected.StackMatchingAnchors = true; _world.FollowTargets = _projected.FollowTargets = true;
            _world.OverflowPolicy = _projected.OverflowPolicy = TextStudioEmissionOverflowPolicy.RecycleOldest;
            _comboReadout.SetSampleText(""); ApplyHosts(); Refresh();
            if (Application.isPlaying) { Damage(); Critical(); Heal(); }
        }
        void Refresh()
        {
            _statusDirty = false;
            _outputLabel.SetSampleText(_projectedOutput ? "Screen projected" : "World text");
            _densityLabel.SetSampleText(_density == 0 ? "Calm" : _density == 1 ? "Ordinary" : "Dense");
            _mergeLabel.SetSampleText(_aggregate ? "Aggregate hits" : "Separate hits"); _stackLabel.SetSampleText(_world.StackMatchingAnchors ? "Stack labels" : "Same anchor");
            _overflowLabel.SetSampleText(_world.OverflowPolicy == TextStudioEmissionOverflowPolicy.RecycleOldest ? "Recycle oldest" : "Reject new");
            _followLabel.SetSampleText(_world.FollowTargets ? "Follow target" : "Stay at impact"); _moveLabel.SetSampleText(_moving ? "Stop target" : "Move target");
            _readout.SetSampleText("Active " + SelectedEmitter.ActiveCount + " / " + SelectedEmitter.MaximumActiveEmissions + "  |  Created " + SelectedEmitter.CreatedCount + "\nEmitted " + _emitted + " / merged " + _merged + " / released " + _released + " / rejected " + _rejected);
        }
    }
}
