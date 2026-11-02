using HighlightPlus;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Outlines what the player can use right now (the interact focus, or what it's repairing) with one Highlight Plus effect
    // on the player, moved to each new target; nothing is added to the interactables themselves.
    public sealed class InteractHighlighter
    {
        readonly HighlightEffect _effect;
        Transform _target;

        public InteractHighlighter(HighlightEffect effect) => _effect = effect;

        public void Tick(Transform target)
        {
            // By reference: a destroyed target (a picked-up item) must still switch the outline off.
            if (ReferenceEquals(target, _target))
                return;
            _target = target;
            if (target == null)
            {
                _effect.SetHighlighted(false);
                return;
            }
            _effect.SetTarget(target);
            _effect.SetHighlighted(true);
        }
    }
}
