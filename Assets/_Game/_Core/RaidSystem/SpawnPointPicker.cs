using System.Collections.Generic;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.PlayerSystem;
using UnityEngine;

namespace Aipocalypse.Core.RaidSystem
{
    // Where a raid group appears in a SpawnZone: sampled points on the ground and navmesh with a path to the zone's origin,
    // scored by SpawnPointScorer; waits up to MaxHiddenWait per zone for one the player can't see.
    public sealed class SpawnPointPicker
    {
        // Horizontal field of view assumed for the player's camera, widened by SpawnRules.ViewMargin on each side (no camera
        // lookup). The default 70° vertical FOV is about 102° across at 16:9, inside 90° + 2 × the default 10° margin.
        const float AssumedFieldOfView = 90f;
        // A sampled point looks for the ground from this high above the zone to as far below it (m).
        const float GroundProbe = 100f;

        readonly EnemyNavigation _navigation;
        readonly PlayerRegistry _players;
        readonly SpawnRules _rules;
        readonly List<SpawnCandidate> _candidates = new();
        readonly Dictionary<SpawnZone, float> _waitingSince = new();
        readonly HashSet<SpawnZone> _warned = new();

        public SpawnPointPicker(EnemyNavigation navigation, PlayerRegistry players, RaidSettings settings)
        {
            _navigation = navigation;
            _players = players;
            _rules = settings.Spawning;
        }

        // A spawn point in `zone`, or false while it waits for one the player can't see (ask again next frame).
        public bool TryPick(SpawnZone zone, out Vector3 point)
        {
            point = default;
            if (zone == null)
                return false;
            Sample(zone);
            float now = Time.time;
            if (!_waitingSince.TryGetValue(zone, out float since))
                _waitingSince[zone] = since = now;
            bool waited = now - since >= _rules.MaxHiddenWait;
            int chosen = SpawnPointScorer.Choose(_candidates, _rules, waited, Random.value);
            if (chosen < 0)
            {
                if (waited && _candidates.Count == 0 && _warned.Add(zone))
                    Debug.LogWarning($"SpawnPointPicker: no sampled point of {zone.name} is on walkable ground connected to it; check the zone, the ground mask and the navmesh.", zone);
                return false;
            }
            _waitingSince.Remove(zone);
            point = _candidates[chosen].Position;
            return true;
        }

        // Whether the player could see `enemy` now (dawn withdrawal): in the view cone at any distance, and the first
        // SightBlockers hit on the line to its chest, if any, is the enemy itself.
        public bool IsSeen(Transform enemy)
        {
            Transform view = _players.Current?.View;
            if (view == null || enemy == null)
                return false;
            Vector3 eye = view.position;
            Vector3 target = enemy.position + Vector3.up * _rules.TargetHeight;
            if (!InViewCone(view, target - eye, out _))
                return false;
            return !Physics.Linecast(eye, target, out RaycastHit hit, _rules.SightBlockers, QueryTriggerInteraction.Ignore)
                || hit.transform.IsChildOf(enemy);
        }

        // Forgets the zones' waits (a raid starts).
        public void ResetWaits() => _waitingSince.Clear();

        void Sample(SpawnZone zone)
        {
            _candidates.Clear();
            IPlayer player = _players.Current;
            Transform view = player?.View;
            Vector3 origin = zone.Origin;
            for (int i = 0; i < _rules.Candidates; i++)
            {
                Vector3 sample = zone.Sample(Random.value, Random.value);
                if (!Physics.Raycast(sample + Vector3.up * GroundProbe, Vector3.down, out RaycastHit ground, GroundProbe * 2f,
                        _rules.GroundMask, QueryTriggerInteraction.Ignore))
                    continue;
                if (!_navigation.TrySnap(ground.point, _rules.SnapDistance, out Vector3 snapped) || !_navigation.IsReachable(snapped, origin))
                    continue;
                if (view == null)
                {
                    _candidates.Add(new SpawnCandidate(snapped, float.PositiveInfinity, false, 180f));
                    continue;
                }
                bool seen = Sees(view, snapped, out float angle);
                _candidates.Add(new SpawnCandidate(snapped, Vector3.Distance(player.Transform.position, snapped), seen, angle));
            }
        }

        // In the assumed view cone (horizontal angle), within VisibleDistance, and nothing in SightBlockers between the eye and
        // an enemy's chest at `point`.
        bool Sees(Transform view, Vector3 point, out float angle)
        {
            Vector3 eye = view.position;
            Vector3 target = point + Vector3.up * _rules.TargetHeight;
            Vector3 toTarget = target - eye;
            if (!InViewCone(view, toTarget, out angle) || toTarget.sqrMagnitude > _rules.VisibleDistance * _rules.VisibleDistance)
                return false;
            return !Physics.Linecast(eye, target, _rules.SightBlockers, QueryTriggerInteraction.Ignore);
        }

        // Within the assumed horizontal field of view plus ViewMargin; `angle` is the horizontal angle from the view's centre.
        bool InViewCone(Transform view, Vector3 toTarget, out float angle)
        {
            Vector3 forward = view.forward;
            angle = Vector3.Angle(new Vector3(forward.x, 0f, forward.z), new Vector3(toTarget.x, 0f, toTarget.z));
            return angle <= AssumedFieldOfView * 0.5f + _rules.ViewMargin;
        }
    }
}
