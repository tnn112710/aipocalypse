using Aipocalypse.Core.PlayerSystem;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
    // Health and stamina bars for the active player.
    public sealed class PlayerVitalsHud : PlayerHudView
    {
        [SerializeField] StatBar _health;
        [SerializeField] StatBar _stamina;

        protected override void Bind(Player player) { }
        protected override void Unbind(Player player) { }

        // Polled after Player.Update: two bars, and StatBar ignores unchanged values.
        void LateUpdate()
        {
            Player player = Player;
            if (player == null || player.Health == null)
                return;

            _health.SetValue(player.Health.Current, player.Health.Max);
            _stamina.SetValue(player.Stamina.Current, player.Stamina.Max);
            _stamina.SetWarning(player.Stamina.IsExhausted);
        }
    }
}
