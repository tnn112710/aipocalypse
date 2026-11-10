using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.SpawnSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MessagePipe;
using VContainer;
using Object = UnityEngine.Object;

// Play-mode probe of the inventory in WorldMap, step by step (enter Play first; each entry reports what it saw):
// Start → Pickups → Magnet → Throw → Weapons → Markers → OpenCrate → DragDrop → CloseAndHold → Goblins → LootGoblin →
// EmptyGoblin → RaidLoot → Finish.
// Drives the real player through IPlayer.IntentSource (look, E) and the real UI through pointer events.
// Run: unity command script run --file AgentScripts/Inventory/InventoryPlayTest.cs --entry InventoryPlayTest.Start
public static class InventoryPlayTest
{
    const string YardNode = "InventoryTest";

    // Turns the view towards a point and presses keys for the player, frame by frame.
    sealed class ScriptedIntent : IPlayerIntentSource
    {
        readonly Player _player;
        public Vector3? LookAt;
        public bool Interact;
        public Vector2 Move;

        public ScriptedIntent(Player player) => _player = player;

        public PlayerIntent Read(float deltaTime)
        {
            var intent = new PlayerIntent { Move = Move };
            if (LookAt is Vector3 target)
            {
                Transform view = _player.View;
                Vector3 direction = (target - view.position).normalized;
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
                float currentPitch = Mathf.DeltaAngle(0f, view.eulerAngles.x);
                intent.LookDegrees = new Vector2(Mathf.DeltaAngle(_player.transform.eulerAngles.y, yaw), currentPitch - pitch);
            }
            if (Interact)
            {
                intent.Interact = true;
                Interact = false;
            }
            return intent;
        }
    }

    static Player _player;
    static readonly List<string> _events = new();
    static Action<ItemDefinition, int> _onPicked;
    static Action<ItemDefinition> _onBlocked;
    static Action<LootContainer> _onOpened;

    static Player Player => _player != null ? _player : _player = Object.FindAnyObjectByType<Player>();
    static Inventory Inv => Player.Equipment.Inventory;

    public static string Start()
    {
        if (!Application.isPlaying)
            return "enter Play first";
        Application.runInBackground = true;
        Player.Invulnerable = true;
        Player.IgnoredByEnemies = true;
        Player.IntentSource = new ScriptedIntent(Player);
        return "start: god, notarget, scripted intent\n" + Describe();
    }

    public static async Task<string> Pickups()
    {
        Listen();
        Transform yard = GameObject.Find(YardNode).transform;
        var log = new StringBuilder();
        foreach (WorldItem item in yard.GetComponentsInChildren<WorldItem>())
        {
            if (item.Item.Definition.AutoCollect)
                continue;
            Teleport(item.transform.position - Flat(item.transform.forward) * 1.2f);
            await Frames(3);
            int before = Inv.CountOf(item.Item.Definition);
            string name = item.Item.Definition.DisplayName;
            bool all = item.Collect(Player.gameObject);
            log.AppendLine($"collect {name}: all={all}, carried {before} -> {Inv.CountOf(item.Item.Definition)}");
        }
        log.AppendLine($"events: {Drain()}");
        log.Append(Describe());
        return log.ToString();
    }

