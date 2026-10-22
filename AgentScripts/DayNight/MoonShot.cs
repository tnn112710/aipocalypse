using System.Linq;
using System.Reflection;
using Aipocalypse.Core.EnvironmentSystem;
using UnityEngine;

// Aims the player camera at the moon and reports what the sky shader is given, so phase captures are comparable.
public static class MoonShot
{
    public static object Aim()
    {
        var director = Object.FindAnyObjectByType<EnvironmentDirector>();
        Vector3 moon = director.MoonDirection;
        float yaw = Mathf.Atan2(moon.x, moon.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(moon.y) * Mathf.Rad2Deg;

        var player = Object.FindObjectsByType<Aipocalypse.Core.PlayerSystem.Player>().First();
        player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        var look = player.GetType().GetField("_look", Private)?.GetValue(player);
        look?.GetType().GetField("_pitch", Private)?.SetValue(look, pitch);

        return $"hour {director.Hour:F2} look {director.LookHour:F2} moonPhase {director.MoonPhase:F3} (shader {Shader.GetGlobalFloat("_TOD_MoonPhase"):F3}) moon elevation {Mathf.Asin(moon.y) * Mathf.Rad2Deg:F1} yaw {yaw:F0}";
    }
}
