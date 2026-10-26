using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Aipocalypse.Core.Debugging
{
    public readonly struct CommandResult
    {
        public readonly bool Succeeded;
        public readonly string Message;

        public CommandResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public static CommandResult Ok(string message = null) => new(true, message);
        public static CommandResult Fail(string message) => new(false, message);
    }

    // Every console command that exists right now. ConsoleCommands classes add their [Command] members when their scope starts
    // and remove them when it ends, so a scene's commands exist only while that scene does. Runs a statement and suggests
    // completions. Names are matched ignoring case; several forms of one name are told apart by argument count, then by type.
    public sealed class CommandRegistry
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        readonly Dictionary<string, List<ConsoleCommand>> _commands = new(StringComparer.OrdinalIgnoreCase);
        readonly List<string> _names = new();
        readonly List<string> _tokens = new();
        readonly List<string> _choices = new();
        readonly List<string> _emptied = new();

        // Sorted.
        public IReadOnlyList<string> Names => _names;

        public void Add(object provider)
        {
            Type type = provider.GetType();
            string prefix = type.GetCustomAttribute<CommandGroupAttribute>()?.Prefix;
            foreach (MemberInfo member in type.GetMembers(Members))
            {
                var attribute = member.GetCustomAttribute<CommandAttribute>();
                if (attribute == null || member is not (MethodInfo or PropertyInfo))
                    continue;
                string name = attribute.Name ?? (string.IsNullOrEmpty(prefix) ? string.Empty : prefix + ".") + member.Name.ToLowerInvariant();
                if (member is MethodInfo { ContainsGenericParameters: true })
                {
                    Debug.LogWarning($"Console: {type.Name}.{member.Name} skipped, generic methods can't be commands");
                    continue;
                }
                var command = new ConsoleCommand(name, attribute.Description, provider, member);
                Type unsupported = command.UnsupportedType;
                if (unsupported != null)
                {
                    Debug.LogWarning($"Console: {type.Name}.{member.Name} skipped, no parser for {unsupported.Name}");
                    continue;
                }

                if (!_commands.TryGetValue(name, out List<ConsoleCommand> forms))
                {
                    forms = new List<ConsoleCommand>();
                    _commands.Add(name, forms);
                    int index = _names.BinarySearch(name, StringComparer.OrdinalIgnoreCase);
                    _names.Insert(index < 0 ? ~index : index, name);
                }
                else if (forms.Exists(f => f.MinArgs == command.MinArgs && f.MaxArgs == command.MaxArgs))
                {
                    Debug.LogWarning($"Console: '{name}' is defined twice with the same arguments; the first one runs");
                }
                forms.Add(command);
            }
        }

        public void Remove(object provider)
        {
            _emptied.Clear();
            foreach (KeyValuePair<string, List<ConsoleCommand>> pair in _commands)
            {
                pair.Value.RemoveAll(command => command.Target == provider);
                if (pair.Value.Count == 0)
                    _emptied.Add(pair.Key);
            }
            foreach (string name in _emptied)
            {
                _commands.Remove(name);
                _names.Remove(name);
            }
        }

        public bool TryGet(string name, out IReadOnlyList<ConsoleCommand> forms)
        {
            bool found = _commands.TryGetValue(name ?? string.Empty, out List<ConsoleCommand> list);
            forms = list;
            return found;
        }

        // Each form's usage and description on its own line, or null for an unknown name.
        public string Describe(string name)
        {
            if (!_commands.TryGetValue(name ?? string.Empty, out List<ConsoleCommand> forms))
                return null;
            var text = new StringBuilder();
            foreach (ConsoleCommand command in forms)
            {
                if (text.Length > 0)
                    text.Append('\n');
                text.Append(command.Usage);
                if (command.Description.Length > 0)
                    text.Append("  ").Append(command.Description);
            }
            return text.ToString();
        }

        // Runs one statement ("clock.set 21"). A command that throws is logged and reported as a failure, never rethrown.
        public CommandResult Execute(string statement)
        {
            CommandParser.Tokenize(statement, _tokens);
            if (_tokens.Count == 0)
                return CommandResult.Ok();
            string name = _tokens[0];
            if (!_commands.TryGetValue(name, out List<ConsoleCommand> forms))
                return CommandResult.Fail(Unknown(name));

            _tokens.RemoveAt(0);
            string error = null;
            foreach (ConsoleCommand command in forms)
            {
                if (!command.Accepts(_tokens.Count))
                    continue;
                if (command.TryBind(_tokens, out object[] values, out string bindError))
                    return Invoke(command, values);
                error ??= bindError;
            }
            error ??= _tokens.Count == 1 ? $"{name} doesn't take 1 argument" : $"{name} doesn't take {_tokens.Count} arguments";
            return CommandResult.Fail(error + "\n" + Describe(name));
        }

        // Completions for the token being typed at the end of the line; returns where that token starts in the line.
        // A command name completes to registered names (prefix matches first, then names containing it); an argument
        // completes to its enum names or on / off.
        public int Suggest(string line, List<string> suggestions)
        {
            suggestions.Clear();
            line ??= string.Empty;
            string statement = line.Substring(CommandParser.LastStatementStart(line));
            CommandParser.Tokenize(statement, _tokens);
            if (_tokens.Count == 0 || statement.IndexOf('"') >= 0)
                return line.Length;

            bool newToken = char.IsWhiteSpace(statement[^1]);
            string partial = newToken ? string.Empty : _tokens[^1];
            int tokenIndex = newToken ? _tokens.Count : _tokens.Count - 1;
            int start = line.Length - partial.Length;
            if (tokenIndex == 0)
            {
                SuggestNames(partial, suggestions);
                return start;
            }
            if (!_commands.TryGetValue(_tokens[0], out List<ConsoleCommand> forms))
                return start;
            foreach (ConsoleCommand command in forms)
            {
                Type type = command.ArgumentType(tokenIndex - 1);
                if (type == null)
                    continue;
                CommandParser.Choices(type, _choices);
                foreach (string choice in _choices)
                    if (choice.StartsWith(partial, StringComparison.OrdinalIgnoreCase) && !suggestions.Contains(choice))
                        suggestions.Add(choice);
            }
            return start;
        }

        void SuggestNames(string partial, List<string> suggestions)
        {
            if (partial.Length == 0)
                return;
            foreach (string name in _names)
                if (name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                    suggestions.Add(name);
            foreach (string name in _names)
                if (!name.StartsWith(partial, StringComparison.OrdinalIgnoreCase) && name.IndexOf(partial, StringComparison.OrdinalIgnoreCase) >= 0)
                    suggestions.Add(name);
        }

        static CommandResult Invoke(ConsoleCommand command, object[] values)
        {
            try
            {
                object result = command.Invoke(values);
                return result is CommandResult own ? own : CommandResult.Ok(result == null ? null : CommandParser.Format(result));
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                Debug.LogException(e.InnerException);
                return CommandResult.Fail($"{command.Name} failed: {e.InnerException.Message}");
            }
        }

        // "Unknown command" with up to three close names: same start, containing it, or a typo away.
        string Unknown(string name)
        {
            var close = new List<string>();
            foreach (string candidate in _names)
            {
                if (close.Count == 3)
                    break;
                if (candidate.StartsWith(name, StringComparison.OrdinalIgnoreCase) || candidate.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Distance(candidate, name) <= 2)
                    close.Add(candidate);
            }
            return close.Count == 0
                ? $"Unknown command '{name}' (help lists them all)"
                : $"Unknown command '{name}'. Did you mean {string.Join(", ", close)}?";
        }

        // Levenshtein distance, ignoring case.
        static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
                previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }
    }
}
