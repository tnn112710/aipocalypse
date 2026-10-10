using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TextStudio.Editor
{
    internal readonly struct TextStudioTagBrowserGroup
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int SortOrder;

        public TextStudioTagBrowserGroup(string id, string displayName, int sortOrder)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
            SortOrder = sortOrder;
        }
    }

    internal readonly struct TextStudioTagBrowserItem
    {
        public readonly TextStudioTagDefinition Definition;
        public readonly string GroupId;
        public readonly string GroupDisplayName;
        public readonly string SortName;
        public readonly string SearchText;
        public readonly string Example;
        public readonly string ParameterSummary;
        public readonly TextStudioTagCategory Category;
        public readonly int ScopeOrder;
        public readonly string Identity;

        public TextStudioTagBrowserItem(
            TextStudioTagDefinition definition,
            string groupId,
            string groupDisplayName,
            string sortName,
            string searchText,
            string example,
            string parameterSummary,
            TextStudioTagCategory category,
            int scopeOrder = 0)
        {
            Definition = definition;
            GroupId = groupId ?? string.Empty;
            GroupDisplayName = groupDisplayName ?? string.Empty;
            SortName = sortName ?? string.Empty;
            SearchText = searchText ?? string.Empty;
            Example = example ?? string.Empty;
            ParameterSummary = parameterSummary ?? string.Empty;
            Category = category;
            ScopeOrder = scopeOrder;
            Identity = definition != null ? ((int)definition.SupportedRenderDomains) + ":" + definition.Id : string.Empty;
        }

        public string Tag => Definition != null ? Definition.Id : string.Empty;
    }

