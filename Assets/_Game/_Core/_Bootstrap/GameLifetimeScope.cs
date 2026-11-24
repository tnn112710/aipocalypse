using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.Bootstrap
{
    // Scene scope for World and prototypes: installs every SceneInstaller on its GameObject, reporting ones that can't install
    // (a strict scene also reports missing systems). Must build before any EntityScope's Awake.
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

        // Installers live in Core and in the system assemblies split from it (Animals, Dogs, Robots...).
        static Type[] InstallerTypes()
        {
            if (_installerTypes != null)
                return _installerTypes;
            var types = new List<Type>();
            string core = typeof(SceneInstaller).Assembly.GetName().Name;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != core && !References(assembly, core))
                    continue;
                foreach (Type type in assembly.GetTypes())
                    if (!type.IsAbstract && typeof(SceneInstaller).IsAssignableFrom(type))
                        types.Add(type);
            }
            return _installerTypes = types.ToArray();
        }

        static bool References(Assembly assembly, string name)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                if (reference.Name == name)
                    return true;
            return false;
        }
    }
}
