using System;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // What one slot's file holds about the save itself (the slot list shows it). Sections may fill the summary fields.
    [Serializable]
    public sealed class SaveHeader
    {
        public int Version;
        public long SavedAtUtcTicks;
        public double PlayTimeSeconds;
        public string Scene;
        public string Build;
        public int Day;
        public float Hour;

        public DateTime SavedAtLocal => new DateTime(SavedAtUtcTicks, DateTimeKind.Utc).ToLocalTime();
    }

    // One save in memory: the header plus each system's own entry under its key. Only SaveService touches Easy Save; sections
    // read and write plain [Serializable] data classes here (stable ids, never Unity object references).
    public sealed class SaveData
    {
        internal const string HeaderKey = "_header";

        readonly ES3File _file;

        internal SaveData(ES3File file, SaveHeader header)
        {
            _file = file;
            Header = header ?? new SaveHeader();
        }

        public SaveHeader Header { get; }

        public bool Has(string key) => _file.KeyExists(key);

        public void Set<T>(string key, T value) => _file.Save(key, value);

        // False (value default) when the key is missing: the system keeps its new-game state.
        public bool TryGet<T>(string key, out T value)
        {
            if (!_file.KeyExists(key))
            {
                value = default;
                return false;
            }
            value = _file.Load<T>(key);
            return true;
        }

        public void Remove(string key)
        {
            if (_file.KeyExists(key))
                _file.DeleteKey(key);
        }

        internal ES3File File => _file;
    }
}
