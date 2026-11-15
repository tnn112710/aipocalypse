// Play-mode captures of the held gun's actions from the real player camera, for `unity command script run`.
// Enter play (WeaponsTest), run AimReview.Prepare and AimReview.Hold first; the Game window must be open.
//   Action(kind, progresses, tag)   kind = Reload, ReloadEmpty or Jam; progresses = "0.2,0.5,0.8": starts the action
//                                   and saves the game view at each progress to Temp/ArmReview/Play_<kind>_<p><tag>.png
//   Still(tag)                      the game view as it is
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ArmPlayReview
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const string OutFolder = "Temp/ArmReview";

    public static async Task<string> Still(string tag)
    {
        Directory.CreateDirectory(OutFolder);
        string path = Path.GetFullPath($"{OutFolder}/Play_Still{tag}.png");
        ScreenCapture.CaptureScreenshot(path);
        await Frames(3);
        return path;
    }

    public static async Task<string> Action(string kind, string progresses, string tag)
    {
        if (!Application.isPlaying)
            return "not in play mode";
        var held = Object.FindAnyObjectByType<Player>().HeldItem as HeldGun;
        if (held == null || held.Gun == null)
            return "no gun in hand";
        Gun gun = held.Gun;
        gun.Interrupt();
        GunInstance item = gun.Item;
        typeof(GunInstance).GetProperty("Rounds", Any).SetValue(item, kind == "ReloadEmpty" ? 0 : Mathf.Max(1, item.Stats.MagazineSize / 2));
        item.SetJammed(kind == "Jam");
        await Frames(2);
        typeof(Gun).GetMethod("StartAction", Any).Invoke(gun, new object[] { kind == "Jam" ? GunAction.ClearingJam : GunAction.Reloading });

        Directory.CreateDirectory(OutFolder);
        var paths = new List<string>();
        foreach (string text in progresses.Split(','))
        {
            float target = float.Parse(text.Trim(), CultureInfo.InvariantCulture);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (gun.Action != GunAction.None && gun.ActionProgress < target && Time.realtimeSinceStartup < deadline)
                await Task.Yield();
            string path = Path.GetFullPath($"{OutFolder}/Play_{kind}_{target.ToString("0.00", CultureInfo.InvariantCulture)}{tag}.png");
            ScreenCapture.CaptureScreenshot(path);
            paths.Add($"{path} (progress {gun.ActionProgress:0.00}, {gun.Action}{(gun.RepairIsStubborn ? ", stubborn" : "")})");
            await Frames(2);
        }
        return string.Join("\n", paths);
    }

    static async Task Frames(int count)
    {
        int until = Time.frameCount + count;
        while (Time.frameCount < until)
            await Task.Yield();
    }
}
