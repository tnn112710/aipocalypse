using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // One request on its way and in play: how many it brings, how many are still to come, and its members while they live.
    // Members still to come count as alive, so caps can't be overshot while a group arrives.
    public sealed class SpawnHandle
    {
        readonly SpawnService _service;
        readonly List<Member> _members = new();

        internal SpawnHandle(SpawnService service, in SpawnRequest request, SpawnCard card, int count)
        {
            _service = service;
            Request = request;
            Card = card;
            Count = Pending = count;
            Since = Time.time;
        }

        internal SpawnRequest Request { get; }
        // The request's Tag, for its members (SpawnInfo.Handle).
        public object Tag => Request.Tag;
        internal float Since { get; }
        internal float NextTry { get; set; }
        internal bool Warned { get; set; }
        // Members of this request killed or used up (Spawnable.Retire), for respawn timers (PopulationDirector).
        public int Spent { get; private set; }
        // Slot 0 once it arrived, for its followers' SpawnInfo.
        internal Spawnable Leader { get; set; }

        // The card (a table's pick; a squad's lead); null when the request had nothing to spawn.
        public SpawnCard Card { get; }
        // Members it brings (each placed); less than asked when some spots couldn't be placed.
        public int Count { get; private set; }
        // Members not in play yet: still looking for a spot or queued.
        public int Pending { get; private set; }
        // Its spot is found and its members are queued.
        public bool IsPlaced { get; private set; }
        public Vector3 Anchor { get; private set; }
        public bool Cancelled { get; private set; }
        // Nothing more will arrive.
        public bool IsDone => Cancelled || (IsPlaced && Pending == 0);

        // Members in play and alive (not dead, withdrawn or despawned), plus those still to come.
        public int Alive
        {
            get
            {
                int alive = Pending;
                for (int i = _members.Count - 1; i >= 0; i--)
                {
                    if (_members[i].IsAlive)
                        alive++;
                    else if (_members[i].IsGone)
                        _members.RemoveAt(i);
                }
                return alive;
            }
        }

        // Drops the members still to come.
        public void Cancel()
        {
            Cancelled = true;
            Pending = 0;
        }

        // Cancels the rest, and despawns its members in play: dead ones are left to lie.
        public void Despawn(DespawnMode mode)
        {
            Cancel();
            foreach (Member member in _members)
                if (member.IsCurrent && member.Instance.State is SpawnState.Active or SpawnState.Leaving)
                    _service.Despawn(member.Instance, mode);
            _members.Clear();
        }

        // Its members in play and alive, added to `into`.
        public void GetAlive(List<Spawnable> into)
        {
            foreach (Member member in _members)
                if (member.IsAlive)
                    into.Add(member.Instance);
        }

        internal void Placed(Vector3 anchor)
        {
            IsPlaced = true;
            Anchor = anchor;
        }

        // A spot that couldn't be placed: one member fewer.
        internal void Dropped()
        {
            Count--;
            Pending = Mathf.Max(0, Pending - 1);
        }

        internal void MemberSpent() => Spent++;

        internal void Arrived(Spawnable instance)
        {
            Pending = Mathf.Max(0, Pending - 1);
            if (instance != null)
                _members.Add(new Member(instance));
        }

        readonly struct Member
        {
            public readonly Spawnable Instance;
            readonly int _life;

            public Member(Spawnable instance)
            {
                Instance = instance;
                _life = instance.Life;
            }

            // Still the life this handle spawned (a pooled instance may have been reused since).
            public bool IsCurrent => Instance != null && Instance.Life == _life;
            public bool IsAlive => IsCurrent && Instance.IsAlive;
            public bool IsGone => !IsCurrent || Instance.State == SpawnState.Pooled;
        }
    }
}
