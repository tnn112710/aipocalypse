using UnityEngine;
using System;
using System.Collections;

namespace SunfishCreations.Utilities
{
    public static class UIUtility
    {
        /// <summary>
        /// Fades a CanvasGroup to the target alpha over a specified duration.
        /// </summary>
        /// <param name="canvasGroup">The CanvasGroup to fade.</param>
        /// <param name="targetAlpha">The alpha value to fade to (0 = invisible, 1 = visible).</param>
        /// <param name="duration">The time it takes to fade.</param>
        /// <param name="runner">A MonoBehaviour used to run the coroutine.</param>
        /// <param name="setInteractable">Automatically set interactable and raycasts based on alpha.</param>
        /// <param name="onComplete">Optional callback invoked when fade finishes.</param>
        public static void FadeCanvasGroup(
            CanvasGroup canvasGroup,
            float targetAlpha,
            float duration,
            MonoBehaviour runner,
            bool setInteractable = true,
            Action onComplete = null)
        {
            if (runner == null)
            {
                Debug.LogError("UIUtility: A MonoBehaviour runner is required to start the fade coroutine.");
                return;
            }

            runner.StartCoroutine(FadeRoutine(canvasGroup, targetAlpha, duration, setInteractable, onComplete));
        }

        private static IEnumerator FadeRoutine(CanvasGroup canvasGroup, float targetAlpha, float duration, bool setInteractable, Action onComplete)
        {
            float startAlpha = canvasGroup.alpha;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;

            if (setInteractable)
            {
                bool visible = targetAlpha > 0.99f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            onComplete?.Invoke();
        }
    }
}
