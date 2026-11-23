// Records how smoothly characters move and animate, every frame, in play mode (WorldMap, enter Play first and set
// Application.runInBackground = true). Start a watch, run any probe or play, then read the report:
//   unity command script run --file AgentScripts/Characters/MotionWatch.cs --entry MotionWatch.Start --args '["dog", 30]'
//   unity command script run --file AgentScripts/Characters/MotionWatch.cs --entry MotionWatch.Report
// Kinds: dog, animals, robots, all. Per character: the fastest turn (body yaw as drawn, deg/s), snaps (a turn faster than
// 720 deg/s for a frame), turn wobble (a turn that flips direction within 0.3 s while turning), animation freezes (the
// same state, not blending, its time not moving for 3+ frames), cuts (a state change with no blend) and the biggest jump
// of the animator's Speed in one frame. The report lands in Temp/MotionWatch.txt, so it survives between runs.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.DogSystem;
using MB13.Aipocalypse.Core.RobotSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MotionWatch
{
    const string ReportPath = "Temp/MotionWatch.txt";
    const float SnapRate = 720f;
    const float TurningRate = 30f;
    const float WobbleWindow = 0.3f;
    const int FreezeFrames = 3;
    const int MaxEvents = 4;
    const float SlideSpeed = 1f;

    static float _from;

    sealed class Track
    {
        public string Name;
        public Transform Body;
        public Animator Animator;
        public Renderer[] Renderers;
        public Dictionary<int, string> States;
        public HashSet<int> Blends;
        public readonly Dictionary<string, float> Slides = new();
        readonly Dictionary<string, int> _kinds = new();
        public float Seconds, PeakTurn, PeakSpeedStep, Moved, LastYaw, LastTurnSign, LastTurnAt = -1f, LastTime;
        public int Snaps, Wobbles, Freezes, Cuts, FrozenRun, LastState;
        public float LastNormalized = -1f;
        public bool LastBlending, Started;
        public float LastSpeed;
        public Vector3 LastPosition;
        public readonly List<string> Events = new();

        // At most MaxEvents of each kind (the line's first word).
        public void Note(string line)
        {
            string kind = line.Split(' ')[0];
            _kinds.TryGetValue(kind, out int n);
            _kinds[kind] = n + 1;
            if (n < MaxEvents)
                Events.Add($"{Time.time:0.00}s {line}");
        }

        public string State(int hash) => States != null && States.TryGetValue(hash, out string name) ? name : hash.ToString();
    }

    public static string Start(string kind, float seconds)
    {
        if (!Application.isPlaying)
            return "enter play mode in WorldMap first";
        Application.runInBackground = true;
        List<Track> tracks = Subjects(kind);
        if (tracks.Count == 0)
            return $"nothing to watch for '{kind}'";
        File.WriteAllText(ReportPath, "running\n");
        _from = Time.time;
        float end = Time.realtimeSinceStartup + seconds;
        int frame = -1;
        void Tick()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= Tick;
                return;
            }
            if (Time.frameCount == frame)
                return;
            frame = Time.frameCount;
            foreach (Track t in tracks)
                Sample(t);
            if (Time.realtimeSinceStartup < end)
                return;
            EditorApplication.update -= Tick;
            File.WriteAllText(ReportPath, Write(tracks));
        }
        EditorApplication.update += Tick;
        return $"watching {tracks.Count} ({string.Join(", ", tracks.Select(t => t.Name))}) for {seconds:0} s";
    }

    public static string Report() => File.Exists(ReportPath) ? File.ReadAllText(ReportPath) : "no report yet";

    static List<Track> Subjects(string kind)
    {
        var list = new List<(string, Animator)>();
        bool all = kind == "all";
        if (all || kind == "dog")
            foreach (Dog dog in Object.FindObjectsByType<Dog>())
                list.Add((dog.name, dog.GetComponentInChildren<Animator>()));
        if (all || kind == "animals")
            foreach (Animal animal in Object.FindObjectsByType<Animal>())
                list.Add((animal.name, animal.GetComponentInChildren<Animator>()));
        if (all || kind == "robots")
            foreach (Robot robot in Object.FindObjectsByType<Robot>())
                list.Add((robot.name, robot.GetComponentInChildren<Animator>()));
        return list.Where(p => p.Item2 != null).Select(p => new Track
        {
            Name = p.Item1,
            Animator = p.Item2,
            // The animator's own transform: the body as drawn (a dog in its house or mid-jump turns its model, not its root).
            Body = p.Item2.transform,
            Renderers = p.Item2.GetComponentsInChildren<Renderer>(),
            States = Names(p.Item2, out HashSet<int> blends),
            Blends = blends,
        }).ToList();
    }

    static Dictionary<int, string> Names(Animator animator, out HashSet<int> blends)
    {
        blends = new HashSet<int>();
        HashSet<int> trees = blends;
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null && animator.runtimeAnimatorController is AnimatorOverrideController o)
            controller = o.runtimeAnimatorController as AnimatorController;
        if (controller == null)
            return null;
        var names = new Dictionary<int, string>();
        void Walk(AnimatorStateMachine machine)
        {
            foreach (ChildAnimatorState s in machine.states)
            {
                names[s.state.nameHash] = s.state.name;
                if (s.state.motion is BlendTree)
                    trees.Add(s.state.nameHash);
            }
            foreach (ChildAnimatorStateMachine m in machine.stateMachines)
                Walk(m.stateMachine);
        }
        Walk(controller.layers[0].stateMachine);
        return names;
    }

    static void Sample(Track t)
    {
        if (t.Body == null || t.Animator == null || !t.Animator.isActiveAndEnabled)
            return;
        float now = Time.time;
        float yaw = t.Body.eulerAngles.y;
        Vector3 position = t.Body.position;
        AnimatorStateInfo info = t.Animator.GetCurrentAnimatorStateInfo(0);
        bool blending = t.Animator.IsInTransition(0);
        float speed = HasSpeed(t.Animator) ? t.Animator.GetFloat("Speed") : 0f;
        if (!t.Started)
        {
            t.Started = true;
            t.LastYaw = yaw;
            t.LastPosition = position;
            t.LastState = info.shortNameHash;
            t.LastNormalized = info.normalizedTime;
            t.LastBlending = blending;
            t.LastSpeed = speed;
            t.LastTime = now;
            return;
        }
        float dt = now - t.LastTime;
        if (dt <= 0f)
            return;
        t.Seconds += dt;
        Vector3 step = position - t.LastPosition;
        step.y = 0f;
        t.Moved += step.magnitude;

        // Turning: rate as drawn, snaps, and wobble (the turn flipping direction soon after turning the other way).
        float delta = Mathf.DeltaAngle(t.LastYaw, yaw);
        float rate = Mathf.Abs(delta) / dt;
        t.PeakTurn = Mathf.Max(t.PeakTurn, rate);
        if (rate > SnapRate && step.magnitude < 3f)
        {
            t.Snaps++;
            t.Note($"snap {delta:0} deg in one frame ({t.State(info.shortNameHash)})");
        }
        if (rate > TurningRate)
        {
            float sign = Mathf.Sign(delta);
            if (t.LastTurnSign != 0f && sign != t.LastTurnSign && now - t.LastTurnAt < WobbleWindow)
            {
                t.Wobbles++;
                t.Note($"wobble {delta:0.0} deg after {t.LastTurnSign:+0;-0} ({t.State(info.shortNameHash)}, {step.magnitude / dt:0.0} m/s)");
            }
            t.LastTurnSign = sign;
            t.LastTurnAt = now;
        }

        // Freezes: same state, not blending, playing at a speed, its time standing still (a non-looping clip held on its
        // last frame counts only when its state is meant to loop).
        bool sameState = info.shortNameHash == t.LastState;
        bool held = !info.loop && info.normalizedTime >= 1f;
        bool frozen = Animated(t) && sameState && !blending && !held && t.Animator.speed > 0f && info.speed * info.speedMultiplier != 0f
                      && Mathf.Approximately(info.normalizedTime, t.LastNormalized);
        if (frozen)
        {
            t.FrozenRun++;
            if (t.FrozenRun == FreezeFrames)
            {
                t.Freezes++;
                t.Note($"frozen in {t.State(info.shortNameHash)} at {info.normalizedTime:0.00}");
            }
        }
        else
            t.FrozenRun = 0;

        // Sliding: travelling in a single-clip state (an act, a pose) that isn't a locomotion blend.
        if (!blending && Animated(t) && dt > 0f && step.magnitude / dt > SlideSpeed && t.Blends != null && !t.Blends.Contains(info.shortNameHash))
        {
            string name = t.State(info.shortNameHash);
            t.Slides.TryGetValue(name, out float slid);
            t.Slides[name] = slid + dt;
        }

        // Cuts: the state changed with no blend on either side of the change.
        if (!sameState && !blending && !t.LastBlending)
        {
            t.Cuts++;
            t.Note($"cut {t.State(t.LastState)} -> {t.State(info.shortNameHash)}");
        }

        t.PeakSpeedStep = Mathf.Max(t.PeakSpeedStep, Mathf.Abs(speed - t.LastSpeed));
        t.LastYaw = yaw;
        t.LastPosition = position;
        t.LastState = info.shortNameHash;
        t.LastNormalized = info.normalizedTime;
        t.LastBlending = blending;
        t.LastSpeed = speed;
        t.LastTime = now;
    }

    // A culled animator (off screen, not AlwaysAnimate) stops on purpose.
    static bool Animated(Track t)
    {
        if (t.Animator.cullingMode == AnimatorCullingMode.AlwaysAnimate)
            return true;
        foreach (Renderer r in t.Renderers)
            if (r != null && r.isVisible)
                return true;
        return false;
    }

    static bool HasSpeed(Animator animator)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float)
                return true;
        return false;
    }

    static string Write(List<Track> tracks)
    {
        var sb = new StringBuilder($"game time {_from:0.0}-{Time.time:0.0} s\n");
        foreach (Track t in tracks)
        {
            sb.AppendLine($"{t.Name}: {t.Seconds:0}s, moved {t.Moved:0} m, peak turn {t.PeakTurn:0} deg/s, snaps {t.Snaps}, wobbles {t.Wobbles}, " +
                          $"freezes {t.Freezes}, cuts {t.Cuts}, Speed step {t.PeakSpeedStep:0.00}");
            if (t.Slides.Count > 0)
                sb.AppendLine("    travelling in a single clip: " + string.Join(", ", t.Slides.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value:0.0}s")));
            foreach (string e in t.Events)
                sb.AppendLine("    " + e);
        }
        return sb.ToString();
    }
}
