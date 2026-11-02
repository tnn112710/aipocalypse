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
        // Interact held long enough (its Hold interaction completed) this frame, and let go this frame.
        public bool InteractHold;
        public bool InteractReleased;
        public bool Drop;
        public bool Fire;
        public bool FirePressed;
        public bool Aim;
        public bool Reload;
    }
}
