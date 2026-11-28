using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog as a comfort source for the player's sanity: near it the darkness drain is slower. Inactive while it's down or
    // the dog is off. Registered by DogManager in the scene's ComfortSources (when the needs are installed).
    public sealed class DogComfort : IComfortSource
    {
        readonly DogRoster _roster;

        public DogComfort(DogRoster roster) => _roster = roster;

        public Vector3 Position => _roster.Dog != null ? _roster.Dog.transform.position : Vector3.zero;
        public float Radius => _roster.Settings.ComfortRadius;
        public bool IsActive => _roster.Enabled && _roster.Dog != null && _roster.Dog.isActiveAndEnabled && !_roster.Dog.State.Down;
    }
}
