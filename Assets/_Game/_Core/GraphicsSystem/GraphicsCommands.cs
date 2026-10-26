using Aipocalypse.Core.Debugging;

namespace Aipocalypse.Core.GraphicsSystem
{
    // Console commands on the player's graphics settings (editor and development builds).
    [CommandGroup("gfx")]
    public sealed class GraphicsCommands : ConsoleCommands
    {
        readonly GraphicsService _graphics;

        public GraphicsCommands(CommandRegistry registry, GraphicsService graphics) : base(registry) => _graphics = graphics;

        [Command("Shadow distance setting in metres (the night and other requests can shorten what's drawn)")]
        float Shadows
        {
            get => _graphics.ShadowDistance;
            set => _graphics.ShadowDistance = value;
        }

        [Command("Twinkling stars in the night sky")]
        bool StarTwinkle
        {
            get => _graphics.StarTwinkle;
            set => _graphics.StarTwinkle = value;
        }
    }
}
