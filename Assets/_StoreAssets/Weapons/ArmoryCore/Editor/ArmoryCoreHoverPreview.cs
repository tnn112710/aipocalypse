using System.Collections.Generic;
using System.Linq;
using ArmoryCore;
using UnityEditor;
using UnityEngine;
using Graph = ArmoryCoreEditor.ArmoryCoreGraph;

namespace ArmoryCoreEditor
{
    /// <summary>
    /// A small card with the model of the asset behind a tile while the mouse rests on it. Tiles opt in through
    /// <see cref="ArmoryCoreGraph.Tile.Preview"/>; call <see cref="Draw"/> after the tiles so the card lies on top of them.
    /// </summary>
    internal static class ArmoryCoreHoverPreview
    {
        private const float Width = 164f;
        private const float Image = 128f;
        private static Dictionary<ArmoryCoreAttachmentDefinition, GameObject> prefabs;
        private static GUIStyle title;
        private static GUIStyle text;

        static ArmoryCoreHoverPreview()
        {
            EditorApplication.projectChanged += () => prefabs = null;
        }

        /// <summary>The module prefab that carries the attachment: the shipped ones are named «Type Definition-Name».</summary>
        public static GameObject PrefabOf(ArmoryCoreAttachmentDefinition attachment)
        {
            if (!attachment)
                return null;
            prefabs ??= new Dictionary<ArmoryCoreAttachmentDefinition, GameObject>();
            if (prefabs.TryGetValue(attachment, out var found))
                return found;
            found = null;
            foreach (var guid in AssetDatabase.FindAssets(attachment.name + " t:Prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var module = prefab ? prefab.GetComponentInChildren<ArmoryCoreAttachmentModule>(true) : null;
                if (module == null || module.Definition != attachment)
                    continue;
                found = prefab;
                break;
            }
            prefabs[attachment] = found;
            return found;
        }

        /// <summary>The prefab to preview for an installed option: its attachment's prefab, else the prefab it was placed from.</summary>
        public static GameObject PrefabOf(Transform option, ArmoryCoreAttachmentDefinition attachment)
        {
            var prefab = PrefabOf(attachment);
            if (prefab || option == null)
                return prefab;
            return PrefabUtility.GetCorrespondingObjectFromSource(option.gameObject);
        }

        public static void Draw()
        {
            var e = Event.current;
            var window = EditorWindow.mouseOverWindow;
            // The card follows the mouse, so the window has to hear mouse moves and repaint on them.
            if (window != null)
            {
                if (!window.wantsMouseMove)
                    window.wantsMouseMove = true;
                if (e.type == EventType.MouseMove)
                    window.Repaint();
            }
            if (e.type != EventType.Repaint)
                return;
            var tile = Graph.Hovered;
            var anchor = Graph.HoveredRect;
            Graph.Hovered = null;
            if (tile == null || !tile.Preview)
                return;

            Init();
            var preview = AssetPreview.GetAssetPreview(tile.Preview);
            if (!preview && AssetPreview.IsLoadingAssetPreview(tile.Preview.GetInstanceID()) && window != null)
                window.Repaint();
            var lines = string.Join("\n", new[] { tile.Text, tile.Tooltip }.Where(line => !string.IsNullOrEmpty(line)));
            var inner = Width - 12f;
            var titleHeight = title.CalcHeight(new GUIContent(tile.Title), inner);
            var textHeight = lines.Length > 0 ? text.CalcHeight(new GUIContent(lines), inner) : 0f;
            var height = 6f + Image + 4f + titleHeight + textHeight + 6f;

            var x = Mathf.Clamp(e.mousePosition.x - Width * 0.5f, 4f, Mathf.Max(4f, EditorGUIUtility.currentViewWidth - Width - 24f));
            var y = anchor.y - height - 4f;
            if (y < 0f)
                y = anchor.yMax + 4f;
            var card = new Rect(x, y, Width, height);
            EditorGUI.DrawRect(new Rect(card.x + 2f, card.y + 3f, card.width, card.height), new Color(0f, 0f, 0f, 0.35f));
            Graph.Panel(card);
            Graph.Frame(card, tile.Color, 1f);
            var image = new Rect(card.x + (Width - Image) * 0.5f, card.y + 6f, Image, Image);
            EditorGUI.DrawRect(image, Graph.TrackColor);
            if (preview)
                GUI.DrawTexture(image, preview, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(image, "loading…", EditorStyles.centeredGreyMiniLabel);
            var line = new Rect(card.x + 6f, image.yMax + 4f, inner, titleHeight);
            GUI.Label(line, tile.Title, title);
            if (textHeight > 0f)
                GUI.Label(new Rect(line.x, line.yMax, inner, textHeight), lines, text);
        }

        private static void Init()
        {
            if (title != null)
                return;
            title = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
            text = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            text.normal.textColor = Graph.Muted;
        }
    }
}
