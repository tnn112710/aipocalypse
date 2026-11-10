using System.Text;
using MB13.Aipocalypse.Core.Debugging;
using UnityEngine;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Console commands on the save slots (editor and cheat builds). Slots are 1-based; 0 = the current game's slot.
    [CommandGroup("save")]
    public sealed class SaveCommands : ConsoleCommands
    {
        readonly GameSession _session;
        readonly SaveService _saves;

        public SaveCommands(CommandRegistry registry, GameSession session, SaveService saves) : base(registry)
        {
            _session = session;
            _saves = saves;
        }

        [Command("Every slot: day, hour, play time, when it was saved")]
        string List()
        {
            var text = new StringBuilder();
            text.Append($"Current: Save {_session.Slot}, played {SaveFormat.PlayTime(_session.PlayTime)}");
            for (int slot = 1; slot <= _saves.SlotCount; slot++)
                text.Append('\n').Append($"Save {slot}: ").Append(SaveFormat.Summary(_saves.Info(slot)));
            return text.ToString();
        }

        [Command("Save the game now to `slot` (0 = the current slot)")]
        CommandResult Write(int slot = 0) =>
            _session.Save(slot > 0 ? slot : _session.Slot, out string message) ? CommandResult.Ok(message) : CommandResult.Fail(message);

        [Command("Load `slot` (0 = the current slot): the world reloads as it was saved")]
        CommandResult Load(int slot = 0) =>
            _session.Load(slot > 0 ? slot : _session.Slot, out string message) ? CommandResult.Ok(message) : CommandResult.Fail(message);

        [Command("Start a new game in `slot` (0 = the current slot); its old save is overwritten by the first save")]
        CommandResult New(int slot = 0) =>
            _session.NewGame(slot > 0 ? slot : _session.Slot, out string message) ? CommandResult.Ok(message) : CommandResult.Fail(message);

        [Command("Delete the save in `slot`")]
        CommandResult Delete(int slot) =>
            _session.Delete(slot) ? CommandResult.Ok($"Save {slot} deleted") : CommandResult.Fail($"Save {slot} is empty");

        [Command("Delete every slot's save")]
        CommandResult DeleteAll()
        {
            int deleted = 0;
            for (int slot = 1; slot <= _saves.SlotCount; slot++)
                if (_session.Delete(slot))
                    deleted++;
            return CommandResult.Ok($"{deleted} save(s) deleted");
        }

        [Command("Autosave now, as each morning does (waits for a raid or death to end)")]
        CommandResult Auto()
        {
            if (!_session.InWorld)
                return CommandResult.Fail("Not in a game");
            _session.RequestAutosave();
            return CommandResult.Ok($"Autosave to Save {_session.Slot} requested");
        }

        [Command("The save folder (also copied to the clipboard)")]
        string Path()
        {
            GUIUtility.systemCopyBuffer = _saves.FolderPath;
            return _saves.FolderPath;
        }

        [Command("Back to the main menu (unsaved progress is lost)")]
        CommandResult Menu() =>
            _session.ReturnToMainMenu() ? CommandResult.Ok("Loading the main menu") : CommandResult.Fail("No main menu scene in the Build Settings");
    }
}
