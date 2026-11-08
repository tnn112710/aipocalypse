using UnityEngine;

namespace MB13.Aipocalypse.Core.Interaction
{
    // Something mended a knock at a time (fence segments, gates). The repairer plays the work; each knock calls Knock.
    public interface IRepairable
    {
        bool NeedsRepair { get; }
        // What the repair tool's key would do ("Repair Fence 63%"); false when there's nothing to do.
        bool TryGetRepairPrompt(GameObject actor, out InteractionPrompt prompt);
        // Where the knocks land, seen from `from` (sounds, dust).
        Vector3 RepairPoint(Vector3 from);
        // One knock of repair. False once it can take no more: whole again, or something stands where it would rebuild.
        bool Knock(GameObject actor);
    }

    // Who can repair (the player): with a repair tool in hand, its key knocks on the IRepairable in view until whole or stopped.
    public interface IRepairer
    {
        // A repair tool is in the hands (raising included).
        bool HasTool { get; }
        bool IsRepairing { get; }
    }
}
