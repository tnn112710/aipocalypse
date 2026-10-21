using System.Collections;
using System.Linq;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Tests
{
    // Loads the prototype scene and plays the core loop through public APIs: everything injects, enemies register,
    // a gun is picked up, fires and reloads. Any error logged along the way fails the test.
    public sealed class PrototypeSmokeTests
    {
        const string ScenePath = "Assets/_Game/Scenes/Prototype/Prototype.unity";

        [UnityTest]
        public IEnumerator PrototypeScene_PlayerPicksUpAGunFiresAndReloads()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            yield return null;

            var player = Object.FindAnyObjectByType<Player>();
            Assert.IsNotNull(player, "no Player in the scene");
            Assert.IsNotNull(player.Equipment, "Player wasn't injected");

            var scope = LifetimeScope.Find<GameLifetimeScope>();
            Assert.IsNotNull(scope, "no GameLifetimeScope");
            // Enemies start (and register) on their first Start, a frame after the scene loads.
            Assert.That(scope.Container.Resolve<EnemyRegistry>().Count, Is.GreaterThan(0), "scene enemies didn't register");

            if (player.HeldItem is not HeldGun)
            {
                WorldItem pickup = Object.FindObjectsByType<WorldItem>().FirstOrDefault(w => w.Item is GunInstance);
                Assert.IsNotNull(pickup, "no gun to pick up");
                pickup.Interact(player.gameObject);
            }

            for (float t = 0f; t < 3f && !(player.HeldItem is HeldGun && player.Equipment.Switcher.IsReady); t += Time.deltaTime)
                yield return null;
            var held = player.HeldItem as HeldGun;
            Assert.IsNotNull(held, "no gun in hand");
            Assert.IsTrue(player.Equipment.Switcher.IsReady, "gun never got ready");

            Gun gun = held.Gun;
            gun.Item.SetJammed(false);
            gun.Item.SetRounds(gun.Item.Gun.Magazine.Size);
            int fired = 0, jammed = 0;
            gun.Fired += () => fired++;
            gun.Jammed += () => jammed++;
            gun.Tick(new GunInput { TriggerHeld = true, TriggerPressed = true, IsReady = true, CanFire = true, CanAim = true }, 0.016f);
            Assert.That(fired + jammed, Is.EqualTo(1), "trigger pull neither fired nor jammed");
            yield return null;

            gun.Interrupt();
            gun.Item.SetJammed(false);
            gun.Item.SetRounds(0);
            gun.Tick(new GunInput { Reload = true, IsReady = true, CanFire = true, CanAim = true }, 0.016f);
            Assert.That(gun.Action, Is.EqualTo(GunAction.Reloading), "empty gun didn't reload");
            for (float t = 0f; t < 10f && gun.Action != GunAction.None; t += Time.deltaTime)
                yield return null;
            Assert.That(gun.Item.Rounds, Is.GreaterThan(0), "reload loaded nothing");
        }
    }
}
