using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.Ghosts;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MessagePipe;
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.DefenseSystem
{
    // One piece of a fence (or a gate, with a FenceGate beside it): its own health and look. While it stands it blocks the way:
    // a navmesh cut keeps enemies' paths out and a tall box keeps the player from climbing over. Enemies that can't reach what
    // they hunt attack it (IBreachable); at 0 it falls and the gap opens. With the hammer in hand the player repairs it a knock
    // at a time (a broken one too until it has its StandAt share back, showing a ghost of it standing meanwhile), or mends
    // every barrier at once from any of them (Repair All). Player damage never counts.
    [DisallowMultipleComponent]
    public sealed class BarrierSegment : MonoBehaviour, IBreachable, IInteractable, IHoldInteractable, IRepairable, IGhostSource
    {
        static readonly LocalizedString RepairVerb = Loc.Interaction("verb.repair");
        static readonly LocalizedString RebuildVerb = Loc.Interaction("verb.rebuild");
        static readonly LocalizedString RepairAllVerb = Loc.Interaction("verb.repair_all");
        static readonly LocalizedString BlockedSuffix = Loc.Interaction("suffix.blocked");
        static readonly LocalizedString HealthTarget = Loc.Interaction("target.health");
        // Metres above the base where knocks, breaks and debug hits land.
        const float RepairHeight = 0.8f;

        static readonly Collider[] Overlaps = new Collider[16];

        [SerializeField, InlineEditor] BarrierDefinition _definition;
        [SerializeField, Tooltip("Where the definition's Look is spawned; the current one, if already there, is kept")] Transform _lookParent;
        [SerializeField] BarrierLook _look;
        [SerializeField, Tooltip("Cuts enemies' navmesh while it stands (and, on a gate, while it's closed)")] NavmeshCut _cut;
        [SerializeField, Tooltip("The tall box the player can't climb over (layer Ignore Raycast: bullets and the interact cast pass)")]
        BoxCollider _blocker;
        [SerializeField, Min(0f), Tooltip("Metres from the centre line where attackers stand and swing")] float _standOff = 0.75f;
        [SerializeField, Range(0f, 1f), Tooltip("Share of its health it starts a new game with; 0 = down to debris, rebuilt with the hammer. A save overrides it")]
        float _startShare = 1f;
        [SerializeField, Tooltip("Fence posts beside it, refreshed when it falls or stands")] FencePost[] _posts = System.Array.Empty<FencePost>();

        AudioService _audio;
        DamageService _damage;
        EffectPool _effects;
        TargetRegistry _targets;
        AstarPath _astar;
        BarrierRegistry _registry;
        IPublisher<BarrierBroken> _broken;
        IPublisher<BarrierRestored> _restored;

        BarrierHealth _health;
        FenceGate _gate;
        bool _blocking;
        bool _blockingKnown;
        string _promptTarget;
        int _promptPercent = -1;
        bool _promptBlocked;
        Locale _promptLocale;
        GhostShape _ghost;
        GhostShape _overlay;

        public BarrierDefinition Definition => _definition;
        public BarrierLook Look => _look;
        public FenceGate Gate => _gate;
        public bool IsBroken => _health != null && _health.IsBroken;
        public float HealthShare => _health != null ? _health.Normalized : 1f;
        // Standing and closed: it's in the way.
        public bool IsBlocking => !IsBroken && (_gate == null || !_gate.IsOpen);

        public Transform Transform => transform;
        public TargetKind Kind => TargetKind.Fence;
        public bool IsTargetable => IsBlocking;
        public bool IsDead => IsBroken;
        public float RemainingHealth => _health != null ? _health.Current : 0f;
        public Vector3 Span => _blocker != null ? _blocker.transform.TransformVector(_blocker.size.x * 0.5f, 0f, 0f) : transform.right;
        public bool NeedsRepair => _health != null && _health.NeedsRepair;
        // Its model as it stands or lies now, for highlights over it (RepairHighlight).
        public GhostShape Overlay => _look != null ? _overlay ??= _look.CaptureLive() : null;

        [Inject]
        void Construct(AudioService audio, DamageService damage, EffectPool effects, IPublisher<BarrierBroken> broken,
            IPublisher<BarrierRestored> restored, IObjectResolver resolver)
        {
            _audio = audio;
            _damage = damage;
            _effects = effects;
            _broken = broken;
            _restored = restored;
            resolver.TryResolve(out _targets);
            resolver.TryResolve(out _astar);
            resolver.TryResolve(out _registry);
            // Injected after OnEnable when the scope builds late: register now (both lists ignore repeats).
            if (isActiveAndEnabled)
                OnEnable();
        }

        void Awake()
        {
            _gate = GetComponent<FenceGate>();
            if (_definition == null)
            {
                Debug.LogError($"{name}: no Barrier Definition.", this);
                return;
            }
            _health = new BarrierHealth(_definition.MaxHealth);
            _health.StartAt(_startShare);
            SpawnLook(_definition);
            Refresh(instant: true);
        }

        void OnEnable()
        {
            _targets?.Add(this);
            _registry?.Add(this);
        }

        void OnDisable()
        {
            _targets?.Remove(this);
            _registry?.Remove(this);
        }

        public Vector3 PointFor(Vector3 from)
        {
            Vector3 forward = transform.forward;
            float side = Vector3.Dot(from - transform.position, forward) >= 0f ? 1f : -1f;
            return transform.position + forward * (side * _standOff);
        }

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (_health == null || !IsBlocking || IsFriendly(info.Source))
                return default;
            float applied = _health.Damage(info.Amount * _definition.ScaleFor(info.Type));
            if (applied <= 0f)
                return default;

            bool killed = _health.IsBroken;
            if (_look != null && info.Source != null)
                _look.PushedToward(transform.position - info.Source.transform.position);
            _audio?.PlayAt(_definition.HitSound, info.Point);
            Spawn(_definition.HitEffect, info.Point);
            if (killed)
                Break(info.Source);
            else
            {
                _look?.Punch();
                Refresh();
            }

            var strike = new BarrierStrike(this, info, applied, _damage);
            foreach (BarrierEffect effect in _definition.OnStruck)
                if (effect != null)
                    effect.OnStruck(strike);
            return new DamageResult(applied, killed);
        }

        // ---- interaction: a standing gate opens and closes on a tap; with the hammer in hand, the interact key mends every
        // barrier (a hold on a standing gate) and the hammer's own key repairs this one (IRepairable, through PlayerRepair) ----

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            if (_gate != null && !IsBroken)
                return _gate.TryGetPrompt(out prompt);
            return TryGetRepairAllPrompt(actor, out prompt);
        }

        public void Interact(GameObject actor)
        {
            if (_gate != null && !IsBroken)
                _gate.Toggle(actor.transform.position);
            else
                RepairAll(actor);
        }

        public bool TryGetHoldPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            return _gate != null && !IsBroken && TryGetRepairAllPrompt(actor, out prompt);
        }

        public void InteractHold(GameObject actor) => RepairAll(actor);

        public bool TryGetRepairPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!NeedsRepair)
                return false;
            prompt = new InteractionPrompt((IsBroken ? RebuildVerb : RepairVerb).Text(), PromptTarget(StandBlocked()));
            return true;
        }

        // While broken: the barrier standing again where it fell, Invalid while something stands in the way of the next knock.
        public bool TryGetGhost(GameObject viewer, out GhostView ghost)
        {
            ghost = default;
            if (!IsBroken || _look == null)
                return false;
            _ghost ??= _look.CaptureStanding();
            ghost = new GhostView(_ghost, _look.transform.localToWorldMatrix, StandBlocked() ? GhostState.Invalid : GhostState.Valid);
            return true;
        }

        public Vector3 RepairPoint(Vector3 from) => PointFor(from) * 0.5f + transform.position * 0.5f + Vector3.up * RepairHeight;

        public bool Knock(GameObject actor)
        {
            if (!NeedsRepair)
                return false;
            Vector3 point = RepairPoint(actor != null ? actor.transform.position : transform.position);
            _health.Repair(_definition.RepairPerKnock);
            _audio?.PlayAt(_definition.RepairSound, point);
            Spawn(_definition.RepairEffect, point);
            if (_health.IsBroken)
            {
                if (!_health.CanStand(_definition.StandAt))
                    return true;
                if (IsOccupied())
                    return false;
                _health.Stand();
                _gate?.ResetClosed();
                Refresh();
                _restored?.Publish(new BarrierRestored(transform.position, _gate != null));
            }
            else
                Refresh();
            return NeedsRepair;
        }

        // ---- upgrades and debug ----

        // Swaps to the next tier (BarrierDefinition.UpgradesTo), keeping its share of health. False at the top tier.
        public bool Upgrade() => _definition != null && _definition.UpgradesTo != null && Apply(_definition.UpgradesTo);

        public bool Apply(BarrierDefinition definition)
        {
            if (definition == null || _health == null)
                return false;
            BarrierDefinition previous = _definition;
            _definition = definition;
            _health.SetMax(definition.MaxHealth);
            if (definition.Look != previous.Look)
                SpawnLook(definition, replace: true);
            Refresh(instant: true);
            return true;
        }

        // Whole again at once, with a puff of chips (Repair All). False when it was whole, or it's broken and something stands
        // in its line.
        public bool RepairFully()
        {
            if (!NeedsRepair || (IsBroken && IsOccupied()))
                return false;
            Restore();
            Spawn(_definition.RepairEffect, transform.position + Vector3.up * RepairHeight);
            return true;
        }

        [Button, HideInEditorMode]
        public void Restore()
        {
            if (_health == null)
                return;
            bool wasBroken = _health.IsBroken;
            _health.Restore();
            _gate?.ResetClosed();
            Refresh();
            if (wasBroken)
                _restored?.Publish(new BarrierRestored(transform.position, _gate != null));
        }

        // Loading a save: the saved health, broken or standing, and the gate open or shut, at once and without a sound,
        // effect or event.
        public void RestoreSaved(float health, bool broken, bool gateOpen)
        {
            if (_health == null)
                return;
            _health.Set(health, broken);
            if (_gate != null)
            {
                _gate.ResetClosed();
                if (gateOpen && !_health.IsBroken)
                    _gate.OpenInstantly();
            }
            Refresh(instant: true);
        }

        [Button, HideInEditorMode]
        public void DealDamage(float amount = 100f) =>
            TakeDamage(new DamageInfo(amount, DamageType.Generic, transform.position + Vector3.up * RepairHeight));

        // ---- internals ----

        // Something alive stands where it would block (the doorway of a closing gate, the line of a fence standing up again).
        public bool IsOccupied()
        {
            if (_blocker == null)
                return false;
            Transform box = _blocker.transform;
            Vector3 center = box.TransformPoint(_blocker.center);
            Vector3 halfExtents = Vector3.Scale(_blocker.size, box.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, Overlaps, box.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = Overlaps[i];
                Overlaps[i] = null;
                if (other.transform.IsChildOf(transform))
                    continue;
                IDamageable body = other.GetComponentInParent<IDamageable>();
                if (body != null && !body.IsDead && body is not BarrierSegment)
                    return true;
            }
            return false;
        }

        internal void Refresh(bool instant = false)
        {
            bool blocking = IsBlocking;
            bool changed = _blockingKnown && blocking != _blocking;
            _blocking = blocking;
            _blockingKnown = true;
            if (_cut != null)
                _cut.enabled = blocking;
            if (_blocker != null)
                _blocker.enabled = blocking;
            if (_look != null)
                _look.Show(HealthShare, IsBroken, instant);
            foreach (FencePost post in _posts)
                if (post != null)
                    post.Refresh();
            if (changed)
                WayChanged();
        }

        // Applies the cut now (A* would on its next update) and makes every hunter choose again, so the ones at the fence see
        // a new gap, or a shut gate, at once instead of picking the next segment first.
        void WayChanged()
        {
            if (_astar != null)
            {
                _astar.navmeshUpdates.ForceUpdate();
                _astar.FlushWorkItems();
            }
            _targets?.BarriersChanged();
        }

        void Break(GameObject source)
        {
            Vector3 center = transform.position + Vector3.up * RepairHeight;
            _audio?.PlayAt(_definition.BreakSound, center);
            // Debris flies away from the attacker, from a 1 m wide box stretched to the barrier's width (a double gate is two panels).
            Vector3 away = transform.forward;
            if (source != null && Vector3.Dot(transform.position - source.transform.position, away) < 0f)
                away = -away;
            PooledEffect debris = _definition.BreakEffect != null && _effects != null
                ? _effects.Spawn(_definition.BreakEffect, center, Quaternion.LookRotation(away))
                : null;
            if (debris != null)
                debris.transform.localScale = new Vector3(Span.magnitude * 2f, 1f, 1f);
            Refresh();
            _broken?.Publish(new BarrierBroken(transform.position, _gate != null));
        }

        // Every barrier in the scene (just this one without a registry), with one repair sound here.
        void RepairAll(GameObject actor)
        {
            if (!HoldsTool(actor))
                return;
            int mended = _registry != null ? _registry.RepairAll() : !IsBroken && RepairFully() ? 1 : 0;
            if (mended > 0)
                _audio?.PlayAt(_definition.RepairSound, RepairPoint(actor.transform.position));
        }

        // "Repair All Fences (5)", offered with the hammer in hand while anything needs repair.
        bool TryGetRepairAllPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!HoldsTool(actor))
                return false;
            int damaged = _registry != null ? _registry.CountDamaged() : NeedsRepair && !IsBroken ? 1 : 0;
            if (damaged == 0)
                return false;
            prompt = new InteractionPrompt(RepairAllVerb.Text(), BarrierRegistry.RepairAllTarget(damaged));
            return true;
        }

        static bool HoldsTool(GameObject actor) => actor.TryGetComponent(out IRepairer repairer) && repairer.HasTool && !repairer.IsRepairing;

        // Only checked once it's ready to stand: the next knock would stand it up if nothing were in the way.
        bool StandBlocked() => IsBroken && _health.CanStand(_definition.StandAt) && IsOccupied();

        // "Fence 63%": rebuilt only when the number or the language changes, so a steady look allocates nothing.
        string PromptTarget(bool blocked)
        {
            int percent = Mathf.FloorToInt(HealthShare * 100f);
            if (percent != _promptPercent || blocked != _promptBlocked || _promptTarget == null || _promptLocale != Loc.Current)
            {
                _promptPercent = percent;
                _promptBlocked = blocked;
                _promptLocale = Loc.Current;
                _promptTarget = HealthTarget.Format(_definition.DisplayName.Text(), percent) + (blocked ? BlockedSuffix.Text() : string.Empty);
            }
            return _promptTarget;
        }

        void SpawnLook(BarrierDefinition definition, bool replace = false)
        {
            if (_look != null && !replace)
                return;
            if (_look != null)
                Destroy(_look.gameObject);
            _look = null;
            _ghost = null;
            _overlay = null;
            if (definition.Look == null)
                return;
            Transform parent = _lookParent != null ? _lookParent : transform;
            _look = Instantiate(definition.Look, parent, false);
            _look.name = definition.Look.name;
        }

        void Spawn(PooledEffect effect, Vector3 point)
        {
            if (effect != null && _effects != null)
                _effects.Spawn(effect, point, Quaternion.LookRotation(transform.forward));
        }

        static bool IsFriendly(GameObject source) => source != null && source.TryGetComponent(out IPlayerTarget _);

#if UNITY_EDITOR
        // Setup scripts only.
        public void Setup(BarrierDefinition definition, Transform lookParent, BarrierLook look, NavmeshCut cut, BoxCollider blocker)
        {
            _definition = definition;
            _lookParent = lookParent;
            _look = look;
            _cut = cut;
            _blocker = blocker;
        }
#endif
    }
}
