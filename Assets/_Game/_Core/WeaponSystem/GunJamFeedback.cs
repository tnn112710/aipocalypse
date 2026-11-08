using System;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MessagePipe;
using MoreMountains.Feedbacks;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // On the gun user's entity: plays camera / time feedback for its own guns' jams and repairs.
    public sealed class GunJamFeedback : MonoBehaviour
    {
        [SerializeField] MMF_Player _jammed;
        [SerializeField] MMF_Player _dryFire;
        [SerializeField] MMF_Player _slap;
        [SerializeField] MMF_Player _punch;
        [SerializeField] MMF_Player _miss;
        [SerializeField] MMF_Player _success;
        [SerializeField] MMF_Player _natural;

        IEquipmentOwner _owner;
        IDisposable _subscriptions;

        [Inject]
        void Construct(ISubscriber<GunJamBeat> beats, ISubscriber<GunRepairCompleted> repairs)
        {
            _owner = GetComponentInParent<IEquipmentOwner>();
            _subscriptions = DisposableBag.Create(
                beats.Subscribe(OnBeat, e => ReferenceEquals(e.Owner, _owner)),
                repairs.Subscribe(OnRepaired, e => ReferenceEquals(e.Owner, _owner)));
        }

        void OnBeat(GunJamBeat beat) => Play(beat.Kind switch
        {
            GunJamBeatKind.Jammed => _jammed,
            GunJamBeatKind.DryFire => _dryFire,
            GunJamBeatKind.Slap => _slap,
            GunJamBeatKind.Punch => _punch,
            GunJamBeatKind.Miss => _miss,
            _ => null,
        });

        void OnRepaired(GunRepairCompleted repair) => Play(repair.QteSuccess ? _success : _natural);

        void Play(MMF_Player player)
        {
            if (player != null)
                player.PlayFeedbacks(transform.position);
        }

        void OnDestroy() => _subscriptions?.Dispose();
    }
}
