using System.Text;
using MB13.Aipocalypse.Core.Debugging;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // Console commands for the scene's campfires (editor and cheat builds), on the home fire (the first one) and wood piles.
    // Wood itself comes from cheat.give wood 4.
    [CommandGroup("campfire")]
    public sealed class CampfireCommands : ConsoleCommands
    {
        static readonly CommandResult NoFire = CommandResult.Fail("No campfire in this scene");

        readonly CampfireRegistry _campfires;

        public CampfireCommands(CommandRegistry registry, CampfireRegistry campfires) : base(registry) => _campfires = campfires;

        [Command("Each campfire's fuel and whether it burns, and each wood pile's logs")]
        CommandResult Status()
        {
            if (_campfires.Fires.Count == 0)
                return NoFire;
            var log = new StringBuilder();
            foreach (Campfire fire in _campfires.Fires)
                log.Append($"{fire.name}: {fire.Fuel.Fuel:0.0} / {fire.Fuel.Max:0.#} h, {(fire.Fuel.Lit ? "lit" : "out")}, strength {fire.Fuel.Strength:0.##}\n");
            foreach (WoodPile pile in _campfires.Piles)
                log.Append($"{pile.name}: {pile.Stock} logs\n");
            return CommandResult.Ok(log.ToString().TrimEnd());
        }

        [Command("Set the home fire's fuel in game hours (keeps it lit or out): campfire.fuel 1.5")]
        CommandResult Fuel(float hours)
        {
            if (_campfires.Home is not { } fire)
                return NoFire;
            fire.RestoreSaved(hours, fire.Fuel.Lit);
            return Status();
        }

        [Command("Light the home fire (needs fuel)")]
        CommandResult Light()
        {
            if (_campfires.Home is not { } fire)
                return NoFire;
            return fire.Fuel.Light() ? Status() : CommandResult.Fail("No fuel: campfire.fuel 8 first");
        }

        [Command("Put the home fire out (the fuel stays)")]
        CommandResult Out()
        {
            if (_campfires.Home is not { } fire)
                return NoFire;
            fire.PutOut();
            return Status();
        }

        [Command("Set the logs in every wood pile: campfire.pile 8")]
        CommandResult Pile(int logs)
        {
            if (_campfires.Piles.Count == 0)
                return CommandResult.Fail("No wood pile in this scene");
            foreach (WoodPile pile in _campfires.Piles)
                pile.SetStock(logs);
            return Status();
        }
    }
}
