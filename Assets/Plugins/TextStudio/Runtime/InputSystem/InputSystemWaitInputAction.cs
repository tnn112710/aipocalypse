using System;
using System.Collections;
using System.Linq;
using TextStudio.BuiltIns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TextStudio.InputSystem
{
    /// <summary>
    /// Input System-backed reusable wait action with default and keyed Input Action modes.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InputSystemWaitInputAction",
        menuName = "Text Studio/Actions/Wait Input (Input System)",
        order = 106)]
#if UNITY_6000_7_OR_NEWER
    // The default provider is stateless and safe to share across play sessions.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed partial class InputSystemWaitInputAction : TextStudioActionAsset, ITextStudioEditModePreviewAction
    {
        static readonly InputSystemTextStudioInputProvider s_defaultInputProvider = new InputSystemTextStudioInputProvider();

        [SerializeField] InputSystemWaitInputMode _inputMode = InputSystemWaitInputMode.KeyedInputActions;
        [SerializeField] string _defaultInputKey = string.Empty;
        [SerializeField] InputActionReference _defaultInputAction;
        [SerializeField] InputSystemWaitInputMapping[] _inputActions = Array.Empty<InputSystemWaitInputMapping>();
        [SerializeField, Tooltip("Maximum wait in seconds. Zero or a negative value waits indefinitely.")]
        float _maxSecondsWaiting = -1f;
        [SerializeField] bool _fallbackToTextStudioInputProvider = true;
        [SerializeField] bool _autoEnableInputAction = true;
        [SerializeField] bool _restoreActionAfterFinish = true;
        [SerializeField] bool _skippable = true;

        public InputSystemWaitInputMode InputMode => _inputMode;
        public string DefaultInputKey => _defaultInputKey ?? string.Empty;
        public float MaxSecondsWaiting => _maxSecondsWaiting;

        protected override string DefaultId => BuiltInActions.WaitForInput.Id;

        public override TextStudioActionDefinition ActionDefinition => new TextStudioActionDefinition(
            Id,
            "Wait For Input (Input System)",
            "Pauses reveal until a configured Input Action, fallback input, timeout, or allowed skip.",
            examples: new[] { "<" + Id + ">", "<" + Id + " input=confirm timeout=5>" },
            parameters: BuiltInActions.WaitForInput.ActionDefinition.Parameters.ToArray());

        public override IEnumerator Execute(ActionContext context)
        {
            if (context.Controller == null)
                yield break;

            string inputKey = context.Marker.GetString("input", 0, DefaultInputKey);
            float timeout = context.Marker.GetFloat("timeout", 1, _maxSecondsWaiting);
            InputAction action = ResolveInputAction(inputKey);
            IDisposable lease = TextStudioInputActionLease.Acquire(action, _autoEnableInputAction, _restoreActionAfterFinish);
            string issue = GetInputDiagnostic(inputKey);
            if (!string.IsNullOrEmpty(issue)) Debug.LogWarning("[Text Studio] " + issue, this);

            context.Controller.PauseReveal();
            try
            {
                float elapsed = 0f;
                while ((!_skippable || !context.SkipRequested) &&
                       !HasAdvanceInput(action, inputKey, context.IsEditModePreview))
                {
                    if (timeout > 0f)
                    {
                        elapsed += context.DeltaTime;
                        if (elapsed >= timeout)
                            break;
                    }
                    yield return null;
                }
            }
            finally
            {
                lease?.Dispose();
                context.Controller.ResumeReveal();
            }
        }

        /// <summary>Reports a missing or disabled binding without changing wait, fallback, or timeout behavior.</summary>
        public string GetInputDiagnostic(string inputKey)
        {
            if (_inputMode == InputSystemWaitInputMode.TextStudioInputProvider)
            {
                ITextStudioInputProvider provider = TextStudioRuntimeServices.InputProvider ?? s_defaultInputProvider;
                return provider is InputSystemTextStudioInputProvider && !InputSystemTextStudioInputProvider.RecognizesKey(inputKey)
                    ? "Unknown input key '" + inputKey + "'. Use a keyboard key, mouse button, 'any', or a configured keyed Input Action."
                    : string.Empty;
            }
            InputAction action = ResolveInputAction(inputKey);
            if (action == null)
                return "No Input Action is assigned for '" + (string.IsNullOrEmpty(inputKey) ? "default" : inputKey) + "'. " +
                    (_fallbackToTextStudioInputProvider ? "The configured fallback input provider remains active." : "Assign an action, or continue through the configured timeout or skip.");
            if (!action.enabled && !_autoEnableInputAction)
                return "Input Action '" + action.name + "' is disabled and automatic enabling is off. Enable it through its owning input map.";
            return string.Empty;
        }

        InputAction ResolveInputAction(string inputKey)
        {
            switch (_inputMode)
            {
                case InputSystemWaitInputMode.DefaultInputAction:
                    return _defaultInputAction != null ? _defaultInputAction.action : null;
                case InputSystemWaitInputMode.KeyedInputActions:
                    return ResolveKeyedAction(inputKey);
                default:
                    return null;
            }
        }

        InputAction ResolveKeyedAction(string inputKey)
        {
            if (string.IsNullOrEmpty(inputKey))
                return _defaultInputAction != null ? _defaultInputAction.action : null;
            if (_inputActions == null)
                return null;

            for (int i = 0; i < _inputActions.Length; i++)
            {
                InputSystemWaitInputMapping mapping = _inputActions[i];
                if (mapping == null || mapping.Action == null)
                    continue;
                if (string.Equals(mapping.InputKey, inputKey, StringComparison.OrdinalIgnoreCase))
                    return mapping.Action.action;
            }
            return null;
        }

        bool HasAdvanceInput(InputAction action, string inputKey, bool isEditModePreview)
        {
            if (isEditModePreview)
                return false;
            if (action != null && (action.triggered || action.WasPressedThisFrame()))
                return true;
            if (!_fallbackToTextStudioInputProvider)
                return false;

            ITextStudioInputProvider provider = TextStudioRuntimeServices.InputProvider ?? s_defaultInputProvider;
            if (!string.IsNullOrEmpty(inputKey) &&
                !string.Equals(inputKey, "any", StringComparison.OrdinalIgnoreCase) &&
                provider is ITextStudioKeyedInputProvider keyedProvider)
            {
                return keyedProvider.WasAdvancePressed(inputKey);
            }
            return provider.WasAnyAdvancePressed();
        }
    }
}
