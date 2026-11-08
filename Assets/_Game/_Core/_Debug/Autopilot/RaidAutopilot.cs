using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.Debugging
{
    // cheat.autopilot (dev builds): through IPlayerIntentSource the player stands still and shoots the nearest enemy in sight,
    // reloading and taking up another gun when the held one is spent.
    [CommandGroup("cheat")]
    public sealed class RaidAutopilot : ConsoleCommands, ITickable, IPlayerIntentSource
    {
        const float Range = 70f;
        const float SearchInterval = 0.2f;
        // Degrees per second.
        const float TurnRate = 540f;
        // Degrees off the aim point it still fires at; narrower for far targets, so a shot stays within TargetRadius.
        const float FireAngle = 2f;
        const float TargetRadius = 0.35f;
        // Aim point: this share of the body collider's height, or this height (m) above an enemy without a root collider.
        const float ChestShare = 0.7f;
        const float ChestHeight = 1.1f;

        static readonly CommandResult NoPlayer = CommandResult.Fail("No player in this scene");

        readonly PlayerRegistry _players;
        readonly EnemyRegistry _enemies;
        readonly RaycastHit[] _hits = new RaycastHit[16];
        // What blocks the view of a target: not enemies (bullets stop at the nearer one anyway) or first-person models.
        readonly int _blockers;

        bool _on;
        IPlayer _driven;
        EnemyBrain _target;
        Collider _targetBody;
        float _nextSearch;

        public RaidAutopilot(CommandRegistry registry, PlayerRegistry players, EnemyRegistry enemies) : base(registry)
        {
            _players = players;
            _enemies = enemies;
            _blockers = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Enemy", "BodyPart", "HeldWeapon", "Debris");
        }

        public override void Dispose()
        {
            base.Dispose();
            _on = false;
            Drive(null);
        }

        [Command("Whether the player fights by itself")]
        CommandResult Autopilot() => CommandResult.Ok(CommandParser.Format(_on));

        [Command("The player stands still and shoots the nearest enemy in sight (raid soak tests)")]
        CommandResult Autopilot(bool on)
        {
            IPlayer player = _players.Current;
            if (on && player?.Equipment == null)
                return NoPlayer;
            _on = on;
            Drive(on ? player : null);
            if (on && !HasGun(player))
                return CommandResult.Ok("on (no gun in the hotbar: it only turns to targets)");
            return Autopilot();
        }

        public void Tick()
        {
            if (!_on)
                return;
            Drive(_players.Current);
            if (_driven == null || _driven.IsDead)
            {
                Choose(null);
                return;
            }
            // A target that died or left is replaced at once; otherwise the nearest one is looked for a few times a second.
            bool lost = _target != null && !IsAlive(_target);
            if (!lost && Time.time < _nextSearch)
                return;
            _nextSearch = Time.time + SearchInterval;
            Choose(FindTarget(_driven));
        }

        public PlayerIntent Read(float deltaTime)
        {
            var intent = new PlayerIntent();
            IPlayer player = _driven;
            if (player?.Equipment == null)
                return intent;

            int spare = SpareGun(player);
            if (spare >= 0)
            {
                intent.SelectSlot = spare;
                return intent;
            }

            Gun gun = player.HeldItem is HeldGun held ? held.Gun : null;
            if (gun != null && gun.Action == GunAction.None && (gun.Item.IsJammed || gun.Item.Rounds == 0))
                intent.Reload = true;
            if (!IsAlive(_target))
                return intent;

            Transform view = player.View;
            Vector3 toTarget = AimPoint(_target, _targetBody) - view.position;
            float distance = toTarget.magnitude;
            if (distance < 0.01f)
                return intent;
            Vector3 forward = view.forward;
            float yaw = Vector3.SignedAngle(Flat(forward), Flat(toTarget), Vector3.up);
            var error = new Vector2(yaw, Elevation(toTarget) - Elevation(forward));
            float step = TurnRate * deltaTime;
            intent.LookDegrees = error.sqrMagnitude > step * step ? error.normalized * step : error;

            float tolerance = Mathf.Min(FireAngle, Mathf.Atan2(TargetRadius, distance) * Mathf.Rad2Deg);
            // A press during a per-round reload stops it once the next round is in, as a player would.
            bool canFire = gun != null && player.Equipment.Switcher.IsReady && !gun.Item.IsJammed && gun.Item.Rounds > 0;
            // Shots follow the crosshair (IGunUser.AimOffset: aim sway, which lags behind turns), not the view's forward.
            Vector3 aim = player is IGunUser user ? view.rotation * (user.AimOffset * Vector3.forward) : forward;
            intent.Fire = canFire && Vector3.Angle(aim, toTarget) <= tolerance;
            intent.FirePressed = intent.Fire;
            return intent;
        }

        // Hands the player to the autopilot (null = back to the devices); only a source it set itself is cleared.
        void Drive(IPlayer player)
        {
            if (ReferenceEquals(player, _driven))
                return;
            if (_driven != null && ReferenceEquals(_driven.IntentSource, this))
                _driven.IntentSource = null;
            _driven = player;
            Choose(null);
            if (player != null)
                player.IntentSource = this;
        }

        void Choose(EnemyBrain target)
        {
            _target = target;
            _targetBody = null;
            if (target != null)
                target.TryGetComponent(out _targetBody);
        }

        // Nearest living enemy within Range whose aim point the eye can see. Farther ones skip the sight ray.
        EnemyBrain FindTarget(IPlayer player)
        {
            Vector3 eye = player.View.position;
            Transform self = player.Transform;
            EnemyBrain best = null;
            float bestSqr = Range * Range;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyBrain enemy = _enemies[i];
                if (enemy.IsDead)
                    continue;
                float sqr = (enemy.transform.position - eye).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                enemy.TryGetComponent(out Collider body);
                if (!InSight(eye, AimPoint(enemy, body), self))
                    continue;
                best = enemy;
                bestSqr = sqr;
            }
            return best;
        }

        bool InSight(Vector3 eye, Vector3 point, Transform self)
        {
            Vector3 toPoint = point - eye;
            float distance = toPoint.magnitude;
            if (distance < 0.01f)
                return true;
            int count = Physics.RaycastNonAlloc(eye, toPoint / distance, _hits, distance, _blockers, QueryTriggerInteraction.Ignore);
            return !PhysicsQueries.Nearest(_hits, count, self, out _);
        }

        // Slot of another gun that can still shoot when the selected item can't (a spent gun, a melee weapon, empty hands); -1 to
        // keep what is selected.
        static int SpareGun(IPlayer player)
        {
            Equipment equipment = player.Equipment;
            if (CanShoot(equipment.Selected as GunInstance, player.Ammo))
                return -1;
            Hotbar hotbar = equipment.Hotbar;
            for (int i = 0; i < hotbar.Count; i++)
                if (i != equipment.SelectedIndex && CanShoot(hotbar[i] as GunInstance, player.Ammo))
                    return i;
            return -1;
        }

        static bool CanShoot(GunInstance gun, AmmoReserve ammo) =>
            gun != null && (gun.Rounds > 0 || (ammo != null && ammo.Has(gun.Gun.Magazine.AmmoType)));

        static bool HasGun(IPlayer player)
        {
            Hotbar hotbar = player.Equipment.Hotbar;
            for (int i = 0; i < hotbar.Count; i++)
                if (hotbar[i] is GunInstance)
                    return true;
            return false;
        }

        static bool IsAlive(EnemyBrain enemy) => enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead;

        static Vector3 AimPoint(EnemyBrain enemy, Collider body)
        {
            if (body == null || !body.enabled)
                return enemy.transform.position + Vector3.up * ChestHeight;
            Bounds bounds = body.bounds;
            return new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * ChestShare, bounds.center.z);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        // Degrees above the horizon.
        static float Elevation(Vector3 v) => Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;
    }
}
