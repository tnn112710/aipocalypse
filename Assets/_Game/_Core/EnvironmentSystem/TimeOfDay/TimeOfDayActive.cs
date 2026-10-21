using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Shows objects only during their hours (a night-only prop, a daytime market stall).
    public sealed class TimeOfDayActive : TimeOfDayReactor
    {
        [SerializeField] GameObject[] _targets = System.Array.Empty<GameObject>();
        [SerializeField, Range(0.01f, 1f)] float _threshold = 0.5f;

        protected override void Apply(float weight)
        {
            bool active = weight >= _threshold;
            foreach (GameObject target in _targets)
                if (target != null && target.activeSelf != active)
                    target.SetActive(active);
        }
    }
}
