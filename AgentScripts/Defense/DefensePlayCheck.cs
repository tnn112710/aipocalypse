using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
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
        // A new game starts with the front half down (DEFENSE.md, In WorldMap): rebuild it so the yard is shut.
        int startBroken = 0;
        foreach (BarrierSegment segment in barriers.Segments.Where(s => s.IsBroken).ToList())
        {
            segment.Restore();
            startBroken++;
        }
        log.AppendLine($"rebuilt {startBroken} segments that start broken");
        await Seconds(0.3f);
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

        // Bullets through the shut gate's gaps (the AK's hit layers): rays across it from outside, inward.
        int through = 0, rays = 0;
        Vector3 gateOut = Flat(gate.transform.position - YardCentre).normalized;
        for (float x = -2f; x <= 2f; x += 0.1f)
            for (float y = 0.3f; y <= 1.7f; y += 0.35f)
            {
                Vector3 origin = gate.transform.position + gate.transform.right * x + Vector3.up * y + gateOut * 1.5f;
                rays++;
                if (!Physics.Raycast(origin, -gateOut, out RaycastHit hit, 3f, 1083211827, QueryTriggerInteraction.Ignore)
                    || !hit.collider.transform.IsChildOf(gate.transform))
                    through++;
            }
        log.AppendLine($"shots through the shut gate: {through} of {rays} rays pass (want some: the gaps)");

        // Goblins against the closed fence; the player stays inside with god mode. Each broken segment's position is recorded.
        commands.Execute("cheat.god on");
        var breaks = new List<Vector3>();
        using var breakLog = c.Resolve<ISubscriber<BarrierBroken>>().Subscribe(e => breaks.Add(e.Position));

        // 1. Shut fence: they open a gap; those within GapNear of it go through, and a share of the ones further off (a third of
        // six: two) breaks a second one away from it. enemy.spawn puts them in front of the player: face out of the yard first.
        commands.Execute("enemy.clear");
        breaks.Clear();
        log.AppendLine("spawn: " + commands.Execute("enemy.spawn goblin 6").Message);
        float end = Time.time + breachSeconds, start = Time.time, firstBreach = -1f;
        int keptHitting = 0, inYard = 0;
        float nearestToGap = float.PositiveInfinity;
        while (Time.time < end)
        {
            await Seconds(0.5f);
            if (firstBreach < 0f && breaks.Count > 0)
            {
                firstBreach = Time.time - start;
                await Seconds(2f);
                for (int i = 0; i < enemies.Count; i++)
                    if (enemies[i].Target is IBreachable hit)
                    {
                        keptHitting++;
                        nearestToGap = Mathf.Min(nearestToGap, Flat(hit.Transform.position - breaks[0]).magnitude);
                    }
            }
            inYard = Count(enemies, InYard);
            if (firstBreach >= 0f && (breaks.Count >= 2 || Time.time - start > firstBreach + 25f))
                break;
        }
        log.AppendLine($"shut fence: first gap after {firstBreach:F1} s; 2 s later {keptHitting} still breaking (nearest of their segments {nearestToGap:F1} m from the gap, want >= 6), " +
            $"second gap {(breaks.Count >= 2 ? $"after {Time.time - start:F1} s, {Flat(breaks[1] - breaks[0]).magnitude:F1} m from the first" : "not yet")}, goblins inside {inYard}");

        // 2. A gap on the far side of the yard (the back fence): goblins arriving at the front make their own instead of going round.
        commands.Execute("enemy.clear");
        commands.Execute("fence.repair");
        await Seconds(0.5f);
        BarrierSegment far = barriers.Segments.First(x => x.name == "Fence 0-07");
        far.DealDamage(10000f);
        await Seconds(0.3f);
        breaks.Clear();
        log.AppendLine("spawn: " + commands.Execute("enemy.spawn goblin 6").Message);
        end = Time.time + breachSeconds;
        start = Time.time;
        int roundTheBack = 0;
        while (Time.time < end && breaks.Count == 0)
        {
            await Seconds(0.5f);
            for (int i = 0; i < enemies.Count; i++)
                if (Flat(enemies[i].transform.position - far.transform.position).magnitude < 6f)
                    roundTheBack++;
        }
        string where = breaks.Count > 0 ? $"{Flat(breaks[0] - gate.transform.position).magnitude:F1} m from the gate, {Flat(breaks[0] - far.transform.position).magnitude:F1} m from the back gap" : "none";
        log.AppendLine($"far gap: new gap after {Time.time - start:F1} s at {where}; goblin samples near the back gap {roundTheBack} (want 0)");

        commands.Execute("enemy.clear");
        commands.Execute("fence.repair");
        commands.Execute("cheat.god off");
        return log.ToString();
    }

    static int Count(EnemyRegistry enemies, Func<Vector3, bool> where)
    {
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
            if (where(enemies[i].transform.position))
                count++;
        return count;
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
