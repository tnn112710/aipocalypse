using System.Collections.Generic;
using Aipocalypse.Core.AmbientSystem;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.SpawnSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    // Open-world population rules, flock steering and the far-enemy think rate.
    public sealed class PopulationTests
    {
        [Test]
        public void Area_WakesInsideAndSleepsPastTheGap()
        {
            Assert.That(PopulationRules.IsActive(false, 140f, 150f, 200f), Is.True, "wakes inside 150 m");
            Assert.That(PopulationRules.IsActive(false, 170f, 150f, 200f), Is.False, "asleep, 170 m doesn't wake it");
            Assert.That(PopulationRules.IsActive(true, 170f, 150f, 200f), Is.True, "awake, 170 m keeps it awake");
            Assert.That(PopulationRules.IsActive(true, 210f, 150f, 200f), Is.False, "past 200 m it sleeps");
        }

        [Test]
        public void Area_TopsUpToCapacity_AGroupAtATime()
        {
            Assert.That(PopulationRules.ToSpawn(6, 0, 0, 0, 100, 100), Is.EqualTo(6));
            Assert.That(PopulationRules.ToSpawn(6, 0, 0, 2, 100, 100), Is.EqualTo(2), "one group of two");
            Assert.That(PopulationRules.ToSpawn(6, 5, 0, 3, 100, 100), Is.EqualTo(1));
            Assert.That(PopulationRules.ToSpawn(6, 7, 0, 3, 100, 100), Is.Zero, "never negative");
        }

        [Test]
        public void Area_KilledOnesWaitForTheirRespawn()
        {
            Assert.That(PopulationRules.ToSpawn(4, 1, 2, 0, 100, 100), Is.EqualTo(1), "two killed still wait");
            var respawnAt = new List<float> { 10f, 20f, float.PositiveInfinity };
            PopulationRules.ReleaseDue(respawnAt, 15f);
            Assert.That(respawnAt, Is.EqualTo(new[] { 20f, float.PositiveInfinity }));
            PopulationRules.ReleaseDue(respawnAt, 1e9f);
            Assert.That(respawnAt.Count, Is.EqualTo(1), "a delay of 0 means never");
        }

        [Test]
        public void Caps_LimitByCategoryAndGlobally()
        {
            Assert.That(PopulationRules.ToSpawn(6, 0, 0, 0, 2, 100), Is.EqualTo(2), "the category has room for two");
            Assert.That(PopulationRules.ToSpawn(6, 0, 0, 0, 100, 1), Is.EqualTo(1), "the world has room for one");
            Assert.That(PopulationRules.ToSpawn(6, 0, 0, 0, -3, 100), Is.Zero, "over the cap already");
        }

        [Test]
        public void Settings_HaveACapForEveryCategory()
        {
            var settings = ScriptableObject.CreateInstance<SpawnSystemSettings>();
            try
            {
                foreach (SpawnCategory category in System.Enum.GetValues(typeof(SpawnCategory)))
                    Assert.That(settings.CategoryCaps.ContainsKey(category), Is.True, $"{category} has no cap");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Flock_TurnsBackIntoItsBand()
        {
            FlockDefinition flock = Definition();
            try
            {
                var positions = new[] { new Vector3(0f, 1f, 0f) };
                var velocities = new[] { Vector3.forward };
                Vector3 below = FlockMath.Steer(0, positions, velocities, 1, Vector3.zero, 5f, 15f, flock, default, false, 0f);
                Assert.That(below.y, Is.GreaterThan(0f), "under the floor: up");
                positions[0].y = 20f;
                Vector3 above = FlockMath.Steer(0, positions, velocities, 1, Vector3.zero, 5f, 15f, flock, default, false, 0f);
                Assert.That(above.y, Is.LessThan(0f), "over the ceiling: down");
                Assert.That(below.magnitude, Is.EqualTo(flock.Speed).Within(1e-3f), "at its speed");
            }
            finally
            {
                Object.DestroyImmediate(flock);
            }
        }

        [Test]
        public void Flock_SeparatesNeighbours_AndFleesThePlayer()
        {
            FlockDefinition flock = Definition();
            try
            {
                flock.Wander = 0f;
                flock.Alignment = 0f;
                flock.Cohesion = 0f;
                var positions = new[] { new Vector3(0f, 10f, 0f), new Vector3(0.5f, 10f, 0f) };
                var velocities = new[] { Vector3.zero, Vector3.zero };
                Vector3 apart = FlockMath.Steer(0, positions, velocities, 2, Vector3.zero, 5f, 15f, flock, default, false, 0f);
                Assert.That(apart.x, Is.LessThan(0f), "away from the one too close");
                Vector3 flee = FlockMath.Steer(0, positions, velocities, 1, Vector3.zero, 5f, 15f, flock, new Vector3(0f, 10f, 3f), true, 0f);
                Assert.That(flee.z, Is.LessThan(0f), "away from the player");
                Assert.That(flee.magnitude, Is.EqualTo(flock.Speed * flock.FleeSpeedMultiplier).Within(1e-3f), "faster while fleeing");
            }
            finally
            {
                Object.DestroyImmediate(flock);
            }
        }

        [Test]
        public void Manager_FarEnemiesThinkLessOften()
        {
            float next = 0f;
            Assert.That(EnemyManager.ThinksNow(50f * 50f, 60f, 1f, ref next, 0.25f), Is.True, "near: always");
            Assert.That(next, Is.Zero);
            Assert.That(EnemyManager.ThinksNow(80f * 80f, 60f, 1f, ref next, 0.25f), Is.True, "far, due");
            Assert.That(next, Is.EqualTo(1.25f));
            Assert.That(EnemyManager.ThinksNow(80f * 80f, 60f, 1.1f, ref next, 0.25f), Is.False, "far, not due yet");
            Assert.That(EnemyManager.ThinksNow(80f * 80f, 60f, 1.3f, ref next, 0.25f), Is.True);
            Assert.That(EnemyManager.ThinksNow(500f * 500f, 0f, 1.31f, ref next, 0.25f), Is.True, "no far distance: always");
        }

        static FlockDefinition Definition()
        {
            var flock = ScriptableObject.CreateInstance<FlockDefinition>();
            flock.Wander = 0.1f;
            flock.RoamRadius = 100f;
            return flock;
        }
    }
}
