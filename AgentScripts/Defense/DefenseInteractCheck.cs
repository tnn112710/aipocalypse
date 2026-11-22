using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Play-mode check of the player side in WorldMap (enter Play first). Gate prompts and toggle with empty hands; picks the
// hammer up from the yard table; then, keys pressed through a scripted intent source: holding the attack button repairs a
// dented gate and a fence segment (looking away or letting go stops it), rebuilds a broken one, and E on a whole segment
// repairs all. Each `shot` step pauses so
// a Game view capture can be taken from outside (step names in the log).
// Run: unity command script run --file AgentScripts/Defense/DefenseInteractCheck.cs --entry DefenseInteractCheck.Run --timeout 120
public static class DefenseInteractCheck
{
    // Instead of the devices: the attack button stays held until let go; interact is pressed for one frame.
    sealed class Keys : IPlayerIntentSource
    {
        public bool Fire;
        public bool Interact;

        public PlayerIntent Read(float deltaTime)
        {
            var intent = new PlayerIntent { Fire = Fire, Interact = Interact };
            Interact = false;
            return intent;
        }
    }

    static readonly Vector3 YardCentre = new(267f, 0f, 282f);

    public static async Task<string> Run(float pauseForShots = 0f)
    {
        if (!Application.isPlaying)
            return "enter play mode in WorldMap first";
        Application.runInBackground = true;
        var log = new StringBuilder();
        IObjectResolver c = LifetimeScope.Find<GameLifetimeScope>().Container;
        var barriers = c.Resolve<BarrierRegistry>();
        var player = (Player)c.Resolve<PlayerRegistry>().Current;
        var keys = new Keys();
        PlayerInteractor interactor = player.Interactor;
        BarrierSegment gate = barriers.Segments.First(s => s.Gate != null);
        if (gate.Gate.IsOpen)
            gate.Gate.Toggle(gate.transform.position);

        try
        {
            // ---- empty hands: the gate opens and shuts; a dented segment offers nothing ----
            FaceFrom(player, gate, 1.8f);
            await Seconds(0.5f);
            log.AppendLine($"gate, no hammer: {Prompts(player)}");
            gate.Interact(player.gameObject);
            await Seconds(0.9f);
            log.AppendLine($"after E: open {gate.Gate.IsOpen}, {Prompts(player)}");
            gate.Interact(player.gameObject);
            await Seconds(0.9f);
            log.AppendLine($"after E again: open {gate.Gate.IsOpen}");

            // A standing segment: a new game starts with the front half down to debris (and the herd's bell by the gate).
            BarrierSegment fence = barriers.Segments.Where(s => s.Gate == null && !s.IsBroken)
                .OrderBy(s => (s.transform.position - gate.transform.position).sqrMagnitude).Skip(1).First();
            fence.DealDamage(200f);
            FaceFrom(player, fence, 1.6f);
            await Seconds(0.5f);
            log.AppendLine($"fence {fence.HealthShare:P0}, no hammer: {Prompts(player)}");

            // ---- the hammer from the table ----
            WorldItem hammer = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude).FirstOrDefault(w => w.name == "Hammer");
            if (hammer == null)
                return log.AppendLine("no Hammer pickup in the scene").ToString();
            log.AppendLine($"hammer pickup: {(hammer.TryGetPrompt(player.gameObject, out InteractionPrompt pick) ? pick.Verb + " " + pick.Target : "none")}");
            hammer.Interact(player.gameObject);
            // Tools go to the backpack: put it in the first quick slot and raise it, as the player would.
            Inventory inventory = player.Equipment.Inventory;
            int carried = Enumerable.Range(0, inventory.Backpack.Count).First(i => inventory.Backpack[i]?.Definition.name == "Hammer");
            int quick = Enumerable.Range(0, inventory.Hotbar.Count).First(i => inventory.Hotbar.CanPlace(i, inventory.Backpack[carried]) && inventory.Hotbar[i] == null);
            inventory.TryMove(inventory.Backpack, carried, inventory.Hotbar, quick, out _);
            player.Equipment.Select(quick);
            await Seconds(1f);
            log.AppendLine($"holding {player.HeldItem?.name} (tool {((IRepairer)player).HasTool}), slot {player.Equipment.SelectedIndex + 1} of {player.Equipment.Hotbar.Count}");
            player.IntentSource = keys;

            // ---- holding LMB on a dented gate ----
            gate.DealDamage(260f);
            FaceFrom(player, gate, 1.8f);
            await Seconds(0.5f);
            log.AppendLine($"gate {gate.HealthShare:P0}, hammer: {Prompts(player)}");
            await Pause("shot1-gate-hammer", pauseForShots);
            keys.Fire = true;
            await Seconds(1.2f);
            log.AppendLine($"LMB held: busy {((IRepairer)player).IsRepairing}, health {gate.HealthShare:P0}");
            await Pause("shot2-hammering", pauseForShots);
            await WhileRepairing(player, 12f);
            log.AppendLine($"done: gate {gate.HealthShare:P0}, busy {((IRepairer)player).IsRepairing}");
            keys.Fire = false;

            // ---- holding LMB on a fence segment; letting go stops it, then looking away does ----
            FaceFrom(player, fence, 1.6f);
            await Seconds(0.5f);
            log.AppendLine($"fence {fence.HealthShare:P0}: {Prompts(player)}");
            keys.Fire = true;
            await Seconds(1.6f);
            keys.Fire = false;
            float released = fence.HealthShare;
            await Seconds(1f);
            log.AppendLine($"let go at {released:P0}: stopped {!((IRepairer)player).IsRepairing} at {fence.HealthShare:P0}");
            keys.Fire = true;
            await Seconds(1.6f);
            float mid = fence.HealthShare;
            Teleport(player, player.transform.position, Quaternion.LookRotation(fence.transform.position - player.transform.position) * Quaternion.Euler(0f, 180f, 0f));
            await Seconds(1.5f);
            log.AppendLine($"looked away: repaired to {mid:P0}, then stopped {!((IRepairer)player).IsRepairing} at {fence.HealthShare:P0}");
            keys.Fire = false;

            // ---- holding LMB rebuilds a broken one (found through its Gap trigger) ----
            fence.DealDamage(10000f);
            FaceFrom(player, fence, 1.6f);
            await Seconds(0.8f);
            log.AppendLine($"broken {fence.IsBroken}: {Prompts(player)}");
            await Pause("shot3-fence-broken", pauseForShots);
            keys.Fire = true;
            await Seconds(0.3f);
            await WhileRepairing(player, 15f);
            log.AppendLine($"rebuilt: broken {fence.IsBroken}, health {fence.HealthShare:P0}");
            keys.Fire = false;

            // ---- E on a whole segment repairs all ----
            BarrierSegment[] others = barriers.Segments.Where(s => s.Gate == null && s != fence).Take(4).ToArray();
            others[0].DealDamage(10000f);
            others[1].DealDamage(120f);
            others[2].DealDamage(220f);
            fence.Restore();
            FaceFrom(player, fence, 1.6f);
            await Seconds(0.6f);
            log.AppendLine($"damaged {barriers.CountDamaged()}, at a whole segment: {Prompts(player)}");
            await Pause("shot4-repair-all", pauseForShots);
            keys.Interact = true;
            await Seconds(0.5f);
            log.AppendLine($"E: damaged left {barriers.CountDamaged()}, broken {others[0].IsBroken}, {Prompts(player)}");
        }
        finally
        {
            player.IntentSource = null;
        }
        return log.ToString();
    }

    static string Prompts(Player player)
    {
        PlayerInteractor i = player.Interactor;
        PlayerRepair r = player.Repair;
        return (r.HasPrompt ? $"LMB [{r.Prompt.Verb} {r.Prompt.Target}] " : "")
            + (i.HasPrompt ? $"E [{i.Prompt.Verb} {i.Prompt.Target}]" : "E [none]")
            + (i.HasHoldPrompt ? $" hold E [{i.HoldPrompt.Verb} {i.HoldPrompt.Target}]" : "");
    }

    // `distance` m in front of it on the yard side, looking at it.
    static void FaceFrom(Player player, BarrierSegment segment, float distance)
    {
        Vector3 forward = segment.transform.forward;
        Vector3 inward = forward * (Vector3.Dot(forward, YardCentre - segment.transform.position) > 0f ? 1f : -1f);
        Teleport(player, segment.transform.position + inward * distance, Quaternion.LookRotation(-inward));
    }

    static async Task WhileRepairing(Player player, float limit)
    {
        float until = Time.time + limit;
        while (((IRepairer)player).IsRepairing && Time.time < until)
            await Seconds(0.2f);
    }

    static void Teleport(Player player, Vector3 position, Quaternion rotation)
    {
        object motor = typeof(Player).GetField("_motor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
        motor.GetType().GetMethod("Teleport").Invoke(motor, new object[] { position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f) });
        object look = typeof(Player).GetField("_look", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
        look.GetType().GetMethod("ResetPitch")?.Invoke(look, new object[] { 8f });
    }

    static async Task Pause(string step, float seconds)
    {
        if (seconds <= 0f)
            return;
        Debug.Log("DefenseInteractCheck " + step);
        await Seconds(seconds);
    }

    static async Task Seconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until)
            await Task.Yield();
    }
}
