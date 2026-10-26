using System;
using System.Collections.Generic;
using System.Text;

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

        [Command("Unity logs shown on screen: Off, Error, Warning or All", Name = "log.screen")]
        ScreenLogLevel ScreenLevel
        {
            get => _screen.MirrorLevel;
            set => _screen.MirrorLevel = value;
        }

        [Command("Remove every message on screen", Name = "log.screenclear")]
        void ScreenClear() => _screen.Clear();

        [Command("Stack trace of the last error Unity logged", Name = "log.trace")]
        string Trace() => _logs.LastStackTrace ?? "No error logged yet";
    }
}
