using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShaderControl {

    [Serializable]
    public class ShaderKeywordConfig {
        public string shaderName;
        public List<string> excludedKeywords = new List<string>();
    }

    [Serializable]
    public class ShaderConfiguration {
        public string name;
        public List<string> excludedShaders = new List<string>();
        public List<ShaderKeywordConfig> shaderKeywordConfigs = new List<ShaderKeywordConfig>();
        public BuildViewSortType sortType = BuildViewSortType.ShaderKeywordCount;

        public ShaderConfiguration(string configName) {
            name = configName;
        }

        public void Clear() {
            excludedShaders.Clear();
            shaderKeywordConfigs.Clear();
        }

        public bool IsShaderExcluded(string shaderName) {
            return excludedShaders.Contains(shaderName);
        }

        public bool IsKeywordExcluded(string shaderName, string keyword) {
            var config = shaderKeywordConfigs.Find(c => c.shaderName == shaderName);
            return config != null && config.excludedKeywords.Contains(keyword);
        }

        public void SetShaderExcluded(string shaderName, bool excluded) {
            if (excluded) {
                if (!excludedShaders.Contains(shaderName)) {
                    excludedShaders.Add(shaderName);
                }
            } else {
                excludedShaders.Remove(shaderName);
            }
        }

        public void SetKeywordExcluded(string shaderName, string keyword, bool excluded) {
            var config = shaderKeywordConfigs.Find(c => c.shaderName == shaderName);
            if (config == null) {
                if (!excluded) return;
                config = new ShaderKeywordConfig { shaderName = shaderName };
                shaderKeywordConfigs.Add(config);
            }

            if (excluded) {
                if (!config.excludedKeywords.Contains(keyword)) {
                    config.excludedKeywords.Add(keyword);
                }
            } else {
                config.excludedKeywords.Remove(keyword);
                if (config.excludedKeywords.Count == 0) {
                    shaderKeywordConfigs.Remove(config);
                }
            }
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "ShaderControl", "Assembly-CSharp-Editor", "BuildConfigurationInfo")]
    [Serializable, ExecuteInEditMode]
    public class BuildConfiguration : ScriptableObject {
        
        public List<ShaderConfiguration> configurations = new List<ShaderConfiguration>();
        public int selectedConfigurationIndex = 0;

        void OnEnable() {
            InitializeConfigurations();
        }

        void InitializeConfigurations() {
            if (configurations == null || configurations.Count == 0) {
                configurations = new List<ShaderConfiguration>();
                configurations.Add(new ShaderConfiguration("Desktop 1"));
                configurations.Add(new ShaderConfiguration("Desktop 2"));
                configurations.Add(new ShaderConfiguration("Mobile 1"));
                configurations.Add(new ShaderConfiguration("Mobile 2"));
                configurations.Add(new ShaderConfiguration("Web"));
                configurations.Add(new ShaderConfiguration("Console 1"));
                configurations.Add(new ShaderConfiguration("Console 2"));
                configurations.Add(new ShaderConfiguration("VR 1"));
                configurations.Add(new ShaderConfiguration("VR 2"));
                configurations.Add(new ShaderConfiguration("Other"));
            }
        }

        public ShaderConfiguration GetSelectedConfiguration() {
            if (configurations == null || configurations.Count == 0 || selectedConfigurationIndex < 0 || selectedConfigurationIndex >= configurations.Count) {
                return null;
            }
            return configurations[selectedConfigurationIndex];
        }

        public void SaveCurrentBuildSettings(ShadersBuildInfo buildInfo) {
            if (buildInfo == null || buildInfo.shaders == null) return;
            
            var config = GetSelectedConfiguration();
            if (config == null) return;

            config.Clear();
            
            // Save sort type
            config.sortType = buildInfo.sortType;

            foreach (var shader in buildInfo.shaders) {
                if (!shader.includeInBuild) {
                    config.SetShaderExcluded(shader.name, true);
                }
                
                if (shader.keywords != null) {
                    foreach (var keyword in shader.keywords) {
                        if (!keyword.includeInBuild) {
                            config.SetKeywordExcluded(shader.name, keyword.keyword, true);
                        }
                    }
                }
            }
        }

        public void LoadToBuildSettings(ShadersBuildInfo buildInfo) {
            if (buildInfo == null || buildInfo.shaders == null) return;
            
            var config = GetSelectedConfiguration();
            if (config == null) return;
            
            // Load sort type
            buildInfo.sortType = config.sortType;
            buildInfo.Resort();

            foreach (var shader in buildInfo.shaders) {
                shader.includeInBuild = !config.IsShaderExcluded(shader.name);
                
                if (shader.keywords != null) {
                    foreach (var keyword in shader.keywords) {
                        keyword.includeInBuild = !config.IsKeywordExcluded(shader.name, keyword.keyword);
                    }
                }
            }
        }

        public void RenameConfiguration(int index, string newName) {
            if (index >= 0 && index < configurations.Count) {
                configurations[index].name = newName;
            }
        }

        public void ResetCurrentConfiguration() {
            var config = GetSelectedConfiguration();
            if (config != null) {
                config.Clear();
            }
        }
    }
} 