using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Where a held model's pose pivot is, easing between pose markers (hip, aim, sprint, idle…): siblings of the pivot that
    // are moved in the prefab to pose it. Shared by every held item that poses its model.
    public sealed class PoseBlender
    {
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;

        public void SnapTo(Transform marker)
        {
            if (marker != null)
                Set(marker.localPosition, marker.localRotation);
        }

        public void Set(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        // Frame-rate independent ease towards the marker (higher speed = faster).
        public void MoveTowards(Transform marker, float speed, float deltaTime)
        {
            if (marker == null)
                return;
            float t = Damping.ByRate(speed, deltaTime);
            Position = Vector3.Lerp(Position, marker.localPosition, t);
            Rotation = Quaternion.Slerp(Rotation, marker.localRotation, t);
        }

        public void ApplyTo(Transform pivot) => pivot.SetLocalPositionAndRotation(Position, Rotation);

#if UNITY_EDITOR
        // Edit-mode preview in a prefab: puts the pivot on a marker, with undo.
        public static void Preview(Transform pivot, Transform marker)
        {
            if (pivot == null || marker == null)
                return;
            UnityEditor.Undo.RecordObject(pivot, "Preview pose");
            pivot.SetLocalPositionAndRotation(marker.localPosition, marker.localRotation);
        }
#endif
    }
}
