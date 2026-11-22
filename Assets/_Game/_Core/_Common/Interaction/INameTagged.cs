using UnityEngine;

namespace MB13.Aipocalypse.Core.Interaction
{
    // Something that shows its name (and an icon for what it does) over itself while the player aims at it: a robot.
    public interface INameTagged
    {
        // False: no tag right now. `point` is where the tag sits in the world (over its head).
        bool TryGetNameTag(out string name, out Sprite icon, out Vector3 point);
    }
}
