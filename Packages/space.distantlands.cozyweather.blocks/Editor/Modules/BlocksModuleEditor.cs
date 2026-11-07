using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using DistantLands.Cozy.Blocks;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(BlocksModule))]
    public class BlocksModuleEditor : CozyModuleEditor
    {
        BlocksModule blocksModule;
        void OnEnable()
        {
            blocksModule = (BlocksModule)target;
        }

        public override VisualElement DisplayUI()
        {
            VisualElement root = new VisualElement();

            Tooltip tooltip = new Tooltip($"No current block");
            tooltip.style.marginTop = 8;
            if (blocksModule.CurrentBlockProfile)
                tooltip.Message = $"Current Block is {blocksModule.CurrentBlockProfile.name}";
            else
                tooltip.Message = $"No current block";

            tooltip.schedule.Execute(() =>
            {
                if (blocksModule.CurrentBlockProfile)
                    tooltip.Q<Label>().text = $"Current Block is {blocksModule.CurrentBlockProfile.name}";
                else
                    tooltip.Q<Label>().text = $"No current block";
            }).Every(500);
            root.Add(tooltip);

            VisualElement profileContainer = new VisualElement();
            profileContainer.AddToClassList("section-bg");
            root.Add(profileContainer);

            PropertyField collection = new PropertyField();
            collection.BindProperty(serializedObject.FindProperty("collection"));
            profileContainer.Add(collection);

            VisualElement testBlockContainer = new VisualElement();
            testBlockContainer.AddToClassList("section-bg");
            root.Add(testBlockContainer);

            PropertyField testBlock = new PropertyField();
            testBlock.BindProperty(serializedObject.FindProperty("testBlock"));
            testBlockContainer.Add(testBlock);

            return root;
        }
    }
}