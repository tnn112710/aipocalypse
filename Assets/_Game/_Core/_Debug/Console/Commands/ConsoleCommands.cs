using System;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.Debugging
{
    // Base of a system's console commands: its [Command] members exist while its scope does. Register a subclass as an entry
    // point in the system's installer, inside the Cheats.Enabled check. A subclass that holds owner-keyed requests
    // releases them in an override of Dispose.
    public abstract class ConsoleCommands : IInitializable, IDisposable
    {
        readonly CommandRegistry _registry;

        protected ConsoleCommands(CommandRegistry registry) => _registry = registry;

        public void Initialize() => _registry.Add(this);

        public virtual void Dispose() => _registry.Remove(this);
    }
}
