using System;
using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Searching a crate or body, after Duckov: its key press opens it at once (Opened: the inventory screen shows it), then
    // what's inside is revealed one stack every SearchSecondsPerItem while it stays open. The screen ticks it (real time:
    // it may pause the game) and stops it on close; reopening carries on with what's still hidden.
    public sealed class PlayerLooting
    {
        readonly PlayerInteractionTuning _tuning;
        float _elapsed;

        // Show this container (the inventory screen opens with it).
        public event Action<LootContainer> Opened;
        // A stack in this slot of the searched container was revealed.
        public event Action<LootContainer, int> Revealed;

        public PlayerLooting(PlayerInteractionTuning tuning) => _tuning = tuning;

        // The open container while it still hides something.
        public LootContainer Searching { get; private set; }
        public bool IsSearching => Searching != null;
        public float SecondsPerItem => _tuning.SearchSecondsPerItem;
        // The slot being revealed now, or -1.
        public int RevealingSlot => Searching != null ? Searching.NextHidden : -1;
        // 0..1 through revealing that slot.
        public float Progress => SecondsPerItem > 0f ? Mathf.Clamp01(_elapsed / SecondsPerItem) : 1f;

        // Seconds until everything in `container` is revealed.
        public float SearchTime(LootContainer container) => container == null ? 0f : SecondsPerItem * container.HiddenCount;

        public void Begin(LootContainer container)
        {
            if (container == null)
                return;
            Stop();
            if (SecondsPerItem <= 0f)
                container.RevealAll();
            if (!container.Searched)
                Searching = container;
            Opened?.Invoke(container);
        }

        public void Tick(float deltaTime)
        {
            if (Searching == null)
                return;
            if (!Searching.Searchable)
            {
                Stop();
                return;
            }
            _elapsed += deltaTime;
            while (Searching != null && _elapsed >= SecondsPerItem)
            {
                _elapsed -= SecondsPerItem;
                LootContainer container = Searching;
                int slot = container.RevealNext();
                if (container.Searched)
                    Stop();
                if (slot >= 0)
                    Revealed?.Invoke(container, slot);
            }
        }

        // The container was closed: the stack being revealed starts over next time.
        public void Stop()
        {
            Searching = null;
            _elapsed = 0f;
        }
    }
}
