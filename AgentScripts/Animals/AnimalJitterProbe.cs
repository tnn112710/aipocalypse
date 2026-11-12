// Play-mode probe of how much the farm animals shake when crowded (WorldMap, enter Play first):
//   unity command script run --file AgentScripts/Animals/AnimalJitterProbe.cs --entry AnimalJitterProbe.Run --timeout 120
// Packs the herd into a small circle in the pen, lets them settle, then samples every frame for a few seconds: for each
// animal standing still (stopped, or eating / asleep), how far it turns and slides per second and how often its turning
// flips direction. A calm animal turns and slides close to nothing; a shaking one flips many times a second.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using Pathfinding;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public static class AnimalJitterProbe
{
    sealed class Track
    {
        public float Turn, Slide, Seconds;
        public int Flips;
        // While walking: how far it turned and how often the turning flipped, and how far it got.
        public float WalkTurn, WalkSeconds, WalkPath;
        public int WalkFlips;
        public float LastWalkDelta;
        public Vector3 WalkStart;
        public bool Walked;
        public float LastYaw, LastDelta;
        public Vector3 LastPosition;
    }

    public static async Task<string> Run(float crowdRadius = 2.5f, float settle = 3f, float sample = 8f)
    {
        if (!Application.isPlaying)
            return "enter play mode in WorldMap first";
        Application.runInBackground = true;
        IObjectResolver c = LifetimeScope.Find<GameLifetimeScope>().Container;
        var herd = c.Resolve<AnimalHerd>();
        var clock = c.Resolve<WorldClock>();
        if (clock.Now.Hour < 8f || clock.Now.Hour > 17f)
            clock.JumpTo(9f);
        if (herd.Pen.Gate.IsOpen)
            herd.Pen.Gate.Close();

        // Pack them round the pen's middle.
        int n = herd.Animals.Count;
        for (int i = 0; i < n; i++)
        {
            float angle = i * Mathf.PI * 2f / n;
            float r = crowdRadius * Mathf.Sqrt((i + 0.5f) / n);
            herd.Animals[i].Mover.Teleport(herd.Pen.Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r);
        }
        await Seconds(settle);

        var tracks = herd.Animals.ToDictionary(a => a, a => new Track { LastYaw = a.transform.eulerAngles.y, LastPosition = a.transform.position });
        float until = Time.realtimeSinceStartup + sample;
        int lastFrame = Time.frameCount;
        while (Time.realtimeSinceStartup < until)
        {
            await Task.Yield();
            if (Time.frameCount == lastFrame)
                continue;
            lastFrame = Time.frameCount;
            float dt = Time.deltaTime;
            foreach (Animal animal in herd.Animals)
            {
                Track t = tracks[animal];
                float yaw = animal.transform.eulerAngles.y;
                Vector3 position = animal.transform.position;
                bool still = animal.GetComponent<FollowerEntity>().isStopped || animal.Eating || animal.Sleeping;
                if (still)
                {
                    float delta = Mathf.DeltaAngle(t.LastYaw, yaw);
                    t.Turn += Mathf.Abs(delta);
                    Vector3 slide = position - t.LastPosition;
                    slide.y = 0f;
                    t.Slide += slide.magnitude;
                    t.Seconds += dt;
                    if (Mathf.Abs(delta) > 0.05f)
                    {
                        if (t.LastDelta != 0f && Mathf.Sign(delta) != Mathf.Sign(t.LastDelta))
                            t.Flips++;
                        t.LastDelta = delta;
                    }
                }
                else
                {
                    t.LastDelta = 0f;
                    float delta = Mathf.DeltaAngle(t.LastYaw, yaw);
                    if (!t.Walked)
                    {
                        t.Walked = true;
                        t.WalkStart = position;
                    }
                    t.WalkTurn += Mathf.Abs(delta);
                    t.WalkSeconds += dt;
                    Vector3 step = position - t.LastPosition;
                    step.y = 0f;
                    t.WalkPath += step.magnitude;
                    if (Mathf.Abs(delta) > 0.05f)
                    {
                        if (t.LastWalkDelta != 0f && Mathf.Sign(delta) != Mathf.Sign(t.LastWalkDelta))
                            t.WalkFlips++;
                        t.LastWalkDelta = delta;
                    }
                }
                t.LastYaw = yaw;
                t.LastPosition = position;
            }
        }

        var sb = new StringBuilder();
        float totalTurn = 0f, totalSlide = 0f, totalFlips = 0f, totalSeconds = 0f;
        foreach (var (animal, t) in tracks.Select(p => (p.Key, p.Value)))
        {
            if (t.WalkSeconds > 0.5f)
                sb.AppendLine($"{animal.name,-12} walking {t.WalkSeconds:0.0}s: {t.WalkPath / t.WalkSeconds:0.00} m/s, turn {t.WalkTurn / t.WalkSeconds,6:0.0} deg/s, flips {t.WalkFlips / t.WalkSeconds,4:0.0}/s");
            if (t.Seconds < 0.5f)
                continue;
            sb.AppendLine($"{animal.name,-12} still {t.Seconds:0.0}s: turn {t.Turn / t.Seconds,6:0.0} deg/s, slide {t.Slide / t.Seconds * 100f,5:0.0} cm/s, flips {t.Flips / t.Seconds,4:0.0}/s");
            totalTurn += t.Turn;
            totalSlide += t.Slide;
            totalFlips += t.Flips;
            totalSeconds += t.Seconds;
        }
        string summary = totalSeconds > 0f
            ? $"HERD (still time {totalSeconds:0}s): turn {totalTurn / totalSeconds:0.0} deg/s, slide {totalSlide / totalSeconds * 100f:0.0} cm/s, flips {totalFlips / totalSeconds:0.00}/s"
            : "nobody stood still";
        return summary + "\n" + sb;
    }

    static async Task Seconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until)
            await Task.Yield();
    }
}
