using System.Text;
using MB13.Aipocalypse.Core.Debugging;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Dev readout of the player's speed and every part that sets it (player.speed on), two screen-log lines a frame.
    public sealed class MovementReadout
    {
        readonly ScreenLog _log;
        readonly StringBuilder _text = new();

        public MovementReadout(ScreenLog log) => _log = log;

        public void Show(PlayerMotor motor, HeldItemDriver held, Fatigue fatigue, PlayerFooting footing)
        {
            string gait = !motor.IsGrounded ? "Air" : motor.IsSprinting ? "Sprint" : motor.IsCrouching ? "Crouch" : "Walk";
            float gaitSpeed = motor.IsSprinting ? motor.SprintSpeed : motor.IsCrouching ? motor.CrouchSpeed : motor.WalkSpeed;
            _text.Clear().Append("Speed ").Append(motor.HorizontalVelocity.magnitude.ToString("0.00"))
                .Append(" → ").Append(motor.TargetSpeed.ToString("0.00")).Append(" m/s   ")
                .Append(gait).Append(' ').Append(gaitSpeed.ToString("0.00"));
            _log.Show("movement.speed", _text.ToString());

            _text.Clear().Append("Direction ×").Append(motor.DirectionShare.ToString("0.00"))
                .Append("  Ground ×").Append(motor.GroundMultiplier.ToString("0.00"));
            if (footing != null && footing.WaterDepth > 0f)
                _text.Append(" (water ").Append(footing.WaterDepth.ToString("0.00")).Append(" m)");
            if (held != null)
                _text.Append("  Mobility ×").Append(held.Mobility.ToString("0.00"))
                    .Append("  Aim ×").Append(held.AimSpeed.ToString("0.00"))
                    .Append("  Use ×").Append(held.UseSpeed.ToString("0.00"));
            _text.Append("  Fatigue ×").Append(fatigue.MoveSpeed.ToString("0.00"))
                .Append("  Accel ×").Append(motor.AccelerationMultiplier.ToString("0.00"))
                .Append("  Sprint cost ×").Append(motor.SprintCostMultiplier.ToString("0.00"));
            _log.Show("movement.parts", _text.ToString());
        }
    }
}
