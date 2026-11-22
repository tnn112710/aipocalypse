using MB13.Aipocalypse.Core.EnvironmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Brightens the robot's face screen at night (set its source to Darkness), ticked by the scene's EnvironmentDirector.
    public sealed class RobotFaceGlow : TimeOfDayReactor
    {
        [SerializeField] RobotFace _face;

        protected override void Apply(float weight)
        {
            if (_face != null)
                _face.SetNight(weight);
        }

        public void Setup(RobotFace face) => _face = face;
    }
}
