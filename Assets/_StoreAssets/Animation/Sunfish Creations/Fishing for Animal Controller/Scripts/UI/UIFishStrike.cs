using MalbersAnimations.Scriptables;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace SunfishCreations.UI
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/secondary-components/fishing-ui-controller")]
    public class UIFishStrike : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Image component used as the time fill bar")]
        public Image timeFillBar;

        [Header("Time Settings")]
        [Tooltip("Reference to the remaining time value (e.g., from fishing state)")]
        public FloatReference remainingTime;

        [Header("Fade Settings")]
        [Tooltip("Duration in seconds for fade-in and fade-out animations")]
        public float fadeInOutDuration = 0.3f;

        [Header("Color Settings")]
        [Tooltip("Gradient used to change the fill bar color based on remaining time")]
        public Gradient timeGradient;

        protected CanvasGroup _canvasGroup;
        protected Coroutine _fadeCoroutine;
        protected Coroutine _monitorCoroutine;
        protected bool _isMonitoring;
        protected float _maxTime;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        protected virtual void Update()
        {
            if (_isMonitoring && timeFillBar != null && remainingTime != null)
            {
                UpdateTimeBar();
            }
        }

        public virtual void StartRemainingTimeMonitoring()
        {
            _isMonitoring = true;

            _maxTime = remainingTime.Value;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeCanvasGroup(1f, fadeInOutDuration));
        }

        public virtual void StopRemainingTimeMonitoring()
        {
            _isMonitoring = false;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, fadeInOutDuration));
        }

        protected virtual void UpdateTimeBar()
        {
            float fillAmount = Mathf.Clamp01(remainingTime.Value / _maxTime);
            timeFillBar.fillAmount = fillAmount;

            timeFillBar.color = timeGradient.Evaluate(1f - fillAmount);
        }

        protected virtual IEnumerator FadeCanvasGroup(float targetAlpha, float duration)
        {
            if (_canvasGroup == null) yield break;

            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
        }
    }
}