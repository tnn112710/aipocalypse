//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Base class for CozyWeather.cs and CozyBiome.cs
    /// </summary>
    [AddComponentMenu("")]
    public partial class CozySystem : MonoBehaviour
    {
        /// <summary>
        /// Calculated by the main weather instance at runtime. To set the target weight use TargetWeight.
        /// </summary>
        public float Weight => weight;

        /// <summary>
        /// Sets the target weight of this system. The actual weight will be calculated based on all of the systems in the scene.
        /// </summary>
        public virtual float TargetWeight { get { return targetWeight; } set { targetWeight = value; } }
        /// <summary>
        /// Determines the order in-which systems are weighted and processed.
        /// Systems with the same priority will be weighted against each other. 
        /// Systems with a higher priority will be evaluated after ones with lower priorities, thus taking precedence.
        /// </summary>
        public int Priority { get { return priority; } set { priority = value; } }

        [Range(0, 1)]
        internal float weight = 1;

        [Range(0, 1)]
        [SerializeField]
        private float targetWeight = 1;

        [SerializeField]
        [Min(-999)]
        private int priority;

        public GameObject ModuleHolder
        {
            get
            {
                if (moduleHolder)
                    return moduleHolder;

                if (GetChild("Modules"))
                    moduleHolder = GetChild("Modules").gameObject;
                else
                {
                    moduleHolder = new GameObject("Modules");
                    moduleHolder.transform.parent = transform;
                    moduleHolder.transform.localPosition = Vector3.zero;
                }

                return moduleHolder;
            }
        }
        private GameObject moduleHolder;

        private Dictionary<Type, ICozyModule> modules = new();
        private Dictionary<Type, ICozyModule> singleModuleCache = new();
        private Dictionary<Type, object> listModuleCache = new();
        internal bool IsLoadingSave { get; private set; }

        [SerializeField]
        private CozyModule[] moduleArray = new CozyModule[0];
        public CozyModule[] ModuleArray => moduleArray;
        public IEnumerable<ICozyModule> ActiveAndEnabledModules => modules.Where(module => module.Value.Active).Select(x => x.Value);

        #region Modules

        public T AddModule<T>() where T : class, ICozyModule, new()
        {
            return (T)AddModule(typeof(T));
        }

        public ICozyModule AddModule(Type type)
        {
            if (!typeof(ICozyModule).IsAssignableFrom(type))
                throw new ArgumentException($"{type.Name} does not implement ICozyModule");

            if (type.GetCustomAttribute(typeof(CozyModuleAttribute)) == null)
                throw new ArgumentException($"{type.Name} does not use the CozyModule Attribute");

            if (modules.TryGetValue(type, out var existing))
            {
                Debug.LogWarning(
                    $"{gameObject.name} already has an instance of {type.Name} added as a module."
                );
                return existing;
            }

            var module = (CozyModule)ModuleHolder.AddComponent(type);
            modules[type] = module;
            module.System = this;

            ClearCaches();
            UpdateModuleArray();

            module.ResetModule();
            module.SetupModule();
            return module;
        }

        public void RemoveModule<T>() where T : class, ICozyModule
        {
            RemoveModule(typeof(T));
        }

        public void RemoveModule(Type type)
        {
            if (!HasModule(type))
            {
                Debug.Log("No module of that type found");
                return;
            }


            if (Application.isPlaying)
                Destroy((CozyModule)modules[type]);
            else
                DestroyImmediate((CozyModule)modules[type]);
            modules.Remove(type);

            ClearCaches();
            UpdateModuleArray();
        }

        public bool HasModule<T>() where T : class, ICozyModule
        {
            return modules.ContainsKey(typeof(T));
        }

        public bool HasModule(Type type)
        {
            if (modules.ContainsKey(type))
                return true;

            return singleModuleCache.TryGetValue(type, out var cachedModule) && cachedModule != null;
        }

        public bool GetModule<T>(out T module) where T : class, ICozyModule
        {
            if (modules.Count == 0)
                ResetModules();

            Type type = typeof(T);

            if (singleModuleCache.TryGetValue(type, out var cachedModule))
            {
                module = (T)cachedModule;
                return module != null;
            }

            module = modules.Values
                .Where(v => v is T)
                .OrderByDescending(v => GetModulePriority(v.GetType()))
                .ThenBy(v => v.GetType().FullName)
                .Cast<T>()
                .FirstOrDefault();

            singleModuleCache[type] = module;
            return module != null;
        }

        public T Get<T>(ref T field) where T : class, ICozyModule
        {
            if (field is UnityEngine.Object obj && obj != null) return field;
            GetModule(out field);
            return field;
        }

        public List<T> GetModules<T>() where T : class, ICozyModule
        {
            if (modules.Count == 0)
                ResetModules();

            Type type = typeof(T);

            if (listModuleCache.TryGetValue(type, out var cachedList))
            {
                return (List<T>)cachedList;
            }

            List<T> modulesList = modules.Values
                .Where(v => v is T)
                .OrderByDescending(v => GetModulePriority(v.GetType()))
                .ThenBy(v => v.GetType().FullName)
                .Cast<T>()
                .ToList();

            listModuleCache[type] = modulesList;
            return modulesList;
        }

        private int GetModulePriority(Type type)
        {
            var attribute = (CozyModuleAttribute)Attribute.GetCustomAttribute(type, typeof(CozyModuleAttribute));
            return attribute != null ? attribute.priority : 0;
        }

        private void ClearCaches()
        {
            singleModuleCache.Clear();
            listModuleCache.Clear();
        }

        private void UpdateModuleArray()
        {
            moduleArray = new CozyModule[modules.Count];
            int index = 0;
            foreach (var module in modules.Values)
            {
                moduleArray[index++] = (CozyModule)module;
            }
        }

        public void ResetModule(Type type)
        {
            RemoveModule(type);
            MonoBehaviour module = AddModule(type) as MonoBehaviour;
        }

        /// <summary>
        /// Makes sure that all module connections are setup properly. Run this after adding or removing a module.
        /// </summary> 
        public virtual void ResetModules()
        {
            RemoveNullValues(modules);
            ClearCaches();

            CozyModule[] instantiatedModules = ModuleHolder.GetComponents<CozyModule>();
            modules.Clear();
            foreach (CozyModule module in instantiatedModules)
            {
                if (!modules.ContainsKey(module.GetType()))
                {
                    modules[module.GetType()] = module;
                }
                module.System = this;
                module.ResetModule();
            }

            UpdateModuleArray();
        }

        void RemoveNullValues<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            var toRemove = new List<TKey>();

            foreach (var kvp in dict)
            {
                if (kvp.Value == null)
                    toRemove.Add(kvp.Key);
            }

            foreach (var key in toRemove)
                dict.Remove(key);
        }
        #endregion

        #region 
        public Transform GetChild(string name)
        {
            foreach (Transform i in transform.GetComponentsInChildren<Transform>())
            {
                if (i.name == name)
                    return i;
            }

            return null;
        }

        public T GetChild<T>(string name) where T : Component
        {
            foreach (T i in transform.GetComponentsInChildren<T>())
            {
                if (i.name == name)
                    return i;
            }

            return default;
        }

        public List<T> FindChildrenComponentsByTag<T>(Transform parentTransform, string tag)
            where T : Component
        {
            var components = new List<T>();
            foreach (Transform child in parentTransform)
            {
                if (child.CompareTag(tag) && child.TryGetComponent<T>(out var component))
                    components.Add(component);
                components.AddRange(FindChildrenComponentsByTag<T>(child, tag));
            }
            return components;
        }
        #endregion

        /// <summary>
        /// Skip a certain amount of time. 
        /// </summary>
        public void Skip(MeridiemTimeDelta delta)
        {
            foreach (var module in GetModules<ISkippable>())
            {
                module.SkipTime(delta);
            }
        }

        /// <summary>
        /// Saves to a slot in the player prefs
        /// </summary>
        public void Save(int slot = 0)
        {
            PlayerPrefs.SetString($"CZY_Save_{slot}", GetSaveString());
        }

        /// <summary>
        /// Loads from a slot in the player prefs
        /// </summary>
        public void Load(int slot = 0)
        {
            LoadFromString(PlayerPrefs.GetString($"CZY_Save_{slot}", ""));
        }

        /// <summary>
        /// Saves to a JSON string
        /// </summary>
        [Serializable]
        private class SaveDataCollection
        {
            public List<SavedData> modules;
        }

        public string GetSaveString()
        {
            SaveDataCollection savedData = new() { modules = new List<SavedData>() };
            foreach (var module in GetModules<ISaveable>())
            {
                savedData.modules.Add(module.Save());
            }

            return JsonUtility.ToJson(savedData);
        }

        /// <summary>
        /// Loads based on a JSON string
        /// </summary>
        public void LoadFromString(string save)
        {
            if (string.IsNullOrWhiteSpace(save))
            {
                Debug.LogWarning("Cannot load an empty COZY save.", this);
                return;
            }
            SaveDataCollection savedData = JsonUtility.FromJson<SaveDataCollection>(save);
            if (savedData == null || savedData.modules == null)
            {
                Debug.LogError("COZY save does not contain module data.", this);
                return;
            }
            Dictionary<string, SavedData> kvps = new();

            foreach (var data in savedData.modules)
            {
                kvps.Add(data.key, data);
            }

            bool wasLoadingSave = IsLoadingSave;
            IsLoadingSave = true;
            try
            {
                foreach (var module in GetModules<ISaveable>())
                {
                    if (kvps.TryGetValue(module.GetType().FullName, out var data) ||
                        kvps.TryGetValue(module.GetType().Name, out data))
                        module.Load(data);
                    else
                        Debug.LogWarning($"COZY save contains no data for {module.GetType().FullName}.", this);
                }
            }
            finally
            {
                IsLoadingSave = wasLoadingSave;
            }
        }
    }
}