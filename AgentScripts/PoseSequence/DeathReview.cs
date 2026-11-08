using System.Linq;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.PlayerSystem;
using Ami.BroAudio;
using UnityEngine;

// Play-mode helpers for the death pose sequence (use VisionReview.Give for a gun, DayNight/Review for the hour).
public static class DeathReview
{
    static Player Player => Object.FindObjectsByType<Player>().First();

    public static object State()
    {
        Player player = Player;
        Transform root = player.transform.Find("CameraRoot");
        return $"t {Time.time:F2} dead {player.IsDead} seq {player.CurrentSequence?.name ?? "-"} held {player.HeldItem?.name ?? "-"} " +
               $"lowered {player.Equipment?.HandsLowered} | root {root.localPosition.ToString("F2")} {root.localEulerAngles.ToString("F1")} " +
               $"pitch {root.GetChild(0).localEulerAngles.x:F1} | pos {player.transform.position.ToString("F1")} | {ScreenState()}";
    }

    static string ScreenState()
    {
        var view = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.UI.DeathScreenView>(FindObjectsInactive.Include);
        if (view == null)
            return "no death screen";
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var eyes = (RectTransform)typeof(MB13.Aipocalypse.Core.UI.DeathScreenView).GetField("_eyes", flags)!.GetValue(view);
        var menu = (CanvasGroup)typeof(MB13.Aipocalypse.Core.UI.DeathScreenView).GetField("_menu", flags)!.GetValue(view);
        var status = (TMPro.TMP_Text)typeof(MB13.Aipocalypse.Core.UI.DeathScreenView).GetField("_status", flags)!.GetValue(view);
        float closed = (float)typeof(MB13.Aipocalypse.Core.UI.DeathScreenView).GetField("_closed", flags)!.GetValue(view);
        return $"eyes {(eyes.gameObject.activeSelf ? closed.ToString("F2") : "off")} menu {(menu.gameObject.activeSelf ? menu.alpha.ToString("F2") : "off")} " +
               $"status '{status.text}' cursor {Cursor.lockState} hud {HudAlpha()}";
    }

    static string HudAlpha()
    {
        var visibility = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.UI.HudVisibilityHud>();
        return visibility != null ? visibility.GetComponent<CanvasGroup>().alpha.ToString("F2") : "-";
    }

