using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Aipocalypse.Core.Debugging
{
    // Commands of the console itself.
    public sealed class BuiltInCommands : ConsoleCommands
    {
        readonly CommandRegistry _registry;
        readonly LogBuffer _logs;
        readonly ScreenLog _screen;

        public BuiltInCommands(CommandRegistry registry, LogBuffer logs, ScreenLog screen) : base(registry)
        {
            _registry = registry;
            _logs = logs;
            _screen = screen;
        }

        [Command("Every command, how to use one, or a group's commands (help clock)", Name = "help")]
        CommandResult Help(string command = null)
        {
            if (command != null && _registry.Describe(command) is { } usage)
                return CommandResult.Ok(usage);

            var names = new List<string>();
            foreach (string name in _registry.Names)
                if (command == null || name.StartsWith(command, StringComparison.OrdinalIgnoreCase))
                    names.Add(name);
            if (names.Count == 0)
                return CommandResult.Fail($"No command starts with '{command}' here");

            int width = 0;
            foreach (string name in names)
                width = Math.Max(width, name.Length);
            var text = new StringBuilder();
            foreach (string name in names)
            {
                _registry.TryGet(name, out IReadOnlyList<ConsoleCommand> forms);
                if (text.Length > 0)
                    text.Append('\n');
                text.Append(name).Append(' ', width - name.Length + 2).Append(forms[0].Description);
            }
            return CommandResult.Ok(text.ToString());
        }

        [Command("Empty the console", Name = "clear")]
        void Clear() => _logs.Clear();

        [Command("Which Unity logs show (Off, Error, Info, All)", Name = "log.screen")]
        ScreenLogLevel ScreenLevel() => _screen.MirrorLevel;

        [Command("Show every Unity log: logs, warnings, errors", Name = "log.screen.all")]
        string ShowAll() => ShowLogs(ScreenLogLevel.All);

        [Command("Show Unity errors only", Name = "log.screen.error")]
        string ShowErrors() => ShowLogs(ScreenLogLevel.Error);

        [Command("Show Debug.Log messages only", Name = "log.screen.info")]
        string ShowInfo() => ShowLogs(ScreenLogLevel.Info);

        [Command("Stop showing Unity logs and remove the ones on screen", Name = "log.screen.clear")]
        string StopLogs()
        {
            _screen.Clear();
            return ShowLogs(ScreenLogLevel.Off);
        }

        // From now on, in the scrollback and on screen.
        string ShowLogs(ScreenLogLevel level)
        {
            _screen.MirrorLevel = level;
            return level == ScreenLogLevel.Off ? "Unity logs hidden" : $"Showing Unity logs: {level}";
        }

        [Command("Stack trace of the last error Unity logged", Name = "log.trace")]
        string Trace() => _logs.LastStackTrace ?? "No error logged yet";

        [Command("Whether the game keeps running while its window isn't focused", Name = "app.background")]
        string Background() => CommandParser.Format(Application.runInBackground);

        [Command("Keep running while the window isn't focused (unattended runs, profiling a build)", Name = "app.background")]
        string Background(bool on)
        {
            Application.runInBackground = on;
            return Background();
        }
    }
}
