using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Common presentation actions for saved examples; product components retain behavior ownership.</summary>
    [UnityEngine.AddComponentMenu("")]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    public sealed partial class TextStudioLessonActions : MonoBehaviour, ICancelHandler
    {
        public static event Action<Object> InspectionRequested = null;
        [SerializeField] Object _inspectTarget;
        [SerializeField] GameObject _details;
        [SerializeField] TMP_Text _feedback;
        [SerializeField] GameObject[] _primaryView;
        [SerializeField] GameObject[] _advancedView;
        [SerializeField] Selectable _primaryFocus;
        [SerializeField] Selectable _advancedFocus;
        [SerializeField] Selectable _detailsFocus;
        [SerializeField] Selectable _detailsReturnFocus;

#if !UNITY_6000_7_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEditorBridge() => InspectionRequested = null;
#endif

        public void Configure(Object target, GameObject details, TMP_Text feedback)
        {
            _inspectTarget = target; _details = details; _feedback = feedback;
        }

        public static bool Inspect(Object target)
        {
            if (target == null || InspectionRequested == null) return false;
            InspectionRequested(target);
            return true;
        }

        public void InspectSetup()
        {
            if (Inspect(_inspectTarget)) return;
            if (_feedback != null && _inspectTarget != null) _feedback.text = "Setup: " + _inspectTarget.name + ". Open this sample in Unity to inspect its saved assets.";
            if (_details != null) _details.SetActive(true);
        }

        public void ToggleDetails()
        {
            if (_details == null) return;
            _details.SetActive(!_details.activeSelf);
            Selectable focus = _details.activeSelf ? _detailsFocus : _detailsReturnFocus;
            if (Application.isPlaying && EventSystem.current != null && focus != null) focus.Select();
        }

        public void ConfigureDetailsFocus(Selectable inside, Selectable outside) { _detailsFocus = inside; _detailsReturnFocus = outside; }

        public void OnCancel(BaseEventData eventData)
        {
            if (_details == null || !_details.activeSelf)
            {
                if (transform.parent != null)
                    ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.cancelHandler);
                return;
            }
            ToggleDetails(); eventData.Use();
        }

        public void ConfigureViews(GameObject[] primary, GameObject[] advanced)
        {
            _primaryView = primary; _advancedView = advanced; ShowPrimaryView();
        }

        public void ShowPrimaryView() => SetAdvancedView(false);
        public void ShowAdvancedView() => SetAdvancedView(true);

        public void ConfigureViewFocus(Selectable primary, Selectable advanced)
        {
            _primaryFocus = primary; _advancedFocus = advanced;
        }

        void SetAdvancedView(bool advanced)
        {
            if (_primaryView != null) foreach (GameObject item in _primaryView) if (item != null) item.SetActive(!advanced);
            if (_advancedView != null) foreach (GameObject item in _advancedView) if (item != null) item.SetActive(advanced);
            Selectable focus = advanced ? _advancedFocus : _primaryFocus;
            if (Application.isPlaying && EventSystem.current != null && focus != null) focus.Select();
        }
    }
}
