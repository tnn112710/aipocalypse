using MalbersAnimations.Scriptables;
using SunfishCreations.UI;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SunfishCreations
{
    /// <summary>
    /// Multi-panel hint system for fishing. Uses a parent CanvasGroup for global
    /// visibility and per-panel CanvasGroups for step transitions.
    /// </summary>
    public class HintSystem : MonoBehaviour
    {
        private const string _fishingHintPrefsName = "HasSeenFishingHint";

        [Header("Timing")]
        public float fadeInDuration = 0.5f;
        public float fadeOutDuration = 0.5f;

        [Header("UI References")]
        public CanvasGroup[] panels;             // Each step panel must have its own CanvasGroup
        public Button finishButton;
        public BoolReference togglePaused;

        [Header("Debug / Testing")]
        public bool alwaysShow = false;

        private CanvasGroup _canvasGroup;       // Parent CanvasGroup (entire hint UI container)
        private int _currentPanelIndex = 0;

        private void Awake()
        {
            Initialization();
        }

        private void Initialization()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup)
            {
                Debug.LogError("HintSystem: Missing CanvasGroup on parent GameObject!", this);
                enabled = false;
                return;
            }

            if (panels == null || panels.Length == 0)
            {
                Debug.LogError("HintSystem: No panels assigned!", this);
                enabled = false;
                return;
            }

            // Hide entire system
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            for (int i = 1; i < panels.Length; i++)
            {
                panels[i].alpha = 0;
                panels[i].interactable = false;
                panels[i].blocksRaycasts = false;
            }

            finishButton?.onClick.AddListener(HandleFinishButton);
            _currentPanelIndex = 0;
        }

        private void OnDestroy()
        {
            finishButton?.onClick.RemoveListener(HandleFinishButton);
        }

        public void NextButton()
        {
            if (_currentPanelIndex < panels.Length - 1)
            {
                StartCoroutine(CycleToPanel(_currentPanelIndex + 1));
            }
        }

        public void PreviousButton()
        {
            if (_currentPanelIndex > 0)
            {
                StartCoroutine(CycleToPanel(_currentPanelIndex - 1));
            }
        }

        private IEnumerator CycleToPanel(int targetIndex)
        {
            CanvasGroup currentPanel = panels[_currentPanelIndex];
            CanvasGroup targetPanel = panels[targetIndex];

            // Ensure target panel is ready but invisible (in case it was hidden before)
            if (targetPanel != null)
            {
                targetPanel.alpha = 0f;
                targetPanel.interactable = true;   // enable interaction early (but blocked by alpha=0)
                targetPanel.blocksRaycasts = true;
            }

            // Crossfade: fade out current AND fade in next *at the same time*
            float duration = Mathf.Max(fadeOutDuration, fadeInDuration);

            float elapsed = 0f;
            float startAlphaCurrent = currentPanel ? currentPanel.alpha : 0f;
            float startAlphaTarget = targetPanel ? targetPanel.alpha : 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                if (currentPanel != null)
                {
                    currentPanel.alpha = Mathf.Lerp(startAlphaCurrent, 0f, t);
                }
                if (targetPanel != null)
                {
                    targetPanel.alpha = Mathf.Lerp(startAlphaTarget, 1f, t);
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Finalize states
            _currentPanelIndex = targetIndex;

            if (currentPanel != null)
            {
                currentPanel.alpha = 0f;
                currentPanel.interactable = false;
                currentPanel.blocksRaycasts = false;
            }

            if (targetPanel != null)
            {
                targetPanel.alpha = 1f;

                AutoFocusButton panelButton = targetPanel.GetComponentInChildren<AutoFocusButton>();
                if(panelButton != null)
                {
                    panelButton.GiveFocus();
                }
            }
        }


        // Entry / Exit

        public virtual void FishingInteraction()
        {
            if (alwaysShow || !HasSeenHint())
            {
                FadePanelIn();
            }
        }

        private bool HasSeenHint()
        {
            return PlayerPrefs.GetInt(_fishingHintPrefsName, 0) == 1;
        }

        public virtual void FadePanelIn()
        {
            StartCoroutine(FadeInRoutine());
        }

        public virtual void FadePanelOut()
        {
            StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            togglePaused.Value = true;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            // Show first panel
            _currentPanelIndex = 0;
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;

                if (i == 0)
                {
                    panels[i].alpha = 1f;
                    panels[i].interactable = true;
                    panels[i].blocksRaycasts = true;


                    AutoFocusButton panelButton = panels[i].GetComponentInChildren<AutoFocusButton>();
                    if (panelButton != null)
                    {
                        panelButton.GiveFocus();
                    }
                }
                else
                {
                    panels[i].alpha = 0f;
                    panels[i].interactable = false;
                    panels[i].blocksRaycasts = false;
                }
            }
        }

        private IEnumerator FadeOutRoutine()
        {
            togglePaused.Value = false;

            // Fade out entire system
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // Also hide all panels
            foreach (var panel in panels)
            {
                if (panel != null)
                {
                    panel.alpha = 0f;
                    panel.interactable = false;
                    panel.blocksRaycasts = false;
                }
            }
        }

        // Finish & Persistence

        private void HandleFinishButton()
        {
            StartCoroutine(HideAndMarkAsSeen());
        }

        private IEnumerator HideAndMarkAsSeen()
        {
            // Fade out current panel first
            if (panels[_currentPanelIndex] != null)            {
                yield return FadePanelAlpha(panels[_currentPanelIndex], 1f, 0f, fadeOutDuration);
            }

            // Then fade out entire UI
            yield return FadeOutRoutine();

            // Save
            PlayerPrefs.SetInt(_fishingHintPrefsName, 1);
            PlayerPrefs.Save();
        }

        // Utility

        private IEnumerator FadePanelAlpha(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null || duration <= 0f) { cg.alpha = to; yield break; }

            cg.alpha = from;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            cg.alpha = to;
        }
    }
}