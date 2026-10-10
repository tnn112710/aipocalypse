using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;

namespace TextStudio.Editor
{
    /// <summary>Shared metadata for help, browser, insertion and selectors. Never compiles styles or searches assets.</summary>
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    // Authoring metadata has explicit asset invalidation and editor shutdown cleanup.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioAppearanceAuthoringMetadata
    {
        sealed class Scope
        {
            internal int Start;
            internal int End;
            internal string ClosingName;
            internal TextStudioAppearanceDefinitionTable Table;
        }

        sealed class MetadataCache
        {
            internal readonly Dictionary<TextStudioAppearanceDefinition, TextStudioAppearanceDefinitionTable> Tables = new Dictionary<TextStudioAppearanceDefinition, TextStudioAppearanceDefinitionTable>();
            internal readonly Dictionary<TextStudioAppearanceDefinitionTable, CachedTable> Items = new Dictionary<TextStudioAppearanceDefinitionTable, CachedTable>();
        }

        sealed class CachedTable
        {
            internal int Identity;
            internal TextStudioTagBrowserItem[] Items;
        }

        sealed class ScopeInterval
        {
            internal int Start;
            internal Composition Composition;
        }

        sealed class Composition
        {
            internal IReadOnlyList<TextStudioTagBrowserItem> Items;
            internal TextStudioTagAnalysisContext.SuggestionSnapshot Suggestions;
        }

        sealed class ClosingInterval
        {
            internal int Start;
            internal IReadOnlyList<TextStudioTagDiagnostic> Tags;
            internal bool IsNoParse;
        }

        sealed class ScopeBoundary
        {
            internal int Position;
            internal bool Opening;
            internal Scope Scope;
        }

        sealed class Snapshot
        {
            internal readonly List<Scope> Scopes = new List<Scope>();
            internal readonly List<ScopeInterval> Intervals = new List<ScopeInterval>();
            internal readonly List<ClosingInterval> Closings = new List<ClosingInterval>();
            internal readonly List<(int Start, int End)> Comments = new List<(int, int)>();
            internal readonly Dictionary<string, Composition> Compositions = new Dictionary<string, Composition>(StringComparer.Ordinal);
            internal TextStudioAppearanceDefinitionTable Root;
            internal TextStudioDiagnosticReport Report;
            internal MetadataCache Metadata;
        }

        static ConditionalWeakTable<TextStudioTagAnalysisContext, Snapshot> _contexts = new ConditionalWeakTable<TextStudioTagAnalysisContext, Snapshot>();
        static IReadOnlyList<TextStudioTagBrowserItem> _globals;
        static readonly Dictionary<string, TextStudioAppearanceDefinition> GlobalDefinitions = new Dictionary<string, TextStudioAppearanceDefinition>(StringComparer.OrdinalIgnoreCase);
        static ulong _globalRegistry;
        static int _globalGeneration = -1;
        internal static int Generation { get; private set; }
        internal static int SourceAnalysisCount { get; private set; }
        internal static int MetadataBuildCount { get; private set; }

        static TextStudioAppearanceAuthoringMetadata()
        {
            Undo.undoRedoPerformed += Invalidate;
            Undo.postprocessModifications += OnModifications;
            EditorApplication.projectChanged += Invalidate;
            TextStudio2DStyleAsset.Changed += OnStyleChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
        }

        internal static void Invalidate() { unchecked { Generation++; } _globals = null; }
        static void OnStyleChanged(TextStudio2DStyleAsset _) => Invalidate();
        static void Clear() { _contexts = new ConditionalWeakTable<TextStudioTagAnalysisContext, Snapshot>(); _globals = null; }
        static UndoPropertyModification[] OnModifications(UndoPropertyModification[] changes)
        {
            foreach (UndoPropertyModification change in changes)
            {
                UnityEngine.Object target = change.currentValue.target;
                string path = change.currentValue.propertyPath ?? string.Empty;
                if (target is TextStudio2DStyleAsset || target is TextStudioProjectSettings || target is TextStudio2DPaintAsset || path.IndexOf("appearance", StringComparison.OrdinalIgnoreCase) >= 0)
                { Invalidate(); break; }
            }
            return changes;
        }

        internal static ulong RegistryRevision
        {
            get
            {
                unchecked
                {
                    ulong hash = TextStudioRegistry.AppearanceStyles.Revision;
                    hash = hash * 397 ^ TextStudioRegistry.Effects.Revision;
                    hash = hash * 397 ^ TextStudioRegistry.Actions.Revision;
                    hash = hash * 397 ^ TextStudioRegistry.Curves.Revision;
                    hash = hash * 397 ^ TextStudioRegistry.Timings.Revision;
                    hash = hash * 397 ^ TextStudioRegistry.Playbacks.Revision;
                    hash = hash * 397 ^ TextStudioRegistry.TagExpansions.Revision;
                    hash = hash * 397 ^ TextStudioRegistry.ExtensionTags.Revision;
                    return hash;
                }
            }
        }

        internal static void Bind(TextStudioTagAnalysisContext context, TextStudio2DStyleSettings settings, string source, TextStudioDiagnosticReport report = null, TextStudioTagAnalysisContext previous = null)
        {
            source ??= string.Empty;
            Snapshot prior = null;
            if (previous != null) _contexts.TryGetValue(previous, out prior);
            Snapshot snapshot = new Snapshot { Root = prior?.Root ?? TextStudioAppearanceDefinitions.Resolve(settings), Metadata = prior?.Metadata ?? new MetadataCache() };
            Globals();
            report ??= TextStudioMarkupDiagnostics.Analyze(source, context);
            SourceAnalysisCount++;
            snapshot.Report = report;
            List<Scope> stack = new List<Scope>();
            foreach (TextStudioTagDiagnostic tag in report.Tags)
            {
                if (tag.IsClosing)
                {
                    if (tag.Kind != TextStudioTagKind.Appearance) continue;
                    for (int i = stack.Count - 1; i >= 0; i--)
                    {
                        if (!string.IsNullOrEmpty(tag.Name) && !string.Equals(tag.Name, stack[i].ClosingName, StringComparison.OrdinalIgnoreCase)) continue;
                        stack[i].End = tag.SourceStart;
                        stack.RemoveAt(i);
                        break;
                    }
                    continue;
                }
                if (tag.Kind != TextStudioTagKind.Appearance || IsSelfClosing(source, tag)) continue;
                TextStudioTagParameters parameters = TextStudioTagParameterParser.Parse(tag.RawParameter, tag.HasParameter);
                if (!TextStudioAppearanceTagSyntax.TryClassify(tag.Name, false, parameters, out string id, out bool adHoc) || adHoc) continue;
                TextStudioAppearanceDefinition definition = null;
                for (int i = stack.Count - 1; i >= 0 && definition == null; i--) stack[i].Table?.TryGet(id, out definition);
                if (definition == null) snapshot.Root.TryGet(id, out definition);
                if (definition == null) GlobalDefinitions.TryGetValue(id, out definition);
                TextStudioAppearanceDefinitionTable table = null;
                if (definition != null && definition.IsValid && definition.IsEnabled && !snapshot.Metadata.Tables.TryGetValue(definition, out table))
                {
                    table = TextStudioAppearanceDefinitions.Resolve(definition.Settings);
                    snapshot.Metadata.Tables[definition] = table;
                }
                Scope scope = new Scope { Start = tag.SourceEnd, End = (source?.Length ?? 0) + 1, ClosingName = tag.Name, Table = table };
                snapshot.Scopes.Add(scope);
                stack.Add(scope);
            }
            BuildIntervals(snapshot, prior, context);
            _contexts.Remove(context);
            _contexts.Add(context, snapshot);
            context.SetSuggestionSnapshot(TextStudioTagSuggestionKeys.AppearanceStyle, snapshot.Intervals[0].Composition.Suggestions);
        }

        internal static TextStudioDiagnosticReport GetAnalysis(TextStudioTagAnalysisContext context, string source)
        {
            if (context == null) return TextStudioMarkupDiagnostics.Analyze(source);
            if (_contexts.TryGetValue(context, out Snapshot snapshot) && string.Equals(snapshot.Report.Source, source, StringComparison.Ordinal)) return snapshot.Report;
            Bind(context, default, source, previous: context);
            return _contexts.GetValue(context, _ => null).Report;
        }

        internal static bool IsLiteral(TextStudioTagAnalysisContext context, string source, int caret)
        {
            if (context == null) return false;
            GetAnalysis(context, source);
            if (!_contexts.TryGetValue(context, out Snapshot snapshot)) return false;
            int low = 0, high = snapshot.Comments.Count - 1;
            while (low <= high)
            {
                int middle = (low + high) / 2;
                (int start, int end) = snapshot.Comments[middle];
                if (caret < start) high = middle - 1;
                else if (caret >= end) low = middle + 1;
                else return true;
            }
            low = 0; high = snapshot.Closings.Count - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (snapshot.Closings[middle].Start <= caret) low = middle;
                else high = middle - 1;
            }
            return snapshot.Closings.Count > 0 && snapshot.Closings[low].IsNoParse;
        }

