using MB13.Aipocalypse.Core.Interaction;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // Test stand-in for something that answers the player's whistle (RobotTests). Only a non-editor assembly's component can
    // be added to a GameObject in edit mode, hence this assembly.
    public sealed class WhistleTargetStub : MonoBehaviour, IWhistleTarget
    {
        public int Calls { get; private set; }
        public bool Answers { get; set; } = true;

        public bool TryWhistle(GameObject actor)
        {
            Calls++;
            return Answers;
        }
    }
}
