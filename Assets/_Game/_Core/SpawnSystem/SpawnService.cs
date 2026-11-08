using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // The one way code brings things into play: enemies, animals, flocks, items, props. A request finds its spot (in an area,
    // out of the player's view when it can, or at a point), lays its group out and queues the members; the tick wakes them from
    // the PrefabPool within SpawnBudgetMs a frame. Despawning hands them back, at once or once the player can't see them.
    public sealed class SpawnService : ITickable, ISpawnCounts
    {
        static readonly ProfilerMarker SpawnMarker = new("Aipocalypse.SpawnService.Spawn");
        static readonly ProfilerMarker PlaceMarker = new("Aipocalypse.SpawnService.Place");
        // Withdrawn instances checked for being out of sight per frame.
        const int LeavingChecksPerFrame = 8;
        // Oldest instances of a full card checked for being out of sight before one is recycled.
        const int RecycleChecks = 8;

        readonly PrefabPool _pool;
        readonly SpawnPointPicker _picker;
        readonly SpawnPlacer _placer;
        readonly SpawnVisibility _visibility;
        readonly SpawnSystemSettings _settings;
        readonly List<SpawnHandle> _placing = new();
        readonly Queue<QueuedMember> _queue = new();
        readonly List<Spawnable> _leaving = new();
        readonly Dictionary<SpawnCard, int> _aliveByCard = new();
        // Alive instances of cards with a MaxAlive, oldest first.
        readonly Dictionary<SpawnCard, List<Spawnable>> _capped = new();
        readonly int[] _aliveByCategory = new int[System.Enum.GetValues(typeof(SpawnCategory)).Length];
        readonly List<Vector3> _slots = new();
        readonly Stopwatch _clock = new();
        int _leavingCursor;
        int _alive;

        public SpawnService(PrefabPool pool, SpawnPointPicker picker, SpawnPlacer placer, SpawnVisibility visibility,
            SpawnSystemSettings settings)
        {
            _pool = pool;
            _picker = picker;
            _placer = placer;
            _visibility = visibility;
            _settings = settings;
        }

        // Members queued (cancelled ones included until their turn) and requests still looking for a spot.
        public int Queued => _queue.Count;
        public int Placing => _placing.Count;
        public int Leaving => _leaving.Count;
        // Everything spawned and alive.
        public int AliveTotal => _alive;
        public PrefabPool Pool => _pool;

        public int CountAlive(SpawnCard card) => card != null && _aliveByCard.TryGetValue(card, out int alive) ? alive : 0;
        public int CountAlive(SpawnCategory category) => _aliveByCategory[(int)category];

        // One of `card` exactly at `position` (already where it can stand), now; null if the card has no variant or is full
        // (SpawnCard.MaxAlive with WhenFull Refuse).
        public Spawnable Spawn(SpawnCard card, Vector3 position, Quaternion rotation)
        {
            Spawnable prefab = PickPooled(card, Random.value);
            if (prefab != null)
                return Activate(prefab, card, position, rotation, new SpawnInfo(card, null, 0, 1, null));
            Debug.LogWarning($"SpawnService: {(card != null ? card.name : "no card")} has no prefab variant to spawn.", card);
            return null;
        }

        // A prefab without a card (a one-off prop or pickup) exactly at `position`, now.
        public Spawnable Spawn(Spawnable prefab, Vector3 position, Quaternion rotation) =>
            prefab != null ? Activate(prefab, null, position, rotation, new SpawnInfo(null, null, 0, 1, null)) : null;

        // Starts `request`: its members arrive over the next frames. The handle says how many and lets them go again.
        public SpawnHandle Spawn(in SpawnRequest request)
        {
            SpawnCard card = request.Squad != null ? request.Squad.Lead
                : request.Table != null ? request.Table.Pick(request.Conditions, this, Random.value)
                : request.Card;
            int count = card == null ? 0
                : request.Squad != null ? request.Squad.Count
                : request.Count > 0 ? request.Count
                : card.PickGroupSize(Random.value);
            var handle = new SpawnHandle(this, request, card, count);
            if (count <= 0)
            {
                handle.Cancel();
                return handle;
            }
            _placing.Add(handle);
            // Placed now when it can be, so a group's first members can arrive this frame.
            TryPlace(handle);
            return handle;
        }

        // Back to its pool now, or withdrawn until the player can't see it (DespawnMode.WhenUnseen). Objects placed in the scene
        // just turn off.
        public void Despawn(Spawnable instance, DespawnMode mode = DespawnMode.Now)
        {
            if (instance == null || !instance.gameObject.activeSelf)
                return;
            if (mode == DespawnMode.WhenUnseen && instance.State == SpawnState.Active)
            {
                instance.Leave();
                _leaving.Add(instance);
                return;
            }
            if (mode == DespawnMode.WhenUnseen && instance.State == SpawnState.Leaving)
                return;
            instance.gameObject.SetActive(false);
        }

        // Creates enough instances of the card's variants, by weight, for `count` in play at once. An `owner` keeps them pooled
        // until it calls Release; without one, copies unused for IdleTrimDelay are destroyed again.
        public void Prewarm(SpawnCard card, int count, object owner = null)
        {
            if (card == null || count <= 0)
                return;
            float total = 0f;
            foreach ((Spawnable prefab, float weight) in card.Variants)
                if (prefab != null && weight > 0f)
                    total += weight;
            if (total <= 0f)
                return;
            foreach ((Spawnable prefab, float weight) in card.Variants)
                if (prefab != null && weight > 0f)
                    _pool.Prewarm(prefab, Mathf.CeilToInt(count * weight / total), owner);
        }

        public void Prewarm(Spawnable prefab, int count, object owner = null) => _pool.Prewarm(prefab, count, owner);

        // Lets go of what `owner` prewarmed: copies nobody else keeps are destroyed over the next frames.
        public void Release(object owner) => _pool.Release(owner);

        // Drops every request still looking for a spot and every queued member (their handles end cancelled).
        public void CancelQueued()
        {
            foreach (SpawnHandle handle in _placing)
                handle.Cancel();
            _placing.Clear();
            foreach (QueuedMember member in _queue)
                member.Handle.Cancel();
            _queue.Clear();
        }

        public void Tick() => Advance(Time.deltaTime);

        // Places what it can, wakes queued members within the budget and sends withdrawn ones off once unseen; none while paused.
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            PlacePending();
            WakeQueued();
            SendOffLeaving();
        }

        // From Spawnable when it stops counting as alive.
        internal void Ended(Spawnable instance)
        {
            _alive = Mathf.Max(0, _alive - 1);
            SpawnCard card = instance.Card;
            if (card == null)
                return;
            _aliveByCategory[(int)card.Category] = Mathf.Max(0, _aliveByCategory[(int)card.Category] - 1);
            if (_aliveByCard.TryGetValue(card, out int alive))
                _aliveByCard[card] = Mathf.Max(0, alive - 1);
            if (_capped.TryGetValue(card, out List<Spawnable> capped))
                capped.Remove(instance);
        }

        void PlacePending()
        {
            if (_placing.Count == 0)
                return;
            using var _ = PlaceMarker.Auto();
            for (int i = _placing.Count - 1; i >= 0; i--)
            {
                SpawnHandle handle = _placing[i];
                if (handle.Cancelled || (Time.time >= handle.NextTry && TryPlace(handle)))
                    _placing.RemoveAt(i);
            }
        }

        // Finds the handle's spot and queues its members; false while it waits (it's then retried every RetryInterval).
        bool TryPlace(SpawnHandle handle)
        {
            if (handle.IsPlaced || handle.Cancelled)
                return true;
            SpawnRequest request = handle.Request;
            SpawnRules rules = request.Rules ?? _settings.DefaultRules;
            SpawnCard lead = handle.Card;
            Formation formation = request.Formation != null ? request.Formation : request.Squad != null ? request.Squad.Formation : null;
            Layout(formation, handle.Count);
            float footprint = 0f;
            foreach (Vector3 slot in _slots)
                footprint = Mathf.Max(footprint, new Vector2(slot.x, slot.z).magnitude);

            Vector3 anchor;
            if (request.Area != null)
            {
                bool waited = Time.time - handle.Since >= rules.MaxHiddenWait;
                if (!_picker.TryPick(request.Area, lead.Placement, rules, footprint, waited, out anchor))
                {
                    handle.NextTry = Time.time + _settings.RetryInterval;
                    if (waited && _picker.LastCandidates == 0 && !handle.Warned)
                    {
                        handle.Warned = true;
                        Debug.LogWarning($"SpawnService: no point of {DescribeArea(request.Area)} fits {lead.name} " +
                            $"({lead.Placement.Medium}); still trying. Check the area, the masks, the navmesh or the water bodies.");
                    }
                    return false;
                }
            }
            else if (!_placer.TryPlace(request.Position, lead.Placement, rules, false, default, Random.value, out anchor, out _))
            {
                Debug.LogWarning($"SpawnService: {lead.name} can't stand at {request.Position} ({lead.Placement.Medium}).");
                handle.Cancel();
                return true;
            }

            Vector3 facing = request.Direction;
            if (facing.sqrMagnitude < 0.0001f && request.Area != null)
                facing = request.Area.Origin - anchor;
            facing.y = 0f;
            Quaternion turn = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            handle.Placed(anchor);
            bool hasOrigin = request.Area != null;
            Vector3 origin = hasOrigin ? request.Area.Origin : default;
            for (int i = 0; i < _slots.Count; i++)
            {
                SpawnCard card = request.Squad != null ? request.Squad.CardAt(i) : lead;
                if (card == null)
                {
                    handle.Dropped();
                    continue;
                }
                Vector3 point = anchor;
                Vector3 up = Vector3.up;
                if (i > 0 || _slots[i].sqrMagnitude > 0.0001f)
                {
                    Vector3 sample = anchor + turn * _slots[i];
                    if (!_placer.TryPlace(sample, card.Placement, rules, hasOrigin, origin, Random.value, out point, out up))
                    {
                        if (!card.Placement.FallBackToAnchor)
                        {
                            handle.Dropped();
                            continue;
                        }
                        point = anchor;
                        up = Vector3.up;
                    }
                }
                Quaternion rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(turn * Vector3.forward, up), up);
                _queue.Enqueue(new QueuedMember(handle, card, point, rotation, i));
            }
            return true;
        }

        // The group's slot offsets into _slots: the formation's, or a scatter.
        void Layout(Formation formation, int count)
        {
            if (formation != null)
            {
                formation.GetSlots(count, _slots);
                return;
            }
            _slots.Clear();
            float radius = Random.Range(_settings.GroupRadius.x, Mathf.Max(_settings.GroupRadius.x, _settings.GroupRadius.y));
            float turn = Random.value * 2f * Mathf.PI;
            for (int i = 0; i < count; i++)
                _slots.Add(FormationMath.Scatter(i, count, radius, _settings.GroupSpacing, turn));
        }

        void WakeQueued()
        {
            if (_queue.Count == 0)
                return;
            using var _ = SpawnMarker.Auto();
            _clock.Restart();
            float budget = _settings.SpawnBudgetMs;
            int woken = 0;
            while (_queue.Count > 0 && (woken == 0 || _clock.Elapsed.TotalMilliseconds < budget))
            {
                QueuedMember member = _queue.Dequeue();
                if (member.Handle.Cancelled)
                    continue;
                Spawnable prefab = PickPooled(member.Card, Random.value);
                if (prefab == null)
                {
                    member.Handle.Dropped();
                    continue;
                }
                var info = new SpawnInfo(member.Card, member.Handle, member.Slot, member.Handle.Count,
                    member.Slot > 0 ? member.Handle.Leader : null);
                Spawnable instance = Activate(prefab, member.Card, member.Position, member.Rotation, info);
                if (instance == null)
                {
                    member.Handle.Dropped();
                    continue;
                }
                if (member.Slot == 0)
                    member.Handle.Leader = instance;
                member.Handle.Arrived(instance);
                woken++;
            }
            _clock.Stop();
        }

        // Null when the card is full and refuses more.
        Spawnable Activate(Spawnable prefab, SpawnCard card, Vector3 position, Quaternion rotation, in SpawnInfo info)
        {
            if (card != null && card.MaxAlive > 0 && CountAlive(card) >= card.MaxAlive && !MakeRoom(card))
                return null;
            Spawnable instance = _pool.Get(prefab, position, rotation);
            _alive++;
            if (card != null)
            {
                _aliveByCategory[(int)card.Category]++;
                _aliveByCard[card] = CountAlive(card) + 1;
                if (card.MaxAlive > 0)
                {
                    if (!_capped.TryGetValue(card, out List<Spawnable> capped))
                        _capped[card] = capped = new List<Spawnable>();
                    capped.Add(instance);
                }
            }
            instance.Spawned(this, info);
            return instance;
        }

        // A full card's oldest instance the player can't see (of the RecycleChecks oldest; else the oldest) goes back to the pool.
        bool MakeRoom(SpawnCard card)
        {
            if (card.WhenFull != SpawnCapRule.RecycleOldest || !_capped.TryGetValue(card, out List<Spawnable> capped))
                return false;
            for (int i = capped.Count - 1; i >= 0; i--)
                if (capped[i] == null)
                    capped.RemoveAt(i);
            if (capped.Count == 0)
                return false;
            SpawnRules rules = _settings.DefaultRules;
            Spawnable recycled = capped[0];
            for (int i = 0; i < capped.Count && i < RecycleChecks; i++)
            {
                if (_visibility.IsSeen(capped[i].transform, rules))
                    continue;
                recycled = capped[i];
                break;
            }
            // Stops counting first, so the slot is free even before its OnDisable runs.
            recycled.Leave();
            recycled.gameObject.SetActive(false);
            return true;
        }

        // The card's variant by weight, or another of its variants with a free instance when that one has none left: random picks
        // can outrun a prewarm made by weight, and instantiating mid-fight costs ten times a wake.
        Spawnable PickPooled(SpawnCard card, float roll)
        {
            Spawnable prefab = card != null ? card.PickVariant(roll) : null;
            if (prefab == null || _pool.Free(prefab) > 0)
                return prefab;
            foreach ((Spawnable other, float weight) in card.Variants)
                if (other != null && weight > 0f && _pool.Free(other) > 0)
                    return other;
            return prefab;
        }

        // Withdrawn instances go back to the pool once the player can't see them, a few checked a frame.
        void SendOffLeaving()
        {
            if (_leaving.Count == 0)
                return;
            SpawnRules rules = _settings.DefaultRules;
            for (int checks = 0; checks < LeavingChecksPerFrame && _leaving.Count > 0; checks++)
            {
                if (_leavingCursor >= _leaving.Count)
                    _leavingCursor = 0;
                Spawnable instance = _leaving[_leavingCursor];
                bool gone = instance == null || instance.State != SpawnState.Leaving || !instance.gameObject.activeInHierarchy;
                if (gone || !_visibility.IsSeen(instance.transform, rules))
                {
                    if (!gone)
                        instance.gameObject.SetActive(false);
                    int last = _leaving.Count - 1;
                    _leaving[_leavingCursor] = _leaving[last];
                    _leaving.RemoveAt(last);
                    continue;
                }
                _leavingCursor++;
            }
        }

        static string DescribeArea(ISpawnArea area) => area is Object unityObject && unityObject != null ? unityObject.name : area.ToString();

        readonly struct QueuedMember
        {
            public readonly SpawnHandle Handle;
            public readonly SpawnCard Card;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly int Slot;

            public QueuedMember(SpawnHandle handle, SpawnCard card, Vector3 position, Quaternion rotation, int slot)
            {
                Handle = handle;
                Card = card;
                Position = position;
                Rotation = rotation;
                Slot = slot;
            }
        }
    }
}
