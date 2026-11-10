using System.Collections.Generic;
using MB13.Aipocalypse.Core.Stats;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Carried in a quick slot (keys 3-9), held when selected, used by holding the attack button for UseSeconds (HeldConsumable).
    // Its effects go to the user (IConsumer): health now, health over time, stat scales for a while.
    [CreateAssetMenu(fileName = "Consumable", menuName = "Aipocalypse/Items/Consumable")]
    public sealed class ConsumableDefinition : EquippableDefinition
    {
        [BoxGroup("Use"), SerializeField, Min(0.1f), Tooltip("Seconds the attack button is held to use it")]
        float _useSeconds = 1f;
        [BoxGroup("Use"), SerializeField, Min(1), Tooltip("Uses per item. Above 1 it doesn't stack (Max Stack 1): the slot shows the uses left")]
        int _uses = 1;
        [BoxGroup("Use"), SerializeField, Range(0.1f, 1f), Tooltip("Move speed while using it (sprinting is off)")]
        float _useMoveSpeedScale = 0.5f;

        [BoxGroup("Effects"), SerializeField, Min(0f), Tooltip("HP at once when used")]
        float _heal;
        [BoxGroup("Effects"), SerializeField, Min(0f), Tooltip("HP over Heal Seconds after it's used; a hit pauses it")]
        float _healOverTime;
        [BoxGroup("Effects"), SerializeField, Min(0.1f)]
        float _healSeconds = 10f;
        [BoxGroup("Effects"), SerializeField, Min(0f), Tooltip("Seconds the stat scales last; using another restarts them")]
        float _buffSeconds;
        [BoxGroup("Effects"), SerializeField, Tooltip("Multiplier per stat while the buff lasts (walk speed 1.25 = +25 %)")]
        Dictionary<StatId, float> _buffScales = new();

        public override ItemCategory Category => ItemCategory.Consumable;

        public float UseSeconds => _useSeconds;
        public int Uses => _uses;
        public float UseMoveSpeedScale => _useMoveSpeedScale;
        public float Heal => _heal;
        public float HealOverTime => _healOverTime;
        public float HealSeconds => _healSeconds;
        public float BuffSeconds => _buffSeconds;
        public IReadOnlyDictionary<StatId, float> BuffScales => _buffScales;
        public bool HasBuff => _buffSeconds > 0f && _buffScales != null && _buffScales.Count > 0;
        // Its only effect is health: pointless at full health.
        public bool OnlyHeals => !HasBuff && (_heal > 0f || _healOverTime > 0f);

        public override ItemInstance CreateInstance() => _uses > 1 ? new ConsumableInstance(this) : base.CreateInstance();

#if UNITY_EDITOR
        // Setup scripts only.
        public void SetUse(float seconds, int uses, float moveSpeedScale)
        {
            _useSeconds = seconds;
            _uses = uses;
            _useMoveSpeedScale = moveSpeedScale;
        }

        public void SetEffects(float heal, float healOverTime, float healSeconds, float buffSeconds, Dictionary<StatId, float> buffScales)
        {
            _heal = heal;
            _healOverTime = healOverTime;
            _healSeconds = healSeconds;
            _buffSeconds = buffSeconds;
            _buffScales = buffScales ?? new Dictionary<StatId, float>();
        }

        void OnValidate()
        {
            if (_uses > 1 && MaxStack > 1)
                Debug.LogWarning($"{name}: an item with {_uses} uses doesn't stack; set Max Stack to 1.", this);
        }
#endif
    }
}
