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

        [Command("Set the dog's stance: active (joins fights) or passive (only when sent)")]
        string Stance(string stance)
        {
            Dog dog = _roster.Dog;
            if (dog == null)
                return "No dog.";
            dog.State.SetStance(stance?.ToLowerInvariant() == "passive" ? DogStance.Passive : DogStance.Active);
            return "Stance " + dog.State.Stance;
        }

        [Command("Set the dog's toughness (0 knocks it down)")]
        string Toughness(float value)
        {
            Dog dog = _roster.Dog;
            if (dog == null)
                return "No dog.";
            float now = Time.time;
            if (value <= 0f)
                dog.State.Hurt(float.MaxValue, now, _manager.Stats);
            else
                dog.State.RestoreFight(dog.State.Stance, value, false, dog.State.LimpLeft(now), now);
            return $"Toughness {dog.State.Toughness:0}{(dog.State.Down ? " (down)" : "")}";
        }

        [Command("Knock the dog down")]
        string Down() => Toughness(0f);

        [Command("Help the dog up (as hold E)")]
        string Up()
        {
            Dog dog = _roster.Dog;
            if (dog == null || !dog.State.Down)
                return "Not down.";
            _manager.HelpUp(dog);
            return "Up, limping.";
        }

        sealed class TakedownCheat : IDogUpgradeSource
        {
            public float Base, Bonus;
            public float Modifier(DogStat stat) => stat == DogStat.TakedownChanceBase ? Base : stat == DogStat.TakedownChanceBonus ? Bonus : 0f;
        }

        readonly TakedownCheat _takedown = new();

        [Command("Takedown chance on top of the stats: base, bonus × missing health (dog.takedown 0.05 0.25; 0 0 to clear)")]
        string Takedown(float chance, float bonus = 0f)
        {
            _takedown.Base = chance;
            _takedown.Bonus = bonus;
            if (chance == 0f && bonus == 0f)
                _manager.Stats.Remove(_takedown);
            else
                _manager.Stats.Add(_takedown);
            return $"Takedown chance now {DogState.TakedownChance(1f, _manager.Stats):0.00} (fresh enemy) to {DogState.TakedownChance(0f, _manager.Stats):0.00} (dying)";
        }

        [Command("At home: run at the sheep now")]
        string Chase()
        {
            Dog dog = _roster.Dog;
            if (dog == null)
                return "No dog.";
            dog.State.ForceSheepChase();
            return "Next: a sheep chase.";
        }

        [Command("Howl now")]
        string Howl()
        {
            Dog dog = _roster.Dog;
            if (dog == null)
                return "No dog.";
            dog.Act(DogAct.Howl);
            _manager.TryPlay(dog, DogSound.Howl);
            return "Awooo.";
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

        // Switched off by `dog.active off` (it leaves the roster while off, so it's kept here to switch back on).
        GameObject _switchedOff;

        [Command("Switch the dog off or back on (to measure what it costs)")]
        string Active(bool on)
        {
            if (!on)
            {
                if (_roster.Dog == null)
                    return "No dog.";
                _switchedOff = _roster.Dog.gameObject;
                _switchedOff.SetActive(false);
                return $"{_switchedOff.name} off.";
            }
            if (_switchedOff == null)
                return "Nothing switched off.";
            _switchedOff.SetActive(true);
            string name = _switchedOff.name;
            _switchedOff = null;
            return $"{name} on.";
        }
}
}
