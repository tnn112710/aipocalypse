using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aipocalypse.Editor.Build
{
    // The last build of each config on this machine (Library/AutoBuild/History.json), shown by the Autobuild window.
    public static class AutoBuildHistory
    {
        [Serializable]
        public sealed class Entry
        {
            public string Guid;
            public string Result;
            public string Finished;
            public double Seconds;
            public long Size;
            public int Errors;
            public string Folder;
            public string Executable;
            public string Zip;

            public bool Succeeded => Result == nameof(UnityEditor.Build.Reporting.BuildResult.Succeeded);
        }

        [Serializable]
        sealed class Data
        {
            public List<Entry> Entries = new();
        }

        static Data _data;

        static string FilePath => Path.Combine(AutoBuild.ProjectRoot, "Library", "AutoBuild", "History.json");

        public static Entry Get(BuildConfig config) =>
            Load().Entries.Find(e => e.Guid == GuidOf(config));

        public static void Record(BuildConfig config, BuildOutcome outcome)
        {
            Data data = Load();
            string guid = GuidOf(config);
            data.Entries.RemoveAll(e => e.Guid == guid);
            data.Entries.Add(new Entry
            {
                Guid = guid,
                Result = outcome.Result.ToString(),
                Finished = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Seconds = outcome.Time.TotalSeconds,
                Size = outcome.Size,
                Errors = outcome.Errors,
                Folder = outcome.Folder,
                Executable = outcome.Executable,
                Zip = outcome.Zip,
            });
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }

        static Data Load()
        {
            if (_data != null)
                return _data;
            try
            {
                _data = File.Exists(FilePath) ? JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AutoBuild] Build history unreadable, starting over: {e.Message}");
            }
            return _data ??= new Data();
        }

        static string GuidOf(BuildConfig config) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
    }
}
