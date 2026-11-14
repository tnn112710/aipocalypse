using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Turns the aim sway into what's drawn: crosshair position and spread cone on screen (world camera) and the held model's
    // turn toward it (weapon camera). Screen values are in half-screen-heights, x right, y up.
    public sealed class PlayerAim
    {
        // Wider cones aren't drawn any wider (the tan would run off).
        const float MaxSpreadDegrees = 60f;

        readonly EquipmentRig _rig;
        readonly PlayerZoom _zoom;

        public PlayerAim(EquipmentRig rig, PlayerZoom zoom)
        {
            _rig = rig;
            _zoom = zoom;
        }

        public Vector2 CrosshairPosition { get; private set; }
        public float CrosshairSpread { get; private set; }

        // After the zoom ticks: the screen mapping and the model's correction depend on this frame's field of view.
        public void Tick(Quaternion aimOffset, float spreadDegrees)
        {
            Vector3 aim = aimOffset * Vector3.forward;
            float tanHalfFov = _zoom.TanHalfFov;
            CrosshairPosition = ScreenPosition(aim, tanHalfFov);
            CrosshairSpread = ScreenRadius(spreadDegrees, tanHalfFov);
            _rig.SetAimRotation(ModelRotation(aim, _zoom.ModelScale));
        }

        // A direction in the eye's space, projected onto the screen.
        public static Vector2 ScreenPosition(Vector3 aim, float tanHalfFov) => new Vector2(aim.x, aim.y) / (aim.z * tanHalfFov);

        public static float ScreenRadius(float degrees, float tanHalfFov) =>
            Mathf.Tan(Mathf.Clamp(degrees, 0f, MaxSpreadDegrees) * Mathf.Deg2Rad) / tanHalfFov;

        // The weapon camera's field of view can differ from the world's (it skips the shot punch and sprint, and may take
        // less of the aim zoom), so the same angle lands on different pixels in each. `zoomScale` = world tan / held tan;
        // turning the model by it keeps its sights on the crosshair's pixel.
        public static Quaternion ModelRotation(Vector3 aim, float zoomScale)
        {
            float scale = Mathf.Max(zoomScale, 0.01f);
            return Quaternion.FromToRotation(Vector3.forward, new Vector3(aim.x / scale, aim.y / scale, aim.z));
        }
    }
}
