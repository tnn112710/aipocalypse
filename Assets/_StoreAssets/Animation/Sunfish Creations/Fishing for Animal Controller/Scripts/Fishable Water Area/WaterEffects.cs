using MalbersAnimations.Scriptables;
using UnityEngine;
using System.Collections;

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/secondary-components/water-effects")]
    public class WaterEffects : MonoBehaviour
    {
        //Shader properties
        private const string SP_FalloffRange = "_FalloffRange";
        private const string SP_BobberPosition = "_BobberPosition";
        private const string SP_InnerHole = "_InnerHole";

        [Header("Bobber Reference")]
        public TransformReference bobber;
        private FishingBobber _fishingBobber;

        [Header("Shader Properties Animation Settings")]
        [Tooltip("Use this for water ripple effect enter animation")]
        public FloatReference enterWaterDuration = 8f;
        [Tooltip("Use this for water ripple effect exit animation")]
        public FloatReference exitWaterDuration = 6f;

        // Private fields
        private Material _materialInstance;
        private MeshRenderer _meshRenderer;
        private float _originalFalloffRange;
        private float _originalInnerHole;

        private bool _bobberInWater = false;
        private Coroutine _currentRippleAnimation;

        private void Start()
        {
            Initialization();
        }

        protected virtual void Initialization()
        {
            // Get the mesh renderer component
            _meshRenderer = GetComponent<MeshRenderer>();

            if (_meshRenderer == null)
            {
                Debug.LogError("WaterEffects: No MeshRenderer found on this GameObject!");
                return;
            }

            // Create a material instance to avoid modifying the original asset
            _materialInstance = new Material(_meshRenderer.material);
            _meshRenderer.material = _materialInstance;

            // Store original values from the material
            _originalFalloffRange = _materialInstance.GetFloat(SP_FalloffRange);
            _originalInnerHole = _materialInstance.GetFloat(SP_InnerHole);

            // Initialize with no ripple effect
            _materialInstance.SetFloat(SP_FalloffRange, 0f);
        }

        //Called by external event
        public virtual void BobberTouchedWater()
        {
            if (_materialInstance == null || _bobberInWater) return;

            _bobberInWater = true;

            // Update bobber position immediately
            if (bobber?.Value != null)
            {
                _fishingBobber = bobber.Value.GetComponent<FishingBobber>();

                _fishingBobber.OnFishStartFleeing += HandleFishStartFleeing;
                _fishingBobber.OnFishFleeingUpdate += HandleFishFleeingUpdate;
                _fishingBobber.OnFishStopFleeing += HandleFishStopFleeing;

                _materialInstance.SetVector(SP_BobberPosition, bobber.Value.position);
            }

            _materialInstance.SetFloat(SP_InnerHole, _originalInnerHole);

            // Stop any current animation and start enter animation
            if (_currentRippleAnimation != null)
                StopCoroutine(_currentRippleAnimation);

            _currentRippleAnimation = StartCoroutine(AnimateWaterEntry());
        }
        private void HandleFishFleeingUpdate(float angle)
        {
            _materialInstance.SetVector(SP_BobberPosition, bobber.Value.position);
        }

        private void HandleFishStopFleeing(float angle) { }

        private void HandleFishStartFleeing(float angle)
        {
            if (_materialInstance == null || bobber?.Value == null) return;

            _materialInstance.SetVector(SP_BobberPosition, bobber.Value.position);
        }


        //Called by external event
        public virtual void BobberExitWater()
        {
            if (_materialInstance == null || !_bobberInWater) return;

            _bobberInWater = false;

            if (_fishingBobber != null)
            {
                _fishingBobber.OnFishFleeingUpdate -= HandleFishStartFleeing;
                _fishingBobber.OnFishStopFleeing -= HandleFishStopFleeing;
            }

            if (_currentRippleAnimation != null)
                StopCoroutine(_currentRippleAnimation);

            _currentRippleAnimation = StartCoroutine(AnimateWaterExit());
        }

        // Called by external (fish is biting) event
        public virtual void BobberSubmerged()
        {
            if (_materialInstance == null || !_bobberInWater) return;

            // Stop any ongoing entry animation
            if (_currentRippleAnimation != null)
            {
                StopCoroutine(_currentRippleAnimation);
                _currentRippleAnimation = null;
            }

            // Fade out FalloffRange (ripple) to 0 over, say, 0.3s — quick & smooth
            _currentRippleAnimation = StartCoroutine(FadeOutRipple());
        }
        protected virtual IEnumerator FadeOutRipple()
        {
            float startFalloff = _materialInstance.GetFloat(SP_FalloffRange);
            float fadeDuration = 0.3f;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                float currentFalloff = Mathf.Lerp(startFalloff, 0f, t);
                _materialInstance.SetFloat(SP_FalloffRange, currentFalloff);
                yield return null;
            }

            _materialInstance.SetFloat(SP_FalloffRange, 0f);
            _currentRippleAnimation = null;
        }
        protected virtual IEnumerator AnimateWaterEntry()
        {
            float elapsedTime = 0f;
            while (elapsedTime < enterWaterDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / enterWaterDuration;

                // Apply ease-out cubic easing for smooth deceleration
                float easedProgress = EaseOutExpo(progress);

                // ONLY animate FalloffRange: from 0 to original material value
                float currentFalloff = Mathf.Lerp(0f, _originalFalloffRange, easedProgress);
                _materialInstance.SetFloat(SP_FalloffRange, currentFalloff);
                yield return null;
            }
            // Ensure final value is set
            _materialInstance.SetFloat(SP_FalloffRange, _originalFalloffRange);
            _currentRippleAnimation = null;
        }

        // Ease-out exponential (very smooth, almost stops at the end)
        protected virtual float EaseOutExpo(float t)
        {
            return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        }
        protected virtual IEnumerator AnimateWaterExit()
        {
            //Shrink inner hole
            float startHole = _materialInstance.GetFloat(SP_InnerHole);
            float holeElapsed = 0f;
            while (holeElapsed < exitWaterDuration)
            {
                holeElapsed += Time.deltaTime;
                float t = holeElapsed / exitWaterDuration;
                float currentHole = Mathf.Lerp(startHole, 1f, t);
                _materialInstance.SetFloat(SP_InnerHole, currentHole);
                yield return null;
            }
            _materialInstance.SetFloat(SP_InnerHole, 1f);

            _currentRippleAnimation = null;
        }

        protected virtual void OnDisable()
        {
            if (_currentRippleAnimation != null) 
                StopCoroutine(_currentRippleAnimation);
            _currentRippleAnimation = null;
        }

        protected virtual void OnDestroy()
        {
            if (_materialInstance != null)
            {
                DestroyImmediate(_materialInstance);
            }
        }
    }
}