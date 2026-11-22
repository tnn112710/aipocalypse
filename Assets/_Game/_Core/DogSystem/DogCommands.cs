using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Console commands on the scene's dog (editor and cheat builds); only in scenes with a DogInstaller.
    [CommandGroup("dog")]
    public sealed class DogCommands : ConsoleCommands
    {
        readonly DogRoster _roster;
        readonly DogManager _manager;
        readonly PlayerRegistry _players;

        public DogCommands(CommandRegistry registry, DogRoster roster, DogManager manager, PlayerRegistry players) : base(registry)
        {
            _roster = roster;
            _manager = manager;
            _players = players;
        }

        [Command("Call the dog to you as if whistled")]
        string Come()
        {
            Dog dog = _roster.Dog;
            IPlayer player = _players.Current;
            if (dog == null || player == null)
                return "No dog or no player.";
            _manager.Whistled(dog, player.Transform.gameObject);
            return $"{dog.name} is coming.";
        }

        [Command("Give the dog an order: follow or stay (stays where it is)")]
        string Order(string order)
        {
            Dog dog = _roster.Dog;
            if (dog == null)
                return "No dog.";
            switch (order?.ToLowerInvariant())
            {
                case "follow":
                    _manager.Order(dog, DogOrder.Follow);
                    return "Following.";
                case "stay":
                    _manager.Order(dog, DogOrder.Stay);
                    return "Staying.";
                default:
                    return "Orders: follow, stay.";
            }
        }

        [Command("Where the dog is and what it's doing")]
        string Where()
        {
            Dog dog = _roster.Dog;
            if (dog == null)
                return "No dog.";
            float now = Time.time;
            DogState state = dog.State;
            string doing = state.Called(now) ? "coming" : state.Petting(now) ? "being petted" : state.Sad(now) ? "sad (told to stay)" :
                state.Order == DogOrder.Follow ? "following (" + dog.Plan.Mode + ")" : _roster.InYard(state.StayPoint) ? "at home" : "staying";
            float fromPlayer = _roster.HasPlayer ? Vector3.Distance(dog.transform.position, _roster.PlayerPosition) : -1f;
            return $"{dog.name}: {doing}, pose {dog.Pose}, {fromPlayer:0.0} m from you, at {dog.transform.position}";
        }
    }
}
