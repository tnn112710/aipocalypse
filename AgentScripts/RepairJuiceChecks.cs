using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.WeaponSystem;
using Object = UnityEngine.Object;

// Play mode: jam pause, slap / punch beats, camera impulses, miss penalty and success hit stop on the held gun.
public static class RepairJuiceChecks
{
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    public static Task<object> Live() => LiveAsync().AsTask();

    static async UniTask<object> LiveAsync()
    {
        Require(Application.isPlaying, "Enter Play first");
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        var resolver = Object.FindAnyObjectByType<ProjectLifetimeScope>().Container;
        var player = Object.FindAnyObjectByType<Player>();
        var held = player.HeldItem as HeldGun;
        Require(held?.Gun != null, "Equip a gun first");
        var gun = held.Gun;
        var qte = resolver.Resolve<QuickTimeService>();
        var profile = gun.Item.Gun.Jam.Repair;
        float chance = profile.StubbornChance;
        var beats = new List<string>();
        int successes = 0;
        using var beatSubscription = resolver.Resolve<ISubscriber<GunJamBeat>>().Subscribe(e => beats.Add(e.Kind.ToString()));
        using var repairSubscription = resolver.Resolve<ISubscriber<GunRepairCompleted>>().Subscribe(e => { if (e.QteSuccess) successes++; });
        var camera = Object.FindAnyObjectByType<CinemachineBrain>().transform;
        var pitch = player.transform.Find("CameraRoot/CameraPitch");
        float maxShake = 0f;
        int frames = 0;
        // Sampled as the world camera starts rendering, i.e. what the player sees.
        void Rendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera rendered)
        {
            if (rendered.transform == camera)
                maxShake = Mathf.Max(maxShake, Quaternion.Angle(camera.rotation, pitch.rotation));
        }
        void Shake() => frames++;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += Rendered;
        try
        {
            profile.StubbornChance = 0f;
            gun.Interrupt();
            gun.Item.SetJammed(true);
            Call(held, "OnJammed");
            Require(Field<float>(held, "_joltTime") >= 0f, "Jam jolt did not start");
            Call(held, "OnDryFired");
            double jammedAt = Time.timeAsDouble;
            await Until(() => gun.Action == GunAction.ClearingJam, player, Shake);
            float pause = (float)(Time.timeAsDouble - jammedAt);
            Require(pause >= profile.JamPause - 0.05f, "Clearing started before the jam pause: " + pause);
            float jamShake = maxShake;
            Require(jamShake > 0.1f, $"Jam camera impulse missing: {jamShake} beats {string.Join(",", beats)} frames {frames} dt {Time.deltaTime} brain {camera.name}");

            bool punchCaptured = false;
            await Until(() => qte.IsActive, player, () =>
            {
                Shake();
                if (!punchCaptured && beats.Contains("Punch")) { punchCaptured = true; Capture("Punch"); }
            });
            Require(beats.Contains("Slap") && beats.Contains("Punch") && beats.IndexOf("Slap") < beats.IndexOf("Punch"), "Slap then punch beats missing: " + string.Join(",", beats));
            var session = (TimingQteSession)qte.Session;
            float before = Field<float>(gun, "_repairDuration");
            float progress = gun.ActionProgress;
            Require(!(session.Progress >= session.ZoneStart && session.Progress <= session.ZoneStart + session.ZoneWidth), "Pointer started inside the zone");
            qte.Advance(0f, true);
            float after = Field<float>(gun, "_repairDuration");
            float remainingBefore = before * (1f - progress), remainingAfter = after * (1f - gun.ActionProgress);
            Require(Mathf.Abs(remainingAfter - remainingBefore - profile.MissPenalty) < 0.01f, $"Miss did not add {profile.MissPenalty}s: {remainingBefore} -> {remainingAfter}");
            Require(beats.Contains("Miss") && Field<float>(held, "_joltTime") >= 0f, "Miss feedback missing");
            await Seconds(0.05f, player, Shake);
            Capture("Miss");

            await Until(() => session.AttemptAvailable && session.Progress >= session.ZoneStart + session.ZoneWidth * 0.5f && session.Progress <= session.ZoneStart + session.ZoneWidth, player, Shake);
            qte.Advance(0f, true);
            Require(successes == 1 && !gun.Item.IsJammed, "Success did not clear the jam");
            Require(Time.timeScale < 0.2f, "Success hit stop missing: " + Time.timeScale);
            double stopStart = Time.realtimeSinceStartupAsDouble;
            await UntilReal(() => Time.timeScale > 0.99f, player);
            float stop = (float)(Time.realtimeSinceStartupAsDouble - stopStart);
            Capture("Success");
            await Seconds(0.5f, player, Shake);
            Require(Field<float>(held, "_joltTime") < 0f, "Success jolt stuck");
            return new { gun = held.name, pause, jamShake, maxShake, frames, beats = string.Join(",", beats), stop, scale = Time.timeScale };
        }
        finally
        {
            profile.StubbornChance = chance;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= Rendered;
        }
    }

    static async UniTask Frame(Player player) => await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, player.destroyCancellationToken);
    static async UniTask Until(Func<bool> condition, Player player, Action each = null)
    {
        double start = Time.realtimeSinceStartupAsDouble;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble - start > 12f) throw new TimeoutException("Live juice check timed out");
            await Frame(player);
            each?.Invoke();
        }
    }
    static UniTask UntilReal(Func<bool> condition, Player player) => Until(condition, player);
    static async UniTask Seconds(float duration, Player player, Action each = null)
    {
        double start = Time.timeAsDouble;
        await Until(() => Time.timeAsDouble - start >= duration, player, each);
    }
    static void Capture(string moment)
    {
        Directory.CreateDirectory("Temp/RepairJuice");
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes("Temp/RepairJuice/" + moment + ".png", texture.EncodeToPNG()); }
        finally { Object.Destroy(texture); }
    }
}
