using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // What the player wants this frame. Built from input by Player; tests or cutscenes can build it directly.
    public struct PlayerIntent
    {
        public Vector2 Move;
        public Vector2 LookDegrees;
        public bool Sprint;
        public bool Jump;
        // Crouch pressed this frame (toggles).
        public bool Crouch;
        public int? SelectSlot;
        public int CycleSlot;
        public bool Interact;
        // Interact held long enough (its Hold interaction completed) this frame, and let go this frame.
        public bool InteractHold;
        public bool InteractReleased;
        // The Collect key (C) pressed this frame: what the focus offers to collect.
        public bool Collect;
        public bool Drop;
        public bool Fire;
        public bool FirePressed;
        public bool Aim;
        public bool Reload;
        // The held gun's light switched on or off this frame.
        public bool GunLight;
        // The whistle key pressed this frame: calls what the player aims at.
        public bool Whistle;
    }
}
