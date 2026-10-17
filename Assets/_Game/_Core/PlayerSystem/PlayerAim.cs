using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Turns the aim sway into what's drawn: where the crosshair sits and how wide the spread cone is on screen
    // (world camera), and how far the held model turns to point there (weapon camera). Screen values are in units of
    // half the screen height, x = right, y = up.
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
            _rig.SetAimRotation(ModelRotation(aim, _zoom.LookScale));
        }

        // A direction in the eye's space, projected onto the screen.
        public static Vector2 ScreenPosition(Vector3 aim, float tanHalfFov) => new Vector2(aim.x, aim.y) / (aim.z * tanHalfFov);

        public static float ScreenRadius(float degrees, float tanHalfFov) =>
            Mathf.Tan(Mathf.Clamp(degrees, 0f, MaxSpreadDegrees) * Mathf.Deg2Rad) / tanHalfFov;

        // The weapon camera keeps the unzoomed field of view while the world zooms, so the same angle lands on different
        // pixels in each. Turning the model further by the zoom keeps its sights on the crosshair's pixel.
        public static Quaternion ModelRotation(Vector3 aim, float zoomScale)
        {
            float scale = Mathf.Max(zoomScale, 0.01f);
            return Quaternion.FromToRotation(Vector3.forward, new Vector3(aim.x / scale, aim.y / scale, aim.z));
        }
    }
}
