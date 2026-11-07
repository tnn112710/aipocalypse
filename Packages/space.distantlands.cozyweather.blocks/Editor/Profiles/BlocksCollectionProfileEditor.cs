using UnityEditor;
using UnityEngine;
using DistantLands.Cozy.Blocks;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(BlocksCollectionProfile))]
    [CanEditMultipleObjects]
    public class BlocksCollectionProfileEditor : Editor
    {
        private int selectedIndex = 0;

        static Gradient BlockColorsGradient => new()
        {
            colorKeys = new GradientColorKey[5]
            {
                new(Branding.charcoal, 0),
                new(Branding.red, 0.25f),
                new(Branding.blue, 0.5f),
                new(Branding.orange, 0.75f),
                new(Branding.charcoal, 1)
            }
        };

        BlocksCollectionProfile profile;

        VisualElement timeGraph;
        VisualElement timeBlockProperties;
        Button addBlockTimeButton;
        int hoveredInsertIndex;

        /// <summary>
        /// This function is called when the object becomes enabled and active.
        /// </summary>
        void OnEnable()
        {
            profile = (BlocksCollectionProfile)target;
            selectedIndex = 0;

        }

        public override VisualElement CreateInspectorGUI()
        {
            SerializedProperty blockTimesProperty = serializedObject.FindProperty("blockTimes");

            VisualElement root = new();

            var mainStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss");
            if (mainStyle != null) root.styleSheets.Add(mainStyle);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.blocks/Editor/UI/Editor Styles.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            timeGraph = new VisualElement();
            root.Add(timeGraph);
            timeGraph.style.height = 50;
            timeGraph.style.overflow = Overflow.Hidden;
            timeGraph.style.borderBottomLeftRadius = 15;
            timeGraph.style.borderBottomRightRadius = 15;
            timeGraph.style.borderTopLeftRadius = 15;
            timeGraph.style.borderTopRightRadius = 15;
            timeGraph.style.marginTop = 8;
            timeGraph.style.marginBottom = 8;
            timeGraph.RegisterCallback<PointerMoveEvent>(OnTimeGraphPointerMove);
            timeGraph.RegisterCallback<PointerLeaveEvent>(evt => addBlockTimeButton.style.display = DisplayStyle.None);

            addBlockTimeButton = new Button(() => AddBlockTime(hoveredInsertIndex)) { text = "+" };
            addBlockTimeButton.tooltip = "Add block time";
            addBlockTimeButton.style.position = Position.Absolute;
            addBlockTimeButton.style.width = 18;
            addBlockTimeButton.style.height = 18;
            addBlockTimeButton.style.marginLeft = -9;
            addBlockTimeButton.style.marginTop = -9;
            addBlockTimeButton.style.top = new Length(50, LengthUnit.Percent);
            addBlockTimeButton.style.borderTopLeftRadius = 12;
            addBlockTimeButton.style.borderTopRightRadius = 12;
            addBlockTimeButton.style.borderBottomLeftRadius = 12;
            addBlockTimeButton.style.borderBottomRightRadius = 12;
            addBlockTimeButton.style.display = DisplayStyle.None;
            addBlockTimeButton.style.backgroundColor = Color.black;
            addBlockTimeButton.style.borderTopWidth = 0;
            addBlockTimeButton.style.borderLeftWidth = 0;
            addBlockTimeButton.style.borderRightWidth = 0;
            addBlockTimeButton.style.borderBottomWidth = 0;
            RebuildGraph();

            timeBlockProperties = new VisualElement();
            timeBlockProperties.style.display = DisplayStyle.None;
            timeBlockProperties.AddToClassList("section-bg");
            root.Add(timeBlockProperties);
            BuildTimeBlockProperties();
            timeGraph.schedule.Execute(RebuildGraph).Every(500);

            return root;
        }

        private void OnTimeGraphPointerMove(PointerMoveEvent evt)
        {
            SerializedProperty blockTimesProperty = serializedObject.FindProperty("blockTimes");
            int count = blockTimesProperty.arraySize;
            float graphWidth = timeGraph.contentRect.width;
            hoveredInsertIndex = 0;

            if (graphWidth <= 0)
                return;

            float pointerPosition = Mathf.Clamp01(evt.localPosition.x / graphWidth);
            float buttonPosition;

            if (count <= 1)
            {
                buttonPosition = graphWidth * 0.5f;
            }
            else
            {
                float firstBlockStart = profile.blockTimes[0].startTime.AsPercentage();
                if (pointerPosition < firstBlockStart)
                {
                    hoveredInsertIndex = count - 1;
                    buttonPosition = firstBlockStart * 0.5f * graphWidth;
                }
                else
                {
                    buttonPosition = graphWidth * 0.5f;
                    for (int i = 0; i < count; i++)
                    {
                        float start = profile.blockTimes[i].startTime.AsPercentage();
                        float end = i + 1 < count
                            ? profile.blockTimes[i + 1].startTime.AsPercentage()
                            : 1f;
                        if (pointerPosition >= start && pointerPosition <= end)
                        {
                            hoveredInsertIndex = i;
                            buttonPosition = (start + end) * 0.5f * graphWidth;
                            break;
                        }
                    }
                }
            }

            addBlockTimeButton.style.left = Mathf.Clamp(buttonPosition, 9, Mathf.Max(9, graphWidth - 9));
            addBlockTimeButton.style.display = DisplayStyle.Flex;
        }

        private void AddBlockTime(int index)
        {
            serializedObject.Update();
            SerializedProperty blockTimesProperty = serializedObject.FindProperty("blockTimes");
            int blockCount = blockTimesProperty.arraySize;
            int insertionIndex;
            MeridiemTime newStartTime = MeridiemTime.Midnight;

            if (blockCount > 0)
            {
                index = Mathf.Clamp(index, 0, blockCount - 1);
                BlockCollection previousBlock = profile.blockTimes[index];
                int nextIndex = (index + 1) % blockCount;
                float currentStart = previousBlock.startTime.AsPercentage();
                float nextStart = profile.blockTimes[nextIndex].startTime.AsPercentage();
                if (nextIndex == 0)
                    nextStart += 1f;

                float gap = nextStart - currentStart;
                if (gap <= 0f)
                {
                    Debug.LogWarning("Cannot add a time block here because the neighboring blocks have no available time between them.", profile);
                    return;
                }

                newStartTime = previousBlock.startTime + (MeridiemTimeDelta)(gap * 0.5f);
                if (newStartTime == previousBlock.startTime ||
                    newStartTime == profile.blockTimes[nextIndex].startTime)
                {
                    Debug.LogWarning("Cannot add a time block here because the available space is smaller than the time block resolution.", profile);
                    return;
                }

                insertionIndex = index + 1;
            }
            else
            {
                insertionIndex = 0;
            }

            blockTimesProperty.InsertArrayElementAtIndex(insertionIndex);
            SerializedProperty newBlockProperty = blockTimesProperty.GetArrayElementAtIndex(insertionIndex);
            newBlockProperty.FindPropertyRelative("blockName").stringValue = "New Time Block";
            SetMeridiemTime(newBlockProperty.FindPropertyRelative("startTime"), newStartTime);
            newBlockProperty.FindPropertyRelative("transitionPercentage").floatValue = 0f;

            SerializedProperty blocksProperty = newBlockProperty.FindPropertyRelative("blocks");
            if (blockCount == 0 && blocksProperty != null && blocksProperty.isArray)
                blocksProperty.arraySize = 0;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            selectedIndex = profile.blockTimes.FindIndex(block =>
                block.blockName == "New Time Block" && block.startTime == newStartTime);

            RebuildGraph();
            BuildTimeBlockProperties();
        }

        private static void SetMeridiemTime(SerializedProperty property, MeridiemTime value)
        {
            property.FindPropertyRelative("hours").intValue = value.hours;
            property.FindPropertyRelative("minutes").intValue = value.minutes;
            property.FindPropertyRelative("seconds").intValue = value.seconds;
            property.FindPropertyRelative("milliseconds").intValue = value.milliseconds;
            property.FindPropertyRelative("meridiem").enumValueIndex = (int)value.meridiem;
        }

        private void RemoveSelectedBlockTime()
        {
            serializedObject.Update();
            SerializedProperty blockTimesProperty = serializedObject.FindProperty("blockTimes");
            if (selectedIndex < 0 || selectedIndex >= blockTimesProperty.arraySize)
                return;

            blockTimesProperty.DeleteArrayElementAtIndex(selectedIndex);
            selectedIndex = blockTimesProperty.arraySize == 0
                ? -1
                : Mathf.Min(selectedIndex, blockTimesProperty.arraySize - 1);
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();

            RebuildGraph();
            BuildTimeBlockProperties();
        }

        public void BuildTimeBlockProperties()
        {
            SerializedProperty blockTimesProperty = serializedObject.FindProperty("blockTimes");

            if (selectedIndex < 0 || selectedIndex >= blockTimesProperty.arraySize)
            {
                timeBlockProperties.style.display = DisplayStyle.None;
                return;
            }
            else
            {
                timeBlockProperties.style.display = DisplayStyle.Flex;
            }

            SerializedProperty currentBlockSelection = blockTimesProperty.GetArrayElementAtIndex(selectedIndex);
            timeBlockProperties.Clear();

            PropertyField blockName = new PropertyField();
            blockName.BindProperty(currentBlockSelection.FindPropertyRelative("blockName"));
            timeBlockProperties.Add(blockName);

            PropertyField startTime = new PropertyField();
            startTime.BindProperty(currentBlockSelection.FindPropertyRelative("startTime"));
            startTime.RegisterValueChangeCallback(evt =>
            {
                RebuildGraph();
            });
            timeBlockProperties.Add(startTime);

            PropertyField transitionPercentage = new PropertyField();
            transitionPercentage.BindProperty(currentBlockSelection.FindPropertyRelative("transitionPercentage"));
            transitionPercentage.RegisterValueChangeCallback(evt =>
            {
                RebuildGraph();
            });
            timeBlockProperties.Add(transitionPercentage);

            PropertyField blocks = new PropertyField();
            blocks.BindProperty(currentBlockSelection.FindPropertyRelative("blocks"));
            timeBlockProperties.Add(blocks);

            Button removeBlockTimeButton = new Button(RemoveSelectedBlockTime) { text = "Remove Block Time" };
            removeBlockTimeButton.style.marginTop = 6;
            timeBlockProperties.Add(removeBlockTimeButton);
        }

        public VisualElement BuildBlockGraphNode(float min, float max, int index, ref Color cachedColor)
        {
            Color currentBlockColor = index == profile.blockTimes.Count - 1 ? BlockColorsGradient.Evaluate(0) : BlockColorsGradient.Evaluate((min + max) / 2f);
            Color previousBlockColor = cachedColor;
            BlockCollection currentBlock = profile.blockTimes[index];

            VisualElement graphElement = new();
            graphElement.AddToClassList("timeGraph");
            if (index == selectedIndex)
                graphElement.AddToClassList("active");

            graphElement.style.backgroundColor = index == profile.blockTimes.Count - 1 ? BlockColorsGradient.Evaluate(0) : BlockColorsGradient.Evaluate((min + max) / 2f);
            graphElement.style.position = Position.Absolute;
            graphElement.style.top = 0;
            graphElement.style.bottom = 0;
            graphElement.style.left = new Length(min * 100f, LengthUnit.Percent);
            graphElement.style.width = new Length((max - (min)) * 100f, LengthUnit.Percent);
            graphElement.style.borderRightColor = Color.white;
            if (max != 1)
                graphElement.style.borderRightWidth = 1;

            graphElement.generateVisualContent += ctx =>
            {
                if (min == 0)
                    return;

                var painter = ctx.painter2D;
                float width = graphElement.contentRect.width;
                float height = graphElement.contentRect.height;

                Gradient gradient = new()
                {
                    colorKeys = new GradientColorKey[2]
                    {
                        new(currentBlockColor, 1),
                        new(previousBlockColor, 0)
                    }
                };

                painter.fillGradient = new FillGradient()
                {
                    gradientType = GradientType.Linear,
                    addressMode = AddressMode.Clamp,
                    gradient = gradient,
                    start = new Vector2(0, 0),
                    end = new Vector2(width * currentBlock.transitionPercentage, 0)
                };

                painter.BeginPath();
                painter.MoveTo(new Vector2(0, 0));
                painter.LineTo(new Vector2(width * currentBlock.transitionPercentage, 0));
                painter.LineTo(new Vector2(width * currentBlock.transitionPercentage, height));
                painter.LineTo(new Vector2(0, height));
                painter.Fill();

                painter.strokeColor = new Color(1, 1, 1, 0.35f);
                painter.lineWidth = 2;

                painter.BeginPath();
                painter.MoveTo(new Vector2(width * currentBlock.transitionPercentage, 0));
                painter.LineTo(new Vector2(width * currentBlock.transitionPercentage, height));
                painter.Stroke();

            };

            graphElement.RegisterCallback<PointerDownEvent>(evt =>
            {
                selectedIndex = index;
                RebuildGraph();
                BuildTimeBlockProperties();
            });

            cachedColor = currentBlockColor;
            return graphElement;
        }

        public void RebuildGraph()
        {
            serializedObject.Update();
            SerializedProperty blockTimesProperty = serializedObject.FindProperty("blockTimes");
            Color cachedColor = Color.clear;
            timeGraph.Clear();

            if (blockTimesProperty.arraySize > 0)
            {
                if (blockTimesProperty.arraySize > 1)
                {
                    timeGraph.Add(BuildBlockGraphNode(0, profile.blockTimes[0].startTime, blockTimesProperty.arraySize - 1, ref cachedColor));
                }

                for (int i = 0; i < blockTimesProperty.arraySize; i++)
                {
                    float min = profile.blockTimes[i].startTime;
                    float max = 1;
                    if (i + 1 < blockTimesProperty.arraySize)
                        max = profile.blockTimes[i + 1].startTime;

                    timeGraph.Add(BuildBlockGraphNode(min, max, i, ref cachedColor));
                }
            }

            timeGraph.Add(addBlockTimeButton);
        }
    }
}
