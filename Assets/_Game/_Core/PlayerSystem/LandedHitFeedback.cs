using System;
using Aipocalypse.Core.Events;
using MessagePipe;
using MoreMountains.Feedbacks;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.PlayerSystem
{
    // On the Player prefab: a short hit stop and camera kick when this player's hit staggers an enemy or kills it with a
    // headshot. Only those moments, never every bullet, so crowd fights don't stutter.
    public sealed class LandedHitFeedback : MonoBehaviour
    {
        [SerializeField, Tooltip("A hit that staggered its target")] MMF_Player _stagger;
        [SerializeField, Tooltip("A kill on a critical hitbox (headshot)")] MMF_Player _headshotKill;

        GameObject _self;
        IDisposable _subscription;

        [Inject]
        void Construct(ISubscriber<DamageDealt> dealt)
        {
            var player = GetComponentInParent<IPlayer>();
            _self = player != null ? player.Transform.gameObject : null;
            _subscription = dealt.Subscribe(OnDealt, e => _self != null && e.Info.Source == _self);
        }

        void OnDealt(DamageDealt hit)
        {
            MMF_Player feedback = hit.Result.Killed ? (hit.Info.Critical ? _headshotKill : null) : hit.Result.Staggered ? _stagger : null;
            if (feedback != null)
                feedback.PlayFeedbacks(transform.position);
        }

        void OnDestroy() => _subscription?.Dispose();
    }
}
