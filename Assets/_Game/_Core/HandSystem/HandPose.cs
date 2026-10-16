using System;
using UnityEngine;

namespace Aipocalypse.Core.HandSystem
{
    public enum HandSide
    {
        Left,
        Right,
    }

    public enum Finger
    {
        Thumb,
        Index,
        Middle,
        Ring,
        Little,
    }

    // One finger's shape as blend weights, so a pose works on any hand rig and on either side.
    // Each rig maps them to its own bones through its Open / Closed / Spread calibration (HandRig).
    [Serializable]
    public struct FingerShape
    {
        // Past 1 the joints keep turning beyond Closed (a handle grip) into a tight fist.
        public const float MaxCurl = 1.8f;

        [Range(0f, MaxCurl), Tooltip("0 = straight (Open), 1 = curled (Closed); above 1 over-curls past Closed into a fist")] public float Curl;
        [Range(-1f, 1f), Tooltip("Towards the rig's Spread shape (thumb: away from the index). Negative goes the other way")] public float Spread;
        [Range(-1f, 1f), Tooltip("Added to Curl for the outer joints only: negative keeps the finger straight past the knuckle (flat on a side), positive hooks the tip")]
        public float Tip;

        public FingerShape(float curl, float spread = 0f, float tip = 0f)
        {
            Curl = curl;
            Spread = spread;
            Tip = tip;
        }

        // Curl of joint `index` along the chain (0 = base): the first two take Curl, the rest Curl + Tip.
        public float JointCurl(int index) => index < 2 ? Curl : Mathf.Clamp(Curl + Tip, 0f, Mathf.Max(1f, Curl));

        public static FingerShape Lerp(FingerShape a, FingerShape b, float t) =>
            new(Mathf.Lerp(a.Curl, b.Curl, t), Mathf.Lerp(a.Spread, b.Spread, t), Mathf.Lerp(a.Tip, b.Tip, t));
    }

    [Serializable]
    public struct HandShape
    {
        public const int FingerCount = 5;

        public FingerShape Thumb;
        public FingerShape Index;
        public FingerShape Middle;
        public FingerShape Ring;
        public FingerShape Little;

        public FingerShape this[int finger] => finger switch
        {
            0 => Thumb,
            1 => Index,
            2 => Middle,
            3 => Ring,
            _ => Little,
        };

        public static HandShape Lerp(in HandShape a, in HandShape b, float t) => new()
        {
            Thumb = FingerShape.Lerp(a.Thumb, b.Thumb, t),
            Index = FingerShape.Lerp(a.Index, b.Index, t),
            Middle = FingerShape.Lerp(a.Middle, b.Middle, t),
            Ring = FingerShape.Lerp(a.Ring, b.Ring, t),
            Little = FingerShape.Lerp(a.Little, b.Little, t),
        };
    }

    // A reusable finger pose (rifle grip, forend, pinch…), shared by every item, hand mesh and side.
    [CreateAssetMenu(menuName = "Aipocalypse/Hands/Hand Pose", fileName = "HandPose")]
    public sealed class HandPose : ScriptableObject
    {
        [SerializeField] HandShape _shape;

        public HandShape Shape => _shape;

        // Fires in the editor when the pose is edited, so previews and live hands refresh.
        public static event Action<HandPose> Edited;

        public void SetShape(HandShape shape) => _shape = shape;

        void OnValidate() => Edited?.Invoke(this);
    }
}
