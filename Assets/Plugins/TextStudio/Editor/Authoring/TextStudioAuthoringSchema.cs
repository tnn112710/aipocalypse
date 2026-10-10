using System;
using System.Collections.Generic;
using UnityEditor;

namespace TextStudio.Editor
{
    internal enum TextStudioAuthoringPropertyId
    {
        Markup,
        BehaviorProfile,
        MarkupSyntax,
        RevealMode,
        SpeedMultiplier,
        LoopReveal,
        SourceMode,
        SourceElement,
        SourcePropertyPath,
        DataSource,
        DataSourceType,
        DataSourcePath,
        SourceConverter,
        UpdateMode,
        PlayOnAttach,
        TextGeneratorPreference,
        TargetProperty
    }

    internal enum TextStudioAuthoringGroup
    {
        Source,
        Authoring,
        Preview,
        Advanced
    }

    internal enum TextStudioAuthoringCapability
    {
        AnyTarget,
        UIToolkit,
        UIToolkitFullParity
    }

    internal sealed class TextStudioAuthoringProperty
    {
        public TextStudioAuthoringPropertyId Id { get; }
        public string Label { get; }
        public string Tooltip { get; }
        public object DefaultValue { get; }
        public TextStudioAuthoringGroup Group { get; }
        public TextStudioAuthoringCapability Capability { get; }
        public bool TargetIndependent { get; }

        public TextStudioAuthoringProperty(
            TextStudioAuthoringPropertyId id,
            string label,
            string tooltip,
            object defaultValue,
            TextStudioAuthoringGroup group,
            TextStudioAuthoringCapability capability,
            bool targetIndependent)
        {
            Id = id;
            Label = label ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            DefaultValue = defaultValue;
            Group = group;
            Capability = capability;
            TargetIndependent = targetIndependent;
        }
    }