    // A left click through the Input System (what the death screen's UI map reads).
    public static async System.Threading.Tasks.Task<string> Click()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
        await System.Threading.Tasks.Task.Delay(150);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState());
        await System.Threading.Tasks.Task.Delay(1200);
        return (string)State();
    }

    public static object Give(string assetPath)
    {
        var item = UnityEditor.AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.EquipmentSystem.ItemDefinition>(assetPath);
        Player.Equipment.TryPickUp(item.CreateInstance(), out _);
        return State();
    }

    public static object Kill()
    {
        Player player = Player;
        ((IDamageable)player).TakeDamage(new DamageInfo(player.Health.Current, DamageType.Generic, player.transform.position));
        return State();
    }

    public static object Revive()
    {
        Player.Revive();
        return State();
    }

    // Teleports the player by (dx, dz), onto the ground there.
    public static object MoveBy(float dx, float dz)
    {
        Player player = Player;
        Vector3 to = player.transform.position + new Vector3(dx, 0f, dz);
        if (Physics.Raycast(to + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            to = hit.point + Vector3.up * 0.05f;
        var motor = (PlayerMotor)typeof(Player).GetField("_motor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(player);
        motor.Teleport(to, player.transform.rotation);
        return State();
    }

    // Faces the player (body yaw), standing.
    public static object Face(float yaw)
    {
        Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return State();
    }

    // Kills the player and samples the camera every `step` seconds: time, eye height, euler, and how far it moved since the last sample.
    public static async System.Threading.Tasks.Task<string> Trace(float seconds, float step)
    {
        Player player = Player;
        Transform root = player.transform.Find("CameraRoot");
        var sb = new System.Text.StringBuilder();
        Kill();
        float start = Time.time;
        Vector3 lastPos = root.localPosition;
        Quaternion lastRot = root.localRotation;
        float stillest = float.MaxValue;
        while (Time.time - start < seconds)
        {
            await System.Threading.Tasks.Task.Delay((int)(step * 1000f));
            float moved = Vector3.Distance(lastPos, root.localPosition) * 100f + Quaternion.Angle(lastRot, root.localRotation);
            stillest = Mathf.Min(stillest, moved);
            Vector3 e = root.localEulerAngles;
            sb.AppendLine($"{Time.time - start,5:F2}  y {root.localPosition.y:F2}  pitch {Mathf.DeltaAngle(0f, e.x),6:F1} yaw {Mathf.DeltaAngle(0f, e.y),6:F1} roll {Mathf.DeltaAngle(0f, e.z),6:F1}  moved {moved:F2}");
            lastPos = root.localPosition;
            lastRot = root.localRotation;
        }
        sb.AppendLine($"least movement per sample (cm + deg): {stillest:F2}");
        return sb.ToString();
    }

    // Play mode: shows the death menu ("menu"), or the eyes at a fixed closing amount ("eyes", 0..1), for a capture.
    public static object Screen(string what, float closed)
    {
        var view = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.UI.DeathScreenView>(FindObjectsInactive.Include);
        view.HideAll();
        if (what == "menu")
        {
            view.SetStatus(view.Style.LoadingText, false);
            DG.Tweening.TweenExtensions.Complete(view.ShowMenu());
        }
        else if (what == "eyes")
        {
            var eyes = (RectTransform)typeof(MB13.Aipocalypse.Core.UI.DeathScreenView)
                .GetField("_eyes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(view);
            eyes.gameObject.SetActive(true);
            typeof(MB13.Aipocalypse.Core.UI.DeathScreenView).GetMethod("SetEyes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(view, new object[] { closed, closed * 0.4f });
        }
        return "screen " + what;
    }

    // Samples the eyelids (closed share, darkness) every `step` seconds while they play.
    public static async System.Threading.Tasks.Task<string> EyesTrace(float seconds, float step)
    {
        var view = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.UI.DeathScreenView>(FindObjectsInactive.Include);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var closed = typeof(MB13.Aipocalypse.Core.UI.DeathScreenView).GetField("_closed", flags)!;
        var dark = typeof(MB13.Aipocalypse.Core.UI.DeathScreenView).GetField("_dark", flags)!;
        var sb = new System.Text.StringBuilder();
        float start = Time.time;
        while (Time.time - start < seconds)
        {
            sb.Append($"{Time.time:F1}:{(float)closed.GetValue(view):F2}/{(float)dark.GetValue(view):F2} ");
            await System.Threading.Tasks.Task.Delay((int)(step * 1000f));
        }
        return sb.ToString();
    }

    // Kills the player and counts, frame by frame, how many times each beat sound starts playing.
    public static async System.Threading.Tasks.Task<string> SoundStarts(float seconds)
    {
        var death = UnityEditor.AssetDatabase.LoadAssetAtPath<PoseSequence>("Assets/_Game/Data/Player/Sequences/PlayerDeath.asset");
        var ids = new System.Collections.Generic.List<(string name, Ami.BroAudio.SoundID id)>();
        foreach (PoseBeat beat in death.Beats)
        {
            if (beat.StartSound.IsValid())
                ids.Add((beat.Name + " start", beat.StartSound));
            if (beat.ArriveSound.IsValid())
                ids.Add((beat.Name + " arrive", beat.ArriveSound));
        }
        var was = new bool[ids.Count];
        var starts = new int[ids.Count];
        Kill();
        float start = Time.time;
        int lastFrame = -1;
        while (Time.time - start < seconds)
        {
            if (Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                for (int i = 0; i < ids.Count; i++)
                {
                    bool playing = Ami.BroAudio.BroAudio.HasAnyPlayingInstances(ids[i].id);
                    if (playing && !was[i])
                        starts[i]++;
                    was[i] = playing;
                }
            }
            await System.Threading.Tasks.Task.Yield();
        }
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < ids.Count; i++)
            sb.Append($"{ids[i].name}: {starts[i]}; ");
        return sb.ToString();
    }

    public static async System.Threading.Tasks.Task<string> Wait(float seconds)
    {
        await System.Threading.Tasks.Task.Delay((int)(seconds * 1000f));
        return (string)State();
    }
}
