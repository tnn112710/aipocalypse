using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Auto-collected world items (Tokens) within the pickup radius fly to the player and are picked up. An item the player
    // threw waits until the player has been out of its reach once, so dropping tokens doesn't pull them straight back.
    public sealed class ItemMagnet
    {
        // Metres from the pull point at which a flying item is picked up.
        const float CatchDistance = 0.35f;
        // Metres past the radius the player must be from a thrown item before the magnet takes it again.
        const float LeaveMargin = 0.5f;
        // Seconds between two looks for items in reach; flying items still move every frame.
        const float ScanInterval = 0.1f;

        readonly Transform _body;
        readonly GameObject _actor;
        readonly Equipment _equipment;
        readonly PlayerInteractionTuning _tuning;
        readonly Collider[] _hits = new Collider[64];
        readonly List<WorldItem> _flying = new();
        readonly List<WorldItem> _waiting = new();
        float _sinceScan = ScanInterval;

        public ItemMagnet(Transform body, Equipment equipment, PlayerInteractionTuning tuning)
        {
            _body = body;
            _actor = body.gameObject;
            _equipment = equipment;
            _tuning = tuning;
        }

        public int Flying => _flying.Count;

        // A thrown item: left alone until the player has walked away from it.
        public void Ignore(WorldItem item)
        {
            if (item == null)
                return;
            item.WaitsForPlayerToLeave = true;
            if (!_waiting.Contains(item))
                _waiting.Add(item);
        }

        // `pullTo`: where items fly (the player's chest).
        public void Tick(float deltaTime, Vector3 pullTo)
        {
            float radius = _tuning.MagnetRadius;
            _sinceScan += deltaTime;
            if (_sinceScan >= ScanInterval)
            {
                _sinceScan = 0f;
                ReleaseLeftBehind(radius);
                if (radius > 0f)
                    FindNearby(radius);
            }
            Fly(deltaTime, pullTo);
        }

        void ReleaseLeftBehind(float radius)
        {
            float leave = radius + LeaveMargin;
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                WorldItem item = _waiting[i];
                // Judged once it has landed: it leaves the hand at eye height, already past the radius.
                if (item != null && !item.IsTaken && (item.IsFlying || (item.transform.position - _body.position).sqrMagnitude <= leave * leave))
                    continue;
                if (item != null)
                    item.WaitsForPlayerToLeave = false;
                _waiting.RemoveAt(i);
            }
        }

        void FindNearby(float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(_body.position, radius, _hits, _tuning.PickupMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                // Pickup boxes are triggers on the item's root: skip the ground, walls and props without a lookup.
                if (!_hits[i].isTrigger || !_hits[i].TryGetComponent(out WorldItem item))
                    continue;
                if (item.IsTaken || item.WaitsForPlayerToLeave || item.Item == null || !item.Item.Definition.AutoCollect
                    || _flying.Contains(item) || _equipment.Inventory.SpaceFor(item.Item) <= 0)
                    continue;
                item.Settle();
                _flying.Add(item);
            }
        }

        void Fly(float deltaTime, Vector3 pullTo)
        {
            float step = _tuning.MagnetSpeed * deltaTime;
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                WorldItem item = _flying[i];
                if (item == null || item.IsTaken)
                {
                    _flying.RemoveAt(i);
                    continue;
                }
                Transform t = item.transform;
                t.position = Vector3.MoveTowards(t.position, pullTo, step);
                if ((t.position - pullTo).sqrMagnitude > CatchDistance * CatchDistance)
                    continue;
                _flying.RemoveAt(i);
                // What didn't fit waits until the player leaves and comes back (no warning every frame).
                if (!item.Collect(_actor))
                    Ignore(item);
            }
        }
    }
}
