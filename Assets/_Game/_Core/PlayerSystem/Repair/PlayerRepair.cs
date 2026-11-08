using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Interaction;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Repair work with the hammer in hand (RepairTool): the reload key on an IRepairable in view starts knocking in a steady
    // rhythm, one repair per knock, until it's whole. Stops early, finishing the swing, when the player looks away, walks off
    // or presses a key (reload or interact again, fire, aim, jump); switching or dropping the hammer stops it at once.
    public sealed class PlayerRepair
    {
        readonly GameObject _actor;
        readonly Transform _eye;
        readonly Equipment _equipment;
        readonly EquipmentRig _rig;
        readonly PlayerInteractor _interactor;
        readonly PlayerInteractionTuning _tuning;

        IRepairable _target;
        RepairTool _working;
        float _cycle;
        float _lookAway;
        bool _finishing;

        public PlayerRepair(GameObject actor, Transform eye, Equipment equipment, EquipmentRig rig, PlayerInteractor interactor,
            PlayerInteractionTuning tuning)
        {
            _actor = actor;
            _eye = eye;
            _equipment = equipment;
            _rig = rig;
            _interactor = interactor;
            _tuning = tuning;
        }

        public RepairTool Tool => _rig != null ? _rig.Current as RepairTool : null;
        public bool HasTool => Tool != null;
        public bool IsBusy => _target != null;
        public IRepairable Target => _target;
        // What the reload key would repair now ("[R] Repair Fence 63%").
        public bool HasPrompt { get; private set; }
        public InteractionPrompt Prompt { get; private set; }

        // At once, with no swing finished (death, a menu).
        public void Cancel()
        {
            if (_working != null)
                _working.Cycle = -1f;
            _working = null;
            _target = null;
            HasPrompt = false;
        }

        public void Tick(in PlayerIntent intent, float deltaTime)
        {
            RepairTool tool = Tool;
            if (IsBusy)
            {
                if (tool != _working)
                {
                    Cancel();
                    return;
                }
                if (ShouldStop(intent, deltaTime))
                    _finishing = true;
                Work(deltaTime);
                return;
            }

            HasPrompt = false;
            if (tool == null || _equipment == null || !_equipment.Switcher.IsReady || _interactor.Focus is not IRepairable target
                || !target.TryGetRepairPrompt(_actor, out InteractionPrompt prompt))
                return;
            HasPrompt = true;
            Prompt = prompt;
            if (intent.Reload)
                Start(target, tool);
        }

        void Start(IRepairable target, RepairTool tool)
        {
            _target = target;
            _working = tool;
            _finishing = false;
            _lookAway = 0f;
            _cycle = 0f;
            HasPrompt = false;
        }

        // A knock lands when the swing passes the tool's strike point; a stop request waits for the swing to finish.
        void Work(float deltaTime)
        {
            float previous = _cycle;
            _cycle += deltaTime / _tuning.RepairKnockInterval;
            if (previous < _working.StrikeAt && _cycle >= _working.StrikeAt && !_finishing && !_target.Knock(_actor))
                _finishing = true;
            if (_cycle >= 1f)
            {
                if (_finishing)
                {
                    Cancel();
                    return;
                }
                _cycle -= 1f;
            }
            _working.Cycle = _cycle;
        }

        bool ShouldStop(in PlayerIntent intent, float deltaTime)
        {
            if (intent.Reload || intent.Interact || intent.FirePressed || intent.Aim || intent.Jump)
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
