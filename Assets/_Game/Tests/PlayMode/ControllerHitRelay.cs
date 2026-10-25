using Aipocalypse.Core.PlayerSystem;
using UnityEngine;

namespace Aipocalypse.Tests
{
    // Test stand-in for Player.OnControllerColliderHit, used by PlayerMotorTests. Edit mode only sends the message to
    // [ExecuteAlways] components, and only a non-editor assembly's component can be added to a GameObject, hence this assembly.
    [ExecuteAlways]
    public sealed class ControllerHitRelay : MonoBehaviour
    {
        public PlayerMotor Motor { get; set; }

        void OnControllerColliderHit(ControllerColliderHit hit) => Motor?.OnControllerHit(hit);
    }
}
