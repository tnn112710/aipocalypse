using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.DefenseSystem;
using Aipocalypse.Core.Interaction;
using Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Play-mode check of the player side in WorldMap (enter Play first): stands the player in front of the gate, reads the prompts, opens and shuts the gate, then dents it and repairs it with the hammer (hold use), then dents, and later breaks and rebuilds, a fence segment. Each `shot` step pauses
// so a Game view capture can be taken from outside (step names in the log).
// Run: unity command script run --file AgentScripts/Defense/DefenseInteractCheck.cs --entry DefenseInteractCheck.Run --timeout 120
public static class DefenseInteractCheck
{
    public static async Task<string> Run(float pauseForShots = 0f)
    {
        if (!Application.isPlaying)
            return "enter play mode in WorldMap first";
        Application.runInBackground = true;
        var log = new StringBuilder();
        IObjectResolver c = LifetimeScope.Find<GameLifetimeScope>().Container;
        var barriers = c.Resolve<BarrierRegistry>();
        var player = (Player)c.Resolve<PlayerRegistry>().Current;
        BarrierSegment gate = barriers.Segments.First(s => s.Gate != null);
        if (gate.Gate.IsOpen)
            gate.Gate.Toggle(gate.transform.position);

        // 1.8 m from the gate on the yard side, eye on the leaf.
        Vector3 centre = new(267f, 0f, 282f);
        Vector3 inward = gate.transform.forward * (Vector3.Dot(gate.transform.forward, centre - gate.transform.position) > 0f ? 1f : -1f);
        Vector3 stand = gate.transform.position + inward * 1.8f;
        Teleport(player, stand, Quaternion.LookRotation(-inward));
        await Seconds(0.5f);

        PlayerInteractor interactor = player.Interactor;
        log.AppendLine($"gate: prompt {Prompt(interactor)}");
        await Pause("shot1-gate-prompt", pauseForShots, log);

        gate.Interact(player.gameObject);
        await Seconds(0.9f);
        log.AppendLine($"after E: open {gate.Gate.IsOpen}, prompt {Prompt(interactor)}");
        await Pause("shot2-gate-open", pauseForShots, log);
        gate.Interact(player.gameObject);
        await Seconds(0.9f);
        log.AppendLine($"after E again: open {gate.Gate.IsOpen}");

        gate.DealDamage(260f);
        await Seconds(0.3f);
        log.AppendLine($"dented gate {gate.HealthShare:P0}: prompt {Prompt(interactor)}");
        await Pause("shot3-gate-damaged", pauseForShots, log);
        gate.InteractHold(player.gameObject);
        await Seconds(1.2f);
        log.AppendLine($"repairing: busy {((IRepairer)player).IsRepairing}, hammer shown {player.GetComponentInChildren<RepairTool>(true).gameObject.activeInHierarchy}, health {gate.HealthShare:P0}");
        await Pause("shot4-hammering", pauseForShots, log);
        float until = Time.time + 12f;
        while (((IRepairer)player).IsRepairing && Time.time < until)
            await Seconds(0.2f);
        await Seconds(1f);
        log.AppendLine($"done: health {gate.HealthShare:P0}, busy {((IRepairer)player).IsRepairing}, item raised {!player.Equipment.HandsLowered}");

        // A plain segment: tap repairs; looking away stops it.
        BarrierSegment fence = barriers.Segments.Where(s => s.Gate == null)
            .OrderBy(s => (s.transform.position - gate.transform.position).sqrMagnitude).Skip(1).First();
        fence.DealDamage(200f);
        Vector3 fenceIn = fence.transform.forward * (Vector3.Dot(fence.transform.forward, centre - fence.transform.position) > 0f ? 1f : -1f);
        Teleport(player, fence.transform.position + fenceIn * 1.6f, Quaternion.LookRotation(-fenceIn));
        await Seconds(0.5f);
        log.AppendLine($"fence {fence.HealthShare:P0}: prompt {Prompt(interactor)}");
        fence.Interact(player.gameObject);
        await Seconds(1.6f);
        float mid = fence.HealthShare;
        Teleport(player, player.transform.position, Quaternion.LookRotation(fenceIn));
        await Seconds(1.5f);
        log.AppendLine($"looked away: repaired to {mid:P0} then stopped {!((IRepairer)player).IsRepairing} at {fence.HealthShare:P0}");

        // A broken segment: its colliders are off, so only its Gap trigger lets the cast find it.
        fence.DealDamage(10000f);
        Teleport(player, fence.transform.position + fenceIn * 1.6f, Quaternion.LookRotation(-fenceIn));
        await Seconds(0.8f);
        log.AppendLine($"broken {fence.IsBroken}: prompt {Prompt(interactor)}");
        await Pause("shot5-fence-broken", pauseForShots, log);
        bool focused = interactor.HasPrompt;
        if (focused)
            fence.Interact(player.gameObject);
        until = Time.time + 15f;
        while (((IRepairer)player).IsRepairing && Time.time < until)
            await Seconds(0.2f);
        log.AppendLine($"rebuilt: broken {fence.IsBroken}, health {fence.HealthShare:P0}, busy {((IRepairer)player).IsRepairing}");
        fence.Restore();
        return log.ToString();
    }

    static string Prompt(PlayerInteractor i) =>
        (i.HasPrompt ? $"[{i.Prompt.Verb} {i.Prompt.Target}]" : "[none]") + (i.HasHoldPrompt ? $" hold [{i.HoldPrompt.Verb} {i.HoldPrompt.Target}]" : "");

    static void Teleport(Player player, Vector3 position, Quaternion rotation)
    {
        object motor = typeof(Player).GetField("_motor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
        motor.GetType().GetMethod("Teleport").Invoke(motor, new object[] { position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f) });
        object look = typeof(Player).GetField("_look", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
        look.GetType().GetMethod("ResetPitch")?.Invoke(look, new object[] { 8f });
    }

    static async Task Pause(string step, float seconds, StringBuilder log)
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
