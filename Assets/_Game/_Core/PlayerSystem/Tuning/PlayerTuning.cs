using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Groups the player's config assets; each rule gets only the group it needs.
    [CreateAssetMenu(fileName = "PlayerTuning", menuName = "Aipocalypse/Player/Player Tuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [SerializeField, Required, InlineEditor] PlayerMovementTuning _movement;
        [SerializeField, Required, InlineEditor] PlayerVitalsTuning _vitals;
        [SerializeField, Required, InlineEditor] PlayerCameraTuning _camera;
        [SerializeField, Required, InlineEditor] PlayerInteractionTuning _interaction;
        [SerializeField, Required, InlineEditor] PlayerHeldItemTuning _heldItem;
        [SerializeField, Required, InlineEditor] PlayerSoundTuning _sounds;
        [SerializeField, Required, InlineEditor] PlayerVisionTuning _vision;
        [SerializeField, Required, InlineEditor, Tooltip("Plays when the player dies")] PoseSequence _deathSequence;

        public PlayerMovementTuning Movement => _movement;
        public PlayerVitalsTuning Vitals => _vitals;
        public PlayerCameraTuning Camera => _camera;
        public PlayerInteractionTuning Interaction => _interaction;
        public PlayerHeldItemTuning HeldItem => _heldItem;
        public PlayerSoundTuning Sounds => _sounds;
        public PlayerVisionTuning Vision => _vision;
        public PoseSequence DeathSequence => _deathSequence;
    }
}
