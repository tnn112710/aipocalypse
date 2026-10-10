using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TextStudio.BuiltIns
{
    public sealed class TextStudioNativeRichTextTagDescriptor
    {
        readonly ReadOnlyCollection<string> _aliases;
        readonly ReadOnlyCollection<string> _examples;
        readonly ReadOnlyCollection<TextStudioTagParameterDefinition> _parameters;

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public TextStudioTagShape Shape { get; }
        public TextStudioTagBackend BackendSupport { get; }
        public TextStudioTagParameterRequirement ParameterRequirement { get; }
        public TextStudioRenderDomain SupportedRenderDomains { get; }
        public IReadOnlyList<string> Aliases => _aliases;
        public IReadOnlyList<string> Examples => _examples;
        public IReadOnlyList<TextStudioTagParameterDefinition> Parameters => _parameters;

        public TextStudioNativeRichTextTagDescriptor(
            string id,
            string displayName,
            string description,
            TextStudioTagShape shape,
            TextStudioTagBackend backendSupport,
            TextStudioTagParameterRequirement parameterRequirement,
            string[] aliases,
            string[] examples,
            TextStudioTagParameterDefinition[] parameters,
            TextStudioRenderDomain supportedRenderDomains = TextStudioRenderDomain.All)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? Id;
            Description = description ?? string.Empty;
            Shape = shape;
            BackendSupport = backendSupport;
            ParameterRequirement = parameterRequirement;
            SupportedRenderDomains = supportedRenderDomains;
            _aliases = Array.AsReadOnly(aliases != null ? (string[])aliases.Clone() : Array.Empty<string>());
            _examples = Array.AsReadOnly(examples != null ? (string[])examples.Clone() : Array.Empty<string>());
            _parameters = Array.AsReadOnly(
                parameters != null
                    ? (TextStudioTagParameterDefinition[])parameters.Clone()
                    : Array.Empty<TextStudioTagParameterDefinition>());
        }
    }

    /// <summary>
    /// Code-defined default stylesheet mapping common rich-text tags to built-in effects.
    /// No asset file is shipped.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Built-in style and rich-text metadata is immutable after construction.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class BuiltInStyles
    {
        /// <summary>
        /// Tag name -> effect id. Used by StyleResolver when a tag is unknown locally but
        /// matches a registered built-in effect.
        /// </summary>
        public static IReadOnlyDictionary<string, string> DefaultTagMap => s_readOnlyMap;

        static readonly Dictionary<string, string> s_map = new Dictionary<string, string>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            { TextStudioBuiltInEffectIds.Wave, TextStudioBuiltInEffectIds.Wave },
            { TextStudioBuiltInEffectIds.Shake, TextStudioBuiltInEffectIds.Shake },
            { TextStudioBuiltInEffectIds.Wiggle, TextStudioBuiltInEffectIds.Wiggle },
            { TextStudioBuiltInEffectIds.Bounce, TextStudioBuiltInEffectIds.Bounce },
            { TextStudioBuiltInEffectIds.Swing, TextStudioBuiltInEffectIds.Swing },
            { TextStudioBuiltInEffectIds.Pendulum, TextStudioBuiltInEffectIds.Pendulum },
            { "pend", TextStudioBuiltInEffectIds.Pendulum },
            { TextStudioBuiltInEffectIds.Dangle, TextStudioBuiltInEffectIds.Dangle },
            { TextStudioBuiltInEffectIds.Pulse, TextStudioBuiltInEffectIds.Pulse },
            { "breathe", TextStudioBuiltInEffectIds.Pulse },
            { TextStudioBuiltInEffectIds.Blink, TextStudioBuiltInEffectIds.Blink },
            { "flash", TextStudioBuiltInEffectIds.Blink },
            { TextStudioBuiltInEffectIds.Shimmer, TextStudioBuiltInEffectIds.Shimmer },
            { "shine", TextStudioBuiltInEffectIds.Shimmer },
            { TextStudioBuiltInEffectIds.GradientFlow, TextStudioBuiltInEffectIds.GradientFlow },
            { TextStudioBuiltInEffectIds.ColorSweep, TextStudioBuiltInEffectIds.ColorSweep },
            { TextStudioBuiltInEffectIds.ColorPulse, TextStudioBuiltInEffectIds.ColorPulse },
            { TextStudioBuiltInEffectIds.Pop, TextStudioBuiltInEffectIds.Pop },
            { "popIn", TextStudioBuiltInEffectIds.Pop },
            { TextStudioBuiltInEffectIds.Float, TextStudioBuiltInEffectIds.Float },
            { "drift", TextStudioBuiltInEffectIds.Float },
            { TextStudioBuiltInEffectIds.Rainbow, TextStudioBuiltInEffectIds.Rainbow },
            { "rainb", TextStudioBuiltInEffectIds.Rainbow },
            { TextStudioBuiltInEffectIds.Fade, TextStudioBuiltInEffectIds.Fade },
            { TextStudioBuiltInEffectIds.Size, TextStudioBuiltInEffectIds.Size },
            { "incr", TextStudioBuiltInEffectIds.Size },
            { TextStudioBuiltInEffectIds.Rotate, TextStudioBuiltInEffectIds.Rotate },
            { "rot", TextStudioBuiltInEffectIds.Rotate },
            { TextStudioBuiltInEffectIds.Shear, TextStudioBuiltInEffectIds.Shear },
            { "skew", TextStudioBuiltInEffectIds.Shear },
            { "slide", TextStudioBuiltInEffectIds.SlideHorizontal },
            { "slideH", TextStudioBuiltInEffectIds.SlideHorizontal },
            { "slideX", TextStudioBuiltInEffectIds.SlideHorizontal },
            { "slideV", TextStudioBuiltInEffectIds.SlideVertical },
            { "slideY", TextStudioBuiltInEffectIds.SlideVertical },
            { TextStudioBuiltInEffectIds.Expand, TextStudioBuiltInEffectIds.Expand },
            { TextStudioBuiltInEffectIds.Typewriter, TextStudioBuiltInEffectIds.Typewriter },
            { TextStudioBuiltInEffectIds.Decrypt, TextStudioBuiltInEffectIds.Decrypt },
            { TextStudioBuiltInEffectIds.Glitch, TextStudioBuiltInEffectIds.Glitch },
            { TextStudioBuiltInEffectIds.Emphasis, TextStudioBuiltInEffectIds.Emphasis },
            { "burnAway", TextStudioBuiltInEffectIds.Burn },
            { "embers", TextStudioBuiltInEffectIds.Burn },
            { "charBurn", TextStudioBuiltInEffectIds.Burn },
            { "em", TextStudioBuiltInEffectIds.Emphasis },
            { "jitter", TextStudioBuiltInEffectIds.Shake },
            { "big", TextStudioBuiltInEffectIds.Size },
            { "small", TextStudioBuiltInEffectIds.Size }
        };

        static readonly ReadOnlyDictionary<string, string> s_readOnlyMap =
            new ReadOnlyDictionary<string, string>(s_map);

        /// <summary>
        /// True if the given tag name refers to a known built-in effect.
        /// </summary>
        public static bool TryResolve(string tagName, out string effectId)
            => s_map.TryGetValue(tagName, out effectId);

        public static IReadOnlyList<TextStudioNativeRichTextTagDescriptor> NativeRichTextTagDescriptors =>
            s_readOnlyNativeRichTextTagDescriptors;

        static readonly TextStudioNativeRichTextTagDescriptor[] s_nativeRichTextTagDescriptors =
            CreateNativeRichTextTagDescriptors();

        static readonly ReadOnlyCollection<TextStudioNativeRichTextTagDescriptor> s_readOnlyNativeRichTextTagDescriptors =
            Array.AsReadOnly(s_nativeRichTextTagDescriptors);

        /// <summary>
        /// Tags that should be passed through to the text backend (TMP) verbatim
        /// rather than stripped. Used for native formatting that TMP already renders
        /// correctly - underline, strikethrough, bold, italic, color, size, sub/sup,
        /// and so on. If TextStudio intercepted these it would lose the native
        /// rendering (e.g. TMP's underline quads that sit on top of the glyphs).
        /// </summary>
        static readonly HashSet<string> s_nativeRichTextFormattingTags =
            CreateNativeTagSet(TextStudioTagShape.Range, includeParameterized: false);

        static readonly HashSet<string> s_parameterizedNativeRichTextFormattingTags =
            CreateNativeTagSet(TextStudioTagShape.Range, includeParameterized: true);

        static readonly HashSet<string> s_selfContainedNativeRichTextFormattingTags =
            CreateNativeTagSet(TextStudioTagShape.SelfContained, includeParameterized: false);

        static readonly HashSet<string> s_parameterizedSelfContainedNativeRichTextFormattingTags =
            CreateNativeTagSet(TextStudioTagShape.SelfContained, includeParameterized: true);

        /// <summary>
        /// True if a tag should be emitted verbatim into the display text so the
        /// text backend (TMP) can render it natively.
        /// </summary>
        public static bool IsNativeRichTextFormatting(string tagName)
            => !string.IsNullOrEmpty(tagName) && (s_nativeRichTextFormattingTags.Contains(tagName) || IsNativeColorShorthand(tagName));

        /// <summary>Recognizes the exact 3, 4, 6, or 8 hexadecimal digits accepted by native color markup.</summary>
        public static bool IsNativeColorShorthand(string tagName)
        {
            if (string.IsNullOrEmpty(tagName) || tagName[0] != '#') return false;
            int digits = tagName.Length - 1;
            if (digits != 3 && digits != 4 && digits != 6 && digits != 8) return false;
            for (int i = 1; i < tagName.Length; i++)
            {
                char c = tagName[i];
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F')) return false;
            }
            return true;
        }

        public static bool IsNativeRichTextFormatting(string tagName, bool hasParameter)
        {
            if (IsNativeRichTextFormatting(tagName)) return true;
            if (IsSelfContainedNativeRichTextFormatting(tagName, hasParameter)) return true;
            return hasParameter &&
                   !string.IsNullOrEmpty(tagName) &&
                   s_parameterizedNativeRichTextFormattingTags.Contains(tagName);
        }

        internal static bool IsNativeRichTextFormattingInvocation(
            string tagName,
            bool hasParameter,
            bool usesDirectAssignment)
        {
            if (!IsNativeRichTextFormatting(tagName, hasParameter))
                return false;

            // Some names intentionally overlap a native TMP formatting tag and a
            // Text Studio effect (for example rotate). TMP's native
            // form uses <name=value>; whitespace-separated parameters belong to
            // the Text Studio effect and must not enter TMP's layout stream.
            return !hasParameter ||
                   !TextStudioPhaseTagUtility.TryResolveEffectId(tagName, out _) ||
                   usesDirectAssignment;
        }

        public static bool IsSelfContainedNativeRichTextFormatting(string tagName, bool hasParameter)
        {
            if (string.IsNullOrEmpty(tagName)) return false;
            if (s_selfContainedNativeRichTextFormattingTags.Contains(tagName)) return true;
            return hasParameter && s_parameterizedSelfContainedNativeRichTextFormattingTags.Contains(tagName);
        }

        /// <summary>
        /// Tags that Text Studio interprets as decoration markers - extensions
        /// to TMP's built-in <c>&lt;u&gt;</c> / <c>&lt;s&gt;</c>. Unlike native
        /// tags these are NOT passed through to TMP (TMP has no overline or
        /// double-underline rendering); the Text Studio pipeline collects the
        /// covered glyph ranges onto <see cref="TextStudioCompiledPlan.CharDecorations"/>
        /// and the decoration subsystem renders tiles for them using the font
        /// atlas material, exactly like it does for <c>&lt;u&gt;</c> / <c>&lt;s&gt;</c>.
        /// </summary>
        static readonly HashSet<string> s_customDecorationTags = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            "uu", // double underline
            "o",  // overline
        };

        /// <summary>
        /// Map a Text Studio custom decoration tag name to the bit it sets on
        /// <see cref="TextStudioCompiledPlan.CharDecorations"/>. Returns
        /// <see cref="DecorationBits.None"/> for unknown names.
        /// </summary>
        public static DecorationBits ResolveDecorationBit(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return DecorationBits.None;
            if (string.Equals(tagName, "uu", StringComparison.OrdinalIgnoreCase)) return DecorationBits.DoubleUnderline;
            if (string.Equals(tagName, "o", StringComparison.OrdinalIgnoreCase)) return DecorationBits.Overline;
            return DecorationBits.None;
        }

        /// <summary>
        /// True if the tag is a Text Studio custom decoration marker
        /// (<c>uu</c>, <c>o</c>). Custom decoration tags are not passed through
        /// to TMP and don't participate in effect binding resolution - they're
        /// consumed purely as decoration range markers.
        /// </summary>
        public static bool IsCustomDecorationTag(string tagName)
            => !string.IsNullOrEmpty(tagName) && s_customDecorationTags.Contains(tagName);

        static HashSet<string> CreateNativeTagSet(TextStudioTagShape shape, bool includeParameterized)
        {
            HashSet<string> result = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < s_nativeRichTextTagDescriptors.Length; i++)
            {
                TextStudioNativeRichTextTagDescriptor descriptor = s_nativeRichTextTagDescriptors[i];
                if (descriptor.Shape != shape)
                    continue;

                bool parameterized = descriptor.ParameterRequirement == TextStudioTagParameterRequirement.Required;
                if (parameterized != includeParameterized)
                    continue;

                result.Add(descriptor.Id);
                if (!parameterized)
                {
                    IReadOnlyList<string> aliases = descriptor.Aliases;
                    for (int aliasIndex = 0; aliasIndex < aliases.Count; aliasIndex++)
                        result.Add(aliases[aliasIndex]);
                }
            }

            return result;
        }

        static TextStudioNativeRichTextTagDescriptor[] CreateNativeRichTextTagDescriptors()
        {
            TextStudioTagBackend richTextBasic =
                TextStudioTagBackend.TextMeshPro |
                TextStudioTagBackend.UIToolkitBasic |
                TextStudioTagBackend.UIToolkitFullParity |
                TextStudioTagBackend.UniTextRichText;
            TextStudioTagBackend legacyRichText =
                TextStudioTagBackend.UnityUITextRichText;
            TextStudioTagBackend tmpOnly = TextStudioTagBackend.TextMeshPro;
            TextStudioTagBackend tmpAndFullParity =
                TextStudioTagBackend.TextMeshPro |
                TextStudioTagBackend.UIToolkitFullParity;

            return new[]
            {
                TmpRange("b", "Bold", richTextBasic | legacyRichText, "<b>Text</b>"),
                TmpRange("spoiler", "Spoiler", TextStudioTagBackend.UniTextRichText, "<spoiler>Hidden note</spoiler>"),
                TmpRange("i", "Italic", richTextBasic | legacyRichText, "<i>Text</i>"),
                TmpRange("u", "Underline", richTextBasic, "<u>Text</u>"),
                TmpRange("s", "Strikethrough", richTextBasic, "<s>Text</s>", aliases: new[] { "strikethrough" }),
                TmpRange("color", "Color", richTextBasic | legacyRichText, "<color=yellow>Text</color>",
                    parameters: new[] { new TextStudioTagParameterDefinition("color", TextStudioTagParameterKind.Color, positionalIndex: 0) }),
                TmpRange("size", "Size", richTextBasic | legacyRichText, "<size=150%>Text</size>",
                    TextStudioTagParameterRequirement.Required,
                    parameters: new[] { new TextStudioTagParameterDefinition("value", TextStudioTagParameterKind.String, true, 0) }),
                TmpRange("align", "Alignment", tmpAndFullParity, "<align=center>Text</align>",
                    parameters: new[]
                    {
                        new TextStudioTagParameterDefinition(
                            "alignment",
                            TextStudioTagParameterKind.Enum,
                            true,
                            0,
                            allowedValues: new[] { "left", "center", "right", "justified", "flush", "geometry" })
                    }),
                TmpRange("alpha", "Alpha", tmpAndFullParity, "<alpha=#80>Text</alpha>"),
                TmpRange("allcaps", "All Caps", richTextBasic, "<allcaps>Text</allcaps>",
                    aliases: new[] { "uppercase" }),
                TmpRange("lowercase", "Lowercase", richTextBasic, "<lowercase>Text</lowercase>"),
                TmpRange("smallcaps", "Small Caps", richTextBasic, "<smallcaps>Text</smallcaps>"),
                TmpRange("mark", "Mark", tmpAndFullParity, "<mark=yellow>Text</mark>",
                    parameters: new[] { new TextStudioTagParameterDefinition("color", TextStudioTagParameterKind.Color, positionalIndex: 0) },
                    supportedRenderDomains: TextStudioRenderDomain.FlatText),
                TmpRange("sub", "Subscript", richTextBasic, "<sub>Text</sub>"),
                TmpRange("sup", "Superscript", richTextBasic, "<sup>Text</sup>"),
                TmpRange("nobr", "No Break", tmpAndFullParity, "<nobr>Text</nobr>"),
                TmpRange("noparse", "No Parse", tmpOnly, "<noparse><wave></noparse>"),
                TmpRange("font", "Font", tmpOnly, "<font=\"LiberationSans SDF\">Text</font>",
                    parameters: new[] { new TextStudioTagParameterDefinition("font", TextStudioTagParameterKind.AssetReference, true, 0, suggestionKey: TextStudioTagSuggestionKeys.FontName) }),
                TmpRange("font-weight", "Font Weight", tmpOnly, "<font-weight=700>Text</font-weight>"),
                TmpRange("gradient", "Native Gradient", tmpAndFullParity, "<gradient=\"Warm\">Text</gradient>",
                    TextStudioTagParameterRequirement.Required),
                TmpRange("link", "Link", tmpAndFullParity | TextStudioTagBackend.UniTextRichText, "<link=\"entry\">Text</link>"),
                TmpRange("style", "TMP Style", tmpOnly, "<style=\"Title\">Text</style>",
                    parameters: new[] { new TextStudioTagParameterDefinition("style", TextStudioTagParameterKind.AssetReference, true, 0, suggestionKey: TextStudioTagSuggestionKeys.StyleName) }),
                TmpRange("indent", "Indent", tmpOnly, "<indent=12%>Text</indent>"),
                TmpRange("line-indent", "Line Indent", tmpOnly, "<line-indent=2em>Text</line-indent>"),
                TmpRange("line-height", "Line Height", tmpOnly, "<line-height=120%>Text</line-height>"),
                TmpRange("margin", "Margin", tmpOnly, "<margin=10px>Text</margin>"),
                TmpRange("margin-left", "Left Margin", tmpOnly, "<margin-left=10px>Text</margin-left>"),
                TmpRange("margin-right", "Right Margin", tmpOnly, "<margin-right=10px>Text</margin-right>"),
                TmpRange("cspace", "Character Spacing", tmpOnly, "<cspace=0.1em>Text</cspace>"),
                TmpRange("mspace", "Monospace", tmpOnly, "<mspace=1em>Text</mspace>"),
                TmpRange("voffset", "Vertical Offset", tmpAndFullParity, "<voffset=0.5em>Text</voffset>"),
                TmpRange("width", "Width", tmpOnly, "<width=50%>Text</width>"),
                TmpRange("rotate", "TMP Rotate", tmpOnly, "<rotate=12>Text</rotate>",
                    TextStudioTagParameterRequirement.Required,
                    parameters: new[] { new TextStudioTagParameterDefinition("degrees", TextStudioTagParameterKind.Float, true, 0) }),
                TmpRange("material", "Material", tmpOnly, "<material=\"Outline\">Text</material>"),
                TmpRange("scale", "Scale", tmpOnly, "<scale=1.2>Text</scale>"),
                TmpRange("a", "Link Alias", tmpOnly, "<a href=\"url\">Text</a>"),
                TmpRange("table", "Table", tmpOnly, "<table>Text</table>"),
                TmpRange("tr", "Table Row", tmpOnly, "<tr>Text</tr>"),
                TmpRange("th", "Table Header", tmpOnly, "<th>Text</th>"),
                TmpRange("td", "Table Cell", tmpOnly, "<td>Text</td>"),
                TmpRange("frac", "Fractions", tmpOnly, "<frac>1/2</frac>"),
                TmpSelfContained("br", "Line Break", richTextBasic, "<br>"),
                TmpSelfContained("page", "Page Break", tmpOnly, "<page>"),
                TmpSelfContained("cr", "Carriage Return", richTextBasic, "<cr>"),
                TmpSelfContained("zwsp", "Zero Width Space", richTextBasic, "<zwsp>"),
                TmpSelfContained("zwj", "Zero Width Joiner", richTextBasic, "<zwj>"),
                TmpSelfContained("nbsp", "No-Break Space", richTextBasic, "<nbsp>"),
                TmpSelfContained("shy", "Soft Hyphen", richTextBasic, "<shy>"),
                TmpSelfContained("pos", "Position", tmpOnly, "<pos=50%>",
                    TextStudioTagParameterRequirement.Required,
                    new[] { new TextStudioTagParameterDefinition("position", TextStudioTagParameterKind.String, true, 0) }),
                TmpSelfContained("space", "Space", tmpOnly, "<space=1em>",
                    TextStudioTagParameterRequirement.Required,
                    new[] { new TextStudioTagParameterDefinition("width", TextStudioTagParameterKind.String, true, 0) }),
                TmpSelfContained("sprite", "Sprite", tmpOnly | TextStudioTagBackend.UniTextRichText, "<sprite=0>",
                    TextStudioTagParameterRequirement.Required,
                    new[] { new TextStudioTagParameterDefinition("sprite", TextStudioTagParameterKind.AssetReference, true, 0, suggestionKey: TextStudioTagSuggestionKeys.SpriteName) })
            };
        }

        static TextStudioNativeRichTextTagDescriptor TmpRange(
            string id,
            string displayName,
            TextStudioTagBackend backendSupport,
            string example,
            TextStudioTagParameterRequirement parameterRequirement = TextStudioTagParameterRequirement.Any,
            string[] aliases = null,
            TextStudioTagParameterDefinition[] parameters = null,
            TextStudioRenderDomain supportedRenderDomains = TextStudioRenderDomain.All)
        {
            return new TextStudioNativeRichTextTagDescriptor(
                id,
                displayName,
                CreateTmpDescription(id),
                TextStudioTagShape.Range,
                backendSupport,
                parameterRequirement,
                aliases,
                new[] { example },
                parameters,
                supportedRenderDomains);
        }

        static TextStudioNativeRichTextTagDescriptor TmpSelfContained(
            string id,
            string displayName,
            TextStudioTagBackend backendSupport,
            string example,
            TextStudioTagParameterRequirement parameterRequirement = TextStudioTagParameterRequirement.Any,
            TextStudioTagParameterDefinition[] parameters = null,
            TextStudioRenderDomain supportedRenderDomains = TextStudioRenderDomain.All)
        {
            return new TextStudioNativeRichTextTagDescriptor(
                id,
                displayName,
                CreateTmpDescription(id),
                TextStudioTagShape.SelfContained,
                backendSupport,
                parameterRequirement,
                null,
                new[] { example },
                parameters,
                supportedRenderDomains);
        }

        static string CreateTmpDescription(string id)
        {
            switch (id)
            {
                case "b":
                    return "Applies bold font weight.";
                case "i":
                    return "Applies italic styling.";
                case "u":
                    return "Draws an underline below text.";
                case "s":
                    return "Draws a strikethrough line through text.";
                case "color":
                    return "Changes text color.";
                case "size":
                    return "Changes text size.";
                case "align":
                    return "Sets paragraph alignment.";
                case "alpha":
                    return "Changes text opacity using a hex alpha value.";
                case "allcaps":
                    return "Converts text to uppercase.";
                case "lowercase":
                    return "Converts text to lowercase.";
                case "smallcaps":
                    return "Renders lowercase letters as small capitals.";
                case "mark":
                    return "Adds a highlighted background behind text.";
                case "sub":
                    return "Positions text as subscript.";
                case "sup":
                    return "Positions text as superscript.";
                case "nobr":
                    return "Keeps the tagged text on one line.";
                case "noparse":
                    return "Shows rich-text tags as literal text.";
                case "font":
                    return "Switches to a named TMP font asset.";
                case "font-weight":
                    return "Applies a numeric font weight.";
                case "gradient":
                    return "Applies a native backend color gradient preset.";
                case "spoiler":
                    return "Marks a native UniText spoiler range. Configure its native interaction and cover style.";
                case "link":
                    return "Marks text as a link target.";
                case "style":
                    return "Applies a TMP style sheet entry.";
                case "indent":
                    return "Indents all lines in the range.";
                case "line-indent":
                    return "Indents the first line in the range.";
                case "line-height":
                    return "Sets line height for the range.";
                case "margin":
                    return "Applies matching left and right margins.";
                case "margin-left":
                    return "Applies a left margin.";
                case "margin-right":
                    return "Applies a right margin.";
                case "cspace":
                    return "Adjusts spacing between characters.";
                case "mspace":
                    return "Renders characters with a fixed-width advance.";
                case "voffset":
                    return "Moves text vertically relative to the baseline.";
                case "width":
                    return "Sets the line width used for wrapping.";
                case "rotate":
                    return "Rotates each character by degrees.";
                case "material":
                    return "Switches to a named material preset.";
                case "scale":
                    return "Scales the tagged text.";
                case "a":
                    return "Marks text with an HTML-style link target.";
                case "table":
                    return "Starts a rich-text table block.";
                case "tr":
                    return "Defines a table row.";
                case "th":
                    return "Defines a table header cell.";
                case "td":
                    return "Defines a table data cell.";
                case "frac":
                    return "Formats fractions such as 1/2.";
                case "br":
                    return "Inserts a line break.";
                case "page":
                    return "Inserts a page break.";
                case "cr":
                    return "Inserts a carriage return.";
                case "zwsp":
                    return "Inserts a zero-width space.";
                case "zwj":
                    return "Inserts a zero-width joiner.";
                case "nbsp":
                    return "Inserts a non-breaking space.";
                case "shy":
                    return "Inserts a soft hyphen.";
                case "pos":
                    return "Moves the next character to an x position.";
                case "space":
                    return "Inserts horizontal space of a specified width.";
                case "sprite":
                    return "Inserts an inline sprite from the renderer's configured sprite asset or provider.";
                default:
                    return "Passes the native <" + id + "> rich-text tag through to TextMeshPro.";
            }
        }
    }
}
