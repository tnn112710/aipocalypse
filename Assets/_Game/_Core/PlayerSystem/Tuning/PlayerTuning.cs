using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Groups the player's config assets; each rule gets only the group it needs.
    [CreateAssetMenu(fileName = "PlayerTuning", menuName = "Aipocalypse/Player/Player Tuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [field: SerializeField, Required, InlineEditor] public PlayerMovementTuning Movement { get; private set; }
        [field: SerializeField, Required, InlineEditor] public PlayerVitalsTuning Vitals { get; private set; }
        [field: SerializeField, Required, InlineEditor] public PlayerCameraTuning Camera { get; private set; }
        [field: SerializeField, Required, InlineEditor] public PlayerInteractionTuning Interaction { get; private set; }
        [field: SerializeField, Required, InlineEditor] public PlayerHeldItemTuning HeldItem { get; private set; }
        [field: SerializeField, Required, InlineEditor] public PlayerSoundTuning Sounds { get; private set; }
    }
}
