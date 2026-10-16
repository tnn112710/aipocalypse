using System;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    public enum GunAction
    {
        None,
        Reloading,
        ClearingJam,
        // Working a pump / bolt after a shot.
        Cycling,
    }

    public struct GunInput
    {
        public bool TriggerHeld;
        public bool TriggerPressed;
        public bool Aim;
        public bool Reload;

        // Raised and usable; false while switching cancels reloads and jam clearing.
        public bool IsReady;
        // False while sprinting.
        public bool CanFire;
        public bool CanAim;
    }

    // A held gun's rules: shooting, magazine, reloading, jams and aiming. Plain C#; HeldGun turns its events into
    // bullets, recoil, sound and motion.
    public sealed class Gun
    {
        readonly GunInstance _item;
        readonly GunDefinition _definition;
        readonly AmmoReserve _ammo;
        readonly GunTrigger _trigger;
        readonly Func<float> _random;

        float _actionTime;
        // Survives an interrupted cycle, so the pump finishes when the gun is raised again.
        bool _needsCycle;
        // Per-round reload: the trigger was pulled, so stop once the round going in is loaded.
        bool _stopReload;

        // One per shot that used a round. A shotgun shot is one event; HeldGun spawns its pellets.
        public event Action Fired;
        public event Action Jammed;
        public event Action<GunAction> ActionStarted;
        // Per-round reload: each round going in.
        public event Action RoundLoaded;
        // The action, and whether it finished (false = interrupted).
        public event Action<GunAction, bool> ActionEnded;

        // `random` returns 0..1 for jam rolls; tests pass a fixed one.
        public Gun(GunInstance item, AmmoReserve ammo, Func<float> random = null)
        {
            _item = item;
            _definition = item.Gun;
            _ammo = ammo;
            _trigger = new GunTrigger(_definition.Fire);
            _random = random ?? (() => UnityEngine.Random.value);
        }

        public GunInstance Item => _item;
        public GunAction Action { get; private set; }
        public bool IsAiming { get; private set; }

        // 0..1 through the current action; per round: through the current round.
        public float ActionProgress
        {
            get
            {
                float duration = ActionDuration();
                return Action == GunAction.None ? 0f : duration > 0f ? Mathf.Clamp01(_actionTime / duration) : 1f;
            }
        }

        public bool CanReload => !_item.IsFull && _ammo.Has(_definition.Magazine.AmmoType);

        public void Tick(in GunInput input, float deltaTime)
        {
            if (!input.IsReady)
            {
                Interrupt();
                return;
            }

            TickAction(deltaTime);
            if (Action == GunAction.Reloading && _definition.Magazine.Reload == ReloadType.PerRound &&
                input.TriggerPressed && input.CanFire)
                _stopReload = true;
            if (Action == GunAction.None)
                StartActionIfNeeded(input.Reload);

            // Reloading and clearing a jam drop the aim; working the pump doesn't.
            IsAiming = input.Aim && input.CanAim && Action is GunAction.None or GunAction.Cycling;

            _trigger.Update(deltaTime, input.TriggerHeld, input.TriggerPressed);
            bool keepsClick = Action == GunAction.Cycling || (Action == GunAction.Reloading && _stopReload);
            if ((Action != GunAction.None && !keepsClick) || !input.CanFire || _item.IsJammed)
            {
                _trigger.Cancel();
                return;
            }
            // A click during the pump, or one that stops a per-round reload, fires once that's done.
            if (keepsClick)
                return;

            while (_trigger.TryShot())
            {
                if (_item.Rounds <= 0)
                {
                    _trigger.Cancel();
                    break;
                }

                if (_random() < _definition.Jam.Chance)
                {
                    _item.SetJammed(true);
                    _trigger.Cancel();
                    Jammed?.Invoke();
                    break;
                }

                _item.SetRounds(_item.Rounds - 1);
                Fired?.Invoke();

                // Nothing left to chamber after the last round: it goes straight to the reload.
                if (_definition.Fire.CycleTime > 0f && _item.Rounds > 0)
                {
                    _needsCycle = true;
                    StartAction(GunAction.Cycling);
                    break;
                }
            }
        }

        // Holstered or switching: drops the aim and stops a reload, jam clear or pump (it restarts next time).
        public void Interrupt()
        {
            IsAiming = false;
            _trigger.Cancel();
            if (Action != GunAction.None)
                EndAction(false);
        }

        void StartActionIfNeeded(bool reloadPressed)
        {
            if (_item.IsJammed)
                StartAction(GunAction.ClearingJam);
            else if (_needsCycle)
                StartAction(GunAction.Cycling);
            else if ((reloadPressed || (_definition.Magazine.AutoReload && _item.Rounds == 0)) && CanReload)
                StartAction(GunAction.Reloading);
        }

        void StartAction(GunAction action)
        {
            Action = action;
            _actionTime = 0f;
            ActionStarted?.Invoke(action);
        }

        void EndAction(bool finished)
        {
            GunAction action = Action;
            Action = GunAction.None;
            _actionTime = 0f;
            _stopReload = false;
            ActionEnded?.Invoke(action, finished);
        }

        void TickAction(float deltaTime)
        {
            if (Action == GunAction.None)
                return;

            _actionTime += deltaTime;
            float duration = ActionDuration();

            if (Action is GunAction.ClearingJam or GunAction.Cycling)
            {
                if (_actionTime >= duration)
                {
                    if (Action == GunAction.ClearingJam)
                        _item.SetJammed(false);
                    else
                        _needsCycle = false;
                    EndAction(true);
                }
                return;
            }

            MagazineSettings magazine = _definition.Magazine;
            if (magazine.Reload == ReloadType.Magazine || duration <= 0f)
            {
                if (_actionTime < duration)
                    return;
                int loaded = _ammo.Take(magazine.AmmoType, magazine.Size - _item.Rounds);
                _item.SetRounds(_item.Rounds + loaded);
                EndAction(true);
                return;
            }

            bool roundIn = false;
            while (_actionTime >= duration)
            {
                _actionTime -= duration;
                if (_ammo.Take(magazine.AmmoType, 1) == 0)
                    break;
                _item.SetRounds(_item.Rounds + 1);
                roundIn = true;
                RoundLoaded?.Invoke();
                if (!CanReload || _stopReload)
                    break;
            }

            if (!CanReload || (roundIn && _stopReload))
                EndAction(true);
        }

        float ActionDuration() => Action switch
        {
            GunAction.Reloading => _definition.Magazine.ReloadTime,
            GunAction.ClearingJam => _definition.Jam.ClearTime,
            GunAction.Cycling => _definition.Fire.CycleTime,
            _ => 0f,
        };
    }
}
