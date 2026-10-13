using Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // Health and stamina bars for the player whose prefab holds this HUD (PlayerScope registers that Player).
    public sealed class PlayerVitalsHud : MonoBehaviour
    {
        [SerializeField] StatBar _health;
        [SerializeField] StatBar _stamina;

        Player _player;

        [Inject]
        void Construct(Player player) => _player = player;

        // Polled after Player.Update: two bars, and StatBar ignores unchanged values.
        void LateUpdate()
        {
            if (_player == null || _player.Health == null)
                return;

            _health.SetValue(_player.Health.Current, _player.Health.Max);
            _stamina.SetValue(_player.Stamina.Current, _player.Stamina.Max);
            _stamina.SetWarning(_player.Stamina.IsExhausted);
        }
    }
}
