using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Core.UI;
using Aipocalypse.Core.TimeSystem;
using Object = UnityEngine.Object;

public static class RepairFeedbackChecks
{
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    public static Task<object> Live(string presentation = "Circle", string outcome = "Success") => LiveAsync(presentation, outcome).AsTask();

    static async UniTask<object> LiveAsync(string presentation, string outcome)
    {
        Require(Application.isPlaying, "Enter Play first");
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        var resolver = Object.FindAnyObjectByType<ProjectLifetimeScope>().Container;
        var player = Object.FindAnyObjectByType<Player>();
        var held = player.HeldItem as HeldGun;
        Require(held?.Gun != null, "Equip a gun first");
        var qte = resolver.Resolve<QuickTimeService>();
        var hud = Object.FindAnyObjectByType<RepairQteHud>();
        var content = Field<GameObject>(hud, "_content");
        var burst = Field<UnityEngine.UI.Image>(hud, "_burst");
        var group = Field<CanvasGroup>(hud, "_group");
        var source = Field<AudioSource>(held, "_audio");
        var profile = held.Gun.Item.Gun.Jam.Repair;
        var challenge = (TimingQteChallenge)profile.Qte;
        var presentations = challenge.Presentations;
        float chance = profile.StubbornChance;
        int attempts = 0, repairs = 0;
        bool quick = false;
        using var attemptSubscription = resolver.Resolve<ISubscriber<QteAttempted>>().Subscribe(e => attempts++);
        using var repairSubscription = resolver.Resolve<ISubscriber<GunRepairCompleted>>().Subscribe(e => { repairs++; quick = e.QteSuccess; });
        try
        {
            held.Gun.Interrupt();
            profile.StubbornChance = 0f;
            challenge.Presentations = new[] { presentation };
            held.Gun.Item.SetRounds(Mathf.Max(1, held.Gun.Item.Gun.Magazine.Size - 1));
            held.Gun.Item.SetJammed(true);
            await Until(() => qte.IsActive, player);
            await Frame(player);
            var session = (TimingQteSession)qte.Session;
            Require(content.activeSelf && qte.Presentation == presentation, "Requested QTE view missing");
            int rounds = held.Gun.Item.Rounds;
            float maximumKick = 0f;
            if (outcome != "Natural" && outcome != "Cancel")
            {
                qte.Advance(0f, true);
                Require(session.Misses == 1 && attempts == 1, "Miss event missing");
                Require(Field<float>(hud, "_flash") > 0f && source.isPlaying, "Miss presentation or audio missing");
                qte.Advance(0f, true);
                Require(attempts == 1, "Ignored spam produced feedback");
                float heldPointer = session.Progress, originalZone = session.ZoneStart, repairProgress = held.Gun.ActionProgress;
                await Seconds(0.045f, player);
                Require(session.IsPointerPaused && Mathf.Abs(session.Progress - heldPointer) < 0.0001f && session.ZoneStart == originalZone, "Pointer did not hold before zone relocation");
                Require(held.Gun.ActionProgress > repairProgress, "Pointer hold stalled gun repair");
                Require(Field<float>(hud, "_flash") > 0.1f, "Miss flash ended too early");
                var selected = Field<QteView>(hud, "_selected") as TimingQteView;
                Require(Field<UnityEngine.UI.Image>(selected, "_track").color.r > 0.35f, "Miss flash was not rendered to the widget");
                Capture(held.name, presentation, "Miss");
                await Until(() => !session.IsPointerPaused, player);
                Require(Mathf.Abs(session.ZoneStart - originalZone) > 0.1f, "Green zone did not relocate after miss recovery");
                if (outcome == "Success")
                {
                    await Until(() => session.AttemptAvailable && session.Progress >= session.ZoneStart + session.ZoneWidth * 0.5f && session.Progress <= session.ZoneStart + session.ZoneWidth, player);
                    qte.Advance(0f, true);
                    Require(!held.Gun.Item.IsJammed && !qte.IsActive && repairs == 1 && quick, "Early success did not finish repair exactly once");
                    Require(Field<bool>(hud, "_finishing") && burst.gameObject.activeSelf, "Success burst did not start");
                    await Seconds(0.055f, player);
                    maximumKick = Field<float>(held, "_joltTime") >= 0f ? 1f : 0f;
                    Require(maximumKick > 0.1f && source.isPlaying, "Weapon kick or success sound did not play");
                    Capture(held.name, presentation, "Success");
                    var clock = resolver.Resolve<TimeService>();
                    float alpha = group.alpha;
                    clock.SetPaused(true);
                    try
                    {
                        for (int i = 0; i < 4; i++) await Frame(player);
                        Require(Mathf.Abs(group.alpha - alpha) < 0.001f && Field<float>(held, "_joltTime") >= 0f,
                            "Repair feedback advanced while paused");
                        Require(!content.activeSelf, "Feedback remained visible over pause");
                    }
                    finally { clock.SetPaused(false); }
                    await Seconds(0.3f, player);
                    Require(content.activeSelf && group.alpha > 0.99f && Field<bool>(hud, "_finishing"), "Success faded before the confirmation hold finished");
                    var successView = Field<QteView>(hud, "_selected") as TimingQteView;
                    Require(Field<UnityEngine.UI.Image>(successView, "_fill").fillAmount == 1f && !Field<UnityEngine.UI.Image>(successView, "_marker").enabled,
                        "Success widget did not show a full green confirmation");
                    Capture(held.name, presentation, "SuccessHold");
                }
            }
            if (outcome == "Natural")
            {
                await Until(() => !held.Gun.Item.IsJammed, player);
                Require(repairs == 1 && !quick && Field<bool>(hud, "_finishing"), "Natural completion did not fade");
                Require(!burst.gameObject.activeSelf && Field<float>(held, "_joltTime") < 0f, "Natural completion used QTE reward feedback");
                Require(source.isPlaying, "Natural completion sound missing");
                Capture(held.name, presentation, "Natural");
            }
            if (outcome == "Cancel")
            {
                held.Gun.Interrupt();
                await Frame(player);
                Require(repairs == 0 && !Field<bool>(hud, "_finishing") && !burst.gameObject.activeSelf, "Cancellation played result feedback");
                held.Gun.Item.SetJammed(false);
            }
            await Seconds(profile.Feedback.SuccessDuration + 0.1f, player);
            Require(!content.activeSelf && !burst.gameObject.activeSelf && !Field<bool>(hud, "_finishing"), "Feedback did not clean up");
            Require(Field<float>(held, "_joltTime") < 0f, "Weapon kick stuck");
            Require(held.Gun.Item.Rounds == rounds && Mathf.Abs(Time.timeScale - 1f) < 0.001f, "Feedback changed ammo or time");
            return new { gun = held.name, presentation, outcome, attempts, repairs, quick, maximumKick, cleanedUp = true, rounds, scale = Time.timeScale };
        }
        finally
        {
            profile.StubbornChance = chance;
            challenge.Presentations = presentations;
        }
    }

    static async UniTask Frame(Player player) => await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, player.destroyCancellationToken);
    static async UniTask Until(Func<bool> condition, Player player)
    {
        double start = Time.realtimeSinceStartupAsDouble;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble - start > 12f) throw new TimeoutException("Live feedback check timed out");
            await Frame(player);
        }
    }
    static async UniTask Seconds(float duration, Player player)
    {
        double start = Time.timeAsDouble;
        await Until(() => Time.timeAsDouble - start >= duration, player);
    }
    static void Capture(string gun, string presentation, string outcome)
    {
        Directory.CreateDirectory("Temp/RepairFeedback");
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes("Temp/RepairFeedback/" + gun.Replace("(Clone)", "") + "_" + presentation + "_" + outcome + ".png", texture.EncodeToPNG()); }
        finally { Object.Destroy(texture); }
    }
}
