using System;
using System.Collections.Generic;

namespace TextStudio.Editor
{
    /// <summary>The primary Text Studio workflow demonstrated or installed by a gallery card.</summary>
    public enum TextGalleryWorkflow
    {
        Unspecified,
        Setup,
        Typewriter,
        EffectTags,
        DefaultEffects,
        TagStack,
        Appearance,
        Writing,
        ComposedEffect,
        CompleteDesign,
        Placement,
        Renderer
    }

    /// <summary>Renderer families on which a gallery recipe can be used as authored.</summary>
    [Flags]
    public enum TextGalleryRendererSupport
    {
        None = 0,
        TextMeshPro = 1 << 0,
        UIToolkit = 1 << 1,
        Extruded3D = 1 << 2,
        Mechanical = 1 << 3,
        Terminal = 1 << 4,
        FlatText = TextMeshPro | UIToolkit
    }

    /// <summary>High-level render cost disclosed by a gallery recipe.</summary>
    public enum TextGalleryRenderCost
    {
        Unspecified,
        InPlace,
        FusedSingleDraw,
        AdditionalGeometry,
        AdditionalPass,
        HeroComposite,
        RendererManaged
    }

    /// <summary>External or prepared data required by a gallery recipe.</summary>
    [Flags]
    public enum TextGalleryResourceRequirements
    {
        None = 0,
        BuiltInAssets = 1 << 0,
        Texture = 1 << 1,
        PreparedWritingData = 1 << 2,
        RendererPackage = 1 << 3
    }

    /// <summary>Behavior exposed when users request less motion.</summary>
    public enum TextGalleryReducedMotionSupport
    {
        Unspecified,
        MotionFree,
        Preserved,
        Reduced,
        Disabled
    }

