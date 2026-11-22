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
        public void Pet_LastsPetTime()
        {
            _state.Pet(1f);
            Assert.That(_state.Petting(1f + DogState.PetTime - 0.1f), Is.True);
            Assert.That(_state.Petting(1f + DogState.PetTime + 0.1f), Is.False);
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
                    InYard = true,
                    StayPoint = new Vector3(4f, 0f, 6f),
                };
                data.Set(DogSaveSection.Key, saved);
                Assert.That(data.TryGet(DogSaveSection.Key, out DogSaveSection.DogSave back), Is.True);
                Assert.That(back.Order, Is.EqualTo(DogOrder.Stay));
                Assert.That(back.Position, Is.EqualTo(saved.Position));
                Assert.That(back.Yaw, Is.EqualTo(45f));
                Assert.That(back.InYard, Is.True);
                Assert.That(back.StayPoint, Is.EqualTo(saved.StayPoint));

                _state.Restore(back.Order, back.StayPoint);
                Assert.That(_state.Order, Is.EqualTo(DogOrder.Stay));
                Assert.That(_state.StayPoint, Is.EqualTo(saved.StayPoint));
                Assert.That(_state.Sad(0f), Is.False, "a load isn't a fresh order");
            }
            finally
            {
                if (System.IO.Directory.Exists(saves.FolderPath))
                    System.IO.Directory.Delete(saves.FolderPath, true);
                Object.DestroyImmediate(settings);
            }
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
    }
}
