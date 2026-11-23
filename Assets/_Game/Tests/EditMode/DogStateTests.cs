using MB13.Aipocalypse.Core.DogSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class DogStateTests
    {
        DogSettings _settings;
        DogState _state;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<DogSettings>();
            _settings.SpamWhistles = 4;
            _settings.SpamWindow = 5f;
            _settings.AnnoyedTime = 6f;
            _settings.HeadTiltGap = 20f;
            _state = new DogState(_settings);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        [Test]
        public void Whistle_FourInFiveSecondsAnnoysThenIgnores()
        {
            Assert.That(_state.Whistle(0f, false), Is.EqualTo(DogWhistle.Come));
            Assert.That(_state.Whistle(1f, false), Is.EqualTo(DogWhistle.Come));
            Assert.That(_state.Whistle(2f, false), Is.EqualTo(DogWhistle.Come));
            Assert.That(_state.Whistle(3f, false), Is.EqualTo(DogWhistle.Annoyed));
            Assert.That(_state.Whistle(4f, false), Is.EqualTo(DogWhistle.Ignored));
            Assert.That(_state.Whistle(8.9f, false), Is.EqualTo(DogWhistle.Ignored));
            Assert.That(_state.Annoyed(8.9f), Is.True);
            Assert.That(_state.Annoyed(9.1f), Is.False);
            Assert.That(_state.Whistle(20f, false), Is.EqualTo(DogWhistle.Come));
        }

        [Test]
        public void Whistle_InAFightNeverCounts()
        {
            for (int i = 0; i < 10; i++)
                Assert.That(_state.Whistle(i * 0.5f, true), Is.EqualTo(DogWhistle.Come));
            Assert.That(_state.Whistle(5.1f, false), Is.EqualTo(DogWhistle.Come), "fight whistles left no streak");
        }

        [Test]
        public void Pet_ForgivesAnnoyance()
        {
            for (int i = 0; i < 4; i++)
                _state.Whistle(i, false);
            Assert.That(_state.Annoyed(4f), Is.True);
            _state.Pet(4f);
            Assert.That(_state.Annoyed(4f), Is.False);
            Assert.That(_state.Whistle(4.5f, false), Is.EqualTo(DogWhistle.Come));
        }

        [Test]
        public void HeadTilt_OncePer20Seconds()
        {
            Assert.That(_state.TryHeadTilt(0f), Is.True);
            Assert.That(_state.TryHeadTilt(19f), Is.False);
            Assert.That(_state.TryHeadTilt(20.1f), Is.True);
        }

        [Test]
        public void Settle_SitsThenLiesThenNaps()
        {
            Assert.That(DogState.SettlePose(2f, 60f, 3f, 15f), Is.EqualTo(DogPose.Stand));
            Assert.That(DogState.SettlePose(4f, 60f, 3f, 15f), Is.EqualTo(DogPose.Sit));
            Assert.That(DogState.SettlePose(18f, 60f, 3f, 15f), Is.EqualTo(DogPose.Lie));
            Assert.That(DogState.SettlePose(61f, 60f, 3f, 15f), Is.EqualTo(DogPose.Sleep));
        }

        [Test]
        public void Voice_GlobalGapBlocksBackToBack()
        {
            var voice = new DogVoice(_settings);
            Assert.That(voice.TryPlay(DogSound.Pant, 0f), Is.True);
            Assert.That(voice.TryPlay(DogSound.Sniff, _settings.VoiceGap - 0.1f), Is.False);
            Assert.That(voice.TryPlay(DogSound.Sniff, _settings.VoiceGap + 0.1f), Is.True);
        }

        [Test]
        public void Voice_BarkAtMostThreePerMinute()
        {
            var voice = new DogVoice(_settings);
            Assert.That(voice.TryPlay(DogSound.Bark, 0f), Is.True);
            Assert.That(voice.TryPlay(DogSound.Bark, 3f), Is.False, "bark gap");
            Assert.That(voice.TryPlay(DogSound.Bark, 6f), Is.True);
            Assert.That(voice.TryPlay(DogSound.Bark, 12f), Is.True);
            Assert.That(voice.TryPlay(DogSound.Bark, 18f), Is.False, "4th within a minute");
            Assert.That(voice.TryPlay(DogSound.Bark, 60.5f), Is.True, "the first left the minute");
        }

        [Test]
        public void Voice_YelpGapIsOneSecond()
        {
            var voice = new DogVoice(_settings);
            Assert.That(voice.TryPlay(DogSound.Yelp, 0f), Is.True);
            Assert.That(voice.TryPlay(DogSound.Yelp, 1.25f), Is.True, "only the global gap and the yelp gap apply");
            Assert.That(voice.TryPlay(DogSound.Whine, 2.5f), Is.True);
            Assert.That(voice.TryPlay(DogSound.Whine, 7f), Is.False, "whine gap 8 s");
        }

        [Test]
        public void Voice_BurstBarksSkipTheBarkGapButNotTheMinuteCap()
        {
            var voice = new DogVoice(_settings);
            Assert.That(voice.TryPlay(DogSound.Bark, 0f), Is.True);
            Assert.That(voice.TryPlay(DogSound.Bark, 1.3f), Is.False, "a lone bark keeps the bark gap");
            Assert.That(voice.TryPlay(DogSound.Bark, 1.3f, burst: true), Is.True, "the second of a burst");
            Assert.That(voice.TryPlay(DogSound.Bark, 2.6f, burst: true), Is.True, "the third");
            Assert.That(voice.TryPlay(DogSound.Bark, 3.9f, burst: true), Is.False, "still three a minute");
            Assert.That(voice.TryPlay(DogSound.Whine, 3f), Is.False, "the global gap still applies");
        }

        [Test]
        public void Call_LastsUntilEndedOrTimedOut()
        {
            _state.Call(0f, annoyed: false);
            Assert.That(_state.Called(5f), Is.True);
            Assert.That(_state.CallAnnoyed, Is.False);
            _state.EndCall();
            Assert.That(_state.Called(5f), Is.False);
            _state.Call(10f, annoyed: true);
            Assert.That(_state.CallAnnoyed, Is.True);
            Assert.That(_state.Called(10f + DogState.CallTimeout + 0.1f), Is.False, "gives up");
        }

        [Test]
        public void Stay_IsSadForAMomentFollowIsNot()
        {
            _state.SetOrder(DogOrder.Stay, Vector3.one, 2f);
            Assert.That(_state.Sad(3f), Is.True);
            Assert.That(_state.Sad(2f + DogState.SadTime + 0.1f), Is.False);
            _state.SetOrder(DogOrder.Follow, Vector3.zero, 20f);
            Assert.That(_state.Sad(20.5f), Is.False);
        }

        [Test]
        public void Pet_GivesItTimeToComeThenLastsPetTime()
        {
            _state.Pet(1f);
            Assert.That(_state.Petting(1f + DogState.PetComeTime - 0.1f), Is.True, "still coming over (getting up takes a while)");
            Assert.That(_state.Petting(1f + DogState.PetComeTime + 0.1f), Is.False, "never arrived: gives up");
            _state.Pet(20f);
            _state.PetArrived(23f);
            Assert.That(_state.Petting(23f + DogState.PetTime - 0.1f), Is.True);
            Assert.That(_state.Petting(23f + DogState.PetTime + 0.1f), Is.False);
        }

        [Test]
        public void Save_RoundTripsOrderPositionAndStayPoint()
        {
            var settings = ScriptableObject.CreateInstance<MB13.Aipocalypse.Core.SaveSystem.SaveSettings>();
            settings.Folder = "DogSaveTests_" + System.Guid.NewGuid().ToString("N");
            var saves = new MB13.Aipocalypse.Core.SaveSystem.SaveService(settings);
            try
            {
                var data = saves.Create();
                var saved = new DogSaveSection.DogSave
                {
                    Order = DogOrder.Stay,
                    Position = new Vector3(1f, 2f, 3f),
                    Yaw = 45f,
                    StayPoint = new Vector3(4f, 0f, 6f),
                };
                data.Set(DogSaveSection.Key, saved);
                Assert.That(data.TryGet(DogSaveSection.Key, out DogSaveSection.DogSave back), Is.True);
                Assert.That(back.Order, Is.EqualTo(DogOrder.Stay));
                Assert.That(back.Position, Is.EqualTo(saved.Position));
                Assert.That(back.Yaw, Is.EqualTo(45f));
                Assert.That(back.StayPoint, Is.EqualTo(saved.StayPoint));

                _state.Restore(back.Order, back.StayPoint, back.HerdFor, back.HowledNight, 0f);
                Assert.That(_state.Order, Is.EqualTo(DogOrder.Stay));
                Assert.That(_state.StayPoint, Is.EqualTo(saved.StayPoint));
                Assert.That(_state.Sad(0f), Is.False, "a load isn't a fresh order");

                var fight = new DogSaveSection.DogSave { Stance = DogStance.Passive, Toughness = 42f, Down = false, LimpLeft = 12f };
                data.Set(DogSaveSection.Key, fight);
                data.TryGet(DogSaveSection.Key, out DogSaveSection.DogSave fightBack);
                _state.RestoreFight(fightBack.Stance, fightBack.Toughness, fightBack.Down, fightBack.LimpLeft, 100f);
                Assert.That(_state.Stance, Is.EqualTo(DogStance.Passive));
                Assert.That(_state.Toughness, Is.EqualTo(42f));
                Assert.That(_state.Limping(111f), Is.True);
                Assert.That(_state.Limping(112.5f), Is.False);
            }
            finally
            {
                if (System.IO.Directory.Exists(saves.FolderPath))
                    System.IO.Directory.Delete(saves.FolderPath, true);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Herding_GreyedWhenNothingOut()
        {
            Assert.That(DogState.CanHerd(0), Is.False);
            Assert.That(DogState.CanHerd(2), Is.True);
        }

        [Test]
        public void Herding_PicksFarthestAndSkipsStuckOrHome()
        {
            var animals = new[]
            {
                new HerdView(0, new Vector3(10f, 0f, 0f), 1f, false, 0f),
                new HerdView(1, new Vector3(30f, 0f, 0f), 1f, false, 25f),
                new HerdView(2, new Vector3(50f, 0f, 0f), 1f, true, 0f),
                new HerdView(3, new Vector3(20f, 0f, 0f), 1f, false, 0f),
            };
            Assert.That(DogState.PickHerdTarget(animals, Vector3.zero), Is.EqualTo(3), "farthest out that isn't home or stuck");
        }

        [Test]
        public void Herding_EndsWhenAllHomeOrAfterThreeMinutes()
        {
            Assert.That(DogState.HerdDone(14, 14, 0f, 10f), Is.True);
            Assert.That(DogState.HerdDone(10, 14, 0f, 179f), Is.False);
            Assert.That(DogState.HerdDone(10, 14, 0f, 181f), Is.True);
        }

        [Test]
        public void SheepChase_EveryThreeToSixMinutesByDayAtHome()
        {
            Assert.That(_state.TryStartSheepChase(0f, true, true, false, 0f), Is.False, "the first one is 3 min away");
            Assert.That(_state.TryStartSheepChase(181f, false, true, false, 0f), Is.False, "never at night");
            Assert.That(_state.TryStartSheepChase(181f, true, true, true, 0f), Is.False, "never while herding");
            Assert.That(_state.TryStartSheepChase(181f, true, true, false, 0f), Is.True);
            Assert.That(_state.TryStartSheepChase(200f, true, true, false, 0f), Is.False);
        }

        [Test]
        public void Howl_OncePerNightNotInRaid()
        {
            Assert.That(_state.TryHowl(3, 23f, true, true, 0f), Is.False, "raid");
            Assert.That(_state.TryHowl(3, 21f, true, false, 0f), Is.False, "too early");
            Assert.That(_state.TryHowl(3, 23f, true, false, 0.1f), Is.True);
            Assert.That(_state.TryHowl(4, 1f, true, false, 0.1f), Is.False, "the same night (after midnight)");
            Assert.That(_state.TryHowl(4, 23f, true, false, 0.5f), Is.False, "a failed roll uses the night up");
            Assert.That(_state.TryHowl(5, 23f, true, false, 0.1f), Is.True);
        }

        [Test]
        public void Howl_ANightUsedBeforeASaveIsNotRolledAgain()
        {
            Assert.That(_state.TryHowl(3, 23f, true, false, 0.9f), Is.False, "the roll failed: night 3 is used");
            var loaded = new DogState(_settings);
            loaded.Restore(DogOrder.Stay, Vector3.zero, 0f, _state.HowledNight, 0f);
            Assert.That(loaded.TryHowl(3, 23.5f, true, false, 0.1f), Is.False);
            Assert.That(loaded.TryHowl(4, 23f, true, false, 0.1f), Is.True);
        }

        [Test]
        public void Herd_ALoadKeepsTheTimeItHadLeft()
        {
            _state.StartHerding(Vector3.zero, 10f);
            float herdFor = _state.HerdingFor(70f);
            Assert.That(herdFor, Is.EqualTo(60f).Within(1e-4f));
            var loaded = new DogState(_settings);
            loaded.Restore(DogOrder.Herding, Vector3.zero, herdFor, -1, 5f);
            Assert.That(loaded.Order, Is.EqualTo(DogOrder.Herding));
            Assert.That(DogState.HerdDone(0, 4, loaded.HerdStartedAt, 5f), Is.False, "not over at once after a load");
            Assert.That(DogState.HerdDone(0, 4, loaded.HerdStartedAt, 5f + DogState.HerdGiveUp - 60f + 0.1f), Is.True);
            Assert.That(_state.HerdingFor(70f), Is.EqualTo(60f).Within(1e-4f));
            _state.SetOrder(DogOrder.Stay, Vector3.zero);
            Assert.That(_state.HerdingFor(70f), Is.Zero, "not herding");
        }

        [Test]
        public void SetOrder_StayRemembersThePoint()
        {
            Assert.That(_state.Order, Is.EqualTo(DogOrder.Follow));
            var at = new Vector3(4f, 0f, -2f);
            _state.SetOrder(DogOrder.Stay, at);
            Assert.That(_state.Order, Is.EqualTo(DogOrder.Stay));
            Assert.That(_state.StayPoint, Is.EqualTo(at));
            _state.Reset();
            Assert.That(_state.Order, Is.EqualTo(DogOrder.Follow));
        }

        // ---- Turning on the spot, rain, welcome home ----

        void TurnSettings()
        {
            _settings.TurnStepSpeed = 0.6f;
            _settings.TurnMinRate = 50f;
            _settings.TurnStillBelow = 0.25f;
        }

        [Test]
        public void Turn_StandingTurnPlaysASlowWalk()
        {
            TurnSettings();
            Assert.That(DogTurn.AnimatorSpeed(0f, 120f, true, _settings), Is.EqualTo(0.6f));
            Assert.That(DogTurn.AnimatorSpeed(0.1f, -120f, true, _settings), Is.EqualTo(0.6f), "either way round");
        }

        [Test]
        public void Turn_WalkingSlowTurnOrNotOnItsFeetKeepsItsSpeed()
        {
            TurnSettings();
            Assert.That(DogTurn.AnimatorSpeed(2f, 120f, true, _settings), Is.EqualTo(2f), "already walking");
            Assert.That(DogTurn.AnimatorSpeed(0f, 20f, true, _settings), Is.EqualTo(0f), "a slow turn");
            Assert.That(DogTurn.AnimatorSpeed(0f, 120f, false, _settings), Is.EqualTo(0f), "sitting, lying or swimming");
        }

        [Test]
        public void Wetness_ShakesOnceOutOfTheRainAfterASoaking()
        {
            _settings.WetToShake = 20f;
            var wet = new DogWetness(_settings);
            for (int i = 0; i < 25; i++)
                Assert.That(wet.Tick(1f, rainedOn: true), Is.False, "never while still in the rain");
            Assert.That(wet.Tick(0.5f, rainedOn: false), Is.True);
            Assert.That(wet.Tick(0.5f, rainedOn: false), Is.False, "once");
        }

        [Test]
        public void Wetness_BriefRainDriesOffWithoutAShake()
        {
            _settings.WetToShake = 20f;
            var wet = new DogWetness(_settings);
            for (int i = 0; i < 15; i++)
                wet.Tick(1f, rainedOn: true);
            for (int i = 0; i < 20; i++)
                Assert.That(wet.Tick(1f, rainedOn: false), Is.False);
            for (int i = 0; i < 10; i++)
                wet.Tick(1f, rainedOn: true);
            Assert.That(wet.Tick(1f, rainedOn: false), Is.False, "15 s, dried off, then 10 s: not soaked");
        }

        void WelcomeSettings()
        {
            _settings.WelcomeAfter = 60f;
            _settings.WelcomeTime = 15f;
        }

        [Test]
        public void Welcome_AfterThePlayerWasAwayLongEnough()
        {
            WelcomeSettings();
            _state.TrackPlayer(playerInYard: true, stayingHome: true, now: 0f);
            _state.TrackPlayer(false, true, 10f);
            _state.TrackPlayer(false, true, 80f);
            Assert.That(_state.Welcoming(80f), Is.False, "still away");
            _state.TrackPlayer(true, true, 90f);
            Assert.That(_state.Welcoming(91f), Is.True);
            Assert.That(_state.Welcoming(106f), Is.False, "over after WelcomeTime");
        }

        [Test]
        public void Welcome_NotAfterAShortTrip()
        {
            WelcomeSettings();
            _state.TrackPlayer(false, true, 10f);
            _state.TrackPlayer(true, true, 40f);
            Assert.That(_state.Welcoming(41f), Is.False);
        }

        [Test]
        public void Welcome_OnlyWhileStayingAtHome()
        {
            WelcomeSettings();
            _state.TrackPlayer(false, stayingHome: false, now: 10f);
            _state.TrackPlayer(true, false, 200f);
            Assert.That(_state.Welcoming(201f), Is.False, "following: it was with the player");
            _state.TrackPlayer(false, true, 300f);
            _state.TrackPlayer(false, false, 330f);
            _state.TrackPlayer(true, true, 400f);
            Assert.That(_state.Welcoming(401f), Is.False, "away time restarts once it stops staying home");
        }

        [Test]
        public void Welcome_CalledOffWhenItStopsStayingHome()
        {
            WelcomeSettings();
            _state.TrackPlayer(false, true, 10f);
            _state.TrackPlayer(true, true, 200f);
            Assert.That(_state.Welcoming(201f), Is.True);
            _state.TrackPlayer(true, false, 202f);
            _state.TrackPlayer(true, true, 205f);
            Assert.That(_state.Welcoming(206f), Is.False, "told to follow, then to stay again: no second welcome");
        }

        [Test]
        public void Welcome_DoneEndsIt()
        {
            WelcomeSettings();
            _state.TrackPlayer(false, true, 0f);
            _state.TrackPlayer(true, true, 100f);
            Assert.That(_state.Welcoming(101f), Is.True);
            _state.WelcomeDone();
            Assert.That(_state.Welcoming(102f), Is.False);
        }
    }
}
