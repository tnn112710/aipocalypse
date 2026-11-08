using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class GunTests
    {
        const float Frame = 1f / 60f;

        GunDefinition _definition;
        AmmoType _rifleAmmo;
        AmmoType _pistolAmmo;
        AmmoReserve _reserve;
        int _shots;

        [SetUp]
        public void SetUp()
        {
            _rifleAmmo = ScriptableObject.CreateInstance<AmmoType>();
            _pistolAmmo = ScriptableObject.CreateInstance<AmmoType>();
            _definition = ScriptableObject.CreateInstance<GunDefinition>();
            _definition.Fire.Mode = FireMode.Auto;
            _definition.Fire.RoundsPerMinute = 600f;
            _definition.Magazine.AmmoType = _rifleAmmo;
            _definition.Magazine.Size = 30;
            _definition.Magazine.ReloadTime = 1f;
            _definition.Jam.Chance = 0f;
            _definition.Jam.ClearTime = 1f;
            _reserve = new AmmoReserve();
            _shots = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_rifleAmmo);
            Object.DestroyImmediate(_pistolAmmo);
        }

        Gun NewGun(float jamRoll = 1f)
        {
            var gun = new Gun(new GunInstance(_definition), _reserve, () => jamRoll);
            gun.Fired += () => _shots++;
            return gun;
        }

        static GunInput Trigger(bool held, bool pressed = false) => new()
        {
            TriggerHeld = held,
            TriggerPressed = pressed,
            IsReady = true,
            CanFire = true,
            CanAim = true,
        };

        static void Run(Gun gun, GunInput input, float seconds, float deltaTime = Frame)
        {
            int frames = Mathf.RoundToInt(seconds / deltaTime);
            for (int i = 0; i < frames; i++)
                gun.Tick(input, deltaTime);
        }

        [Test]
        public void Auto_FiresAtItsRate_AtAnyFrameRate()
        {
            Run(NewGun(), Trigger(true), 1f);
            Assert.AreEqual(10, _shots, 1);

            _shots = 0;
            Run(NewGun(), Trigger(true), 2f, 1f / 7f);
            Assert.AreEqual(20, _shots, 2);
        }

        [Test]
        public void SemiAuto_OneShotPerClick_AndClicksCantBeatTheFireRate()
        {
            _definition.Fire.Mode = FireMode.SemiAuto;
            _definition.Fire.RoundsPerMinute = 60f;
            Gun gun = NewGun();

            gun.Tick(Trigger(true, true), Frame);
            Assert.AreEqual(1, _shots);

            gun.Tick(Trigger(true, true), Frame);
            Run(gun, Trigger(true), 0.5f);
            Assert.AreEqual(1, _shots, "second click waits for the fire rate");

            Run(gun, Trigger(true), 0.6f);
            Assert.AreEqual(2, _shots, "the buffered click fires once the rate allows");

            Run(gun, Trigger(true), 2f);
            Assert.AreEqual(2, _shots, "holding doesn't fire again");
        }

        [Test]
        public void SingleBurst_FiresOneBurstPerClick()
        {
            _definition.Fire.Mode = FireMode.SingleBurst;
            _definition.Fire.BurstCount = 3;
            Gun gun = NewGun();

            gun.Tick(Trigger(true, true), Frame);
            Run(gun, Trigger(false), 1f);

            Assert.AreEqual(3, _shots);
        }

        [Test]
        public void AutoBurst_PausesBetweenBursts()
        {
            _definition.Fire.Mode = FireMode.AutoBurst;
            _definition.Fire.BurstCount = 3;
            _definition.Fire.BurstCooldown = 0.5f;

            // Bursts at 0 / 0.1 / 0.2 s, then 0.7 / 0.8 / 0.9 s.
            Run(NewGun(), Trigger(true), 1f);

            Assert.AreEqual(6, _shots);
        }

        [Test]
        public void Firing_UsesRounds_ThenAutoReloadsFromTheReserve()
        {
            _definition.Magazine.Size = 5;
            _reserve.Add(_rifleAmmo, 12);
            Gun gun = NewGun();

            Run(gun, Trigger(true), 1f);
            Assert.AreEqual(5, _shots);
            Assert.AreEqual(GunAction.Reloading, gun.Action);

            Run(gun, Trigger(false), 1.05f);
            Assert.AreEqual(GunAction.None, gun.Action);
            Assert.AreEqual(5, gun.Item.Rounds);
            Assert.AreEqual(7, _reserve.Count(_rifleAmmo));
        }

        [Test]
        public void Reload_TakesOnlyWhatTheReserveHas()
        {
            _reserve.Add(_rifleAmmo, 4);
            Gun gun = NewGun();
            gun.Item.SetRounds(0);

            Run(gun, Trigger(false), 1.05f);

            Assert.AreEqual(4, gun.Item.Rounds);
            Assert.AreEqual(0, _reserve.Count(_rifleAmmo));
            Assert.IsFalse(gun.CanReload);
        }

        [Test]
        public void Reload_NeedsAKey_UnlessEmpty()
        {
            _reserve.Add(_rifleAmmo, 30);
            Gun gun = NewGun();
            gun.Item.SetRounds(10);

            gun.Tick(Trigger(false), Frame);
            Assert.AreEqual(GunAction.None, gun.Action);

            gun.Tick(new GunInput { Reload = true, IsReady = true, CanFire = true, CanAim = true }, Frame);
            Assert.AreEqual(GunAction.Reloading, gun.Action);
        }

        [Test]
        public void PerRoundReload_LoadsOneRoundAtATime()
        {
            _definition.Magazine.Size = 5;
            _definition.Magazine.Reload = ReloadType.PerRound;
            _definition.Magazine.ReloadTime = 0.5f;
            _reserve.Add(_rifleAmmo, 10);
            Gun gun = NewGun();
            gun.Item.SetRounds(2);
            int loaded = 0;
            gun.RoundLoaded += () => loaded++;

            gun.Tick(new GunInput { Reload = true, IsReady = true }, Frame);
            Run(gun, Trigger(false), 0.55f);
            Assert.AreEqual(3, gun.Item.Rounds);
            Assert.AreEqual(GunAction.Reloading, gun.Action);

            Run(gun, Trigger(false), 1f);
            Assert.AreEqual(5, gun.Item.Rounds);
            Assert.AreEqual(3, loaded);
            Assert.AreEqual(GunAction.None, gun.Action);
        }

        [Test]
        public void PerRoundReload_Firing_StopsAfterTheRoundGoingIn_ThenFires()
        {
            _definition.Fire.Mode = FireMode.SemiAuto;
            _definition.Magazine.Size = 5;
            _definition.Magazine.Reload = ReloadType.PerRound;
            _definition.Magazine.ReloadTime = 0.5f;
            _reserve.Add(_rifleAmmo, 10);
            Gun gun = NewGun();
            gun.Item.SetRounds(1);
            bool? finished = null;
            gun.ActionEnded += (_, done) => finished = done;

            gun.Tick(new GunInput { Reload = true, IsReady = true }, Frame);
            Run(gun, Trigger(false), 0.7f);
            Assert.AreEqual(2, gun.Item.Rounds);

            gun.Tick(Trigger(true, pressed: true), Frame);
            Run(gun, Trigger(false), 0.2f);
            Assert.AreEqual(GunAction.Reloading, gun.Action, "the round going in finishes first");
            Assert.AreEqual(0, _shots);

            Run(gun, Trigger(false), 0.15f);
            Assert.AreEqual(true, finished);
            Assert.AreEqual(1, _shots, "the click fires once the reload stops");
            Assert.AreEqual(2, gun.Item.Rounds);
            Assert.AreEqual(GunAction.None, gun.Action);
        }

        [Test]
        public void PerRoundReload_FromEmpty_ClickFiresAfterTheFirstRound()
        {
            _definition.Fire.Mode = FireMode.SemiAuto;
            _definition.Magazine.Size = 5;
            _definition.Magazine.Reload = ReloadType.PerRound;
            _definition.Magazine.ReloadTime = 0.5f;
            _definition.Magazine.AutoReload = false;
            _reserve.Add(_rifleAmmo, 10);
            Gun gun = NewGun();
            gun.Item.SetRounds(0);
            gun.Tick(new GunInput { Reload = true, IsReady = true }, Frame);

            gun.Tick(Trigger(true, pressed: true), Frame);
            Assert.AreEqual(GunAction.Reloading, gun.Action);
            Assert.AreEqual(0, _shots);

            Run(gun, Trigger(false), 0.55f);
            Assert.AreEqual(1, _shots);
            Assert.AreEqual(0, gun.Item.Rounds);
            Assert.AreEqual(GunAction.None, gun.Action);
        }

        [Test]
        public void Pump_WaitsForTheCycle_ThenFiresABufferedClick()
        {
            _definition.Fire.Mode = FireMode.SemiAuto;
            _definition.Fire.RoundsPerMinute = 600f;
            _definition.Fire.CycleTime = 0.5f;
            Gun gun = NewGun();

            gun.Tick(Trigger(true, pressed: true), Frame);
            Assert.AreEqual(1, _shots);
            Assert.AreEqual(GunAction.Cycling, gun.Action);

            Run(gun, Trigger(false), 0.2f);
            gun.Tick(Trigger(true, pressed: true), Frame);
            Run(gun, Trigger(false), 0.2f);
            Assert.AreEqual(1, _shots, "no shot while the pump is worked");

            Run(gun, Trigger(false), 0.15f);
            Assert.AreEqual(2, _shots, "the click made during the pump fires once it's done");
        }

        [Test]
        public void Pump_KeepsTheAim_AndSkipsTheCycleAfterTheLastRound()
        {
            _definition.Fire.Mode = FireMode.SemiAuto;
            _definition.Fire.CycleTime = 0.5f;
            _definition.Magazine.AutoReload = false;
            Gun gun = NewGun();
            gun.Item.SetRounds(2);
            var aimFire = new GunInput { Aim = true, TriggerHeld = true, TriggerPressed = true, IsReady = true, CanFire = true, CanAim = true };

            gun.Tick(aimFire, Frame);
            gun.Tick(new GunInput { Aim = true, IsReady = true, CanFire = true, CanAim = true }, Frame);
            Assert.AreEqual(GunAction.Cycling, gun.Action);
            Assert.IsTrue(gun.IsAiming);

            Run(gun, Trigger(false), 0.6f);
            gun.Tick(aimFire, Frame);

            Assert.AreEqual(2, _shots);
            Assert.AreEqual(0, gun.Item.Rounds);
            Assert.AreEqual(GunAction.None, gun.Action);
        }

        [Test]
        public void Pump_InterruptedBySwitching_FinishesWhenRaisedAgain()
        {
            _definition.Fire.Mode = FireMode.SemiAuto;
            _definition.Fire.CycleTime = 0.5f;
            Gun gun = NewGun();

            gun.Tick(Trigger(true, pressed: true), Frame);
            gun.Tick(new GunInput { IsReady = false }, 2f);
            Assert.AreEqual(GunAction.None, gun.Action);

            gun.Tick(Trigger(false), Frame);
            Assert.AreEqual(GunAction.Cycling, gun.Action);
        }

        [Test]
        public void Jam_StopsTheShot_ThenClearsItself()
        {
            _definition.Jam.Chance = 0.5f;
            Gun gun = NewGun(jamRoll: 0f);

            gun.Tick(Trigger(true), Frame);
            Assert.AreEqual(0, _shots);
            Assert.IsTrue(gun.Item.IsJammed);
            Assert.AreEqual(30, gun.Item.Rounds, "a jammed shot uses no round");

            gun.Tick(Trigger(true), Frame);
            Assert.AreEqual(GunAction.ClearingJam, gun.Action);

            Run(gun, Trigger(false), 1.05f);
            Assert.IsFalse(gun.Item.IsJammed);
            Assert.AreEqual(GunAction.None, gun.Action);
        }

        [Test]
        public void Switching_InterruptsTheReload()
        {
            _reserve.Add(_rifleAmmo, 30);
            Gun gun = NewGun();
            gun.Item.SetRounds(0);
            gun.Tick(Trigger(false), Frame);
            Assert.AreEqual(GunAction.Reloading, gun.Action);

            gun.Tick(new GunInput { IsReady = false }, 2f);

            Assert.AreEqual(GunAction.None, gun.Action);
            Assert.AreEqual(0, gun.Item.Rounds);
        }

        [Test]
        public void Sprinting_BlocksFireAndAim()
        {
            Gun gun = NewGun();

            Run(gun, new GunInput { TriggerHeld = true, Aim = true, IsReady = true }, 0.5f);

            Assert.AreEqual(0, _shots);
            Assert.IsFalse(gun.IsAiming);
        }

        [Test]
        public void Reloading_DropsTheAim()
        {
            _reserve.Add(_rifleAmmo, 30);
            Gun gun = NewGun();
            var aim = new GunInput { Aim = true, IsReady = true, CanFire = true, CanAim = true };
            gun.Tick(aim, Frame);
            Assert.IsTrue(gun.IsAiming);

            gun.Item.SetRounds(0);
            gun.Tick(aim, Frame);

            Assert.AreEqual(GunAction.Reloading, gun.Action);
            Assert.IsFalse(gun.IsAiming);
        }

        [Test]
        public void Reserve_IsSharedByAmmoType()
        {
            _reserve.Add(_rifleAmmo, 10);
            _reserve.Add(_pistolAmmo, 3);
            var other = new GunInstance(_definition);
            other.SetRounds(0);
            var otherGun = new Gun(other, _reserve, () => 1f);
            Gun gun = NewGun();
            gun.Item.SetRounds(25);

            Run(gun, new GunInput { Reload = true, IsReady = true }, 1.05f);
            Run(otherGun, Trigger(false), 1.05f);

            Assert.AreEqual(30, gun.Item.Rounds);
            Assert.AreEqual(5, other.Rounds);
            Assert.AreEqual(0, _reserve.Count(_rifleAmmo));
            Assert.AreEqual(3, _reserve.Count(_pistolAmmo));
        }

        [Test]
        public void InfiniteReserve_NeverRunsDown()
        {
            _reserve.Infinite = true;
            Gun gun = NewGun();
            gun.Item.SetRounds(0);

            Run(gun, Trigger(false), 1.05f);

            Assert.AreEqual(30, gun.Item.Rounds);
            Assert.AreEqual(0, _reserve.Count(_rifleAmmo));
        }

        [Test]
        public void Recoil_KicksTheViewUp_ThenSettles()
        {
            var recoil = new GunRecoil(_definition.Recoil);

            recoil.Kick(0f, 1f);
            for (int i = 0; i < 6; i++)
                recoil.Tick(Frame, 0f);
            Assert.Less(recoil.ViewEuler.x, -0.5f);
            Assert.Less(recoil.GunEuler.x, 0f);
            Assert.Less(Mathf.Abs(recoil.GunEuler.x), Mathf.Abs(recoil.ViewEuler.x), "the gun's extra kick stays smaller than the aim kick");

            for (int i = 0; i < 180; i++)
                recoil.Tick(Frame, 0f);
            Assert.AreEqual(0f, recoil.ViewEuler.magnitude, 0.01f);
        }

        [Test]
        public void Recoil_KeepsItsShareAsALookTurn_TheRestSpringsBack()
        {
            _definition.Recoil.KeptShare = 0.4f;
            _definition.Recoil.Hip.NoisePitch = 0f;
            var recoil = new GunRecoil(_definition.Recoil);

            recoil.Kick(0f, 1f);
            Vector2 turned = Vector2.zero;
            for (int i = 0; i < 240; i++)
            {
                recoil.Tick(Frame, 0f);
                turned += recoil.LookTurn;
            }

            Assert.AreEqual(0f, recoil.ViewEuler.magnitude, 0.01f, "the springing part settles");
            Assert.Less(turned.x, -_definition.Recoil.Hip.Vertical * 0.4f * 0.5f, "the kept part turned the look up");
            Assert.Greater(turned.x, -_definition.Recoil.Hip.Vertical * 0.4f * 2f);
        }

        [Test]
        public void Recoil_NoKeptShare_NeverTurnsTheLook()
        {
            _definition.Recoil.KeptShare = 0f;
            var recoil = new GunRecoil(_definition.Recoil);

            recoil.Kick(0f, 1f);
            for (int i = 0; i < 30; i++)
            {
                recoil.Tick(Frame, 0f);
                Assert.AreEqual(Vector2.zero, recoil.LookTurn);
            }
        }

        [Test]
        public void RecoilValues_BlendBetweenHipAndAim()
        {
            var hip = new RecoilValues { Vertical = 4f, VerticalReturnSpeed = 10f };
            var aim = new RecoilValues { Vertical = 2f, VerticalReturnSpeed = 20f };
            var blend = new RecoilValues();

            RecoilValues.Lerp(hip, aim, 0.5f, blend);

            Assert.AreEqual(3f, blend.Vertical, 0.0001f);
            Assert.AreEqual(15f, blend.VerticalReturnSpeed, 0.0001f);
        }

        [Test]
        public void Falloff_FullUpToStart_EasesToTheMinimumAtEnd()
        {
            var falloff = new DamageFalloff { Start = 20f, End = 60f, MinShare = 0.4f };

            Assert.AreEqual(1f, falloff.At(0f));
            Assert.AreEqual(1f, falloff.At(20f));
            Assert.AreEqual(0.7f, falloff.At(40f), 0.0001f);
            Assert.AreEqual(0.4f, falloff.At(60f), 0.0001f);
            Assert.AreEqual(0.4f, falloff.At(500f), 0.0001f);
            Assert.Greater(falloff.At(30f), falloff.At(50f));
        }

        [Test]
        public void MuzzleToCrosshair_PointsFromTheMuzzleAtTheAimPoint()
        {
            var muzzle = new Vector3(0.2f, -0.2f, 0.5f);
            var aimPoint = new Vector3(0f, 0f, 20f);

            Vector3 direction = GunAim.MuzzleToPoint(muzzle, Vector3.forward, aimPoint);

            Assert.AreEqual(0f, Vector3.Angle(direction, aimPoint - muzzle), 0.01f);
        }

        [Test]
        public void MuzzleToCrosshair_UsesTheEyeDirection_WhenThePointIsTooCloseOrBehind()
        {
            var muzzle = new Vector3(0.2f, -0.2f, 0.5f);

            Assert.AreEqual(Vector3.forward, GunAim.MuzzleToPoint(muzzle, Vector3.forward, new Vector3(0f, 0f, 0.8f)));
            Assert.AreEqual(Vector3.forward, GunAim.MuzzleToPoint(muzzle, Vector3.forward, new Vector3(0f, 0f, -5f)));
        }

        [Test]
        public void Spread_StaysInsideTheCone()
        {
            for (int i = 0; i < 200; i++)
            {
                Vector3 direction = BulletSpread.Apply(Vector3.forward, 5f);
                Assert.LessOrEqual(Vector3.Angle(Vector3.forward, direction), 5.01f);
            }
            Assert.AreEqual(Vector3.forward, BulletSpread.Apply(Vector3.forward, 0f));
        }

        [Test]
        public void Empty_ClicksOncePerPull_WhenThereIsNothingToReload()
        {
            _definition.Magazine.Size = 2;
            Gun gun = NewGun();
            int clicks = 0;
            gun.EmptyFired += () => clicks++;

            Run(gun, Trigger(true), 1f);
            Assert.AreEqual(2, _shots);
            Assert.AreEqual(0, clicks, "holding the trigger past the last round doesn't click");

            gun.Tick(Trigger(true, true), Frame);
            Run(gun, Trigger(true), 0.5f);
            gun.Tick(Trigger(true, true), Frame);
            Assert.AreEqual(2, clicks);
            Assert.AreEqual(GunAction.None, gun.Action);
        }

        [Test]
        public void Empty_DoesntClick_WhileItAutoReloads()
        {
            _definition.Magazine.Size = 2;
            _reserve.Add(_rifleAmmo, 10);
            Gun gun = NewGun();
            int clicks = 0;
            gun.EmptyFired += () => clicks++;

            Run(gun, Trigger(true), 0.5f);
            gun.Tick(Trigger(true, true), Frame);

            Assert.AreEqual(GunAction.Reloading, gun.Action);
            Assert.AreEqual(0, clicks);
        }

        [Test]
        public void SprintRecovery_HoldsShots_AndKeepsTheClick()
        {
            _definition.Fire.Mode = FireMode.SemiAuto;
            Gun gun = NewGun();
            GunInput recovering = Trigger(false);
            recovering.Recovering = true;

            GunInput click = recovering;
            click.TriggerPressed = true;
            gun.Tick(click, Frame);
            Run(gun, recovering, 0.2f);
            Assert.AreEqual(0, _shots, "no shot while coming out of the sprint");

            gun.Tick(Trigger(false), Frame);
            Assert.AreEqual(1, _shots, "the click fires once it's ready");
        }

        [Test]
        public void SprintRecovery_StillLetsItAim()
        {
            Gun gun = NewGun();
            GunInput input = Trigger(false);
            input.Aim = true;
            input.Recovering = true;

            gun.Tick(input, Frame);

            Assert.IsTrue(gun.IsAiming);
        }
    }
}
