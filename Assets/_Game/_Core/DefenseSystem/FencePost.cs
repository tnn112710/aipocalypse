using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DefenseSystem
{
    // A fence post between two segments: shown while either of them stands, so a run that is down to debris has no lone
    // posts left in it. The segments refresh it whenever they fall or stand (BarrierSegment._posts).
    [DisallowMultipleComponent]
    public sealed class FencePost : MonoBehaviour
    {
        [SerializeField, Tooltip("The segments either side of it (one or two)")] BarrierSegment[] _segments = Array.Empty<BarrierSegment>();

        public void Refresh()
        {
            bool shown = false;
            foreach (BarrierSegment segment in _segments)
                if (segment != null && !segment.IsBroken)
                    shown = true;
            if (gameObject.activeSelf != shown)
                gameObject.SetActive(shown);
        }
    }
}
