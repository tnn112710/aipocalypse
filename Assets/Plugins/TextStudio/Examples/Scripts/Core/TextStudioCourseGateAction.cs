using System.Collections;
using UnityEngine;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>A scene-local action owns a small gameplay outcome, not text animation.</summary>
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioCourseGateAction : TextStudioActionComponent
    {
        public const string ActionId = "courseGate";
        protected override string DefaultId => ActionId;
        public override TextStudioActionDefinition ActionDefinition => Define(Id);
        public static TextStudioActionDefinition Define(string id) => new TextStudioActionDefinition(id, "Open the course gate", "Opens the gate and grants the authored reward.",
            aliases: new[] { "openCourseGate" }, examples: new[] { "<courseGate reward=25>" },
            parameters: new[] { new TextStudioTagParameterDefinition("reward", TextStudioTagParameterKind.Float, positionalIndex: 0, defaultValue: "25", minimum: 0, maximum: 100, aliases: new[] { "coins" }) });
        public override IEnumerator Execute(ActionContext context)
        {
            GetComponent<TextStudioExtensionLesson>().OpenGate(Mathf.RoundToInt(context.Marker.GetFloat("reward", 0, 25)), "Scene-local action");
            yield break;
        }
    }
}
