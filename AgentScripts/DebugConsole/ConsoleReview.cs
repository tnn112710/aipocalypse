using System.Text;
using System.Threading.Tasks;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.TimeSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using VContainer;

// Play-mode checks of the dev console through a virtual keyboard (the real one needs the Game view focused).
// Enter Play, set Application.runInBackground, then run Drive / State / Close.
public static class ConsoleReview
{
    // Opens the console with `, completes "time.sc" with Tab, runs "time.scale 0.5; help clock", logs an error,
    // then types a partial line and leaves the console open for a capture.
    public static async Task<string> Drive()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>("AgentKeyboard");
        var report = new StringBuilder();
        try
        {
            ConsoleService console = Resolve<ConsoleService>();
            if (console.IsOpen)
                console.Close();
            await Frames(2);

            await Press(keyboard, Key.Backquote);
            report.AppendLine($"after `: open={console.IsOpen} line='{console.Line.Text}'");

            await Type(keyboard, "time.sc");
            report.AppendLine($"typed: line='{console.Line.Text}' suggestions={string.Join(",", console.Suggestions)} hint='{console.Hint}'");
            await Press(keyboard, Key.Tab);
            report.AppendLine($"after Tab: line='{console.Line.Text}'");
            await Type(keyboard, "0.5; help clock");
            await Press(keyboard, Key.Enter);
            report.AppendLine($"after Enter: line='{console.Line.Text}' timeScale={Resolve<TimeService>().Scale}");

            Debug.LogError("ConsoleReview: a test error for the scrollback and the screen log");
            await Frames(3);
            await Press(keyboard, Key.UpArrow);
            report.AppendLine($"after Up: line='{console.Line.Text}'");
            await Press(keyboard, Key.Escape);
            report.AppendLine($"after Esc: open={console.IsOpen}");

            await Press(keyboard, Key.Backquote);
            await Type(keyboard, "log.screen.a");
            await Frames(2);
            report.AppendLine($"reopened: line='{console.Line.Text}' suggestions={string.Join(",", console.Suggestions)}");

            LogBuffer logs = console.Logs;
            report.AppendLine($"scrollback ({logs.Count}):");
            for (int i = Mathf.Max(0, logs.Count - 8); i < logs.Count; i++)
                report.AppendLine($"  [{logs[i].Kind}] {logs[i].Text.Split('\n')[0]}");
            ScreenLog screen = Resolve<ScreenLog>();
            report.AppendLine($"screen log: {screen.Count} message(s)");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
        }
        return report.ToString();
    }

    // Closes the console and fills the screen log: keyed lines, a coloured print and a mirrored error.
    public static string ScreenDemo()
    {
        ConsoleService console = Resolve<ConsoleService>();
        console.Close();
        ScreenLog screen = Resolve<ScreenLog>();
        screen.Clear();
        screen.Show("speed", "Player speed: 4.2 m/s", 30f);
        screen.Show("speed", "Player speed: 5.7 m/s", 30f);
        screen.Print("Picked up AK-47", Color.cyan, 30f);
        screen.Print("Plain message", 30f);
        Debug.LogError("ConsoleReview: mirrored error");
        return $"screen log: {screen.Count} message(s) (the mirrored error lands next frame)";
    }

    // Up / Down through suggestions while there are some, through history on an empty line.
    public static async Task<string> Arrows()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>("AgentKeyboard");
        var report = new StringBuilder();
        try
        {
            ConsoleService console = Resolve<ConsoleService>();
            console.Close();
            console.Line.Clear();
            await Frames(2);
            await Press(keyboard, Key.Backquote);
            await Type(keyboard, "log.");
            report.AppendLine($"typed: '{console.Line.Text}' suggestions={string.Join(",", console.Suggestions)}");
            await Press(keyboard, Key.DownArrow);
            report.AppendLine($"down: '{console.Line.Text}' selected={console.SelectedSuggestion}");
            await Press(keyboard, Key.DownArrow);
            report.AppendLine($"down: '{console.Line.Text}' selected={console.SelectedSuggestion}");
            await Press(keyboard, Key.UpArrow);
            report.AppendLine($"up: '{console.Line.Text}' selected={console.SelectedSuggestion}");
            await Press(keyboard, Key.Enter);
            report.AppendLine($"enter → last: '{console.Logs[console.Logs.Count - 1].Text}'");
            await Press(keyboard, Key.UpArrow);
            report.AppendLine($"empty line, up: '{console.Line.Text}'");
            await Press(keyboard, Key.Escape);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
        }
        return report.ToString();
    }

    public static string Caret()
    {
        var view = Object.FindAnyObjectByType<ConsoleView>();
        var caret = view.transform.Find("Panel/InputBar/Input/Caret");
        if (caret == null)
            return "no caret object";
        var image = caret.GetComponent<UnityEngine.UI.Image>();
        var rect = (RectTransform)caret;
        var input = caret.parent.GetComponent<TMPro.TMP_Text>();
        return $"active={caret.gameObject.activeInHierarchy} enabled={image.enabled} color={image.color} local={rect.localPosition} size={rect.sizeDelta} " +
               $"inputRect={((RectTransform)input.transform).rect} chars={input.textInfo.characterCount} text='{input.GetParsedText()}'";
    }

    public static string State()
    {
        ConsoleService console = Resolve<ConsoleService>();
        var view = Object.FindAnyObjectByType<ConsoleView>();
        return $"open={console.IsOpen} line='{console.Line.Text}' logs={console.Logs.Count} view={(view != null ? view.name : "none")} " +
               $"timeScale={Time.timeScale} cursor={Cursor.lockState}";
    }

    // Closes the console and puts the time scale back.
    public static string Close()
    {
        ConsoleService console = Resolve<ConsoleService>();
        console.Run("time.scale 1");
        console.Close();
        return State();
    }

    static T Resolve<T>()
    {
        var root = Object.FindAnyObjectByType<ProjectLifetimeScope>();
        return root.Container.Resolve<T>();
    }

    static async Task Press(Keyboard keyboard, Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        await Frames(2);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await Frames(2);
    }

    static async Task Type(Keyboard keyboard, string text)
    {
        foreach (char c in text)
            InputSystem.QueueTextEvent(keyboard, c);
        await Frames(2);
    }

    static async Task Frames(int count)
    {
        int target = Time.frameCount + count;
        while (Time.frameCount < target)
            await Task.Yield();
    }
}
