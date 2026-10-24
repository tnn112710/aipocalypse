using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using MalbersAnimations;

namespace SunfishCreations.UI
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/secondary-components/fishing-ui-controller")]
    public class FishingUIController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("CanvasGroup controlling the visibility and interaction of the fish caught panel")]
        [SerializeField] private CanvasGroup _fishCaughtGroup;

        [Tooltip("Text element to display the caught fish's name")]
        [SerializeField] private Text _fishNameText;

        [Tooltip("Text element to display the caught fish's weight")]
        [SerializeField] private Text _fishWeightText;

        [Tooltip("Image to display the fish's icon/sprite")]
        [SerializeField] private Image _fishIcon;

        [Tooltip("Text element to display the caught fish's rarity tier")]
        [SerializeField] private Text _fishRarityText;

        [Header("Rarity Colors")]
        [Tooltip("Text color for Common fish")]
        [SerializeField] private Color _commonColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [Tooltip("Text color for Uncommon fish")]
        [SerializeField] private Color _uncommonColor = new Color(0.3f, 0.8f, 0.3f, 1f);
        [Tooltip("Text color for Rare fish")]
        [SerializeField] private Color _rareColor = new Color(0.4f, 0.5f, 1f, 1f);

        [Header("Panel Settings")]
        [Tooltip("Delay before opening the panel after catching a fish")]
        [SerializeField] private float _openPanelDelay = 2f;

        [Tooltip("Duration of the panel open fade-in animation")]
        [SerializeField] private float _openPanelDuration = 1f;

        [Tooltip("Duration of the panel close fade-out animation")]
        [SerializeField] private float _closePanelDuration = 0.8f;

        [Tooltip("Automatically close the panel after a set time")]
        [SerializeField] private bool _autoClose;

        [Hide("_autoClose")]
        [Tooltip("Time in seconds before auto-closing the panel (only if Auto Close is enabled)")]
        [SerializeField] private float _autoCloseTime = 5f;

        public static event Action<FishData> OnFishDisplayed;
        public static event Action OnPanelOpened;
        public static event Action OnPanelClosed;

        protected bool _panelOpen = false;
        protected bool _isAnimating = false;
        protected Coroutine _autoCloseCoroutine;
        protected Coroutine _currentAnimationCoroutine;
        protected FishData _currentFishData;

        public virtual bool IsPanelOpen => _panelOpen;
        public virtual bool IsAnimating => _isAnimating;

        protected virtual void Awake()
        {
            InitializeComponents();
            ValidateReferences();
        }

        protected virtual void Start()
        {
            InitializeUI();
        }

        protected virtual void OnDestroy()
        {
            StopAllAnimations();
        }

        protected virtual void InitializeComponents()
        {
            if (!_fishCaughtGroup)
            {
                _fishCaughtGroup = GetComponentInChildren<CanvasGroup>();
            }
        }

        protected virtual void ValidateReferences()
        {
            if (!_fishCaughtGroup)
            {
                Debug.LogError($"[{name}] FishCaughtGroup is not assigned!", this);
            }

            if (!_fishNameText)
            {
                Debug.LogWarning($"[{name}] FishNameText is not assigned!", this);
            }

            if (!_fishWeightText)
            {
                Debug.LogWarning($"[{name}] FishWeightText is not assigned!", this);
            }

            if (!_fishRarityText)
            {
                Debug.LogWarning($"[{name}] FishRarityText is not assigned!", this);
            }
        }

        protected virtual void InitializeUI()
        {
            if (_fishCaughtGroup)
            {
                _fishCaughtGroup.alpha = 0f;
                _fishCaughtGroup.interactable = false;
                _fishCaughtGroup.blocksRaycasts = false;
            }
        }

        public virtual void HandleFishStateExit()
        {
            StopAllCoroutines();
            ForceClosePanel();
        }

        public virtual void HandleFishDataReceived(FishData fish)
        {
            if (_isAnimating)
            {
                Debug.LogWarning($"[{name}] Panel is already animating, ignoring fish data.");
                return;
            }

            if (fish == null)
            {
                Debug.LogError($"[{name}] Received null fish data!");
                return;
            }

            _currentFishData = fish;
            UpdateFishDisplay(fish);
            StartCoroutine(OpenPanelRoutine());

            OnFishDisplayed?.Invoke(fish);
        }

        public virtual void OpenFishingCaughtPanel()
        {
            if (_panelOpen || _isAnimating) return;

            StopAllAnimations();
            _currentAnimationCoroutine = StartCoroutine(FadePanel(true));
        }

        public virtual void CloseFishingCaughtPanel()
        {
            if (!_panelOpen || _isAnimating) return;

            StopAllAnimations();
            _currentAnimationCoroutine = StartCoroutine(FadePanel(false));
        }

        public virtual void ForceClosePanel()
        {
            StopAllAnimations();

            if (_fishCaughtGroup)
            {
                _fishCaughtGroup.alpha = 0f;
                _fishCaughtGroup.interactable = false;
                _fishCaughtGroup.blocksRaycasts = false;
            }

            _panelOpen = false;
            _isAnimating = false;
        }

        protected virtual void UpdateFishDisplay(FishData fish)
        {
            if (_fishNameText)
            {
                _fishNameText.text = fish.fishName;
            }

            if (_fishWeightText)
            {
                _fishWeightText.text = $"{fish.Weight:F1}kg";
            }

            if (_fishIcon && fish.fishIcon)
            {
                _fishIcon.sprite = fish.fishIcon;
                _fishIcon.gameObject.SetActive(true);
            }
            else if (_fishIcon)
            {
                _fishIcon.gameObject.SetActive(false);
            }

            if (_fishRarityText)
            {
                _fishRarityText.text = GetRarityDisplayName(fish.rarity);
                _fishRarityText.color = GetRarityColor(fish.rarity);
                _fishRarityText.gameObject.SetActive(true);
            }
        }

        protected virtual string GetRarityDisplayName(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return "Common";
                case Rarity.Uncommon: return "Uncommon";
                case Rarity.Rare: return "Rare";
                default: return "Common";
            }
        }

        protected virtual Color GetRarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return _commonColor;
                case Rarity.Uncommon: return _uncommonColor;
                case Rarity.Rare: return _rareColor;
                default: return _commonColor;
            }
        }

        protected virtual IEnumerator OpenPanelRoutine()
        {
            yield return new WaitForSeconds(_openPanelDelay);
            OpenFishingCaughtPanel();
        }

        protected virtual IEnumerator FadePanel(bool fadeIn)
        {
            if (!_fishCaughtGroup) yield break;

            _isAnimating = true;

            float duration = fadeIn ? _openPanelDuration : _closePanelDuration;
            float startAlpha = _fishCaughtGroup.alpha;
            float targetAlpha = fadeIn ? 1f : 0f;

            if (fadeIn)
            {
                _fishCaughtGroup.blocksRaycasts = true;
            }

            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / duration;

                _fishCaughtGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, normalizedTime);

                yield return null;
            }

            _fishCaughtGroup.alpha = targetAlpha;

            if (fadeIn)
            {
                _fishCaughtGroup.interactable = true;
                _panelOpen = true;
                OnPanelOpened?.Invoke();

                if (_autoClose && _autoCloseTime > 0)
                {
                    _autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
                }
            }
            else
            {
                _fishCaughtGroup.interactable = false;
                _fishCaughtGroup.blocksRaycasts = false;
                _panelOpen = false;
                OnPanelClosed?.Invoke();
            }

            _isAnimating = false;
        }

        protected virtual IEnumerator AutoCloseRoutine()
        {
            yield return new WaitForSeconds(_autoCloseTime);

            if (_panelOpen && !_isAnimating)
            {
                CloseFishingCaughtPanel();
            }
        }

        protected virtual void StopAllAnimations()
        {
            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = null;
            }

            if (_currentAnimationCoroutine != null)
            {
                StopCoroutine(_currentAnimationCoroutine);
                _currentAnimationCoroutine = null;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test Open Panel")]
        protected virtual void TestOpenPanel()
        {
            if (Application.isPlaying)
            {
                var testFish = ScriptableObject.CreateInstance<FishData>();
                testFish.fishName = "Test Fish";
                testFish.rarity = Rarity.Uncommon;
                testFish.minWeight = 2.5f;
                testFish.maxWeight = 10.5f;
                HandleFishDataReceived(testFish);
            }
        }

        [ContextMenu("Test Close Panel")]
        protected virtual void TestClosePanel()
        {
            if (Application.isPlaying)
            {
                CloseFishingCaughtPanel();
            }
        }
#endif
    }
}