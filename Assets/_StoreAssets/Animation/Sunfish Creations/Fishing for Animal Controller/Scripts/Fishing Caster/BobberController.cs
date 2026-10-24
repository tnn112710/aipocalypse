using SunfishCreations.Animations;
using MalbersAnimations;
using MalbersAnimations.Scriptables;
using MalbersAnimations.Utilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Animations;

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/bobber-controller")]
    public class BobberController : MonoBehaviour, IBobberSpawner
    {
        public UnityEvent OnBobberTouchWater;
        public UnityEvent OnBobberExitWater;
        public UnityEvent OnReelerComplete;

        [SerializeField] private GameObject _bobberPrefab;
        [SerializeField] private TransformReference _rodTip = new();
        [SerializeField] private TransformReference _fisherRef = new();

        [Header("Bobber Positioning")]
        [Range(0, 1)]
        [Tooltip("Controls where the bobber spawns between rod tip (0) and water surface (1)")]
        [SerializeField] private float _spawnInterpolation = 0.1f;

        [SerializeField] private bool _useArcTrajectory = true;
        [Range(0, 5f)]
        [Tooltip("Height of the arc when using arc trajectory (only applies when _useArcTrajectory is true)")]
        [SerializeField] private float _arcHeight = 2f;

        [Header("Animation Settings")]
        [SerializeField] private bool _animateToSurface = true;
        [Range(0.1f, 5f)]
        [Tooltip("Duration of the bobber animation from spawn to surface")]
        [SerializeField] private float _animationDuration = 1.5f;
        [SerializeField] private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Retreat Animation Settings")]
        [Range(0.1f, 5f)]
        [Tooltip("Duration of the bobber retreat animation from surface back to rod tip")]
        [SerializeField] private float _retreatAnimationDuration = 1.2f;

        [Tooltip("Use this to despawn the bobber before the end of the animation")]
        [Range(0, 1)]
        [SerializeField] private float _retreatDespawn;
        [SerializeField] private AnimationCurve _retreatAnimationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Reeling Settings")]
        [Range(0.5f, 10f)]
        [Tooltip("Base speed at which the bobber moves towards the player when reeling")]
        [SerializeField] private float _baseReelingSpeed = 3f;

        [Range(0f, 2f)]
        [Tooltip("How much the bobber bobs up and down while reeling (0 = no bobbing)")]
        [SerializeField] private float _reelingBobAmount = 0.2f;

        [Range(0.5f, 5f)]
        [Tooltip("Speed of the bobbing motion while reeling")]
        [SerializeField] private float _reelingBobSpeed = 2f;

        [Tooltip("Use this to edit bobber retreat end position")]
        public TransformOffset RetreatPositionOffset;

        private FishTugOfWar _tugOfWar;
        private FishingBobber _fishingBobber;
        private bool _successfulHit;
        private Vector3 _surfacePoint;

        // Animation variables
        private Coroutine _animationCoroutine;
        private bool _isAnimating = false;
        private bool _isRetreating = false;

        // Reeling variables
        private bool _isReeling = false;
        private float _currentReelingSpeed = 0f;
        private float _reelingBobTimer = 0f;
        private float _targetReelingDistance = 0f;

        private Vector3 _bobberRetreatDestination;
        private Vector3 _retrievalPoint;

        private BobberRetreatPosition _retreatPos;

        private FishCatcher fishCatcher;

        private BobberRetreatPosition GetFishHolder()
        {
            if (_retreatPos == null && _fisherRef.Value != null)
            {
                _retreatPos = _fisherRef.Value.GetComponentInChildren<BobberRetreatPosition>();
            }

            if (_retreatPos == null)
            {
                Debug.LogError("Retreat Position not found");
            }

            return _retreatPos;
        }

        private void Start()
        {
            Initialization();
        }
        protected virtual void Initialization()
        {
            fishCatcher = GetComponentInChildren<FishCatcher>();
            _tugOfWar = GetComponentInChildren<FishTugOfWar>();

            SpawnBobber();
            GetFishHolder();
        }

        protected virtual void Update()
        {
            // Handle reeling movement
            if (_isReeling && _fishingBobber != null && _fishingBobber.gameObject.activeInHierarchy && _fisherRef.Value != null)
            {
                UpdateReelingMovement();
            }
        }

        protected virtual void UpdateReelingMovement()
        {
            Vector3 currentBobberPosition = _fishingBobber.transform.position;

            // Calculate direction towards player (horizontal plane only)
            Vector3 directionToPlayer = _retrievalPoint - currentBobberPosition;
            directionToPlayer.y = 0f;
            float horizontalDistance = directionToPlayer.magnitude;

            // Reeling complete condition: bobber is close enough to _retrievalPoint
            if (horizontalDistance <= _targetReelingDistance && _isReeling)
            {
                _fishingBobber.PreventStruggle();
                StopReeling();
                OnReelerComplete?.Invoke();
                return;
            }

            if (horizontalDistance > 0f)
            {
                directionToPlayer /= horizontalDistance;
            }

            // Move bobber toward player
            Vector3 targetPosition = currentBobberPosition + directionToPlayer * _currentReelingSpeed * Time.deltaTime;

            targetPosition.y = _surfacePoint.y;

            // Add bobbing effect
            if (_reelingBobAmount > 0f)
            {
                _reelingBobTimer += Time.deltaTime * _reelingBobSpeed;
                float bobOffset = Mathf.Sin(_reelingBobTimer) * _reelingBobAmount;
                //targetPosition.y += bobOffset;
            }

            _fishingBobber.ApplyReelingMovement(targetPosition, _retrievalPoint, _currentReelingSpeed);

        }

        protected virtual void SpawnBobber()
        {
            if (_bobberPrefab == null)
            {
                Debug.LogWarning("No bobber prefab assigned. Fishing will work without visual bobber.", this);
                return;
            }

            GameObject bobber = Instantiate(_bobberPrefab);
            _fishingBobber = bobber.GetComponent<FishingBobber>();

            if (_tugOfWar != null && _tugOfWar.enabled)
            {
                _fishingBobber.SetTugOfWarRef(_tugOfWar);
            }

            bobber.SetActive(false);
            bobber.transform.SetParent(null);
        }

        public virtual void InformWaterHit(Vector3 point, LayerMask waterLayer, Vector3 retrievalPoint)
        {
            if (_bobberPrefab == null || _fishingBobber == null)
            {
                _successfulHit = true;
                return;
            }

            _retrievalPoint = retrievalPoint;

            Vector3 surfacePoint = point;
            Vector3 rayStart = point + Vector3.up * 5f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit surfaceHit, 10f, waterLayer, QueryTriggerInteraction.Collide))
            {
                surfacePoint = surfaceHit.point;
            }

            _surfacePoint = surfacePoint;
            _successfulHit = true;
        }

        protected virtual void SetBobberPosition()
        {
            if (_rodTip.Value == null)
            {
                // Fallback: place directly at surface point if no rod tip reference

                _fishingBobber.SetPosition(_surfacePoint);
                return;
            }

            Vector3 rodTipPosition = _rodTip.Value.position;
            Vector3 targetPosition;

            if (_useArcTrajectory)
            {
                targetPosition = CalculateArcPosition(rodTipPosition, _surfacePoint, _spawnInterpolation, _arcHeight);
            }
            else
            {
                // Simple linear interpolation
                targetPosition = Vector3.Lerp(rodTipPosition, _surfacePoint, _spawnInterpolation);
            }
            _fishingBobber.SetPosition(targetPosition);
        }

        /// <summary>
        /// Calculates a position along an arc trajectory between two points.
        /// </summary>
        protected virtual Vector3 CalculateArcPosition(Vector3 start, Vector3 end, float t, float arcHeight)
        {
            Vector3 basePosition = Vector3.Lerp(start, end, t);

            float heightMultiplier = 4f * t * (1f - t);
            Vector3 arcOffset = Vector3.up * (arcHeight * heightMultiplier);

            return basePosition + arcOffset;
        }

        /// <summary>
        /// Starts the bobber animation from spawn position to surface.
        /// </summary>
        protected virtual void StartBobberAnimation()
        {
            if (_isAnimating || _isRetreating)
            {
                StopBobberAnimation();
            }

            _animationCoroutine = StartCoroutine(AnimateBobberToSurface());
        }

        /// <summary>
        /// Starts the bobber retreat animation from current position back to rod tip.
        /// </summary>
        protected virtual void StartBobberRetreatAnimation()
        {
            if (_isAnimating || _isRetreating)
            {
                StopBobberAnimation();
            }

            _animationCoroutine = StartCoroutine(AnimateBobberRetreat());
        }

        protected virtual void StopBobberAnimation()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
            _isAnimating = false;
            _isRetreating = false;
        }

        /// <summary>
        /// Animates the bobber from spawn position to surface.
        /// </summary>
        protected virtual System.Collections.IEnumerator AnimateBobberToSurface()
        {
            if (_rodTip.Value == null || _fishingBobber == null)
                yield break;

            _isAnimating = true;
            float elapsedTime = 0f;
            Vector3 rodTipPosition = _rodTip.Value.position;

            while (elapsedTime < _animationDuration)
            {
                float normalizedTime = elapsedTime / _animationDuration;
                float curveValue = _animationCurve.Evaluate(normalizedTime);

                // Interpolate from spawn position to surface (1.0)
                float currentInterpolation = Mathf.Lerp(_spawnInterpolation, 1f, curveValue);

                Vector3 targetPosition;
                if (_useArcTrajectory)
                {
                    targetPosition = CalculateArcPosition(rodTipPosition, _surfacePoint, currentInterpolation, _arcHeight);
                }
                else
                {
                    targetPosition = Vector3.Lerp(rodTipPosition, _surfacePoint, currentInterpolation);
                }

                _fishingBobber.SetPosition(targetPosition);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Ensure final position is exactly at surface
            _fishingBobber.SetPosition(_surfacePoint);
            _isAnimating = false;
            BobberIsOnWater();
        }


        /// <summary>
        /// Animates the bobber from current position back to rod tip.
        /// Updates rod tip position each frame to handle moving rod.
        /// </summary>
        protected virtual System.Collections.IEnumerator AnimateBobberRetreat()
        {
            if (_rodTip.Value == null || _fishingBobber == null)
                yield break;

            _isRetreating = true;
            float elapsedTime = 0f;
            Vector3 startPosition = _fishingBobber.transform.position; // Current reeled position

            // Store original transform values to restore later
            Vector3 originalScale = _fishingBobber.transform.localScale;
            Quaternion originalRotation = _fishingBobber.transform.rotation;

            // When a caught fish will be presented in the player's hand, retreat toward the
            // hand-tracking Fish Holder instead of the static Retreat Position.
            bool trackHandDuringRetreat = FishingGlobalSettings.SpawnFishOnWater
                && fishCatcher != null
                && fishCatcher.fishHolder != null;

            if (trackHandDuringRetreat)
            {
                _bobberRetreatDestination = fishCatcher.fishHolder.transform.position;
            }
            else if (_retreatPos != null)
            {
                _bobberRetreatDestination = _retreatPos.transform.position;
            }
            else
            {
                Debug.LogError("Fallback to rod tip if Retreat isn't assigned");
                _bobberRetreatDestination = _rodTip.Value != null ? _rodTip.Value.position : transform.position;
            }



            while (elapsedTime < _retreatAnimationDuration)
            {
                float progress = Mathf.Clamp01(elapsedTime / _retreatAnimationDuration);

                // Keep tracking the hand every frame since it may be animating.
                if (trackHandDuringRetreat)
                {
                    _bobberRetreatDestination = fishCatcher.fishHolder.transform.position;
                }

                // If we already passed the given animationPercentage, stop updating destination
                /*if (progress <= _retreatAnimationPercentage)
                {
                    _bobberRetreatDestination = _rodTip.Value.position;
                }*/
                if (progress >= _retreatDespawn)                {
                    break;
                }

                float normalizedTime = elapsedTime / _retreatAnimationDuration;
                float curveValue = _retreatAnimationCurve.Evaluate(normalizedTime);

                // When tracking the hand, skip RetreatPositionOffset — it's a fixed world-space
                // nudge meant for the static Retreat Position target.
                Vector3 targetPosition;
                Vector3 offsetPosition = trackHandDuringRetreat
                    ? _bobberRetreatDestination
                    : _bobberRetreatDestination + RetreatPositionOffset.Position;

                if (_useArcTrajectory)
                {
                    targetPosition = CalculateArcPosition(startPosition, offsetPosition, curveValue, _arcHeight);
                }
                else
                {
                    targetPosition = Vector3.Lerp(startPosition, offsetPosition, curveValue);
                }

                _fishingBobber.SetPosition(targetPosition);

                // When tracking the hand, blend toward the Fish Holder's live rotation.
                if (trackHandDuringRetreat)
                {
                    Quaternion handRotation = fishCatcher.fishHolder.transform.rotation;
                    _fishingBobber.transform.rotation = Quaternion.Lerp(originalRotation, handRotation, curveValue);
                }
                else
                {
                    // Apply rotation offset (interpolate from current to target rotation)
                    Quaternion targetRotation = Quaternion.Euler(RetreatPositionOffset.Rotation) * originalRotation;
                    _fishingBobber.transform.rotation = Quaternion.Lerp(originalRotation, targetRotation, curveValue);
                }

                // Apply scale offset
                Vector3 targetScale = Vector3.Scale(originalScale, RetreatPositionOffset.Scale);
                _fishingBobber.transform.localScale = Vector3.Lerp(originalScale, targetScale, curveValue);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (trackHandDuringRetreat)
            {
                _bobberRetreatDestination = fishCatcher.fishHolder.transform.position;
            }

            _fishingBobber.SetPosition(trackHandDuringRetreat
                ? _bobberRetreatDestination
                : _bobberRetreatDestination + RetreatPositionOffset.Position);

            // Restore original scale. When tracking the hand, keep the rotation matching Fish Holder.
            _fishingBobber.transform.localScale = originalScale;
            _fishingBobber.transform.rotation = trackHandDuringRetreat
                ? fishCatcher.fishHolder.transform.rotation
                : originalRotation;

            _isRetreating = false;

            HandleFishingEnd();
        }

        protected virtual void OnDestroy()
        {
            StopBobberAnimation();
            if (_fishingBobber != null)
            {
                DestroyImmediate(_fishingBobber);
            }
        }

        void IBobberSpawner.OnBobberAnimationStart(int animationHash) { }
        void IBobberSpawner.OnBobberAnimationEnd(int animationHash) { }

        void IBobberSpawner.OnBobberSpawn()
        {
            if (!_successfulHit || _fishingBobber == null)
                return;

            // Set initial spawn position
            SetBobberPosition();

            //_fishingBobber.gameObject.SetActive(true);
            _fishingBobber.ToggleHide(false);

            if (_animateToSurface)
            {
                StartBobberAnimation();
            }
            else
            {
                BobberIsOnWater();
            }
        }

        void IBobberSpawner.OnBobberRetreat()
        {
            if (_isReeling)
            {
                StopReeling();
            }

            // Start the retreat animation back to rod tip
            if (_fishingBobber != null && _fishingBobber.gameObject.activeInHierarchy)
            {
                StartBobberRetreatAnimation();
                OnBobberExitWater?.Invoke();
            }
        }

        private void BobberIsOnWater()
        {
            OnBobberTouchWater?.Invoke();

            //Remove this for top down usage

            /*var aligner = _fishingBobber.GetComponent<Aligner>();
            if (aligner != null && _fisherRef.Value != null)
            {
                aligner.Align(_fisherRef.Value);
            }*/
        }

        /// <summary>
        /// Called when fishing ends (reeling in, canceling).
        /// </summary>
        public virtual void HandleFishingEnd()
        {
            StopBobberAnimation();
            StopReeling();
            _successfulHit = false;
            if (_fishingBobber != null)
            {
                if (FishingGlobalSettings.SpawnFishOnWater)
                {
                    fishCatcher.SetBobberReference(_fishingBobber);

                    // Attach the fish to the hand-tracking Fish Holder immediately.
                    fishCatcher.AttachFishToHand();
                }
                _fishingBobber.ToggleHide(true);
            }
        }

        protected virtual void SetConstraintSource(Transform affected, Transform targetSource)
        {
            ParentConstraint parentConstraint = affected.GetComponent<ParentConstraint>();

            ConstraintSource source = new ConstraintSource();

            source.sourceTransform = targetSource;
            source.weight = 1;

            //Clear Sources
            while (parentConstraint.sourceCount > 0)
            {
                parentConstraint.RemoveSource(parentConstraint.sourceCount - 1);
            }

            parentConstraint.AddSource(source);
            parentConstraint.constraintActive = true;
        }

        //Called by RodReeler
        public virtual void StartReeling(float speed, float distance)
        {
            if (_fishingBobber == null || !_fishingBobber.gameObject.activeInHierarchy)
                return;

            _isReeling = true;
            _fishingBobber.SetReeling(_isReeling);
            _currentReelingSpeed = _baseReelingSpeed * speed;
            //_reelingStartPosition = _fishingBobber.transform.position;
            _reelingBobTimer = 0f;
            _targetReelingDistance = distance;

            //Debug.Log($"Started reeling at speed: {_currentReelingSpeed}, target distance: {distance}");
        }

        //Called by RodReeler
        public virtual void StopReeling()
        {
            if (_isReeling)
            {
                _isReeling = false;
                _fishingBobber.SetReeling(_isReeling);
                _currentReelingSpeed = 0f;
            }
        }
        // Debug/Utility methods
#if UNITY_EDITOR        /// <summary>
        /// Visualize the trajectory in the Scene view
        /// </summary>
        protected virtual void OnDrawGizmos()
        {
            if (_rodTip.Value == null || !_successfulHit)
                return;

            Vector3 rodTipPos = _rodTip.Value.position;
            Vector3 surfacePos = _surfacePoint;
            Vector3 offsetRodTipPos = rodTipPos + RetreatPositionOffset.Position;

            // Draw trajectory line/arc (Cast animation)
            Gizmos.color = Color.yellow;

            if (_useArcTrajectory)
            {
                // Draw arc trajectory
                Vector3 previousPos = rodTipPos;
                for (int i = 1; i <= 20; i++)
                {
                    float t = i / 20f;
                    Vector3 currentPos = CalculateArcPosition(rodTipPos, surfacePos, t, _arcHeight);
                    Gizmos.DrawLine(previousPos, currentPos);
                    previousPos = currentPos;
                }
            }
            else
            {
                // Draw straight line
                Gizmos.DrawLine(rodTipPos, surfacePos);
            }

            // Draw current bobber position
            Gizmos.color = Color.red;
            Vector3 bobberPos = _useArcTrajectory
                ? CalculateArcPosition(rodTipPos, surfacePos, _spawnInterpolation, _arcHeight)
                : Vector3.Lerp(rodTipPos, surfacePos, _spawnInterpolation);
            Gizmos.DrawWireSphere(bobberPos, 0.1f);

            // Draw spawn position (green) and surface position (blue) for reference
            if (_isAnimating || _isRetreating)
            {
                Gizmos.color = Color.green;
                Vector3 spawnPos = _useArcTrajectory
                    ? CalculateArcPosition(rodTipPos, surfacePos, _spawnInterpolation, _arcHeight)
                    : Vector3.Lerp(rodTipPos, surfacePos, _spawnInterpolation);
                Gizmos.DrawWireSphere(spawnPos, 0.08f);

                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(surfacePos, 0.08f);
            }

            // Draw retreat trajectory with offset
            if (_isRetreating)
            {
                Gizmos.color = Color.magenta;
                if (_useArcTrajectory)
                {
                    Vector3 previousPos = surfacePos;
                    for (int i = 1; i <= 20; i++)
                    {
                        float t = 1f - (i / 20f); // Reverse direction
                        Vector3 currentPos = CalculateArcPosition(offsetRodTipPos, surfacePos, t, _arcHeight);
                        Gizmos.DrawLine(previousPos, currentPos);
                        previousPos = currentPos;
                    }
                }
                else
                {
                    // Draw straight line retreat with offset
                    Gizmos.DrawLine(surfacePos, offsetRodTipPos);
                }
            }

            // Always visualize the retreat offset position
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(offsetRodTipPos, Vector3.one * 0.1f);

            // Draw line from rod tip to offset position to show the offset
            if (RetreatPositionOffset.Position != Vector3.zero)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(rodTipPos, offsetRodTipPos);

                // Add a small label indicator
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(rodTipPos, 0.05f);
            }

            // Visualize scale offset with a wireframe representation
            if (RetreatPositionOffset.Scale != Vector3.one)
            {
                Gizmos.color = Color.magenta;
                Matrix4x4 originalMatrix = Gizmos.matrix;

                // Set up matrix for scaled visualization at offset position
                Gizmos.matrix = Matrix4x4.TRS(offsetRodTipPos, Quaternion.identity, RetreatPositionOffset.Scale);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.15f);

                // Restore original matrix
                Gizmos.matrix = originalMatrix;
            }

            // Visualize rotation offset with coordinate axes
            if (RetreatPositionOffset.Rotation != Vector3.zero)
            {
                Quaternion offsetRotation = Quaternion.Euler(RetreatPositionOffset.Rotation);
                Vector3 forward = offsetRotation * Vector3.forward * 0.2f;
                Vector3 right = offsetRotation * Vector3.right * 0.2f;
                Vector3 up = offsetRotation * Vector3.up * 0.2f;

                // Draw rotation axes at offset position
                Gizmos.color = Color.red;
                Gizmos.DrawRay(offsetRodTipPos, right);
                Gizmos.color = Color.green;
                Gizmos.DrawRay(offsetRodTipPos, up);
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(offsetRodTipPos, forward);
            }
        }
#endif
    }
}