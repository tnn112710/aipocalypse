using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.GraphicsSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.MapSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Core.QuickTimeSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.Stats;
using MB13.Aipocalypse.Core.SurfaceSystem;
using MB13.Aipocalypse.Input;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.Bootstrap
{
    // Root scope: VContainerSettings instantiates this prefab before any scene loads, so every scene has these services.
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField] ControlSettings _controls = new();
        [SerializeField, Tooltip("Languages players can pick; the others keep their text but are hidden")] LanguageSettings _languages = new();
        [SerializeField, Tooltip("Dev console (editor and development builds): Data/Debug/ConsoleSettings")] ConsoleSettings _consoleSettings;
        [SerializeField, Tooltip("_Debug/Console/Prefabs/DebugConsole")] ConsoleView _consoleOverlay;
        [SerializeField, Tooltip("Data/Save/SaveSettings: slots, autosave, the menu and world scenes")] SaveSettings _saveSettings;
        [SerializeField, Tooltip("Data/Equipment/ItemCatalog: every item by id, for saves")] ItemCatalog _itemCatalog;
        [SerializeField, Tooltip("Data/Surfaces/SurfaceSettings: footsteps and bullet impacts per surface; empty = one surface, no sounds")]
        SurfaceSettings _surfaceSettings;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe(options =>
            {
#if UNITY_EDITOR
                options.EnableCaptureStackTrace = true;
#endif
            });
            // Only for the MessagePipe Diagnostics window (editor and development builds); game code never uses GlobalMessagePipe.
            if (Debug.isDebugBuild)
                builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));

            var stamp = BuildStamp.Load();
            if (!Application.isEditor)
                Debug.Log($"Build: {stamp}");
            builder.RegisterInstance(stamp);

            new LocalizationInstaller(_languages).Install(builder);
            new InputInstaller(_controls).Install(builder);
            new ConsoleInstaller(_consoleSettings, _consoleOverlay).Install(builder);
            new AudioInstaller().Install(builder);
            new CombatInstaller().Install(builder);
            new EffectsInstaller().Install(builder);
            new SurfaceInstaller(_surfaceSettings).Install(builder);
            new TimeInstaller().Install(builder);
            new RandomInstaller().Install(builder);
            new StatInstaller().Install(builder);
            new GraphicsInstaller().Install(builder);
            new QuickTimeInstaller().Install(builder);
            new PlayerInstaller().Install(builder);
            new WeaponInstaller().Install(builder);
            new EquipmentInstaller(_itemCatalog).Install(builder);
            new MapInstaller().Install(builder);
            new SaveInstaller(_saveSettings != null ? _saveSettings : ScriptableObject.CreateInstance<SaveSettings>()).Install(builder);
        }
    }
}
