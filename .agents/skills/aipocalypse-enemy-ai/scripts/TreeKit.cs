// Behavior Designer Pro trees as text, for `unity command script run --file <this> --entry TreeKit.<Method> --args '[...]'`.
// Nothing here touches the open scene. Spec format: references/tree-specs.md.
//
//   Describe(path, outSpecPath = "")   a Subtree asset, or the BehaviorTree on a prefab, as a spec (round-trips with Build);
//                                      writes it to outSpecPath when given
//   Check(specPath)                    parse and validate a spec without writing anything
//   Build(specPath)                    create or rebuild the Subtree asset named in the spec (keeps its GUID), then
//                                      returns the asset described back
//   Tasks(filter = "")                 every task and event node available (no samples/demos), with category,
//                                      description and tunable fields; filter matches name, category or namespace
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Opsive.BehaviorDesigner.Runtime;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Events;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TreeKit
{
    const ushort None = ushort.MaxValue;
    const float ColumnWidth = 230f;
    const float RowHeight = 130f;

    static readonly HashSet<string> s_internalFields = new()
    {
        "m_Index", "m_ParentIndex", "m_SiblingIndex", "m_Enabled", "m_ComponentIndex", "m_RuntimeIndex",
        "m_GameObject", "m_Transform", "m_BehaviorTree", "m_ConnectedIndex",
    };

    sealed class Node
    {
        public string TypeToken;
        public Type Type;
        public readonly List<(string Key, string Value)> Fields = new();
        public string DisplayName = "";
        public string Comment = "";
        public bool Disabled;
        public int Line;
        public Node Parent;
        public readonly List<Node> Children = new();
        public object Instance;
        public ushort Index;
        public float X;
        public int Depth;
    }

    sealed class Spec
    {
        public string AssetPath;
        public readonly List<(string Type, string Name, string Value, bool Internal)> Variables = new();
        public readonly List<Node> Roots = new();
    }

    // ---------------------------------------------------------------- Describe

    public static string Describe(string path, string outSpecPath = "")
    {
        var sb = new StringBuilder();
        IGraph graph;
        if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return "ERROR no prefab at " + path;
            var tree = prefab.GetComponentInChildren<BehaviorTree>(true);
            if (tree == null) return "ERROR no BehaviorTree on " + path;
            sb.AppendLine($"# BehaviorTree on {path} ({tree.gameObject.name}): UpdateMode {tree.UpdateMode}, StartWhenEnabled {tree.StartWhenEnabled}, " +
                          $"PauseWhenDisabled {tree.PauseWhenDisabled}, EvaluationType {tree.EvaluationType}");
            if (tree.Subgraph is Subtree sub)
            {
                sb.AppendLine("# Subgraph " + AssetDatabase.GetAssetPath(sub));
                graph = sub;
                path = AssetDatabase.GetAssetPath(sub);
            }
            else
            {
                graph = tree;
                sb.AppendLine("# graph stored on the component (Build only writes Subtree assets)");
            }
        }
        else
        {
            var subtree = AssetDatabase.LoadAssetAtPath<Subtree>(path);
            if (subtree == null) return "ERROR no Subtree at " + path;
            graph = subtree;
        }

        if (graph is Subtree s) s.Deserialize(true);
        var text = sb + Write(graph, path);
        if (!string.IsNullOrEmpty(outSpecPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outSpecPath)));
            File.WriteAllText(outSpecPath, text);
        }
        return text;
    }

    static string Write(IGraph graph, string assetPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("tree " + assetPath);
        foreach (var variable in graph.SharedVariables ?? Array.Empty<SharedVariable>())
        {
            if (variable == null) continue;
            var value = FormatValue(variable.GetValue(), variable.ElementType);
            var defaultValue = FormatValue(Default(variable.ElementType), variable.ElementType);
            sb.Append("var ").Append(TypeName(variable.ElementType)).Append(' ').Append(variable.Name.Contains(' ') ? "\"" + variable.Name + "\"" : variable.Name);
            if (value != null && value != defaultValue) sb.Append(" = ").Append(value);
            if (variable.IsInternal) sb.Append(" internal");
            sb.AppendLine();
        }

        var logic = graph.LogicNodes ?? Array.Empty<ILogicNode>();
        var props = graph.LogicNodeProperties ?? Array.Empty<LogicNodeProperties>();
        var disabled = new HashSet<ushort>(GetMember(graph, "DisabledLogicNodes") as ushort[] ?? Array.Empty<ushort>());
        var events = graph.EventNodes ?? Array.Empty<IEventNode>();
        var nodes = logic.Cast<ITreeLogicNode>().ToArray();

        var eventProps = graph.EventNodeProperties ?? Array.Empty<NodeProperties>();
        bool implicitStart = events.Length == 1 && events[0] is Start && (eventProps.Length == 0 || string.IsNullOrEmpty(eventProps[0]?.DisplayName));
        for (int e = 0; e < events.Length; e++)
        {
            var eventNode = events[e];
            var connected = (ushort)GetMember(eventNode, "ConnectedIndex");
            int depth = 0;
            if (!implicitStart)
            {
                sb.AppendLine(NodeLine(eventNode, e < eventProps.Length ? eventProps[e] : null, false));
                depth = 1;
            }
            if (connected != None && connected < nodes.Length)
                WriteBranch(sb, nodes, props, disabled, connected, depth);
        }

        var orphans = nodes.Where(n => n.ParentIndex == None && !events.Any(e => (ushort)GetMember(e, "ConnectedIndex") == n.Index)).ToArray();
        foreach (var orphan in orphans)
        {
            sb.AppendLine("# not connected to an event node (Build drops these):");
            WriteBranch(sb, nodes, props, disabled, orphan.Index, 0, "# ");
        }
        return sb.ToString();
    }

    static void WriteBranch(StringBuilder sb, ITreeLogicNode[] nodes, LogicNodeProperties[] props, HashSet<ushort> disabled, ushort index, int depth, string prefix = "")
    {
        var node = nodes[index];
        var p = index < props.Length ? props[index] : null;
        sb.Append(prefix).Append(new string(' ', depth * 2)).AppendLine(NodeLine(node, p, disabled.Contains(index)));
        foreach (var child in ChildrenOf(nodes, index))
            WriteBranch(sb, nodes, props, disabled, child, depth + 1, prefix);
    }

    static IEnumerable<ushort> ChildrenOf(ITreeLogicNode[] nodes, ushort parent)
    {
        var children = nodes.Where(n => n.ParentIndex == parent).Select(n => n.Index).ToHashSet();
        var siblings = children.Select(c => nodes[c].SiblingIndex).ToHashSet();
        var heads = children.Where(c => !siblings.Contains(c)).OrderBy(c => c).ToList();
        var first = heads.Count > 0 ? heads[0] : None;
        for (var c = first; c != None && children.Contains(c); c = nodes[c].SiblingIndex)
        {
            yield return c;
            children.Remove(c);
        }
        foreach (var rest in children.OrderBy(c => c)) yield return rest; // broken sibling chain: still list them
    }

    static string NodeLine(object node, NodeProperties props, bool disabled)
    {
        disabled |= GetMember(node, "Enabled") is false;
        // A stack holding one task with default stack settings prints as that task (Build wraps it again).
        if (IsA(node.GetType(), "StackedTask") && SerializedFields(node.GetType()).ToList() is var stackFields &&
            stackFields.FirstOrDefault(f => FieldKey(f.Name) == "Tasks")?.GetValue(node) is Array { Length: 1 } single &&
            single.GetValue(0) is { } inner && CreateDefault(node.GetType()) is { } freshStack &&
            stackFields.Where(f => FieldKey(f.Name) != "Tasks").All(f => FormatField(f.GetValue(node), f.FieldType) == FormatField(f.GetValue(freshStack), f.FieldType)) &&
            !inner.GetType().IsGenericType)
            node = inner;
        var sb = new StringBuilder(ShortTypeName(node.GetType()));
        var fresh = CreateDefault(node.GetType());
        foreach (var field in SerializedFields(node.GetType()))
        {
            var value = FormatField(field.GetValue(node), field.FieldType);
            var defaultValue = fresh == null ? null : FormatField(field.GetValue(fresh), field.FieldType);
            if (value == null || value == defaultValue) continue;
            sb.Append(' ').Append(FieldKey(field.Name)).Append('=').Append(value);
        }
        if (disabled) sb.Append(" !disabled");
        if (props != null)
        {
            if (!string.IsNullOrEmpty(props.DisplayName)) sb.Append(" \"").Append(props.DisplayName.Replace("\"", "'")).Append('"');
            if (!string.IsNullOrEmpty(props.Comment)) sb.Append(" // ").Append(props.Comment.Replace("\n", " "));
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- Check / Build

    public static string Check(string specPath)
    {
        var errors = new List<string>();
        var spec = Parse(File.ReadAllText(specPath), errors);
        if (spec != null) Prepare(spec, errors);
        if (errors.Count > 0) return "ERRORS\n" + string.Join("\n", errors);
        int count = 0;
        foreach (var root in spec.Roots) count += Flatten(root).Count(n => !IsEventType(n.Type));
        return $"OK {specPath}: {count} logic nodes, {spec.Roots.Count} event branch(es), {spec.Variables.Count} variable(s) -> {spec.AssetPath}";
    }

    public static string Build(string specPath)
    {
        var errors = new List<string>();
        var spec = Parse(File.ReadAllText(specPath), errors);
        if (spec != null) Prepare(spec, errors);
        if (errors.Count > 0) return "ERRORS (nothing written)\n" + string.Join("\n", errors);

        var eventNodes = new List<IEventNode>();
        var eventProps = new List<NodeProperties>();
        var logicNodes = new List<ITreeLogicNode>();
        var logicProps = new List<LogicNodeProperties>();
        var disabled = new List<ushort>();
        float offset = 0f;
        foreach (var root in spec.Roots)
        {
            var eventNode = (IEventNode)root.Instance;
            var branch = root.Children[0];
            Layout(branch, 0, ref offset);
            offset += 1f;
            SetMember(eventNode, "ConnectedIndex", branch.Index);
            eventNodes.Add(eventNode);
            eventProps.Add(new NodeProperties { DisplayName = root.DisplayName, Comment = root.Comment, Position = new Vector2(branch.X * ColumnWidth, 0f) });
            foreach (var node in Flatten(branch))
            {
                logicNodes.Add((ITreeLogicNode)node.Instance);
                logicProps.Add(new LogicNodeProperties
                {
                    DisplayName = node.DisplayName,
                    Comment = node.Comment,
                    Position = new Vector2(node.X * ColumnWidth, RowHeight * (node.Depth + 1)),
                });
                if (!node.Disabled) continue;
                disabled.Add(node.Index);
                SetMember(node.Instance, "Enabled", false);
            }
        }

        var subtree = AssetDatabase.LoadAssetAtPath<Subtree>(spec.AssetPath);
        bool created = subtree == null;
        if (created) subtree = ScriptableObject.CreateInstance<Subtree>();
        subtree.name = Path.GetFileNameWithoutExtension(spec.AssetPath);
        subtree.EventNodes = eventNodes.ToArray();
        subtree.EventNodeProperties = eventProps.ToArray();
        subtree.TreeLogicNodes = logicNodes.ToArray();
        subtree.LogicNodeProperties = logicProps.ToArray();
        subtree.DisabledLogicNodes = disabled.ToArray();
        subtree.DisabledEventNodes = Array.Empty<ushort>();
        subtree.SharedVariables = BuildVariables(spec, errors);
        if (errors.Count > 0) return "ERRORS (nothing written)\n" + string.Join("\n", errors);
        subtree.Serialize();
        if (created)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(spec.AssetPath));
            AssetDatabase.CreateAsset(subtree, spec.AssetPath);
        }
        else
            EditorUtility.SetDirty(subtree);
        AssetDatabase.SaveAssetIfDirty(subtree);
        return (created ? "created " : "rebuilt ") + spec.AssetPath + "\n" + Describe(spec.AssetPath);
    }

    static Spec Parse(string text, List<string> errors)
    {
        var spec = new Spec();
        var stack = new List<(int Indent, Node Node)>();
        var lines = text.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
            int lineNo = i + 1;
            if (trimmed.StartsWith("tree "))
            {
                spec.AssetPath = trimmed.Substring(5).Trim();
                continue;
            }
            if (trimmed.StartsWith("var "))
            {
                var tokens = Tokenize(trimmed.Substring(4));
                bool isInternal = tokens.Remove("internal");
                if (tokens.Count < 2) { errors.Add($"line {lineNo}: var needs '<Type> <Name> [= value]'"); continue; }
                string value = tokens.Count >= 4 && tokens[2] == "=" ? tokens[3] : null;
                spec.Variables.Add((tokens[0], Unquote(tokens[1]), value, isInternal));
                continue;
            }

            int indent = raw.Length - raw.TrimStart(' ', '\t').Length;
            var node = ParseNode(trimmed, lineNo, errors);
            while (stack.Count > 0 && stack[^1].Indent >= indent) stack.RemoveAt(stack.Count - 1);
            if (stack.Count == 0) spec.Roots.Add(node);
            else
            {
                node.Parent = stack[^1].Node;
                node.Parent.Children.Add(node);
            }
            stack.Add((indent, node));
        }
        if (string.IsNullOrEmpty(spec.AssetPath)) errors.Add("missing 'tree Assets/.../Name.asset' line");
        else if (!spec.AssetPath.StartsWith("Assets/") || !spec.AssetPath.EndsWith(".asset")) errors.Add("tree path must be Assets/.../<Name>.asset: " + spec.AssetPath);
        if (spec.Roots.Count == 0) errors.Add("no nodes");
        return spec;
    }

    static Node ParseNode(string line, int lineNo, List<string> errors)
    {
        var node = new Node { Line = lineNo };
        int comment = IndexOutsideQuotes(line, "//");
        if (comment >= 0)
        {
            node.Comment = line.Substring(comment + 2).Trim();
            line = line.Substring(0, comment).Trim();
        }
        var tokens = Tokenize(line);
        node.TypeToken = tokens[0];
        foreach (var token in tokens.Skip(1))
        {
            if (token == "!disabled") node.Disabled = true;
            else if (token.StartsWith("\"")) node.DisplayName = Unquote(token);
            else
            {
                int eq = token.IndexOf('=');
                if (eq <= 0) { errors.Add($"line {lineNo}: '{token}' is not Field=Value, \"Display name\" or !disabled"); continue; }
                node.Fields.Add((token.Substring(0, eq), token.Substring(eq + 1)));
            }
        }
        return node;
    }

    static Dictionary<string, Type> s_variableTypes = new();

    static void Prepare(Spec spec, List<string> errors)
    {
        s_variableTypes = new Dictionary<string, Type>();
        foreach (var (typeName, name, _, _) in spec.Variables)
            if (FindValueType(typeName) is { } valueType) s_variableTypes[name] = valueType;
        var types = NodeTypes();
        var roots = new List<Node>();
        bool hasStart = false;
        foreach (var root in spec.Roots)
        {
            Resolve(root, types, errors);
            if (root.Type == typeof(Start)) hasStart = true;
        }
        foreach (var root in spec.Roots)
        {
            if (root.Type != null && !IsEventType(root.Type))
            {
                // One top-level logic node is the Start branch; other branches sit under their own event node.
                if (hasStart)
                {
                    errors.Add($"line {root.Line}: top-level {root.TypeToken} next to a Start branch; put it under an event node (OnReceivedEvent…)");
                    continue;
                }
                hasStart = true;
                var start = new Node { TypeToken = "Start", Type = typeof(Start), Line = root.Line };
                start.Children.Add(root);
                root.Parent = start;
                roots.Add(start);
            }
            else roots.Add(root);
        }
        spec.Roots.Clear();
        spec.Roots.AddRange(roots);

        ushort next = 0;
        foreach (var root in spec.Roots)
        {
            if (root.Type == null) continue;
            root.Instance = Instantiate(root, errors);
            if (root.Children.Count != 1) { errors.Add($"line {root.Line}: event node {root.TypeToken} needs exactly one child branch"); continue; }
            foreach (var node in Flatten(root.Children[0]))
            {
                if (node.Type == null) Resolve(node, types, errors);
                if (node.Type == null) continue;
                if (IsEventType(node.Type)) { errors.Add($"line {node.Line}: event node {node.TypeToken} can only be top-level"); continue; }
                node.Index = next++;
                node.Instance = Instantiate(node, errors);
            }
            foreach (var node in Flatten(root.Children[0]))
            {
                if (node.Instance is not ITreeLogicNode logic) continue;
                logic.Index = node.Index;
                logic.ParentIndex = node == root.Children[0] ? None : node.Parent.Index;
                var siblings = node.Parent.Children;
                int at = siblings.IndexOf(node);
                logic.SiblingIndex = node == root.Children[0] || at == siblings.Count - 1 ? None : siblings[at + 1].Index;
                CheckChildren(node, errors);
            }
        }
        if (next >= None) errors.Add("too many nodes");
    }

    static void Resolve(Node node, Dictionary<string, List<Type>> types, List<string> errors)
    {
        var token = node.TypeToken;
        var matches = types.TryGetValue(token.Split('.').Last(), out var list)
            ? list.Where(t => t.FullName == token || t.FullName.EndsWith("." + token) || t.Name == token).ToList()
            : new List<Type>();
        if (matches.Count > 1) matches = matches.Where(t => t.FullName == token || t.FullName.EndsWith("." + token)).ToList();
        if (matches.Count == 1) node.Type = matches[0];
        else if (matches.Count == 0) errors.Add($"line {node.Line}: unknown task '{token}' (TreeKit.Tasks lists them)");
        else errors.Add($"line {node.Line}: '{token}' is ambiguous, write more of the namespace: " + string.Join(", ", list.Select(t => t.FullName)));
    }

    static object Instantiate(Node node, List<string> errors)
    {
        var instance = CreateDefault(node.Type);
        if (instance == null) { errors.Add($"line {node.Line}: can't create {node.Type.FullName}"); return null; }
        SetFields(instance, node.Fields, $"line {node.Line}: ", errors);
        if (!IsStackable(node.Type)) return instance;

        // A stackable Action/Conditional only lives in a graph inside a StackedAction/StackedConditional.
        var wrapperType = FindNodeType(typeof(IConditional).IsAssignableFrom(node.Type) ? "StackedConditional" : "StackedAction", typeof(ITreeLogicNode));
        var wrapper = CreateDefault(wrapperType);
        var tasksField = SerializedFields(wrapperType).First(f => FieldKey(f.Name) == "Tasks");
        var tasks = Array.CreateInstance(tasksField.FieldType.GetElementType(), 1);
        tasks.SetValue(instance, 0);
        tasksField.SetValue(wrapper, tasks);
        return wrapper;
    }

    static bool IsStackable(Type t) => typeof(Task).IsAssignableFrom(t) && !typeof(ITreeLogicNode).IsAssignableFrom(t);

    // Overrides of subtree variables go last: they need the referenced Subtree set first.
    static void SetFields(object instance, IEnumerable<(string Key, string Value)> values, string where, List<string> errors)
    {
        var type = instance.GetType();
        var fields = SerializedFields(type).ToList();
        foreach (var (key, value) in values.OrderBy(v => IsOverrideField(fields, v.Key) ? 1 : 0))
        {
            var field = fields.FirstOrDefault(f => FieldKey(f.Name).Equals(key, StringComparison.OrdinalIgnoreCase));
            if (field == null)
            {
                errors.Add($"{where}{type.Name} has no field '{key}' (fields: {string.Join(", ", fields.Select(f => FieldKey(f.Name)))})");
                continue;
            }
            try
            {
                field.SetValue(instance, field.FieldType == typeof(SharedVariableOverride[])
                    ? ParseOverrides(value, instance)
                    : ParseField(value, field.FieldType));
            }
            catch (Exception e) { errors.Add($"{where}{key}={value}: {e.Message}"); }
        }
    }

    static bool IsOverrideField(List<FieldInfo> fields, string key) =>
        fields.Any(f => f.FieldType == typeof(SharedVariableOverride[]) && FieldKey(f.Name).Equals(key, StringComparison.OrdinalIgnoreCase));

    // "[Name:value, Other:{ParentVariable}]" against the first referenced subtree's variables.
    static SharedVariableOverride[] ParseOverrides(string text, object reference)
    {
        var subtree = (GetMember(reference, "m_Subtrees") as Subtree[])?.FirstOrDefault(s => s != null)
            ?? throw new Exception("set Subtrees before Variables");
        subtree.Deserialize(true);
        var inner = text.Trim().TrimStart('[').TrimEnd(']');
        var result = new List<SharedVariableOverride>();
        foreach (var part in SplitTopLevel(inner, ',').Where(p => p.Length > 0))
        {
            int colon = part.IndexOf(':');
            if (colon <= 0) throw new Exception($"'{part}' is not Name:value");
            var name = Unquote(part.Substring(0, colon).Trim());
            var source = subtree.SharedVariables?.FirstOrDefault(v => v.Name == name)
                ?? throw new Exception($"{subtree.name} has no variable '{name}'");
            var overrideType = typeof(SharedVariable<>).MakeGenericType(source.ElementType);
            result.Add(new SharedVariableOverride { Source = source, SourceGraph = subtree, Override = (SharedVariable)ParseField(part.Substring(colon + 1).Trim(), overrideType) });
        }
        return result.ToArray();
    }

    static void CheckChildren(Node node, List<string> errors)
    {
        int maxChildren = MaxChildren(node.Type, node.Instance);
        if (node.Children.Count > maxChildren)
            errors.Add($"line {node.Line}: {node.Type.Name} takes {(maxChildren == 0 ? "no" : "at most " + maxChildren)} child(ren), has {node.Children.Count}");
        if (maxChildren > 0 && node.Children.Count == 0 && !typeof(Opsive.BehaviorDesigner.Runtime.Tasks.ISubtreeReferenceNode).IsAssignableFrom(node.Type))
            errors.Add($"line {node.Line}: {node.Type.Name} has no children");
    }

    static SharedVariable[] BuildVariables(Spec spec, List<string> errors)
    {
        var result = new List<SharedVariable>();
        foreach (var (typeName, name, value, isInternal) in spec.Variables)
        {
            var type = FindValueType(typeName);
            if (type == null) { errors.Add($"var {name}: unknown type {typeName}"); continue; }
            var variable = (SharedVariable)Activator.CreateInstance(typeof(SharedVariable<>).MakeGenericType(type));
            variable.Name = name;
            variable.Scope = SharedVariable.SharingScope.Graph;
            variable.IsInternal = isInternal;
            if (value != null)
            {
                try { variable.SetValue(ParseValue(value, type)); }
                catch (Exception e) { errors.Add($"var {name} = {value}: {e.Message}"); }
            }
            result.Add(variable);
        }
        return result.Count > 0 ? result.ToArray() : null;
    }

    static void Layout(Node node, int depth, ref float nextColumn)
    {
        node.Depth = depth;
        if (node.Children.Count == 0)
        {
            node.X = nextColumn;
            nextColumn += 1f;
            return;
        }
        foreach (var child in node.Children) Layout(child, depth + 1, ref nextColumn);
        node.X = (node.Children[0].X + node.Children[^1].X) * 0.5f;
    }

    static IEnumerable<Node> Flatten(Node node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var n in Flatten(child))
                yield return n;
    }

    // ---------------------------------------------------------------- Tasks

    public static string Tasks(string filter = "")
    {
        var sb = new StringBuilder();
        var all = NodeTypes().SelectMany(p => p.Value).Distinct()
            .Select(t => (Type: t, Category: Attr(t, "CategoryAttribute") ?? "", Description: Attr(t, "DescriptionAttribute") ?? ""))
            .Where(x => string.IsNullOrEmpty(filter) || x.Type.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        x.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(x => Kind(x.Type)).ThenBy(x => x.Category).ThenBy(x => x.Type.Name);
        string lastKind = null;
        foreach (var (type, category, description) in all)
        {
            var kind = Kind(type);
            if (kind != lastKind) sb.AppendLine("== " + kind);
            lastKind = kind;
            var fresh = CreateDefault(type);
            var fields = SerializedFields(type).Select(f => FieldKey(f.Name) + ":" + TypeName(f.FieldType) +
                (fresh != null && FormatField(f.GetValue(fresh), f.FieldType) is { } v ? "=" + v : ""));
            sb.Append(ShortTypeName(type)).Append("  [").Append(category).Append("] ").Append(type.Namespace);
            if (description.Length > 0) sb.Append(" — ").Append(description.Replace("\n", " "));
            sb.AppendLine();
            var list = string.Join(", ", fields);
            if (list.Length > 0) sb.Append("    ").AppendLine(list);
        }
        return sb.ToString();
    }

    static string Kind(Type t) =>
        IsEventType(t) ? "Event" :
        typeof(Opsive.BehaviorDesigner.Runtime.Tasks.ISubtreeReferenceNode).IsAssignableFrom(t) ? "Subtree" :
        MaxChildren(t, CreateDefault(t)) is var n && n == 1 ? "Decorator" : n > 1 ? "Composite" :
        IsA(t, "Conditional") ? "Conditional" : "Action";

    static int MaxChildren(Type t, object instance) =>
        instance != null && t.GetProperty("MaxChildCount")?.GetValue(instance) is IConvertible c ? Convert.ToInt32(c) : 0;

    static bool IsA(Type t, string baseName)
    {
        for (var c = t; c != null; c = c.BaseType)
            if (c.Name.StartsWith(baseName)) return true;
        return false;
    }

    // ---------------------------------------------------------------- reflection helpers

    static Dictionary<string, List<Type>> s_types;

    static Dictionary<string, List<Type>> NodeTypes()
    {
        if (s_types != null) return s_types;
        s_types = new Dictionary<string, List<Type>>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name.Contains("Samples") || name.Contains("Demo") || name.StartsWith("PipelineRunScript")) continue;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            foreach (var t in types)
            {
                if (t.IsAbstract || t.IsInterface || t.IsGenericTypeDefinition || !t.IsPublic && !t.IsNestedPublic) continue;
                if (!typeof(ITreeLogicNode).IsAssignableFrom(t) && !typeof(IEventNode).IsAssignableFrom(t) && !typeof(Task).IsAssignableFrom(t)) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                if (!s_types.TryGetValue(t.Name, out var list)) s_types[t.Name] = list = new List<Type>();
                list.Add(t);
            }
        }
        return s_types;
    }

    static IEnumerable<Type> AllTypes() =>
        AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); } });

    static bool IsEventType(Type t) => t != null && typeof(IEventNode).IsAssignableFrom(t);

    static string ShortTypeName(Type t)
    {
        if (t.IsGenericType) return "<unsupported:" + t.Name.Split('`')[0] + ">";
        var types = NodeTypes();
        return types.TryGetValue(t.Name, out var list) && list.Count == 1 && list[0] == t ? t.Name : t.FullName;
    }

    // Name or namespace suffix → type assignable to `baseType`; listed tasks first, then everything (sample tasks).
    static Type FindNodeType(string token, Type baseType)
    {
        bool Match(Type t) => (t.FullName == token || t.FullName.EndsWith("." + token) || t.Name == token) && baseType.IsAssignableFrom(t) && !t.IsAbstract;
        var exact = AllTypes().FirstOrDefault(t => t.FullName == token && baseType.IsAssignableFrom(t));
        if (exact != null) return exact;
        var listed = NodeTypes().TryGetValue(token.Split('.').Last(), out var list) ? list.Where(Match).ToList() : new List<Type>();
        return listed.Count == 1 ? listed[0] : listed.Count == 0 ? AllTypes().FirstOrDefault(Match) : null;
    }

    static object CreateDefault(Type t)
    {
        try
        {
            var instance = Activator.CreateInstance(t);
            t.GetMethod("Reset", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null)?.Invoke(instance, null);
            return instance;
        }
        catch { return null; }
    }

    static IEnumerable<FieldInfo> SerializedFields(Type type)
    {
        var chain = new List<Type>();
        for (var t = type; t != null && t != typeof(object); t = t.BaseType) chain.Add(t);
        chain.Reverse();
        foreach (var t in chain)
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (s_internalFields.Contains(f.Name) || f.IsNotSerialized || f.Name.Contains("k__BackingField")) continue;
                if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                yield return f;
            }
    }

    static string FieldKey(string fieldName) =>
        fieldName.StartsWith("m_") ? fieldName.Substring(2) : fieldName.StartsWith("_") ? fieldName.Substring(1) : fieldName;

    static object GetMember(object o, string name) =>
        o.GetType().GetProperty(name)?.GetValue(o) ?? o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(o);

    static void SetMember(object o, string name, object value)
    {
        var p = o.GetType().GetProperty(name);
        if (p != null && p.CanWrite) p.SetValue(o, value);
        else o.GetType().GetField("m_" + name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(o, value);
    }

    static string Attr(Type t, string attributeName)
    {
        var a = t.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().Name == attributeName || x.GetType().Name + "Attribute" == attributeName);
        return a?.GetType().GetProperties().Where(p => p.PropertyType == typeof(string)).Select(p => p.GetValue(a) as string).FirstOrDefault(s => !string.IsNullOrEmpty(s));
    }

    static object Default(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

    static string TypeName(Type t)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SharedVariable<>)) return "Shared<" + TypeName(t.GetGenericArguments()[0]) + ">";
        if (t.IsArray) return TypeName(t.GetElementType()) + "[]";
        return t == typeof(float) ? "float" : t == typeof(int) ? "int" : t == typeof(bool) ? "bool" : t == typeof(string) ? "string" : t.Name;
    }

    static Type FindValueType(string name)
    {
        bool array = name.EndsWith("[]");
        if (array) name = name.Substring(0, name.Length - 2);
        var t = name switch
        {
            "float" => typeof(float), "int" => typeof(int), "bool" => typeof(bool), "string" => typeof(string),
            _ => AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(x => x.FullName == name || x.Name == name && (x.Namespace == "UnityEngine" || x.IsPublic)),
        };
        return t == null ? null : array ? t.MakeArrayType() : t;
    }

    // ---------------------------------------------------------------- values

    static string FormatField(object value, Type fieldType)
    {
        if (value is SharedVariable shared)
        {
            if (shared.IsShared || !string.IsNullOrEmpty(shared.Name))
                return "{" + shared.Name + (shared.Scope is SharedVariable.SharingScope.Graph or SharedVariable.SharingScope.Empty ? "" : "@" + shared.Scope) + "}";
            return FormatValue(shared.GetValue(), shared.ElementType);
        }
        return FormatValue(value, fieldType);
    }

    static string FormatValue(object value, Type type)
    {
        if (value == null) return null;
        switch (value)
        {
            case bool b: return b ? "true" : "false";
            case float f: return f.ToString("R", CultureInfo.InvariantCulture);
            case double d: return d.ToString("R", CultureInfo.InvariantCulture);
            case IFormattable n when value.GetType().IsPrimitive: return n.ToString(null, CultureInfo.InvariantCulture);
            case string s: return "\"" + s.Replace("\"", "'") + "\"";
            case Enum e: return e.ToString().Replace(", ", "|");
            case Vector2 v: return $"({F(v.x)},{F(v.y)})";
            case Vector3 v: return $"({F(v.x)},{F(v.y)},{F(v.z)})";
            case Color c: return $"({F(c.r)},{F(c.g)},{F(c.b)},{F(c.a)})";
            case LayerMask m: return m.value.ToString(CultureInfo.InvariantCulture);
            case Object o:
                if (o == null) return null;
                var path = AssetDatabase.GetAssetPath(o);
                return string.IsNullOrEmpty(path) ? "<scene:" + o.name + ">" : path;
            case SharedVariableOverride o:
                var name = o.Source?.Name ?? "?";
                return (name.Contains(' ') ? "\"" + name + "\"" : name) + ":" + (FormatField(o.Override, o.Override?.GetType()) ?? "null");
            case IList list when type.IsArray || value.GetType().IsArray:
                if (list.Count == 0) return null;
                var element = value.GetType().GetElementType();
                return "[" + string.Join(",", list.Cast<object>().Select(x => FormatValue(x, element) ?? "null")) + "]";
        }
        if (IsA(value.GetType(), "Task") && value.GetType().Namespace?.StartsWith("Opsive") != false || value is ITreeLogicNode)
        {
            // A task inside a stacked task: Name(Field=value …).
            var fresh = CreateDefault(value.GetType());
            var parts = SerializedFields(value.GetType())
                .Select(f => (Key: FieldKey(f.Name), Value: FormatField(f.GetValue(value), f.FieldType), Default: fresh == null ? null : FormatField(f.GetValue(fresh), f.FieldType)))
                .Where(p => p.Value != null && p.Value != p.Default)
                .Select(p => p.Key + "=" + p.Value);
            return ShortTypeName(value.GetType()) + "(" + string.Join(" ", parts) + ")";
        }
        if (value.GetType().GetCustomAttribute<SerializableAttribute>() != null && !value.GetType().IsAbstract)
        {
            var json = JsonUtility.ToJson(value);
            return json == "{}" ? "new:" + value.GetType().Name : "json:" + value.GetType().Name + json;
        }
        return "<" + value.GetType().Name + ">";
    }

    static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);

    static object ParseField(string text, Type fieldType)
    {
        // A field typed as the untyped base binds to any variable: take the type from the spec's var line.
        if (fieldType == typeof(SharedVariable) && text.StartsWith("{") && text.EndsWith("}"))
        {
            var name = text.Substring(1, text.Length - 2).Split('@')[0];
            if (!s_variableTypes.TryGetValue(name, out var valueType)) throw new Exception($"bind {{{name}}} to a var declared in this spec");
            return ParseField(text, typeof(SharedVariable<>).MakeGenericType(valueType));
        }
        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(SharedVariable<>))
        {
            var shared = (SharedVariable)Activator.CreateInstance(fieldType);
            if (text.StartsWith("{") && text.EndsWith("}"))
            {
                var inner = text.Substring(1, text.Length - 2);
                var at = inner.IndexOf('@');
                shared.Name = at < 0 ? inner : inner.Substring(0, at);
                shared.Scope = at < 0 ? SharedVariable.SharingScope.Graph : (SharedVariable.SharingScope)Enum.Parse(typeof(SharedVariable.SharingScope), inner.Substring(at + 1), true);
            }
            else shared.SetValue(ParseValue(text, fieldType.GetGenericArguments()[0]));
            return shared;
        }
        return ParseValue(text, fieldType);
    }

    static object ParseValue(string text, Type type)
    {
        if (text == "null") return null;
        if (type == typeof(string)) return Unquote(text);
        if (type == typeof(bool)) return bool.Parse(text);
        if (type.IsEnum)
        {
            try { return Enum.Parse(type, text.Replace("|", ","), true); }
            catch { throw new Exception($"not a {type.Name}: use {string.Join(", ", Enum.GetNames(type))}"); }
        }
        if (type.IsPrimitive) return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
        if (type == typeof(Vector2)) { var v = Floats(text); return new Vector2(v[0], v[1]); }
        if (type == typeof(Vector3)) { var v = Floats(text); return new Vector3(v[0], v[1], v[2]); }
        if (type == typeof(Color)) { var v = Floats(text); return new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1f); }
        if (type == typeof(LayerMask))
            return int.TryParse(text, out var mask) ? (LayerMask)mask : (LayerMask)LayerMask.GetMask(text.Split('|'));
        if (typeof(Object).IsAssignableFrom(type))
        {
            var asset = AssetDatabase.LoadAssetAtPath(Unquote(text), type);
            if (asset == null) throw new Exception($"no {type.Name} at {text}");
            return asset;
        }
        if (type.IsArray)
        {
            var inner = text.Trim();
            if (inner.StartsWith("[")) inner = inner.Substring(1, inner.Length - 2);
            var parts = SplitTopLevel(inner, ',').Where(p => p.Length > 0).ToArray();
            var array = Array.CreateInstance(type.GetElementType(), parts.Length);
            for (int i = 0; i < parts.Length; i++) array.SetValue(ParseValue(parts[i], type.GetElementType()), i);
            return array;
        }
        int paren = text.IndexOf('(');
        if (paren > 0 && text.EndsWith(")") && !type.IsValueType)
        {
            var typeName = text.Substring(0, paren);
            if (typeName.StartsWith("<unsupported:")) throw new Exception(typeName.Substring(13).TrimEnd('>') + " can't be built from a spec (sample-style reflected call); write a task instead");
            var concrete = FindNodeType(typeName, type) ?? throw new Exception($"no single {type.Name} named {typeName}");
            var instance = CreateDefault(concrete) ?? throw new Exception("can't create " + concrete.FullName);
            var nested = new List<string>();
            var values = Tokenize(text.Substring(paren + 1, text.Length - paren - 2)).Select(t =>
            {
                int eq = t.IndexOf('=');
                return eq > 0 ? (t.Substring(0, eq), t.Substring(eq + 1)) : (t, "");
            });
            SetFields(instance, values, typeName + ": ", nested);
            if (nested.Count > 0) throw new Exception(string.Join("; ", nested));
            return instance;
        }
        if (text.StartsWith("new:") || text.StartsWith("json:"))
        {
            bool json = text.StartsWith("json:");
            var body = text.Substring(json ? 5 : 4);
            int brace = body.IndexOf('{');
            var typeName = brace < 0 ? body : body.Substring(0, brace);
            var concrete = AllTypes().FirstOrDefault(t => (t.Name == typeName || t.FullName == typeName) && type.IsAssignableFrom(t) && !t.IsAbstract);
            if (concrete == null) throw new Exception($"no concrete {type.Name} named {typeName}");
            return json ? JsonUtility.FromJson(body.Substring(brace), concrete) : Activator.CreateInstance(concrete);
        }
        throw new Exception($"can't parse {type.Name} from '{text}'");
    }

    static float[] Floats(string text) =>
        text.Trim('(', ')', ' ').Split(',').Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();

    // ---------------------------------------------------------------- text

    static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        int depth = 0;
        bool quoted = false;
        foreach (var ch in line)
        {
            if (ch == '"') quoted = !quoted;
            else if (!quoted && (ch == '(' || ch == '[' || ch == '{')) depth++;
            else if (!quoted && (ch == ')' || ch == ']' || ch == '}')) depth--;
            if (char.IsWhiteSpace(ch) && !quoted && depth == 0)
            {
                if (sb.Length > 0) tokens.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(ch);
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    static IEnumerable<string> SplitTopLevel(string text, char separator)
    {
        int depth = 0, start = 0;
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"') quoted = !quoted;
            else if (!quoted && (ch == '(' || ch == '[' || ch == '{')) depth++;
            else if (!quoted && (ch == ')' || ch == ']' || ch == '}')) depth--;
            else if (!quoted && depth == 0 && ch == separator)
            {
                yield return text.Substring(start, i - start).Trim();
                start = i + 1;
            }
        }
        yield return text.Substring(start).Trim();
    }

    static int IndexOutsideQuotes(string text, string pattern)
    {
        bool quoted = false;
        for (int i = 0; i < text.Length - pattern.Length + 1; i++)
        {
            if (text[i] == '"') quoted = !quoted;
            else if (!quoted && string.CompareOrdinal(text, i, pattern, 0, pattern.Length) == 0) return i;
        }
        return -1;
    }

    static string Unquote(string s) => s.Length >= 2 && s.StartsWith("\"") && s.EndsWith("\"") ? s.Substring(1, s.Length - 2) : s;
}
