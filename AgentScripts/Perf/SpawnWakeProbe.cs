// What waking a pooled enemy costs, part by part (run in play mode on WorldMap):
//   unity command script run --file AgentScripts/Perf/SpawnWakeProbe.cs --entry SpawnWakeProbe.Run
// Cold wake = first activation of a prewarmed (parked) enemy; the pool wakes each once while prewarming, so Awake and Start
// should be paid already.
// Warm wake = a used enemy taken again. Then each part toggled on an active enemy, and a 30-enemy crowd under the shared
// "Enemies" parent vs at the scene root. Editor numbers (Mono, unoptimised): compare them with each other, not with a build.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.SpawnSystem;
using Pathfinding;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

public static class SpawnWakeProbe
{
    public static Task<string> Run(int cold = 6, int crowd = 30, int frames = 120)
    {
        if (!EditorApplication.isPlaying) return Task.FromResult("enter play mode first");
        Application.runInBackground = true;
        var tcs = new TaskCompletionSource<string>();
        var steps = Steps(cold, crowd, frames, tcs).GetEnumerator();
        int last = -1;
        void Tick()
        {
            if (Time.frameCount == last) return;
            last = Time.frameCount;
            try
            {
                if (!steps.MoveNext()) EditorApplication.update -= Tick;
            }
            catch (Exception e)
            {
                EditorApplication.update -= Tick;
                tcs.TrySetResult("error: " + e);
            }
        }
        EditorApplication.update += Tick;
        return tcs.Task;
    }

