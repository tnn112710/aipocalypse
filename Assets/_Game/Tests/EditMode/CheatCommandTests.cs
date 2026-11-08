using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.PlayerSystem;
using MessagePipe;
using NUnit.Framework;
using VContainer;

namespace MB13.Aipocalypse.Tests
{
    public class CheatCommandTests
    {
        [Test]
        public void PlayerCheats_LiveInTheCheatGroup_AndPosStaysUnderPlayer()
        {
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.Register<PlayerRegistry>(Lifetime.Singleton);
            builder.Register<DamageService>(Lifetime.Singleton);
            IObjectResolver resolver = builder.Build();
            var registry = new CommandRegistry();
            var cheats = new PlayerCheatCommands(registry, resolver.Resolve<PlayerRegistry>(), resolver.Resolve<DamageService>());
            var player = new PlayerCommands(registry, resolver.Resolve<PlayerRegistry>());
            try
            {
                cheats.Initialize();
                player.Initialize();
                foreach (string name in new[] { "cheat.kill", "cheat.revive", "cheat.hp", "cheat.stamina", "cheat.ammo", "cheat.infammo", "cheat.god", "cheat.notarget", "player.pos" })
                    Assert.IsTrue(registry.TryGet(name, out _), $"{name} is missing");
                foreach (string name in new[] { "player.kill", "player.heal", "player.ammo", "player.infammo" })
                    Assert.IsFalse(registry.TryGet(name, out _), $"{name} should have moved to the cheat group");

                CommandResult god = registry.Execute("cheat.god on");
                Assert.IsFalse(god.Succeeded, "no player in the scene");
                Assert.AreEqual("No player in this scene", god.Message);
            }
            finally
            {
                cheats.Dispose();
                player.Dispose();
                resolver.Dispose();
            }
            Assert.IsFalse(registry.TryGet("cheat.god", out _), "its commands are gone with it");
        }
    }
}
