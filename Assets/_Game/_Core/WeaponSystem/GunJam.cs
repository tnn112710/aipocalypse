using System;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.QuickTimeSystem;
using MessagePipe;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // A gun's jam and repair as one part of HeldGun: the timed clear with its optional QTE, plus its feedback. Built by
    // GunJamFactory; HeldGun ticks it and forwards the gun's action events.
    public sealed class GunJam : IDisposable
    {
        readonly Gun _gun;
        readonly GunDefinition _definition;
        readonly GunAnimation _animation;
        readonly GunRepairFlow _repair;
        readonly GunRepairFeedback _feedback;
        readonly IDisposable _attempts;

        internal GunJam(Gun gun, GunDefinition definition, GunAnimation animation, GunJoltPlayer jolt, object qteOwner, IEquipmentOwner holder,
            ItemInstance item, AudioService audio, QuickTimeService qte, IPublisher<GunJamBeat> beats, IPublisher<GunRepairCompleted> completed,
            ISubscriber<QteAttempted> attempts)
        {
            _gun = gun;
            _definition = definition;
            _animation = animation;
            _repair = new GunRepairFlow(gun, definition.Jam.Repair, qte, qteOwner);
            _feedback = new GunRepairFeedback(definition, audio, jolt, beats, completed, holder, item);
            _repair.Missed += _feedback.Missed;
            _gun.Jammed += _feedback.Jammed;
            _gun.DryFired += _feedback.DryFired;
            if (_animation != null)
                _animation.RepairSlapped += OnRepairSlapped;
            _attempts = attempts.Subscribe(OnAttempted);
        }

        // The last clear was finished by a QTE success (the clip ends on its success flourish).
        public bool QteSucceeded => _repair.QteSucceeded;
        // Guns without repair feedback play their own "jam cleared" sound.
        public bool HasFeedback => _definition.Jam.Feedback != null;

        public void Tick(bool ready, float deltaTime)
        {
            if (ready)
                _repair.Tick();
            _feedback.Tick(deltaTime);
        }

        public void ActionStarted() => _repair.Reset();

        // Ends any repair QTE; true when this action cleared a jam.
        public bool ActionEnded(GunAction action, bool finished)
        {
            _repair.Cancel();
            return finished && action == GunAction.ClearingJam;
        }

        public void Cleared() => _feedback.Repaired(_repair.QteSucceeded);

        public void Hide() => _feedback.Reset();

        public void Dispose()
        {
            _attempts.Dispose();
            _repair.Cancel();
            _repair.Missed -= _feedback.Missed;
            _gun.Jammed -= _feedback.Jammed;
            _gun.DryFired -= _feedback.DryFired;
            if (_animation != null)
                _animation.RepairSlapped -= OnRepairSlapped;
        }

        // A slap or punch keyed in the repair clip landed.
        void OnRepairSlapped() => _feedback.Impact(_animation.ImpactIsPunch);

        // A shake of the gun landed (GunShake): the same beat as a slap, or a punch for a hard one.
        public void Shaken(bool hard) => _feedback.Impact(hard);

        void OnAttempted(QteAttempted attempt) => _repair.OnAttempted(attempt);
    }

    // Root service: holds what every gun's jam needs (QTE, sounds, events), so HeldGun asks for one thing.
    public sealed class GunJamFactory
    {
        readonly AudioService _audio;
        readonly QuickTimeService _qte;
        readonly IPublisher<GunJamBeat> _beats;
        readonly IPublisher<GunRepairCompleted> _completed;
        readonly ISubscriber<QteAttempted> _attempts;

        public GunJamFactory(AudioService audio, QuickTimeService qte, IPublisher<GunJamBeat> beats, IPublisher<GunRepairCompleted> completed,
            ISubscriber<QteAttempted> attempts)
        {
            _audio = audio;
            _qte = qte;
            _beats = beats;
            _completed = completed;
            _attempts = attempts;
        }

        // `qteOwner` keys the repair QTE (the held gun).
        public GunJam Create(Gun gun, GunDefinition definition, GunAnimation animation, GunJoltPlayer jolt, object qteOwner, IEquipmentOwner holder,
            ItemInstance item) =>
            new(gun, definition, animation, jolt, qteOwner, holder, item, _audio, _qte, _beats, _completed, _attempts);
    }
}
