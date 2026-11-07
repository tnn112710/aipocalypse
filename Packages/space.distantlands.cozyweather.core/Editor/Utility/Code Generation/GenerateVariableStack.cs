//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEditor;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.Assemblies;

#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts.Generation
{
    /// <summary>
    /// Used to generate code for several repeated properties for the atmosphere system
    /// </summary>
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public partial class GenerateVariableStack : Editor
    {
#if COZY_DEV
        [MenuItem("Tools/COZY: Weather 4/Generate Variants")]
#endif
        public static void RegenerateAllCode()
        {
#if UNITY_6000_5_OR_NEWER
            var generationTypes = CurrentAssemblies.GetLoadedAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IGeneration).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);
#else
            var generationTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IGeneration).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);
#endif

            foreach (var type in generationTypes)
            {
                if (Activator.CreateInstance(type) is IGeneration generator)
                {
                    generator.Generate(layers);
                }
            }
        }

        public static bool ReadFile(out string content, string path)
        {
            content = "";
            if (!File.Exists(path))
            {
                Debug.LogError($"File missing at {path}");
                return false;
            }

            content = File.ReadAllText(path);
            return true;
        }

        public static bool WriteFile(string content, string path, string fileName, string extension)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            string packagePath = Path.GetFullPath($"{path}/{fileName}.{extension}");
            File.WriteAllText(packagePath, content);
            return true;
        }

        public static void Replace(ref string content, params (string symbol, string newContent)[] tokens)
        {
            foreach (var (symbol, newContent) in tokens)
                content = content.Replace(symbol, newContent);
        }

        public static string VariableName(string str)
        {
            if (!string.IsNullOrEmpty(str) && char.IsUpper(str[0]))
                return (str.Length == 1 ? char.ToLower(str[0]).ToString() : char.ToLower(str[0]) + str[1..]).Replace(" ", "");

            return str;
        }

        public static string DisplayName(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var result = new System.Text.StringBuilder();
            result.Append(input[0]);

            for (int i = 1; i < input.Length; i++)
            {
                char current = input[i];
                char prev = input[i - 1];

                bool isCapital = char.IsUpper(current);
                bool isPrevLower = char.IsLower(prev);
                bool isNextLower = (i + 1 < input.Length) && char.IsLower(input[i + 1]);

                if (isCapital && (isPrevLower || isNextLower))
                {
                    result.Append(' ');
                }

                result.Append(current);
            }

            return result.ToString();
        }
    }

    public interface IGeneration
    {
        public void Generate(LayerData[] data);
    }
}