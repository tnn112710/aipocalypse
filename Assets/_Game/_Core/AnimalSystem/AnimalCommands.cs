using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.PlayerSystem;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Console commands on the scene's farm animals (editor and cheat builds); only in scenes with an AnimalInstaller.
    [CommandGroup("animal")]
    public sealed class AnimalCommands : ConsoleCommands
    {
        readonly AnimalHerd _herd;
        readonly AnimalManager _manager;
        readonly PlayerRegistry _players;

        public AnimalCommands(CommandRegistry registry, AnimalHerd herd, AnimalManager manager, PlayerRegistry players) : base(registry)
        {
            _herd = herd;
            _manager = manager;
            _players = players;
        }

        [Command("The herd: how many, how many home, its orders, the Tokens held")]
        string Status()
        {
            float held = 0f;
            foreach (Animal animal in _herd.Animals)
                held += animal.Earnings.Held;
            HerdOrders orders = _herd.Orders;
            string state = orders.Released ? "released" : orders.GateOpen ? "called home" : "penned";
            return $"{_herd.Animals.Count} animals, {_herd.CountHome()} home, {state}{(_herd.Asleep ? ", asleep" : "")}, {held:0.#} Tokens held";
        }

        [Command("Open the pen gate: the herd goes out to graze")]
        string Release()
        {
            PenGate gate = Gate();
            if (gate == null)
                return "No pen gate in this scene.";
            if (!gate.IsOpen)
                gate.Open(gate.transform.position + gate.transform.forward);
            else
                _herd.Orders.OpenGate();
            return "Released.";
        }

        [Command("Ring the bell: the herd comes home")]
        string Call() => _herd.Orders.Call() ? "Called home." : "Nothing is out (the gate is shut, or the bell already rang).";

        [Command("Hurry every animal")]
        string Hurry()
        {
            foreach (Animal animal in _herd.Animals)
                animal.Hurry();
            return "Hurried.";
        }

        [Command("Give every animal this many Tokens to hand over")]
        string Fill(float tokens = 10f)
        {
            foreach (Animal animal in _herd.Animals)
                animal.Earnings.Set(animal.Earnings.Held + tokens);
            return $"Each animal holds {tokens:0.#} more.";
        }

        [Command("Every animal hands its Tokens over now, wherever the player is")]
        string Collect()
        {
            IPlayer player = _players.Current;
            if (player == null)
                return "No player.";
            foreach (Animal animal in _herd.Animals)
                _manager.HandOver(animal, player.Transform.position, player.Transform.GetComponent<UnityEngine.Collider>());
            return "Collected.";
        }

        PenGate Gate() => _herd.Pen != null ? _herd.Pen.Gate : null;
    }
}
