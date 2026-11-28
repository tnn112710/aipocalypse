using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.CampfireSystem;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Play-mode checks for the campfire (docs/technicals/CAMPFIRE.md). Enter Play in WorldMap first.
// Run: unity command script run --file AgentScripts/Campfire/CampfireProbe.cs --entry CampfireProbe.State
public static class CampfireProbe
{
    static IObjectResolver Container => LifetimeScope.Find<GameLifetimeScope>()?.Container;

    // Console commands (';' between them), then the state.
    public static string Run(string commands)
    {
        var log = new StringBuilder();
        if (Container == null)
            return "no GameLifetimeScope (enter Play in WorldMap)";
        if (!string.IsNullOrWhiteSpace(commands) && Container.TryResolve(out CommandRegistry registry))
            foreach (string command in commands.Split(';'))
                if (!string.IsNullOrWhiteSpace(command))
                {
                    CommandResult result = registry.Execute(command.Trim());
                    log.AppendLine($"> {command.Trim()}: {(result.Succeeded ? "ok" : "FAILED")} {result.Message}");
                }
        return log.Append(State()).ToString();
    }

    public static string State()
    {
        IObjectResolver c = Container;
        if (c == null || !c.TryResolve(out CampfireRegistry campfires))
            return "no CampfireRegistry";
        var log = new StringBuilder();
        Campfire fire = campfires.Home;
        if (fire != null)
        {
            var safe = fire.GetComponent<SafeLight>();
            Light core = fire.transform.Find("Core Light")?.GetComponent<Light>();
            log.AppendLine($"fire {fire.Fuel.Fuel:0.00}/{fire.Fuel.Max} h lit={fire.Fuel.Lit} strength={fire.Fuel.Strength:0.00} safe={(safe != null ? $"{safe.enabled} r{safe.Radius:0.#}" : "none")} " +
                $"core={(core != null ? $"{core.enabled} i{core.intensity:0.#} r{core.range:0.#}" : "none")} at {fire.transform.position:F1}");
        }
        foreach (WoodPile pile in campfires.Piles)
            log.AppendLine($"pile {pile.Stock} logs");
        IPlayer player = c.Resolve<PlayerRegistry>().Current;
        if (player != null)
        {
            var wood = campfires.Piles.Count > 0 ? campfires.Piles[0].Wood : null;
            log.AppendLine($"player at {player.Transform.position:F1}, wood {(wood != null ? player.Equipment.Inventory.CountOf(wood) : -1)}, darkness {player.Darkness:0.##}");
            if (c.TryResolve(out NeedsTicker ticker))
            {
                SanityInput input = ticker.Exposure(player);
                log.AppendLine($"sanity {player.Needs?.Sanity.Current:0.#}: outside light {input.DistanceOutsideLight:0.#} m, in light {input.InLight}");
            }
            if (c.Resolve<PlayerRegistry>().Current is Player p)
            {
                var interactor = p.Interactor;
                if (interactor != null && interactor.HasPrompt)
                    log.AppendLine($"prompt: {interactor.Prompt.Verb} {interactor.Prompt.Target}" + (interactor.HasHoldPrompt ? $" | hold: {interactor.HoldPrompt.Verb} {interactor.HoldPrompt.Target}" : ""));
            }
        }
        var hud = Object.FindAnyObjectByType<CampfireFuelHud>();
        if (hud != null)
            log.AppendLine($"fuel bar alpha {hud.GetComponent<CanvasGroup>().alpha:0.00}");
        return log.ToString();
    }

    // Presses E on the wood pile, then on the fire, as the player (no aiming needed).
    public static string TakeAndTap()
    {
        IObjectResolver c = Container;
        var campfires = c.Resolve<CampfireRegistry>();
        var player = (Player)c.Resolve<PlayerRegistry>().Current;
        var log = new StringBuilder();
        if (campfires.Piles.Count > 0)
        {
            campfires.Piles[0].Interact(player.gameObject);
            log.AppendLine("took from the pile");
        }
        campfires.Home.TryGetPrompt(player.gameObject, out var prompt);
        log.AppendLine($"tap: {prompt.Verb} {prompt.Target}");
        campfires.Home.Interact(player.gameObject);
        return log.Append(State()).ToString();
    }

    public static string Hold()
    {
        IObjectResolver c = Container;
        var player = (Player)c.Resolve<PlayerRegistry>().Current;
        Campfire fire = c.Resolve<CampfireRegistry>().Home;
        fire.InteractHold(player.gameObject);
        return State();
    }

    // Takes `count` logs out of the player's inventory and throws them into the pit from 2 m up; reports after they land.
    public static async Task<string> Throw(int count = 1)
    {
        IObjectResolver c = Container;
        var campfires = c.Resolve<CampfireRegistry>();
        var player = (Player)c.Resolve<PlayerRegistry>().Current;
        MaterialDefinition wood = campfires.Piles[0].Wood;
        int taken = player.Equipment.Inventory.Take(wood, count);
        if (taken == 0)
            return "no wood carried";
        Campfire fire = campfires.Home;
        float before = fire.Fuel.Fuel;
        WorldItem item = WorldItem.Spawn(wood.CreateInstance(taken), fire.transform.position + new Vector3(1.5f, 2f, 0f), Quaternion.identity, null);
        item.Throw(new Vector3(-2.5f, 0f, 0f));
        float end = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < end)
            await Task.Yield();
        return $"threw {taken}: fuel {before:0.0} → {fire.Fuel.Fuel:0.0}, item left: {(item != null ? item.Item.Count.ToString() : "burned (gone)")}\n" + State();
    }

    // Real G throws (Player.ThrowAway, the inventory's drop) at the pit: the player stands `distances` m away facing it and
    // looks `pitches` degrees down. Reports, for each pair, whether the log burned or how far from the pit it landed.
    public static async Task<string> ThrowG(string distances = "1.8,2.5,3.5", string pitches = "15,30,45,60")
    {
        IObjectResolver c = Container;
        var campfires = c.Resolve<CampfireRegistry>();
        var player = (Player)c.Resolve<PlayerRegistry>().Current;
        Campfire fire = campfires.Home;
        MaterialDefinition wood = campfires.Piles[0].Wood;
        Vector3 pit = fire.transform.position;
        Vector3 dir = new Vector3(312f, 0f, 362f) - pit;
        dir.y = 0f;
        dir.Normalize();
        var log = new StringBuilder();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (string d in distances.Split(','))
        {
            float distance = float.Parse(d, inv);
            log.Append($"{distance} m:");
            foreach (string p in pitches.Split(','))
            {
                float pitch = float.Parse(p, inv);
                fire.RestoreSaved(0f, false);
                Vector3 at = pit + dir * distance;
                if (Physics.Raycast(at + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                    at.y = hit.point.y + 0.05f;
                float yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
                player.Teleport(at, yaw);
                for (int i = 0; i < 10; i++)
                    await Task.Yield();
                player.View.localRotation = Quaternion.Euler(pitch, 0f, 0f);   // the rig sets it back next frame
                player.ThrowAway(wood.CreateInstance(1));
                float end = Time.realtimeSinceStartup + 2.5f;
                while (Time.realtimeSinceStartup < end)
                    await Task.Yield();
                if (fire.Fuel.Fuel > 0f)
                    log.Append($" {pitch}°=burned");
                else
                {
                    WorldItem landed = null;
                    foreach (WorldItem w in Object.FindObjectsByType<WorldItem>())
                        if (w.Item != null && w.Item.Definition == wood && (landed == null || (w.transform.position - pit).sqrMagnitude < (landed.transform.position - pit).sqrMagnitude))
                            landed = w;
                    Vector3 off = landed != null ? landed.transform.position - pit : Vector3.positiveInfinity;
                    log.Append($" {pitch}°=missed({new Vector2(off.x, off.z).magnitude:0.0} m)");
                    if (landed != null)
                        Object.Destroy(landed.gameObject);
                }
            }
            log.AppendLine();
        }
        fire.RestoreSaved(8f, false);
        return log.ToString();
    }

    // The nearest walkable A* node to the pit's centre: it should lie outside the cut (about 1 m out).
    public static string Navmesh()
    {
        Campfire fire = Container.Resolve<CampfireRegistry>().Home;
        Vector3 at = fire.transform.position;
        var info = AstarPath.active.GetNearest(at, Pathfinding.NNConstraint.Walkable);
        Vector3 p = info.position;
        return $"nearest walkable node to the pit {Vector2.Distance(new Vector2(at.x, at.z), new Vector2(p.x, p.z)):0.00} m away (cut radius 1 m)";
    }

    // Median CPU / GPU frame ms (FrameTimingManager) with the home fire lit vs out, alternating `rounds` times, `frames` each.
    public static async Task<string> Cost(int frames = 240, int rounds = 2)
    {
        Campfire fire = Container.Resolve<CampfireRegistry>().Home;
        var lit = new System.Collections.Generic.List<(double cpu, double gpu)>();
        var dark = new System.Collections.Generic.List<(double cpu, double gpu)>();
        var timings = new FrameTiming[1];
        for (int round = 0; round < rounds; round++)
            foreach (bool on in new[] { true, false })
            {
                if (on) { fire.RestoreSaved(8f, true); } else { fire.PutOut(); }
                for (int i = 0; i < 90; i++)   // fade and particles settle
                    await Task.Yield();
                for (int i = 0; i < frames; i++)
                {
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                        (on ? lit : dark).Add((timings[0].cpuMainThreadFrameTime, timings[0].gpuFrameTime));
                    await Task.Yield();
                }
            }
        fire.RestoreSaved(8f, true);
        static double Median(System.Collections.Generic.List<double> v) { v.Sort(); return v.Count == 0 ? 0 : v[v.Count / 2]; }
        double litCpu = Median(lit.ConvertAll(t => t.cpu)), litGpu = Median(lit.ConvertAll(t => t.gpu));
        double darkCpu = Median(dark.ConvertAll(t => t.cpu)), darkGpu = Median(dark.ConvertAll(t => t.gpu));
        return $"lit: CPU main {litCpu:0.00} ms, GPU {litGpu:0.00} ms ({lit.Count} frames) | out: CPU {darkCpu:0.00}, GPU {darkGpu:0.00} ({dark.Count}) | Δ CPU {litCpu - darkCpu:+0.00;-0.00}, GPU {litGpu - darkGpu:+0.00;-0.00}";
    }

    // Fixed capture views facing the fire: "close" 4.5 m, "yard" 6.5 m (4 m to the side), "road" 45 m toward the bridge.
    public static string View(string name)
    {
        IObjectResolver c = Container;
        Vector3 fire = c.Resolve<CampfireRegistry>().Home.transform.position;
        Vector3 dir = new Vector3(312f, fire.y, 362f) - fire;
        dir.y = 0f;
        dir.Normalize();
        float distance = name switch { "close" => 4.5f, "road" => 45f, _ => 6.5f };
        Vector3 at = fire + dir * distance + (name == "yard" ? Vector3.Cross(Vector3.up, dir) * -4f : Vector3.zero);
        if (Physics.Raycast(at + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            at.y = hit.point.y;
        Vector3 look = fire - at;
        float yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
        CommandResult r = c.Resolve<CommandRegistry>().Execute(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "cheat.tp {0:0.00},{1:0.00},{2:0.00} {3:0}", at.x, at.y + 0.1f, at.z, yaw));
        return r.Message;
    }

    // A string: each script run compiles a new assembly, so a static object would differ between the pause and the release.
    const string PauseOwner = "CampfireProbe.Pause";

    // Pauses (or resumes) the game as the pause menu does and reports whether the crackle loop is playing.
    public static string Pause(bool paused)
    {
        IObjectResolver c = Container;
        var time = c.Resolve<MB13.Aipocalypse.Core.TimeSystem.TimeService>();
        if (paused) time.RequestPause(PauseOwner); else time.ReleasePause(PauseOwner);
        return $"paused {time.IsPaused}; " + Crackle();
    }

    public static string Crackle()
    {
        Campfire fire = Container.Resolve<CampfireRegistry>().Home;
        var loop = typeof(Campfire).GetField("_crackleLoop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(fire) as Ami.BroAudio.IAudioPlayer;
        return loop == null ? "crackle: none" : $"crackle: active {loop.IsActive}, playing {loop.IsPlaying}";
    }

    // Presses E (or holds E) on the wood pile as the player.
    public static string Pile(bool hold)
    {
        IObjectResolver c = Container;
        var player = (Player)c.Resolve<PlayerRegistry>().Current;
        WoodPile pile = c.Resolve<CampfireRegistry>().Piles[0];
        pile.TryGetPrompt(player.gameObject, out var tap);
        string holdText = pile.TryGetHoldPrompt(player.gameObject, out var held) ? $"{held.Verb} {held.Target}" : "none";
        if (hold) pile.InteractHold(player.gameObject); else pile.Interact(player.gameObject);
        return $"prompt: {tap.Verb} {tap.Target} | hold: {holdText} → pile {pile.Stock}, carried {player.Equipment.Inventory.CountOf(pile.Wood)}";
    }

    // Min / max of the core and fill intensity over `frames` frames: the fill should swing a quarter of the core's dip.
    public static async Task<string> Flicker(int frames = 240)
    {
        Campfire fire = Container.Resolve<CampfireRegistry>().Home;
        Light core = fire.transform.Find("Core Light").GetComponent<Light>(), fill = fire.transform.Find("Fill Light").GetComponent<Light>();
        float coreMin = float.MaxValue, coreMax = 0f, fillMin = float.MaxValue, fillMax = 0f;
        for (int i = 0; i < frames; i++)
        {
            await Task.Yield();
            coreMin = Mathf.Min(coreMin, core.intensity); coreMax = Mathf.Max(coreMax, core.intensity);
            fillMin = Mathf.Min(fillMin, fill.intensity); fillMax = Mathf.Max(fillMax, fill.intensity);
        }
        return $"core {coreMin:0.00}–{coreMax:0.00} (dip {(1f - coreMin / coreMax) * 100f:0}%), fill {fillMin:0.0}–{fillMax:0.0} (dip {(1f - fillMin / fillMax) * 100f:0}%), fill shadows {fill.shadows}";
    }

    // Puts a log on the lit fire (as the tap does) and samples the core light and the flame's embers rate for a second.
    public static async Task<string> Flare()
    {
        IObjectResolver c = Container;
        var campfires = c.Resolve<CampfireRegistry>();
        var player = (Player)c.Resolve<PlayerRegistry>().Current;
        Campfire fire = campfires.Home;
        fire.RestoreSaved(4f, true);
        for (int i = 0; i < 10; i++) await Task.Yield();
        Light core = fire.transform.Find("Core Light").GetComponent<Light>();
        var vfx = fire.GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
        float before = core.intensity, embers = vfx.GetFloat("AmbersRate");
        player.Equipment.Inventory.TryAdd(campfires.Piles[0].Wood.CreateInstance(1), out _);
        fire.Tap(player.Equipment.Inventory);
        float peak = 0f, peakEmbers = 0f, end = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < end)
        {
            await Task.Yield();
            peak = Mathf.Max(peak, core.intensity);
            peakEmbers = Mathf.Max(peakEmbers, vfx.GetFloat("AmbersRate"));
        }
        return $"core {before:0.0} → peak {peak:0.0} → {core.intensity:0.0}; embers {embers:0} → peak {peakEmbers:0} → {vfx.GetFloat("AmbersRate"):0}; fuel {fire.Fuel.Fuel:0.0} h";
    }

    // Counts sputters (the timer resetting) over `seconds` with the fire lit at `fuel` hours.
    public static async Task<string> Sputter(float fuel = 1.5f, float seconds = 20f)
    {
        Campfire fire = Container.Resolve<CampfireRegistry>().Home;
        fire.RestoreSaved(fuel, true);
        var field = typeof(Campfire).GetField("_sputterIn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float last = (float)field.GetValue(fire);
        int sputters = 0;
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            await Task.Yield();
            float now = (float)field.GetValue(fire);
            if (now > last + 0.5f && last >= 0f)
                sputters++;
            last = now;
        }
        return $"fuel {fuel} h (share {fire.Fuel.Share:0.00}): {sputters} sputters in {seconds} s";
    }

    // Stands inside the light, then steps out (to `outside` m) and samples the LeftLight look; captures need the game view.
    public static async Task<string> Edge(float inside = 25f, float outside = 36f)
    {
        IObjectResolver c = Container;
        var looks = c.Resolve<MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks.ScreenLookService>();
        StandAt(inside);
        for (int i = 0; i < 30; i++) await Task.Yield();
        float atInside = looks.Strength(MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks.ScreenLookId.LeftLight);
        StandAt(outside);
        var samples = new StringBuilder();
        float start = Time.realtimeSinceStartup, next = 0f;
        while (Time.realtimeSinceStartup - start < 4.5f)
        {
            await Task.Yield();
            float t = Time.realtimeSinceStartup - start;
            if (t >= next)
            {
                samples.Append($" {t:0.0}s={looks.Strength(MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks.ScreenLookId.LeftLight):0.00}");
                next += 0.5f;
            }
        }
        return $"inside {atInside:0.00}; out:{samples}";
    }

    // The HUD fuel bar's label and alpha.
    public static string FuelBar()
    {
        var hud = Object.FindAnyObjectByType<CampfireFuelHud>();
        var label = hud.GetComponentInChildren<TMPro.TMP_Text>(true);
        return $"fuel bar alpha {hud.GetComponent<CanvasGroup>().alpha:0.00}, label '{label?.text}'";
    }

    // Logs showing in the pit.
    public static string Logs()
    {
        Campfire fire = Container.Resolve<CampfireRegistry>().Home;
        Transform logs = fire.transform.Find("Logs");
        int shown = 0;
        foreach (Transform log in logs) if (log.gameObject.activeSelf) shown++;
        return $"fuel {fire.Fuel.Fuel:0.0} h: {shown}/{logs.childCount} logs";
    }

    // Teleports the player inside the house (`back` m behind the door line), facing a side wall: does the fire's shadowless
    // fill light leak through the walls? Capture with the fire lit, then out.
    public static string Inside(float back = 2.5f, float side = 90f)
    {
        IObjectResolver c = Container;
        Transform house = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>())
            if (t.name.StartsWith("House_1") && t.parent != null && t.parent.name == "Home Base") { house = t; break; }
        if (house == null)
            return "no House_1 under Home Base";
        Vector3 at = house.TransformPoint(new Vector3(0f, 0.1f, -back));
        float yaw = house.eulerAngles.y + side;
        CommandResult r = c.Resolve<CommandRegistry>().Execute(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "cheat.tp {0:0.00},{1:0.00},{2:0.00} {3:0}", at.x, at.y, at.z, yaw));
        return $"house at {house.position:F1} yaw {house.eulerAngles.y:0}; {r.Message}";
    }

    // Teleports the player `metres` from the fire toward the gate side and reports the sanity exposure.
    public static string StandAt(float metres)
    {
        IObjectResolver c = Container;
        Campfire fire = c.Resolve<CampfireRegistry>().Home;
        Vector3 dir = new Vector3(312f, 0f, 362f) - fire.transform.position;
        dir.y = 0f;
        Vector3 at = fire.transform.position + dir.normalized * metres;
        if (Physics.Raycast(at + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            at.y = hit.point.y + 0.1f;
        c.Resolve<CommandRegistry>().Execute(string.Format(System.Globalization.CultureInfo.InvariantCulture, "cheat.tp {0:0.00},{1:0.00},{2:0.00} 0", at.x, at.y, at.z));
        return State();
    }
}
