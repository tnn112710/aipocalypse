using UnityEngine;

namespace MB13.Aipocalypse.Core.Ghosts
{
    // Fits / can be done (Valid) or something is in the way (Invalid).
    public enum GhostState
    {
        Valid,
        Invalid,
    }

    // How ghosts look: a see-through material per state (Aipocalypse/Ghosts/Ghost), and a depth-only pass drawn first so only
    // the ghost's nearest surfaces show, not the faces inside it.
    [CreateAssetMenu(menuName = "Aipocalypse/Ghosts/Ghost Style", fileName = "GhostStyle")]
    public sealed class GhostStyle : ScriptableObject
    {
        public Material Valid;
        public Material Invalid;
        [Tooltip("Optional: the ghost shader with zero alpha, at a render queue below the looks'")]
        public Material DepthOnly;

        public Material LookFor(GhostState state) => state == GhostState.Invalid ? Invalid : Valid;
    }
}
