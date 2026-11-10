using System.Linq;
using System.Text;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Play-mode probes for the save system (WorldMap, Play pressed in the editor). Run step by step with `script run`:
// Status → Morning (autosave) → Mutate → Save 2 → Load 2 → Status (compare) → Pause / Slots / MainMenu / Continue.
public static class SavePlayTest
{
    static IObjectResolver Root => VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;
    static GameSession Session => Root.Resolve<GameSession>();
    static IObjectResolver Scene => Object.FindAnyObjectByType<GameLifetimeScope>()?.Container;

    public static string Status()
    {
        Application.runInBackground = true;
        var text = new StringBuilder();
        GameSession session = Session;
        text.AppendLine($"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} slot={session.Slot} inWorld={session.InWorld} " +
            $"play={session.PlayTime:0.0}s canSave={session.CanSave(out string why)} {why}");
        if (Scene != null && Scene.TryResolve(out WorldClock clock))
            text.AppendLine($"clock day {clock.Now.Day} {clock.Now.Hour:0.00}h phase {clock.Phase}");
        if (Root.Resolve<PlayerRegistry>().Current is Player player)
        {
            text.AppendLine($"player pos {player.transform.position} yaw {player.transform.eulerAngles.y:0} pitch {player.Pitch:0} hp {player.Health.Current:0}");
            Inventory inventory = player.Equipment.Inventory;
            text.AppendLine($"hotbar [{Items(inventory.Hotbar)}] selected {player.Equipment.SelectedIndex}");
            text.AppendLine($"backpack [{Items(inventory.Backpack)}] gear [{Items(inventory.Gear)}]");
        }
        if (Scene != null && Scene.TryResolve(out BarrierRegistry barriers))
        {
            var damaged = barriers.Segments.Where(s => s.HealthShare < 1f || s.IsBroken).Select(s => $"{s.name}:{s.RemainingHealth:0}{(s.IsBroken ? "X" : "")}");
            var open = barriers.Segments.Where(s => s.Gate != null && s.Gate.IsOpen).Select(s => s.name);
            text.AppendLine($"damaged fences [{string.Join(", ", damaged)}] open gates [{string.Join(", ", open)}]");
        }
        foreach (LootContainer crate in Root.Resolve<LootRegistry>().Containers.Where(c => c.TryGetComponent(out SaveId id) && id.HasId))
            text.AppendLine($"crate {crate.name}@{crate.transform.position}: [{Items(crate.Items)}] hidden {crate.HiddenCount}");
        WorldItem[] worldItems = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude);
        text.AppendLine($"world items {worldItems.Length}: {string.Join(", ", worldItems.Take(12).Select(w => $"{w.Item?.Definition.name}x{w.Item?.Count}@{w.transform.position.x:0},{w.transform.position.z:0}"))}");
        SaveService saves = Root.Resolve<SaveService>();
        foreach (int slot in saves.Slots)
            text.AppendLine($"{SaveFormat.SlotName(slot)}: {SaveFormat.Summary(saves.Info(slot))}");
        return text.ToString();
    }

    static string Items(ItemContainer container) =>
        string.Join(" ", Enumerable.Range(0, container.Count).Select(i => container[i] == null ? "-"
            : container[i] is MB13.Aipocalypse.Core.WeaponSystem.GunInstance gun ? $"{gun.Definition.name}(r{gun.Rounds}{(gun.IsJammed ? "J" : "")})"
            : $"{container[i].Definition.name}x{container[i].Count}"));

    // Jumps to the next Morning: day 2 → the autosave should write the current slot.
    public static string Morning()
    {
        Scene.Resolve<WorldClock>().JumpToPhase(DayPhase.Morning);
        return "jumped to " + Scene.Resolve<WorldClock>().Now.Day;
    }

    // Jumps to the next start of this phase (Dusk, Dawn, Morning…).
    public static string Jump(string phase)
    {
        var clock = Scene.Resolve<WorldClock>();
        clock.JumpToPhase(System.Enum.Parse<DayPhase>(phase));
        return $"day {clock.Now.Day} {clock.Now.Hour:0.00} {clock.Phase}";
    }

    // Changes things every section saves.
    public static string Mutate()
    {
        var clock = Scene.Resolve<WorldClock>();
        clock.SetTime(WorldTime.At(3, 10.5f));
        var player = (Player)Root.Resolve<PlayerRegistry>().Current;
        foreach (WorldItem item in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude))
            if (item.Item != null && (item.Item.Definition.name is "Pistol" or "PumpShotgun" or "Hammer" or "Backpack"))
                item.Collect(player.gameObject);
        var pistol = (MB13.Aipocalypse.Core.WeaponSystem.GunInstance)Enumerable.Range(0, player.Equipment.Hotbar.Count)
            .Select(i => player.Equipment.Hotbar[i]).FirstOrDefault(i => i is MB13.Aipocalypse.Core.WeaponSystem.GunInstance);
        pistol?.SetRounds(3);
        pistol?.SetJammed(true);
        player.RestoreSaved(player.transform.position + new Vector3(4f, 0.5f, 3f), 123f, 15f, 61f);
        Inventory inventory = player.Equipment.Inventory;
        ItemInstance thrown = inventory.Backpack.Remove(6);
        if (thrown != null)
            player.ThrowAway(thrown);
        BarrierSegment[] segments = Scene.Resolve<BarrierRegistry>().Segments.ToArray();
        segments[0].DealDamage(37f);
        segments[1].DealDamage(100000f);
        BarrierSegment gate = segments.FirstOrDefault(s => s.Gate != null);
        gate?.Gate.Toggle(gate.transform.position + gate.transform.forward);
        LootContainer crate = Root.Resolve<LootRegistry>().Containers.First(c => c.TryGetComponent(out SaveId id) && id.HasId);
        crate.RevealNext();
        crate.RevealNext();
        ItemInstance taken = crate.Items.Remove(crate.Items[0] != null ? 0 : 1);
        if (taken != null)
            inventory.TryAdd(taken, out _);
        return "mutated\n" + Status();
    }

    public static string Save(int slot) => Session.Save(slot, out string message) + " " + message;

    public static string Load(int slot) => Session.Load(slot, out string message) + " " + message;

    // Main-thread cost of a save, by stage, `count` times into `slot` (the slot is overwritten). The disk write runs
    // on the thread pool, so "save" is only what the frame pays.
    public static string Timing(int slot, int count)
    {
        SaveService saves = Root.Resolve<SaveService>();
        SaveRegistry sections = Root.Resolve<SaveRegistry>();
        saves.TryRead(Session.Slot, out SaveData carry, out _);
        var text = new StringBuilder();
        var watch = new System.Diagnostics.Stopwatch();
        for (int i = 0; i < count; i++)
        {
            watch.Restart();
            SaveData data = saves.Create(carry);
            double create = watch.Elapsed.TotalMilliseconds;
            sections.Capture(data);
            double capture = watch.Elapsed.TotalMilliseconds - create;
            watch.Restart();
            Session.Save(slot, out _);
            double save = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            saves.Info(slot);
            double info = watch.Elapsed.TotalMilliseconds;
            text.AppendLine($"create {create:0.0} capture {capture:0.0} | Session.Save {save:0.0} | Info {info:0.0} ms");
        }
        return text.ToString();
    }

    public static string Pause()
    {
        var menu = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        menu.Open();
        return $"pause open={menu.IsOpen} timeScale={Time.timeScale}";
    }

    // Opens the slot list from the pause menu (mode: Save or Load).
    public static string Slots(string mode)
    {
        var menu = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        var panel = menu.GetComponentInChildren<SaveSlotsPanel>(true);
        menu.transform.Find("Page").gameObject.SetActive(false);
        panel.Open(mode == "Save" ? SlotsMode.Save : SlotsMode.Load);
        return "slots open " + mode;
    }

    public static string ClosePause()
    {
        var menu = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        menu.Close();
        return $"pause open={menu.IsOpen} timeScale={Time.timeScale}";
    }

    public static string MainMenu() => Session.ReturnToMainMenu().ToString();

    public static string MainMenuStatus()
    {
        var screen = Object.FindAnyObjectByType<MainMenuScreen>();
        return screen == null ? "no main menu" : $"main menu up; cursor {Cursor.lockState} visible {Cursor.visible}\n" + Status();
    }

    public static string DeleteAll()
    {
        SaveService saves = Root.Resolve<SaveService>();
        int deleted = 0;
        foreach (int slot in saves.Slots)
            if (saves.Delete(slot))
                deleted++;
        return $"{deleted} deleted ({saves.FolderPath})";
    }
}
