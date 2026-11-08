using System.Linq;
using System.Reflection;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.Ghosts;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Play-mode look at the broken-fence ghost in WorldMap (enter Play first): Break stands the player in front of a broken
// segment looking at it, so a Game view capture shows the ghost; Restore mends it. Inside the yard, facing out.
// Run: unity command script run --file AgentScripts/Ghosts/GhostShot.cs --entry GhostShot.Break --args '[2.2, 10]'
public static class GhostShot
{
    static readonly Vector3 YardCentre = new(267f, 0f, 282f);

    public static string Break(float distance = 2.2f, float pitch = 10f)
    {
        if (!Application.isPlaying)
            return "enter play mode in WorldMap first";
        Application.runInBackground = true;
        (BarrierSegment fence, Player player) = Find();
        fence.DealDamage(10000f);
        Vector3 inward = fence.transform.forward * (Vector3.Dot(fence.transform.forward, YardCentre - fence.transform.position) > 0f ? 1f : -1f);
        Vector3 stand = fence.transform.position + inward * distance;
        object motor = typeof(Player).GetField("_motor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
        motor.GetType().GetMethod("Teleport").Invoke(motor, new object[] { stand, Quaternion.LookRotation(-inward) });
        object look = typeof(Player).GetField("_look", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
        look.GetType().GetMethod("ResetPitch")?.Invoke(look, new object[] { pitch });
        bool shown = fence.TryGetGhost(player.gameObject, out GhostView ghost);
        return $"{fence.name} broken {fence.IsBroken}, ghost {shown} ({ghost.State}, {ghost.Shape?.Count} parts)";
    }

    public static string Restore()
    {
        (BarrierSegment fence, _) = Find();
        fence.Restore();
        return $"{fence.name} restored";
    }

    static (BarrierSegment, Player) Find()
    {
        IObjectResolver c = LifetimeScope.Find<GameLifetimeScope>().Container;
        BarrierSegment gate = c.Resolve<BarrierRegistry>().Segments.First(s => s.Gate != null);
        BarrierSegment fence = c.Resolve<BarrierRegistry>().Segments.Where(s => s.Gate == null)
            .OrderBy(s => (s.transform.position - gate.transform.position).sqrMagnitude).Skip(2).First();
        return (fence, (Player)c.Resolve<PlayerRegistry>().Current);
    }
}
