using Aipocalypse.Core.Combat;
using UnityEngine;

namespace Aipocalypse.Core.Entities
{
    // What enemies, companions and other outside systems need from the player: where it is and how to hurt it.
    public interface IPlayerTarget : IDamageable
    {
        Transform Transform { get; }
    }
}
