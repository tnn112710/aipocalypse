using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;

// Play mode: steps the clock through the listed hours and reports the sun/moon light, ambient, grade and the player's light.
public static class LightSweep
{
    public static async Task<string> Run(string hours, float settle)
    {
        var clock = Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve<WorldClock>();
        var director = Object.FindAnyObjectByType<EnvironmentDirector>();
        var player = Object.FindObjectsByType<Player>().First();
        var vision = player.transform.Find("VisionLight").GetComponent<Light>();
        var darkness = player.GetComponentInChildren<TimeOfDayWeight>();
        var sun = RenderSettings.sun;
        var volumes = Object.FindObjectsByType<Volume>();
        var sb = new StringBuilder("hour look | src light int col | ambSky | exp grades on | dark vision on int range\n");
        clock.Freeze("LightSweep");
        try
        {
            foreach (float hour in hours.Split(',').Select(float.Parse))
            {
                clock.SetTime(WorldTime.At(clock.Now.Day, hour));
                await Task.Delay((int)(settle * 1000f));
                var stack = VolumeManager.instance.stack.GetComponent<ColorAdjustments>();
                string on = string.Join("+", volumes.Where(v => v.isActiveAndEnabled && v.weight > 0f).Select(v => $"{v.sharedProfile?.name}@{v.weight:F2}"));
                bool moon = director.State.MoonColor == sun.color && director.State.SunColor != sun.color;
                sb.AppendLine($"{hour,5:F1} {director.LookHour,5:F1} | {(moon ? "moon" : "sun ")} {sun.intensity:F3} {Hex(sun.color)} | {Hex(RenderSettings.ambientSkyColor)} " +
                              $"| {stack.postExposure.value:F2} {on} | {darkness?.Weight:F2} {vision.enabled} {vision.intensity:F2} {vision.range:F1}");
            }
        }
        finally
        {
            clock.Release("LightSweep");
        }
        return sb.ToString();
    }

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
}
