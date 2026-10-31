using System.Collections.Generic;
using Aipocalypse.Core.Entities;

namespace Aipocalypse.Core.EnemySystem
{
    // How many enemies may close in on one target at once (EnemySystemSettings.AttackersPerTarget; a kind without an entry is
    // unlimited). A holder keeps its slot while near the target and frees it when it backs off, switches target or stops.
    public sealed class AttackSlots
    {
        readonly EnemySystemSettings _settings;
        readonly Dictionary<object, ITargetable> _held = new();
        readonly Dictionary<ITargetable, int> _counts = new();

        public AttackSlots(EnemySystemSettings settings) => _settings = settings;

        public bool Holds(object holder, ITargetable target) =>
            target != null && _held.TryGetValue(holder, out ITargetable held) && ReferenceEquals(held, target);

        public int Taken(ITargetable target) => target != null && _counts.TryGetValue(target, out int count) ? count : 0;

        // True if `holder` holds a slot on `target` afterwards. Taking one on another target frees the previous one.
        public bool TryTake(object holder, ITargetable target)
        {
            if (target == null)
                return false;
            if (Holds(holder, target))
                return true;
            if (_settings.AttackersPerTarget.TryGetValue(target.Kind, out int capacity) && Taken(target) >= capacity)
                return false;
            Release(holder);
            _held[holder] = target;
            _counts[target] = Taken(target) + 1;
            return true;
        }

        public void Release(object holder)
        {
            if (!_held.Remove(holder, out ITargetable target))
                return;
            int count = Taken(target) - 1;
            if (count > 0)
                _counts[target] = count;
            else
                _counts.Remove(target);
        }
    }
}
