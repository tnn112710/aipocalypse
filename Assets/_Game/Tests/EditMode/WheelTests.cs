using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Input;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Core.UI;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Tests
{
    // The command wheel: which slice a direction picks, and that every way of closing it gives time and input back.
    public sealed class WheelTests
    {
        IObjectResolver _resolver;
        CommandWheelPresenter _wheel;
        InputService _input;
        TimeService _time;
        CursorLockMode _lockState;
        bool _cursorVisible;

        [SetUp]
        public void SetUp()
        {
            _lockState = Cursor.lockState;
            _cursorVisible = Cursor.visible;
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.RegisterInstance(new ControlSettings());
            builder.Register<InputService>(Lifetime.Singleton);
            builder.Register<TimeService>(Lifetime.Singleton);
            builder.Register<CommandWheelPresenter>(Lifetime.Singleton);
            _resolver = builder.Build();
            _input = _resolver.Resolve<InputService>();
            _time = _resolver.Resolve<TimeService>();
            _wheel = _resolver.Resolve<CommandWheelPresenter>();
            _input.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _resolver.Dispose();
            Time.timeScale = 1f;
            Cursor.lockState = _lockState;
            Cursor.visible = _cursorVisible;
        }

        static WheelSlice[] Slices(System.Action picked, bool secondEnabled = true) => new[]
        {
            new WheelSlice("A", null, true, false, picked),
            new WheelSlice("B", null, secondEnabled, false, picked),
            new WheelSlice("C", null, true, true, picked),
        };

        [Test]
        public void WheelMath_PicksSlicesClockwiseFromTop()
        {
            Assert.That(WheelMath.SliceAt(Vector2.up * 100f, 4, 30f), Is.EqualTo(0));
            Assert.That(WheelMath.SliceAt(Vector2.right * 100f, 4, 30f), Is.EqualTo(1));
            Assert.That(WheelMath.SliceAt(Vector2.down * 100f, 4, 30f), Is.EqualTo(2));
            Assert.That(WheelMath.SliceAt(Vector2.left * 100f, 4, 30f), Is.EqualTo(3));
            Assert.That(WheelMath.SliceAt(Vector2.up * 10f, 4, 30f), Is.EqualTo(-1), "inside the dead zone");
        }

        [Test]
        public void WheelMath_SixSlicesBoundaries()
        {
            // Six slices of 60°: slice 0 spans −30°..30° from the top, slice 1 30°..90° clockwise.
            static Vector2 At(float degrees) => new(Mathf.Sin(degrees * Mathf.Deg2Rad) * 100f, Mathf.Cos(degrees * Mathf.Deg2Rad) * 100f);
            Assert.That(WheelMath.SliceAt(At(29f), 6, 10f), Is.EqualTo(0));
            Assert.That(WheelMath.SliceAt(At(31f), 6, 10f), Is.EqualTo(1));
            Assert.That(WheelMath.SliceAt(At(-29f), 6, 10f), Is.EqualTo(0));
            Assert.That(WheelMath.SliceAt(At(-31f), 6, 10f), Is.EqualTo(5));
            Assert.That(WheelMath.SliceAt(At(179f), 6, 10f), Is.EqualTo(3));
        }

        [Test]
        public void Wheel_ReleasesTimeAndInputOnEveryClose()
        {
            int picked = 0;
            object owner = new();

            Assert.That(_wheel.TryOpen(owner, Slices(() => picked++), 0.3f), Is.True);
            Assert.That(_input.IsUIRequested, Is.True);
            Assert.That(_time.Scale, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(_wheel.Pick(0), Is.True);
            Assert.That(picked, Is.EqualTo(1));
            AssertReleased("after a pick");

            _wheel.TryOpen(owner, Slices(() => picked++), 0.3f);
            _wheel.Close();
            AssertReleased("after Close");

            _wheel.TryOpen(owner, Slices(() => picked++), 0.3f);
            _resolver.Resolve<IPublisher<PlayerDied>>().Publish(new PlayerDied(Vector3.zero, DamageType.Fall));
            AssertReleased("after the player died");
            Assert.That(picked, Is.EqualTo(1), "closing never picks");
        }

        [Test]
        public void Wheel_DisabledSliceCannotBePicked()
        {
            int picked = 0;
            _wheel.TryOpen(new object(), Slices(() => picked++, secondEnabled: false), 0.3f);
            Assert.That(_wheel.Pick(1), Is.False);
            Assert.That(_wheel.IsOpen, Is.True);
            Assert.That(picked, Is.EqualTo(0));
            _wheel.Close();
        }

        [Test]
        public void Wheel_DoesNotOpenOverAnotherMenu()
        {
            object menu = new();
            _input.RequestUI(menu);
            Assert.That(_wheel.TryOpen(new object(), Slices(() => { }), 0.3f), Is.False);
            _input.ReleaseUI(menu);
        }

        void AssertReleased(string when)
        {
            Assert.That(_wheel.IsOpen, Is.False, when);
            Assert.That(_input.IsUIRequested, Is.False, when);
            Assert.That(_time.Scale, Is.EqualTo(1f).Within(1e-4f), when);
        }
    }
}
