using Aipocalypse.Core.PlayerSystem;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // What the player can see, for spawning out of view and despawning once out of it: a view cone (no camera lookup) plus a
    // line of sight. Points it finds seen stay off-limits for SpawnRules.SeenLock seconds, so a spot the player just looked
    // away from doesn't fill up behind their back.
    public sealed class SpawnVisibility
    {
        // Horizontal field of view assumed for the player's camera, widened by SpawnRules.ViewMargin on each side. The default
        // 70° vertical FOV is about 102° across at 16:9, inside 90° + 2 × the default 10° margin.
        const float AssumedFieldOfView = 90f;
        const int Remembered = 32;

        readonly PlayerRegistry _players;
        readonly Vector3[] _seenPoints = new Vector3[Remembered];
        readonly float[] _seenAt = new float[Remembered];
        int _next;

        public SpawnVisibility(PlayerRegistry players)
        {
            _players = players;
            for (int i = 0; i < Remembered; i++)
                _seenAt[i] = float.NegativeInfinity;
        }

        // The player's eye, or null without a player.
        public Transform View => _players.Current?.View;

        // Metres from the player's body to `point` (infinity without a player).
        public float PlayerDistance(Vector3 point)
        {
            IPlayer player = _players.Current;
            return player != null ? Vector3.Distance(player.Transform.position, point) : float.PositiveInfinity;
        }

        // Whether the player sees something standing at `point` (its chest at TargetHeight), or saw near it within SeenLock
        // seconds; `angle` is the horizontal angle from the view's centre.
        public bool Sees(Vector3 point, SpawnRules rules, out float angle)
        {
            angle = 180f;
            Transform view = View;
            if (view == null)
                return false;
            Vector3 eye = view.position;
            Vector3 target = point + Vector3.up * rules.TargetHeight;
            Vector3 toTarget = target - eye;
            if (!InViewCone(view, toTarget, rules.ViewMargin, out angle) || toTarget.sqrMagnitude > rules.VisibleDistance * rules.VisibleDistance)
                return RecentlySeen(point, rules);
            if (Physics.Linecast(eye, target, rules.SightBlockers, QueryTriggerInteraction.Ignore))
                return RecentlySeen(point, rules);
            Remember(point);
            return true;
        }

        // A group's whole footprint: seen if its centre or any of four points `radius` out round it is.
        public bool SeesFootprint(Vector3 center, float radius, SpawnRules rules, out float angle)
        {
            bool seen = Sees(center, rules, out angle);
            if (seen || radius <= 0.01f)
                return seen;
            for (int i = 0; i < 4; i++)
            {
                float turn = i * 0.5f * Mathf.PI;
                if (Sees(center + new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * radius, rules, out float edge))
                {
                    angle = edge;
                    return true;
                }
            }
            return false;
        }

        // Whether the player could see `target` now (despawning out of sight): in the view cone at any distance, and the first
        // SightBlockers hit on the line to its chest, if any, is the target itself.
        public bool IsSeen(Transform target, SpawnRules rules)
        {
            Transform view = View;
            if (view == null || target == null)
                return false;
            Vector3 eye = view.position;
            Vector3 chest = target.position + Vector3.up * rules.TargetHeight;
            if (!InViewCone(view, chest - eye, rules.ViewMargin, out _))
                return false;
            return !Physics.Linecast(eye, chest, out RaycastHit hit, rules.SightBlockers, QueryTriggerInteraction.Ignore)
                || hit.transform.IsChildOf(target);
        }

        bool RecentlySeen(Vector3 point, SpawnRules rules)
        {
            if (rules.SeenLock <= 0f)
                return false;
            float since = Time.time - rules.SeenLock;
            float radius = rules.SeenLockRadius * rules.SeenLockRadius;
            for (int i = 0; i < Remembered; i++)
            {
                if (_seenAt[i] < since)
                    continue;
                Vector3 away = _seenPoints[i] - point;
                away.y = 0f;
                if (away.sqrMagnitude <= radius)
                    return true;
            }
            return false;
        }

        void Remember(Vector3 point)
        {
            _seenPoints[_next] = point;
            _seenAt[_next] = Time.time;
            _next = (_next + 1) % Remembered;
        }

        static bool InViewCone(Transform view, Vector3 toTarget, float margin, out float angle)
        {
            Vector3 forward = view.forward;
            angle = Vector3.Angle(new Vector3(forward.x, 0f, forward.z), new Vector3(toTarget.x, 0f, toTarget.z));
            return angle <= AssumedFieldOfView * 0.5f + margin;
        }
    }
}
