using System.Collections;
using TMPro;
using TextStudio.Examples.CustomEffects;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Coordinates a game outcome while native actions, factories and Code Effects own text behavior.</summary>
    [ExecuteAlways]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    [UnityEngine.AddComponentMenu("")]
    public sealed partial class TextStudioExtensionLesson : MonoBehaviour
    {
        sealed class RegisteredGate : ITextStudioAction, ITextStudioActionDefinitionProvider
        {
            public string Id => TextStudioCourseGateAction.ActionId;
            public TextStudioActionDefinition ActionDefinition => TextStudioCourseGateAction.Define(Id);
            public IEnumerator Execute(ActionContext context)
            {
                Component host = context.Owner as Component;
                TextStudioExtensionLesson lesson = host != null ? host.GetComponent<TextStudioExtensionLesson>() : null;
                if (lesson != null) lesson.OpenGate(Mathf.RoundToInt(context.Marker.GetFloat("reward", 0, 25)), "Registered action");
                yield break;
            }
        }
        [SerializeField] TextStudioText _subject, _reward;
        [SerializeField] TextStudioCourseGateAction _local;
        [SerializeField] TextStudioCustomEffectSampleHost _effects;
        [SerializeField] TextStudioOrbitCodeEffectAsset _asset;
        [SerializeField] RawImage _gate;
        [SerializeField] Texture2D _closed, _open;
        [SerializeField] TMP_Text _gateStatus, _actionLabel, _effectLabel, _parametersLabel, _areaLabel, _compositionLabel, _ownerLabel, _previewLabel, _source;
        // Registered actions are shared by live ExecuteAlways lesson owners, including Fast Enter Play Mode.
        static readonly RegisteredGate s_registration = new RegisteredGate();
        static int s_registrationOwners;
        bool _registered;
        bool _global, _assetRoute, _combined, _ownerEnabled = true;
        int _parameters, _area, _actionCount, _coins;
        public int ActionCount => _actionCount;
        public int Coins => _coins;
        public TextStudioText Subject => _subject;
        public bool OwnerEnabled => _ownerEnabled;
        public void Configure(TextStudioText subject, TextStudioText reward, TextStudioCourseGateAction local, TextStudioCustomEffectSampleHost effects, TextStudioOrbitCodeEffectAsset asset,
            RawImage gate, Texture2D closed, Texture2D open, TMP_Text gateStatus, TMP_Text actionLabel, TMP_Text effectLabel, TMP_Text parametersLabel, TMP_Text areaLabel, TMP_Text compositionLabel, TMP_Text ownerLabel, TMP_Text previewLabel, TMP_Text source)
        {
            _subject = subject; _reward = reward; _local = local; _effects = effects; _asset = asset; _gate = gate; _closed = closed; _open = open; _gateStatus = gateStatus;
            _actionLabel = actionLabel; _effectLabel = effectLabel; _parametersLabel = parametersLabel; _areaLabel = areaLabel; _compositionLabel = compositionLabel; _ownerLabel = ownerLabel; _previewLabel = previewLabel; _source = source; ResetLesson();
        }
        // Players start with the reveal running, so the native gate action fires without pressing Replay first.
        void OnEnable() { if (_subject == null) return; ApplyOwnership(); if (Application.isPlaying) Replay(); }
        void OnDisable() { ReleaseRegistration(); if (_local != null) _local.enabled = false; if (_effects != null) _effects.enabled = false; }
        void ApplyOwnership()
        {
            ReleaseRegistration(); _local.enabled = _ownerEnabled && !_global; _effects.enabled = _ownerEnabled;
            if (_ownerEnabled && _global)
            {
                if (s_registrationOwners++ == 0) TextStudioActions.Register(s_registration);
                _registered = true;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RestoreRegistration() { if (s_registrationOwners > 0) TextStudioActions.Register(s_registration); }
        void ReleaseRegistration()
        {
            if (!_registered) return;
            _registered = false;
            if (--s_registrationOwners == 0 && TextStudioActions.TryGet(s_registration.Id, out ITextStudioAction current) && ReferenceEquals(current, s_registration))
                TextStudioActions.Unregister(s_registration.Id);
        }
        public void OpenGate(int reward, string route)
        {
            _actionCount++; _coins += Mathf.Clamp(reward, 0, 100); _gate.texture = _open;
            _gateStatus.SetSampleText(route + " / gate open"); _reward.ShowText("REWARD / " + _coins + " COINS");
        }
        public void NextAction() { _global = !_global; ApplyOwnership(); Replay(); }
        public void NextEffect() { _assetRoute = !_assetRoute; Replay(); }
        public void NextParameters() { _parameters = (_parameters + 1) % 3; Replay(); }
        public void NextArea() { _area = (_area + 1) % 3; Replay(); }
        public void ToggleComposition() { _combined = !_combined; Replay(); }
        public void ToggleOwner() { _ownerEnabled = !_ownerEnabled; ApplyOwnership(); Replay(); }
        public void TogglePreviewActions() { _subject.SetRunActionsInPreview(!_subject.RunActionsInPreview); RefreshLabels(); }
        public void InspectImplementation() { TextStudioLessonActions.Inspect(!_ownerEnabled ? (UnityEngine.Object)_subject : _assetRoute ? _asset : (UnityEngine.Object)_effects); }
        public void ResetLesson()
        {
            _global = _assetRoute = _combined = false; _ownerEnabled = true; _parameters = _area = _actionCount = _coins = 0;
            _subject.SetRunActionsInPreview(false); ApplyOwnership(); Replay();
        }
        public void Replay()
        {
            _gate.texture = _closed; _gateStatus.SetSampleText(_ownerEnabled ? "Gate closed / waiting for the native action" : "Owner disabled / inspect native missing-registration diagnostics");
            _reward.ShowTextInstant("REWARD / " + _coins + " COINS");
            string id = _assetRoute ? _asset.Id : TextStudioOrbitEffectSampleFactory.EffectId;
            string parameters = _parameters == 0 ? "" : _parameters == 1 ? " radius=0.14 speed=1.1 tilt=7" : " r=0.14 s=1.1 t=7";
            string area = _area == 0 ? " area=glyph" : _area == 1 ? " area=word" : " area=word group=2";
            string text = "THE GATE REMEMBERS";
            if (_combined) text = "<wave>" + text + "</wave>";
            string action = _parameters == 2 ? "<openCourseGate coins=40>" : _parameters == 1 ? "<courseGate reward=25>" : "<courseGate>";
            string markup = "Speak the word. " + action + "<" + id + parameters + area + ">" + text + "</" + id + ">";
            _source.SetSampleText(markup); _subject.ShowText(markup); RefreshLabels();
        }
        void RefreshLabels()
        {
            _actionLabel.SetSampleText(_global ? "Action: Registered" : "Action: Scene-local");
            _effectLabel.SetSampleText(_assetRoute ? "Effect: Code asset" : "Effect: Code factory");
            _parametersLabel.SetSampleText(_parameters == 0 ? "Parameters: Defaults" : _parameters == 1 ? "Parameters: Named" : "Parameters: Aliases");
            _areaLabel.SetSampleText(_area == 0 ? "Area: Glyph" : _area == 1 ? "Area: Word" : "Area: Word group");
            _compositionLabel.SetSampleText(_combined ? "Built-in wave: On" : "Built-in wave: Off");
            _ownerLabel.SetSampleText(_ownerEnabled ? "Owner: Enabled" : "Owner: Disabled");
            _previewLabel.SetSampleText(_subject.RunActionsInPreview ? "Edit Mode actions: On" : "Edit Mode actions: Off");
        }
    }
}
