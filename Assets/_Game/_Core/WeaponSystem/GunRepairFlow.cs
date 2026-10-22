using System;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.QuickTimeSystem;

namespace Aipocalypse.Core.WeaponSystem
{
    // The jam repair's QTE rules for one gun: the QTE starts part-way through clearing, a hit finishes the repair early,
    // a miss adds repair time, a cancelled QTE interrupts the clear. Plain C#; HeldGun presents what happens.
    public sealed class GunRepairFlow
    {
        readonly Gun _gun;
        readonly GunRepairProfile _profile;
        readonly QuickTimeService _qte;
        readonly object _owner;
        readonly Action<QteResult> _completed;
        bool _requested;

        public event Action<IQteSession> QteStarted;
        public event Action Missed;

        // `owner` identifies this gun's QTE in the QuickTimeService.
        public GunRepairFlow(Gun gun, GunRepairProfile profile, QuickTimeService qte, object owner)
        {
            _gun = gun;
            _profile = profile;
            _qte = qte;
            _owner = owner;
            _completed = OnCompleted;
        }

        public bool QteSucceeded { get; private set; }

        // A new gun action: the last repair's QTE no longer counts.
        public void Reset()
        {
            _requested = false;
            QteSucceeded = false;
        }

        // Only while the gun is held and ready.
        public void Tick()
        {
            if (_requested || _qte == null || _profile == null || _profile.Qte == null || _gun.Action != GunAction.ClearingJam ||
                _gun.ActionProgress < _profile.QteStartProgress)
                return;
            _requested = true;
            if (_qte.TryStart(_owner, _profile.Qte, _completed, ownerControlled: true))
                QteStarted?.Invoke(_qte.Session);
        }

        public void Cancel() => _qte?.Cancel(_owner);

        public void OnAttempted(in QteAttempted attempt)
        {
            if (_profile == null || _qte == null || attempt.Result != QteAttemptResult.Miss || !_qte.IsActiveFor(_owner))
                return;
            _gun.AddRepairTime(_profile.MissPenalty, _profile.MaxMissPenalty);
            Missed?.Invoke();
        }

        void OnCompleted(QteResult result)
        {
            if (_gun.Action != GunAction.ClearingJam)
                return;
            if (result == QteResult.Success)
            {
                QteSucceeded = true;
                _gun.CompleteRepair();
            }
            else if (result == QteResult.Cancelled)
                _gun.Interrupt();
        }
    }
}
