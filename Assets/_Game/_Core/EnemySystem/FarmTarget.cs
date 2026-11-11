using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Entities;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // The home's yard as a target: the middle of the fence ring (every registered barrier, standing or not), what raiders go
    // for before they notice the player. It can't be hurt; an enemy that reaches it roams there (EnemyBrain.ChooseTarget).
    public sealed class FarmTarget : ITargetable
    {
        Transform _transform;

        // Made when first needed; never saved with a scene (edit-mode tests touch it too).
        public Transform Transform => _transform != null ? _transform
            : _transform = new GameObject("Farm Target") { hideFlags = HideFlags.HideAndDontSave }.transform;
        public TargetKind Kind => TargetKind.Farm;
        public bool IsTargetable => true;
        public bool IsDead => false;

        public DamageResult TakeDamage(in DamageInfo info) => default;

        public void Place(IReadOnlyList<IBreachable> barriers)
        {
            if (barriers.Count == 0)
                return;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < barriers.Count; i++)
                sum += barriers[i].Transform.position;
            Transform.position = sum / barriers.Count;
        }

        public void Dispose()
        {
            if (_transform == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(_transform.gameObject);
            else
                Object.DestroyImmediate(_transform.gameObject);
            _transform = null;
        }
    }
}
