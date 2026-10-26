using System;

namespace Aipocalypse.Core.Debugging
{
    // A console command on a ConsoleCommands class. Its name is the class's group + the member name in lower case ("clock.set");
    // Name replaces the whole name ("help"). A property reads with no argument and sets with one.
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class CommandAttribute : Attribute
    {
        public CommandAttribute(string description = null) => Description = description;

        public string Description { get; }
        public string Name { get; set; }
    }

    // Prefix of every command on a ConsoleCommands class: [CommandGroup("clock")] → "clock.set".
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CommandGroupAttribute : Attribute
    {
        public CommandGroupAttribute(string prefix) => Prefix = prefix;

        public string Prefix { get; }
    }
}
