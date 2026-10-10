using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioComposedEffectParameterEditor
    {
        static readonly List<string> s_OperationChoices = new List<string> { "Set", "Multiply" };

        internal static VisualElement Create(
            SerializedObject serializedObject,
            SerializedProperty effectIdProperty,
            SerializedProperty entriesProperty,
            SerializedProperty advancedParametersProperty,
            Action onChanged)
        {
            VisualElement root = new VisualElement
            {
                name = "ts-composed-effect-parameter-editor"
            };
            root.AddToClassList("ts-property-group");

            string effectId = effectIdProperty?.stringValue ?? string.Empty;
            if (!TryGetMetadata(effectId, out TextStudioEffectMetadata metadata) ||
                metadata.Parameters.Count == 0)
            {
                root.Add(CreateHint(
                    "This effect does not publish structured parameter metadata. Use Advanced Parameters for its documented tag syntax."));
                root.Add(CreateAdvancedFoldout(advancedParametersProperty));
                return root;
            }

            VisualElement heading = new VisualElement();
            heading.style.flexDirection = FlexDirection.Row;
            heading.style.alignItems = Align.Center;
            Label title = new Label("Parameters");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1f;
            heading.Add(title);
            Button resetAll = new Button(() => ResetAll(
                serializedObject,
                entriesProperty,
                metadata.Parameters,
                onChanged))
            {
                name = "ts-composed-effect-reset-all-parameters",
                text = "Reset All",
                tooltip = "Adopt every current source default as a new explicit parameter snapshot."
            };
            heading.Add(resetAll);
            root.Add(heading);

            int visibleCount = 0;
            for (int i = 0; i < metadata.Parameters.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = metadata.Parameters[i];
                if (parameter == null || IsOwnedByLayer(parameter.Name, metadata))
                    continue;

                SerializedProperty entry = FindEntry(entriesProperty, parameter.Name);
                if (entry == null)
                {
                    root.Add(CreateHint(
                        "Parameter '" + parameter.DisplayName +
                        "' has not been snapshotted yet. Re-select the effect or use Reset All Parameters."));
                    continue;
                }

                root.Add(CreateParameterRow(
                    serializedObject,
                    entry,
                    parameter,
                    onChanged));
                visibleCount++;
            }

            if (visibleCount == 0)
            {
                root.Add(CreateHint(
                    "Timing and area are controlled by this layer's Timing & Sequence and Scope sections. This effect has no additional parameters."));
                resetAll.style.display = DisplayStyle.None;
            }

            root.Add(CreateAdvancedFoldout(advancedParametersProperty));
            return root;
        }

        static bool TryGetMetadata(string effectId, out TextStudioEffectMetadata metadata)
        {
            if (TextStudioEffects.TryGetDefinition(effectId, out ITextStudioEffectDefinition definition) &&
                definition is ITextStudioEffectMetadataProvider provider &&
                provider.Metadata != null)
            {
                metadata = provider.Metadata;
                return true;
            }

            metadata = null;
            return false;
        }

        static VisualElement CreateParameterRow(
            SerializedObject serializedObject,
            SerializedProperty entry,
            TextStudioTagParameterDefinition parameter,
            Action onChanged)
        {
            SerializedProperty valueProperty = entry.FindPropertyRelative("_value");
            SerializedProperty operationProperty = entry.FindPropertyRelative("_operation");
            SerializedProperty baselineProperty = entry.FindPropertyRelative("_baseline");
            VisualElement row = new VisualElement
            {
                name = "ts-composed-effect-parameter-" + parameter.Name
            };
            row.AddToClassList("ts-composed-parameter-row");

            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.alignItems = Align.Center;
            controls.style.flexGrow = 1f;
            row.Add(controls);

            VisualElement valueHost = new VisualElement();
            valueHost.style.flexGrow = 1f;
            valueHost.style.flexShrink = 1f;
            controls.Add(valueHost);

            PopupField<string> operation = null;
            if (IsNumeric(parameter.Kind))
            {
                operation = new PopupField<string>(s_OperationChoices, 0)
                {
                    name = "ts-composed-effect-parameter-operation-" + parameter.Name,
                    tooltip = "Set stores the authored value. Multiply stores a factor against this layer's captured baseline."
                };
                operation.style.width = 82f;
                operation.style.marginLeft = 4f;
                controls.Add(operation);
            }

            Button reset = new Button(() => ResetOne(
                serializedObject,
                valueProperty,
                operationProperty,
                baselineProperty,
                parameter.DefaultValue,
                onChanged))
            {
                name = "ts-composed-effect-reset-parameter-" + parameter.Name,
                text = "↺",
                tooltip = "Reset this parameter to the source effect's current default."
            };
            reset.style.width = 24f;
            reset.style.marginLeft = 4f;
            controls.Add(reset);

            bool refreshing = false;
            Action refreshValue = CreateValueControl(
                serializedObject,
                valueHost,
                valueProperty,
                operationProperty,
                parameter,
                onChanged,
                () => refreshing,
                value => refreshing = value);

            if (operation != null)
            {
                operation.RegisterValueChangedCallback(evt =>
                {
                    if (refreshing)
                        return;
                    TextStudioTagParameterOperation next = evt.newValue == "Multiply"
                        ? TextStudioTagParameterOperation.Multiply
                        : TextStudioTagParameterOperation.Set;
                    SwitchOperation(
                        serializedObject,
                        valueProperty,
                        operationProperty,
                        baselineProperty,
                        parameter,
                        next,
                        onChanged);
                });
            }

            Action refresh = () =>
            {
                if (serializedObject.targetObject == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                refreshing = true;
                refreshValue();
                if (operation != null)
                {
                    bool multiply = operationProperty.enumValueIndex ==
                        (int)TextStudioTagParameterOperation.Multiply;
                    operation.SetValueWithoutNotify(multiply ? "Multiply" : "Set");
                    bool canMultiply = TryParseFinite(baselineProperty.stringValue, out float baseline) &&
                        !Mathf.Approximately(baseline, 0f);
                    operation.SetEnabled(canMultiply || multiply);
                    operation.tooltip = canMultiply
                        ? "Set stores the authored value. Multiply stores a factor against the captured baseline " +
                          baselineProperty.stringValue + "."
                        : "Multiply requires a finite, nonzero captured baseline.";
                }
                refreshing = false;
            };

            row.TrackPropertyValue(valueProperty, _ => refresh());
            row.TrackPropertyValue(operationProperty, _ => refresh());
            row.TrackPropertyValue(baselineProperty, _ => refresh());
            refresh();

            if (!string.IsNullOrWhiteSpace(parameter.Description))
                row.Add(CreateHint(parameter.Description));
            return row;
        }

        static Action CreateValueControl(
            SerializedObject serializedObject,
            VisualElement host,
            SerializedProperty valueProperty,
            SerializedProperty operationProperty,
            TextStudioTagParameterDefinition parameter,
            Action onChanged,
            Func<bool> isRefreshing,
            Action<bool> setRefreshing)
        {
            switch (parameter.Kind)
            {
                case TextStudioTagParameterKind.Float:
                case TextStudioTagParameterKind.Integer:
                    return CreateNumericControl(
                        serializedObject,
                        host,
                        valueProperty,
                        operationProperty,
                        parameter,
                        onChanged,
                        isRefreshing,
                        setRefreshing);
                case TextStudioTagParameterKind.Boolean:
                {
                    Toggle field = CreateAligned(new Toggle(parameter.DisplayName));
                    host.Add(field);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (!isRefreshing())
                            CommitValue(serializedObject, valueProperty, evt.newValue ? "true" : "false", onChanged);
                    });
                    return () => field.SetValueWithoutNotify(ParseBool(valueProperty.stringValue));
                }
                case TextStudioTagParameterKind.Enum when parameter.AllowedValues.Count > 0:
                {
                    List<string> choices = new List<string>(parameter.AllowedValues.Count);
                    for (int i = 0; i < parameter.AllowedValues.Count; i++)
                        choices.Add(parameter.AllowedValues[i]);
                    PopupField<string> field = CreateAligned(new PopupField<string>(parameter.DisplayName, choices, 0));
                    host.Add(field);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (!isRefreshing())
                            CommitValue(serializedObject, valueProperty, evt.newValue, onChanged);
                    });
                    return () =>
                    {
                        string current = valueProperty.stringValue;
                        field.SetValueWithoutNotify(choices.Contains(current) ? current : choices[0]);
                    };
                }
                case TextStudioTagParameterKind.Color:
                {
                    ColorField field = CreateAligned(new ColorField(parameter.DisplayName));
                    host.Add(field);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (!isRefreshing())
                        {
                            CommitValue(
                                serializedObject,
                                valueProperty,
                                "#" + ColorUtility.ToHtmlStringRGBA(evt.newValue),
                                onChanged);
                        }
                    });
                    return () =>
                    {
                        Color color = Color.white;
                        ColorUtility.TryParseHtmlString(valueProperty.stringValue, out color);
                        field.SetValueWithoutNotify(color);
                    };
                }
                case TextStudioTagParameterKind.Vector2:
                {
                    Vector2Field field = CreateAligned(new Vector2Field(parameter.DisplayName));
                    host.Add(field);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (!isRefreshing())
                            CommitValue(serializedObject, valueProperty, Format(evt.newValue), onChanged);
                    });
                    return () =>
                    {
                        TryParseVector(valueProperty.stringValue, 2, out Vector3 value);
                        field.SetValueWithoutNotify(new Vector2(value.x, value.y));
                    };
                }
                case TextStudioTagParameterKind.Vector3:
                {
                    Vector3Field field = CreateAligned(new Vector3Field(parameter.DisplayName));
                    host.Add(field);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (!isRefreshing())
                            CommitValue(serializedObject, valueProperty, Format(evt.newValue), onChanged);
                    });
                    return () =>
                    {
                        TryParseVector(valueProperty.stringValue, 3, out Vector3 value);
                        field.SetValueWithoutNotify(value);
                    };
                }
                default:
                {
                    TextField field = CreateAligned(new TextField(parameter.DisplayName));
                    field.tooltip = BuildTypeTooltip(parameter);
                    host.Add(field);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (!isRefreshing())
                            CommitValue(serializedObject, valueProperty, evt.newValue ?? string.Empty, onChanged);
                    });
                    return () => field.SetValueWithoutNotify(valueProperty.stringValue ?? string.Empty);
                }
            }
        }

        static Action CreateNumericControl(
            SerializedObject serializedObject,
            VisualElement host,
            SerializedProperty valueProperty,
            SerializedProperty operationProperty,
            TextStudioTagParameterDefinition parameter,
            Action onChanged,
            Func<bool> isRefreshing,
            Action<bool> setRefreshing)
        {
            bool integer = parameter.Kind == TextStudioTagParameterKind.Integer;
            bool ranged = parameter.Minimum.HasValue &&
                parameter.Maximum.HasValue &&
                parameter.Maximum.Value > parameter.Minimum.Value;
            VisualElement setHost = new VisualElement();
            setHost.style.flexGrow = 1f;
            VisualElement multiplyHost = new VisualElement();
            multiplyHost.style.flexGrow = 1f;
            host.Add(setHost);
            host.Add(multiplyHost);

            FloatField multiplyField = CreateAligned(new FloatField(parameter.DisplayName));
            multiplyField.tooltip = "Scale the captured default baseline by this factor.";
            multiplyHost.Add(multiplyField);
            multiplyField.RegisterValueChangedCallback(evt =>
            {
                if (!isRefreshing())
                    CommitValue(serializedObject, valueProperty, FormatFloat(evt.newValue), onChanged);
            });

            Action refreshSet;
            if (integer && ranged)
            {
                VisualElement rangeRow = CreateRangeRow();
                SliderInt slider = new SliderInt(
                    parameter.DisplayName,
                    Mathf.RoundToInt(parameter.Minimum.Value),
                    Mathf.RoundToInt(parameter.Maximum.Value));
                slider.style.flexGrow = 1f;
                IntegerField input = new IntegerField();
                input.style.width = 72f;
                rangeRow.Add(slider);
                rangeRow.Add(input);
                AddSuffix(rangeRow, parameter.Suffix);
                setHost.Add(rangeRow);
                slider.RegisterValueChangedCallback(evt =>
                {
                    if (!isRefreshing())
                        CommitNumeric(serializedObject, valueProperty, evt.newValue, parameter, onChanged);
                });
                input.RegisterValueChangedCallback(evt =>
                {
                    if (!isRefreshing())
                        CommitNumeric(serializedObject, valueProperty, evt.newValue, parameter, onChanged);
                });
                refreshSet = () =>
                {
                    int value = ParseInt(valueProperty.stringValue);
                    slider.SetValueWithoutNotify(Mathf.Clamp(
                        value,
                        Mathf.RoundToInt(parameter.Minimum.Value),
                        Mathf.RoundToInt(parameter.Maximum.Value)));
                    input.SetValueWithoutNotify(value);
                };
            }
            else if (!integer && ranged)
            {
                VisualElement rangeRow = CreateRangeRow();
                Slider slider = new Slider(
                    parameter.DisplayName,
                    parameter.Minimum.Value,
                    parameter.Maximum.Value);
                slider.style.flexGrow = 1f;
                FloatField input = new FloatField();
                input.style.width = 72f;
                rangeRow.Add(slider);
                rangeRow.Add(input);
                AddSuffix(rangeRow, parameter.Suffix);
                setHost.Add(rangeRow);
                slider.RegisterValueChangedCallback(evt =>
                {
                    if (!isRefreshing())
                        CommitNumeric(serializedObject, valueProperty, evt.newValue, parameter, onChanged);
                });
                input.RegisterValueChangedCallback(evt =>
                {
                    if (!isRefreshing())
                        CommitNumeric(serializedObject, valueProperty, evt.newValue, parameter, onChanged);
                });
                refreshSet = () =>
                {
                    float value = ParseFloat(valueProperty.stringValue);
                    slider.SetValueWithoutNotify(Mathf.Clamp(
                        value,
                        parameter.Minimum.Value,
                        parameter.Maximum.Value));
                    input.SetValueWithoutNotify(value);
                };
            }
            else if (integer)
            {
                IntegerField field = CreateAligned(new IntegerField(parameter.DisplayName));
                setHost.Add(field);
                AddSuffix(setHost, parameter.Suffix);
                field.RegisterValueChangedCallback(evt =>
                {
                    if (!isRefreshing())
                        CommitNumeric(serializedObject, valueProperty, evt.newValue, parameter, onChanged);
                });
                refreshSet = () => field.SetValueWithoutNotify(ParseInt(valueProperty.stringValue));
            }
            else
            {
                FloatField field = CreateAligned(new FloatField(parameter.DisplayName));
                setHost.Add(field);
                AddSuffix(setHost, parameter.Suffix);
                field.RegisterValueChangedCallback(evt =>
                {
                    if (!isRefreshing())
                        CommitNumeric(serializedObject, valueProperty, evt.newValue, parameter, onChanged);
                });
                refreshSet = () => field.SetValueWithoutNotify(ParseFloat(valueProperty.stringValue));
            }

            return () =>
            {
                bool multiply = operationProperty.enumValueIndex ==
                    (int)TextStudioTagParameterOperation.Multiply;
                setHost.style.display = multiply ? DisplayStyle.None : DisplayStyle.Flex;
                multiplyHost.style.display = multiply ? DisplayStyle.Flex : DisplayStyle.None;
                if (multiply)
                    multiplyField.SetValueWithoutNotify(ParseFloat(valueProperty.stringValue));
                else
                    refreshSet();
            };
        }

        static VisualElement CreateRangeRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1f;
            return row;
        }

        static void AddSuffix(VisualElement parent, string suffix)
        {
            if (string.IsNullOrWhiteSpace(suffix))
                return;
            Label label = new Label(suffix);
            label.AddToClassList("ts-muted-text");
            label.style.marginLeft = 3f;
            parent.Add(label);
        }

        static T CreateAligned<T>(T field) where T : VisualElement
        {
            field.AddToClassList("ts-property-field");
            field.AddToClassList("unity-base-field__aligned");
            field.AddToClassList("ts-composed-layer-aligned-field");
            return field;
        }

        static void CommitNumeric(
            SerializedObject serializedObject,
            SerializedProperty valueProperty,
            float value,
            TextStudioTagParameterDefinition parameter,
            Action onChanged)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 0f;
            if (parameter.Snap > 0f)
            {
                float origin = !parameter.AllowOverflow && parameter.Minimum.HasValue
                    ? parameter.Minimum.Value
                    : 0f;
                value = origin + Mathf.Round((value - origin) / parameter.Snap) * parameter.Snap;
            }
            if (!parameter.AllowOverflow)
            {
                if (parameter.Minimum.HasValue)
                    value = Mathf.Max(parameter.Minimum.Value, value);
                if (parameter.Maximum.HasValue)
                    value = Mathf.Min(parameter.Maximum.Value, value);
            }
            string serialized = parameter.Kind == TextStudioTagParameterKind.Integer
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : FormatFloat(value);
            CommitValue(serializedObject, valueProperty, serialized, onChanged);
        }

        static void CommitValue(
            SerializedObject serializedObject,
            SerializedProperty valueProperty,
            string value,
            Action onChanged)
        {
            if (serializedObject?.targetObject == null || valueProperty == null)
                return;
            Undo.RecordObjects(serializedObject.targetObjects, "Change Composed Effect Parameter");
            serializedObject.UpdateIfRequiredOrScript();
            valueProperty.stringValue = value ?? string.Empty;
            serializedObject.ApplyModifiedProperties();
            MarkTargetsDirty(serializedObject);
            onChanged?.Invoke();
        }

        static void SwitchOperation(
            SerializedObject serializedObject,
            SerializedProperty valueProperty,
            SerializedProperty operationProperty,
            SerializedProperty baselineProperty,
            TextStudioTagParameterDefinition parameter,
            TextStudioTagParameterOperation next,
            Action onChanged)
        {
            TextStudioTagParameterOperation current =
                (TextStudioTagParameterOperation)operationProperty.enumValueIndex;
            if (current == next)
                return;

            string nextValue = valueProperty.stringValue;
            bool baselineValid = TryParseFinite(baselineProperty.stringValue, out float baseline) &&
                !Mathf.Approximately(baseline, 0f);
            if (next == TextStudioTagParameterOperation.Multiply)
            {
                if (!baselineValid || !TryParseFinite(valueProperty.stringValue, out float effective))
                    return;
                nextValue = FormatFloat(effective / baseline);
            }
            else if (baselineValid && TryParseFinite(valueProperty.stringValue, out float factor))
            {
                float effective = baseline * factor;
                nextValue = parameter.Kind == TextStudioTagParameterKind.Integer
                    ? Mathf.RoundToInt(effective).ToString(CultureInfo.InvariantCulture)
                    : FormatFloat(effective);
            }

            Undo.RecordObjects(serializedObject.targetObjects, "Change Composed Effect Parameter Mode");
            serializedObject.UpdateIfRequiredOrScript();
            valueProperty.stringValue = nextValue;
            operationProperty.enumValueIndex = (int)next;
            serializedObject.ApplyModifiedProperties();
            MarkTargetsDirty(serializedObject);
            onChanged?.Invoke();
        }

        static void ResetOne(
            SerializedObject serializedObject,
            SerializedProperty valueProperty,
            SerializedProperty operationProperty,
            SerializedProperty baselineProperty,
            string defaultValue,
            Action onChanged)
        {
            Undo.RecordObjects(serializedObject.targetObjects, "Reset Composed Effect Parameter");
            serializedObject.UpdateIfRequiredOrScript();
            valueProperty.stringValue = defaultValue ?? string.Empty;
            baselineProperty.stringValue = defaultValue ?? string.Empty;
            operationProperty.enumValueIndex = (int)TextStudioTagParameterOperation.Set;
            serializedObject.ApplyModifiedProperties();
            MarkTargetsDirty(serializedObject);
            onChanged?.Invoke();
        }

        static void ResetAll(
            SerializedObject serializedObject,
            SerializedProperty entriesProperty,
            IReadOnlyList<TextStudioTagParameterDefinition> definitions,
            Action onChanged)
        {
            if (serializedObject?.targetObject == null || entriesProperty == null)
                return;
            Undo.RecordObjects(serializedObject.targetObjects, "Reset All Composed Effect Parameters");
            serializedObject.UpdateIfRequiredOrScript();
            for (int i = 0; i < definitions.Count; i++)
            {
                TextStudioTagParameterDefinition definition = definitions[i];
                SerializedProperty entry = FindEntry(entriesProperty, definition.Name);
                if (entry == null)
                    continue;
                string value = definition.DefaultValue ?? string.Empty;
                entry.FindPropertyRelative("_value").stringValue = value;
                entry.FindPropertyRelative("_baseline").stringValue = value;
                entry.FindPropertyRelative("_operation").enumValueIndex =
                    (int)TextStudioTagParameterOperation.Set;
            }
            serializedObject.ApplyModifiedProperties();
            MarkTargetsDirty(serializedObject);
            onChanged?.Invoke();
        }

        static SerializedProperty FindEntry(SerializedProperty entries, string canonicalName)
        {
            if (entries == null || !entries.isArray)
                return null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty candidate = entries.GetArrayElementAtIndex(i);
                SerializedProperty name = candidate.FindPropertyRelative("_name");
                if (name != null && string.Equals(
                        name.stringValue,
                        canonicalName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }

        static void MarkTargetsDirty(SerializedObject serializedObject)
        {
            UnityEngine.Object[] targets = serializedObject.targetObjects;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    EditorUtility.SetDirty(targets[i]);
            }
        }

        static bool IsOwnedByLayer(
            string name,
            TextStudioEffectMetadata metadata)
        {
            return TextStudioEffectParameterOwnership.IsOwnedByProgressLayer(metadata, name) ||
                   string.Equals(name, "area", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "group", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "charOffset", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "wordOffset", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "lineOffset", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsNumeric(TextStudioTagParameterKind kind)
        {
            return kind == TextStudioTagParameterKind.Float ||
                   kind == TextStudioTagParameterKind.Integer;
        }

        static string BuildTypeTooltip(TextStudioTagParameterDefinition parameter)
        {
            string type = parameter.Kind switch
            {
                TextStudioTagParameterKind.Pivot => "Named pivot or x,y coordinates",
                TextStudioTagParameterKind.AssetReference => "Registered asset id",
                _ => "Text value"
            };
            if (!string.IsNullOrEmpty(parameter.DefaultValue))
                type += ". Captured default: " + parameter.DefaultValue;
            return string.IsNullOrWhiteSpace(parameter.Description)
                ? type
                : type + ". " + parameter.Description;
        }

        static Foldout CreateAdvancedFoldout(SerializedProperty rawParametersProperty)
        {
            Foldout raw = new Foldout
            {
                text = "Advanced Parameters",
                value = false
            };
            if (rawParametersProperty != null)
                raw.Add(new PropertyField(rawParametersProperty, "Additional Tag Parameters"));
            raw.Add(CreateHint(
                "Use this only for keywords or parameters the effect does not publish. A published parameter cannot also appear here."));
            return raw;
        }

        static Label CreateHint(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("ts-muted-text");
            return label;
        }

        static bool ParseBool(string value)
        {
            return bool.TryParse(value, out bool parsed) && parsed;
        }

        static int ParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : Mathf.RoundToInt(ParseFloat(value));
        }

        static float ParseFloat(string value)
        {
            return TryParseFinite(value, out float parsed) ? parsed : 0f;
        }

        static bool TryParseFinite(string value, out float parsed)
        {
            return float.TryParse(
                       value,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out parsed) &&
                   !float.IsNaN(parsed) &&
                   !float.IsInfinity(parsed);
        }

        static bool TryParseVector(string raw, int dimensions, out Vector3 value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string[] parts = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != dimensions ||
                !TryParseFinite(parts[0], out float x) ||
                !TryParseFinite(parts[1], out float y))
            {
                return false;
            }
            float z = 0f;
            if (dimensions == 3 && !TryParseFinite(parts[2], out z))
                return false;
            value = new Vector3(x, y, z);
            return true;
        }

        static string Format(Vector2 value)
        {
            return FormatFloat(value.x) + "," + FormatFloat(value.y);
        }

        static string Format(Vector3 value)
        {
            return FormatFloat(value.x) + "," + FormatFloat(value.y) + "," + FormatFloat(value.z);
        }

        static string FormatFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
