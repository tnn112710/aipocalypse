using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.Interaction;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Repair work on something the player looked at (IRepairable): puts the held item away, raises the hammer (RepairTool) and
    // knocks in a steady rhythm, one repair per knock, until it's whole. Stops early, finishing the swing, when the player looks
    // away, walks off or uses a key (interact again, fire, aim, jump, slot, drop); free for now (a cost comes with currency).
    public sealed class PlayerRepair
    {
        enum Phase
        {
            None,
            Lowering,
            Raising,
            Working,
            PuttingAway,
        }

        readonly GameObject _actor;
        readonly Transform _eye;
        readonly Equipment _equipment;
        readonly RepairTool _tool;
        readonly PlayerInteractionTuning _tuning;

        Phase _phase;
        IRepairable _target;
        float _raise;
        float _cycle;
        float _lookAway;
        bool _finishing;

        public PlayerRepair(GameObject actor, Transform eye, Equipment equipment, RepairTool tool, PlayerInteractionTuning tuning)
        {
            _actor = actor;
            _eye = eye;
            _equipment = equipment;
            _tool = tool;
            _tuning = tuning;
        }

        public bool IsBusy => _phase != Phase.None;
        public IRepairable Target => _target;
        // The hammer's grips while it's up, for the hands.
        public ItemHands Hands => _tool != null && _phase is Phase.Raising or Phase.Working or Phase.PuttingAway ? _tool.Hands : null;

        public bool TryStart(IRepairable target)
        {
            if (IsBusy || target == null || !target.NeedsRepair)
                return false;
            _target = target;
            _finishing = false;
            _lookAway = 0f;
            _cycle = 0f;
            _raise = 0f;
            _equipment?.LowerHands();
            _phase = Phase.Lowering;
            return true;
        }

        // At once, with no swing finished (death, a menu).
        public void Cancel(bool raiseHands)
        {
            if (!IsBusy)
                return;
            _tool?.SetVisible(false);
            _phase = Phase.None;
            _target = null;
            if (raiseHands)
                _equipment?.RaiseHands();
        }

        public void Tick(in PlayerIntent intent, float deltaTime)
        {
            if (!IsBusy)
                return;
            if (_phase != Phase.PuttingAway && ShouldStop(intent, deltaTime))
                _finishing = true;

            switch (_phase)
            {
                case Phase.Lowering:
                    if (_finishing)
                    {
                        Cancel(raiseHands: true);
                        return;
                    }
                    if (_equipment == null || _equipment.Switcher.Held == null)
                    {
                        _tool?.SetVisible(true);
                        _phase = Phase.Raising;
                    }
                    break;
                case Phase.Raising:
                    _raise = Mathf.MoveTowards(_raise, 1f, deltaTime / _tuning.RepairRaiseTime);
                    if (_finishing)
                        _phase = Phase.PuttingAway;
                    else if (_raise >= 1f)
                        _phase = Phase.Working;
                    _tool?.Pose(_raise, -1f);
                    break;
                case Phase.Working:
                    Work(deltaTime);
                    break;
                case Phase.PuttingAway:
                    _raise = Mathf.MoveTowards(_raise, 0f, deltaTime / _tuning.RepairRaiseTime);
                    _tool?.Pose(_raise, -1f);
                    if (_raise <= 0f)
                        Cancel(raiseHands: true);
                    break;
            }
        }

        // A knock lands when the swing passes the tool's strike point; a stop request waits for the swing to finish.
        void Work(float deltaTime)
        {
            float strikeAt = _tool != null ? _tool.StrikeAt : 0.5f;
            float previous = _cycle;
            _cycle += deltaTime / _tuning.RepairKnockInterval;
            if (previous < strikeAt && _cycle >= strikeAt && !_finishing && !_target.Knock(_actor))
                _finishing = true;
            if (_cycle >= 1f)
            {
                if (_finishing)
                {
                    _phase = Phase.PuttingAway;
                    _cycle = -1f;
                }
                else
                    _cycle -= 1f;
            }
            _tool?.Pose(1f, _cycle);
        }

        bool ShouldStop(in PlayerIntent intent, float deltaTime)
        {
            if (intent.Interact || intent.FirePressed || intent.Aim || intent.Jump || intent.SelectSlot.HasValue || intent.CycleSlot != 0
                || intent.Drop || _target == null)
                return true;
            Vector3 point = _target.RepairPoint(_actor.transform.position);
            Vector3 toPoint = point - _eye.position;
            float reach = _tuning.InteractRange + _tuning.RepairReachSlack;
            if (toPoint.sqrMagnitude > reach * reach)
                return true;
            bool lookingAway = Vector3.Angle(_eye.forward, toPoint) > _tuning.RepairLookAngle;
            _lookAway = lookingAway ? _lookAway + deltaTime : 0f;
            return _lookAway > _tuning.RepairLookAwayGrace;
        }
    }
}