    /// <summary>
    /// Typed production guidance shown alongside a gallery card. Preview fidelity remains a separate
    /// concern so a card never implies that its lightweight editor preview is the runtime render cost.
    /// </summary>
    public readonly struct TextGalleryProductionMetadata
    {
        public TextGalleryWorkflow Workflow { get; }
        public TextGalleryRendererSupport RendererSupport { get; }
        public TextGalleryRenderCost RenderCost { get; }
        public TextGalleryResourceRequirements ResourceRequirements { get; }
        public TextGalleryReducedMotionSupport ReducedMotionSupport { get; }

        public bool IsSpecified => Workflow != TextGalleryWorkflow.Unspecified;

        /// <summary>Creates complete, searchable production guidance for a gallery card.</summary>
        public TextGalleryProductionMetadata(
            TextGalleryWorkflow workflow,
            TextGalleryRendererSupport rendererSupport,
            TextGalleryRenderCost renderCost,
            TextGalleryResourceRequirements resourceRequirements,
            TextGalleryReducedMotionSupport reducedMotionSupport)
        {
            Workflow = workflow;
            RendererSupport = rendererSupport;
            RenderCost = renderCost;
            ResourceRequirements = resourceRequirements;
            ReducedMotionSupport = reducedMotionSupport;
        }

        internal string BuildDisplayLine()
        {
            if (!IsSpecified)
                return string.Empty;

            List<string> values = new List<string>(5)
            {
                FormatWorkflow(Workflow),
                FormatRendererSupport(RendererSupport),
                FormatRenderCost(RenderCost)
            };
            if (ResourceRequirements != TextGalleryResourceRequirements.None)
                values.Add(FormatResources(ResourceRequirements));
            values.Add(FormatReducedMotion(ReducedMotionSupport));
            return string.Join("  •  ", values);
        }

        internal string BuildTooltip()
        {
            if (!IsSpecified)
                return string.Empty;

            return FormatWorkflowTooltip(Workflow) + "\n" +
                   FormatRendererTooltip(RendererSupport) + "\n" +
                   FormatRenderCostTooltip(RenderCost) + "\n" +
                   FormatResourceTooltip(ResourceRequirements) + "\n" +
                   FormatReducedMotionTooltip(ReducedMotionSupport);
        }

        static string FormatWorkflow(TextGalleryWorkflow workflow)
        {
            return workflow switch
            {
                TextGalleryWorkflow.EffectTags => "Effect Tags",
                TextGalleryWorkflow.DefaultEffects => "Default Effects",
                TextGalleryWorkflow.TagStack => "Tag Stack",
                TextGalleryWorkflow.ComposedEffect => "Composed Effect",
                TextGalleryWorkflow.CompleteDesign => "Complete Design",
                _ => workflow.ToString()
            };
        }

        static string FormatRendererSupport(TextGalleryRendererSupport support)
        {
            if (support == TextGalleryRendererSupport.FlatText)
                return "TMP + UI Toolkit";
            if (support == TextGalleryRendererSupport.TextMeshPro)
                return "TMP";
            if (support == TextGalleryRendererSupport.UIToolkit)
                return "UI Toolkit";
            if (support == TextGalleryRendererSupport.Extruded3D)
                return "3D";
            if (support == TextGalleryRendererSupport.Mechanical)
                return "Mechanical";
            if (support == TextGalleryRendererSupport.Terminal)
                return "Terminal";

            List<string> values = new List<string>(5);
            AddRenderer(values, support, TextGalleryRendererSupport.TextMeshPro, "TMP");
            AddRenderer(values, support, TextGalleryRendererSupport.UIToolkit, "UI Toolkit");
            AddRenderer(values, support, TextGalleryRendererSupport.Extruded3D, "3D");
            AddRenderer(values, support, TextGalleryRendererSupport.Mechanical, "Mechanical");
            AddRenderer(values, support, TextGalleryRendererSupport.Terminal, "Terminal");
            return values.Count == 0 ? "Renderer-specific" : string.Join(" + ", values);
        }

        static void AddRenderer(
            List<string> values,
            TextGalleryRendererSupport support,
            TextGalleryRendererSupport flag,
            string label)
        {
            if ((support & flag) != 0)
                values.Add(label);
        }

        static string FormatRenderCost(TextGalleryRenderCost cost)
        {
            return cost switch
            {
                TextGalleryRenderCost.InPlace => "In-place",
                TextGalleryRenderCost.FusedSingleDraw => "Fused single draw",
                TextGalleryRenderCost.AdditionalGeometry => "Extra geometry",
                TextGalleryRenderCost.AdditionalPass => "Extra pass",
                TextGalleryRenderCost.HeroComposite => "Hero composite",
                TextGalleryRenderCost.RendererManaged => "Renderer-managed",
                _ => "Cost varies"
            };
        }

        static string FormatResources(TextGalleryResourceRequirements requirements)
        {
            List<string> values = new List<string>(4);
            if ((requirements & TextGalleryResourceRequirements.BuiltInAssets) != 0)
                values.Add("Built-in assets");
            if ((requirements & TextGalleryResourceRequirements.Texture) != 0)
                values.Add("Texture");
            if ((requirements & TextGalleryResourceRequirements.PreparedWritingData) != 0)
                values.Add("Prepared paths");
            if ((requirements & TextGalleryResourceRequirements.RendererPackage) != 0)
                values.Add("Add-on");
            return values.Count == 0 ? "No resources" : string.Join(" + ", values);
        }

        static string FormatReducedMotion(TextGalleryReducedMotionSupport support)
        {
            return support switch
            {
                TextGalleryReducedMotionSupport.MotionFree => "Motion-free",
                TextGalleryReducedMotionSupport.Preserved => "Motion preserved",
                TextGalleryReducedMotionSupport.Reduced => "Reduced-motion",
                TextGalleryReducedMotionSupport.Disabled => "Motion disables",
                _ => "Motion varies"
            };
        }

        static string FormatWorkflowTooltip(TextGalleryWorkflow workflow)
        {
            return "Workflow: " + FormatWorkflow(workflow) + ".";
        }

        static string FormatRendererTooltip(TextGalleryRendererSupport support)
        {
            return "Renderer support: " + FormatRendererSupport(support) + ".";
        }

        static string FormatRenderCostTooltip(TextGalleryRenderCost cost)
        {
            return cost switch
            {
                TextGalleryRenderCost.InPlace => "Render cost: modifies the existing text render without adding geometry or a separate compositor.",
                TextGalleryRenderCost.FusedSingleDraw => "Render cost: compatible appearance layers are fused into one field render and one text draw.",
                TextGalleryRenderCost.AdditionalGeometry => "Render cost: creates retained supporting geometry in addition to the primary text.",
                TextGalleryRenderCost.AdditionalPass => "Render cost: uses at least one additional render pass.",
                TextGalleryRenderCost.HeroComposite => "Render cost: intended for hero text where additional composition work is acceptable.",
                TextGalleryRenderCost.RendererManaged => "Render cost: controlled by the selected renderer or add-on topology.",
                _ => "Render cost varies with the authored configuration."
            };
        }

        static string FormatResourceTooltip(TextGalleryResourceRequirements requirements)
        {
            return requirements == TextGalleryResourceRequirements.None
                ? "Resources: procedural or already available on the text object."
                : "Resources: " + FormatResources(requirements) + ".";
        }

        static string FormatReducedMotionTooltip(TextGalleryReducedMotionSupport support)
        {
            return support switch
            {
                TextGalleryReducedMotionSupport.MotionFree => "Reduced motion: the authored result contains no required motion.",
                TextGalleryReducedMotionSupport.Preserved => "Reduced motion: the authored motion remains enabled.",
                TextGalleryReducedMotionSupport.Reduced => "Reduced motion: displacement and temporal intensity are reduced.",
                TextGalleryReducedMotionSupport.Disabled => "Reduced motion: the motion layer is disabled while the readable design remains.",
                _ => "Reduced motion: behavior depends on the selected recipe."
            };
        }
    }
}
