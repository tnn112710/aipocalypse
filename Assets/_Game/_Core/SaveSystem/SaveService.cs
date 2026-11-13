using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MB13.Aipocalypse.Core.LocalizationSystem;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Root service: the save slot files, through Easy Save 3 (JSON, one file per slot under persistentDataPath/Saves). Knows no
    // gameplay: GameSession fills a SaveData from the sections and hands it here. Main thread only (Easy Save reads its
    // settings through Unity), except a write's file work: backup, .tmp file, swap, on the thread pool, one write at a time.
    // Every other file access waits for it first (Flush). A slot whose file can't be read falls back to its .bak.
    public sealed class SaveService
    {
        // Bump when a saved data shape changes; older saves are refused (no compatibility promise during development).
        public const int Version = 1;
        // The editor's own slot, after the player's: Play pressed in a world scene (or Prototype in the main menu) saves
        // here, never into Save 1-5. Exists only in the editor.
        public const int EditorSlot = 99;
        const string BackupSuffix = ".bak";
        const string TempSuffix = ".tmp";
        public static readonly LocalizedString EmptySlot = Loc.UI("save.empty_slot");
        static readonly LocalizedString NoSlotPicked = Loc.UI("session.no_slot_picked");
        static readonly LocalizedString NoSlotInRange = Loc.UI("save.no_slot_range");
        static readonly LocalizedString MissingFile = Loc.UI("save.missing_file");
        static readonly LocalizedString DamagedNoHeader = Loc.UI("save.damaged_no_header");
        public static readonly LocalizedString NewerVersion = Loc.UI("save.newer_version");
        static readonly LocalizedString OldFormat = Loc.UI("save.old_format");
        static readonly LocalizedString Damaged = Loc.UI("save.damaged");

        readonly SaveSettings _settings;
        // Slots whose file is known to read (read or written this session, and unchanged since: its time and size), so the
        // next write can keep it as the backup without reading it first.
        readonly Dictionary<int, (DateTime time, long size)> _readable = new();
        readonly object _lock = new();
        Task _writing = Task.CompletedTask;

        public SaveService(SaveSettings settings) => _settings = settings;

        public int SlotCount => _settings.SlotCount;
        public static bool HasEditorSlot => Application.isEditor;
        public string FolderPath => Path.GetFullPath(Path.Combine(Application.persistentDataPath, _settings.Folder));
        public bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount || slot == EditorSlot && HasEditorSlot;
        public string PathOf(int slot) => Path.Combine(FolderPath, slot == EditorSlot ? "slot_editor.es3" : $"slot_{slot}.es3");

        // Every slot the menus list: Save 1..SlotCount, then the editor slot in the editor.
        public IEnumerable<int> Slots
        {
            get
            {
                for (int slot = 1; slot <= SlotCount; slot++)
                    yield return slot;
                if (HasEditorSlot)
                    yield return EditorSlot;
            }
        }

        public bool Exists(int slot)
        {
            Flush();
            return IsValidSlot(slot) && (File.Exists(PathOf(slot)) || File.Exists(PathOf(slot) + BackupSuffix));
        }

        // A save to fill with a fresh header. With `carry` (the save loaded or last written) it goes on from carry's entries
        // instead of copying them (~1.5 ms): sections overwrite theirs, and keys no section claims today stay. Safe because
        // a write takes its bytes at once (WriteAsync), so nothing written earlier changes.
        public SaveData Create(SaveData carry = null)
        {
            if (carry != null)
                return new SaveData(carry.File, null);
            var file = new ES3File(FileSettings(Path.Combine(FolderPath, "memory.es3")), false);
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
            foreach (int slot in Slots)
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
                problem = NoSlot(slot);
                return false;
            }
            Flush();
            string path = PathOf(slot);
            if (TryReadFile(path, out data, out problem, out bool otherVersion))
            {
                MarkReadable(slot, path);
                return true;
            }
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
                problem = EmptySlot.Text();
            else if (problem == null)
                problem = backupProblem;
            return false;
        }

        // Starts writing the slot and returns at once: the task ends with null, or why the disk refused. The bytes are taken
        // now (so `data` can change afterwards); the previous save becomes the .bak if it reads.
        public Task<string> WriteAsync(int slot, SaveData data)
        {
            if (!IsValidSlot(slot))
                return Task.FromResult(NoSlot(slot));
            Flush();
            byte[] bytes;
            data.Header.Version = Version;
            data.Set(SaveData.HeaderKey, data.Header);
            try
            {
                bytes = data.File.LoadRawBytes();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Task.FromResult(exception.Message);
            }
            finally
            {
                data.Remove(SaveData.HeaderKey);
            }
            string folder = FolderPath;
            string path = PathOf(slot);
            // Only a readable save is worth keeping as the backup: never let a damaged file push out the last good one.
            bool backup = File.Exists(path) && (IsKnownReadable(slot, path) || TryReadFile(path, out _, out _, out _));
            Task<string> write = Task.Run(() => WriteFile(slot, folder, path, bytes, backup));
            _writing = write;
            return write;
        }

        // Waits for the write in progress, if any.
        public void Flush() => _writing.Wait();

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

        // Thread pool: the new file goes to .tmp first, so a failed write leaves the slot as it was.
        string WriteFile(int slot, string folder, string path, byte[] bytes, bool backup)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string temp = path + TempSuffix;
                File.WriteAllBytes(temp, bytes);
                if (File.Exists(path))
                {
                    if (backup)
                        File.Copy(path, path + BackupSuffix, true);
                    File.Replace(temp, path, null);
                }
                else
                    File.Move(temp, path);
                MarkReadable(slot, path);
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return exception.Message;
            }
        }

        void MarkReadable(int slot, string path)
        {
            var file = new FileInfo(path);
            lock (_lock)
                _readable[slot] = (file.LastWriteTimeUtc, file.Length);
        }

        bool IsKnownReadable(int slot, string path)
        {
            (DateTime time, long size) known;
            lock (_lock)
                if (!_readable.TryGetValue(slot, out known))
                    return false;
            var file = new FileInfo(path);
            return file.Exists && file.LastWriteTimeUtc == known.time && file.Length == known.size;
        }

        string NoSlot(int slot) => slot == 0 ? NoSlotPicked.Text() : NoSlotInRange.Format(slot, SlotCount);

        bool TryReadFile(string path, out SaveData data, out string problem, out bool otherVersion)
        {
            data = null;
            problem = null;
            otherVersion = false;
            if (!File.Exists(path))
            {
                problem = MissingFile.Text();
                return false;
            }
            try
            {
                var file = new ES3File(FileSettings(path), true);
                if (!file.KeyExists(SaveData.HeaderKey))
                {
                    problem = DamagedNoHeader.Text();
                    return false;
                }
                var header = file.Load<SaveHeader>(SaveData.HeaderKey);
                if (header.Version != Version)
                {
                    otherVersion = true;
                    problem = header.Version > Version ? NewerVersion.Text() : OldFormat.Format(header.Version);
                    return false;
                }
                file.DeleteKey(SaveData.HeaderKey);
                data = new SaveData(file, header);
                return true;
            }
            catch (Exception exception)
            {
                problem = Damaged.Text();
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
