using UnityEngine;

namespace SunfishCreations.IK
{
    /// <summary>
    /// Provides world-space IK target data for a character's limbs.
    /// </summary>
    public interface IIKBridge
    {
        Vector3 LeftHandPosition { get; }
        Quaternion LeftHandRotation { get; }
        Vector3 RightHandPosition { get; }
        Quaternion RightHandRotation { get; }
        Vector3 LeftFootPosition { get; }
        Vector3 RightFootPosition { get; }

        /// <summary>Character-local offset from the left shoulder for the elbow pole direction.</summary>
        Vector3 LeftElbowPoleLocal { get; }

        /// <summary>Character-local offset from the right shoulder for the elbow pole direction.</summary>
        Vector3 RightElbowPoleLocal { get; }

        /// <summary>Character-local offset from the left upper leg for the knee pole direction.</summary>
        Vector3 LeftKneePoleLocal { get; }

        /// <summary>Character-local offset from the right upper leg for the knee pole direction.</summary>
        Vector3 RightKneePoleLocal { get; }

        /// <summary>Seconds to lerp IK weights on activation/deactivation. Zero means instant.</summary>
        float IKLerpDuration { get; }
    }
}
