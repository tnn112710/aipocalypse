using UnityEngine;

namespace Aipocalypse.Core.Interaction
{
    // Something mended a knock at a time (fence segments, gates). The repairer plays the work; each knock calls Knock.
    public interface IRepairable
    {
        bool NeedsRepair { get; }
        // Where the knocks land, seen from `from` (sounds, dust).
        Vector3 RepairPoint(Vector3 from);
        // One knock of repair. False once it can take no more: whole again, or something stands where it would rebuild.
        bool Knock(GameObject actor);
    }

    // Who can repair (the player): starts its repair work on `target` (put the item away, hammer until whole or stopped).
    public interface IRepairer
    {
        bool IsRepairing { get; }
        bool TryStartRepair(IRepairable target);
    }
}
