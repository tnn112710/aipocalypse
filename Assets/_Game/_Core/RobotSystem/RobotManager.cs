using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Runs the robots on scaled time: tells the roster about night and the player, starts greetings when the player looks
    // at a robot, asks a robot walked into to step aside, makes one with a gun pointed at its head nervous, animates
    // bodies and faces, thinks each robot's Manual tree every ThinkInterval, and after the Animators turns their heads.
    // Beeps go through TryBeep, which keeps them from piling up.
    public sealed class RobotManager : ITickable, ILateTickable
    {
        readonly RobotRoster _roster;
        readonly RobotSettings _settings;
        readonly WorldClock _clock;
        readonly PlayerRegistry _players;
        readonly AudioService _audio;
        readonly RobotBeeps _beeps;
        IInteractable _lastFocus;
        int _nextId;

        public RobotManager(RobotRoster roster, RobotSettings settings, WorldClock clock, PlayerRegistry players, AudioService audio)
        {
            _roster = roster;
            _settings = settings;
            _clock = clock;
            _players = players;
            _audio = audio;
            _beeps = new RobotBeeps(settings.BeepGap, settings.SharedBeepGap);
        }

        public int NextId() => _nextId++;

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            if (!_roster.Enabled)
                return;
            float now = Time.time;
            _roster.Night = !_settings.Awake.Contains(_clock.Now.Hour);

            IPlayer player = _players.Current;
            bool hasPlayer = player != null && !player.IsDead;
            _roster.HasPlayer = hasPlayer;
            IInteractable focus = null;
            Vector3 eyes = default;
            Vector3 aim = Vector3.forward;
            bool holdsGun = false;
            if (hasPlayer)
            {
                _roster.PlayerPosition = player.Transform.position;
                eyes = player.View.position;
                aim = player.View.forward;
                _roster.PlayerEyes = eyes;
                focus = player.Interactor?.Focus;
                holdsGun = player.HeldItem is HeldGun;
            }

            for (int i = _roster.Robots.Count - 1; i >= 0; i--)
            {
                Robot robot = _roster.Robots[i];
                robot.Advance(deltaTime, now);
                bool lookedAt = hasPlayer && ReferenceEquals(focus, robot);
                // A first look starts a fresh greeting; looking on keeps it going.
                if (lookedAt)
                    robot.Greet(now, fresh: !ReferenceEquals(_lastFocus, robot));
                robot.LookedAt = lookedAt;

                if (hasPlayer)
                    React(robot, now, eyes, aim, holdsGun);
                else
                    robot.HasLookTarget = false;

                if (now >= robot.NextThinkAt)
                {
                    robot.NextThinkAt = now + _settings.ThinkInterval;
                    robot.Think();
                }
            }
            _lastFocus = focus;
        }

        void React(Robot robot, float now, Vector3 eyes, Vector3 aim, bool holdsGun)
        {
            Vector3 position = robot.transform.position;
            Vector3 feet = _roster.PlayerPosition;
            Vector3 flat = feet - position;
            flat.y = 0f;
            float distance = flat.magnitude;

            // Walked into: step aside, even while waiting by the player it was called to (never pin them), but not mid-flinch.
            if (distance <= _settings.BumpRadius && !robot.Hit)
                robot.RequestStep(feet);

            Vector3 head = robot.Head != null ? robot.Head.position : position + Vector3.up * 1.1f;
            if (holdsGun && !robot.Asleep && !robot.Nervous && robot.CanGetNervous(now) && distance <= _settings.NervousRadius
                && Vector3.Angle(aim, head - eyes) <= _settings.NervousAngle)
                robot.MakeNervous(now);

            robot.HasLookTarget = distance <= _settings.NoticeRadius || robot.Greeting || robot.Call.Active || robot.Nervous;
            robot.LookTarget = eyes;
        }

        public void LateTick()
        {
            if (!_roster.Enabled)
                return;
            float deltaTime = Time.deltaTime;
            foreach (Robot robot in _roster.Robots)
                robot.TurnHead(deltaTime);
        }

        // Plays `sound` at the robot unless it (or any robot) beeped too recently.
        public bool TryBeep(Robot robot, RobotSound sound)
        {
            if (!_settings.Sounds.TryGetValue(sound, out SoundID id) || !_beeps.TryPlay(robot.Id, Time.time))
                return false;
            _audio.PlayAt(id, robot.transform.position + Vector3.up);
            return true;
        }
    }
}
