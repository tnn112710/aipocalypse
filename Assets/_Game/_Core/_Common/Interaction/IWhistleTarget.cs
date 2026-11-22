using UnityEngine;

namespace MB13.Aipocalypse.Core.Interaction
{
    // Something the player can call from afar by aiming at it and whistling (a service robot). `actor` is the whistler's root.
    public interface IWhistleTarget
    {
        // True when it answers the call.
        bool TryWhistle(GameObject actor);
    }
}
