using MalbersAnimations.Scriptables;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using SunfishCreations.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/fishable-area")]
    public class FishableArea : MonoBehaviour
    {
        private const float _globalInitialDelay = 2f;

        [Header("Fish that you can get from this area")]
        [Tooltip("Array of fish types that can be caught in this area")]
        [SerializeField] private FishData[] _fishTypes;

        [Header("Rarity Weights")]
        [Tooltip("Relative weight for Common fish in the pool (higher = more frequent)")]
        [SerializeField] private float _commonWeight = 60;
        [Tooltip("Relative weight for Uncommon fish in the pool (higher = more frequent)")]
        [SerializeField] private float _uncommonWeight = 30;
        [Tooltip("Relative weight for Rare fish in the pool (higher = more frequent)")]
        [SerializeField] private float _rareWeight = 10;

        [Tooltip("Reference to the remaining time value for the strike window")]
        public FloatReference remainingTime;

        [Header("Events")]
        [Tooltip("Invoked when a fish nibbles at the bait")]
        public UnityEvent OnFishNibble;
        [Tooltip("Invoked when a fish strikes and becomes hookable")]
        public UnityEvent OnFishBite;
        [Tooltip("Invoked when the strike window ends")]
        public UnityEvent OnBiteWindowEnd;
        [Tooltip("Invoked when a new Fish IS created")]
        public UnityEvent<FishData> OnFishCreated;

        [Tooltip("Is the player reeling when the fish is trying to flee?")]
        public BoolReference reelingDuringStruggle;

        public FEvent OnFishCreatedX;

        public virtual bool IsFishStriking => _fishIsStriking;
        public virtual int CurrentNibbleCount => _currentNibbleCount;
        public virtual float TimeUntilNextNibble => Mathf.Max(0, _nextNibbleTime - _timeSinceLastNibble);


        [Header("Bobber Reference")]
        public TransformReference bobber;
        protected FishingBobber _fishingBobber;

        protected FishData _currentFish;
        protected int _currentNibbleCount;
        protected float _timeSinceLastNibble;
        protected float _nextNibbleTime;
        protected float _strikeStartTime;
        protected bool _fishIsStriking;
        protected bool _isPlayerFishing;
        protected float _strikeTimer;
        protected float _strikeElapsed;

        protected virtual void Update()
        {
            if (!_isPlayerFishing) return;

            _timeSinceLastNibble += Time.deltaTime;

            if (!_fishIsStriking && _timeSinceLastNibble >= _nextNibbleTime)
                TriggerNibble();

            if (_fishIsStriking)
                UpdateStrikeTimer();
        }

        protected virtual void UpdateStrikeTimer()
        {
            float speedMultiplier = reelingDuringStruggle ? 4f : 1f;

            // accumulate time smoothly, frame by frame
            _strikeElapsed += Time.deltaTime * speedMultiplier;

            // update UI
            remainingTime.Value = _strikeTimer - _strikeElapsed;

            if (_strikeElapsed >= _strikeTimer)
                EndStrikeWindow();
        }

        //Called by external event (in Fishable Area)
        public virtual void BobberTouchedWater()
        {
            // Update bobber position immediately
            if (bobber?.Value != null)
            {
                _fishingBobber = bobber.Value.GetComponent<FishingBobber>();
            }
        }

        public virtual void RespondToHookedEvent(FishData fishData)
        {
            if (_isPlayerFishing && FishingGlobalSettings.SpawnFishOnWater)
            {
                _fishingBobber.SpawnFish(fishData);
            }
        }

        /// <summary>
        /// Selects a random fish from the pool, weighted by rarity tier.
        /// </summary>
        public virtual FishData GenerateFish()
        {
            if (_fishTypes == null || _fishTypes.Length == 0)
                return null;

            float totalWeight = 0f;
            float[] weights = new float[_fishTypes.Length];

            for (int i = 0; i < _fishTypes.Length; i++)
            {
                weights[i] = GetRarityWeight(_fishTypes[i].rarity);
                totalWeight += weights[i];
            }

            // If all weights are zero, fall back to uniform random.
            if (totalWeight <= 0f)            {
                int uniformIndex = Random.Range(0, _fishTypes.Length);
                return _fishTypes[uniformIndex];
            }

            float randomValue = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < _fishTypes.Length; i++)
            {
                cumulative += weights[i];
                if (randomValue <= cumulative)
                    return _fishTypes[i];
            }

            // Fallback
            return _fishTypes[_fishTypes.Length - 1];
        }

        /// <summary>
        /// Returns the pool weight for the given rarity tier.
        /// </summary>
        protected virtual float GetRarityWeight(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:
                    return _commonWeight;
                case Rarity.Uncommon:
                    return _uncommonWeight;
                case Rarity.Rare:
                    return _rareWeight;
                default:
                    return _commonWeight;
            }
        }

        public virtual void StartFishing()
        {
            StartCoroutine(InitialDelayRoutine());
            GenerateNewFish();
            ResetFishingState();
        }

        protected virtual IEnumerator InitialDelayRoutine()
        {
            yield return new WaitForSeconds(_globalInitialDelay);
            _isPlayerFishing = true;
        }

        public virtual void StopFishing()
        {
            _isPlayerFishing = false;
            _fishIsStriking = false;
            ResetFishingState();
        }

        public virtual void FishHooked()
        {
            _isPlayerFishing = false;
            _fishIsStriking = false;
            ResetFishingState();
        }

        protected virtual void GenerateNewFish()
        {
            _currentFish = GenerateFish();
            if (_currentFish != null)
            {
                _strikeTimer = _currentFish.StrikeTimer;
                remainingTime.Value = _strikeTimer;
                OnFishCreated.Invoke(_currentFish);
            }
        }

        public virtual FishData GetCurrentFish()
        {
            return _currentFish;
        }

        protected virtual void ResetFishingState()
        {
            _strikeElapsed = 0f;
            _currentNibbleCount = 0;
            _timeSinceLastNibble = 0f;
            ScheduleNextNibble();
        }

        protected virtual void TriggerNibble()
        {
            _currentNibbleCount++;
            float strikeChance = CalculateStrikeChance();

            if (Random.value <= strikeChance)
            {
                TriggerStrike();
            }
            else
            {
                OnFishNibble?.Invoke();
                _timeSinceLastNibble = 0f;
                ScheduleNextNibble();
            }
        }

        protected virtual float CalculateStrikeChance()
        {
            if (_currentFish == null) return 0f;

            if (_currentNibbleCount <= _currentFish.minNibbles)
                return 0f;

            if (_currentNibbleCount >= _currentFish.Nibbles)
                return 1f;

            return 0.5f;
        }

        protected virtual void TriggerStrike()
        {
            _fishIsStriking = true;
            _strikeElapsed = 0f;
            _strikeStartTime = Time.time;
            OnFishBite?.Invoke();
        }

        protected virtual void EndStrikeWindow()
        {
            _fishIsStriking = false;
            _currentNibbleCount = 0;
            _timeSinceLastNibble = 0f;
            ScheduleNextNibble();
            OnBiteWindowEnd?.Invoke();
        }

        protected virtual void ScheduleNextNibble()
        {
            if (_currentFish != null)
            {
                _nextNibbleTime = _currentFish.NibbleTimer;
            }
            else
            {
                _nextNibbleTime = Random.Range(1f, 2f);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = _fishIsStriking ? Color.red : Color.blue;
            Gizmos.DrawWireSphere(transform.position, 2f);

            if (_isPlayerFishing)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, Vector3.one * 0.5f);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(FishableArea))]
    public class FishableAreaEditor : Editor
    {
        private SerializedProperty _fishTypes;
        private SerializedProperty _remainingTime;
        private SerializedProperty _commonWeight;
        private SerializedProperty _uncommonWeight;
        private SerializedProperty _rareWeight;

        private SerializedProperty _onFishNibble;
        private SerializedProperty _onFishBite;
        private SerializedProperty _onBiteWindowEnd;
        private SerializedProperty reelingDuringStruggle;

        private SerializedProperty OnFishCreated;
        private SerializedProperty bobber;


        private void OnEnable()
        {
            // Serialized Properties
            _fishTypes = serializedObject.FindProperty("_fishTypes");
            _remainingTime = serializedObject.FindProperty("remainingTime");
            _commonWeight = serializedObject.FindProperty("_commonWeight");
            _uncommonWeight = serializedObject.FindProperty("_uncommonWeight");
            _rareWeight = serializedObject.FindProperty("_rareWeight");

            _onFishNibble = serializedObject.FindProperty("OnFishNibble");
            _onFishBite = serializedObject.FindProperty("OnFishBite");
            _onBiteWindowEnd = serializedObject.FindProperty("OnBiteWindowEnd");
            reelingDuringStruggle = serializedObject.FindProperty("reelingDuringStruggle");
            OnFishCreated = serializedObject.FindProperty("OnFishCreated");

            bobber = serializedObject.FindProperty("bobber");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            FishableArea fa = (FishableArea)target;

            EditorGUILayout.Space();
            DrawHeader("Fish Settings");

            EditorGUILayout.PropertyField(_fishTypes, true);
            EditorGUILayout.PropertyField(_remainingTime);

            EditorGUILayout.Space();
            DrawHeader("Rarity Weights");
            EditorGUILayout.HelpBox("Higher weight = more likely to be selected from the pool. " +
                "Default ratio is 60 / 30 / 10 (Common / Uncommon / Rare).", MessageType.Info);
            EditorGUILayout.PropertyField(_commonWeight);
            EditorGUILayout.PropertyField(_uncommonWeight);
            EditorGUILayout.PropertyField(_rareWeight);

            EditorGUILayout.Space();
            DrawHeader("Events");

            EditorGUILayout.PropertyField(_onFishNibble);
            EditorGUILayout.PropertyField(_onFishBite);
            EditorGUILayout.PropertyField(_onBiteWindowEnd);
            EditorGUILayout.PropertyField(OnFishCreated);

            EditorGUILayout.Space();
            DrawHeader("Global References");
            EditorGUILayout.HelpBox("Reeling During Struggle is defined in 'Tug-of War' mode, the value is used here to decrease the bar faster if the player is trying to reel during a struggle.", MessageType.Info);
            EditorGUILayout.PropertyField(reelingDuringStruggle);
            EditorGUILayout.PropertyField(bobber);

            EditorGUILayout.Space();
            DrawHeader("Debug Info", 14);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Current Fish", fa.GetCurrentFish(), typeof(FishData), false);
                EditorGUILayout.FloatField("Strike Timer", fa.IsFishStriking ? fa.remainingTime.Value : 0f);
                EditorGUILayout.IntField("Current Nibbles", fa.CurrentNibbleCount);
                EditorGUILayout.FloatField("Time Until Next Nibble", fa.TimeUntilNextNibble);
                EditorGUILayout.Toggle("Is Player Fishing", GetPrivateBool(fa, "_isPlayerFishing"));
                EditorGUILayout.Toggle("Fish Is Striking", fa.IsFishStriking);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // Helper to draw a bold header
        private void DrawHeader(string label, int size = 12)        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.fontSize = size;
            EditorGUILayout.LabelField(label, style);
        }

        // Allows accessing protected booleans for debugging
        private bool GetPrivateBool(FishableArea fa, string fieldName)
        {
            var field = typeof(FishableArea).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(fa);

            return false;
        }
    }
#endif
}