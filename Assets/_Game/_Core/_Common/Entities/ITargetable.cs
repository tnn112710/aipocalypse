using Aipocalypse.Core.Combat;
using UnityEngine;

namespace Aipocalypse.Core.Entities
{
    // What enemies can go after. Append new kinds at the end (definitions store weights by number).
    public enum TargetKind
    {
        Player,
        Fence,
    }

    // Something enemies may choose as a target (EnemySystem's TargetSelector) and hurt through DamageService.
    public interface ITargetable : IDamageable
    {
        Transform Transform { get; }
        TargetKind Kind { get; }
        // False while it can't be chosen: dead, or hidden by a cheat.
        bool IsTargetable { get; }

        // Where an attacker standing at `from` goes and swings at: the root by default; a fence gives its face on that side.
        Vector3 PointFor(Vector3 from) => Transform.position;
    }

    // Something that stands in the way (a closed fence segment or gate); not targetable = a way through (broken, gate open).
    // Enemies attack one when they have no path to what they hunt, or when the open way in is too far (TargetSelector).
    public interface IBreachable : ITargetable
    {
        float RemainingHealth { get; }
        // From the centre to one end along the barrier (world, on the ground): it blocks the line between the two ends.
        Vector3 Span { get; }
    }
}
