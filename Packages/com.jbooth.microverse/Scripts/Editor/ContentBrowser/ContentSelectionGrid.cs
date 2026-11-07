using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroVerseCore.Browser.ContentBrowser;
#if UNITY_6000_7_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace JBooth.MicroVerseCore.Browser
{

    /// <summary>
    /// Draw the contents of selected collection. Support dragging into the scene view
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class ContentSelectionGrid
    {
        private ContentBrowser contentBrowser;

        private SceneDragHandler sceneDragHandler;

        private static int cellSize = 96;
        private const int infoWidth = 240;

        private Vector2 contentScrollPosition = Vector2.zero;

        public PresetItem selectedPresetItem = null;

        /// <summary>
        /// Preset that was hit on mouse down. The drag operation uses this instead of resolving the item
        /// under the cursor again on mouse drag: the rects come from the layout cache of the previous Layout
        /// event, while the preset list is rebuilt on every event, so the two can disagree and the drag would
        /// pick up a neighbouring preset.
        /// </summary>
        private PresetItem dragCandidate = null;

        public ContentSelectionGrid(ContentBrowser contentBrowser)
        {
            this.contentBrowser = contentBrowser;
        }

        public void OnEnable()
        {
            // scene drag handler
            if (sceneDragHandler == null) 
            {
                sceneDragHandler = new SceneDragHandler(contentBrowser);
            }
            cellSize = EditorPrefs.GetInt("MicroVerseCBCellSize");
            if (cellSize <= 32)
                cellSize = 96;
            sceneDragHandler.OnEnable();

        }

        public void OnDisable()
        {
            // scene drag handler
            sceneDragHandler.OnDisable();
            sceneDragHandler = null;
        }

        /// <summary>
        /// Get the width of the info / description panel depending on whether the description is visible or not
        /// </summary>
        /// <returns></returns>
        private int GetInfoWidth()
        {
            return contentBrowser.IsDescriptionVisible() ? infoWidth : 0;
        }

        public void Draw(List<PresetItem> presets, Grouping grouping)
        {
            // handle movement of preset items via keyport shortcut
            ContentSelectionGridMovement.OnInspectorGUI( selectedPresetItem);

            int cellWidth = cellSize;
            int cellHeight = cellSize;
            float safetyMargin = cellWidth / 2f; // just some margin to keep the stamp preview almost fully visible
            float verticalScrollbarWidth = 22f;
            float gridWidth = EditorGUIUtility.currentViewWidth - contentBrowser.GetListWidth() - safetyMargin - GetInfoWidth() - verticalScrollbarWidth;
            int columnCount = Mathf.FloorToInt(gridWidth / cellWidth);

            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical("Box");
                {
                    contentScrollPosition = EditorGUILayout.BeginScrollView(contentScrollPosition, GUILayout.Width(gridWidth + 30));
                    {
                        var groups = presets.GroupBy(x => x.collection.packName);

                        int groupIndex = -1;
                        foreach (var group in groups)
                        {
                            groupIndex++;

                            string groupHeader = group.Key;

                            List<PresetItem> presetGroup = new List<PresetItem>();
                            presetGroup.AddRange(group);

                            if (grouping == Grouping.ContentType)
                            {
                                if (groupIndex > 0)
                                {
                                    GUILayout.Space(10f);
                                }

                                EditorGUILayout.LabelField(groupHeader, EditorStyles.miniBoldLabel);

                                // separator
                                GUILayout.Box(GUIContent.none, GUIStyles.SeparatorStyle, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
                                GUILayout.Space(2);

                            }

                            int gridRows = Mathf.CeilToInt((float)presetGroup.Count / columnCount);

                            for (int row = 0; row < gridRows; row++)
                            {
                                GUILayout.BeginHorizontal();
                                {
                                    for (int column = 0; column < columnCount; column++)
                                    {
                                        // get preset; we fill up the row/column grid, so at some point the preset will be null
                                        int index = column + row * columnCount;
                                        PresetItem presetItem = index < presetGroup.Count ? presetGroup[index] : null;

                                        Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(cellWidth), GUILayout.Height(cellHeight));

                                        if (presetItem != null)
                                        {
                                            var content = presetItem.GetGUIContent();
                                            Texture image = content.image;
                                            string label = content.text;

                                            if (selectedPresetItem != null && presetItem.content == selectedPresetItem.content)
                                            {
                                                var outline = rect;
                                                outline.max += Vector2.one;
                                                outline.min -= Vector2.one;

                                                Texture2D texture = EditorGUIUtility.isProSkin ? Texture2D.whiteTexture : Texture2D.blackTexture;
                                                GUI.DrawTexture(outline, texture, ScaleMode.ScaleToFit, false);

                                            }

                                            // check for context click on this cell
                                            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
                                            {
                                                Vector2 position = Event.current.mousePosition;

                                                Rect popupRect = new Rect(position, Vector2.zero); // 2nd parameter is the offset from mouse position

                                                // the popup operates on the preset that was right-clicked, keep the selection in sync with it
                                                selectedPresetItem = presetItem;

                                                PopupWindow.Show(popupRect, new ContentItemPopup(contentBrowser, presetItem));

                                                Event.current.Use();
                                            }

                                            // texture
                                            GUI.DrawTexture(rect, image != null ? image : Texture2D.blackTexture, ScaleMode.ScaleToFit, false);

                                            
                                            {
                                                // label
                                                GUIStyle style = GUIUtil.SelectionElementLabelStyle;
                                                style.fontSize = (int)(EditorStyles.wordWrappedMiniLabel.fontSize * (Mathf.InverseLerp(32, 128, cellSize) + 0.5f));
                                                Rect labelRect = rect;
                                                labelRect.height = style.CalcHeight(new GUIContent(label), labelRect.width);
                                                GUI.DrawTexture(labelRect, GUIUtil.LabelBackgroundTexture, ScaleMode.StretchToFill);
                                                GUI.Box(labelRect, label, style);
                                            }

                                            // remember what was hit; the drag operation below uses this
                                            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                                            {
                                                selectedPresetItem = presetItem;
                                                dragCandidate = presetItem;

                                                contentBrowser.Repaint();
                                            }
                                        }
                                    }

                                    // stretch, so that the content doesn't move position (flicker) when we resize the editorwindow
                                    GUILayout.FlexibleSpace();
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();

                    // start the drag with the preset that was hit on mouse down, not with whatever the
                    // cursor resolves to now. see dragCandidate
                    if (Event.current.type == EventType.MouseDrag && dragCandidate != null)
                    {
                        if (ContentBrowser.placementMode == PlacementMode.Place)
                        {
                            sceneDragHandler.OnDragStart(dragCandidate);

                            Event.current.Use();
                        }

                        dragCandidate = null;
                    }
                }
                GUILayout.EndVertical();

                if (contentBrowser.IsDescriptionVisible())
                {
                    GUILayout.BeginHorizontal("Box", GUILayout.Width(GetInfoWidth() - 20));
                    {
                        if (selectedPresetItem != null)
                        {
                            ContentData contentData = selectedPresetItem.content;
                            GUILayout.BeginVertical();
                            {
                                GUILayout.BeginVertical("box");
                                {
                                    var content = selectedPresetItem.GetGUIContent();
                                    EditorGUILayout.LabelField(content.text, EditorStyles.miniBoldLabel);

                                    if (!string.IsNullOrEmpty(contentData.description))
                                    {
                                        EditorGUILayout.LabelField(contentData.description, EditorStyles.wordWrappedMiniLabel);
                                    }
                                    GUILayout.FlexibleSpace();
                                }
                                GUILayout.EndVertical();

                                GUILayout.BeginVertical("box");
                                {
                                    EditorGUILayout.LabelField("Author", EditorStyles.miniBoldLabel);
                                    EditorGUILayout.LabelField(selectedPresetItem.collection.author, EditorStyles.miniLabel);
                                }
                                GUILayout.EndVertical();
                            }
                            GUILayout.EndVertical();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndHorizontal();

            if (contentBrowser.IsHelpBoxVisible())
            {
                GUILayout.BeginVertical("box");
                EditorGUI.BeginChangeCheck();
                if (ContentBrowser.placementMode == PlacementMode.Place)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Keyboard shortcuts:", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    cellSize = EditorGUILayout.IntSlider(cellSize, 32, 128, GUILayout.Width(infoWidth-10));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(1);
                    EditorGUILayout.LabelField("Shift at drag start: Set falloff override to global. For height stamps this scales the stamp to total terrain bounds.\nControl at drag start: For height stamps this positions the stamp at the center of the terrain.", EditorStyles.miniLabel);
                }
                else if (ContentBrowser.placementMode == PlacementMode.PaintSplinePath)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Keyboard shortcuts:", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    cellSize = EditorGUILayout.IntSlider(cellSize, 32, 128, GUILayout.Width(infoWidth - 10));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(1);
                    EditorGUILayout.LabelField("Shift Paint - Stay in this mode at end of stroke", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Keyboard shortcuts:", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    cellSize = EditorGUILayout.IntSlider(cellSize, 32, 128, GUILayout.Width(infoWidth-10));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(1);
                    EditorGUILayout.LabelField("Control Paint - Adds a clear stamp to the operation\nShift Paint - Stay in this mode at end of stroke", EditorStyles.miniLabel);
                }
                

                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetInt("MicroVerseCBCellSize", cellSize);
                }
                GUILayout.EndVertical();
            }
        }
    }
}