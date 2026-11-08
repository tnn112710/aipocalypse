using System.Linq;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using VContainer;

// Play-mode helpers for checking the day/night cycle.
public static class Review
{
    static WorldClock Clock => Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve<WorldClock>();
    static EnvironmentDirector Director => Object.FindAnyObjectByType<EnvironmentDirector>();

    public static object State()
    {
        var clock = Clock;
        var d = Director;
        var light = RenderSettings.sun;
        return $"clock {clock.Now} phase {clock.Phase} moon {clock.MoonPhase:F2} speed {clock.HoursPerSecond:F4} h/s | director hour {d.Hour:F2} daylight {d.Daylight:F2} sunEl {d.SunElevation:F1} | light rot {light.transform.eulerAngles} int {light.intensity:F2} col {light.color} | fog {RenderSettings.fogColor} {RenderSettings.fogDensity:F4} amb {RenderSettings.ambientSkyColor}";
    }

    public static object SetHour(float hour)
    {
        var clock = Clock;
        clock.SetTime(WorldTime.At(clock.Now.Day, hour));
        return State();
    }

    public static object Freeze(bool freeze)
    {
        // A string key: each run_script compiles a new assembly, so typeof(Review) would differ between calls.
        if (freeze) Clock.Freeze("Review");
        else Clock.Release("Review");
        return State();
    }

    // Puts the player at a spot looking a direction, so shots are comparable.
    public static object View(float x, float y, float z, float yaw, float pitch)
    {
        var player = Object.FindObjectsByType<MB13.Aipocalypse.Core.PlayerSystem.Player>().First();
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.position = new Vector3(x, y, z);
        player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cc) cc.enabled = true;
        const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var look = player.GetType().GetField("_look", Private)?.GetValue(player);
        look?.GetType().GetField("_pitch", Private)?.SetValue(look, pitch);
        return $"player at {player.transform.position} yaw {yaw} pitch {pitch}";
    }
}
