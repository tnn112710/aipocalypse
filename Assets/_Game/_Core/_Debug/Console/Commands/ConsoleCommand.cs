using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Aipocalypse.Core.Debugging
{
    // One callable form of a command: a method (its parameters are the arguments) or a property (read with none, set with one).
    public sealed class ConsoleCommand
    {
        readonly MethodInfo _method;
        readonly PropertyInfo _property;
        readonly Type[] _types;
        readonly string[] _names;
        readonly object[] _defaults;

        public ConsoleCommand(string name, string description, object target, MemberInfo member)
        {
            Name = name;
            Description = description ?? string.Empty;
            Target = target;
            if (member is PropertyInfo property)
            {
                _property = property;
                _types = property.CanWrite ? new[] { property.PropertyType } : Type.EmptyTypes;
                _names = property.CanWrite ? new[] { "value" } : Array.Empty<string>();
                _defaults = new object[_types.Length];
                MinArgs = 0;
            }
            else
            {
                _method = (MethodInfo)member;
                ParameterInfo[] parameters = _method.GetParameters();
                _types = new Type[parameters.Length];
                _names = new string[parameters.Length];
                _defaults = new object[parameters.Length];
                MinArgs = parameters.Length;
                for (int i = 0; i < parameters.Length; i++)
                {
                    _types[i] = parameters[i].ParameterType;
                    _names[i] = parameters[i].Name;
                    _defaults[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                    if (parameters[i].HasDefaultValue && MinArgs == parameters.Length)
                        MinArgs = i;
                }
            }
            MaxArgs = _types.Length;
            Usage = BuildUsage();
        }

        public string Name { get; }
        public string Description { get; }
        public object Target { get; }
        public int MinArgs { get; }
        public int MaxArgs { get; }
        // "clock.set <hour:number>", optional arguments in brackets with their default.
        public string Usage { get; }

        public bool IsProperty => _property != null;

        // Null when an argument's type has no parser (the registry skips such a command).
        public Type UnsupportedType
        {
            get
            {
                foreach (Type type in _types)
                    if (!CommandParser.IsSupported(type))
                        return type;
                return null;
            }
        }

        public Type ArgumentType(int index) => index >= 0 && index < _types.Length ? _types[index] : null;

        public bool Accepts(int count) => count >= MinArgs && count <= MaxArgs;

        // Parses each argument to its type and fills the defaults of the ones left out.
        public bool TryBind(IReadOnlyList<string> args, out object[] values, out string error)
        {
            // A property reads with no value and sets with one; a method always takes all its parameters.
            values = new object[IsProperty ? Math.Min(args.Count, _types.Length) : _types.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (i >= args.Count)
                {
                    values[i] = _defaults[i];
                    continue;
                }
                if (!CommandParser.TryParse(args[i], _types[i], out values[i]))
                {
                    error = $"'{args[i]}' is not a valid {_names[i]} ({CommandParser.TypeName(_types[i])})";
                    return false;
                }
            }
            error = null;
            return true;
        }

        // The command's result to print: a method's return value, or a property's value after a set; null prints nothing.
        public object Invoke(object[] values)
        {
            if (_method != null)
                return _method.Invoke(Target, values);
            if (values.Length == 1)
                _property.SetValue(Target, values[0]);
            return _property.CanRead ? _property.GetValue(Target) : null;
        }

        string BuildUsage()
        {
            var usage = new StringBuilder(Name);
            for (int i = 0; i < _types.Length; i++)
            {
                bool optional = i >= MinArgs;
                usage.Append(optional ? " [" : " <").Append(_names[i]).Append(':').Append(CommandParser.TypeName(_types[i]));
                if (optional && _defaults[i] != null)
                    usage.Append('=').Append(CommandParser.Format(_defaults[i]));
                usage.Append(optional ? ']' : '>');
            }
            return usage.ToString();
        }
    }
}
