using System.Collections.Generic;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Tests.EditMode
{
    // Gun parts: the stat sheet, what fits, fitting and removing them with the inventory, and saving them.
    public sealed class GunPartsTests
    {
        readonly List<Object> _assets = new();
        AmmoType _rifleAmmo;
        AmmoType _pistolAmmo;
        GunDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifleAmmo = Track(TestItems.Create<AmmoType>(maxStack: 120));
            _pistolAmmo = Track(TestItems.Create<AmmoType>(maxStack: 120));
            _rifle = Track(TestItems.Create<GunDefinition>(weight: 3f));
            SetGun(_rifle, GunFamily.Rifle, GunSlot.Optic, GunSlot.Muzzle, GunSlot.Grip, GunSlot.Magazine);
            _rifle.Magazine.AmmoType = _rifleAmmo;
            _rifle.Magazine.Size = 30;
            _rifle.Magazine.ReloadTime = 1f;
            _rifle.Handling.AimSpeed = 10f;
            _rifle.Handling.AimZoom = 0.87f;
            _rifle.Jam.Chance = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _assets)
                Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        T Track<T>(T asset) where T : Object
        {
            _assets.Add(asset);
            return asset;
        }

        static void SetGun(GunDefinition gun, GunFamily family, params GunSlot[] slots)
        {
            var so = new SerializedObject(gun);
            so.FindProperty("_family").enumValueFlag = (int)family;
            SerializedProperty list = so.FindProperty("_slots");
            list.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                list.GetArrayElementAtIndex(i).enumValueIndex = (int)slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AttachmentDefinition Part(GunSlot slot, GunFamily families, float weight = 0f, AmmoType ammo = null, float magnification = 1f,
            params (GunStat stat, float percent)[] modifiers)
        {
            AttachmentDefinition part = Track(TestItems.Create<AttachmentDefinition>(weight: weight));
            var so = new SerializedObject(part);
            so.FindProperty("_slot").enumValueIndex = (int)slot;
            so.FindProperty("_families").enumValueFlag = (int)families;
            so.FindProperty("_ammo").objectReferenceValue = ammo;
            so.FindProperty("_optic.Magnification").floatValue = magnification;
            SerializedProperty list = so.FindProperty("_modifiers");
            list.arraySize = modifiers.Length;
            for (int i = 0; i < modifiers.Length; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("Stat").enumValueIndex = (int)modifiers[i].stat;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("Percent").floatValue = modifiers[i].percent;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return part;
        }

        AttachmentDefinition ExtendedMag() => Part(GunSlot.Magazine, GunFamily.Rifle, ammo: _rifleAmmo,
            modifiers: (GunStat.MagazineSize, 33f));

        GunInstance NewRifle() => (GunInstance)_rifle.CreateInstance();

        [Test]
        public void Stats_AddsPercentagesBeforeMultiplying()
        {
            GunInstance gun = NewRifle();
            gun.TryAttach(Part(GunSlot.Grip, GunFamily.Rifle, modifiers: (GunStat.AimTime, -20f)).CreateInstance(), out _);
            gun.TryAttach(Part(GunSlot.Muzzle, GunFamily.Rifle, modifiers: (GunStat.AimTime, -20f)).CreateInstance(), out _);

            Assert.That(gun.Stats.Percent(GunStat.AimTime), Is.EqualTo(-40f));
            Assert.That(gun.Stats.AimSpeed, Is.EqualTo(10f / 0.6f).Within(0.001f));
            Assert.That(gun.Stats.AimTime, Is.EqualTo(gun.Stats.GetBase(GunStat.AimTime) * 0.6f).Within(0.001f));
        }

        [Test]
        public void Stats_MagazineRoundsToWholeRounds()
        {
            GunInstance gun = NewRifle();
            gun.TryAttach(ExtendedMag().CreateInstance(), out _);
            Assert.That(gun.Stats.MagazineSize, Is.EqualTo(40));
        }

        [Test]
        public void Stats_OpticZoomNeverZoomsOutPastTheGun()
        {
            GunInstance gun = NewRifle();
            gun.TryAttach(Part(GunSlot.Optic, GunFamily.Rifle, magnification: 1f).CreateInstance(), out _);
            Assert.That(gun.Stats.AimZoom, Is.EqualTo(0.87f).Within(0.0001f));

            gun.TryAttach(Part(GunSlot.Optic, GunFamily.Rifle, magnification: 2f).CreateInstance(), out _);
            Assert.That(gun.Stats.AimZoom, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(gun.Stats.Optic, Is.Not.Null);
        }

        [Test]
        public void Stats_PercentSumIsClamped()
        {
            GunInstance gun = NewRifle();
            gun.TryAttach(Part(GunSlot.Grip, GunFamily.Rifle, modifiers: (GunStat.ReloadTime, -200f)).CreateInstance(), out _);
            Assert.That(gun.Stats.ReloadTime, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void Fits_ByFamilyOrExactGunAndAmmoLock()
        {
            GunDefinition pistol = Track(TestItems.Create<GunDefinition>());
            SetGun(pistol, GunFamily.Pistol, GunSlot.Magazine, GunSlot.Optic);
            pistol.Magazine.AmmoType = _pistolAmmo;

            Assert.That(Part(GunSlot.Optic, GunFamily.Rifle | GunFamily.Smg).Fits(_rifle), Is.True);
            Assert.That(Part(GunSlot.Optic, GunFamily.Smg).Fits(_rifle), Is.False);
            Assert.That(ExtendedMag().Fits(_rifle), Is.True);
            // Right family, wrong calibre.
            Assert.That(Part(GunSlot.Magazine, GunFamily.Rifle | GunFamily.Pistol, ammo: _rifleAmmo).Fits(pistol), Is.False);

            AttachmentDefinition exact = Part(GunSlot.Optic, GunFamily.None);
            var so = new SerializedObject(exact);
            so.FindProperty("_guns").arraySize = 1;
            so.FindProperty("_guns").GetArrayElementAtIndex(0).objectReferenceValue = pistol;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(exact.Fits(pistol), Is.True);
            Assert.That(exact.Fits(_rifle), Is.False);
        }

        [Test]
        public void Fits_NeedsTheGunToHaveTheSlot()
        {
            Assert.That(Part(GunSlot.Tactical, GunFamily.Rifle).Fits(_rifle), Is.False);
        }

        [Test]
        public void Attach_ReplacesThePartInItsSlotAndReturnsIt()
        {
            GunInstance gun = NewRifle();
            ItemInstance first = Part(GunSlot.Optic, GunFamily.Rifle).CreateInstance();
            ItemInstance second = Part(GunSlot.Optic, GunFamily.Rifle).CreateInstance();
            int changes = 0;
            gun.AttachmentsChanged += () => changes++;

            Assert.That(gun.TryAttach(first, out ItemInstance previous), Is.True);
            Assert.That(previous, Is.Null);
            Assert.That(gun.TryAttach(second, out previous), Is.True);
            Assert.That(previous, Is.SameAs(first));
            Assert.That(gun.AttachmentIn(GunSlot.Optic), Is.SameAs(second));
            Assert.That(gun.Detach(GunSlot.Optic), Is.SameAs(second));
            Assert.That(gun.AttachmentIn(GunSlot.Optic), Is.Null);
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void Attach_RefusesAPartThatDoesntFit()
        {
            GunInstance gun = NewRifle();
            Assert.That(gun.TryAttach(Part(GunSlot.Optic, GunFamily.Pistol).CreateInstance(), out _), Is.False);
            Assert.That(gun.TryAttach(_rifleAmmo.CreateInstance(), out _), Is.False);
            Assert.That(gun.AttachmentIn(GunSlot.Optic), Is.Null);
        }

        [Test]
        public void Weight_IncludesFittedParts()
        {
            GunInstance gun = NewRifle();
            gun.TryAttach(Part(GunSlot.Optic, GunFamily.Rifle, weight: 0.4f).CreateInstance(), out _);
            Assert.That(gun.Weight, Is.EqualTo(3.4f).Within(0.0001f));

            Inventory inventory = TestItems.Inventory();
            inventory.TryAdd(gun, out _);
            Assert.That(inventory.Weight, Is.EqualTo(3.4f).Within(0.0001f));
        }

        [Test]
        public void Modding_SwapPutsTheOldPartWhereTheNewOneCameFrom()
        {
            Inventory inventory = TestItems.Inventory(backpackSlots: 4);
            GunInstance gun = NewRifle();
            ItemInstance oldPart = Part(GunSlot.Optic, GunFamily.Rifle).CreateInstance();
            ItemInstance newPart = Part(GunSlot.Optic, GunFamily.Rifle).CreateInstance();
            gun.TryAttach(oldPart, out _);
            inventory.Backpack.TryPlace(2, newPart);

            Assert.That(GunModding.TryAttach(inventory, inventory.Backpack, 2, gun, out string refusal), Is.True, refusal);
            Assert.That(gun.AttachmentIn(GunSlot.Optic), Is.SameAs(newPart));
            Assert.That(inventory.Backpack[2], Is.SameAs(oldPart));
        }

        [Test]
        public void Modding_DetachingAnExtendedMagazineReturnsTheExtraRounds()
        {
            Inventory inventory = TestItems.Inventory(backpackSlots: 4);
            GunInstance gun = NewRifle();
            gun.TryAttach(ExtendedMag().CreateInstance(), out _);
            gun.SetRounds(40);

            Assert.That(GunModding.TryDetach(inventory, gun, GunSlot.Magazine, null, 0, out string refusal), Is.True, refusal);
            Assert.That(gun.Rounds, Is.EqualTo(30));
            Assert.That(inventory.CountOf(_rifleAmmo), Is.EqualTo(10));
            Assert.That(inventory.Backpack.UsedSlots, Is.EqualTo(2));
        }

        [Test]
        public void Modding_RefusesDetachWhenTheBackpackIsFull()
        {
            Inventory inventory = TestItems.Inventory(backpackSlots: 1);
            inventory.Backpack.TryPlace(0, Part(GunSlot.Grip, GunFamily.Rifle).CreateInstance());
            GunInstance gun = NewRifle();
            ItemInstance mag = ExtendedMag().CreateInstance();
            gun.TryAttach(mag, out _);
            gun.SetRounds(40);

            Assert.That(GunModding.TryDetach(inventory, gun, GunSlot.Magazine, null, 0, out string refusal), Is.False);
            Assert.That(refusal, Is.EqualTo(GunModding.NoRoom.Text()));
            Assert.That(gun.AttachmentIn(GunSlot.Magazine), Is.SameAs(mag));
            Assert.That(gun.Rounds, Is.EqualTo(40));
        }

        [Test]
        public void Modding_RefusesAPartThatDoesntFit()
        {
            Inventory inventory = TestItems.Inventory(backpackSlots: 2);
            ItemInstance part = Part(GunSlot.Optic, GunFamily.Pistol).CreateInstance();
            inventory.Backpack.TryPlace(0, part);

            Assert.That(GunModding.TryAttach(inventory, inventory.Backpack, 0, NewRifle(), out string refusal), Is.False);
            Assert.That(refusal, Is.EqualTo(GunModding.NoFit.Text()));
            Assert.That(inventory.Backpack[0], Is.SameAs(part));
        }

        [Test]
        public void Save_RoundTripsAttachments()
        {
            AttachmentDefinition mag = ExtendedMag();
            ItemCatalog catalog = Track(ScriptableObject.CreateInstance<ItemCatalog>());
            catalog.GetType().GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(catalog, new Dictionary<string, ItemDefinition> { ["rifle"] = _rifle, ["mag"] = mag });

            GunInstance gun = NewRifle();
            gun.TryAttach(mag.CreateInstance(), out _);
            gun.SetRounds(38);
            ItemSave save = ItemSaves.Write(gun, catalog);
            var loaded = (GunInstance)ItemSaves.Read(save, catalog);

            Assert.That(loaded.AttachmentIn(GunSlot.Magazine)?.Definition, Is.SameAs(mag));
            Assert.That(loaded.Rounds, Is.EqualTo(38), "rounds over the base magazine survive because the part is restored first");
        }

        [Test]
        public void Gun_ReloadFillsTheExtendedMagazine()
        {
            GunInstance item = NewRifle();
            item.TryAttach(ExtendedMag().CreateInstance(), out _);
            item.SetRounds(0);
            AmmoReserve reserve = TestItems.Reserve();
            reserve.Add(_rifleAmmo, 100);
            var gun = new Gun(item, reserve, () => 1f);

            var input = new GunInput { Reload = true, IsReady = true, CanFire = true, CanAim = true };
            gun.Tick(input, 0.01f);
            input.Reload = false;
            gun.Tick(input, 1.1f);
            Assert.That(item.Rounds, Is.EqualTo(40));
        }

        [Test]
        public void Recoil_ScalesVerticalAndHorizontalKick()
        {
            // Same random spray for both.
            var plain = new GunRecoil(_rifle.Recoil, new RandomStream(7));
            plain.Kick(0f, 1f);
            var scaled = new GunRecoil(_rifle.Recoil, new RandomStream(7));
            scaled.Kick(0f, 1f, 0.5f, 0.5f);
            plain.Tick(0.05f, 0f);
            scaled.Tick(0.05f, 0f);
            Assert.That(plain.ViewEuler.x, Is.Not.EqualTo(0f));
            Assert.That(scaled.ViewEuler.x, Is.EqualTo(plain.ViewEuler.x * 0.5f).Within(0.0001f));
            Assert.That(scaled.ViewEuler.y, Is.EqualTo(plain.ViewEuler.y * 0.5f).Within(0.0001f));
        }

        [Test]
        public void Stats_LaserShrinksTheHipConeAndAimingClosesIt()
        {
            _rifle.Accuracy.HipSpread = 3f;
            _rifle.Accuracy.AimSpread = 0f;
            GunInstance gun = NewRifle();
            Assert.That(gun.Stats.ConeAt(0f), Is.EqualTo(3f).Within(0.0001f));
            gun.TryAttach(Part(GunSlot.Grip, GunFamily.Rifle, modifiers: (GunStat.HipSpread, -25f)).CreateInstance(), out _);
            Assert.That(gun.Stats.HipSpread, Is.EqualTo(2.25f).Within(0.0001f));
            Assert.That(gun.Stats.ConeAt(0.5f), Is.EqualTo(1.125f).Within(0.0001f));
            Assert.That(gun.Stats.ConeAt(1f), Is.EqualTo(0f));
        }

        [Test]
        public void Scope_FadesInOnlyNearFullAimForFullScopes()
        {
            var scope = new OpticSettings { FullScope = true };
            Assert.That(scope.ScopeWeight(0.5f), Is.EqualTo(0f));
            Assert.That(scope.ScopeWeight(0.9f), Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(scope.ScopeWeight(1f), Is.EqualTo(1f));
            Assert.That(new ScopeView(scope.ScopeWeight(1f), null).HidesHeld, Is.True);
            Assert.That(new OpticSettings { FullScope = false }.ScopeWeight(1f), Is.EqualTo(0f));
        }

        [Test]
        public void Inspector_NamesEveryGunStat()
        {
            Assert.That(MB13.Aipocalypse.Core.UI.ItemInspectorView.StatNames.Length, Is.EqualTo(GunStats.StatCount));
        }

        [Test]
        public void Stats_SuppressorReplacesTheShotSoundAndQuietsTheShot()
        {
            AudioEntity entity = Track(AudioEntity.CreateNewInstance(null, "Suppressed", BroAudioType.SFX));
            AttachmentDefinition suppressor = Part(GunSlot.Muzzle, GunFamily.Rifle);
            var so = new SerializedObject(suppressor);
            so.FindProperty("_fireSound._entity").objectReferenceValue = entity;
            so.FindProperty("_loudness").floatValue = 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            GunInstance gun = NewRifle();
            Assert.That(gun.Stats.Loudness, Is.EqualTo(1f));
            Assert.That(gun.Stats.Muzzle, Is.Null);

            gun.TryAttach(suppressor.CreateInstance(), out _);

            Assert.That(gun.Stats.Muzzle, Is.SameAs(suppressor));
            Assert.That(gun.Stats.FireSound, Is.EqualTo(new SoundID(entity)));
            Assert.That(gun.Stats.FireTailSound.IsValid(), Is.False, "no tail unless the suppressor names one");
            Assert.That(gun.Stats.Loudness, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void LootCrate_RollsItsTableOnceOnTopOfItsContents()
        {
            AttachmentDefinition part = Part(GunSlot.Grip, GunFamily.Rifle);
            LootTable table = Track(ScriptableObject.CreateInstance<LootTable>());
            table.Drops.Count = Vector2Int.one;
            table.Drops.Entries.Add(new WeightedEntry<LootDrop> { Value = new LootDrop { Item = part, Count = Vector2Int.one }, Weight = 1f });
            var crate = Track(new GameObject("Crate")).AddComponent<LootContainer>();
            var so = new SerializedObject(crate);
            so.FindProperty("_table").objectReferenceValue = table;
            so.ApplyModifiedPropertiesWithoutUndo();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(new RandomService(7));
            builder.Build().Inject(crate);

            int Parts()
            {
                int count = 0;
                for (int i = 0; i < crate.Items.Count; i++)
                    if (crate.Items[i]?.Definition == part)
                        count++;
                return count;
            }

            Assert.That(Parts(), Is.EqualTo(1));
            Assert.That(crate.HiddenCount, Is.EqualTo(1), "rolled loot starts hidden like the rest");
            Assert.That(Parts(), Is.EqualTo(1), "it rolls once");
        }

        // ---- Content wiring ----

        // The five built first, then the block-outs (AgentScripts/Weapons/GunBlockoutSetup.cs).
        static readonly string[] Guns =
        {
            "AK47", "MP5", "Pistol", "PumpShotgun", "DoubleBarrel",
            "Glock17", "DesertEagle", "Uzi", "M1Carbine", "M4", "AK74M", "FAL", "HuntingRifle", "AA12", "RPD",
        };

        [Test]
        public void GunPrefabs_ShootFromAMuzzleAndHoldBothHands()
        {
            foreach (string name in Guns)
            {
                var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>($"Assets/_Game/Content/Weapons/{name}/{name}.asset");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Content/Weapons/{name}/{name}.prefab");
                Assert.That(definition, Is.Not.Null, name + ": no item");
                Assert.That(definition.Prefab, Is.Not.Null.And.SameAs(prefab.GetComponent<WorldItem>()), name + ": item doesn't point at its prefab");
                Assert.That(definition.Icon, Is.Not.Null, name + ": no icon");
                Assert.That(definition.Magazine.AmmoType, Is.Not.Null, name + ": no ammo type");
                Assert.That(definition.Magazine.AmmoType.Prefab, Is.Not.Null, name + ": its ammo has no box");
                var held = new SerializedObject(prefab.GetComponent<HeldGun>());
                var muzzle = (Transform)held.FindProperty("_muzzle").objectReferenceValue;
                Assert.That(muzzle, Is.Not.Null.And.Property("parent").Not.Null, name + ": no muzzle");
                Assert.That(muzzle.IsChildOf(prefab.transform.Find("PosePivot")), Is.True, name + ": muzzle outside the posed model");
                var hands = prefab.GetComponent<MB13.Aipocalypse.Core.HandSystem.ItemHands>();
                Assert.That(hands, Is.Not.Null, name + ": no ItemHands");
                Assert.That(hands.GetGrip(MB13.Aipocalypse.Core.HandSystem.HandSide.Right), Is.Not.Null, name + ": no right grip");
                Assert.That(hands.GetGrip(MB13.Aipocalypse.Core.HandSystem.HandSide.Left), Is.Not.Null, name + ": no left grip");
            }
        }

        [Test]
        public void Loot_GoblinsAndCratesDropEveryPart()
        {
            var goblin = AssetDatabase.LoadAssetAtPath<LootTable>("Assets/_Game/Data/Loot/GoblinLoot.asset");
            var crate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Props/LootCrate/LootCrate.prefab");
            var crateTable = (LootTable)new SerializedObject(crate.GetComponent<LootContainer>()).FindProperty("_table").objectReferenceValue;
            Assert.That(crateTable, Is.Not.Null, "the loot crate rolls no table");
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AttachmentDefinition), new[] { "Assets/_Game" }))
            {
                var part = AssetDatabase.LoadAssetAtPath<AttachmentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(goblin.Drops.Entries.Exists(e => e.Value.Item == part && e.Weight > 0f), Is.True, part.name + ": goblins never drop it");
                Assert.That(crateTable.Drops.Entries.Exists(e => e.Value.Item == part && e.Weight > 0f), Is.True, part.name + ": crates never hold it");
            }
        }

        [Test]
        public void GunPrefabs_HaveAMountForEverySlotTheyDeclare()
        {
            foreach (string name in Guns)
            {
                var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>($"Assets/_Game/Content/Weapons/{name}/{name}.asset");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Content/Weapons/{name}/{name}.prefab");
                Assert.That(definition.Slots, Is.Not.Empty, name + " declares no part slots");
                Assert.That(definition.Family, Is.Not.EqualTo(GunFamily.None), name);

                var held = prefab.GetComponent<HeldGun>();
                var mounts = (GunMounts)new SerializedObject(held).FindProperty("_mounts").objectReferenceValue;
                Assert.That(mounts, Is.Not.Null, name + ": HeldGun has no GunMounts");
                Assert.That(mounts.IronSight, Is.Not.Null, name + ": no iron sight marker");
                GunMounts world = prefab.transform.Find("World/Model").GetComponent<GunMounts>();
                Assert.That(world, Is.Not.Null, name + ": World/Model has no GunMounts");

                foreach (GunSlot slot in definition.Slots)
                {
                    if (slot == GunSlot.Magazine)
                        continue;
                    Assert.That(mounts.MountOf(slot), Is.Not.Null, $"{name}: no held mount for {slot}");
                    Assert.That(world.MountOf(slot), Is.Not.Null, $"{name}: no world mount for {slot}");
                }
            }
        }

        [Test]
        public void Attachments_HaveAModelPickupIconAndName()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(AttachmentDefinition), new[] { "Assets/_Game" });
            Assert.That(guids, Is.Not.Empty);
            foreach (string guid in guids)
            {
                var part = AssetDatabase.LoadAssetAtPath<AttachmentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(part.Icon, Is.Not.Null, part.name + ": no icon");
                Assert.That(part.Prefab, Is.Not.Null, part.name + ": no pickup prefab");
                Assert.That(new SerializedObject(part.Prefab).FindProperty("_definition").objectReferenceValue, Is.SameAs(part),
                    part.name + ": its pickup holds another item");
                Assert.That(part.Slot == GunSlot.Magazine || part.Model != null, Is.True, part.name + ": no model");
                Assert.That(part.Families != GunFamily.None || part.Guns.Count > 0, Is.True, part.name + ": fits nothing");
                Assert.That(part.Slot != GunSlot.Optic || part.Model == null || part.Model.Sight != null, Is.True, part.name + ": optic without a sight point");
                Assert.That(part.Slot != GunSlot.Muzzle || part.Model.Tip != null, Is.True, part.name + ": muzzle without a tip");
            }
        }
    }
}
