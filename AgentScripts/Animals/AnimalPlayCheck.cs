// Play-mode check of the farm animals in WorldMap (enter Play first, editor auto tick on):
//   unity command script run --file AgentScripts/Animals/AnimalPlayCheck.cs --entry AnimalPlayCheck.Run --timeout 600
// Steps: the herd (cows, sheep, chickens, pigs) is injected and penned; it grazes inside the pen; Tokens held show the coin,
// walking up takes nothing, the Collect key bursts them out to the player; a grazing animal looks up at the player close
// by and steps aside when walked into; opening the pen gate (and the home gate) sends them to the pasture, where they earn
// at the roaming rate and the flocks keep round their leaders; E sends one animal home, where it stays, and lets it out
// again; the sheep leader sent home hands the flock to another; a gunshot sends the animals near it running; the bell
// brings them home, E speeding up only the one looked at; the pen gate shuts itself once all are in; at night they sleep
// where they are, the coin lower over them; a save section capture and restore puts an animal, its Tokens, its order and
// the gate back. Animals are hurried (`animal.hurry`) where a walk at their own pace would take minutes.
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RaidSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
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
        if (c == null || !c.TryResolve(out AnimalHerd herd) || !c.TryResolve(out AnimalManager _))
            return "no AnimalHerd / AnimalManager (no AnimalInstaller on the GameLifetimeScope, or it didn't install)";
        var clock = c.Resolve<WorldClock>();
        var settings = c.Resolve<AnimalSettings>();
        var commands = c.Resolve<CommandRegistry>();
        var shots = c.Resolve<IPublisher<ShotFired>>();
        IPlayer player = c.Resolve<PlayerRegistry>().Current;
        if (player == null)
            return "no player";
        GameObject actor = player.Transform.gameObject;
        var log = new StringBuilder();
        int failed = 0;
        void Check(string what, bool ok, string detail = null)
        {
            if (!ok)
                failed++;
            log.AppendLine($"{(ok ? "ok  " : "FAIL")} {what}{(detail != null ? ": " + detail : "")}");
        }
        string Where() => string.Join(" ", herd.Animals.Select(a => $"{a.name}({(a.IsHome ? "pen" : a.IsRoaming ? "pasture" : "out")})"));
        string Prompt(Animal a) => a.TryGetPrompt(actor, out var p) ? p.Verb : "-";
        void Unpause()
        {
            foreach (PauseMenu menu in Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Exclude))
                if (menu.IsOpen)
                    menu.Close();
        }
        async Task<Animal> Grazing(System.Func<Animal, bool> which = null)
        {
            for (int i = 0; i < 40; i++)
            {
                Animal found = herd.Animals.FirstOrDefault(a => a.Eating && (which == null || which(a)));
                if (found != null)
                    return found;
                await Seconds(0.5f);
            }
            return null;
        }
        // Puts the player `distance` from the animal, on its far side from the pen's middle.
        void Beside(Animal animal, float distance)
        {
            Vector3 away = animal.transform.position - herd.Pen.Center;
            away.y = 0f;
            Vector3 at = animal.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * distance;
            player.Teleport(at, 0f);
        }
        void Away() => player.Teleport(herd.Pen.Gate.transform.position + herd.Pen.Gate.transform.forward * 12f, 0f);

        commands.Execute("cheat.god on");
        commands.Execute("cheat.notarget on");
        Unpause();
        if (clock.Now.Hour < 8f || clock.Now.Hour > 11f)
            clock.JumpTo(8f);
        Away();
        await Seconds(1f);

        // 1. Injected, penned.
        int expected = 14;
        Check("herd registered", herd.Animals.Count == expected, $"{herd.Animals.Count} animals ({string.Join(", ", herd.Animals.GroupBy(a => a.Definition.DisplayName).Select(g => g.Count() + " " + g.Key))})");
        Check("all start in the pen", herd.CountHome() == herd.Animals.Count, Where());
        Check("penned, awake", !herd.Orders.Released && !herd.Asleep, $"hour {clock.Now.Hour:0.0}");

        // 2. Grazing inside the pen.
        var before = herd.Animals.ToDictionary(a => a, a => a.transform.position);
        bool ate = false;
        var fidgeted = new System.Collections.Generic.HashSet<string>();
        bool darted = false;
        for (int i = 0; i < 60; i++)
        {
            await Seconds(0.5f);
            ate |= herd.Animals.Any(a => a.Eating);
            foreach (Animal a in herd.Animals.Where(a => a.Fidget > 0))
                fidgeted.Add(a.Definition.name);
            darted |= herd.Animals.Any(a => a.Darting && a.Definition.name == "Chicken");
        }
        foreach (var kind in herd.Animals.GroupBy(a => a.Definition))
            Check($"{kind.Key.DisplayName}s walk about the pen", kind.Max(a => Vector3.Distance(before[a], a.transform.position)) > 0.8f,
                $"furthest {kind.Max(a => Vector3.Distance(before[a], a.transform.position)):0.0} m");
        Check("they eat", ate);
        Check("now and then they stand about with a spare idle", fidgeted.Count >= 2, string.Join(", ", fidgeted));
        Check("chickens dart", darted);
        Check("each has its own pace", herd.Animals.Select(a => Mathf.Round(a.Pace * 100f)).Distinct().Count() > herd.Animals.Count / 2,
            string.Join(" ", herd.Animals.Select(a => a.Pace.ToString("0.00"))));
        Check("none left the pen", herd.CountHome() == herd.Animals.Count, Where());

        // 3. Tokens: shown, not taken by walking up, taken with the Collect key.
        commands.Execute("animal.fill 10");
        await Seconds(0.3f);
        Check("coin markers show", herd.Animals.All(a => a.transform.Find("CoinMarker").gameObject.activeSelf));
        int tokensBefore = player.Equipment.Inventory.CountOf(settings.Tokens);
        Animal nearest = herd.Animals.First(a => a.Definition.name == "Pig");
        Beside(nearest, 2f);
        await Seconds(2f);
        Check("walking up takes nothing", nearest.HasTokens && player.Equipment.Inventory.CountOf(settings.Tokens) == tokensBefore);
        Check("it offers Collect on C", nearest.TryGetCollectPrompt(actor, out var collectPrompt) && collectPrompt.Verb == "Collect", $"{collectPrompt.Verb} {collectPrompt.Target}");
        Check("penned, E offers nothing", !nearest.TryGetPrompt(actor, out _), Prompt(nearest));
        nearest.Collect(actor);
        await Seconds(3f);
        int tokensAfter = player.Equipment.Inventory.CountOf(settings.Tokens);
        Check("C collects", tokensAfter >= tokensBefore + 10, $"Tokens {tokensBefore} -> {tokensAfter}");
        Check("its marker is gone", !nearest.transform.Find("CoinMarker").gameObject.activeSelf);
        commands.Execute("animal.collect");
        await Seconds(2f);

        // 4. Noticing the player, stepping aside.
        Away();
        Animal grazer = await Grazing(a => a.Definition.name == "Sheep");
        if (grazer != null)
        {
            Beside(grazer, settings.NoticeRadius * 0.7f);
            await Seconds(1.2f);
            Check("a grazing animal looks up at the player", !grazer.Eating && !grazer.Startled, grazer.name);
            // Its meal may end meanwhile and it steps on: the closest it came to facing them in 3 s.
            float facing = 180f;
            for (int i = 0; i < 12; i++)
            {
                await Seconds(0.25f);
                Vector3 toPlayer = Vector3.ProjectOnPlane(player.Transform.position - grazer.transform.position, Vector3.up);
                facing = Mathf.Min(facing, Vector3.Angle(grazer.transform.forward, toPlayer));
            }
            Check("and turns to face them", facing < 40f, $"{facing:0} deg off at best");
        }
        else
            Check("a grazing animal looks up at the player", false, "nobody grazed");
        Away();
        Animal bumped = await Grazing();
        if (bumped != null)
        {
            Vector3 bumpedAt = bumped.transform.position;
            Beside(bumped, settings.BumpRadius * 0.6f);
            await Seconds(0.3f);
            bool startled = bumped.Startled;
            Away();
            float furthest = 0f;
            for (int i = 0; i < 16; i++)
            {
                await Seconds(0.25f);
                furthest = Mathf.Max(furthest, Vector3.Distance(bumpedAt, bumped.transform.position));
            }
            Check("walked into, it steps aside", startled && furthest > 1f, $"{bumped.name} moved up to {furthest:0.0} m");
        }
        else
            Check("walked into, it steps aside", false, "nobody grazed");

        // 5. Release: pen gate and home gate open, the herd goes to the pasture.
        BarrierSegment homeGate = Object.FindObjectsByType<BarrierSegment>(FindObjectsInactive.Exclude).FirstOrDefault(s => s.Gate != null);
        if (homeGate != null && !homeGate.Gate.IsOpen)
            homeGate.Gate.Toggle(homeGate.transform.position - homeGate.transform.forward);
        PenGate gate = herd.Pen.Gate;
        gate.Interact(actor);
        await Seconds(1f);
        Check("opening the pen gate releases", gate.IsOpen && herd.Orders.Released);
        float deadline = Time.realtimeSinceStartup + 150f;
        while (Time.realtimeSinceStartup < deadline && herd.Animals.Count(a => a.IsRoaming) < herd.Animals.Count)
        {
            commands.Execute("animal.hurry");
            await Seconds(1f);
        }
        Check("the herd reaches the pasture", herd.Animals.All(a => a.IsRoaming), Where());
        Animal cow = herd.Animals.First(a => a.Definition.name == "Cow");
        float cowHeld = cow.Earnings.Held;
        double hourBefore = clock.Now.TotalHours;
        await Seconds(20f);
        float perHour = (cow.Earnings.Held - cowHeld) / (float)(clock.Now.TotalHours - hourBefore);
        float rate = cow.Definition.TokensPerDayRoaming / AnimalEarnings.DayLength(settings.Awake);
        Check("roaming rate on the pasture", Mathf.Abs(perHour - rate) < rate * 0.2f, $"{perHour:0.00} Tokens per game hour, expected {rate:0.00}");
        foreach (var flock in herd.Animals.Where(a => a.Definition.FollowsLeader).GroupBy(a => a.Definition))
        {
            // Stragglers from the walk out trot back; give them a while.
            Animal leader = herd.LeaderOf(flock.Key);
            float Spread() => flock.Max(a => Vector3.Distance(a.transform.position, leader.transform.position));
            float gather = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < gather && Spread() > flock.Key.FlockRadius * 2.2f)
                await Seconds(1f);
            float spread = Spread();
            Check($"the {flock.Key.name.ToLower()}s keep round their leader", spread <= flock.Key.FlockRadius * 2.2f && (flock.Key.name != "Chicken" || leader.name.StartsWith("Rooster")),
                $"{spread:0.0} m from {leader.name} (FlockRadius {flock.Key.FlockRadius})");
        }

        // Dusk: the herd drifts to the pasture's home side.
        Vector3 homeSide = herd.Pasture.Center + Vector3.ProjectOnPlane(herd.Pen.Center - herd.Pasture.Center, Vector3.up).normalized * herd.Pasture.Radius * 0.65f;
        float FromHome() => herd.Animals.Where(a => !a.Definition.FollowsLeader).Average(a => Vector3.Distance(a.transform.position, homeSide));
        float dayDistance = FromHome();
        clock.JumpTo(settings.Awake.Off - 0.9f);
        await Seconds(1f);
        bool dusk = herd.Dusk;
        float duskHour = clock.Now.Hour;
        // Dusk lasts well under a minute at the clock's pace; stop before the night.
        deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline && FromHome() > herd.Pasture.Radius * 0.5f)
            await Seconds(1f);
        Check("at dusk they graze the home side of the pasture", dusk && FromHome() < dayDistance && FromHome() <= herd.Pasture.Radius * 0.5f,
            $"hour {duskHour:0.0}, dusk {dusk}: cows and pigs {dayDistance:0.0} m -> {FromHome():0.0} m from it");
        clock.JumpTo(9f);
        await Seconds(1f);

        // 6. E on one animal: home it goes and stays; E again lets it out.
        Check("out on the pasture, E offers Send Home", Prompt(cow) == "Send Home", Prompt(cow));
        cow.Interact(actor);
        Check("E sends it home", cow.SentHome && Prompt(cow) == "Let Out", Prompt(cow));
        deadline = Time.realtimeSinceStartup + 90f;
        while (Time.realtimeSinceStartup < deadline && !cow.IsHome)
        {
            cow.Hurry();
            await Seconds(1f);
        }
        Check("the sent cow walks into the pen", cow.IsHome, Where());
        await Seconds(8f);
        Check("it stays in while the rest graze out", cow.IsHome && herd.Animals.Count(a => a.IsRoaming) == herd.Animals.Count - 1, Where());
        cow.Interact(actor);
        deadline = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < deadline && cow.IsHome)
            await Seconds(1f);
        Check("E again lets it out", !cow.SentHome && !cow.IsHome, $"{Prompt(cow)}, {Where()}");

        AnimalDefinition sheep = herd.Animals.First(a => a.Definition.name == "Sheep").Definition;
        Animal oldLeader = herd.LeaderOf(sheep);
        oldLeader.Interact(actor);
        Animal newLeader = herd.LeaderOf(sheep);
        Check("the lead sheep sent home hands the flock to another", newLeader != null && newLeader != oldLeader && !herd.Animals.Where(a => a != oldLeader).Any(a => a.SentHome),
            $"{oldLeader.name} -> {(newLeader != null ? newLeader.name : "none")}");
        oldLeader.Interact(actor);

        // 7. A gunshot: the animals near it run.
        Animal target = herd.Animals.First(a => a.Definition.name == "Pig" && !a.GoingHome);
        await Seconds(1f);
        Vector3 targetAt = target.transform.position;
        Vector3 shotAt = targetAt + Vector3.forward * 4f;
        shots.Publish(new ShotFired(shotAt, actor));
        await Seconds(0.3f);
        int ran = herd.Animals.Count(a => a.Startled && a.StartleRuns);
        float top = 0f;
        for (int i = 0; i < 12; i++)
        {
            top = Mathf.Max(top, target.Mover.Velocity.magnitude);
            await Seconds(0.25f);
        }
        Check("a gunshot sends the animals near it running", ran >= 2 && Vector3.Distance(shotAt, target.transform.position) > Vector3.Distance(shotAt, targetAt) + 2f,
            $"{ran} running; {target.name} moved {Vector3.Distance(targetAt, target.transform.position):0.0} m away, top speed {top:0.0} m/s (FleeSpeed {target.Definition.FleeSpeed})");
        await Seconds(4f);

        // 8. The bell: called home, E speeds up only the one looked at, the pen gate shuts itself.
        AnimalBell bell = Object.FindAnyObjectByType<AnimalBell>();
        bell.Interact(actor);
        await Seconds(0.5f);
        Check("the bell calls home", herd.Orders.Called && !herd.Orders.Released);
        Check("after the bell, E offers Hurry", Prompt(cow) == "Hurry", Prompt(cow));
        Vector3 cowStart = cow.transform.position;
        await Seconds(6f);
        float walked = Vector3.Distance(cowStart, cow.transform.position) / 6f;
        Check("the cow walks home at its walking pace", walked > 0.4f && walked < cow.Definition.WalkSpeed * 1.3f, $"{walked:0.00} m/s (WalkSpeed {cow.Definition.WalkSpeed})");
        cow.Interact(actor);
        int hurried = herd.Animals.Count(a => a.Hurrying);
        Check("E speeds up only that one", hurried == 1 && cow.Hurrying, $"{hurried} hurrying");
        deadline = Time.realtimeSinceStartup + 150f;
        while (Time.realtimeSinceStartup < deadline && (herd.CountHome() < herd.Animals.Count || gate.IsOpen))
        {
            foreach (Animal animal in herd.Animals)
                if (!animal.IsHome)
                    animal.Hurry();
            await Seconds(1f);
        }
        Check("the herd is home", herd.CountHome() == herd.Animals.Count, Where());
        Check("the pen gate shut itself", !gate.IsOpen && !herd.Orders.GateOpen);
        await Seconds(1f);
        Check("nobody is still hurrying", herd.Animals.All(a => !a.Hurrying));

        // 9. Night: asleep where they stand, earning nothing, the coin lower.
        commands.Execute("animal.fill 10");
        Transform marker = cow.transform.Find("CoinMarker");
        await Seconds(0.5f);
        float awakeHeight = marker.localPosition.y;
        clock.JumpTo(21f);
        if (c.TryResolve(out RaidDirector raids))
        {
            await Seconds(0.5f);
            raids.Stop();
        }
        await Seconds(3f);
        Check("asleep at night", herd.Asleep && herd.Animals.All(a => a.Sleeping), $"hour {clock.Now.Hour:0.0}");
        Check("the coin sits lower over a sleeping animal", marker.localPosition.y < awakeHeight - 0.3f, $"{awakeHeight:0.00} -> {marker.localPosition.y:0.00}");
        float nightHeld = herd.Animals.Sum(a => a.Earnings.Held);
        await Seconds(4f);
        Check("nothing earned asleep", Mathf.Approximately(nightHeld, herd.Animals.Sum(a => a.Earnings.Held)));
        clock.JumpTo(8f);
        await Seconds(3f);
        Check("awake in the morning", !herd.Asleep && herd.Animals.All(a => !a.Sleeping), $"hour {clock.Now.Hour:0.0}");
        commands.Execute("animal.collect");

        // 10. Save and restore: where each stands, what it holds, its order, the gate.
        var saves = c.Resolve<SaveService>();
        SaveSection section = c.Resolve<SaveRegistry>().Sections.First(x => x is AnimalSaveSection);
        Animal saved = herd.Animals[0];
        saved.Earnings.Set(7.5f);
        saved.SetSentHome(true);
        Vector3 savedAt = saved.transform.position;
        SaveData data = saves.Create();
        section.Capture(data);
        saved.Earnings.Set(0f);
        saved.SetSentHome(false);
        saved.Mover.Teleport(herd.Pen.Center);
        gate.Interact(actor);
        await Seconds(1f);
        section.Restore(data);
        await Seconds(0.5f);
        Check("restore puts an animal back with its Tokens and its order", Vector3.Distance(saved.transform.position, savedAt) < 0.6f && Mathf.Abs(saved.Earnings.Held - 7.5f) < 0.2f && saved.SentHome,
            $"{Vector3.Distance(saved.transform.position, savedAt):0.00} m off, holds {saved.Earnings.Held:0.00}, sent home {saved.SentHome}");
        Check("restore shuts the gate that was saved shut", !gate.IsOpen && !herd.Orders.GateOpen);
        saved.SetSentHome(false);

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
