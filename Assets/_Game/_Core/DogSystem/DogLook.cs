using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Where the dog turns its head (DogBody): the foe it fights, else the nearest foe within `range`, else the player's head
    // within `range` while it's calm (slower than `calmSpeed`). Nothing behind it (beyond `maxAngle` of its facing): it
    // would have to turn, which its legs do.
    public static class DogLook
    {
        const float FoeHeadHeight = 1.2f;

        public static bool Pick(Vector3 head, Vector3 forward, int targetId, IReadOnlyList<FoeView> foes, bool hasPlayer, Vector3 playerHead,
            float speed, out Vector3 point, float range, float calmSpeed, float maxAngle)
        {
            point = default;
            float nearest = range * range;
            bool found = false;
            for (int i = 0; i < foes.Count; i++)
            {
                FoeView foe = foes[i];
                if (!foe.Alive)
                    continue;
                Vector3 at = foe.Position + Vector3.up * FoeHeadHeight;
                if (!InView(head, forward, at, maxAngle))
                    continue;
                if (foe.Id == targetId)
                {
                    point = at;
                    return true;
                }
                float distance = (at - head).sqrMagnitude;
                if (distance >= nearest)
                    continue;
                nearest = distance;
                point = at;
                found = true;
            }
            if (found)
                return true;
            if (!hasPlayer || speed >= calmSpeed || (playerHead - head).sqrMagnitude > range * range || !InView(head, forward, playerHead, maxAngle))
                return false;
            point = playerHead;
            return true;
        }

        static bool InView(Vector3 head, Vector3 forward, Vector3 at, float maxAngle)
        {
            Vector3 to = at - head;
            to.y = 0f;
            forward.y = 0f;
            return to.sqrMagnitude > 0.01f && Vector3.Angle(forward, to) <= maxAngle;
        }
    }

    // Panting after a run: running at `runSpeed` or faster for `after` seconds (short dashes don't add up) sets it panting
    // for `pantFor` seconds after the run ends. Level 0..1 eases in and out.
    public sealed class DogPant
    {
        const float Ease = 0.6f;
        // A walk wears the built-up run off at this rate (s per s).
        const float CoolRate = 1f;

        float _ran;
        float _until = float.NegativeInfinity;

        public float Level { get; private set; }

        public void Tick(float speed, float now, float dt, float runSpeed, float after, float pantFor)
        {
            if (speed >= runSpeed)
            {
                _ran += dt;
                if (_ran >= after)
                    _until = now + pantFor;
            }
            else
                _ran = Mathf.Max(0f, _ran - CoolRate * dt);
            Level = Mathf.MoveTowards(Level, now < _until ? 1f : 0f, dt / Ease);
        }
    }
}
