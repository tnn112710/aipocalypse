using UnityEngine;
using MalbersAnimations.Scriptables;
using MalbersAnimations.Controller;

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/secondary-components/fish-tug-of-war")]
    public class FishTugOfWar : MonoBehaviour
    {
        [Header("References")]
        public TransformReference bobber;

        private MAnimal _animal;
        private BobberController _bobberController;
        private FishingBobber _fishingBobber;

        private enum Phase { Idle, Struggle, Tired }
        private Phase _phase = Phase.Idle;
        private float _phaseTimer;
        private Vector3 _fishPullDirection = Vector3.forward;
        private FishData _activeFish;

        public float FleeSpeed { get { return _activeFish.FleeSpeed; } }
        public float FleeRange { get { return _activeFish.FleeRange; } }


        private void Awake()
        {
            Initialization();
        }

        public virtual void Initialization()
        {
            _animal = GetComponentInParent<MAnimal>();
            _bobberController = GetComponentInParent<BobberController>();

            if (_animal == null) { Debug.LogError("Missing Animal"); }
            if (_bobberController == null) { Debug.LogError("Missing Bobber Controller"); }
        }

        private void OnEnable()
        {
            // Subscribe to bobber lifecycle events
            if (_bobberController != null)
            {
                _bobberController.OnBobberTouchWater.AddListener(OnBobberTouchWater);
                _bobberController.OnBobberExitWater.AddListener(OnBobberExitWater);
            }
        }
        public virtual void HandleNewFishGenerated(FishData data)
        {
            _activeFish = data;
        }       

        private void OnBobberTouchWater()
        {
            // get FishingBobber
            if (bobber?.Value != null)
            {
                _fishingBobber = bobber.Value.GetComponent<FishingBobber>();
                _fishingBobber.OnFishFleeingUpdate += HandleFishFleeing;
                _fishingBobber.OnFinalBite += HandleFishFinalBite;
            }
        }

        private void OnBobberExitWater()
        {
            _phase = Phase.Idle;
            UnsubscribeFromBobber();
        }

        private void UnsubscribeFromBobber()
        {
            if (_fishingBobber != null)
            {
                _fishingBobber.OnFishFleeingUpdate -= HandleFishFleeing;
                _fishingBobber.OnFinalBite -= HandleFishFinalBite;
                _fishingBobber = null;
            }
        }

        private void HandleFishFinalBite()
        {
            _phaseTimer = 0;
            _phase = Phase.Tired; //Start struggle cycle
        }

        private void HandleFishFleeing(float worldAngle)
        {
            _fishPullDirection = Quaternion.Euler(0, worldAngle, 0) * Vector3.forward;
            _fishPullDirection.y = 0f;
            _fishPullDirection.Normalize();
        }

        private Vector3 GetInputDirection()
        {
            return new Vector3(_animal.RawInputAxis.x, 0f, _animal.RawInputAxis.z).normalized;
        }

        private void StartStrugglePhase()
        {
            _phase = Phase.Struggle;
            _phaseTimer = 0f;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Struggle:
                    UpdateStruggle();
                    break;
                case Phase.Tired:
                    UpdateTired();
                    break;
            }
        }

        private void UpdateStruggle()
        {
            _phaseTimer += Time.deltaTime;
            Vector3 inputDir = GetInputDirection();

            float opposition = inputDir == Vector3.zero ? 0f : Mathf.Clamp01(Vector3.Dot(inputDir, -_fishPullDirection));
           
            _fishingBobber.SetResistance(opposition); //inject resistance

            // Transition to Tired
            if (_phaseTimer >= _activeFish.StruggleDuration)
            {
                _phase = Phase.Tired;
                _phaseTimer = 0f;
                _fishingBobber.FishIsTired();
            }
        }

        private void UpdateTired()
        {
            _phaseTimer += Time.deltaTime;

            if (_phaseTimer >= _activeFish.TiredDuration)
            {
                _fishingBobber.FishIsFleeing();
                StartStrugglePhase();
            }
        }
        private void OnDisable()
        {
            if (_bobberController != null)
            {
                _bobberController.OnBobberTouchWater.RemoveListener(OnBobberTouchWater);
                _bobberController.OnBobberExitWater.RemoveListener(OnBobberExitWater);
            }
            UnsubscribeFromBobber();
        }
    }
}