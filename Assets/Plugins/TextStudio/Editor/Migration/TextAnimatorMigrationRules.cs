using System;
using System.Collections.Generic;
using TextStudio.BuiltIns;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextAnimatorMigrationRules
    {
        static readonly Dictionary<string, TextAnimatorMigrationRule> s_explicitRules =
            new Dictionary<string, TextAnimatorMigrationRule>(StringComparer.OrdinalIgnoreCase)
            {
                { "pend", Effect("pend", TextStudioBuiltInEffectIds.Pendulum) },
                { "incr", Effect("incr", TextStudioBuiltInEffectIds.Size) },
                { "rot", Effect("rot", TextStudioBuiltInEffectIds.Rotate) },
                { "rainb", Effect("rainb", TextStudioBuiltInEffectIds.Rainbow) },
                { "slide", Effect("slide", TextStudioBuiltInEffectIds.SlideHorizontal) },
                { "slideh", Effect("slideh", TextStudioBuiltInEffectIds.SlideHorizontal) },
                { "slidex", Effect("slidex", TextStudioBuiltInEffectIds.SlideHorizontal) },
                { "slidev", Effect("slidev", TextStudioBuiltInEffectIds.SlideVertical) },
                { "slidey", Effect("slidey", TextStudioBuiltInEffectIds.SlideVertical) },
                { "waitfor", Action("waitfor", TextStudioBuiltInActionIds.Wait) },
                { "waitinput", Action("waitinput", TextStudioBuiltInActionIds.WaitForInput) },
                { "short", Playback("short", "once") }
            };

        static readonly HashSet<string> s_textAnimatorActionTags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                TextStudioBuiltInActionIds.Speed
            };

        static TextAnimatorMigrationRule Effect(string source, string target)
        {
            return new TextAnimatorMigrationRule(
                source,
                target,
                TextAnimatorMigrationRuleKind.Effect,
                TextAnimatorMigrationStatus.Covered,
                "Text Studio supports this Text Animator effect spelling directly.");
        }

        static TextAnimatorMigrationRule Action(string source, string target)
        {
            return new TextAnimatorMigrationRule(
                source,
                target,
                TextAnimatorMigrationRuleKind.Action,
                TextAnimatorMigrationStatus.Covered,
                "Text Studio supports this Text Animator action spelling directly.");
        }

        static TextAnimatorMigrationRule Playback(string source, string target)
        {
            return new TextAnimatorMigrationRule(
                source,
                target,
                TextAnimatorMigrationRuleKind.Playback,
                TextAnimatorMigrationStatus.Covered,
                "Text Studio accepts this Text Animator playback keyword on effect tags.");
        }

        public static bool TryGet(string tag, out TextAnimatorMigrationRule rule)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                rule = default;
                return false;
            }

            string normalized = Normalize(tag);
            if (s_explicitRules.TryGetValue(normalized, out rule))
                return true;

            string trimmed = tag.Trim();
            if (TextStudioPhaseTagUtility.TryResolveEffectId(trimmed, out string effectId))
            {
                rule = Effect(trimmed, effectId);
                return true;
            }

            if (s_textAnimatorActionTags.Contains(normalized) &&
                BuiltInActions.TryGetCanonicalActionId(trimmed, out string actionId))
            {
                rule = Action(trimmed, actionId);
                return true;
            }

            if (BuiltInPlaybacks.TryResolveTextAnimatorKeyword(trimmed, out string playback))
            {
                rule = Playback(trimmed, playback);
                return true;
            }

            rule = default;
            return false;
        }

        /// <summary>
        /// Finds project coverage created during migration or authored in Text Studio: registered tag aliases,
        /// actions, and Playback assets. Built-in effects and phase wrappers are resolved by <see cref="TryGet"/>.
        /// </summary>
        public static bool TryGetRegisteredCoverage(string tag, out TextAnimatorMigrationRuleKind kind)
        {
            kind = TextAnimatorMigrationRuleKind.Unknown;
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            string trimmed = tag.Trim();
            TextStudioRegistry.EnsureInitialized();
            if (TextStudioRegistry.TagExpansions.TryGet(trimmed, out _))
            {
                kind = TextAnimatorMigrationRuleKind.Style;
                return true;
            }

            if (TextStudioRegistry.Actions.TryGetQuiet(trimmed, out _))
            {
                kind = TextAnimatorMigrationRuleKind.Action;
                return true;
            }

            if (TextStudioRegistry.Playbacks.TryGetQuiet(trimmed, out _))
            {
                kind = TextAnimatorMigrationRuleKind.Playback;
                return true;
            }

            return false;
        }

        /// <summary>Returns true when a project Playback asset, rather than a built-in keyword, owns the tag.</summary>
        public static bool IsProjectPlayback(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            string trimmed = tag.Trim();
            TextStudioRegistry.EnsureInitialized();
            return TextStudioRegistry.Playbacks.TryGetQuiet(trimmed, out IEffectPlayback playback) &&
                   !TextStudioRegistry.Playbacks.IsBuiltIn(trimmed, playback);
        }

        public static bool IsKnownEffect(string tag)
        {
            return TryGet(tag, out TextAnimatorMigrationRule rule) &&
                   rule.Kind == TextAnimatorMigrationRuleKind.Effect;
        }

        public static string ResolveEffectTag(string tag)
        {
            if (TryGet(tag, out TextAnimatorMigrationRule rule) &&
                rule.Kind == TextAnimatorMigrationRuleKind.Effect)
            {
                return rule.TextStudioTag;
            }

            if (TextStudioPhaseTagUtility.TryResolveEffectId(tag, out string effectId))
                return effectId;

            return tag ?? string.Empty;
        }

        public static IReadOnlyList<TextAnimatorMigrationRule> All()
        {
            List<TextAnimatorMigrationRule> rules = new List<TextAnimatorMigrationRule>(s_explicitRules.Count);
            foreach (KeyValuePair<string, TextAnimatorMigrationRule> pair in s_explicitRules)
                rules.Add(pair.Value);
            return rules;
        }

        public static bool IsKnownPlaybackKeyword(string keyword, out string playback)
        {
            playback = string.Empty;
            if (TryGet(keyword, out TextAnimatorMigrationRule rule) &&
                rule.Kind == TextAnimatorMigrationRuleKind.Playback)
            {
                playback = rule.TextStudioTag;
                return true;
            }

            return BuiltInPlaybacks.TryResolveTextAnimatorKeyword(keyword, out playback);
        }

        static string Normalize(string tag)
        {
            return (tag ?? string.Empty).Trim().Replace("-", string.Empty).Replace("_", string.Empty);
        }
    }
}