    internal interface ITextStudioAuthoringStorage
    {
        bool Supports(TextStudioAuthoringPropertyId id);
        SerializedProperty FindProperty(TextStudioAuthoringPropertyId id);
        string GetExclusionReason(TextStudioAuthoringPropertyId id);
    }

#if UNITY_6000_7_OR_NEWER
    // The property table is immutable authoring schema metadata, not play-session state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioAuthoringSchema
    {
        static readonly TextStudioAuthoringProperty[] s_Properties =
        {
            Shared(TextStudioAuthoringPropertyId.Markup, "Markup", "Canonical Text Studio source markup.", string.Empty, TextStudioAuthoringGroup.Source),
            Shared(TextStudioAuthoringPropertyId.BehaviorProfile, "Behavior Profile", "Optional shared behavior defaults.", null, TextStudioAuthoringGroup.Authoring),
            new TextStudioAuthoringProperty(
                TextStudioAuthoringPropertyId.MarkupSyntax,
                "Markup Syntax",
                "Legacy programmatic override. Normal authoring uses the global Text Studio project setting.",
                null,
                TextStudioAuthoringGroup.Advanced,
                TextStudioAuthoringCapability.AnyTarget,
                false),
            Shared(TextStudioAuthoringPropertyId.RevealMode, "Reveal", "How Text Studio reveals semantic text units.", RevealMode.Instant, TextStudioAuthoringGroup.Authoring),
            Shared(TextStudioAuthoringPropertyId.SpeedMultiplier, "Speed", "Multiplier applied to reveal timing.", 1f, TextStudioAuthoringGroup.Authoring),
            Shared(TextStudioAuthoringPropertyId.LoopReveal, "Loop Reveal", "Restart reveal after it completes.", false, TextStudioAuthoringGroup.Authoring),
            UIToolkit(TextStudioAuthoringPropertyId.SourceMode, "Source", "Where the binding reads canonical markup.", TextStudioBindingSourceMode.LabelText, TextStudioAuthoringGroup.Source),
            UIToolkit(TextStudioAuthoringPropertyId.SourceElement, "Source Element", "Unique name of an element in the same panel.", string.Empty, TextStudioAuthoringGroup.Source),
            UIToolkit(TextStudioAuthoringPropertyId.SourcePropertyPath, "Property Path", "Property read from the named element.", "value", TextStudioAuthoringGroup.Source),
            UIToolkit(TextStudioAuthoringPropertyId.DataSource, "Data Source", "Optional explicit native UI Toolkit data source.", null, TextStudioAuthoringGroup.Source),
            UIToolkit(TextStudioAuthoringPropertyId.DataSourceType, "Data Source Type", "Design-time type hint for an inherited data source.", string.Empty, TextStudioAuthoringGroup.Source),
            UIToolkit(TextStudioAuthoringPropertyId.DataSourcePath, "Data Source Path", "Path to markup in the effective native data source.", string.Empty, TextStudioAuthoringGroup.Source),
            UIToolkit(TextStudioAuthoringPropertyId.SourceConverter, "Converter", "Optional converter group applied before Text Studio parses the value.", string.Empty, TextStudioAuthoringGroup.Source),
            UIToolkit(TextStudioAuthoringPropertyId.UpdateMode, "Update Mode", "Use the panel scheduler or explicit host updates.", TextStudioUITKUpdateMode.Automatic, TextStudioAuthoringGroup.Advanced),
            UIToolkit(TextStudioAuthoringPropertyId.PlayOnAttach, "Play On Attach", "Start locally configured reveal when the Label enters a panel.", true, TextStudioAuthoringGroup.Authoring),
            new TextStudioAuthoringProperty(TextStudioAuthoringPropertyId.TextGeneratorPreference, "Text Generator", "Preferred UI Toolkit text generator where Unity exposes the choice.", TextStudioUITKTextGeneratorPreference.ProjectDefault, TextStudioAuthoringGroup.Advanced, TextStudioAuthoringCapability.UIToolkitFullParity, false),
            UIToolkit(TextStudioAuthoringPropertyId.TargetProperty, "Target Property", "Text Studio bindings target Label.text only.", "text", TextStudioAuthoringGroup.Advanced)
        };

        public static IReadOnlyList<TextStudioAuthoringProperty> Properties => s_Properties;

        public static TextStudioAuthoringProperty Get(TextStudioAuthoringPropertyId id)
        {
            for (int i = 0; i < s_Properties.Length; i++)
            {
                if (s_Properties[i].Id == id)
                    return s_Properties[i];
            }

            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown Text Studio authoring property.");
        }

        public static bool ValidateParity(
            ITextStudioAuthoringStorage tmp,
            ITextStudioAuthoringStorage uiToolkit,
            out string error)
        {
            if (tmp == null || uiToolkit == null)
            {
                error = "Both authoring storage adapters are required.";
                return false;
            }

            for (int i = 0; i < s_Properties.Length; i++)
            {
                TextStudioAuthoringProperty property = s_Properties[i];
                if (!property.TargetIndependent)
                    continue;

                if (!IsSupportedOrExplicitlyExcluded(tmp, property.Id, "TMP", out error) ||
                    !IsSupportedOrExplicitlyExcluded(uiToolkit, property.Id, "UI Toolkit", out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        static bool IsSupportedOrExplicitlyExcluded(
            ITextStudioAuthoringStorage storage,
            TextStudioAuthoringPropertyId id,
            string target,
            out string error)
        {
            if (storage.Supports(id) || !string.IsNullOrWhiteSpace(storage.GetExclusionReason(id)))
            {
                error = string.Empty;
                return true;
            }

            error = target + " neither supports nor explicitly excludes " + id + ".";
            return false;
        }

        static TextStudioAuthoringProperty Shared(
            TextStudioAuthoringPropertyId id,
            string label,
            string tooltip,
            object defaultValue,
            TextStudioAuthoringGroup group)
        {
            return new TextStudioAuthoringProperty(
                id,
                label,
                tooltip,
                defaultValue,
                group,
                TextStudioAuthoringCapability.AnyTarget,
                true);
        }

        static TextStudioAuthoringProperty UIToolkit(
            TextStudioAuthoringPropertyId id,
            string label,
            string tooltip,
            object defaultValue,
            TextStudioAuthoringGroup group)
        {
            return new TextStudioAuthoringProperty(
                id,
                label,
                tooltip,
                defaultValue,
                group,
                TextStudioAuthoringCapability.UIToolkit,
                false);
        }
    }

    internal sealed class TextStudioTmpAuthoringStorage : ITextStudioAuthoringStorage
    {
        readonly SerializedObject _serializedObject;

        public TextStudioTmpAuthoringStorage(SerializedObject serializedObject)
        {
            _serializedObject = serializedObject;
        }

        public bool Supports(TextStudioAuthoringPropertyId id)
        {
            switch (id)
            {
                case TextStudioAuthoringPropertyId.Markup:
                case TextStudioAuthoringPropertyId.BehaviorProfile:
                case TextStudioAuthoringPropertyId.MarkupSyntax:
                case TextStudioAuthoringPropertyId.RevealMode:
                case TextStudioAuthoringPropertyId.SpeedMultiplier:
                case TextStudioAuthoringPropertyId.LoopReveal:
                    return true;
                default:
                    return false;
            }
        }

        public SerializedProperty FindProperty(TextStudioAuthoringPropertyId id)
        {
            if (_serializedObject == null)
                return null;

            string path = id switch
            {
                TextStudioAuthoringPropertyId.Markup => "_markup",
                TextStudioAuthoringPropertyId.BehaviorProfile => "_behaviorProfile",
                TextStudioAuthoringPropertyId.MarkupSyntax => "_markupSyntaxOverride",
                TextStudioAuthoringPropertyId.RevealMode => "_revealMode",
                TextStudioAuthoringPropertyId.SpeedMultiplier => "_revealSpeedMultiplier",
                TextStudioAuthoringPropertyId.LoopReveal => "_loopReveal",
                _ => string.Empty
            };
            return string.IsNullOrEmpty(path) ? null : _serializedObject.FindProperty(path);
        }

        public string GetExclusionReason(TextStudioAuthoringPropertyId id)
        {
            return Supports(id)
                ? string.Empty
                : "UI Toolkit source routing and target controls do not apply to a TextMesh Pro component.";
        }
    }

    internal sealed class TextStudioUxmlBindingAuthoringStorage : ITextStudioAuthoringStorage
    {
        readonly SerializedProperty _bindingProperty;
        readonly IUIToolkitUxmlAuthoringStore _ownerStore;

        public TextStudioUxmlBindingAuthoringStorage(
            SerializedProperty bindingProperty,
            IUIToolkitUxmlAuthoringStore ownerStore)
        {
            _bindingProperty = bindingProperty;
            _ownerStore = ownerStore;
        }

        public bool Supports(TextStudioAuthoringPropertyId id)
        {
            return Enum.IsDefined(typeof(TextStudioAuthoringPropertyId), id);
        }

        public SerializedProperty FindProperty(TextStudioAuthoringPropertyId id)
        {
            if (id == TextStudioAuthoringPropertyId.Markup)
                return _ownerStore?.TextProperty;
            if (_bindingProperty == null)
                return null;

            string relativePath = id switch
            {
                TextStudioAuthoringPropertyId.BehaviorProfile => nameof(TextStudioBinding.BehaviorProfile),
                TextStudioAuthoringPropertyId.MarkupSyntax => nameof(TextStudioBinding.MarkupSyntaxOverride),
                TextStudioAuthoringPropertyId.RevealMode => nameof(TextStudioBinding.RevealMode),
                TextStudioAuthoringPropertyId.SpeedMultiplier => nameof(TextStudioBinding.SpeedMultiplier),
                TextStudioAuthoringPropertyId.LoopReveal => nameof(TextStudioBinding.LoopReveal),
                TextStudioAuthoringPropertyId.SourceMode => nameof(TextStudioBinding.SourceMode),
                TextStudioAuthoringPropertyId.SourceElement => nameof(TextStudioBinding.SourceElementName),
                TextStudioAuthoringPropertyId.SourcePropertyPath => nameof(TextStudioBinding.SourcePropertyPath),
                TextStudioAuthoringPropertyId.DataSource => "dataSourceAsset",
                TextStudioAuthoringPropertyId.DataSourceType => "dataSourceTypeString",
                TextStudioAuthoringPropertyId.DataSourcePath => "dataSourcePathString",
                TextStudioAuthoringPropertyId.SourceConverter => "sourceToUiConvertersString",
                TextStudioAuthoringPropertyId.UpdateMode => nameof(TextStudioBinding.UpdateMode),
                TextStudioAuthoringPropertyId.PlayOnAttach => nameof(TextStudioBinding.PlayOnAttach),
                TextStudioAuthoringPropertyId.TextGeneratorPreference => nameof(TextStudioBinding.TextGeneratorPreference),
                TextStudioAuthoringPropertyId.TargetProperty => "property",
                _ => string.Empty
            };
            return string.IsNullOrEmpty(relativePath)
                ? null
                : _bindingProperty.FindPropertyRelative(relativePath);
        }

        public string GetExclusionReason(TextStudioAuthoringPropertyId id)
        {
            return Supports(id) ? string.Empty : "The property is not represented by TextStudioBinding.";
        }
    }
}
