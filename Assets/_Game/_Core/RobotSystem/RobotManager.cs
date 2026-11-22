using System;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Runs the robots on scaled time: night, raids (hide, then cheer or cry) and the player's comings and goings (a wave
    // goodbye, a welcome home) go to the roster and their states; a look greets, walking into one asks it to step aside, a
    // gun at its head makes it nervous, two free robots close together chat. Animates bodies and faces, thinks each Manual
    // tree every ThinkInterval, turns heads after the Animators, limits beeps.
    public sealed class RobotManager : ITickable, ILateTickable, IDisposable
    {
        readonly RobotRoster _roster;
        readonly RobotSettings _settings;
        readonly WorldClock _clock;
        readonly PlayerRegistry _players;
        readonly AudioService _audio;
        readonly RobotBeeps _beeps = new();
        readonly RobotHomecoming _homecoming = new();
        readonly IDisposable _subscriptions;
        IInteractable _lastFocus;
        bool _barrierBroken;
        int _nextId;

        public RobotManager(RobotRoster roster, RobotSettings settings, WorldClock clock, PlayerRegistry players, AudioService audio,
            ISubscriber<RaidStarted> raidStarted, ISubscriber<RaidEnded> raidEnded, ISubscriber<BarrierBroken> barrierBroken)
        {
            _roster = roster;
            _settings = settings;
            _clock = clock;
            _players = players;
            _audio = audio;
            _subscriptions = DisposableBag.Create(
                raidStarted.Subscribe(_ => StartRaid()),
                raidEnded.Subscribe(ended => EndRaid(ended.Reason)),
                barrierBroken.Subscribe(_ => _barrierBroken |= _roster.Raid));
        }

        public int NextId() => _nextId++;

        public void Dispose() => _subscriptions.Dispose();

        // A good night: the raid was beaten or outlasted with every fence standing. A bad one: the player fell or a barrier
        // broke. Ended by hand: no reaction.
        public static RobotCheer CheerFor(RaidEndReason reason, bool barrierBroken) => reason switch
        {
            RaidEndReason.Stopped => RobotCheer.None,
            RaidEndReason.Defeat => RobotCheer.Cry,
            _ => barrierBroken ? RobotCheer.Cry : RobotCheer.Win,
        };

        public void StartRaid()
        {
            _roster.Raid = true;
            _roster.RaidNumber++;
            _barrierBroken = false;
            for (int i = 0; i < _roster.Robots.Count; i++)
                _roster.Robots[i].Call.End();
        }

        public void EndRaid(RaidEndReason reason)
        {
            if (!_roster.Raid)
                return;
            _roster.Raid = false;
            RobotCheer cheer = CheerFor(reason, _barrierBroken);
            float now = Time.time;
            for (int i = 0; i < _roster.Robots.Count; i++)
                _roster.Robots[i].State.CheerFor(cheer, now);
        }

        // Every awake robot near the player that isn't busy comes over to say hello.
        public void WelcomeHome()
        {
            float now = Time.time;
            for (int i = 0; i < _roster.Robots.Count; i++)
            {
                Robot robot = _roster.Robots[i];
                if (Answers(robot, now, _settings.WelcomeRadius))
                    robot.State.Welcome();
            }
        }

        // The player walked out of the yard: every awake robot near them that isn't busy waves goodbye.
        public void SeeOff()
        {
            float now = Time.time;
            for (int i = 0; i < _roster.Robots.Count; i++)
            {
                Robot robot = _roster.Robots[i];
                if (Answers(robot, now, _settings.SeeOffRadius))
                    robot.State.SeeOff(now);
            }
        }

        bool Answers(Robot robot, float now, float radius) => !robot.Asleep && !robot.Call.Active && !robot.State.Hit(now)
            && FlatDistance(robot.transform.position, _roster.PlayerPosition) <= radius;

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
                focus = player.Interactor?.Focus;
                holdsGun = player.HeldItem is HeldGun;
                RobotYard yard = _roster.Yard;
                YardCrossing crossing = yard != null ? _homecoming.Update(yard.Contains(_roster.PlayerPosition), now, _settings.WelcomeAway) : YardCrossing.None;
                if (crossing == YardCrossing.Back && !_roster.Night && !_roster.Raid)
                    WelcomeHome();
                else if (crossing == YardCrossing.Left && !_roster.Raid)
                    SeeOff();
            }
            else
                _homecoming.Forget();

            for (int i = _roster.Robots.Count - 1; i >= 0; i--)
            {
                Robot robot = _roster.Robots[i];
                robot.Advance(deltaTime, now);
                if (hasPlayer)
                    React(robot, now, eyes, aim, holdsGun, ReferenceEquals(focus, robot));
                else
                    robot.HasLookTarget = false;

                if (now >= robot.NextThinkAt)
                {
                    robot.NextThinkAt = now + _settings.ThinkInterval;
                    robot.Think();
                }
            }
            StartChats(now);
            _lastFocus = focus;
        }

        // Two free robots close together start a chat (each pair checked; there are only a few robots at home).
        void StartChats(float now)
        {
            if (_roster.Raid)
                return;
            float reach = _settings.ChatRadius * _settings.ChatRadius;
            for (int i = 0; i < _roster.Robots.Count; i++)
            {
                Robot a = _roster.Robots[i];
                if (!FreeToChat(a, now))
                    continue;
                for (int j = i + 1; j < _roster.Robots.Count; j++)
                {
                    Robot b = _roster.Robots[j];
                    Vector3 between = b.transform.position - a.transform.position;
                    between.y = 0f;
                    if (between.sqrMagnitude > reach || !FreeToChat(b, now))
                        continue;
                    a.State.Chat(HeadOf(b), leads: true, now);
                    b.State.Chat(HeadOf(a), leads: false, now);
                    break;
                }
            }
        }

        // Awake and doing nothing it was asked to: not called, hit, greeted, nervous, cheering, welcoming, seeing the player
        // off, working or already chatting.
        static bool FreeToChat(Robot robot, float now)
        {
            RobotState state = robot.State;
            return state.CanChat(now) && !robot.Asleep && !robot.Call.Active && !state.Hit(now) && !state.Greeting(now)
                && !state.Nervous(now) && !state.Cheering(now) && !state.WelcomePending && !state.SeeingOff(now)
                && !state.Chatting(now) && !state.StepRequested(now) && (robot.Work == null || !robot.Work.WantsToWork(now));
        }

        static Vector3 HeadOf(Robot robot) => robot.Head != null ? robot.Head.position : robot.transform.position + Vector3.up * 1.1f;

        void React(Robot robot, float now, Vector3 eyes, Vector3 aim, bool holdsGun, bool lookedAt)
        {
            RobotState state = robot.State;
            Vector3 position = robot.transform.position;
            Vector3 feet = _roster.PlayerPosition;
            float distance = FlatDistance(feet, position);
            // Busy with something it can't break off for a wave or a frown: answering a call, a hit, a raid.
            bool busy = robot.Call.Active || state.Hit(now) || _roster.Raid;

            // A look greets an awake robot (a first look starts a fresh greeting; looking on keeps it going). A sleeping
            // one only wakes for E, a pat or the whistle.
            if (lookedAt && !busy && !robot.Asleep)
                state.Greet(now, fresh: !ReferenceEquals(_lastFocus, robot));

            // Walked into: step aside, even while waiting by the player it was called to (never pin them), but not mid-flinch.
            if (distance <= _settings.BumpRadius && !state.Hit(now))
                state.RequestStep(feet, now);

            Vector3 head = robot.Head != null ? robot.Head.position : position + Vector3.up * 1.1f;
            if (holdsGun && !busy && !robot.Asleep && !state.Nervous(now) && state.CanGetNervous(now) && distance <= _settings.NervousRadius
                && Vector3.Angle(aim, head - eyes) <= _settings.NervousAngle)
                state.MakeNervous(now);

            robot.HasLookTarget = distance <= _settings.NoticeRadius || state.Greeting(now) || robot.Call.Active || state.Nervous(now)
                || state.SeeingOff(now);
            robot.LookTarget = eyes;
            // Chatting, it looks at the other robot unless the player greets it.
            if (state.Chatting(now) && !state.Greeting(now))
            {
                robot.HasLookTarget = true;
                robot.LookTarget = state.ChatWith;
            }
        }

        public void LateTick()
        {
            if (!_roster.Enabled)
                return;
            float deltaTime = Time.deltaTime;
            float now = Time.time;
            for (int i = 0; i < _roster.Robots.Count; i++)
                _roster.Robots[i].TurnHead(deltaTime, now);
        }

        // Plays `sound` at the robot unless it (or any robot) beeped too recently.
        public bool TryBeep(Robot robot, RobotSound sound)
        {
            if (!_settings.Sounds.TryGetValue(sound, out SoundID id) || !_beeps.TryPlay(robot.Id, Time.time, _settings.BeepGap, _settings.SharedBeepGap))
                return false;
            _audio.PlayAt(id, robot.transform.position + Vector3.up);
            return true;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }
    }
}
