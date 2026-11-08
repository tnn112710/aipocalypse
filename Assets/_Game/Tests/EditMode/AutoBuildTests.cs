using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Editor.Build;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // Build names, which defines a build gets (a distribution build must never pick up cheats or the Pipeline server
    // by accident), what the zip leaves out, the shipped configs, and the build stamp file.
    public class AutoBuildTests
    {
        const string ConfigFolder = "Assets/_Game/Data/Build";

        readonly List<BuildConfig> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (BuildConfig config in _made)
                Object.DestroyImmediate(config);
            _made.Clear();
        }

        BuildConfig Config(bool development = false, bool profiler = false, bool cheat = false, bool pipeline = false)
        {
            var config = ScriptableObject.CreateInstance<BuildConfig>();
            config.Target = BuildTarget.StandaloneWindows64;
            config.Development = development;
            config.Profiler = profiler;
            config.Cheat = cheat;
            config.Pipeline = pipeline;
            _made.Add(config);
            return config;
        }

        static string Name(BuildConfig config) => AutoBuild.GetBuildName(config, "aipocalypse", "0.1.0");

        [Test]
        public void BuildName_DistributionHasOnlyPlatformAndKind() =>
            Assert.AreEqual("aipocalypse_0.1.0_windows_distribution", Name(Config()));

        [Test]
        public void BuildName_DistributionWithCheatSaysSo() =>
            Assert.AreEqual("aipocalypse_0.1.0_windows_distribution_cheat", Name(Config(cheat: true)));

        [Test]
        public void BuildName_DevelopmentAlwaysHasCheat() =>
            Assert.AreEqual("aipocalypse_0.1.0_windows_development_cheat", Name(Config(development: true)));

        [Test]
        public void BuildName_ProfilerMakesADevelopmentBuild() =>
            Assert.AreEqual("aipocalypse_0.1.0_windows_development_profiler_cheat", Name(Config(profiler: true)));

        [Test]
        public void BuildName_ListsOptionsInFixedOrder() =>
            Assert.AreEqual("aipocalypse_0.1.0_windows_development_profiler_cheat_pipeline",
                Name(Config(development: true, profiler: true, pipeline: true)));

        [Test]
        public void BuildName_MakesProductAndVersionSafeForFiles() =>
            Assert.AreEqual("My-Game_1.0-beta_windows_distribution", AutoBuild.GetBuildName(Config(), "My Game!", "1.0 beta"));

        [Test]
        public void PlayerDefines_DropStrayCheatAndPipelineToggles()
        {
            string[] player = { "A", AutoBuild.CheatDefine, AutoBuild.PipelineDevDefine, "B", AutoBuild.PipelineReleaseDefine };
            CollectionAssert.AreEqual(new[] { "A", "B" }, AutoBuild.PlayerDefinesFor(player, Config()));
            CollectionAssert.AreEqual(new[] { "A", "B" }, AutoBuild.PlayerDefinesFor(player, Config(development: true)));
        }

        [Test]
        public void PlayerDefines_PipelineAddsTheToggleOfItsBuildKind()
        {
            string[] player = { "A" };
            CollectionAssert.AreEqual(new[] { "A", AutoBuild.PipelineDevDefine },
                AutoBuild.PlayerDefinesFor(player, Config(development: true, pipeline: true)));
            CollectionAssert.AreEqual(new[] { "A", AutoBuild.PipelineReleaseDefine },
                AutoBuild.PlayerDefinesFor(player, Config(pipeline: true)));
        }

        [Test]
        public void ExtraDefines_CheatOnlyWhenTheBuildHasIt()
        {
            CollectionAssert.IsEmpty(AutoBuild.ExtraDefinesFor(Config()));
            CollectionAssert.AreEqual(new[] { AutoBuild.CheatDefine }, AutoBuild.ExtraDefinesFor(Config(cheat: true)));
            CollectionAssert.AreEqual(new[] { AutoBuild.CheatDefine }, AutoBuild.ExtraDefinesFor(Config(development: true)));
        }

        [Test]
        public void Zip_LeavesOutOnlyTheSymbolFolders()
        {
            Assert.IsTrue(AutoBuild.IsDoNotShip(@"aipocalypse_BurstDebugInformation_DoNotShip\Data\lib.txt"));
            Assert.IsTrue(AutoBuild.IsDoNotShip("aipocalypse_BackUpThisFolder_ButDontShipItWithYourGame/il2cpp/a.cpp"));
            Assert.IsFalse(AutoBuild.IsDoNotShip("aipocalypse.exe"));
            Assert.IsFalse(AutoBuild.IsDoNotShip(@"aipocalypse_Data\resources.assets"));
            Assert.IsFalse(AutoBuild.IsDoNotShip("notes_DoNotShip"));
            // How DeleteDoNotShipFolders asks about a top-level folder
            Assert.IsTrue(AutoBuild.IsDoNotShip("aipocalypse_BackUpThisFolder_ButDontShipItWithYourGame/"));
            Assert.IsFalse(AutoBuild.IsDoNotShip("aipocalypse_Data/"));
        }

        [Test]
        public void NewConfig_DeletesTheDoNotShipFoldersByDefault() => Assert.IsTrue(Config().DeleteDoNotShipFolders);

        [Test]
        public void ShippedConfigs_KeepTheirOptionsAndHaveUniqueNames()
        {
            BuildConfig[] configs = AssetDatabase.FindAssets("t:" + nameof(BuildConfig), new[] { ConfigFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<BuildConfig>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            CollectionAssert.AllItemsAreUnique(configs.Select(Name));
            Assert.IsTrue(configs.All(c => c.DeleteDoNotShipFolders), "a shipped config keeps the do-not-ship folders");

            BuildConfig distribution = configs.Single(c => c.name == "WindowsDistribution");
            Assert.IsFalse(distribution.IsDevelopment, "WindowsDistribution is a development build");
            Assert.IsFalse(distribution.HasCheat, "WindowsDistribution has cheats");
            Assert.IsFalse(distribution.Pipeline, "WindowsDistribution carries the Pipeline server");
            Assert.IsFalse(configs.Single(c => c.name == "WindowsDistributionCheat").Pipeline,
                "WindowsDistributionCheat carries the Pipeline server");
        }

        [Test]
        public void BuildStamp_RoundTripsThroughItsFile()
        {
            var stamp = new BuildStamp("aipocalypse_0.1.0_windows_distribution", "WindowsDistribution", "2d83070-dirty",
                "2026-11-02 22:55 +07:00");
            BuildStamp read = BuildStamp.Parse(stamp.ToFileText().Replace("\n", "\r\n"));
            Assert.AreEqual(stamp.Name, read.Name);
            Assert.AreEqual(stamp.Config, read.Config);
            Assert.AreEqual(stamp.Commit, read.Commit);
            Assert.AreEqual(stamp.Built, read.Built);
            Assert.AreEqual("aipocalypse_0.1.0_windows_distribution · 2d83070-dirty · 2026-11-02 22:55 +07:00", read.ToString());
        }

        [Test]
        public void BuildStamp_ParseSkipsUnknownKeysAndKeepsMissingOnesEmpty()
        {
            BuildStamp read = BuildStamp.Parse("name=x\nflavour=mint\nnot a line\n");
            Assert.AreEqual("x", read.Name);
            Assert.IsNull(read.Commit);
            Assert.AreEqual("x", read.ToString());
        }
    }
}
