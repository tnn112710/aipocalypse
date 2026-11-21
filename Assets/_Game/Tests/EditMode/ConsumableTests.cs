using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Defaults;
using MB13.Aipocalypse.Core.Stats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class ConsumableTests
    {
        // Takes what a consumable does, and says when it was last hurt.
        sealed class FakeConsumer : IEquipmentOwner, IConsumer
        {
            public readonly List<ConsumableDefinition> Used = new();
            public string Refusal;

            public Equipment Equipment => null;
            public float LastHurtTime { get; set; } = float.NegativeInfinity;
            public string CannotUse(ConsumableDefinition item) => Refusal;
            public void Use(ConsumableDefinition item) => Used.Add(item);
        }

        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        ConsumableDefinition Consumable(int maxStack, int uses, float useSeconds = 1f, int value = 0)
        {
            ConsumableDefinition definition = ScriptableObject.CreateInstance<ConsumableDefinition>().With(maxStack);
            definition.SetUse(useSeconds, uses, 0.5f);
            var so = new SerializedObject(definition);
            so.FindProperty("_value").intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            _created.Add(definition);
            return definition;
        }

        ConsumableDefinition Buff(StatId stat, float scale, float seconds)
        {
            ConsumableDefinition definition = Consumable(3, 1);
            definition.SetEffects(0f, 0f, 10f, seconds, new Dictionary<StatId, float> { { stat, scale } });
            return definition;
        }

        ConsumableDefinition Bandage()
        {
            ConsumableDefinition definition = Consumable(5, 1, 1.5f);
            definition.SetEffects(0f, 15f, 10f, 0f, null);
            return definition;
        }

        HeldConsumable Hold(ItemInstance item, FakeConsumer consumer)
        {
            var go = new GameObject("Held");
            _created.Add(go);
            var held = go.AddComponent<HeldConsumable>();
            held.Bind(item, consumer);
            return held;
        }

        static HeldItemInput Press(bool pressed = false) => new() { IsReady = true, UseHeld = true, UsePressed = pressed };

        [Test]
        public void MultiUseConsumable_IsOneItemShowingUsesLeft_AndWorthLessAsItRunsOut()
        {
            ItemInstance item = Consumable(1, 5, value: 120).CreateInstance();

            var kit = item as ConsumableInstance;
            Assert.IsNotNull(kit, "more than one use makes a ConsumableInstance");
            Assert.AreEqual(5, kit.Badge.Count);
            Assert.AreEqual(120, kit.Value);
            Assert.IsFalse(kit.UseOnce());
            Assert.AreEqual(4, kit.Badge.Count);
            Assert.AreEqual(96, kit.Value);
            for (int i = 0; i < 3; i++)
                kit.UseOnce();
            Assert.IsTrue(kit.UseOnce(), "the last use says so");
        }

        [Test]
        public void SingleUseConsumable_StacksAsUsual_AndIsWorthValueTimesCount()
        {
            ItemInstance stack = Consumable(5, 1, value: 15).CreateInstance(3);

            Assert.IsNotInstanceOf<ConsumableInstance>(stack);
            Assert.AreEqual(3, stack.Badge.Count);
            Assert.AreEqual(45, stack.Value);
        }

        [Test]
        public void Effects_BuffScalesTheStat_UntilItRunsOut()
        {
            var effects = new PlayerEffects(new BaseStats(), 3f);
            int changed = 0;
            effects.Changed += () => changed++;
            ConsumableDefinition adrenaline = Buff(StatId.WalkSpeed, 1.25f, 60f);

            effects.Use(adrenaline, new Health(100f));
            Assert.AreEqual(5f, effects.Get(StatId.WalkSpeed, 4f), 0.0001f);
            Assert.AreEqual(4f, effects.Get(StatId.SprintSpeedScale, 4f), 0.0001f, "other stats untouched");
            Assert.AreEqual(1, changed, "cached maxima are read again");

            effects.Tick(59f, null);
            Assert.AreEqual(5f, effects.Get(StatId.WalkSpeed, 4f), 0.0001f);
            effects.Tick(1.5f, null);
            Assert.AreEqual(4f, effects.Get(StatId.WalkSpeed, 4f), 0.0001f);
            Assert.AreEqual(2, changed);
            Assert.AreEqual(0, effects.Effects.Count);
        }

        [Test]
        public void Effects_SameBuffAgain_RestartsInsteadOfStacking()
        {
            var effects = new PlayerEffects(new BaseStats(), 3f);
            ConsumableDefinition adrenaline = Buff(StatId.WalkSpeed, 1.25f, 60f);
            var health = new Health(100f);

            effects.Use(adrenaline, health);
            effects.Tick(30f, health);
            effects.Use(adrenaline, health);

            Assert.AreEqual(1, effects.Effects.Count);
            Assert.AreEqual(60f, effects.Effects[0].Remaining, 0.0001f);
            Assert.AreEqual(5f, effects.Get(StatId.WalkSpeed, 4f), 0.0001f);
        }

        [Test]
        public void Effects_HealOverTime_HealsItsTotalOverItsSeconds()
        {
            var effects = new PlayerEffects(new BaseStats(), 3f);
            var health = new Health(100f);
            health.Damage(50f);

            effects.Use(Bandage(), health);
            effects.Tick(5f, health);
            Assert.AreEqual(57.5f, health.Current, 0.01f, "half the time, half the healing");
            for (int i = 0; i < 6; i++)
                effects.Tick(1f, health);

            Assert.AreEqual(65f, health.Current, 0.01f);
            Assert.AreEqual(0, effects.Effects.Count);
        }

        [Test]
        public void Effects_AHitPausesHealing_ThenItCarriesOn()
        {
            var effects = new PlayerEffects(new BaseStats(), 3f);
            var health = new Health(100f);
            health.Damage(50f);
            effects.Use(Bandage(), health);
            effects.Tick(2f, health);
            float before = health.Current;

            effects.OnHurt();
            effects.Tick(2f, health);
            Assert.AreEqual(before, health.Current, 0.0001f);
            Assert.IsTrue(effects.HealingPaused);
            for (int i = 0; i < 12; i++)
                effects.Tick(1f, health);

            Assert.AreEqual(65f, health.Current, 0.01f, "nothing lost, only later");
        }

        [Test]
        public void Effects_Clear_DropsEverything()
        {
            var effects = new PlayerEffects(new BaseStats(), 3f);
            var health = new Health(100f);
            effects.Use(Buff(StatId.StaminaRegen, 2f, 60f), health);
            effects.Use(Bandage(), health);

            effects.Clear();

            Assert.AreEqual(0, effects.Effects.Count);
            Assert.AreEqual(18f, effects.Get(StatId.StaminaRegen, 18f), 0.0001f);
        }

        [Test]
        public void HeldConsumable_HoldingForUseSeconds_UsesOne()
        {
            ConsumableDefinition bandage = Bandage();
            ItemInstance stack = bandage.CreateInstance(3);
            var consumer = new FakeConsumer();
            HeldConsumable held = Hold(stack, consumer);

            held.Tick(Press(pressed: true), 0f);
            Assert.IsTrue(held.IsUsing);
            Assert.AreEqual(0.5f, held.MoveSpeedScale);
            Assert.IsTrue(held.StopsSprint(true, false));
            held.Tick(Press(), 1f);
            Assert.AreEqual(1f / 1.5f, held.UseProgress, 0.001f);
            held.Tick(Press(), 0.6f);

            CollectionAssert.AreEqual(new[] { bandage }, consumer.Used);
            Assert.AreEqual(2, stack.Count);
            Assert.IsFalse(held.IsUsing);
            held.Tick(Press(), 2f);
            Assert.IsFalse(held.IsUsing, "holding on doesn't start the next one");
        }

        [Test]
        public void HeldConsumable_ReleasingOrAHitCancels()
        {
            ItemInstance stack = Bandage().CreateInstance(3);
            var consumer = new FakeConsumer();
            HeldConsumable held = Hold(stack, consumer);

            held.Tick(Press(pressed: true), 0f);
            held.Tick(Press(), 1f);
            held.Tick(new HeldItemInput { IsReady = true }, 0.1f);
            Assert.IsFalse(held.IsUsing, "let go");

            held.Tick(Press(pressed: true), 0f);
            consumer.LastHurtTime = Time.time + 1f;
            held.Tick(Press(), 2f);

            Assert.IsFalse(held.IsUsing, "hit");
            Assert.AreEqual(0, consumer.Used.Count);
            Assert.AreEqual(3, stack.Count);
        }

        [Test]
        public void HeldConsumable_Refused_ShowsWhyAndDoesntStart()
        {
            var consumer = new FakeConsumer { Refusal = "Health full" };
            HeldConsumable held = Hold(Bandage().CreateInstance(1), consumer);

            held.Tick(Press(pressed: true), 0f);

            Assert.IsFalse(held.IsUsing);
            Assert.AreEqual("Health full", held.Refusal);
        }

        [Test]
        public void HeldConsumable_MultiUseKit_SpendsAUse_AndTheLastEmptiesIt()
        {
            ConsumableDefinition kitDefinition = Consumable(1, 2, useSeconds: 3f);
            var kit = (ConsumableInstance)kitDefinition.CreateInstance();
            var consumer = new FakeConsumer();
            HeldConsumable held = Hold(kit, consumer);

            for (int use = 0; use < 2; use++)
            {
                held.Tick(Press(pressed: true), 0f);
                held.Tick(Press(), 3f);
                held.Tick(new HeldItemInput { IsReady = true }, 0f);
            }

            Assert.AreEqual(2, consumer.Used.Count);
            Assert.AreEqual(0, kit.UsesLeft);
            Assert.IsTrue(kit.IsEmpty, "the slot lets go of an emptied item");
        }
    }
}
