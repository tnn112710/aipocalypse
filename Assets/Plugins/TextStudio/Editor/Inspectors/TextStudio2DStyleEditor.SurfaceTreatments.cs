using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        readonly HashSet<string> _expandedTreatments = new HashSet<string>(StringComparer.Ordinal);

        VisualElement CreateSurfaceTreatmentEditor(SerializedProperty treatments)
        {
            VisualElement section = new VisualElement { name = "ts-surface-treatments" };
            section.AddToClassList("ts-subsection");
            section.Add(new Label("Surface Treatments"));
            section.Add(TextStudioInspectorElements.CreateMutedText("Treatments share this shape and selection. The first treatment is on top. The layer's opacity and blend mode apply to the complete surface."));
            string path = treatments.propertyPath;
            if (treatments.arraySize == 0)
                section.Add(TextStudioInspectorElements.CreateMutedText("Add a treatment to make this surface visible."));
            for (int index = 0; index < treatments.arraySize; index++)
            {
                int rowIndex = index;
                SerializedProperty treatment = treatments.GetArrayElementAtIndex(index);
                SerializedProperty enabled = treatment.FindPropertyRelative("_enabled");
                SerializedProperty displayName = treatment.FindPropertyRelative("_displayName");
                TextStudio2DLayerKind kind = (TextStudio2DLayerKind)treatment.FindPropertyRelative("_kind").intValue;
                string identity = GetItemId(treatment.FindPropertyRelative("_itemId"));
                TextStudioInspectorElements.LayerCardShell shell = TextStudioInspectorElements.CreateLayerCardShell(
                    "ts-surface-treatment-card-" + identity, displayName.stringValue, _expandedTreatments.Contains(identity),
                    "Edit this treatment on the shared surface.",
                    () => MutateSurfaceTreatments(path, "Toggle Surface Treatment", array =>
                    {
                        SerializedProperty flag = array.GetArrayElementAtIndex(rowIndex).FindPropertyRelative("_enabled");
                        flag.boolValue = !flag.boolValue;
                    }, identity), null,
                    () => MoveSurfaceTreatment(path, rowIndex, -1, identity),
                    () => MoveSurfaceTreatment(path, rowIndex, 1, identity),
                    () => MutateSurfaceTreatments(path, "Remove Surface Treatment", array => array.DeleteArrayElementAtIndex(rowIndex)));
                shell.Card.AddToClassList("ts-2d-style-layer-card");
                shell.SoloButton.style.display = DisplayStyle.None;
                Foldout row = shell.Foldout;
                row.name = "ts-surface-treatment-" + identity;
                row.RegisterValueChangedCallback(evt =>
                {
                    if (evt.target != row) return;
                    if (evt.newValue) _expandedTreatments.Add(identity);
                    else _expandedTreatments.Remove(identity);
                });
                void RefreshTitle() => shell.Refresh(displayName.stringValue + " (" + ObjectNames.NicifyVariableName(kind.ToString()) + ")",
                    enabled.boolValue, false, rowIndex > 0, rowIndex + 1 < treatments.arraySize);
                row.TrackPropertyValue(displayName, _ => RefreshTitle());
                row.TrackPropertyValue(enabled, _ => RefreshTitle());
                RefreshTitle();
                row.Add(CreateField(displayName.Copy(), "Name", "Name shown in the surface treatment list."));
                row.Add(CreateField(treatment.FindPropertyRelative("_opacity"), "Opacity", "Opacity of this treatment before the surface is composited."));
                row.Add(CreateField(treatment.FindPropertyRelative("_blendMode"), "Blend Mode", "How this treatment blends with the treatments behind it."));
                row.Add(CreateField(treatment.FindPropertyRelative("_participatesInAnimatedTint"), "Animated Tint", "Allow color animation to tint this treatment when enabled on the owner."));
                if (UsesPaint(kind)) row.Add(CreatePaintEditor(treatment.FindPropertyRelative("_paint")));
                string spec = kind switch
                {
                    TextStudio2DLayerKind.Fill => "_fill",
                    TextStudio2DLayerKind.Stroke => "_stroke",
                    TextStudio2DLayerKind.InnerShadow or TextStudio2DLayerKind.OuterShadow => "_shadow",
                    TextStudio2DLayerKind.InnerGlow or TextStudio2DLayerKind.OuterGlow => "_glow",
                    TextStudio2DLayerKind.Bevel => "_bevel",
                    TextStudio2DLayerKind.MaterialResponse => "_materialResponse",
                    _ => string.Empty
                };
                if (!string.IsNullOrEmpty(spec)) AddApplicableTreatmentChildren(row, treatment.FindPropertyRelative(spec), kind, true);
                row.Add(TextStudioInspectorElements.CreateLayerFooter(TextStudioInspectorElements.CreateButton("Duplicate Treatment", "Add an independent copy of this treatment.", () => DuplicateSurfaceTreatment(path, rowIndex))));
                section.Add(shell.Card);
            }
            section.Add(TextStudioInspectorElements.CreateButton("Add Treatment", "Add a paintable treatment to this surface.", () => ShowSurfaceTreatmentMenu(path)));
            return section;
        }

        void ShowSurfaceTreatmentMenu(string path)
        {
            GenericMenu menu = new GenericMenu();
            foreach (TextStudio2DLayerKind kind in new[] { TextStudio2DLayerKind.Fill, TextStudio2DLayerKind.Stroke,
                TextStudio2DLayerKind.InnerShadow, TextStudio2DLayerKind.OuterShadow, TextStudio2DLayerKind.InnerGlow,
                TextStudio2DLayerKind.OuterGlow, TextStudio2DLayerKind.Bevel, TextStudio2DLayerKind.MaterialResponse })
            {
                TextStudio2DLayerKind selected = kind;
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(kind.ToString())), false, () =>
                {
                    TextStudio2DSurfaceTreatment value = CreateSurfaceTreatmentForAuthoring(selected);
                    _expandedTreatments.Add(value.ItemId.Value);
                    MutateSurfaceTreatments(path, "Add Surface Treatment", array =>
                    {
                        array.InsertArrayElementAtIndex(0);
                        array.GetArrayElementAtIndex(0).boxedValue = value;
                    }, value.ItemId.Value);
                });
            }
            menu.ShowAsContext();
        }

        internal static TextStudio2DSurfaceTreatment CreateSurfaceTreatmentForAuthoring(TextStudio2DLayerKind kind)
        {
            TextStudio2DLayer defaults = CreateDefaultLayerForAuthoring(kind);
            return kind switch
            {
                TextStudio2DLayerKind.Fill => TextStudio2DSurfaceTreatment.CreateFill(defaults.DisplayName, defaults.Paint, defaults.Fill),
                TextStudio2DLayerKind.Stroke => TextStudio2DSurfaceTreatment.CreateStroke(defaults.DisplayName, defaults.Paint, defaults.Stroke),
                TextStudio2DLayerKind.InnerShadow or TextStudio2DLayerKind.OuterShadow => TextStudio2DSurfaceTreatment.CreateShadow(defaults.DisplayName, kind == TextStudio2DLayerKind.InnerShadow, defaults.Paint, defaults.Shadow),
                TextStudio2DLayerKind.InnerGlow or TextStudio2DLayerKind.OuterGlow => TextStudio2DSurfaceTreatment.CreateGlow(defaults.DisplayName, kind == TextStudio2DLayerKind.InnerGlow, defaults.Paint, defaults.Glow),
                TextStudio2DLayerKind.Bevel => TextStudio2DSurfaceTreatment.CreateBevel(defaults.DisplayName, defaults.Bevel),
                TextStudio2DLayerKind.MaterialResponse => TextStudio2DSurfaceTreatment.CreateMaterialResponse(defaults.DisplayName, defaults.MaterialResponse),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        void MoveSurfaceTreatment(string path, int index, int delta, string identity)
        {
            MutateSurfaceTreatments(path, "Reorder Surface Treatments", array =>
            {
                int destination = index + delta;
                if (index >= 0 && destination >= 0 && index < array.arraySize && destination < array.arraySize)
                    array.MoveArrayElement(index, destination);
            }, identity);
        }

        void DuplicateSurfaceTreatment(string path, int index)
        {
            string identity = string.Empty;
            MutateSurfaceTreatments(path, "Duplicate Surface Treatment", array =>
            {
                TextStudio2DSurfaceTreatment value = ((TextStudio2DSurfaceTreatment)array.GetArrayElementAtIndex(index).boxedValue)
                    .Clone().WithIdentity(TextStudioStackItemId.CreateUnique("surface"));
                identity = value.ItemId.Value;
                _expandedTreatments.Add(identity);
                array.InsertArrayElementAtIndex(index + 1);
                array.GetArrayElementAtIndex(index + 1).boxedValue = value;
            });
            _root.Q<Foldout>("ts-surface-treatment-" + identity)?.Focus();
        }

        void MutateSurfaceTreatments(string path, string undoName, Action<SerializedProperty> mutation, string focusIdentity = null)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            MutateLayers(undoName, _ =>
            {
                SerializedProperty array = _serializedObject.FindProperty(path);
                if (array != null && array.isArray) mutation(array);
            });
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(undoGroup);
            Undo.IncrementCurrentGroup();
            if (focusIdentity != null) _root.Q<Foldout>("ts-surface-treatment-" + focusIdentity)?.Focus();
        }
    }
}
