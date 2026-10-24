using System.Collections;
using UnityEngine;
using MalbersAnimations.Scriptables;
using MalbersAnimations;
using SunfishCreations.Effects;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/bobber-controller")]
    public class FishingBobber : MonoBehaviour
    {
        [Header("Bobber References")]
        public Transform bobberModel;

        [Header("Global References")]
        public TransformReference playerReference; //Nibble animations will be in relation to player position
        public IntReference fishingMode;

        public delegate void FishFleeingEvent(float angle);
        public FishFleeingEvent OnFishStartFleeing;
        public FishFleeingEvent OnFishFleeingUpdate;
        public FishFleeingEvent OnFishStopFleeing;

        public delegate void InternalFinalBite();
        public InternalFinalBite OnFinalBite;

        [Header("Water Entry Animation")]
        public float nibbleAnimationDuration = 0.4f;
        [Tooltip("How deep the bobber goes underwater")]
        public float submersionDepth = 0.2f;
        [Tooltip("Time to go down")]
        public float submersionDuration = 0.3f;
        [Tooltip("Time to come back up")]
        public float surfaceDuration = 0.4f;
        public AnimationCurve submersionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Idle Bobbing")]
        public float gentleBobIntensity = 10f;
        public float idleBobSpeed = 2f;

        [Header("Fish Tapping")]

        [Tooltip("Number of taps before final bite")]
        public int tapCount = 3;
        [Tooltip("Time between taps")]
        public float tapInterval = 1.5f;
        [Tooltip("Side rotation angle for taps")]
        public float tapRotationAngle = 15f;
        [Tooltip("Small downward movement on tap")]
        public float tapDownDistance = 0.05f;

        public AnimationCurve tapCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Final Bite")]
        public float bitePullDistance = 0.3f;
        public float biteRotationAngle = 25f; // Stronger rotation for final bite
        public AnimationCurve biteCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("Is the player reeling when the fish is trying to flee?")]
        public BoolReference reelingDuringStruggle;

        private BobberEffects _effects;
        private FishTugOfWar _tugOfWar;

        private bool fightThePlayer;

        private Vector3 originalPosition;
        private Vector3 originalRotation;
        private bool isFishActive = false;
        private bool isBiting = false;
        private bool isSubmersing = false;
        private bool struggleAllowed = true;

        private bool _isReeling;
        public Vector3 _retrievalPosition;

        // Track the last rotation direction for alternating taps
        private float lastTapDirection = 1f;

        private float _resistanceOverride = 0f;  // 0 = no resistance, 1 = full stop

        private FishJoint fishJoint;


        public Vector3 _fleeDirection = Vector3.forward;
        public Vector3 FleeDirection
        {
            get => _fleeDirection;
            set
            {
                if (Vector3.Angle(_fleeDirection, value) > 5f) //: ignore tiny changes
                {
                    _fleeDirection = value;
                    _fleeDirection.y = 0f;
                    _fleeDirection.Normalize();

                    startPos = transform.position;
                    elapsed = 0f;
                }
            }
        }

        public virtual void Awake()
        {
            _effects = GetComponent<BobberEffects>();
            fishJoint = GetComponentInChildren<FishJoint>();
        }

        public virtual void BobberTouchedWater()
        {
            transform.LookAt(playerReference.Value);

            if (bobberModel == null)
                bobberModel = transform.GetChild(0);

            originalPosition = bobberModel.localPosition;
            originalRotation = bobberModel.localEulerAngles;

            StartCoroutine(WaterEntrySequence());
        }

        protected virtual IEnumerator WaterEntrySequence()
        {
            isSubmersing = true;

            // Phase 1: Submersion - bobber goes down into the water
            yield return StartCoroutine(SubmerseBobber());

            // Phase 2: Surface - bobber comes back up to the surface
            yield return StartCoroutine(SurfaceBobber());

            isSubmersing = false;

            // Phase 3: Start bobbing
            StartCoroutine(GentleBobbing());
        }

        protected virtual IEnumerator SubmerseBobber()
        {
            Vector3 startPos = bobberModel.localPosition;
            Vector3 targetPos = originalPosition + Vector3.down * submersionDepth;
            float elapsed = 0f;

            while (elapsed < submersionDuration)
            {
                elapsed += Time.deltaTime;
                float t = submersionCurve.Evaluate(elapsed / submersionDuration);

                bobberModel.localPosition = Vector3.Lerp(startPos, targetPos, t);

                yield return null;
            }

            bobberModel.localPosition = targetPos;
        }

        public virtual void SpawnFish(FishData fishData)
        {
            fishJoint.SpawnFish(fishData);
        }

        public virtual void SetPosition(Vector3 pos)
        {
            transform.position = pos;
        }

        public virtual void FishIsTired()
        {
            fightThePlayer = false;
            reelingDuringStruggle.Value = _isReeling && fightThePlayer;
            OnFishStopFleeing?.Invoke(0f);
            _effects.StopVEffect();
        }

        public virtual void FishIsFleeing()
        {
            if (!struggleAllowed)
            {
                return;
            }

            StartCoroutine(FightThePlayer());
        }

        protected virtual IEnumerator SurfaceBobber()
        {
            Vector3 startPos = bobberModel.localPosition;
            Vector3 targetPos = originalPosition;
            float elapsed = 0f;

            while (elapsed < surfaceDuration)
            {
                elapsed += Time.deltaTime;
                float t = submersionCurve.Evaluate(elapsed / surfaceDuration);

                bobberModel.localPosition = Vector3.Lerp(startPos, targetPos, t);

                yield return null;
            }

            bobberModel.localPosition = targetPos;
        }

        public virtual void SetTugOfWarRef(FishTugOfWar tugOfWar)
        {
            _tugOfWar = tugOfWar;
        }

        protected virtual IEnumerator GentleBobbing()
        {
            while (!isFishActive && !isBiting && !isSubmersing)
            {
                // Gentle idle bobbing - only vertical movement
                float time = Time.time;
                float yOffset = Mathf.Sin(time * idleBobSpeed) * (gentleBobIntensity / 100f); //divide by 100 to have readble values

                Vector3 newPos = originalPosition + Vector3.up * yOffset;
                bobberModel.localPosition = newPos;

                yield return null;
            }
        }

        public virtual void ApplyReelingMovement(Vector3 targetPosition, Vector3 _retrievalPoint, float speed)
        {
            _retrievalPosition = _retrievalPoint;
            if (!fightThePlayer)
            {
                transform.position = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
            }
        }

        public virtual void SetReeling(bool isReeling)
        {
            _isReeling = isReeling;
            reelingDuringStruggle.Value = _isReeling && fightThePlayer;
        }

        public virtual void StartFishSequence()
        {
            if (isFishActive || isSubmersing) return;

            isFishActive = true;
            StartCoroutine(FishTappingSequence());
        }

        protected virtual IEnumerator FishTappingSequence()
        {
            // Perform the tapping sequence
            for (int i = 0; i < tapCount; i++)
            {
                yield return StartCoroutine(PerformTap(i));
                yield return new WaitForSeconds(tapInterval);
            }

            // After all taps, do the final bite
            yield return StartCoroutine(PerformFinalBite());
        }

        protected virtual IEnumerator PerformTap(int tapIndex)
        {
            // Alternate tap direction
            lastTapDirection *= -1f;
            Vector3 targetRotation = originalRotation + Vector3.forward * (tapRotationAngle * lastTapDirection);
            Vector3 targetPosition = originalPosition + Vector3.down * tapDownDistance;

            yield return StartCoroutine(AnimateTap(targetPosition, targetRotation, nibbleAnimationDuration * 0.3f));

            yield return StartCoroutine(AnimateTap(originalPosition, originalRotation, nibbleAnimationDuration * 0.7f));
        }

        protected virtual IEnumerator AnimateTap(Vector3 targetPos, Vector3 targetRot, float duration)
        {
            Vector3 startPos = bobberModel.localPosition;
            Quaternion startRot = bobberModel.localRotation;
            Quaternion targetRotation = Quaternion.Euler(targetRot);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = tapCurve.Evaluate(elapsed / duration);

                bobberModel.localPosition = Vector3.Lerp(startPos, targetPos, t);
                bobberModel.localRotation = Quaternion.Lerp(startRot, targetRotation, t);

                yield return null;
            }

            bobberModel.localPosition = targetPos;
            bobberModel.localRotation = targetRotation;
        }

        protected virtual IEnumerator PerformFinalBite()
        {
            isBiting = true;
            OnFinalBite?.Invoke();

            _effects.StopVEffect(true);

            // Strong pull down with rotation
            float rotationDirection = (Random.value > 0.5f) ? 1f : -1f;
            Vector3 pullPosition = originalPosition + Vector3.down * bitePullDistance;
            Vector3 biteRotation = originalRotation + Vector3.forward * (biteRotationAngle * rotationDirection);

            float biteDuration = 0.3f;
            Vector3 startPos = bobberModel.localPosition;
            Vector3 startRot = bobberModel.localEulerAngles;
            float elapsed = 0f;
            struggleAllowed = true;

            // Pull down animation
            while (elapsed < biteDuration)
            {
                elapsed += Time.deltaTime;
                float t = biteCurve.Evaluate(elapsed / biteDuration);

                bobberModel.localPosition = Vector3.Lerp(startPos, pullPosition, t);
                bobberModel.localEulerAngles = Vector3.Lerp(startRot, biteRotation, t);

                yield return null;
            }

            if (fishingMode.Value == 2) //Tug of War mode
            {
                // StartCoroutine(FightThePlayer());
            }
            else
            {
                // Stay in bite position until player reacts or fish escapes
                StartCoroutine(BiteHoldingPattern(pullPosition));
            }
        }

        /// <summary>
        /// Called by FishTugOfWar to prevent bobber movement.
        /// 0f = no resistance (full pull), 1f = full resistance (no movement).
        /// </summary>
        public void SetResistance(float resistance)
        {
            if (resistance < 0.4f)
            {
                resistance = 0;
            }

            //Debug.Log("Setting Resistence to: " + resistance);
            _resistanceOverride = Mathf.Clamp(resistance, 0, 0.8f);
        }

        public virtual void PreventStruggle()
        {
            _effects.StopVEffect(true);
            struggleAllowed = false;
        }

        public Vector3 GetDirectionToPlayer()
        {
            if (playerReference?.Value == null)
                return Vector3.zero;

            Vector3 toPlayer = playerReference.Value.transform.position - transform.position;
            toPlayer.y = 0f; // ignore vertical difference
            toPlayer.Normalize();

            return toPlayer;
        }

        private Vector3 startPos;
        private float elapsed;
        protected virtual IEnumerator FightThePlayer()
        {
            if (playerReference?.Value == null) yield break;

            startPos = transform.position;

            Vector3 targetPos = Vector3.zero;
            Vector3 originalFleeDirection;

            // Initialize
            FleeDirection = originalFleeDirection = ComputeInitialFleeDirection(); // triggers setter → sets startPos & elapsed
            fightThePlayer = true;
            reelingDuringStruggle.Value = _isReeling && fightThePlayer;

            _effects?.SetVDirection(originalFleeDirection);
            _effects?.StartVEffect(); // now applies the rotation internally

            float worldAngle = Mathf.Atan2(_fleeDirection.x, _fleeDirection.z) * Mathf.Rad2Deg;
            OnFishStartFleeing?.Invoke(worldAngle);

            while (fightThePlayer && struggleAllowed)
            {
                //float resistance = Mathf.Clamp01(_resistanceOverride);
                elapsed += Time.deltaTime * (1f - _resistanceOverride * 0.9f);

                if (_isReeling)
                {
                    FleeDirection = GetDirectionToPlayer();
                    targetPos = startPos + _fleeDirection * ((_tugOfWar.FleeSpeed * 0.05f) * elapsed);
                }
                else
                {
                    FleeDirection = originalFleeDirection;
                    targetPos = startPos + _fleeDirection * (_tugOfWar.FleeSpeed * elapsed);
                }


                transform.position = targetPos;

                OnFishFleeingUpdate?.Invoke(
                    Mathf.Atan2(originalFleeDirection.x, originalFleeDirection.z) * Mathf.Rad2Deg
                );

                yield return null;
            }
        }

        private Vector3 ComputeInitialFleeDirection()
        {
            float relativeYaw = _tugOfWar.FleeRange - 90f;
            return Quaternion.Euler(0, relativeYaw, 0) * playerReference.Value.forward;
        }

        protected virtual IEnumerator BiteHoldingPattern(Vector3 submergedPos)
        {
            // Subtle movement while fish is on the hook
            Vector3 bitePosition = submergedPos;
            Vector3 biteRotation = bobberModel.localEulerAngles;

            while (isBiting)
            {
                // Small erratic movements
                float time = Time.time;
                Vector3 jitter = new Vector3(
                    Mathf.Sin(time * 8f) * 0.02f,
                    Mathf.Sin(time * 6f) * 0.01f,
                    0f
                );

                Vector3 rotJitter = new Vector3(0f, 0f, Mathf.Sin(time * 7f) * 2f);

                bobberModel.localPosition = bitePosition + jitter;
                bobberModel.localEulerAngles = biteRotation + rotJitter;

                yield return null;
            }
        }

        //Called by external event
        public virtual void EndFishSequence()        {
            FishIsTired();

            isFishActive = false;
            isBiting = false;

            bobberModel.localPosition = originalPosition;
            bobberModel.localEulerAngles = originalRotation;
        }

        // Inspector buttons for testing
        [ContextMenu("Test Water Entry")]        public void TestWaterEntry()
        {
            BobberTouchedWater();
        }

        [ContextMenu("Test Tap Animation")]
        public void FishTap()
        {
            if (!isFishActive && !isSubmersing)
            {
                StartCoroutine(PerformTap(0));
            }
        }

        [ContextMenu("Test Final Bite (Hook) Animation")]
        public virtual void FishHooked()
        {
            if (!isFishActive && !isSubmersing)
            {
                StartCoroutine(PerformFinalBite());
            }
        }

        public virtual FishJoint GetJoint()
        {
            return fishJoint;
        }

        public GameObject hideModel;

        internal void ToggleHide(bool hide)
        {
            if (hide)
            {

            }
            else
            {
                gameObject.SetActive(true);
            }
            hideModel.SetActive(!hide);
        }
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(FishingBobber))]
    public class FishingBobberEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector
            DrawDefaultInspector();
            // Add buttons for testing
            FishingBobber fishingBobber = (FishingBobber)target;
            if (GUILayout.Button("Test Water Entry"))
            {
                fishingBobber.TestWaterEntry();
            }

            if (GUILayout.Button("Test Tap Animation"))
            {
                fishingBobber.FishTap();
            }

            if (GUILayout.Button("Test Final Bite (Hook) Animation"))
            {
                fishingBobber.FishHooked();
            }
        }
    }
#endif
}