using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    public sealed class HeldItemClearance
    {
        readonly Transform _eye;
        readonly Transform _root;
        readonly PlayerHeldItemTuning _tuning;
        readonly EquipmentRig _rig;
        readonly RaycastHit[] _hits = new RaycastHit[16];
        HeldItem _previous;
        float _weight;

        public HeldItemClearance(Transform eye, Transform root, PlayerHeldItemTuning tuning, EquipmentRig rig)
        {
            _eye = eye;
            _root = root;
            _tuning = tuning;
            _rig = rig;
        }

        public void Tick(HeldItem held, float deltaTime)
        {
            if (held != _previous)
            {
                _previous = held;
                _weight = 0f;
            }
            if (held == null || _rig == null || !held.isActiveAndEnabled || !held.KeepsClear)
            {
                _weight = 0f;
                return;
            }

            float target = 0f;
            if (_tuning.KeepHeldItemClear && held.TryGetTip(_eye.position, _eye.forward, out Vector3 tip))
            {
                Vector3 toTip = tip - _eye.position;
                float reach = toTip.magnitude + _tuning.ClearanceMargin;
                if (reach > 0.001f && TryHit(_eye.position, toTip.normalized, reach, out float distance))
                    target = Mathf.Clamp01((reach - distance) / Mathf.Max(0.01f, _tuning.ClearanceRetractDistance));
            }

            float blend = 1f - Mathf.Exp(-_tuning.ClearanceBlendSpeed * Mathf.Max(0f, deltaTime));
            _weight = Mathf.Lerp(_weight, target, blend);
            _rig.ApplyObstaclePose(_weight, _tuning.ClearanceRetractDistance, _tuning.ClearanceEuler);
        }

        bool TryHit(Vector3 origin, Vector3 direction, float distance, out float nearestDistance)
        {
            int count = Physics.SphereCastNonAlloc(origin, _tuning.ClearanceRadius, direction, _hits, distance,
                _tuning.ClearanceLayers, QueryTriggerInteraction.Ignore);
            nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance >= nearestDistance || hit.collider.transform.IsChildOf(_root))
                    continue;
                nearestDistance = hit.distance;
            }
            return nearestDistance < float.MaxValue;
        }
    }
}
