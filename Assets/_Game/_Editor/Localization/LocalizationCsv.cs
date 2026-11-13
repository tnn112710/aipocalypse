using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace MB13.Aipocalypse.Editor.Localization
{
    // The CSVs in Data/Localization/Csv are the source of truth for the string tables: one file per table (file name =
    // table name), columns Key, Id, one per locale code (en, vi, fr…) and any others (Context) that the import ignores.
    // Importing makes the table match the file: adds, renames (same Id), updates and removes entries. A row without an Id
    // gets the new entry's Id written back into the file, so references survive a later key rename.
    public static class LocalizationCsv
    {
        public const string CsvFolder = "Assets/_Game/Data/Localization/Csv";
        public const string TablesFolder = "Assets/_Game/Data/Localization/Tables";
        public const string KeyColumn = "Key";
        public const string IdColumn = "Id";
        const string LogTag = "[Localization]";

        [MenuItem("Aipocalypse/Localization/Import All CSVs")]
        static void ImportAllMenu() => Debug.Log($"{LogTag} {ImportAll()}");

        public static string ImportAll() =>
            string.Join("\n", CsvPaths().Select(Import).DefaultIfEmpty("no CSV in " + CsvFolder));

        public static IEnumerable<string> CsvPaths() =>
            Directory.Exists(CsvFolder)
                ? Directory.GetFiles(CsvFolder, "*.csv").Select(p => p.Replace('\\', '/')).OrderBy(p => p)
                : Enumerable.Empty<string>();

        public static bool IsTableCsv(string assetPath) =>
            assetPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            Path.GetDirectoryName(assetPath)?.Replace('\\', '/') == CsvFolder;

        public static string TableName(string csvPath) => Path.GetFileNameWithoutExtension(csvPath);

        // Makes the table named after the file match it. Returns a one-line report.
        public static string Import(string csvPath)
        {
            string name = TableName(csvPath);
            List<string[]> rows = Parse(File.ReadAllText(csvPath));
            if (rows.Count == 0)
                return $"{name}: empty file, skipped";
            Header header = Header.Read(rows[0]);
            if (header.Key < 0)
                return $"{name}: no '{KeyColumn}' column, skipped";

            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(name) ??
                                               LocalizationEditorSettings.CreateStringTableCollection(name, TablesFolder);
            var tables = new Dictionary<int, StringTable>();
            foreach ((int column, Locale locale) in header.Locales)
                tables[column] = (StringTable)(collection.GetTable(locale.Identifier) ?? collection.AddNewTable(locale.Identifier));
            foreach (StringTable table in tables.Values)
                if (!LocalizationEditorSettings.GetPreloadTableFlag(table))
                    LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            SharedTableData shared = collection.SharedData;

            var errors = new List<string>();
            var seen = new HashSet<long>();
            bool idsWritten = false, changed = false;
            int added = 0, renamed = 0, updated = 0;
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string key = Cell(row, header.Key).Trim();
                if (key.Length == 0)
                    continue;
                long.TryParse(Cell(row, header.Id).Trim(), out long id);

                SharedTableData.SharedTableEntry entry = id != 0 ? shared.GetEntry(id) : null;
                entry ??= shared.GetEntry(key);
                if (entry != null && seen.Contains(entry.Id))
                {
                    errors.Add($"line {r + 1}: '{key}' repeats an earlier key or Id");
                    continue;
                }
                if (entry == null)
                {
                    entry = id != 0 ? shared.AddKey(key, id) : shared.AddKey(key);
                    added++;
                    changed = true;
                }
                else if (entry.Key != key)
                {
                    SharedTableData.SharedTableEntry other = shared.GetEntry(key);
                    if (other != null && other.Id != entry.Id)
                    {
                        errors.Add($"line {r + 1}: can't rename '{entry.Key}' to '{key}', another entry has that key");
                        continue;
                    }
                    shared.RenameKey(entry.Id, key);
                    renamed++;
                    changed = true;
                }
                seen.Add(entry.Id);

                if (header.Id >= 0 && Cell(row, header.Id).Trim() != entry.Id.ToString())
                {
                    rows[r] = WithCell(row, header.Id, entry.Id.ToString());
                    idsWritten = true;
                }

                foreach ((int column, StringTable table) in tables)
                {
                    string value = Cell(row, column);
                    StringTableEntry localized = table.GetEntry(entry.Id);
                    if (value.Length == 0)
                    {
                        if (localized != null)
                        {
                            table.RemoveEntry(entry.Id);
                            changed = true;
                        }
                        continue;
                    }
                    if (localized == null)
                        table.AddEntry(entry.Id, value);
                    else if (localized.Value != value || localized.IsSmart)
                    {
                        localized.Value = value;
                        localized.IsSmart = false;
                    }
                    else
                        continue;
                    updated++;
                    changed = true;
                }
            }

            var stale = shared.Entries.Where(e => !seen.Contains(e.Id)).Select(e => e.Id).ToList();
            foreach (long id in stale)
                collection.RemoveEntry(id);
            changed |= stale.Count > 0;

            if (changed)
            {
                EditorUtility.SetDirty(collection);
                EditorUtility.SetDirty(shared);
                foreach (StringTable table in tables.Values)
                    EditorUtility.SetDirty(table);
                LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
                AssetDatabase.SaveAssetIfDirty(shared);
                AssetDatabase.SaveAssetIfDirty(collection);
                foreach (StringTable table in tables.Values)
                    AssetDatabase.SaveAssetIfDirty(table);
            }
            if (idsWritten)
                WriteBack(csvPath, rows);

            string report = $"{name}: {seen.Count} keys ({added} added, {renamed} renamed, {stale.Count} removed, {updated} texts set)" +
                            (idsWritten ? ", Ids written back" : string.Empty);
            if (errors.Count > 0)
            {
                report += $"\n  {errors.Count} error(s): " + string.Join("; ", errors);
                Debug.LogError($"{LogTag} {csvPath}: {string.Join("; ", errors)}");
            }
            return report;
        }

        // Differences between a CSV and its table (empty = in sync). For tests and checks.
        public static List<string> Compare(string csvPath)
        {
            var diffs = new List<string>();
            string name = TableName(csvPath);
            List<string[]> rows = Parse(File.ReadAllText(csvPath));
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(name);
            if (collection == null || rows.Count == 0)
            {
                diffs.Add($"{name}: no table (or empty CSV), run Aipocalypse > Localization > Import All CSVs");
                return diffs;
            }
            Header header = Header.Read(rows[0]);
            int keys = 0;
            foreach (string[] row in rows.Skip(1))
            {
                string key = Cell(row, header.Key).Trim();
                if (key.Length == 0)
                    continue;
                keys++;
                SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(key);
                if (entry == null)
                {
                    diffs.Add($"{name}/{key}: not in the table");
                    continue;
                }
                if (header.Id >= 0 && Cell(row, header.Id).Trim() != entry.Id.ToString())
                    diffs.Add($"{name}/{key}: Id {Cell(row, header.Id)} in the CSV, {entry.Id} in the table");
                foreach ((int column, Locale locale) in header.Locales)
                {
                    var table = collection.GetTable(locale.Identifier) as StringTable;
                    string inTable = table?.GetEntry(entry.Id)?.Value ?? string.Empty;
                    if (inTable != Cell(row, column))
                        diffs.Add($"{name}/{key} [{locale.Identifier.Code}]: CSV '{Cell(row, column)}', table '{inTable}'");
                }
            }
            if (collection.SharedData.Entries.Count != keys)
                diffs.Add($"{name}: {keys} keys in the CSV, {collection.SharedData.Entries.Count} in the table");
            return diffs;
        }

        // RFC 4180: comma-separated, quoted cells may hold commas, quotes ("") and line breaks.
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, any = false;
            int i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
            for (; i < text.Length; i++)
            {
                char c = text[i];
                any = true;
                if (quoted)
                {
                    if (c != '"')
                        cell.Append(c);
                    else if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                        quoted = false;
                    continue;
                }
                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(row.ToArray());
                        row.Clear();
                        any = false;
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }
            if (any || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }

        public static string Write(IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            foreach (string[] row in rows)
            {
                for (int c = 0; c < row.Length; c++)
                {
                    if (c > 0)
                        sb.Append(',');
                    string cell = row[c] ?? string.Empty;
                    bool quote = cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || cell != cell.Trim();
                    sb.Append(quote ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static void WriteBack(string csvPath, List<string[]> rows)
        {
            string text = Write(rows);
            if (text == File.ReadAllText(csvPath))
                return;
            // Unity re-imports the file on its next refresh, and that import finds every Id in place (no change).
            File.WriteAllText(csvPath, text, new UTF8Encoding(false));
        }

        static string Cell(string[] row, int column) => column >= 0 && column < row.Length ? row[column] : string.Empty;

        static string[] WithCell(string[] row, int column, string value)
        {
            string[] copy = row.Length > column ? (string[])row.Clone() : row.Concat(new string[column + 1 - row.Length]).ToArray();
            copy[column] = value;
            return copy;
        }

        readonly struct Header
        {
            public readonly int Key;
            public readonly int Id;
            public readonly List<(int column, Locale locale)> Locales;

            Header(int key, int id, List<(int, Locale)> locales)
            {
                Key = key;
                Id = id;
                Locales = locales;
            }

            public static Header Read(string[] cells)
            {
                var locales = new List<(int, Locale)>();
                ReadOnlyCollection<Locale> known = LocalizationEditorSettings.GetLocales();
                for (int c = 0; c < cells.Length; c++)
                {
                    string code = cells[c].Trim();
                    Locale locale = known.FirstOrDefault(l => l.Identifier.Code == code);
                    if (locale != null)
                        locales.Add((c, locale));
                }
                return new Header(Array.IndexOf(cells.Select(h => h.Trim()).ToArray(), KeyColumn),
                    Array.IndexOf(cells.Select(h => h.Trim()).ToArray(), IdColumn), locales);
            }
        }
    }

    sealed class LocalizationCsvPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported.Where(LocalizationCsv.IsTableCsv))
                Debug.Log("[Localization] " + LocalizationCsv.Import(path));
        }
    }
}
