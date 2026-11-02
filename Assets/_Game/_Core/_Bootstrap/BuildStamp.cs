using System.IO;
using System.Text;
using UnityEngine;

namespace Aipocalypse.Core.Bootstrap
{
    // Which build this is: AutoBuild writes build_info.txt next to the player, the root scope reads it at start, logs it
    // once (Player.log) and the console shows it. The editor, or a player without the file, says so instead.
    public sealed class BuildStamp
    {
        public const string FileName = "build_info.txt";

        public BuildStamp(string name, string config, string commit, string built)
        {
            Name = name;
            Config = config;
            Commit = commit;
            Built = built;
        }

        public string Name { get; }
        public string Config { get; }
        public string Commit { get; }
        public string Built { get; }

        public static BuildStamp Load()
        {
            if (Application.isEditor)
                return new BuildStamp("editor", null, null, null);
            // dataPath is <build folder>/<product>_Data on Windows and Linux.
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, FileName);
            try
            {
                if (File.Exists(path))
                    return Parse(File.ReadAllText(path));
            }
            catch (IOException)
            {
            }
            return new BuildStamp($"{Application.productName} {Application.version} (no {FileName})", null, null, null);
        }

        // key=value lines; unknown keys are ignored, missing ones stay null.
        public static BuildStamp Parse(string text)
        {
            string name = null, config = null, commit = null, built = null;
            foreach (string line in text.Split('\n'))
            {
                int split = line.IndexOf('=');
                if (split <= 0)
                    continue;
                string value = line[(split + 1)..].Trim();
                switch (line[..split].Trim())
                {
                    case "name": name = value; break;
                    case "config": config = value; break;
                    case "commit": commit = value; break;
                    case "built": built = value; break;
                }
            }
            return new BuildStamp(name, config, commit, built);
        }

        public string ToFileText()
        {
            var text = new StringBuilder();
            Line("name", Name);
            Line("config", Config);
            Line("commit", Commit);
            Line("built", Built);
            return text.ToString();

            void Line(string key, string value)
            {
                if (!string.IsNullOrEmpty(value))
                    text.Append(key).Append('=').Append(value).Append('\n');
            }
        }

        public override string ToString()
        {
            var text = new StringBuilder(Name ?? "unknown build");
            if (!string.IsNullOrEmpty(Commit))
                text.Append(" · ").Append(Commit);
            if (!string.IsNullOrEmpty(Built))
                text.Append(" · ").Append(Built);
            return text.ToString();
        }
    }
}
