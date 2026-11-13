using System.Linq;
using MB13.Aipocalypse.Core.Debugging;

namespace MB13.Aipocalypse.Core.LocalizationSystem
{
    // Console commands on the game's language (editor and cheat builds).
    [CommandGroup("lang")]
    public sealed class LanguageCommands : ConsoleCommands
    {
        readonly LanguageService _language;

        public LanguageCommands(CommandRegistry registry, LanguageService language) : base(registry) => _language = language;

        [Command("Every language (code and name); * marks the current one")]
        string List() => string.Join("\n", _language.Languages.Select(l =>
            $"{(l == _language.Current ? "*" : " ")} {l.Identifier.Code}  {_language.NameOf(l)}"));

        [Command("Switch to the language with this code (en, vi, fr); kept for the next launch")]
        CommandResult Set(string code) =>
            _language.Select(code) ? CommandResult.Ok($"Language: {_language.NameOf(_language.Current)}") : CommandResult.Fail($"No language '{code}'");

        [Command("Switch to the next language")]
        string Next()
        {
            _language.Step(1);
            return $"Language: {_language.NameOf(_language.Current)}";
        }
    }
}
