// Builds the shared enemy subtrees that are generated rather than drawn: unity command script run --file
// AgentScripts/Enemies/EnemyTrees.cs --entry EnemyTrees.Melee. Rebuilding keeps the asset's GUID, so prefabs stay wired;
// hand edits made in the Behavior Designer window are overwritten.
using Aipocalypse.Core.EnemySystem;
using Opsive.BehaviorDesigner.Runtime;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Composites;
using Opsive.BehaviorDesigner.Runtime.Tasks.Decorators;
using Opsive.BehaviorDesigner.Runtime.Tasks.Events;
using Opsive.GraphDesigner.Runtime;
using UnityEditor;
using UnityEngine;

public static class EnemyTrees
{
    public const string MeleePath = "Assets/_Game/Content/Enemies/_Shared/Melee.asset";
    const ushort None = ushort.MaxValue;

    // Repeat forever: fight the player while in reach (chase, swing), else wander. The fight's abort (Both) re-checks
    // PlayerInReach every think: it cuts into the wander when the player comes near, and ends the fight when they get away.
    public static string Melee()
    {
        var subtree = AssetDatabase.LoadAssetAtPath<Subtree>(MeleePath);
        bool created = subtree == null;
        if (created)
            subtree = ScriptableObject.CreateInstance<Subtree>();
        subtree.name = "Melee";
        subtree.EventNodes = new IEventNode[] { new Start { ConnectedIndex = 0 } };
        subtree.TreeLogicNodes = new ITreeLogicNode[]
        {
            new Repeater { Index = 0, ParentIndex = None, SiblingIndex = None, RepeatForever = true },
            new Selector { Index = 1, ParentIndex = 0, SiblingIndex = None },
            new Sequence { Index = 2, ParentIndex = 1, SiblingIndex = 6, AbortType = ConditionalAbortType.Both },
            new PlayerInReach { Index = 3, ParentIndex = 2, SiblingIndex = 4 },
            new ChaseTarget { Index = 4, ParentIndex = 2, SiblingIndex = 5 },
            new MeleeAttack { Index = 5, ParentIndex = 2, SiblingIndex = None },
            new Sequence { Index = 6, ParentIndex = 1, SiblingIndex = None },
            new ChooseWanderDestination { Index = 7, ParentIndex = 6, SiblingIndex = 8 },
            new WalkToWanderDestination { Index = 8, ParentIndex = 6, SiblingIndex = 9 },
            new PauseWander { Index = 9, ParentIndex = 6, SiblingIndex = None },
        };
        subtree.EventNodeProperties = new[] { new NodeProperties { Position = new Vector2(0, 0) } };
        subtree.LogicNodeProperties = new[]
        {
            Props("Repeat forever", 0, 100), Props("Fight, else wander", 0, 220),
            Props("Fight (aborts both ways)", -400, 350), Props("Player in reach?", -650, 480), Props("Chase", -400, 480), Props("Swing", -150, 480),
            Props("Wander", 400, 350), Props("Choose reachable point", 150, 480), Props("Walk to point", 400, 480), Props("Pause", 650, 480),
        };
        subtree.Serialize();
        if (created)
            AssetDatabase.CreateAsset(subtree, MeleePath);
        else
            EditorUtility.SetDirty(subtree);
        AssetDatabase.SaveAssetIfDirty(subtree);
        return (created ? "created " : "rebuilt ") + MeleePath;
    }

    static LogicNodeProperties Props(string label, float x, float y) => new LogicNodeProperties { DisplayName = label, Position = new Vector2(x, y) };
}
