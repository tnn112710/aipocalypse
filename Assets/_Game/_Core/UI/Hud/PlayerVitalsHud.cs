using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // Health and stamina bars for the active player. At low sanity they flicker (SanityGlitch); the values stay true.
    public sealed class PlayerVitalsHud : PlayerHudView
    {
        [SerializeField] StatBar _health;
        [SerializeField] StatBar _stamina;
        [SerializeField, Tooltip("Optional: faded by sanity's flicker")] CanvasGroup _group;

        // Polled after Player.Update: two bars, and StatBar ignores unchanged values.
        void LateUpdate()
        {
            IPlayer player = Player;
            if (player == null || player.Health == null)
                return;

            _health.SetValue(player.Health.Current, player.Health.Max);
            _stamina.SetValue(player.Stamina.Current, player.Stamina.Max);
            _stamina.SetWarning(player.Stamina.IsExhausted);
            if (_group != null)
                _group.alpha = SanityGlitch.TryRead(player, out float sanity, out SanityEffectSettings effects)
                    ? SanityGlitch.Flicker(sanity, Time.time, effects.LiesFrom) : 1f;
        }
    }
}