    // One step per frame (yield = wait a frame).
    static IEnumerable<int> Steps(int cold, int crowd, int frames, TaskCompletionSource<string> done)
    {
        var sb = new StringBuilder();
        var resolver = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var pool = resolver.Resolve<PrefabPool>();
        var nav = resolver.Resolve<EnemyNavigation>();
        var card = resolver.Resolve<EnemySystemSettings>().Cards[0];
        var prefab = card.Variants.Keys.First(k => k != null);
        EnemyBrain Brain(Spawnable s) => s.GetComponent<EnemyBrain>();
        var zone = Object.FindAnyObjectByType<SpawnZone>();
        Vector3 forward = zone.transform.forward;
        forward.y = 0f;
        Vector3 basePoint = zone.Origin + forward.normalized * 110f;
        var spawned = new List<Spawnable>();

        Vector3 Point(int i)
        {
            Vector3 p = basePoint + new Vector3((i % 6) * 2.5f, 0f, (i / 6) * 2.5f);
            return nav.TrySnap(p, 6f, out Vector3 s) ? s : p;
        }

        var startup = Marker("ScriptRunDelayedStartupFrame");
        sb.AppendLine($"prefab {prefab.name}; startup marker: {startup.name ?? "none"}");

        // Prewarm: parked, never woken.
        int want = pool.Made(prefab) + cold + crowd;
        pool.Prewarm(prefab, want);
        for (int guard = 0; (pool.Made(prefab) < want || pool.Warming) && guard < 600; guard++) yield return 0;
        yield return 0;

        // Cold wakes, one per frame; Start runs in the next frame's PlayerLoop.
        var coldMs = new List<double>();
        var startMs = new List<double>();
        for (int i = 0; i < cold; i++)
        {
            var sw = Stopwatch.StartNew();
            var e = pool.Get(prefab, Point(i), Quaternion.identity);
            sw.Stop();
            coldMs.Add(sw.Elapsed.TotalMilliseconds);
            spawned.Add(e);
            yield return 0;
            if (startup.rec.Valid) startMs.Add(startup.rec.LastValue / 1e6);
        }
        for (int i = 0; i < 20; i++) yield return 0;

        // Despawn, then warm wakes.
        var offMs = new List<double>();
        foreach (var e in spawned)
        {
            var sw = Stopwatch.StartNew();
            e.gameObject.SetActive(false);
            offMs.Add(sw.Elapsed.TotalMilliseconds);
        }
        spawned.Clear();
        yield return 0;
        var warmMs = new List<double>();
        for (int i = 0; i < cold; i++)
        {
            var sw = Stopwatch.StartNew();
            var e = pool.Get(prefab, Point(i), Quaternion.identity);
            sw.Stop();
            warmMs.Add(sw.Elapsed.TotalMilliseconds);
            spawned.Add(e);
            yield return 0;
        }
        sb.AppendLine($"cold wake (Awake+OnEnable) {Stat(coldMs)} | next-frame startup {Stat(startMs)}");
        sb.AppendLine($"despawn SetActive(false) {Stat(offMs)}");
        sb.AppendLine($"warm wake (OnEnable, Activate) {Stat(warmMs)}");
        for (int i = 0; i < 30; i++) yield return 0;

        // Parts, toggled on one active, warmed enemy.
        var probe = Brain(spawned[0]);
        var model = probe.Animation != null ? probe.GetComponentInChildren<Animator>(true) : null;
        var agent = probe.GetComponent<FollowerEntity>();
        var renderers = probe.GetComponentsInChildren<Renderer>(true);
        var colliders = probe.GetComponentsInChildren<Collider>(true);
        var parts = new (string name, Action off, Action on)[]
        {
            ("Animator.enabled", () => model.enabled = false, () => model.enabled = true),
            ("Model GameObject (Animator's)", () => model.gameObject.SetActive(false), () => model.gameObject.SetActive(true)),
            ("FollowerEntity.enabled", () => agent.enabled = false, () => agent.enabled = true),
            ("BehaviorTree Stop/Start", () => probe.Tree.StopBehavior(false), () => probe.Tree.StartBehavior()),
            ($"Renderers x{renderers.Length}", () => { foreach (var r in renderers) r.enabled = false; }, () => { foreach (var r in renderers) r.enabled = true; }),
            ($"Colliders x{colliders.Length}", () => { foreach (var c in colliders) c.enabled = false; }, () => { foreach (var c in colliders) c.enabled = true; }),
            ("EnemyBrain.enabled (Activate)", () => probe.enabled = false, () => probe.enabled = true),
        };
        foreach (var part in parts)
        {
            var on = new List<double>();
            var off = new List<double>();
            for (int k = 0; k < 5; k++)
            {
                var sw = Stopwatch.StartNew();
                part.off();
                off.Add(sw.Elapsed.TotalMilliseconds);
                yield return 0;
                sw.Restart();
                part.on();
                on.Add(sw.Elapsed.TotalMilliseconds);
                yield return 0;
                yield return 0;
            }
            sb.AppendLine($"  {part.name}: on {Stat(on)} | off {Stat(off)}");
        }
        foreach (var e in spawned) e.gameObject.SetActive(false);
        spawned.Clear();
        yield return 0;

        // Crowd: shared parent vs scene root.
        for (int i = 0; i < crowd; i++)
        {
            spawned.Add(pool.Get(prefab, Point(i), Quaternion.identity));
            yield return 0;
        }
        for (int i = 0; i < 60; i++) yield return 0;
        var loop = ProfilerRecorder.StartNew(new ProfilerCategory("Internal"), "PlayerLoop", frames + 10);
        var anim = Marker("PreLateUpdate.DirectorUpdateAnimationEnd");
        var animBegin = Marker("PreLateUpdate.DirectorUpdateAnimationBegin");
        string Measure(string label)
        {
            var l = Samples(loop);
            return $"{label}: PlayerLoop {Stat(l)} | anim begin {Stat(Samples(animBegin.rec))} | anim end {Stat(Samples(anim.rec))}";
        }
        Reset(loop, anim.rec, animBegin.rec);
        for (int i = 0; i < frames; i++) yield return 0;
        sb.AppendLine(Measure($"crowd {crowd} under '{spawned[0].transform.parent?.name}'"));
        Transform parent = spawned[0].transform.parent;
        foreach (var e in spawned) e.transform.SetParent(null, true);
        for (int i = 0; i < 10; i++) yield return 0;
        Reset(loop, anim.rec, animBegin.rec);
        for (int i = 0; i < frames; i++) yield return 0;
        sb.AppendLine(Measure($"crowd {crowd} at the scene root"));
        foreach (var e in spawned) { e.transform.SetParent(parent, true); e.gameObject.SetActive(false); }
        loop.Dispose();
        anim.rec.Dispose();
        animBegin.rec.Dispose();
        startup.rec.Dispose();
        done.TrySetResult(sb.ToString());
    }

    static (string name, ProfilerRecorder rec) Marker(string contains)
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var h in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            if (d.Name.Contains(contains))
                return (d.Name, ProfilerRecorder.StartNew(d.Category, d.Name, 400));
        }
        return (null, default);
    }

    static void Reset(params ProfilerRecorder[] recs)
    {
        foreach (var r in recs)
        {
            if (!r.Valid) continue;
            r.Reset();
            r.Start();
        }
    }

    static List<double> Samples(ProfilerRecorder r)
    {
        var list = new List<double>();
        if (!r.Valid) return list;
        for (int i = 0; i < r.Count; i++) list.Add(r.GetSample(i).Value / 1e6);
        return list;
    }

    static string Stat(List<double> v)
    {
        if (v.Count == 0) return "n/a";
        var s = v.OrderBy(x => x).ToList();
        return $"med {s[s.Count / 2]:F2} min {s[0]:F2} max {s[^1]:F2} ms (n {s.Count})";
    }
}