        internal static IReadOnlyList<TextStudioTagDiagnostic> OpenTags(TextStudioTagAnalysisContext context, string source, int caret)
        {
            if (context == null) return Array.Empty<TextStudioTagDiagnostic>();
            GetAnalysis(context, source);
            if (!_contexts.TryGetValue(context, out Snapshot snapshot) || snapshot.Closings.Count == 0) return Array.Empty<TextStudioTagDiagnostic>();
            int low = 0, high = snapshot.Closings.Count - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (snapshot.Closings[middle].Start <= caret) low = middle;
                else high = middle - 1;
            }
            return snapshot.Closings[low].Tags;
        }

        internal static IReadOnlyList<TextStudioTagBrowserItem> AtCaret(TextStudioTagAnalysisContext context, int caret)
        {
            if (context == null || !_contexts.TryGetValue(context, out Snapshot snapshot) || snapshot.Intervals.Count == 0) return Globals();
            int low = 0, high = snapshot.Intervals.Count - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (snapshot.Intervals[middle].Start <= caret) low = middle;
                else high = middle - 1;
            }
            return snapshot.Intervals[low].Composition.Items;
        }

        static void BuildIntervals(Snapshot snapshot, Snapshot prior, TextStudioTagAnalysisContext context)
        {
            string source = snapshot.Report.Source;
            for (int position = 0; position < source.Length;)
            {
                int start = source.IndexOf("<!--", position, StringComparison.Ordinal);
                if (start < 0) break;
                if (TextStudioTagExpansionUtility.IsEscaped(source, start)) { position = start + 4; continue; }
                int close = source.IndexOf("-->", start + 4, StringComparison.Ordinal);
                int end = close < 0 ? source.Length + 1 : close + 3;
                snapshot.Comments.Add((start, end));
                position = end;
            }
            List<ScopeBoundary> boundaries = new List<ScopeBoundary>();
            foreach (Scope scope in snapshot.Scopes)
            {
                boundaries.Add(new ScopeBoundary { Position = scope.Start, Opening = true, Scope = scope });
                boundaries.Add(new ScopeBoundary { Position = scope.End, Opening = false, Scope = scope });
            }
            boundaries.Sort((a, b) => a.Position.CompareTo(b.Position));
            List<Scope> active = new List<Scope>();
            snapshot.Intervals.Add(new ScopeInterval { Start = 0, Composition = Compose(snapshot, prior, active) });
            for (int i = 0; i < boundaries.Count;)
            {
                int position = boundaries[i].Position;
                do
                {
                    ScopeBoundary boundary = boundaries[i++];
                    if (boundary.Opening) active.Add(boundary.Scope);
                    else active.Remove(boundary.Scope);
                } while (i < boundaries.Count && boundaries[i].Position == position);
                active.Sort((a, b) => a.Start.CompareTo(b.Start));
                snapshot.Intervals.Add(new ScopeInterval { Start = position, Composition = Compose(snapshot, prior, active) });
            }
            List<TextStudioTagDiagnostic> open = new List<TextStudioTagDiagnostic>();
            snapshot.Closings.Add(new ClosingInterval { Start = 0, Tags = Array.Empty<TextStudioTagDiagnostic>() });
            foreach (TextStudioTagDiagnostic tag in snapshot.Report.Tags)
            {
                bool changed = false;
                if (tag.IsClosing)
                {
                    for (int i = open.Count - 1; i >= 0; i--)
                    {
                        if (!string.IsNullOrEmpty(tag.Name) && (!string.Equals(TextStudioRangeLedger.CanonicalCloseName(tag.Name), TextStudioRangeLedger.CanonicalCloseName(open[i].Name), StringComparison.OrdinalIgnoreCase) || tag.Kind != open[i].Kind)) continue;
                        open.RemoveAt(i); changed = true; break;
                    }
                }
                else if (!IsSelfClosing(source, tag) && (tag.Kind == TextStudioTagKind.Appearance || context.Catalog.TryGetDefinition(tag.Name, tag.HasParameter, tag.Kind, out TextStudioTagDefinition definition) && definition.Shape == TextStudioTagShape.Range))
                { open.Add(tag); changed = true; }
                if (!changed) continue;
                TextStudioTagDiagnostic[] names = open.ToArray();
                Array.Reverse(names);
                bool noParse = false;
                foreach (TextStudioTagDiagnostic range in names) if (string.Equals(range.Name, "noparse", StringComparison.OrdinalIgnoreCase)) { noParse = true; break; }
                snapshot.Closings.Add(new ClosingInterval { Start = tag.SourceEnd, Tags = names, IsNoParse = noParse });
            }
        }

        static bool IsSelfClosing(string source, TextStudioTagDiagnostic tag)
        {
            int position = tag.SourceEnd - 2;
            while (position > tag.SourceStart && char.IsWhiteSpace(source[position])) position--;
            return position > tag.SourceStart && source[position] == '/';
        }

        static Composition Compose(Snapshot snapshot, Snapshot prior, List<Scope> active)
        {
            List<CachedTable> tables = new List<CachedTable>();
            StringBuilder key = new StringBuilder();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].Table == null) continue;
                CachedTable table = GetTable(snapshot.Metadata, active[i].Table, "Referenced style");
                tables.Add(table); key.Append(table.Identity).Append('/');
            }
            CachedTable root = GetTable(snapshot.Metadata, snapshot.Root, "Inline");
            tables.Add(root); key.Append(root.Identity);
            string identity = key.ToString();
            if (snapshot.Compositions.TryGetValue(identity, out Composition existing)) return existing;
            if (prior != null && prior.Compositions.TryGetValue(identity, out existing))
            { snapshot.Compositions[identity] = existing; return existing; }
            List<TextStudioTagBrowserItem> result = new List<TextStudioTagBrowserItem>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tables.Count; i++) AddItems(result, ids, tables[i].Items, i);
            AddGlobals(result, ids, tables.Count);
            List<string> names = new List<string>(result.Count);
            foreach (TextStudioTagBrowserItem item in result) names.Add(item.Definition.Id.TrimStart('$'));
            Composition composition = new Composition { Items = result, Suggestions = new TextStudioTagAnalysisContext.SuggestionSnapshot(names) };
            snapshot.Compositions[identity] = composition;
            return composition;
        }

        static CachedTable GetTable(MetadataCache metadata, TextStudioAppearanceDefinitionTable table, string origin)
        {
            if (metadata.Items.TryGetValue(table, out CachedTable cached)) return cached;
            List<TextStudioAppearanceDefinition> sorted = new List<TextStudioAppearanceDefinition>(table.Definitions);
            sorted.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id));
            TextStudioTagBrowserItem[] items = new TextStudioTagBrowserItem[sorted.Count];
            for (int i = 0; i < sorted.Count; i++) items[i] = CreateItem(sorted[i], origin, 0, TextStudioRegistry.AppearanceStyles.TryGetQuiet(sorted[i].Id, out _));
            cached = new CachedTable { Identity = metadata.Items.Count, Items = items };
            metadata.Items.Add(table, cached); MetadataBuildCount++;
            return cached;
        }

        static void AddItems(List<TextStudioTagBrowserItem> result, HashSet<string> ids, IReadOnlyList<TextStudioTagBrowserItem> items, int order)
        {
            foreach (TextStudioTagBrowserItem item in items)
            {
                if (!ids.Add(item.Definition.Id.TrimStart('$'))) continue;
                result.Add(new TextStudioTagBrowserItem(item.Definition, item.GroupId, item.GroupDisplayName, item.SortName, item.SearchText, item.Example, item.ParameterSummary, item.Category, order));
            }
        }

        internal static IReadOnlyList<TextStudioTagBrowserItem> ForSettings(TextStudio2DStyleSettings settings)
        {
            MetadataCache metadata = new MetadataCache();
            List<TextStudioTagBrowserItem> result = new List<TextStudioTagBrowserItem>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddItems(result, ids, GetTable(metadata, TextStudioAppearanceDefinitions.Resolve(settings), "Inline").Items, 0);
            AddGlobals(result, ids, 1);
            return result;
        }

        static void AddGlobals(List<TextStudioTagBrowserItem> result, HashSet<string> ids, int order)
        {
            foreach (TextStudioTagBrowserItem item in Globals())
            {
                if (!ids.Add(item.Definition.Id.TrimStart('$'))) continue;
                result.Add(new TextStudioTagBrowserItem(item.Definition, item.GroupId, item.GroupDisplayName, item.SortName, item.SearchText, item.Example, item.ParameterSummary, item.Category, order));
            }
        }

        static IReadOnlyList<TextStudioTagBrowserItem> Globals()
        {
            if (_globals != null && _globalGeneration == Generation && _globalRegistry == RegistryRevision) return _globals;
            MetadataBuildCount++;
            List<TextStudioTagBrowserItem> result = new List<TextStudioTagBrowserItem>();
            GlobalDefinitions.Clear();
            foreach (KeyValuePair<string, TextStudio2DStyleAsset> pair in TextStudioRegistry.AppearanceStyles.GetAll())
            {
                if (pair.Value == null) continue;
                string origin = TextStudioRegistry.AppearanceStyles.IsRuntime(pair.Key, pair.Value) ? "Runtime" : "Project catalog";
                TextStudioAppearanceDefinition definition = TextStudioAppearanceDefinitions.FromAsset(pair.Value);
                GlobalDefinitions[pair.Key] = definition;
                result.Add(CreateItem(definition, origin, 0, false));
            }
            result.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SortName, b.SortName));
            _globals = result;
            _globalGeneration = Generation;
            _globalRegistry = RegistryRevision;
            return result;
        }

        static TextStudioTagBrowserItem CreateItem(TextStudioAppearanceDefinition appearance, string origin, int order, bool shadows)
        {
            string status = !appearance.IsEnabled ? "Disabled; shadows outer definitions." : !appearance.IsValid ? appearance.Diagnostic : appearance.DefaultApplyMode + ".";
            string description = origin + (shadows ? " · overrides project/runtime definition. " : ". ") + status;
            List<TextStudioTagParameterDefinition> parameters = new List<TextStudioTagParameterDefinition>
            {
                new TextStudioTagParameterDefinition("mode", TextStudioTagParameterKind.Enum, allowedValues: new[] { "replace", "overlay" }, defaultValue: appearance.DefaultApplyMode.ToString().ToLowerInvariant(), description: "Override composition for this occurrence."),
                new TextStudioTagParameterDefinition("tint", TextStudioTagParameterKind.Color, defaultValue: "#FFFFFFFF", description: "Multiply the complete invocation color."),
                new TextStudioTagParameterDefinition("opacity", TextStudioTagParameterKind.Float, defaultValue: "1", minimum: 0, maximum: 1, description: "Multiply the composed invocation alpha once.")
            };
            TextStudio2DStyleSettings authoredSettings = appearance.Settings;
            TextStudio2DStyleSettings settings = TextStudio2DStyleCompiler.FlattenAuthoredSettings(in authoredSettings);
            HashSet<string> parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TextStudioAppearanceExposure exposure in settings.Exposures)
            {
                if (!TextStudioAppearanceExposure.IsValidName(exposure.Name) || !parameterNames.Add(exposure.Name) || !TextStudioAppearanceProperties.TryGet(exposure.Property, out TextStudioAppearancePropertyDescriptor descriptor))
                {
                    description += " Parameter '" + exposure.Name + "' has an invalid binding.";
                    continue;
                }
                string authored = string.Empty;
                bool available = false;
                for (int i = 0; i < settings.ItemCount; i++)
                {
                    TextStudio2DLayer layer = settings.GetLayer(i);
                    if (layer.ItemId != exposure.LayerId || !TextStudioAppearanceProperties.TryGetTarget(layer, exposure.TreatmentId, out TextStudio2DLayer target)) continue;
                    available = TextStudioAppearanceProperties.TryRead(target, exposure.Property, out authored);
                    break;
                }
                if (!available)
                {
                    description += " Parameter '" + exposure.Name + "' has an unavailable target. Repair its binding in the style inspector.";
                    continue;
                }
                string[] allowed = new string[descriptor.EnumValues.Count];
                for (int i = 0; i < allowed.Length; i++) allowed[i] = descriptor.EnumValues[i];
                TextStudioTagParameterKind kind = descriptor.ValueType switch
                {
                    TextStudioAppearanceValueType.Color => TextStudioTagParameterKind.Color,
                    TextStudioAppearanceValueType.Float => TextStudioTagParameterKind.Float,
                    TextStudioAppearanceValueType.Integer => TextStudioTagParameterKind.Integer,
                    TextStudioAppearanceValueType.Boolean => TextStudioTagParameterKind.Boolean,
                    _ => TextStudioTagParameterKind.Enum
                };
                parameters.Add(new TextStudioTagParameterDefinition(exposure.Name, kind, displayName: exposure.Name, description: descriptor.Label + (string.IsNullOrEmpty(descriptor.Unit) ? "" : " (" + descriptor.Unit + ")"), allowedValues: allowed, defaultValue: authored));
            }
            string snippet = CreateSnippet(appearance.Id);
            if (string.IsNullOrEmpty(snippet)) description += " Both appearance spellings are occupied by existing registrations; this style cannot be inserted.";
            else if (snippet.StartsWith("<style", StringComparison.Ordinal)) description += " Use the style ref spelling because the compact name is already registered.";
            TextStudioTagDefinition definition = new TextStudioTagDefinition("$" + appearance.Id, TextStudioTagKind.Appearance, TextStudioTagCategories.Appearance, displayName: appearance.Id + " (2D)", description: description, ownerId: origin, ownerDisplayName: origin, examples: new[] { snippet }, parameters: parameters.ToArray(), supportedRenderDomains: TextStudioRenderDomain.FlatText);
            return new TextStudioTagBrowserItem(definition, TextStudioTagCategories.Appearance, origin, appearance.Id, appearance.Id + " " + origin + " " + description, snippet, parameters.Count + " parameters", TextStudioTagCategory.Appearance, order);
        }

        internal static string CreateSnippet(string id)
        {
            string opening = TextStudioAppearanceTagSyntax.GetPreferredOpeningTag(id);
            if (opening == null) return string.Empty;
            return opening + "text" + (opening.StartsWith("<$", StringComparison.Ordinal) ? "</$" + id + ">" : "</style>");
        }
    }
}
