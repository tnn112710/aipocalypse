using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.TimeSystem;
using Aipocalypse.Core.WorldTimeSystem;
using Aipocalypse.Input;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

namespace Aipocalypse.Tests
{
    // Dev console: parsing, the command registry, the input line, history, scrollback, screen messages, the text input
    // mode and the commands systems register.
    public class ConsoleTests
    {
        enum Mood
        {
            Calm,
            Angry,
            Anxious,
        }

        [CommandGroup("test")]
        sealed class TestCommands
        {
            public string LastText;
            public Mood LastMood;
            public Vector3 LastPoint;

            [Command("Adds two numbers")]
            int Add(int a, int b = 2) => a + b;

            [Command]
            void Say(string text) => LastText = text;

            [Command]
            void Feel(Mood mood) => LastMood = mood;

            [Command]
            void Point(Vector3 point) => LastPoint = point;

            [Command]
            public float Volume { get; set; } = 0.5f;

            [Command]
            void Boom() => throw new InvalidOperationException("boom");

            [Command(Name = "shout")]
            string Loud(string text) => text.ToUpperInvariant();
        }

        CommandRegistry _registry;
        TestCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _registry = new CommandRegistry();
            _commands = new TestCommands();
            _registry.Add(_commands);
        }

        [Test]
        public void Parser_TokenizesWords_KeepsQuotedSpaces_AndEscapes()
        {
            var tokens = new List<string>();
            CommandParser.Tokenize("say  \"hello world\" next \"a\\\"b\" \"\"", tokens);
            CollectionAssert.AreEqual(new[] { "say", "hello world", "next", "a\"b", "" }, tokens);
        }

        [Test]
        public void Parser_SplitsStatementsAtSemicolonsOutsideQuotes()
        {
            var statements = new List<string>();
            CommandParser.SplitStatements("a 1; say \"x;y\" ;; c", statements);
            CollectionAssert.AreEqual(new[] { "a 1", "say \"x;y\"", "c" }, statements);
            Assert.AreEqual(2, CommandParser.LastStatementStart("a; b"));
        }

        [Test]
        public void Parser_ReadsInvariantNumbers_OnOff_EnumPrefixes_Vectors_AndColors()
        {
            Assert.IsTrue(CommandParser.TryParse("1.5", typeof(float), out object number));
            Assert.AreEqual(1.5f, number);
            Assert.IsFalse(CommandParser.TryParse("abc", typeof(int), out _));
            Assert.IsTrue(CommandParser.TryParse("ON", typeof(bool), out object flag));
            Assert.AreEqual(true, flag);
            Assert.IsTrue(CommandParser.TryParse("ang", typeof(Mood), out object mood));
            Assert.AreEqual(Mood.Angry, mood);
            Assert.IsFalse(CommandParser.TryParse("an", typeof(Mood), out _), "a prefix of two names is ambiguous");
            Assert.IsTrue(CommandParser.TryParse("(1, 2.5, -3)", typeof(Vector3), out object point));
            Assert.AreEqual(new Vector3(1f, 2.5f, -3f), point);
            Assert.IsTrue(CommandParser.TryParse("#ff0000", typeof(Color), out object color));
            Assert.AreEqual(Color.red, color);
        }

