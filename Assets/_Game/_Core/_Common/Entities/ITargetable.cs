using Aipocalypse.Core.Combat;
using UnityEngine;

namespace Aipocalypse.Core.Entities
{
    // What enemies can go after. Append new kinds at the end (definitions store weights by number).
    public enum TargetKind
    {
        Player,
    }

    // Something enemies may choose as a target (EnemySystem's TargetSelector) and hurt through DamageService.
    public interface ITargetable : IDamageable
    {
        Transform Transform { get; }
        TargetKind Kind { get; }
        // False while it can't be chosen: dead, or hidden by a cheat.
        bool IsTargetable { get; }
    }
}
