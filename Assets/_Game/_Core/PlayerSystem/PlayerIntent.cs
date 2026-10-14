using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // What the player wants this frame. Built from input by Player; tests or cutscenes can build it directly.
    public struct PlayerIntent
    {
        public Vector2 Move;
        public Vector2 LookDegrees;
        public bool Sprint;
        public bool Jump;
        public int? SelectSlot;
        public int CycleSlot;
        public bool Interact;
        public bool Drop;
    }
}