        [Test]
        public void Registry_RunsAMethod_WithOptionalArguments_IgnoringCase()
        {
            Assert.AreEqual("5", _registry.Execute("test.add 3").Message);
            Assert.AreEqual("7", _registry.Execute("test.add 3 4").Message);
            Assert.AreEqual("3", _registry.Execute("TEST.ADD 1").Message);
            Assert.IsTrue(_registry.Execute("test.say \"two words\"").Succeeded);
            Assert.AreEqual("two words", _commands.LastText);
            _registry.Execute("test.feel anx");
            Assert.AreEqual(Mood.Anxious, _commands.LastMood);
            _registry.Execute("test.point 1,2,3");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), _commands.LastPoint);
            Assert.AreEqual("HI", _registry.Execute("shout hi").Message, "an explicit name replaces group + member");
        }

        [Test]
        public void Registry_PropertyReadsWithNoArgument_AndSetsWithOne()
        {
            Assert.AreEqual("0.5", _registry.Execute("test.volume").Message);
            Assert.AreEqual("0.25", _registry.Execute("test.volume 0.25").Message);
            Assert.AreEqual(0.25f, _commands.Volume);
        }

        [Test]
        public void Registry_ReportsUnknownNames_BadArguments_AndCommandsThatThrow()
        {
            CommandResult typo = _registry.Execute("test.ad 1");
            Assert.IsFalse(typo.Succeeded);
            StringAssert.Contains("test.add", typo.Message);

            CommandResult bad = _registry.Execute("test.add x");
            Assert.IsFalse(bad.Succeeded);
            StringAssert.Contains("'x' is not a valid a", bad.Message);

            CommandResult tooMany = _registry.Execute("test.add 1 2 3");
            Assert.IsFalse(tooMany.Succeeded);
            StringAssert.Contains("test.add <a:int> [b:int=2]", tooMany.Message, "the usage is shown");

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            CommandResult thrown = _registry.Execute("test.boom");
            Assert.IsFalse(thrown.Succeeded);
            StringAssert.Contains("boom", thrown.Message);
        }

        [Test]
        public void Registry_Remove_DropsTheProvidersCommands()
        {
            _registry.Remove(_commands);
            Assert.IsFalse(_registry.Execute("test.add 1").Succeeded);
            Assert.AreEqual(0, _registry.Names.Count);
        }

        [Test]
        public void Registry_SuggestsNames_ThenArgumentValues_ForTheLastStatement()
        {
            var suggestions = new List<string>();
            Assert.AreEqual(0, _registry.Suggest("test.v", suggestions));
            CollectionAssert.AreEqual(new[] { "test.volume" }, suggestions);

            _registry.Suggest("lum", suggestions);
            CollectionAssert.AreEqual(new[] { "test.volume" }, suggestions, "a name containing the text matches too");

            Assert.AreEqual(10, _registry.Suggest("test.feel a", suggestions));
            CollectionAssert.AreEqual(new[] { "Angry", "Anxious" }, suggestions);

            _registry.Suggest("test.feel ", suggestions);
            Assert.AreEqual(3, suggestions.Count);

            Assert.AreEqual(10, _registry.Suggest("test.add; test.s", suggestions));
            CollectionAssert.AreEqual(new[] { "test.say" }, suggestions);

            _registry.Suggest("", suggestions);
            Assert.AreEqual(0, suggestions.Count);

            _registry.Groups(suggestions);
            CollectionAssert.AreEqual(new[] { "shout", "test" }, suggestions, "each group once; a top-level name is its own group");
        }

        [Test]
        public void Help_ListsEveryCommand_AGroup_OrOneCommandsUsage()
        {
            using var logs = new LogBuffer(4);
            var builtIns = new BuiltInCommands(_registry, logs, new ScreenLog(true));
            builtIns.Initialize();

            StringAssert.Contains("shout", _registry.Execute("help").Message);
            CommandResult group = _registry.Execute("help test");
            StringAssert.Contains("test.volume", group.Message);
            StringAssert.DoesNotContain("shout", group.Message);
            StringAssert.StartsWith("test.add <a:int>", _registry.Execute("help test.add").Message);
            Assert.IsFalse(_registry.Execute("help clock").Succeeded);

            logs.Add("line", ConsoleEntryKind.Output);
            _registry.Execute("clear");
            Assert.AreEqual(0, logs.Count);
            builtIns.Dispose();
        }

        [Test]
        public void ConsoleLine_EditsAtTheCaret_AndCtrlJumpsWholeWords()
        {
            var line = new ConsoleLine();
            line.Set("clock.set 21");
            line.MoveLeft(true);
            Assert.AreEqual(10, line.Caret);
            line.Backspace(true);
            Assert.AreEqual("clock.21", line.Text);
            line.Insert("set ");
            Assert.AreEqual("clock.set 21", line.Text);
            line.Delete(false);
            Assert.AreEqual("clock.set 1", line.Text);
            line.Home();
            line.Delete(true);
            Assert.AreEqual(".set 1", line.Text);
            line.End();
            Assert.AreEqual(6, line.Caret);
        }

        [Test]
        public void ConsoleHistory_BrowsesLikeAShell_AndGivesTheDraftBack()
        {
            var history = new ConsoleHistory(10);
            history.Add("a");
            history.Add("b");
            history.Add("b");
            history.Add("c");
            Assert.AreEqual(3, history.Lines.Count, "a repeat of the newest line isn't stored twice");

            Assert.IsTrue(history.TryPrevious("draft", out string line));
            Assert.AreEqual("c", line);
            history.TryPrevious("c", out line);
            history.TryPrevious("b", out line);
            Assert.AreEqual("a", line);
            Assert.IsFalse(history.TryPrevious("a", out _));
            history.TryNext(out line);
            history.TryNext(out line);
            Assert.AreEqual("c", line);
            history.TryNext(out line);
            Assert.AreEqual("draft", line);
            Assert.IsFalse(history.TryNext(out _));

            var capped = new ConsoleHistory(2);
            capped.Load(new[] { "x", "y", "z" });
            CollectionAssert.AreEqual(new[] { "y", "z" }, capped.Lines);
        }

        [Test]
        public void LogBuffer_CollapsesRepeats_DropsTheOldest_AndTakesUnityLogsOnFlush()
        {
            using var logs = new LogBuffer(3);
            logs.Add("a", ConsoleEntryKind.Output);
            logs.Add("a", ConsoleEntryKind.Output);
            Assert.AreEqual(1, logs.Count);
            Assert.AreEqual(2, logs[0].Count);
            logs.Add("b", ConsoleEntryKind.Output);
            logs.Add("c", ConsoleEntryKind.Output);
            logs.Add("d", ConsoleEntryKind.Output);
            Assert.AreEqual(3, logs.Count);
            Assert.AreEqual("b", logs[0].Text);
            Assert.AreEqual("d", logs[2].Text);

            var added = new List<ConsoleEntry>();
            Debug.LogWarning("console test warning");
            Assert.AreEqual("d", logs[2].Text, "Unity logs wait for Flush");
            logs.Flush(null, added.Add);
            Assert.AreEqual("console test warning", logs[2].Text);
            Assert.AreEqual(ConsoleEntryKind.Warning, logs[2].Kind);
            Assert.AreEqual(1, added.Count);

            logs.Clear();
            Assert.AreEqual(0, logs.Count);
        }

        [Test]
        public void LogScreen_UnityLogsStayOutUntilALevelIsChosen()
        {
            using var logs = new LogBuffer(16);
            var screen = new ScreenLog(true);
            var builtIns = new BuiltInCommands(_registry, logs, screen);
            builtIns.Initialize();
            void Log()
            {
                Debug.Log("console info");
                Debug.LogWarning("console warning");
                logs.Flush(screen.Mirrors);
            }

            Log();
            Assert.AreEqual(0, logs.Count, "off by default: the console shows only commands");

            _registry.Execute("log.screen.info");
            Log();
            Assert.AreEqual(1, logs.Count);
            Assert.AreEqual(ConsoleEntryKind.Log, logs[0].Kind);

            _registry.Execute("log.screen.all");
            Log();
            Assert.AreEqual(2, logs.Count, "the repeated info line collapses into one");
            Assert.AreEqual(2, logs[0].Count);
            Assert.AreEqual(ConsoleEntryKind.Warning, logs[1].Kind);
            Assert.AreEqual("All", _registry.Execute("log.screen").Message);

            screen.Print("on screen");
            _registry.Execute("log.screen.clear");
            Assert.AreEqual(ScreenLogLevel.Off, screen.MirrorLevel);
            Assert.AreEqual(0, screen.Count);
            builtIns.Dispose();
        }

        [Test]
        public void ScreenLog_KeyedLinesReplaceThemselves_ExpireInRealTime_AndMirrorByLevel()
        {
            var log = new ScreenLog(true);
            log.Show("speed", "1");
            log.Show("speed", "2");
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual("2", log[0].Text);
            log.Print("hello", 1f);
            Assert.AreEqual(2, log.Count);

            log.Advance(0.5f);
            Assert.AreEqual(1, log.Count, "a 0-second line lasts one frame");
            Assert.AreEqual("hello", log[0].Text);
            log.Advance(0.6f);
            Assert.AreEqual(0, log.Count);

            log.MirrorLevel = ScreenLogLevel.Error;
            log.Mirror(ConsoleEntryKind.Warning, "warning", 1f);
            log.Mirror(ConsoleEntryKind.Error, "error", 1f);
            log.Advance(0.9f);
            log.Mirror(ConsoleEntryKind.Error, "error", 1f);
            Assert.AreEqual(1, log.Count, "an error logged every frame stays one line");
            log.Advance(0.5f);
            Assert.AreEqual(1, log.Count, "and its time restarts");

            for (int i = 0; i < ScreenLog.MaxMessages + 5; i++)
                log.Print("spam");
            Assert.AreEqual(ScreenLog.MaxMessages, log.Count);

            var off = new ScreenLog(false);
            off.Print("release build");
            Assert.AreEqual(0, off.Count);
        }

        [Test]
        public void KeyRepeat_FiresOnPress_ThenAfterTheDelay_EveryInterval()
        {
            var repeat = new KeyRepeat();
            Assert.IsTrue(repeat.Step(true, true, 0f, 0.4f, 0.1f));
            Assert.IsFalse(repeat.Step(false, true, 0.3f, 0.4f, 0.1f));
            Assert.IsTrue(repeat.Step(false, true, 0.15f, 0.4f, 0.1f));
            Assert.IsFalse(repeat.Step(false, true, 0.04f, 0.4f, 0.1f));
            Assert.IsTrue(repeat.Step(false, true, 0.02f, 0.4f, 0.1f));
            Assert.IsFalse(repeat.Step(false, false, 1f, 0.4f, 0.1f));
        }

        [Test]
        public void ConsoleService_Run_EchoesTheLine_ThenEachStatementsResult()
        {
            var settings = ScriptableObject.CreateInstance<ConsoleSettings>();
            var input = new InputService(new ControlSettings());
            using var logs = new LogBuffer(16);
            using var time = new TimeService();
            try
            {
                var console = new ConsoleService(input, _registry, logs, new ScreenLog(true), time, settings);
                console.Run("test.add 1; test.nope");
                Assert.AreEqual(3, logs.Count);
                Assert.AreEqual(ConsoleEntryKind.Input, logs[0].Kind);
                Assert.AreEqual("3", logs[1].Text);
                Assert.AreEqual(ConsoleEntryKind.Output, logs[1].Kind);
                Assert.AreEqual(ConsoleEntryKind.Error, logs[2].Kind);
            }
            finally
            {
                input.Dispose();
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void InputService_TextInput_TurnsGameplayUiAndDebugOff_UntilReleased()
        {
            CursorLockMode lockState = Cursor.lockState;
            bool cursorVisible = Cursor.visible;
            var input = new InputService(new ControlSettings());
            object menu = new();
            object console = new();
            try
            {
                input.Initialize();
                Assert.IsTrue(input.GameplayEnabled);
                Assert.IsTrue(Enabled(input, "Debug"));
                Assert.IsTrue(Enabled(input, "Console", "Toggle"), "the console key works while closed");
                Assert.IsFalse(Enabled(input, "Console", "Submit"));

                input.RequestUI(menu);
                input.RequestTextInput(console);
                Assert.IsFalse(input.GameplayEnabled);
                Assert.IsFalse(Enabled(input, "UI"), "typing Enter must not confirm a menu");
                Assert.IsFalse(Enabled(input, "Debug"), "typing k must not kill the player");
                Assert.IsTrue(Enabled(input, "Console", "Submit"));

                input.ReleaseTextInput(console);
                Assert.IsTrue(Enabled(input, "UI"), "the menu's UI map comes back");
                Assert.IsTrue(Enabled(input, "Debug"));
                Assert.IsFalse(Enabled(input, "Console", "Submit"));
                input.ReleaseUI(menu);
                Assert.IsTrue(input.GameplayEnabled);
            }
            finally
            {
                input.Dispose();
                Cursor.lockState = lockState;
                Cursor.visible = cursorVisible;
            }
        }

        [Test]
        public void TimeCommands_ScaleAndPause_AreReleasedOnDispose()
        {
            using var time = new TimeService();
            var commands = new TimeCommands(_registry, time);
            commands.Initialize();

            Assert.AreEqual("0.25", _registry.Execute("time.scale 0.25").Message);
            _registry.Execute("pause");
            Assert.IsTrue(time.IsPaused);

            commands.Dispose();
            Assert.IsFalse(time.IsPaused);
            Assert.AreEqual(1f, time.Scale);
            Assert.IsFalse(_registry.Execute("time.scale").Succeeded, "its commands are gone");
        }

        [Test]
        public void WorldClockCommands_JumpTheClock_AndReleaseTheirSpeedOnDispose()
        {
            var settings = ScriptableObject.CreateInstance<ClockSettings>();
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.RegisterInstance(settings);
            builder.Register<WorldClock>(Lifetime.Singleton);
            IObjectResolver resolver = builder.Build();
            try
            {
                var clock = resolver.Resolve<WorldClock>();
                var commands = new WorldClockCommands(_registry, clock);
                commands.Initialize();

                Assert.IsTrue(_registry.Execute("clock.set 21.5").Succeeded);
                Assert.AreEqual(21, clock.Now.WholeHour);
                Assert.AreEqual(30, clock.Now.Minute);
                Assert.IsFalse(_registry.Execute("clock.set 25").Succeeded);
                _registry.Execute("clock.phase dus");
                Assert.AreEqual(DayPhase.Dusk, clock.Phase);
                _registry.Execute("clock.speed 0");
                Assert.AreEqual(0f, clock.SpeedScale);

                commands.Dispose();
                Assert.AreEqual(1f, clock.SpeedScale);
                Assert.IsFalse(_registry.Execute("clock.now").Succeeded);
            }
            finally
            {
                resolver.Dispose();
                Object.DestroyImmediate(settings);
            }
        }

        // An InputService action map (or one of its actions) is on; read by reflection, the test assembly has no Input System reference.
        static bool Enabled(InputService input, string map, string action = null)
        {
            object actions = typeof(InputService).GetField("_actions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(input);
            object target = actions.GetType().GetProperty(map)!.GetValue(actions);
            if (action != null)
                target = target.GetType().GetProperty(action)!.GetValue(target);
            return (bool)target.GetType().GetProperty("enabled")!.GetValue(target);
        }
    }
}
