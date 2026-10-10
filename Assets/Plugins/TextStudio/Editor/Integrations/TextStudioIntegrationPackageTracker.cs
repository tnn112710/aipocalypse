using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImpossibleRobert.Common.Editor.Packages;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal enum TextStudioPackageStatus
    {
        NotApplicable,
        Unavailable,
        Checking,
        Current,
        UpdateAvailable,
        BundledPackageDiffers,
        InstalledNewer,
        LocalChanges,
        IncompleteImport,
        Unknown,
        InvalidBundle,
        RetiredFiles
    }

    internal sealed class TextStudioPackageSnapshot
    {
        public TextStudioPackageStatus Status;
        public IntegrationPackagePayloadKind PayloadKind;
        public IntegrationPackageManifest Available;
        public IntegrationPackageManifest Installed;
        public string ArchiveSha256;
        public string Detail;
        public string[] Changed = Array.Empty<string>();
        public string[] Missing = Array.Empty<string>();
        public string[] Retired = Array.Empty<string>();
        public IntegrationPackageRevisionRelation Relation;
        public bool IsUpdate => Relation == IntegrationPackageRevisionRelation.AvailableNewer &&
            (Status == TextStudioPackageStatus.UpdateAvailable || Status == TextStudioPackageStatus.LocalChanges ||
             Status == TextStudioPackageStatus.IncompleteImport);
    }

    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    // Package checks and notifications follow asset and assembly changes, including Play Mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioIntegrationPackageTracker
    {
        internal sealed class FileInput
        {
            public IntegrationPackageEntry Entry;
            public string AssetPath;
            public string MetaPath;
        }

        [Serializable]
        private sealed class InventoryHistory
        {
            public IntegrationPackageManifest[] inventories;
        }

        private static readonly Dictionary<string, TextStudioPackageSnapshot> Cache = new Dictionary<string, TextStudioPackageSnapshot>(StringComparer.Ordinal);
        private static readonly SemaphoreSlim Workers = new SemaphoreSlim(2, 2);
        private static CancellationTokenSource _checks = new CancellationTokenSource();
        private static int _generation;
        private static bool _notificationPending;

        internal static event Action Changed;

        static TextStudioIntegrationPackageTracker()
        {
            EditorApplication.projectChanged += Invalidate;
            AssetDatabase.importPackageCompleted += _ => Invalidate();
            AssetDatabase.importPackageCancelled += _ => Invalidate();
            AssetDatabase.importPackageFailed += (_, __) => Invalidate();
            AssemblyReloadEvents.beforeAssemblyReload += Invalidate;
            EditorApplication.quitting += Invalidate;
        }

        internal static void Invalidate()
        {
            _checks.Cancel();
            _checks.Dispose();
            _checks = new CancellationTokenSource();
            _generation++;
            Cache.Clear();
            NotifyChanged();
        }

        private static void NotifyChanged()
        {
            if (_notificationPending)
                return;
            _notificationPending = true;
            EditorApplication.delayCall += () =>
            {
                _notificationPending = false;
                Changed?.Invoke();
            };
        }

        internal static void RememberOldInventory(TextStudioPackageSnapshot package)
        {
            if (package?.Installed == null || package.Available == null ||
                !HasMatchingIdentity(package.Installed, package.Available) ||
                string.Equals(package.Installed.contentFingerprint, package.Available.contentFingerprint, StringComparison.Ordinal))
                return;

            string path = HistoryPath(package.Available.packageId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            List<IntegrationPackageManifest> inventories = ReadHistory(package.Available);
            if (!inventories.Any(inventory => inventory.contentFingerprint == package.Installed.contentFingerprint))
                inventories.Add(package.Installed);
            File.WriteAllText(path, JsonUtility.ToJson(new InventoryHistory { inventories = inventories.ToArray() }));
        }

        private static List<IntegrationPackageManifest> ReadHistory(IntegrationPackageManifest available)
        {
            List<IntegrationPackageManifest> inventories = new List<IntegrationPackageManifest>();
            string path = HistoryPath(available.packageId);
            if (!File.Exists(path)) return inventories;
            try
            {
                string json = File.ReadAllText(path);
                InventoryHistory history = JsonUtility.FromJson<InventoryHistory>(json);
                // Accept the original single-inventory cache while upgrading it on the next import.
                IntegrationPackageManifest[] records = history?.inventories;
                if (records == null && IntegrationPackageManifest.TryRead(json, out IntegrationPackageManifest legacy, out _))
                    records = new[] { legacy };
                foreach (IntegrationPackageManifest record in records ?? Array.Empty<IntegrationPackageManifest>())
                {
                    if (IntegrationPackageManifest.TryRead(JsonUtility.ToJson(record), out IntegrationPackageManifest valid, out _) &&
                        HasMatchingIdentity(valid, available))
                        inventories.Add(valid);
                }
            }
            catch (IOException) { }
            catch (ArgumentException) { }
            return inventories;
        }

        private static string HistoryPath(string packageId)
        {
            return Path.GetFullPath(Path.Combine("Library", "TextStudioIntegrationTracking", packageId + ".json"));
        }

        internal static TextStudioPackageSnapshot Get(TextStudioIntegrationDefinition definition, string archiveAssetPath)
        {
            if (definition == null || string.IsNullOrEmpty(definition.NestedPackageFileName))
                return new TextStudioPackageSnapshot { Status = TextStudioPackageStatus.NotApplicable };
            if (string.IsNullOrEmpty(archiveAssetPath))
                return new TextStudioPackageSnapshot { Status = TextStudioPackageStatus.Unavailable, Detail = "No bundled archive is installed with this product." };

            string key = archiveAssetPath;
            if (Cache.TryGetValue(key, out TextStudioPackageSnapshot cached))
                return cached;

            string descriptorPath = archiveAssetPath + ".manifest.json";
            if (!File.Exists(descriptorPath))
                return Cache[key] = new TextStudioPackageSnapshot { Status = TextStudioPackageStatus.Unknown, Detail = "This bundled package has no update metadata. Review or import the package manually." };

            string descriptorJson;
            try { descriptorJson = File.ReadAllText(descriptorPath); }
            catch (Exception exception)
            {
                return Cache[key] = new TextStudioPackageSnapshot { Status = TextStudioPackageStatus.InvalidBundle, Detail = "Bundled metadata could not be read: " + exception.Message };
            }
            if (!IntegrationPackageDescriptor.TryRead(descriptorJson, out IntegrationPackageDescriptor descriptor, out string error))
                return Cache[key] = new TextStudioPackageSnapshot { Status = TextStudioPackageStatus.InvalidBundle, Detail = error };
            if (!string.Equals(descriptor.manifest.ownerPackageName, definition.NestedPackageOwnerPackageName, StringComparison.Ordinal))
                return Cache[key] = new TextStudioPackageSnapshot { Status = TextStudioPackageStatus.InvalidBundle, Detail = "Bundled metadata names a different owning product." };

            string installedPath = AssetDatabase.GUIDToAssetPath(IntegrationPackageFingerprint.TrackingGuid(descriptor.manifest.packageId, true));
            IntegrationPackageManifest installed = null;
            if (!string.IsNullOrEmpty(installedPath) && File.Exists(installedPath))
            {
                try { IntegrationPackageManifest.TryRead(File.ReadAllText(installedPath), out installed, out _); }
                catch (IOException) { installed = null; }
            }

            string historyPath = HistoryPath(descriptor.manifest.packageId);
            List<IntegrationPackageManifest> previous = ReadHistory(descriptor.manifest);

            if (!HasMatchingIdentity(installed, descriptor.manifest)) installed = null;
            List<FileInput> availableFiles = ResolveFiles(descriptor.manifest);
            List<FileInput> installedFiles = installed == null ? null : ResolveFiles(installed);
            List<FileInput> previousFiles = previous.SelectMany(ResolveFiles).ToList();
            HashSet<string> availableGuids = new HashSet<string>(descriptor.manifest.entries.Select(entry => entry.guid), StringComparer.Ordinal);
            string[] retired = previousFiles.Concat(installedFiles ?? new List<FileInput>())
                .Where(file => !availableGuids.Contains(file.Entry.guid) && !string.IsNullOrEmpty(file.AssetPath) &&
                    (file.Entry.folder ? Directory.Exists(file.AssetPath) : File.Exists(file.AssetPath)))
                .Select(file => file.AssetPath).Distinct(StringComparer.Ordinal).ToArray();
            if (previous.Count > 0 && retired.Length == 0)
                File.Delete(historyPath);
            int generation = _generation;
            CancellationToken cancellation = _checks.Token;
            SynchronizationContext mainThread = SynchronizationContext.Current;
            if (mainThread == null)
                return Cache[key] = new TextStudioPackageSnapshot { Status = TextStudioPackageStatus.Unknown, Detail = "Unity's editor synchronization context is unavailable." };
            TextStudioPackageSnapshot pending = new TextStudioPackageSnapshot
            {
                Status = TextStudioPackageStatus.Checking,
                PayloadKind = descriptor.manifest.payloadKind,
                Available = descriptor.manifest,
                ArchiveSha256 = descriptor.archiveSha256,
                Installed = installed,
                Detail = "Checking installed package files."
            };
            Cache[key] = pending;
            Task.Run(async () =>
                {
                    await Workers.WaitAsync(cancellation);
                    try { return Verify(archiveAssetPath, descriptor, installed, availableFiles, installedFiles, retired, cancellation); }
                    finally { Workers.Release(); }
                }, cancellation)
                .ContinueWith(task => mainThread.Post(_ =>
                {
                    if (generation != _generation || !Cache.ContainsKey(key))
                        return;
                    Cache[key] = task.IsFaulted
                        ? new TextStudioPackageSnapshot { Status = TextStudioPackageStatus.Unknown, Detail = task.Exception?.GetBaseException().Message ?? "Package verification failed." }
                        : task.Result;
                    NotifyChanged();
                }, null));
            return pending;
        }

        private static bool HasMatchingIdentity(IntegrationPackageManifest installed, IntegrationPackageManifest available)
        {
            return installed != null && installed.packageId == available.packageId &&
                installed.ownerPackageName == available.ownerPackageName && installed.payloadKind == available.payloadKind;
        }

        private static List<FileInput> ResolveFiles(IntegrationPackageManifest manifest)
        {
            List<FileInput> files = new List<FileInput>(manifest.entries.Length);
            foreach (IntegrationPackageEntry entry in manifest.entries)
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                files.Add(new FileInput { Entry = entry, AssetPath = path, MetaPath = string.IsNullOrEmpty(path) ? string.Empty : path + ".meta" });
            }
            return files;
        }

        internal static TextStudioPackageSnapshot Verify(string archivePath, IntegrationPackageDescriptor descriptor, IntegrationPackageManifest installed, List<FileInput> availableFiles, List<FileInput> installedFiles, string[] retired, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            IntegrationPackageManifest available = descriptor.manifest;
            if (!HasMatchingIdentity(installed, available)) installed = null;
            TextStudioPackageSnapshot result = new TextStudioPackageSnapshot
            {
                PayloadKind = available.payloadKind,
                Available = available,
                ArchiveSha256 = descriptor.archiveSha256,
                Installed = installed
            };
            result.Relation = IntegrationPackageRevisionComparer.Compare(installed, available);
            result.Retired = retired;
            if (!File.Exists(archivePath) || !MatchesStableHash(archivePath, descriptor.archiveSha256))
            {
                result.Status = TextStudioPackageStatus.InvalidBundle;
                result.Detail = "The archive does not match its bundled update metadata.";
                return result;
            }

            cancellation.ThrowIfCancellationRequested();
            CompareFiles(availableFiles, cancellation, out string[] changed, out string[] missing);
            result.Changed = changed;
            result.Missing = missing;
            if (changed.Length == 0 && missing.Length == 0 && retired.Length == 0)
            {
                result.Status = TextStudioPackageStatus.Current;
                result.Detail = "Installed files match the bundled package.";
                return result;
            }

            if (changed.Length == 0 && missing.Length == 0 && retired.Length > 0)
            {
                result.Status = TextStudioPackageStatus.RetiredFiles;
                result.Detail = "Files from an earlier package remain. Review them before considering the update complete.";
                return result;
            }

            if (installed != null)
            {
                CompareFiles(installedFiles, cancellation, out string[] installedChanged, out string[] installedMissing);
                if (installedChanged.Length > 0)
                {
                    result.Status = TextStudioPackageStatus.LocalChanges;
                    result.Detail = "Installed package files differ from their import record. Review local changes before reimporting.";
                    return result;
                }
                if (installedMissing.Length > 0)
                {
                    result.Status = TextStudioPackageStatus.IncompleteImport;
                    result.Detail = "Installed package files are missing. Review the package before reimporting.";
                    return result;
                }

                IntegrationPackageRevisionRelation relation = result.Relation;
                result.Status = relation == IntegrationPackageRevisionRelation.AvailableNewer ? TextStudioPackageStatus.UpdateAvailable :
                    relation == IntegrationPackageRevisionRelation.InstalledNewer ? TextStudioPackageStatus.InstalledNewer :
                    relation == IntegrationPackageRevisionRelation.DifferentUnordered ? TextStudioPackageStatus.BundledPackageDiffers :
                    TextStudioPackageStatus.Unknown;
                result.Detail = relation == IntegrationPackageRevisionRelation.AvailableNewer
                    ? "A changed package is bundled with a newer product release."
                    : relation == IntegrationPackageRevisionRelation.InstalledNewer
                        ? "Installed files came from a newer product release."
                        : "The bundled and installed packages differ. Review their files before importing.";
                return result;
            }

            result.Status = TextStudioPackageStatus.Unknown;
            result.Detail = "This installation has no import record and does not match the bundled files. Review or reimport to establish its state.";
            return result;
        }

        private static void CompareFiles(List<FileInput> files, CancellationToken cancellation, out string[] changed, out string[] missing)
        {
            List<string> changedPaths = new List<string>();
            List<string> missingPaths = new List<string>();
            foreach (FileInput file in files)
            {
                cancellation.ThrowIfCancellationRequested();
                IntegrationPackageEntry entry = file.Entry;
                if (string.IsNullOrEmpty(file.AssetPath) || !File.Exists(file.MetaPath) || !entry.folder && !File.Exists(file.AssetPath))
                {
                    missingPaths.Add(entry.path);
                    continue;
                }

                if (!MatchesStableHash(file.MetaPath, entry.metaSha256) ||
                    !entry.folder && !MatchesStableHash(file.AssetPath, entry.assetSha256))
                    changedPaths.Add(entry.path);
            }
            changed = changedPaths.ToArray();
            missing = missingPaths.ToArray();
        }

        private static bool MatchesStableHash(string path, string expected)
        {
            FileInfo before = new FileInfo(path);
            long length = before.Length;
            DateTime writeTime = before.LastWriteTimeUtc;
            string actual = IntegrationPackageFingerprint.HashFile(path);
            FileInfo after = new FileInfo(path);
            if (length != after.Length || writeTime != after.LastWriteTimeUtc)
                throw new IOException("Package file changed during verification: " + path);
            return string.Equals(actual, expected, StringComparison.Ordinal);
        }
    }
}
