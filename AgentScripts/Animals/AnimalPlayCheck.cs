// Play-mode check of the farm animals in WorldMap (enter Play first, editor auto tick on):
//   unity command script run --file AgentScripts/Animals/AnimalPlayCheck.cs --entry AnimalPlayCheck.Run --timeout 300
// Steps: the herd is injected and penned; it grazes inside the pen; Tokens held show the coin, walking up takes nothing,
// and E (Interact) bursts them out to the player; opening the pen gate (and the home gate) sends them to the pasture, where they earn at the roaming rate;
// the bell brings them home, hurried; the pen gate shuts itself once all are in; at night they sleep where they are;
// a save section capture and restore puts an animal, its Tokens and the gate back.
// Animals are hurried (`animal.hurry`) where a walk at their own pace would take minutes. Leaves the clock where it ends.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RaidSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public static class AnimalPlayCheck
{
    public static async Task<string> Run()
    {
        if (!Application.isPlaying)
            return "enter play mode in WorldMap first";
        Application.runInBackground = true;
        LifetimeScope scope = LifetimeScope.Find<GameLifetimeScope>();
        IObjectResolver c = scope != null ? scope.Container : null;
        if (c == null || !c.TryResolve(out AnimalHerd herd) || !c.TryResolve(out AnimalManager manager))
            return "no AnimalHerd / AnimalManager (no AnimalInstaller on the GameLifetimeScope, or it didn't install)";
        var clock = c.Resolve<WorldClock>();
        var settings = c.Resolve<AnimalSettings>();
        var commands = c.Resolve<CommandRegistry>();
        IPlayer player = c.Resolve<PlayerRegistry>().Current;
        if (player == null)
            return "no player";
        var log = new StringBuilder();
        int failed = 0;
        void Check(string what, bool ok, string detail = null)
        {
            if (!ok)
                failed++;
            log.AppendLine($"{(ok ? "ok  " : "FAIL")} {what}{(detail != null ? ": " + detail : "")}");
        }
        string Where() => string.Join(" ", herd.Animals.Select(a => $"{a.name}({(a.IsHome ? "pen" : a.IsRoaming ? "pasture" : "out")})"));
        void Unpause()
        {
            foreach (PauseMenu menu in Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Exclude))
                if (menu.IsOpen)
                    menu.Close();
        }

        commands.Execute("cheat.god on");
        commands.Execute("cheat.notarget on");
        Unpause();
        if (clock.Now.Hour < 8f || clock.Now.Hour > 12f)
            clock.JumpTo(9f);
        await Seconds(1f);

        // 1. Injected, penned.
        Check("herd registered", herd.Animals.Count == 6, $"{herd.Animals.Count} animals; pen {(herd.Pen != null)}, pasture {(herd.Pasture != null)}, enabled {herd.Enabled}");
        Check("all start in the pen", herd.CountHome() == herd.Animals.Count, Where());
        Check("penned, awake", !herd.Orders.Released && !herd.Asleep, $"hour {clock.Now.Hour:0.0}");

        // 2. Grazing inside the pen.
        var before = herd.Animals.ToDictionary(a => a, a => a.transform.position);
        float moved = 0f;
        bool ate = false;
        float topSpeed = 0f;
        for (int i = 0; i < 40; i++)
        {
            await Seconds(0.5f);
            ate |= herd.Animals.Any(a => a.Eating);
            topSpeed = Mathf.Max(topSpeed, herd.Animals.Max(a => a.Mover.Velocity.magnitude));
        }
        foreach (Animal animal in herd.Animals)
            moved = Mathf.Max(moved, Vector3.Distance(before[animal], animal.transform.position));
        Check("they walk about the pen", moved > 1f && topSpeed > 0.3f, $"furthest moved {moved:0.0} m, top speed {topSpeed:0.00} m/s");
        Check("they eat", ate);
        Check("none left the pen", herd.CountHome() == herd.Animals.Count, Where());

        // 3. Tokens: earned over game hours, shown, handed over to the player.
        float heldBefore = herd.Animals.Sum(a => a.Earnings.Held);
        Check("earned something in 20 s of morning", heldBefore > 0f, $"{heldBefore:0.00} Tokens across the herd");
        commands.Execute("animal.fill 10");
        await Seconds(0.3f);
        GameObject[] markers = herd.Animals.Select(a => a.transform.Find("CoinMarker").gameObject).ToArray();
        Check("coin markers show", markers.All(m => m.activeSelf));
        int tokensBefore = player.Equipment.Inventory.CountOf(settings.Tokens);
        Animal nearest = herd.Animals[0];
        player.Teleport(nearest.transform.position + (herd.Pen.Center - nearest.transform.position).normalized * 1.5f, 0f);
        await Seconds(2f);
        Check("walking up takes nothing", nearest.HasTokens && player.Equipment.Inventory.CountOf(settings.Tokens) == tokensBefore);
        Check("it offers Collect", nearest.TryGetPrompt(player.Transform.gameObject, out var collectPrompt) && collectPrompt.Verb == "Collect",
            $"{collectPrompt.Verb} {collectPrompt.Target}");
        nearest.Interact(player.Transform.gameObject);
        await Seconds(3f);
        int tokensAfter = player.Equipment.Inventory.CountOf(settings.Tokens);
        Check("E collects", tokensAfter >= tokensBefore + 10, $"Tokens {tokensBefore} -> {tokensAfter}, {nearest.name} holds {nearest.Earnings.Held:0.00}");
        Check("its marker is gone", !nearest.transform.Find("CoinMarker").gameObject.activeSelf);
        commands.Execute("animal.collect");
        await Seconds(3f);
        log.AppendLine($"     after animal.collect: {player.Equipment.Inventory.CountOf(settings.Tokens)} Tokens");

        // 4. Release: pen gate and home gate open, the herd goes to the pasture.
        BarrierSegment homeGate = Object.FindObjectsByType<BarrierSegment>(FindObjectsInactive.Exclude).FirstOrDefault(s => s.Gate != null);
        if (homeGate != null && !homeGate.Gate.IsOpen)
            homeGate.Gate.Toggle(homeGate.transform.position - homeGate.transform.forward);
        PenGate gate = herd.Pen.Gate;
        gate.Interact(player.Transform.gameObject);
        await Seconds(1.5f);
        Check("opening the pen gate releases", gate.IsOpen && herd.Orders.Released);
        commands.Execute("animal.hurry");
        float deadline = Time.realtimeSinceStartup + 90f;
        while (Time.realtimeSinceStartup < deadline && herd.Animals.Count(a => a.IsRoaming) < herd.Animals.Count)
        {
            await Seconds(1f);
            // A hurry ends at each stop: keep them running, a walk there takes over a minute.
            commands.Execute("animal.hurry");
        }
        Check("the herd reaches the pasture", herd.Animals.All(a => a.IsRoaming), Where());
        Animal cow = herd.Animals.First(a => !a.Definition.FollowsLeader);
        float cowHeld = cow.Earnings.Held;
        double hourBefore = clock.Now.TotalHours;
        await Seconds(8f);
        float perHour = (cow.Earnings.Held - cowHeld) / (float)(clock.Now.TotalHours - hourBefore);
        float expected = cow.Definition.TokensPerDayRoaming / AnimalEarnings.DayLength(settings.Awake);
        Check("roaming rate on the pasture", Mathf.Abs(perHour - expected) < expected * 0.2f, $"{perHour:0.00} Tokens per game hour, expected {expected:0.00}");
        Animal leader = herd.LeaderOf(herd.Animals.First(a => a.Definition.FollowsLeader).Definition);
        float spread = herd.Animals.Where(a => a.Definition == leader.Definition).Max(a => Vector3.Distance(a.transform.position, leader.transform.position));
        log.AppendLine($"     flock spread round its leader: {spread:0.0} m (FlockRadius {leader.Definition.FlockRadius})");

        // 5. The bell: called home, hurried by the player, the pen gate shuts itself.
        AnimalBell bell = Object.FindAnyObjectByType<AnimalBell>();
        Check("an animal out on the pasture offers no Hurry before the bell", !cow.TryGetPrompt(player.Transform.gameObject, out _));
        bell.Interact(player.Transform.gameObject);
        await Seconds(0.5f);
        Check("the bell calls home", herd.Orders.Called && !herd.Orders.Released);
        Check("a called animal offers Hurry", cow.TryGetPrompt(player.Transform.gameObject, out var prompt), $"{prompt.Verb} {prompt.Target}");
        Vector3 cowStart = cow.transform.position;
        await Seconds(6f);
        float walked = Vector3.Distance(cowStart, cow.transform.position) / 6f;
        Check("the cow walks home at its walking pace", walked > 0.4f && walked < cow.Definition.WalkSpeed * 1.3f, $"{walked:0.00} m/s (WalkSpeed {cow.Definition.WalkSpeed})");
        cow.Interact(player.Transform.gameObject);
        int hurried = herd.Animals.Count(a => a.Hurrying);
        Check("hurrying one hurries those nearby and the flock", hurried >= 2, $"{hurried} hurrying");
        commands.Execute("animal.hurry");
        deadline = Time.realtimeSinceStartup + 90f;
        while (Time.realtimeSinceStartup < deadline && (herd.CountHome() < herd.Animals.Count || gate.IsOpen))
            await Seconds(1f);
        Check("the herd is home", herd.CountHome() == herd.Animals.Count, Where());
        Check("the pen gate shut itself", !gate.IsOpen && !herd.Orders.GateOpen);
        Check("nobody is still hurrying", herd.Animals.All(a => !a.Hurrying));

        // 6. Night: asleep where they stand, earning nothing.
        clock.JumpTo(21f);
        if (c.TryResolve(out RaidDirector raids))
        {
            await Seconds(0.5f);
            raids.Stop();
        }
        await Seconds(3f);
        Check("asleep at night", herd.Asleep && herd.Animals.All(a => a.Sleeping), $"hour {clock.Now.Hour:0.0}");
        float nightHeld = herd.Animals.Sum(a => a.Earnings.Held);
        await Seconds(4f);
        Check("nothing earned asleep", Mathf.Approximately(nightHeld, herd.Animals.Sum(a => a.Earnings.Held)));
        clock.JumpTo(8f);
        await Seconds(3f);
        Check("awake in the morning", !herd.Asleep && herd.Animals.All(a => !a.Sleeping), $"hour {clock.Now.Hour:0.0}");

        // 7. Save and restore: where each stands, what it holds, the gate.
        var saves = c.Resolve<SaveService>();
        SaveSection section = c.Resolve<SaveRegistry>().Sections.First(x => x is AnimalSaveSection);
        Animal saved = herd.Animals[0];
        saved.Earnings.Set(7.5f);
        Vector3 savedAt = saved.transform.position;
        SaveData data = saves.Create();
        section.Capture(data);
        saved.Earnings.Set(0f);
        saved.Mover.Teleport(herd.Pen.Center);
        gate.Interact(player.Transform.gameObject);
        await Seconds(1f);
        section.Restore(data);
        await Seconds(0.5f);
        Check("restore puts an animal back with its Tokens", Vector3.Distance(saved.transform.position, savedAt) < 0.6f && Mathf.Abs(saved.Earnings.Held - 7.5f) < 0.2f,
            $"{Vector3.Distance(saved.transform.position, savedAt):0.00} m off, holds {saved.Earnings.Held:0.00}");
        Check("restore shuts the gate that was saved shut", !gate.IsOpen && !herd.Orders.GateOpen);

        commands.Execute("cheat.god off");
        commands.Execute("cheat.notarget off");
        return $"{(failed == 0 ? "ALL OK" : failed + " FAILED")}\n{log}";
    }

    static async Task Seconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until)
            await Task.Yield();
    }
}
