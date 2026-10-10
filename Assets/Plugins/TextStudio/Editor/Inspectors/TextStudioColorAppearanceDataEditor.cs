using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioColorAppearanceData))]
    internal sealed class TextStudioColorAppearanceDataEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            TextStudioColorAppearanceData data = (TextStudioColorAppearanceData)target;
            VisualElement root = TextStudioPreparedDataInspector.Root("ts-image-appearance-data");
            bool valid = TextStudioColorAppearanceBuildValidation.ValidateAsset(data, out string error);
            root.Add(TextStudioPreparedDataInspector.Summary("Prepared image appearance", valid, error, "Reusable silhouettes reduce work for image outlines, shadows and other text effects. Original image colors and transparency stay in the source textures."));
            root.Add(TextStudioPreparedDataInspector.Metrics(("Prepared shapes", data.FieldCount.ToString("N0"), "Prepared image geometries. One source texture may contain several distinct shapes."), ("Field textures", EditorUtility.FormatBytes(data.TextureBytes), "Estimated uncompressed silhouette texture payload. Original images and runtime caches are not included.")));
            int groupIndex = 0;
            foreach (IGrouping<Texture2D, TextStudioPreparedColorField> group in Enumerable.Range(0, data.FieldCount).Select(data.GetField).Where(field => field != null).GroupBy(field => field.SourceTexture))
            {
                TextStudioPreparedColorField[] fields = group.ToArray();
                Texture2D texture = group.Key;
                string name = texture != null ? texture.name : "Missing source image";
                Foldout image = TextStudioPreparedDataInspector.Details(name + " (" + fields.Length + (fields.Length == 1 ? " shape)" : " shapes)"), "prepared-image-" + groupIndex++);
                image.Add(TextStudioPreparedDataInspector.Source("Image", texture));
                Image preview = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, tooltip = "Original source texture. Prepared shapes may use different regions of this image." };
                preview.AddToClassList("ts-prepared-image"); image.Add(preview);
                DropdownField shape = new DropdownField("Shape", Enumerable.Range(1, fields.Length).Select(index => "Shape " + index).ToList(), 0) { tooltip = "Inspect an individual prepared geometry from this source image." };
                shape.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                if (fields.Length > 1) image.Add(shape);
                Label resolution = TextStudioPreparedDataInspector.Copy("");
                Label padding = TextStudioPreparedDataInspector.Copy("");
                Label memory = TextStudioPreparedDataInspector.Copy("");
                image.Add(resolution); image.Add(padding); image.Add(memory);
                void UpdateDetails()
                {
                    TextStudioPreparedColorField field = fields[shape.index];
                    resolution.text = "Resolution: " + field.PixelsPerEm.ToString("0.#") + " pixels per em"; resolution.tooltip = resolution.text;
                    padding.text = "Effect padding: " + field.PaddingEm.ToString("0.###") + " em"; padding.tooltip = padding.text;
                    memory.text = "Field texture: " + EditorUtility.FormatBytes(field.TextureBytes); memory.tooltip = memory.text;
                }
                shape.RegisterValueChangedCallback(_ => UpdateDetails()); UpdateDetails();
                root.Add(image);
            }
            TextStudioPreparedDataInspector.Storage(root, data);
            return root;
        }
    }
}
