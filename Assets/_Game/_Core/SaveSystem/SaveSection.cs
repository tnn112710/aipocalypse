using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // One system's part of a save: it writes its own state under its own key and puts it back on load. A system that's off
    // (or newer than the save) simply finds its key missing and keeps its new-game state. Register a subclass as an entry
    // point in the system's installer; it adds itself to the SaveRegistry while its scope lives.
    public abstract class SaveSection : IInitializable, IDisposable
    {
        readonly SaveRegistry _registry;

        protected SaveSection(SaveRegistry registry) => _registry = registry;

        // Lower restores first (SaveOrder).
        public abstract int Order { get; }

        public void Initialize() => _registry.Add(this);

        public virtual void Dispose() => _registry.Remove(this);

        // Writes this system's state now. Main thread, game state as it is this frame.
        public abstract void Capture(SaveData save);

        // Puts a saved state back, on the first frame of a freshly loaded scene (after every Start).
        public abstract void Restore(SaveData save);
    }

    // Restore order: the clock first (others react to the time), the player last (it may stand in what was restored).
    public static class SaveOrder
    {
        public const int Random = 0;
        public const int Clock = 10;
        public const int Weather = 20;
        public const int Raids = 30;
        public const int World = 40;
        public const int WorldItems = 50;
        public const int Stats = 90;
        public const int Player = 100;
    }

    // Root: the sections alive now (root ones always, scene ones while their scene is loaded).
    public sealed class SaveRegistry
    {
        readonly List<SaveSection> _sections = new();

        public IReadOnlyList<SaveSection> Sections => _sections;

        public void Add(SaveSection section)
        {
            if (section == null || _sections.Contains(section))
                return;
            int index = 0;
            while (index < _sections.Count && _sections[index].Order <= section.Order)
                index++;
            _sections.Insert(index, section);
        }

        public void Remove(SaveSection section) => _sections.Remove(section);

        // A section that fails is reported and skipped; its entry from the save being carried over (if any) stays.
        public int Capture(SaveData save)
        {
            int failed = 0;
            foreach (SaveSection section in _sections)
            {
                try
                {
                    section.Capture(save);
                }
                catch (Exception exception)
                {
                    failed++;
                    Debug.LogError($"Saving {section.GetType().Name} failed; its last saved state is kept.");
                    Debug.LogException(exception);
                }
            }
            return failed;
        }

        // A section that fails is reported and skipped, so one broken system doesn't lose the rest of the save.
        public int Restore(SaveData save)
        {
            int failed = 0;
            foreach (SaveSection section in _sections)
            {
                try
                {
                    section.Restore(save);
                }
                catch (Exception exception)
                {
                    failed++;
                    Debug.LogError($"Loading {section.GetType().Name} failed; it keeps its new-game state.");
                    Debug.LogException(exception);
                }
            }
            return failed;
        }
    }
}
