using UnityEngine;

namespace MB13.Aipocalypse.Core.Interaction
{
    // Something the player can call from afar by aiming at it and whistling (a robot). `actor` is the whistler's root.
    public interface IWhistleTarget
    {
        // What aiming at it shows ("[Q] Call Bolt"); false: no prompt.
        bool TryGetWhistlePrompt(GameObject actor, out InteractionPrompt prompt);
        // True when it answers the call.
        bool TryWhistle(GameObject actor);
    }
}