#if UNITY_6000_7_OR_NEWER
    // Editor metadata caches are invalidated by catalog and registry revisions.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioTagBrowserModel
    {
        public const string AllGroupId = "All";
        static readonly ConditionalWeakTable<TextStudioTagCatalog, Dictionary<int, TextStudioTagBrowserModel>> Cache = new ConditionalWeakTable<TextStudioTagCatalog, Dictionary<int, TextStudioTagBrowserModel>>();
        static TextStudioTagAnalysisContext _defaultContext;
        static int _defaultGeneration = -1;
        static ulong _defaultRevision;
        internal static int BuildCount { get; private set; }

        static TextStudioTagAnalysisContext DefaultContext()
        {
            ulong revision = TextStudioAppearanceAuthoringProviders.RegistryRevision;
            if (_defaultContext == null || _defaultGeneration != TextStudioAppearanceAuthoringProviders.Generation || _defaultRevision != revision)
            {
                _defaultContext = new TextStudioTagAnalysisContext();
                _defaultGeneration = TextStudioAppearanceAuthoringProviders.Generation;
                _defaultRevision = TextStudioAppearanceAuthoringProviders.RegistryRevision;
            }
            return _defaultContext;
        }

        readonly TextStudioTagBrowserGroup[] _groups;
        readonly TextStudioTagBrowserItem[] _items;

        TextStudioTagBrowserModel(TextStudioTagBrowserGroup[] groups, TextStudioTagBrowserItem[] items)
        {
            _groups = groups ?? Array.Empty<TextStudioTagBrowserGroup>();
            _items = items ?? Array.Empty<TextStudioTagBrowserItem>();
        }

        public IReadOnlyList<TextStudioTagBrowserGroup> Groups => _groups;
        public IReadOnlyList<TextStudioTagBrowserItem> Items => _items;

        public static TextStudioTagBrowserModel Build()
        {
            return Build(null);
        }

        public static TextStudioTagBrowserModel Build(TextStudioTagAnalysisContext context)
        {
            return Build(context, TextStudioAuthoringCapabilities.AvailableRenderDomains);
        }

        internal static TextStudioTagBrowserModel Build(
            TextStudioTagAnalysisContext context,
            TextStudioRenderDomain availableRenderDomains)
        {
            context ??= DefaultContext();
            TextStudioTagCatalog catalog = context.Catalog;
            int key = (int)availableRenderDomains * 397 ^ (int)context.RenderDomains * 17 ^ (int)context.Backend;
            Dictionary<int, TextStudioTagBrowserModel> cached = Cache.GetOrCreateValue(catalog);
            if (cached.TryGetValue(key, out TextStudioTagBrowserModel existing)) return existing;
            BuildCount++;

            Dictionary<string, TextStudioTagBrowserGroup> groupMap =
                new Dictionary<string, TextStudioTagBrowserGroup>(StringComparer.OrdinalIgnoreCase);
            List<TextStudioTagBrowserItem> items = new List<TextStudioTagBrowserItem>();

            AddGroup(groupMap, AllGroupId, AllGroupId, 0);

            IReadOnlyList<TextStudioTagDefinition> definitions = catalog.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                TextStudioTagDefinition definition = TextStudioAppearanceAuthoringProviders.ForContext(definitions[i], context);
                if (!IsVisibleForRenderDomains(
                        definition,
                        context,
                        availableRenderDomains))
                {
                    continue;
                }
                string groupId = ResolveGroupId(definition);
                string groupDisplayName = ResolveGroupDisplayName(definition, groupId);
                AddGroup(groupMap, groupId, groupDisplayName, GetGroupSortOrder(definition, groupId));
                items.Add(CreateItem(definition, groupId, groupDisplayName));
            }

            foreach (TextStudioTagBrowserItem item in TextStudioAppearanceAuthoringProviders.AtCaret(context, 0))
            {
                if (!IsVisibleForRenderDomains(item.Definition, context, availableRenderDomains)) continue;
                AddGroup(groupMap, TextStudioTagCategories.Appearance, "Appearance Styles", 15);
                items.Add(item);
            }
            List<TextStudioTagBrowserGroup> groups = new List<TextStudioTagBrowserGroup>(groupMap.Values);
            groups.Sort(CompareGroups);
            items.Sort(CompareItems);
            TextStudioTagBrowserModel model = new TextStudioTagBrowserModel(groups.ToArray(), items.ToArray());
            cached[key] = model;
            return model;
        }

        internal static TextStudioTagBrowserModel ForCaret(TextStudioTagAnalysisContext context, int caret)
        {
            TextStudioTagBrowserModel baseline = Build(context);
            List<TextStudioTagBrowserItem> items = new List<TextStudioTagBrowserItem>();
            foreach (TextStudioTagBrowserItem item in baseline.Items)
                if (item.Definition.Kind != TextStudioTagKind.Appearance || !item.Definition.Id.StartsWith("$", StringComparison.Ordinal)) items.Add(item);
            foreach (TextStudioTagBrowserItem item in TextStudioAppearanceAuthoringProviders.AtCaret(context, caret))
                if (IsVisibleForRenderDomains(item.Definition, context, TextStudioAuthoringCapabilities.AvailableRenderDomains)) items.Add(item);
            return new TextStudioTagBrowserModel(baseline._groups, items.ToArray());
        }

        static bool IsVisibleForRenderDomains(
            TextStudioTagDefinition definition,
            TextStudioTagAnalysisContext context,
            TextStudioRenderDomain availableRenderDomains)
        {
            TextStudioRenderDomain visibleDomains = availableRenderDomains;
            if (context != null)
                visibleDomains &= context.RenderDomains;

            if (definition == null ||
                (definition.SupportedRenderDomains & visibleDomains) == TextStudioRenderDomain.None)
            {
                return false;
            }

            if (definition.Kind != TextStudioTagKind.TextStudioEffect ||
                !TextStudioRegistry.Effects.TryGetQuiet(
                    definition.Id,
                    out ITextStudioEffectDefinition effect))
            {
                return true;
            }

            return (TextStudioEffectCompatibilityResolver.GetSupportedDomains(effect) & visibleDomains) !=
                   TextStudioRenderDomain.None;
        }

        public IReadOnlyList<TextStudioTagBrowserItem> Filter(string groupId, string search)
        {
            bool allGroups = string.IsNullOrEmpty(groupId) ||
                             string.Equals(groupId, AllGroupId, StringComparison.OrdinalIgnoreCase);
            string needle = search != null ? search.Trim() : string.Empty;
            List<TextStudioTagBrowserItem> result = new List<TextStudioTagBrowserItem>();

            for (int i = 0; i < _items.Length; i++)
            {
                TextStudioTagBrowserItem item = _items[i];
                if (!allGroups && !string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!MatchesSearch(item, needle))
                    continue;

                result.Add(item);
            }

            return result;
        }

        static TextStudioTagBrowserItem CreateItem(TextStudioTagDefinition definition, string groupId, string groupDisplayName)
        {
            string searchText =
                definition.Id + "\n" +
                definition.DisplayName + "\n" +
                definition.Description + "\n" +
                definition.Category + "\n" +
                definition.OwnerDisplayName + "\n" +
                string.Join("\n", definition.Aliases);

            return new TextStudioTagBrowserItem(
                definition,
                groupId,
                groupDisplayName,
                definition.Id,
                searchText,
                TextStudioTagSourceEditor.CreateExample(definition),
                BuildParameterSummary(definition),
                ResolveSemanticCategory(definition));
        }

        static TextStudioTagCategory ResolveSemanticCategory(TextStudioTagDefinition definition)
        {
            if (definition == null)
                return TextStudioTagCategory.Unknown;

            if (definition.Kind == TextStudioTagKind.TextStudioEffect &&
                TextStudioRegistry.Effects.TryGetQuiet(definition.Id, out ITextStudioEffectDefinition effect) &&
                effect is TextStudioComposedEffectAsset)
            {
                return TextStudioTagCategory.ComposedEffect;
            }

            return definition.SemanticCategory;
        }

        static string BuildParameterSummary(TextStudioTagDefinition definition)
        {
            if (definition.Parameters.Count == 0)
            {
                switch (definition.ParameterRequirement)
                {
                    case TextStudioTagParameterRequirement.Required:
                        return "Parameter required";
                    case TextStudioTagParameterRequirement.Forbidden:
                        return "No parameter";
                    default:
                        return "No documented parameters";
                }
            }

            List<string> parts = new List<string>(definition.Parameters.Count);
            for (int i = 0; i < definition.Parameters.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = definition.Parameters[i];
                parts.Add(BuildParameterSummaryPart(parameter));
            }

            return string.Join(", ", parts);
        }

        static string BuildParameterSummaryPart(TextStudioTagParameterDefinition parameter)
        {
            string text = parameter.DisplayName;
            if (string.Equals(parameter.Name, "playback", StringComparison.OrdinalIgnoreCase) &&
                parameter.AllowedValues.Count > 0)
            {
                text += " (" + string.Join(", ", parameter.AllowedValues) + ")";
            }

            if (parameter.Required)
                text += " required";

            return text;
        }

        static bool MatchesSearch(TextStudioTagBrowserItem item, string needle)
        {
            return string.IsNullOrEmpty(needle) ||
                   item.SearchText.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string ResolveGroupId(TextStudioTagDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(definition.Category) &&
                definition.Category.StartsWith(TextStudioTagCategories.IntegrationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(definition.OwnerDisplayName))
                    return definition.OwnerDisplayName;
                if (!string.IsNullOrEmpty(definition.OwnerId))
                    return definition.OwnerId;
            }

            return definition.Category;
        }

        static string ResolveGroupDisplayName(TextStudioTagDefinition definition, string groupId)
        {
            if (definition != null &&
                !string.IsNullOrEmpty(definition.Category) &&
                definition.Category.StartsWith(TextStudioTagCategories.IntegrationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(definition.OwnerDisplayName))
                    return definition.OwnerDisplayName;
                if (!string.IsNullOrEmpty(definition.OwnerId))
                    return definition.OwnerId;
            }

            return groupId;
        }

        static int GetGroupSortOrder(TextStudioTagDefinition definition, string groupId)
        {
            if (string.Equals(groupId, AllGroupId, StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(groupId, TextStudioTagCategories.Styling, StringComparison.OrdinalIgnoreCase))
                return 10;
            if (string.Equals(groupId, TextStudioTagCategories.Actions, StringComparison.OrdinalIgnoreCase))
                return 20;
            if (string.Equals(groupId, TextStudioTagCategories.Transitions, StringComparison.OrdinalIgnoreCase))
                return 25;
            if (string.Equals(groupId, TextStudioTagCategories.Effects, StringComparison.OrdinalIgnoreCase))
                return 30;
            if (definition != null &&
                !string.IsNullOrEmpty(definition.Category) &&
                definition.Category.StartsWith(TextStudioTagCategories.IntegrationPrefix, StringComparison.OrdinalIgnoreCase))
                return 40;

            return 100;
        }

        static void AddGroup(
            Dictionary<string, TextStudioTagBrowserGroup> groups,
            string id,
            string displayName,
            int sortOrder)
        {
            if (string.IsNullOrEmpty(id) || groups.ContainsKey(id))
                return;

            groups.Add(id, new TextStudioTagBrowserGroup(id, displayName, sortOrder));
        }

        static int CompareGroups(TextStudioTagBrowserGroup a, TextStudioTagBrowserGroup b)
        {
            int order = a.SortOrder.CompareTo(b.SortOrder);
            return order != 0
                ? order
                : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        static int CompareItems(TextStudioTagBrowserItem a, TextStudioTagBrowserItem b)
        {
            return string.Compare(a.SortName, b.SortName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
