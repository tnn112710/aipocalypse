using Aipocalypse.Core.Combat;
using Aipocalypse.Input;
using VContainer.Unity;

namespace Aipocalypse.Core.PlayerSystem
{
    // Dev builds only: K kills the active player (through DamageService, like a real hit), L revives it (Debug action map).
    public sealed class PlayerCheats : ITickable
    {
        readonly PlayerRegistry _registry;
        readonly DamageService _damage;
        readonly InputService _input;

        public PlayerCheats(PlayerRegistry registry, DamageService damage, InputService input)
        {
            _registry = registry;
            _damage = damage;
            _input = input;
        }

        public void Tick()
        {
            IPlayer player = _registry.Current;
            if (player == null || player.Health == null)
                return;
            if (_input.DebugKillPlayerPressed)
                _damage.Deal(player, new DamageInfo(player.Health.Current, DamageType.Generic, player.Transform.position));
            if (_input.DebugRevivePlayerPressed)
                player.Revive();
        }
    }
}
