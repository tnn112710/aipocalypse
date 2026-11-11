using MB13.Aipocalypse.Core.Entities;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Line of sight for spotting: one ray from the looker's eyes to the target's chest. Other enemies (the looker's own
    // layer), the looker and the target themselves, and barriers (a fence is seen through) don't block it.
    public static class EnemySight
    {
        public const float EyeHeight = 1.4f;
        public const float TargetHeight = 1.2f;

        static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static bool IsClear(Vector3 eye, Vector3 point, int mask, Transform looker, Transform target)
        {
            Vector3 path = point - eye;
            float distance = path.magnitude;
            if (distance < 0.01f)
                return true;
            mask &= ~(1 << looker.gameObject.layer);
            int count = Physics.RaycastNonAlloc(eye, path / distance, Hits, distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = Hits[i].collider.transform;
                if (hit.IsChildOf(looker) || (target != null && hit.IsChildOf(target)) || Hits[i].collider.GetComponentInParent<IBreachable>() != null)
                    continue;
                return false;
            }
            return true;
        }
    }
}
