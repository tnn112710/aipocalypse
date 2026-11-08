using System.Diagnostics;
using System.Reflection;
using System.Text;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using UnityEngine;
using UnityEngine.Rendering;

// Play mode: samples the director across the day and flags sudden changes (pops), then times Apply + reactors.
public static class Sweep
{
    public static object Day(float step)
    {
        var director = Object.FindAnyObjectByType<EnvironmentDirector>();
        var apply = typeof(EnvironmentDirector).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Instance);
        var from = (Volume)typeof(EnvironmentDirector).GetField("_gradeFrom", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(director);
        var to = (Volume)typeof(EnvironmentDirector).GetField("_gradeTo", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(director);
        var light = RenderSettings.sun;
        var sb = new StringBuilder();
        float lastI = -1, lastFog = -1, lastAmb = -1, lastTo = -1;
        Quaternion lastRot = Quaternion.identity;
        for (float h = 0; h < 24f + 1e-4f; h += step)
        {
            apply.Invoke(director, new object[] { h % 24f, 0.5f });
            float amb = RenderSettings.ambientSkyColor.grayscale;
            float angle = lastI < 0 ? 0 : Quaternion.Angle(lastRot, light.transform.rotation);
            bool pop = lastI >= 0 && (Mathf.Abs(light.intensity - lastI) > 0.35f || angle > 25f || Mathf.Abs(amb - lastAmb) > 0.08f || Mathf.Abs(RenderSettings.fogDensity - lastFog) > 0.0004f);
            sb.AppendLine($"{h,5:F2} I {light.intensity,4:F2} rot {light.transform.eulerAngles.x,5:F1}/{light.transform.eulerAngles.y,5:F0} d{angle,5:F1} amb {amb:F2} fog {RenderSettings.fogDensity * 1000f:F2} day {director.Daylight:F2} sunEl {director.SunElevation,5:F1} grades {(from.sharedProfile ? from.sharedProfile.name : "-")}>{(to.sharedProfile ? to.sharedProfile.name : "-")} {to.weight:F2}{(pop ? "  <-- POP" : "")}");
            lastI = light.intensity; lastRot = light.transform.rotation; lastAmb = amb; lastFog = RenderSettings.fogDensity; lastTo = to.weight;
        }
        return sb.ToString();
    }

    public static object Cost(int frames)
    {
        var director = Object.FindAnyObjectByType<EnvironmentDirector>();
        var late = typeof(EnvironmentDirector).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        late.Invoke(director, null);
        var sw = Stopwatch.StartNew();
        long before = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < frames; i++)
            late.Invoke(director, null);
        long bytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
        sw.Stop();
        return $"{frames} LateUpdates: {sw.Elapsed.TotalMilliseconds / frames * 1000:F1} µs each (reflection call included), {bytes} bytes allocated total";
    }
}
