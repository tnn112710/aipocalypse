using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/secondary-components/ui-logger")]
    public class UILogger : MonoBehaviour
    {
        public static UILogger Instance { get; private set; }

        [Header("References")]
        [Tooltip("Parent container for log entries (Vertical Layout Group recommended)")]
        [SerializeField] private Transform logContainer;

        [Tooltip("Prefab used to create each log line")]
        [SerializeField] private Text logPrefab;

        [Header("Fade Settings")]
        [Tooltip("Duration in seconds for log entries to fade in")]
        [SerializeField] private float fadeInDuration = 0.5f;

        [Tooltip("Time in seconds each log entry remains fully visible")]
        [SerializeField] private float activeTime = 2f;

        [Tooltip("Duration in seconds for log entries to fade out")]
        [SerializeField] private float fadeOutDuration = 0.5f;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static void Log(string message, string colorName = null)
        {
            if (Instance == null)
            {
                Debug.LogWarning("UILogger not present in scene.");
                return;
            }
            Instance.SpawnLog(message, colorName);
        }

        protected virtual void SpawnLog(string message, string colorName)
        {
            if (logPrefab == null || logContainer == null)
            {
                Debug.LogError("UILogger: Prefab or container not assigned!");
                return;
            }

            Text newLog = Instantiate(logPrefab, logContainer);
            newLog.gameObject.SetActive(true);
            newLog.text = message;

            Color baseColor = GetColorFromString(colorName, newLog.color);
            baseColor.a = 0f;
            newLog.color = baseColor;

            StartCoroutine(FadeSequence(newLog, colorName));
        }

        protected virtual IEnumerator FadeSequence(Text log, string colorName)
        {
            if (log == null) yield break;

            yield return Fade(log, 0f, 1f, fadeInDuration);

            if (log == null) yield break;

            yield return new WaitForSeconds(activeTime);

            if (log == null) yield break;

            yield return Fade(log, 1f, 0f, fadeOutDuration);

            if (log != null)
                Destroy(log.gameObject);
        }

        protected virtual IEnumerator Fade(Text log, float startAlpha, float endAlpha, float duration)
        {
            if (log == null) yield break;

            float elapsed = 0f;
            Color c = log.color;

            while (elapsed < duration)
            {
                if (log == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(startAlpha, endAlpha, t);
                log.color = c;
                yield return null;
            }

            if (log != null)
            {
                c.a = endAlpha;
                log.color = c;
            }
        }

        protected static Color GetColorFromString(string colorName, Color fallback, float alpha = 1f)
        {
            if (!string.IsNullOrEmpty(colorName))
            {
                if (ColorUtility.TryParseHtmlString(colorName, out Color parsed))
                {
                    parsed.a = alpha;
                    return parsed;
                }

                switch (colorName.ToLower())
                {
                    case "red": return new Color(1f, 0f, 0f, alpha);
                    case "green": return new Color(0f, 1f, 0f, alpha);
                    case "blue": return new Color(0f, 0f, 1f, alpha);
                    case "yellow": return new Color(1f, 1f, 0f, alpha);
                    case "cyan": return new Color(0f, 1f, 1f, alpha);
                    case "magenta": return new Color(1f, 0f, 1f, alpha);
                    case "white": return new Color(1f, 1f, 1f, alpha);
                    case "black": return new Color(0f, 0f, 0f, alpha);
                }
            }
            return new Color(fallback.r, fallback.g, fallback.b, alpha);
        }
    }
}