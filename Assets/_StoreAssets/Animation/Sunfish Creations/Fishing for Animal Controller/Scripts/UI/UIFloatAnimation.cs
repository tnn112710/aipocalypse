using UnityEngine;

namespace SunfishCreations.UI
{
    public class UIFloatAnimation : MonoBehaviour
    {
        [Header("Float Settings")]
        [Tooltip("How far up and down the UI element moves (in pixels)")]
        public float amplitude = 20f;

        [Tooltip("How fast the UI element oscillates")]
        public float speed = 2f;

        [Tooltip("Start at a random point in the animation cycle to avoid synchronization")]
        public bool startRandomPhase = true;

        protected RectTransform rectTransform;
        protected Vector3 startPosition;
        protected float timeOffset;

        protected virtual void Start()
        {
            rectTransform = GetComponent<RectTransform>();

            startPosition = rectTransform.anchoredPosition;

            if (startRandomPhase)
            {
                timeOffset = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        protected virtual void Update()
        {
            float newY = startPosition.y + Mathf.Sin((Time.time * speed) + timeOffset) * amplitude;

            rectTransform.anchoredPosition = new Vector3(startPosition.x, newY, startPosition.z);
        }
    }
}