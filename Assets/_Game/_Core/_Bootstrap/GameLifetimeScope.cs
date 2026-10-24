using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.Bootstrap
{
    // Scene scope for World and for prototypes that need real systems: installs every SceneInstaller on its GameObject (world
    // clock, environment, enemies…). An installer that can't install is reported, and a strict scene (World) also reports every
    // scene system it lacks. Must build before any EntityScope's Awake.
    [DefaultExecutionOrder(-5000)]
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField, Tooltip("On in World: an entity falling back to a default service, or a scene system that's missing or can't " +
            "install, is an error. Off (prototypes): systems are optional and problems are warnings")]
        bool _strict;

        static Type[] _installerTypes;

        public bool Strict => _strict;

        protected override void Configure(IContainerBuilder builder)
        {
            foreach (SceneInstaller installer in GetComponents<SceneInstaller>())
            {
                string problem = installer.Problem;
                if (problem == null)
                    installer.Install(builder);
                else
                    Report($"{installer.GetType().Name} not installed: {problem}", installer);
            }

            if (!_strict)
                return;
            foreach (Type type in InstallerTypes())
                if (GetComponent(type) == null)
                    Report($"no {type.Name}: a strict scene runs every scene system", this);
        }

        void Report(string message, UnityEngine.Object context)
        {
            if (_strict)
                Debug.LogError($"{name}: {message}", context);
            else
                Debug.LogWarning($"{name}: {message}", context);
        }

        static Type[] InstallerTypes()
        {
            if (_installerTypes != null)
                return _installerTypes;
            var types = new List<Type>();
            foreach (Type type in typeof(SceneInstaller).Assembly.GetTypes())
                if (!type.IsAbstract && typeof(SceneInstaller).IsAssignableFrom(type))
                    types.Add(type);
            return _installerTypes = types.ToArray();
        }
    }
}