    // Spawns a pile of tokens on open ground and walks up to it: untouched at 3 m and 1.6 m, pulled in within the 1 m radius.
    public static async Task<string> Magnet()
    {
        Listen();
        var tokens = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Game/Content/Items/Tokens/Tokens.asset");
        var log = new StringBuilder();
        Vector3 forward = Flat(Player.transform.forward);
        Vector3 spot = Player.transform.position + forward * 6f;
        if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 10f, ~0, QueryTriggerInteraction.Ignore))
            spot = ground.point;
        WorldItem pile = WorldItem.Spawn(tokens.CreateInstance(25), spot, Quaternion.identity, null);
        int before = Inv.CountOf(tokens);
        foreach (float distance in new[] { 3f, 1.6f, 0.7f })
        {
            Teleport(spot - forward * distance);
            await Seconds(1.2f);
            log.AppendLine($"at {distance} m: pile there={pile != null}, carried {Inv.CountOf(tokens)}");
        }
        log.AppendLine($"tokens {before} -> {Inv.CountOf(tokens)} (+25 expected once within 1 m); events: {Drain()}");
        return log.ToString();
    }

    // Throws a medic box (it flies, lands and settles) and 10 tokens (the magnet leaves them until the player walks off).
    public static async Task<string> Throw()
    {
        Listen();
        var log = new StringBuilder();
        ItemContainer backpack = Inv.Backpack;
        int medic = Find(backpack, d => d is ConsumableDefinition);
        int tokens = Find(backpack, d => d.AutoCollect);
        if (medic < 0 || tokens < 0)
            return "throw: needs a medic box and tokens in the backpack\n" + Describe();
        ItemDefinition tokenDefinition = backpack[tokens].Definition;
        int carriedTokens = Inv.CountOf(tokenDefinition);

        Inv.TryRemove(backpack, medic, out ItemInstance box, out _, 1);
        Player.ThrowAway(box);
        WorldItem thrownBox = Latest(box.Definition);
        bool flew = thrownBox != null && thrownBox.GetComponent<Rigidbody>() != null;
        Vector3 start = thrownBox != null ? thrownBox.transform.position : default;
        await Seconds(3f);
        bool settled = thrownBox != null && thrownBox.GetComponent<Rigidbody>() == null;
        log.AppendLine($"medic box thrown: flew={flew}, settled={settled}, moved {(thrownBox != null ? Vector3.Distance(start, thrownBox.transform.position) : -1):0.00} m, ground y {(thrownBox != null ? thrownBox.transform.position.y : 0):0.00}");

        WorldItem thrown = null;
        Action<WorldItem> capture = w => thrown = w;
        Player.Interactor.Thrown += capture;

        // Thrown forward: it lands out of reach, so walking up to it pulls it in.
        Inv.TryRemove(backpack, Find(backpack, d => d.AutoCollect), out ItemInstance pile, out _, 10);
        Player.ThrowAway(pile);
        WorldItem far = thrown;
        await Seconds(2f);
        float landed = far != null ? Vector3.Distance(far.transform.position, Player.transform.position) : -1f;
        log.AppendLine($"10 tokens thrown forward: landed {landed:0.0} m away, still there={far != null}, carried {Inv.CountOf(tokenDefinition)} (was {carriedTokens})");
        if (far != null)
        {
            Teleport(far.transform.position - Flat(Player.transform.forward) * 0.5f);
            await Seconds(1.5f);
            log.AppendLine($"walked up to it: collected={far == null}, carried {Inv.CountOf(tokenDefinition)}");
        }

        // Dropped at the feet (looking down): it stays until the player has walked away once.
        Intent("LookAt", (Vector3?)(Player.transform.position + Flat(Player.transform.forward) * 0.3f));
        await Seconds(0.4f);
        Inv.TryRemove(backpack, Find(backpack, d => d.AutoCollect), out ItemInstance near, out _, 5);
        Player.ThrowAway(near);
        WorldItem feet = thrown;
        Intent("LookAt", null);
        await Seconds(2f);
        log.AppendLine($"5 tokens dropped at the feet: {(feet != null ? Vector3.Distance(feet.transform.position, Player.transform.position) : -1f):0.0} m away, still there={feet != null} (the magnet waits)");
        if (feet != null)
        {
            Vector3 at = feet.transform.position;
            Teleport(at - Flat(Player.transform.forward) * 4f);
            await Seconds(0.5f);
            Teleport(at - Flat(Player.transform.forward) * 0.5f);
            await Seconds(1.5f);
            log.AppendLine($"walked away and back: collected={feet == null}, carried {Inv.CountOf(tokenDefinition)} (was {carriedTokens})");
        }
        Player.Interactor.Thrown -= capture;
        if (thrownBox != null)
        {
            Teleport(thrownBox.transform.position - Flat(Player.transform.forward) * 1.2f);
            await Frames(2);
            log.AppendLine($"picked the medic box back up: {thrownBox.Collect(Player.gameObject)}");
        }
        log.AppendLine($"events: {Drain()}");
        return log.ToString();
    }

    // Picks up every gun, melee weapon and tool lying in the scene (the table by the house): two guns go active, the rest
    // to the backpack; the axe to the melee slot (key V).
    public static async Task<string> Weapons()
    {
        Listen();
        var log = new StringBuilder();
        foreach (WorldItem item in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude))
        {
            if (item.IsTaken || item.Item == null || !(item.Item.Definition is EquippableDefinition) || item.transform.IsChildOf(GameObject.Find(YardNode).transform))
                continue;
            if (item.Item.Definition is ConsumableDefinition)
                continue;
            Teleport(item.transform.position - Flat(item.transform.forward) * 1.2f);
            await Frames(2);
            string name = item.Item.Definition.DisplayName;
            bool all = item.Collect(Player.gameObject);
            log.AppendLine($"collect {name} ({item.Item.Definition.Category}): {all}, hotbar slot {Player.Equipment.Hotbar.IndexOf(item.Item)}, backpack slot {Inv.Backpack.IndexOf(item.Item)}");
        }
        Equipment equipment = Player.Equipment;
        int melee = equipment.SlotOf(HotbarKey.Melee);
        log.AppendLine($"melee key V -> slot {melee}: {equipment.Hotbar[melee]?.Definition.DisplayName}; key 1 -> {equipment.Hotbar[equipment.SlotOf(HotbarKey.Key1)]?.Definition.DisplayName}");
        equipment.Select(melee);
        await Seconds(1f);
        log.AppendLine($"selected V: holding {equipment.Switcher.Held?.Definition.DisplayName} ready={equipment.Switcher.IsReady}");
        log.AppendLine($"events: {Drain()}");
        log.Append(Describe());
        return log.ToString();
    }

    static LootContainer Crate()
    {
        LootContainer crate = null;
        foreach (LootContainer container in GameObject.Find(YardNode).GetComponentsInChildren<LootContainer>())
            crate = container;
        return crate;
    }

    // Stands 4 m from the crate looking away from it: its marker shows (bright: unsearched). Reports the shown markers.
    public static async Task<string> Markers()
    {
        LootContainer crate = Crate();
        if (crate == null)
            return "no crate";
        Teleport(crate.transform.position + Flat(crate.transform.forward) * -4f);
        Intent("LookAt", (Vector3?)(crate.transform.position + Vector3.up * 0.8f));
        await Seconds(0.6f);
        return $"crate holds loot={crate.HoldsLoot}, searched={crate.Searched}; {MarkerReport()}";
    }

    static string MarkerReport()
    {
        var hud = Object.FindAnyObjectByType<LootMarkerHud>();
        if (hud == null)
            return "no LootMarkerHud";
        var shown = new List<string>();
        foreach (Image marker in hud.GetComponentsInChildren<Image>())
            shown.Add($"{marker.name} at {(Vector2)marker.rectTransform.position:F0} alpha {marker.color.a:0.00} scale {marker.rectTransform.localScale.x:0.00}");
        return $"markers shown {shown.Count} (screen {UnityEngine.Screen.width}x{UnityEngine.Screen.height}): {string.Join("; ", shown)}";
    }

    // Faces the loot crate and presses E: the paused screen opens at once with its items hidden, then reveals one a second.
    // Hidden items can't be dragged or taken.
    public static async Task<string> OpenCrate()
    {
        Listen();
        LootContainer crate = Crate();
        if (crate == null)
            return "no crate";
        Vector3 target = crate.transform.position + Vector3.up * 0.35f;
        Teleport(crate.transform.position + Flat(crate.transform.forward) * -1.6f);
        Intent("LookAt", (Vector3?)(target));
        await Seconds(0.4f);
        string focus = Player.Interactor.Focus?.GetType().Name ?? "nothing";
        float searchTime = Player.Looting.SearchTime(crate);
        Intent("Interact", true);
        await Seconds(0.3f);
        InventoryScreen screen = Screen();
        var log = new StringBuilder($"focus={focus}, search time {searchTime:0.0} s\n");
        log.AppendLine($"after 0.3 s: screen open={screen.IsOpen}, loot={screen.Loot?.Label}, timeScale={Time.timeScale}, " +
                       $"hidden {crate.HiddenCount}/{crate.Items.UsedSlots}, revealing slot {Player.Looting.RevealingSlot} ({Player.Looting.Progress:P0})");
        int lastHidden = -1;
        for (int i = 0; i < crate.Items.Count; i++)
            if (crate.IsHidden(i))
                lastHidden = i;
        if (lastHidden >= 0)
        {
            ItemInstance item = crate.Items[lastHidden];
            int free = Inv.Backpack.FindFreeSlot(item);
            Drag(screen, crate.Items, lastHidden, Inv.Backpack, free);
            Click(screen, crate.Items, lastHidden, PointerEventData.InputButton.Right);
            PressButton(screen, "_takeAllButton");
            log.AppendLine($"drag / right-click / take all on hidden slot {lastHidden}: still in the crate={crate.Items[lastHidden] == item}");
            Refresh(screen);
            ItemSlotView view = View(screen, crate.Items, lastHidden);
            log.AppendLine($"hidden slot view: '?' shown={view.transform.Find("Hidden").gameObject.activeSelf}, icon shown={view.transform.Find("Icon").GetComponent<Image>().enabled}");
        }
        await Seconds(searchTime + 0.6f);
        Intent("LookAt", null);
        log.AppendLine($"after the search: screen open={screen.IsOpen}, timeScale={Time.timeScale}, crate searched={crate.Searched}, hidden {crate.HiddenCount}");
        log.AppendLine($"crate: {Contents(crate.Items)}");
        log.Append($"events: {Drain()}");
        return log.ToString();
    }

    // Starts searching the crate and returns halfway, for a capture of the reveal.
    public static async Task<string> StartCrateSearch()
    {
        LootContainer crate = Crate();
        Teleport(crate.transform.position + Flat(crate.transform.forward) * -1.6f);
        Intent("LookAt", (Vector3?)(crate.transform.position + Vector3.up * 0.35f));
        await Seconds(0.4f);
        Intent("Interact", true);
        await Seconds(1.5f);
        return $"searching={Player.Looting.IsSearching} slot {Player.Looting.RevealingSlot} progress={Player.Looting.Progress:P0}, hidden {crate.HiddenCount}";
    }

    // With the crate open: right-click take, drag-merge, drag-swap a gun, drag to a quick slot, a refused backpack move,
    // take all, sort, and drag-out-to-throw. Everything through pointer events on the slot views.
    public static string DragDrop()
    {
        InventoryScreen screen = Screen();
        if (!screen.IsOpen)
            return "screen not open";
        var log = new StringBuilder();
        ItemContainer backpack = Inv.Backpack;
        ItemContainer loot = screen.Loot.Items;

        int lootTokens = Find(loot, d => d.AutoCollect);
        if (lootTokens >= 0)
        {
            int carried = Inv.CountOf(loot[lootTokens].Definition);
            Click(screen, loot, lootTokens, PointerEventData.InputButton.Right);
            log.AppendLine($"right-click loot tokens: loot slot now {loot[lootTokens]?.Count.ToString() ?? "empty"}, carried {carried} -> {Inv.CountOf(Find(backpack, d => d.AutoCollect) >= 0 ? backpack[Find(backpack, d => d.AutoCollect)].Definition : null)}");
        }

        int lootPistol = Find(loot, d => d.name == "PistolAmmo");
        int bagPistol = Find(backpack, d => d.name == "PistolAmmo");
        if (lootPistol >= 0 && bagPistol >= 0)
        {
            int before = backpack[bagPistol].Count;
            Drag(screen, loot, lootPistol, backpack, bagPistol);
            log.AppendLine($"drag loot pistol ammo onto the carried stack: {before} -> {backpack[bagPistol]?.Count}, loot slot {loot[lootPistol]?.Count.ToString() ?? "empty"}");
        }

        int lootRifle = Find(loot, d => d.name == "RifleAmmo");
        int free = backpack.FindFreeSlot(loot[lootRifle >= 0 ? lootRifle : 0]);
        if (lootRifle >= 0 && free >= 0)
        {
            Drag(screen, loot, lootRifle, backpack, free);
            log.AppendLine($"drag loot rifle ammo to empty backpack slot {free}: {backpack[free]?.Definition.name} x{backpack[free]?.Count}");
        }

        int spareGun = Find(backpack, d => d.Category == ItemCategory.Gun);
        if (spareGun >= 0)
        {
            ItemInstance bagGun = backpack[spareGun];
            ItemInstance activeGun = Inv.Hotbar[0];
            Drag(screen, backpack, spareGun, Inv.Hotbar, 0);
            log.AppendLine($"drag spare gun {bagGun.Definition.name} onto active slot 1 ({activeGun?.Definition.name}): slot 1 = {Inv.Hotbar[0]?.Definition.name}, backpack slot = {backpack[spareGun]?.Definition.name}");
        }

        int medic = Find(backpack, d => d is ConsumableDefinition);
        int quick = Player.Equipment.SlotOf(HotbarKey.Key3);
        if (medic >= 0)
        {
            Drag(screen, backpack, medic, Inv.Hotbar, quick);
            log.AppendLine($"drag medic box to quick slot (key 3): slot = {Inv.Hotbar[quick]?.Definition.DisplayName} x{Inv.Hotbar[quick]?.Count}");
        }
        int ammo = Find(backpack, d => d.Category == ItemCategory.Ammo);
        if (ammo >= 0)
        {
            Drag(screen, backpack, ammo, Inv.Hotbar, quick + 1);
            log.AppendLine($"drag ammo to quick slot (key 4): refused={Inv.Hotbar[quick + 1] == null}");
        }

        int worn = Find(Inv.Gear, d => d is BackpackDefinition);
        if (worn >= 0)
        {
            int extra = backpack.Count - 1;
            if (backpack[extra] == null && ammo >= 0 && backpack[ammo] != null)
                ItemContainer.Move(backpack, ammo, backpack, extra);
            Drag(screen, Inv.Gear, worn, backpack, 0);
            log.AppendLine($"drag the worn backpack off with items in its slots: still worn={Inv.WornBackpack != null}, message='{Message(screen)}'");
            if (backpack[extra] != null)
                ItemContainer.Move(backpack, extra, backpack, backpack.FindFreeSlot(backpack[extra]));
        }

        PressButton(screen, "_takeAllButton");
        log.AppendLine($"take all: loot left {loot.UsedSlots}, crate {Contents(loot)}");

        PressButton(screen, "_sortButton");
        log.AppendLine($"sort: {Contents(backpack)}");

        int lastStack = -1;
        for (int i = backpack.Count - 1; i >= 0 && lastStack < 0; i--)
            if (backpack[i] != null && backpack[i].Definition.Category == ItemCategory.Ammo)
                lastStack = i;
        if (lastStack >= 0)
        {
            ItemDefinition thrown = backpack[lastStack].Definition;
            int before = Inv.CountOf(thrown);
            DragOut(screen, backpack, lastStack);
            log.AppendLine($"drag {thrown.name} onto the backdrop: carried {before} -> {Inv.CountOf(thrown)}, on the ground: {Latest(thrown) != null}");
        }
        log.AppendLine($"weight {Inv.Weight:0.0}/{Inv.MaxWeight:0.0} kg, backpack {backpack.UsedSlots}/{backpack.Count}");
        return log.ToString();
    }

    public static async Task<string> CloseAndHold()
    {
        InventoryScreen screen = Screen();
        screen.Close();
        await Frames(2);
        string closed = $"closed: open={screen.IsOpen}, timeScale={Time.timeScale}, gameplay input={Player.IntentSource != null}";
        int quick = Player.Equipment.SlotOf(HotbarKey.Key3);
        Player.Equipment.Select(quick);
        await Seconds(1f);
        return closed + $"\nkey 3: holding {Player.Equipment.Switcher.Held?.Definition.DisplayName}, model shown={Player.HeldItem != null && Player.HeldItem.gameObject.activeInHierarchy}";
    }

    // Spawns goblins beside the player and kills them; reports which bodies hold loot.
    public static async Task<string> Goblins()
    {
        Listen();
        var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
        var spawns = scope.Container.Resolve<SpawnService>();
        var settings = scope.Container.Resolve<EnemySystemSettings>();
        EnemySpawnCard card = settings.Cards.Find(c => c != null && c.name.StartsWith("Goblin"));
        var bodies = new List<EnemyBody>();
        Vector3 origin = Player.transform.position + Flat(Player.transform.forward) * 4f;
        for (int i = 0; i < 6; i++)
        {
            Spawnable spawned = spawns.Spawn(card, origin + Player.transform.right * (i - 2.5f) * 1.8f, Quaternion.identity);
            if (spawned != null && spawned.TryGetComponent(out EnemyBody body))
                bodies.Add(body);
        }
        await Seconds(1.5f);
        foreach (EnemyBody body in bodies)
            body.TakeDamage(new DamageInfo(9999f, DamageType.Gunshot, body.transform.position + Vector3.up));
        await Seconds(2f);
        var log = new StringBuilder();
        var manager = scope.Container.Resolve<EnemyBodyManager>();
        foreach (EnemyBody body in bodies)
            log.AppendLine($"{body.name}: dead={body.IsDead}, holds loot={body.HoldsLoot}, searchable={body.Loot != null && body.Loot.Searchable}: {(body.Loot != null ? Contents(body.Loot.Items) : "-")}");
        log.AppendLine($"loot bodies tracked: {manager.LootBodies.Count}");
        return log.ToString();
    }

    // Bodies lying with loot stop animating once settled (their Animator turns off); the rest keep the pool lean.
    public static async Task<string> BodiesSettle()
    {
        var manager = Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve<EnemyBodyManager>();
        await Seconds(7f);
        var log = new StringBuilder();
        foreach (EnemyBody body in manager.LootBodies)
            log.AppendLine($"{body.name}: animator on={body.GetComponentInChildren<Animator>().enabled}");
        log.AppendLine($"lying with loot: {manager.LootBodies.Count}");
        return log.ToString();
    }

    // Dawn: every body still holding loot gives it up and sinks.
    public static async Task<string> Dawn()
    {
        var manager = Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve<EnemyBodyManager>();
        var bodies = new List<EnemyBody>(manager.LootBodies);
        manager.ExpireLoot();
        await Seconds(3f);
        var log = new StringBuilder($"dawn: lying with loot {bodies.Count} -> {manager.LootBodies.Count}");
        log.AppendLine();
        foreach (EnemyBody body in bodies)
            log.AppendLine($"{body.name}: active={body.gameObject.activeSelf}, holds loot={body.HoldsLoot}");
        return log.ToString();
    }

    // Looks at the first body with loot, presses E, waits out the search: the screen opens with its loot.
    public static async Task<string> LootGoblin()
    {
        Listen();
        var manager = Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve<EnemyBodyManager>();
        if (manager.LootBodies.Count == 0)
            return "no body holds loot";
        EnemyBody body = manager.LootBodies[0];
        Vector3 at = body.transform.position;
        Teleport(at + Flat(Player.transform.position - at).normalized * 1.5f);
        Intent("LookAt", (Vector3?)(at + Vector3.up * 0.3f));
        await Seconds(0.4f);
        string focus = Player.Interactor.Focus?.GetType().Name ?? "nothing";
        string prompt = Player.Interactor.HasPrompt ? $"{Player.Interactor.Prompt.Verb} {Player.Interactor.Prompt.Target}" : "no prompt";
        float time = Player.Looting.SearchTime(body.Loot);
        Intent("Interact", true);
        await Seconds(0.3f);
        InventoryScreen screen = Screen();
        string early = $"open after 0.3 s={screen.IsOpen}, hidden {body.Loot.HiddenCount}";
        await Seconds(time + 0.5f);
        Intent("LookAt", null);
        return $"{body.name}: focus={focus}, prompt '{prompt}', search {time:0.0} s, {early}; then screen open={screen.IsOpen}, loot={screen.Loot?.Label}, " +
               $"hidden {body.Loot.HiddenCount}: {Contents(body.Loot.Items)}";
    }

    // Opens the last body with loot and returns partway through its reveal, for a capture.
    public static async Task<string> StartBodySearch()
    {
        var manager = Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve<EnemyBodyManager>();
        if (manager.LootBodies.Count == 0)
            return "no body holds loot";
        EnemyBody body = manager.LootBodies[0];
        foreach (EnemyBody candidate in manager.LootBodies)
            if (candidate.Loot.HiddenCount > body.Loot.HiddenCount)
                body = candidate;
        Screen().Close();
        Vector3 at = body.transform.position;
        Teleport(at + Flat(Player.transform.position - at).normalized * 1.5f);
        Intent("LookAt", (Vector3?)(at + Vector3.up * 0.3f));
        await Seconds(0.4f);
        Intent("Interact", true);
        await Seconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "claude_reveal.png"));
        await Seconds(0.2f);
        Intent("LookAt", null);
        return $"{body.name}: open={Screen().IsOpen}, revealing slot {Player.Looting.RevealingSlot} ({Player.Looting.Progress:P0}), hidden {body.Loot.HiddenCount}";
    }

    // Takes everything from the open body, closes, and watches it sink.
    public static async Task<string> EmptyGoblin()
    {
        Listen();
        InventoryScreen screen = Screen();
        LootContainer loot = screen.Loot;
        if (loot == null)
            return "no body open";
        EnemyBody body = loot.GetComponent<EnemyBody>();
        PressButton(screen, "_takeAllButton");
        string taken = $"take all: left {loot.Items.UsedSlots}, holds loot={body.HoldsLoot}, searchable={loot.Searchable}";
        screen.Close();
        float y0 = body.Visual.localPosition.y;
        await Seconds(2.5f);
        float y1 = body.Visual.localPosition.y;
        await Seconds(3f);
        return $"{taken}\nafter 2.5 s: sunk {y0 - y1:0.00} m; after 5.5 s: active={body.gameObject.activeSelf}\n{Describe()}";
    }

    // During a raid the screen doesn't pause, and a hit closes it.
    public static async Task<string> RaidLoot()
    {
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var started = resolver.Resolve<IPublisher<RaidStarted>>();
        var ended = resolver.Resolve<IPublisher<RaidEnded>>();
        var damage = resolver.Resolve<DamageService>();
        InventoryScreen screen = Screen();
        var log = new StringBuilder();

        screen.Open(null);
        await Frames(2);
        log.AppendLine($"at home: open={screen.IsOpen}, timeScale={Time.timeScale}");
        screen.Close();
        await Frames(2);

        started.Publish(new RaidStarted(99));
        screen.Open(null);
        await Frames(5);
        log.AppendLine($"raid: open={screen.IsOpen}, timeScale={Time.timeScale}");
        float health = Player.Health.Current;
        Player.Invulnerable = false;
        damage.Deal(Player, new DamageInfo(5f, DamageType.Melee, Player.transform.position + Vector3.up));
        Player.Invulnerable = true;
        await Frames(2);
        log.AppendLine($"hit for {health - Player.Health.Current:0} hp: open={screen.IsOpen}, timeScale={Time.timeScale}");
        Player.Health.Heal(100f);
        ended.Publish(new RaidEnded(99, RaidEndReason.Stopped));
        screen.Open(null);
        await Frames(2);
        log.Append($"raid over: open={screen.IsOpen}, timeScale={Time.timeScale}");
        screen.Close();
        return log.ToString();
    }

    public static string Finish()
    {
        if (Player == null)
            return "no player";
        Player.IntentSource = null;
        Player.Invulnerable = false;
        Player.IgnoredByEnemies = false;
        Screen()?.Close();
        return "finished";
    }

    // ---- helpers ----

    static InventoryScreen Screen() => Object.FindAnyObjectByType<InventoryScreen>(FindObjectsInactive.Include);

    static void Teleport(Vector3 position)
    {
        var controller = Player.GetComponent<CharacterController>();
        controller.enabled = false;
        if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point;
        Player.transform.position = position + Vector3.up * 0.05f;
        controller.enabled = true;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    static async Task Seconds(float seconds) => await Task.Delay(Mathf.RoundToInt(seconds * 1000f));

    static async Task Frames(int count)
    {
        int end = Time.frameCount + count;
        while (Time.frameCount < end)
            await Task.Yield();
    }

    static int Find(ItemContainer container, Func<ItemDefinition, bool> match)
    {
        for (int i = 0; i < container.Count; i++)
            if (container[i] != null && match(container[i].Definition))
                return i;
        return -1;
    }

    static WorldItem Latest(ItemDefinition definition)
    {
        foreach (WorldItem item in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude))
            if (!item.IsTaken && item.Item != null && item.Item.Definition == definition && item.WaitsForPlayerToLeave)
                return item;
        return null;
    }

    // Each step is its own compiled assembly: it subscribes to the player's events itself and drops them in Drain.
    static void Listen()
    {
        _events.Clear();
        _onPicked = (item, count) => _events.Add($"+{count} {item.DisplayName}");
        _onBlocked = item => _events.Add($"BLOCKED {item.DisplayName}");
        _onOpened = c => _events.Add($"OPENED {c.Label}");
        Player.Equipment.PickedUp += _onPicked;
        Player.Equipment.PickUpBlocked += _onBlocked;
        Player.Looting.Opened += _onOpened;
    }

    static string Drain()
    {
        if (_onPicked != null)
        {
            Player.Equipment.PickedUp -= _onPicked;
            Player.Equipment.PickUpBlocked -= _onBlocked;
            Player.Looting.Opened -= _onOpened;
            _onPicked = null;
        }
        string events = string.Join(", ", _events);
        _events.Clear();
        return events;
    }

    // The intent source was made by Start's own assembly: set its fields by name.
    static void Intent(string field, object value)
    {
        object source = Player.IntentSource;
        source?.GetType().GetField(field)?.SetValue(source, value);
    }

    static string Contents(ItemContainer container)
    {
        var parts = new List<string>();
        for (int i = 0; i < container.Count; i++)
            parts.Add(container[i] == null ? "-" : $"{container[i].Definition.name}x{container[i].Count}");
        return "[" + string.Join(" ", parts) + "]";
    }

    static string Describe()
    {
        Inventory inv = Inv;
        return $"hotbar {Contents(inv.Hotbar)}\nbackpack {Contents(inv.Backpack)}\ngear {Contents(inv.Gear)}\n" +
               $"weight {inv.Weight:0.0}/{inv.MaxWeight:0.0} kg, ammo reserve pistol={Player.Ammo.Count(Ammo("PistolAmmo"))}";
    }

    static MB13.Aipocalypse.Core.WeaponSystem.AmmoType Ammo(string name) =>
        UnityEditor.AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.WeaponSystem.AmmoType>($"Assets/_Game/Content/Weapons/_Shared/Ammo/{name}.asset");

    // The view showing slot `index` of `container`.
    static ItemSlotView View(InventoryScreen screen, ItemContainer container, int index)
    {
        var slots = (System.Collections.IList)typeof(InventoryScreen).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
        foreach (object slot in slots)
        {
            Type type = slot.GetType();
            if (type.GetField("Container").GetValue(slot) == container && (int)type.GetField("Index").GetValue(slot) == index)
                return (ItemSlotView)type.GetField("View").GetValue(slot);
        }
        throw new Exception($"no view for slot {index}");
    }

    static PointerEventData Pointer(PointerEventData.InputButton button, GameObject over) => new(EventSystem.current)
    {
        button = button,
        position = over != null ? (Vector2)over.transform.position : Vector2.zero,
        pointerCurrentRaycast = new RaycastResult { gameObject = over },
    };

    static void Click(InventoryScreen screen, ItemContainer container, int index, PointerEventData.InputButton button)
    {
        ItemSlotView view = View(screen, container, index);
        ExecuteEvents.Execute(view.gameObject, Pointer(button, view.gameObject), ExecuteEvents.pointerClickHandler);
        Refresh(screen);
    }

    static void Drag(InventoryScreen screen, ItemContainer from, int fromIndex, ItemContainer to, int toIndex)
    {
        ItemSlotView source = View(screen, from, fromIndex);
        ItemSlotView target = View(screen, to, toIndex);
        PointerEventData pointer = Pointer(PointerEventData.InputButton.Left, target.gameObject);
        ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.beginDragHandler);
        ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.dropHandler);
        ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.endDragHandler);
        Refresh(screen);
    }

    static void DragOut(InventoryScreen screen, ItemContainer from, int fromIndex)
    {
        ItemSlotView source = View(screen, from, fromIndex);
        var backdrop = (GameObject)typeof(InventoryScreen).GetField("_backdrop", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
        PointerEventData pointer = Pointer(PointerEventData.InputButton.Left, backdrop);
        ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.beginDragHandler);
        ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.endDragHandler);
        Refresh(screen);
    }

    static void PressButton(InventoryScreen screen, string field)
    {
        var button = (UnityEngine.UI.Button)typeof(InventoryScreen).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
        button.onClick.Invoke();
        Refresh(screen);
    }

    // The screen refreshes in its Update; while paused that still runs, but tests read it at once.
    static void Refresh(InventoryScreen screen) =>
        typeof(InventoryScreen).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(screen, null);

    static string Message(InventoryScreen screen)
    {
        var text = (TMPro.TMP_Text)typeof(InventoryScreen).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
        return text != null && text.enabled ? text.text : "";
    }
}
