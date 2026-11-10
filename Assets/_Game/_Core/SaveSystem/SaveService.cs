using System;
using System.IO;
using UnityEngine;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Root service: the save slot files, through Easy Save 3 (JSON, one file per slot under persistentDataPath/Saves). Knows no
    // gameplay: GameSession fills a SaveData from the sections and hands it here. Main thread only (Easy Save reads its
    // settings through Unity). Easy Save writes to a .tmp file and swaps it in; before that, the previous save is copied to
    // .bak, and a slot whose file can't be read falls back to it.
    public sealed class SaveService
    {
        // Bump when a saved data shape changes; older saves are refused (no compatibility promise during development).
        public const int Version = 1;
        const string BackupSuffix = ".bak";

        readonly SaveSettings _settings;

        public SaveService(SaveSettings settings) => _settings = settings;

        public int SlotCount => _settings.SlotCount;
        public string FolderPath => Path.GetFullPath(Path.Combine(Application.persistentDataPath, _settings.Folder));
        public bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount;
        public string PathOf(int slot) => Path.Combine(FolderPath, $"slot_{slot}.es3");

        public bool Exists(int slot) => IsValidSlot(slot) && (File.Exists(PathOf(slot)) || File.Exists(PathOf(slot) + BackupSuffix));

        // An empty save to fill, holding a copy of `carry` (keys no section claims today stay in the file).
        public SaveData Create(SaveData carry = null)
        {
            var file = new ES3File(FileSettings(Path.Combine(FolderPath, "memory.es3")), false);
            if (carry != null)
                file.SaveRaw(carry.File.LoadRawBytes());
            file.DeleteKey(SaveData.HeaderKey);
            return new SaveData(file, null);
        }

        // What the slot list shows, without keeping the save.
        public SaveSlotInfo Info(int slot)
        {
            if (!Exists(slot))
                return new SaveSlotInfo(slot, null, null, false);
            return TryRead(slot, out SaveData data, out string problem, out bool fromBackup)
                ? new SaveSlotInfo(slot, data.Header, null, fromBackup)
                : new SaveSlotInfo(slot, null, problem, false);
        }

        // The slot saved most recently, or 0.
        public int LatestSlot()
        {
            int latest = 0;
            long newest = long.MinValue;
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                SaveSlotInfo info = Info(slot);
                if (info.Header != null && info.Header.SavedAtUtcTicks > newest)
                {
                    newest = info.Header.SavedAtUtcTicks;
                    latest = slot;
                }
            }
            return latest;
        }

        public bool TryRead(int slot, out SaveData data, out string problem) => TryRead(slot, out data, out problem, out _);

        // The slot's save; its backup when the file is missing or damaged (fromBackup). False with a reason the menu can show.
        public bool TryRead(int slot, out SaveData data, out string problem, out bool fromBackup)
        {
            data = null;
            fromBackup = false;
            if (!IsValidSlot(slot))
            {
                problem = $"No slot {slot} (1-{SlotCount})";
                return false;
            }
            string path = PathOf(slot);
            if (TryReadFile(path, out data, out problem, out bool otherVersion))
                return true;
            // A save from another version isn't damaged: its older backup must not stand in for it.
            if (otherVersion)
                return false;
            if (TryReadFile(path + BackupSuffix, out data, out string backupProblem, out _))
            {
                Debug.LogWarning($"Save slot {slot}: {problem}; loaded its backup instead.");
                fromBackup = true;
                problem = null;
                return true;
            }
            if (!File.Exists(path) && !File.Exists(path + BackupSuffix))
                problem = "Empty slot";
            else if (problem == null)
                problem = backupProblem;
            return false;
        }

        // Writes the slot (the previous save becomes its backup). False with a reason when the disk refused.
        public bool Write(int slot, SaveData data, out string problem)
        {
            problem = null;
            if (!IsValidSlot(slot))
            {
                problem = $"No slot {slot} (1-{SlotCount})";
                return false;
            }
            try
            {
                Directory.CreateDirectory(FolderPath);
                string path = PathOf(slot);
                data.Header.Version = Version;
                data.Set(SaveData.HeaderKey, data.Header);
                // Only a readable save is worth keeping as the backup: never let a damaged file push out the last good one.
                if (File.Exists(path) && TryReadFile(path, out _, out _, out _))
                    File.Copy(path, path + BackupSuffix, true);
                ES3.SaveRaw(data.File.LoadRawBytes(), FileSettings(path));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                problem = exception.Message;
                return false;
            }
            finally
            {
                data.Remove(SaveData.HeaderKey);
            }
        }

        // Deletes the slot and its backup. False when there was nothing to delete.
        public bool Delete(int slot)
        {
            if (!Exists(slot))
                return false;
            string path = PathOf(slot);
            File.Delete(path);
            File.Delete(path + BackupSuffix);
            return true;
        }

        bool TryReadFile(string path, out SaveData data, out string problem, out bool otherVersion)
        {
            data = null;
            problem = null;
            otherVersion = false;
            if (!File.Exists(path))
            {
                problem = "Missing file";
                return false;
            }
            try
            {
                var file = new ES3File(FileSettings(path), true);
                if (!file.KeyExists(SaveData.HeaderKey))
                {
                    problem = "Damaged save (no header)";
                    return false;
                }
                var header = file.Load<SaveHeader>(SaveData.HeaderKey);
                if (header.Version != Version)
                {
                    otherVersion = true;
                    problem = header.Version > Version ? "Saved by a newer version" : $"Old save format (v{header.Version})";
                    return false;
                }
                file.DeleteKey(SaveData.HeaderKey);
                data = new SaveData(file, header);
                return true;
            }
            catch (Exception exception)
            {
                problem = "Damaged save";
                Debug.LogWarning($"{path}: {exception.Message}");
                return false;
            }
        }

        static ES3Settings FileSettings(string path) =>
            new(path, ES3.Location.File, ES3.Format.JSON, ES3.EncryptionType.None, ES3.CompressionType.None);
    }

    // One row of the slot list: a save's header, or why it can't be read, or nothing (empty slot).
    public readonly struct SaveSlotInfo
    {
        public readonly int Slot;
        public readonly SaveHeader Header;
        public readonly string Problem;
        public readonly bool FromBackup;

        public SaveSlotInfo(int slot, SaveHeader header, string problem, bool fromBackup)
        {
            Slot = slot;
            Header = header;
            Problem = problem;
            FromBackup = fromBackup;
        }

        public bool IsEmpty => Header == null && Problem == null;
    }
}
