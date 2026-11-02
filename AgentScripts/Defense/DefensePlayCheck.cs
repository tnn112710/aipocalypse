using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.DefenseSystem;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.Entities;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Play-mode check of the fences in WorldMap (enter Play first): barriers registered, the yard cut off while the gate is shut,
// open through the gate and through a broken segment, player damage ignored, then goblins spawned outside breaking in.
// Run: unity command script run --file AgentScripts/Defense/DefensePlayCheck.cs --entry DefensePlayCheck.Run --args '[60]' --timeout 120
public static class DefensePlayCheck
{
    static readonly Vector3 YardCentre = new(267f, 0f, 282f);

    public static async Task<string> Run(float breachSeconds = 60f)
    {
        if (!Application.isPlaying)
            return "enter play mode in WorldMap first";
        Application.runInBackground = true;
        var log = new StringBuilder();
        LifetimeScope scope = LifetimeScope.Find<GameLifetimeScope>();
        IObjectResolver c = scope != null ? scope.Container : null;
        if (c == null || !c.TryResolve(out BarrierRegistry barriers) || !c.TryResolve(out TargetRegistry targets) || !c.TryResolve(out EnemyNavigation nav))
            return "missing services (DefenseInstaller / EnemyInstaller)";
        var commands = c.Resolve<CommandRegistry>();
        var enemies = c.Resolve<EnemyRegistry>();
        IPlayer player = c.Resolve<PlayerRegistry>().Current;
        int broken = 0, restored = 0;
        using var subs = DisposableBag.Create(
            c.Resolve<ISubscriber<BarrierBroken>>().Subscribe(_ => broken++),
            c.Resolve<ISubscriber<BarrierRestored>>().Subscribe(_ => restored++));

        log.AppendLine($"barriers {barriers.Segments.Count}, enemy breach targets {targets.Barriers.Count}");
        BarrierSegment gate = barriers.Segments.First(s => s.Gate != null);
        Vector3 inside = player.SpawnPosition;
        Vector3 outward = Flat(gate.transform.position - YardCentre).normalized;
        Vector3 outside = gate.transform.position + outward * 6f;

        await Flush();
        log.AppendLine($"gate shut: path in {nav.IsReachable(outside, inside)} (want False)");
        gate.Gate.Toggle(inside);
        await Seconds(0.8f);
        await Flush();
        log.AppendLine($"gate open: path in {nav.IsReachable(outside, inside)} (want True), targetable {gate.IsTargetable} (want False)");
        gate.Gate.Toggle(inside);
        await Seconds(1f);
        await Flush();
        log.AppendLine($"gate shut again: path in {nav.IsReachable(outside, inside)} (want False), open {gate.Gate.IsOpen}");

        // The middle of the back side, and a corner slot.
        BarrierSegment back = barriers.Segments.First(s => s.name == "Fence 0-07");
        BarrierSegment corner = barriers.Segments.First(s => s.name == "Fence 0-14");
        Vector3 backOutside = back.transform.position + Flat(back.transform.position - YardCentre).normalized * 6f;
        DamageResult friendly = back.TakeDamage(new DamageInfo(500f, DamageType.Gunshot, back.transform.position, default, player.Transform.gameObject));
        log.AppendLine($"player shot: applied {friendly.Applied} (want 0)");
        back.DealDamage(10000f);
        await Seconds(0.5f);
        await Flush();
        log.AppendLine($"{back.name} broken {back.IsBroken}: path in through it {nav.IsReachable(backOutside, inside)} (want True), events {broken}");
        back.Restore();
        await Seconds(0.3f);
        await Flush();
        log.AppendLine($"restored: path in {nav.IsReachable(backOutside, inside)} (want False), events {restored}");
        Vector3 cornerOutside = corner.transform.position + corner.transform.forward * (Vector3.Dot(corner.transform.forward, corner.transform.position - YardCentre) > 0f ? 4f : -4f);
        corner.DealDamage(10000f);
        await Seconds(0.5f);
        await Flush();
        log.AppendLine($"{corner.name} (corner slot) broken: path in through it {nav.IsReachable(cornerOutside, inside)} (want True)");
        corner.Restore();
        await Seconds(0.3f);
        await Flush();

        // Goblins against the closed fence; the player stays inside with god mode.
        commands.Execute("cheat.god on");
        commands.Execute("enemy.clear");
        int brokenBefore = broken;
        CommandResult spawn = commands.Execute("enemy.spawn goblin 6");
        log.AppendLine("spawn: " + spawn.Message);
        float end = Time.time + breachSeconds;
        float firstBreach = -1f, start = Time.time;
        int maxOnFence = 0, inYard = 0;
        while (Time.time < end)
        {
            await Seconds(0.5f);
            int onFence = 0;
            inYard = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyBrain e = enemies[i];
                if (e.Target is IBreachable)
                    onFence++;
                if (InYard(e.transform.position))
                    inYard++;
            }
            maxOnFence = Mathf.Max(maxOnFence, onFence);
            if (firstBreach < 0f && broken > brokenBefore)
                firstBreach = Time.time - start;
            if (inYard > 0 && firstBreach >= 0f)
                break;
        }
        float lowest = barriers.Segments.Min(s => s.HealthShare);
        log.AppendLine($"breach: most attacking the fence at once {maxOnFence}, first break after {firstBreach:F1} s, segments broken {broken - brokenBefore}, " +
            $"goblins inside the yard {inYard}, lowest segment {lowest:P0}");

        commands.Execute("enemy.clear");
        commands.Execute("fence.repair");
        commands.Execute("cheat.god off");
        return log.ToString();
    }

    // Yard rectangle (34 × 27 m turned 50°, WorldMapLayout.FenceHalf / HomeYaw).
    static bool InYard(Vector3 p)
    {
        Vector3 d = Flat(p - YardCentre);
        float yaw = 50f * Mathf.Deg2Rad;
        float x = d.x * Mathf.Cos(yaw) - d.z * Mathf.Sin(yaw);
        float z = d.x * Mathf.Sin(yaw) + d.z * Mathf.Cos(yaw);
        return Mathf.Abs(x) < 16.5f && Mathf.Abs(z) < 13f;
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    static async Task Flush()
    {
        AstarPath.active.navmeshUpdates.ForceUpdate();
        AstarPath.active.FlushWorkItems();
        await Seconds(0.1f);
    }

    static async Task Seconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until)
            await Task.Yield();
    }
}
