using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.QuickTimeSystem;
using Ami.BroAudio;
using MessagePipe;

namespace Aipocalypse.Core.WeaponSystem
{
    // Presents one gun's jam and repair beats: sounds, jolts of the gun model (on the gun's GunJoltPlayer) and the
    // GunJamBeat / GunRepairQteStarted / GunRepairCompleted events the camera feedback and the HUD listen to.
    // Plain C#; HeldGun forwards the gun's, the repair flow's and the clip's events to it.
    public sealed class GunRepairFeedback
    {
        readonly GunDefinition _definition;
        readonly AudioService _audio;
        readonly GunJoltPlayer _jolt;
        readonly IPublisher<GunJamBeat> _beats;
        readonly IPublisher<GunRepairQteStarted> _qteStarted;
        readonly IPublisher<GunRepairCompleted> _completed;
        readonly IEquipmentOwner _owner;
        readonly ItemInstance _item;
        // Seconds until the "action snaps home" sound after a QTE success; negative = none due.
        float _mechanismIn = -1f;

        public GunRepairFeedback(GunDefinition definition, AudioService audio, GunJoltPlayer jolt, IPublisher<GunJamBeat> beats,
            IPublisher<GunRepairQteStarted> qteStarted, IPublisher<GunRepairCompleted> completed, IEquipmentOwner owner, ItemInstance item)
        {
            _definition = definition;
            _audio = audio;
            _jolt = jolt;
            _beats = beats;
            _qteStarted = qteStarted;
            _completed = completed;
            _owner = owner;
            _item = item;
        }

        RepairFeedbackSettings Feedback => _definition.Jam.Feedback;

        public void Jammed()
        {
            Play(_definition.Sounds.Jam);
            RepairFeedbackSettings feedback = Feedback;
            if (feedback != null)
                _jolt.Play(feedback.JamJolt);
            Beat(GunJamBeatKind.Jammed);
        }

        public void DryFired()
        {
            Play(_definition.Sounds.DryFire);
            RepairFeedbackSettings feedback = Feedback;
            if (feedback != null)
                _jolt.Play(feedback.DryFireJolt);
            Beat(GunJamBeatKind.DryFire);
        }

        // A slap or punch keyed in the repair clip landed.
        public void Impact(bool punch)
        {
            GunSounds sounds = _definition.Sounds;
            Play(punch && sounds.RepairPunch.IsValid() ? sounds.RepairPunch : sounds.RepairSlap);

            RepairFeedbackSettings feedback = Feedback;
            if (feedback != null)
                _jolt.Play(punch ? feedback.PunchJolt : feedback.SlapJolt);
            Beat(punch ? GunJamBeatKind.Punch : GunJamBeatKind.Slap);
        }

        public void QteStarted(IQteSession session) => _qteStarted?.Publish(new GunRepairQteStarted(_owner, _item, session, Feedback));

        public void Missed()
        {
            RepairFeedbackSettings feedback = Feedback;
            if (feedback != null)
            {
                Play(feedback.MissSound);
                Play(feedback.MissAccent);
                _jolt.Play(feedback.MissJolt);
            }
            Beat(GunJamBeatKind.Miss);
        }

        public void Repaired(bool qteSuccess)
        {
            RepairFeedbackSettings feedback = Feedback;
            if (feedback != null && qteSuccess)
            {
                Play(feedback.SuccessSound);
                Play(feedback.SuccessAccent);
                _mechanismIn = feedback.MechanismDelay;
                _jolt.Play(feedback.SuccessJolt);
            }
            else if (feedback != null)
                Play(feedback.NaturalSound);
            _completed?.Publish(new GunRepairCompleted(_owner, _item, qteSuccess));
        }

        public void Tick(float deltaTime)
        {
            if (_mechanismIn < 0f)
                return;
            _mechanismIn -= deltaTime;
            RepairFeedbackSettings feedback = Feedback;
            if (_mechanismIn < 0f && feedback != null)
                Play(feedback.SuccessMechanism);
        }

        public void Reset() => _mechanismIn = -1f;

        void Beat(GunJamBeatKind kind) => _beats?.Publish(new GunJamBeat(_owner, _item, kind));

        void Play(SoundID sound) => _audio?.Play(sound, _definition.Sounds.Volume);
    }
}
