using System;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // How a low-sanity HUD lies, as pure functions of the sanity share and time (a hash, never UnityEngine.Random, so a view
    // can ask every frame and get the same answer). Each starts at its `from` threshold and grows to full at 0. The clock and
    // the minimap may lie; the bars and gauges only flicker, their values stay true.
    public static class SanityGlitch
    {
        const float ClockSlot = 8f;
        const float ClockLength = 0.6f;
        const float CompassSlot = 10f;
        const float CompassLength = 1f;
        const float FlickerStep = 0.08f;
        const float FlickerFloor = 0.35f;
        const int MaxBlips = 3;

        // A wrong time for at most 0.6 s in each 8 s slot (more slots lie the lower sanity is), never two windows back to back.
        public static bool ClockLies(float sanity, float time, float from, out int hour, out int minute)
        {
            hour = minute = 0;
            float depth = Depth(sanity, from);
            if (depth <= 0f)
                return false;
            int slot = Mathf.FloorToInt(time / ClockSlot);
            if (Hash(slot, 1) > 0.3f + 0.6f * depth)
                return false;
            float start = ClockLength + Hash(slot, 2) * (ClockSlot - 3f * ClockLength);
            float local = time - slot * ClockSlot;
            if (local < start || local >= start + ClockLength)
                return false;
            hour = Mathf.Min(23, (int)(Hash(slot, 3) * 24f));
            minute = Mathf.Min(59, (int)(Hash(slot, 4) * 60f));
            return true;
        }

        // Fake enemy dots inside the unit disc (scale by the radar's radius): up to 3, more the lower sanity is; each lives
        // 2–4 s at one spot, then moves or hides. Returns how many were written.
        public static int FakeBlips(float sanity, float time, float from, Span<Vector2> blips)
        {
            float depth = Depth(sanity, from);
            if (depth <= 0f)
                return 0;
            int wanted = Mathf.Min(Mathf.Min(MaxBlips, blips.Length), Mathf.CeilToInt(depth * MaxBlips));
            int count = 0;
            for (int i = 0; i < wanted; i++)
            {
                float life = 2f + 2f * Hash(i, 11);
                int slot = Mathf.FloorToInt((time + life * Hash(i, 12)) / life);
                // The first is always there; the others show in most slots.
                if (i > 0 && Hash(slot, 13 + i) > 0.7f)
                    continue;
                float angle = Hash(slot, 20 + i) * Mathf.PI * 2f;
                float radius = Mathf.Sqrt(Hash(slot, 30 + i)) * 0.9f;
                blips[count++] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return count;
        }

        // The compass letters swap (N for S, E for W) for at most a second in each 10 s slot.
        public static bool CompassSwapped(float sanity, float time, float from)
        {
            float depth = Depth(sanity, from);
            if (depth <= 0f)
                return false;
            int slot = Mathf.FloorToInt(time / CompassSlot);
            if (Hash(slot, 5) > 0.3f + 0.5f * depth)
                return false;
            float start = Hash(slot, 6) * (CompassSlot - CompassLength);
            float local = time - slot * CompassSlot;
            return local >= start && local < start + CompassLength;
        }

        // An alpha multiplier for the bars and gauges: 1 above `from`, short dips below it, never under 0.35.
        public static float Flicker(float sanity, float time, float from)
        {
            float depth = Depth(sanity, from);
            if (depth <= 0f)
                return 1f;
            int step = Mathf.FloorToInt(time / FlickerStep);
            if (Hash(step, 7) > 0.05f + 0.2f * depth)
                return 1f;
            return Mathf.Lerp(1f, FlickerFloor, depth * Hash(step, 8));
        }

        // The player's sanity share and its effect settings, when the player has sanity (else false: no glitch).
        public static bool TryRead(IPlayer player, out float sanity, out SanityEffectSettings effects)
        {
            PlayerNeeds needs = player?.Needs;
            effects = needs?.Settings.SanityEffects;
            sanity = needs != null ? needs.Sanity.Normalized : 1f;
            return effects != null && needs.Sanity.Settings.Enabled && !player.IsDead;
        }

        // 0 at the threshold, 1 at empty.
        static float Depth(float sanity, float from) => from <= 0f ? 0f : Mathf.Clamp01((from - sanity) / from);

        // 0..1 from two integers.
        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u + 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
