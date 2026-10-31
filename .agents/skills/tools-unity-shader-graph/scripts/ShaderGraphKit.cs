// Shader Graph helpers for `unity command script run`. Shader Graph 17.6 has no public graph-editing API, so this
// drives its internal GraphData through reflection, with the same calls its importer and editor window make.
//
//   Describe(graphPath)          nodes (id, type, position, ports with defaults and links), properties, keywords,
//                                targets and their settings, master-stack blocks (JSON). Read it before editing.
//   Apply(graphPath, opsPath)    runs an ops file (format below) in order, validates, then saves once. Nothing is
//                                saved when an op fails. Works on .shadergraph and .shadersubgraph.
//   Create(graphPath, kind, menuPath)  new URP graph, kind Lit or Unlit; shader name = menuPath/<file name>.
//   ListNodes(filter)            built-in node classes and their Create Node menu paths ("fresnel", "Math/Vector").
//   Check(graphPath)             reimports and compiles the graph (one tiny synchronous render), returns compile
//                                messages. An import alone doesn't surface HLSL errors: shaders compile lazily.
//   Preview(path, pngPath, size) renders a sphere with the graph's shader (or a .mat) offscreen, compiling
//                                synchronously, and returns the PNG path plus compile messages. Open the PNG.
//
// Ops file: {"ops":[ {...}, ... ]}
//   {"op":"add", "id":"burn", "node":"subgraph:Assets/.../Fade - Burn.shadersubgraph", "x":-700, "y":0}
//   {"op":"add", "id":"mul", "node":"Multiply"}       built-in node: class (MultiplyNode, "Node" optional) or menu name
//   {"op":"property", "id":"amt", "kind":"Float", "name":"Dissolve", "reference":"_Dissolve", "value":"0"}
//        adds an exposed blackboard property and a node for it. Kinds: Float, Color, HDRColor, Vector4, Texture2D,
//        Boolean. Texture value = asset path. An existing property: {"op":"add", "id":"a", "node":"property:_Ref"}
//   {"op":"connect", "from":"amt.Out", "to":"burn.Fade Amount"}
//        node.port. Node = an id from this file, an objectId from Describe, or block:<Name> (block:BaseColor).
//        Port = display name, shader name or numeric slot id (spaces and case ignored). Replaces the input's link.
//   {"op":"custom", "id":"fx", "name":"HitFlash", "file":"Assets/_Game/.../HitFlash.hlsl",
//    "inputs":"Color:Vector4,Amount:Float", "outputs":"Out:Vector4"}
//        Custom Function node, File mode. The file declares void HitFlash_float(float4 Color, float Amount, out
//        float4 Out); ports in that order. Types: Float, Vector2, Vector3, Vector4, Boolean, Texture2D, SamplerState.
//   {"op":"disconnect", "to":"block:BaseColor"}       removes the links into an input port
//   {"op":"set", "port":"mul.B", "value":"1,0,0,1"}   default of an unlinked input port (numbers, true/false, asset path)
//   {"op":"set", "port":"pos.@space", "value":"Object"}   a node setting (dropdowns: enum names)
//   {"op":"remove", "node":"<id or objectId>"}
//   {"op":"graph", "name":"path", "value":"Aipocalypse/Characters"}   the shader's menu folder (name = path/<file>)
//   {"op":"target", "name":"alphaClip", "value":"true"}
//        a setting on every active target or sub-target that has it (Describe lists them), then adds or drops
//        master-stack blocks as the editor would (alphaClip adds Alpha + AlphaClipThreshold)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ShaderGraphKit
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    sealed class Op
    {
        public string op, id, node, from, to, port, value, kind, name, reference, file, inputs, outputs;
        public float x, y;
    }

    sealed class KitException : Exception
    {
        public KitException(string message) : base(message) { }
    }

    static Assembly _sg;
    static Assembly SG => _sg ??= Asm("Unity.ShaderGraph.Editor");
    static Assembly _urp;
    static Assembly URP => _urp ??= Asm("Unity.RenderPipelines.Universal.Editor");

    static Assembly Asm(string name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name);

    // Full name first, else the class name in any Shader Graph or URP editor namespace (property types moved to .Internal).
    static Type T(string fullName)
    {
        string shortName = fullName.Substring(fullName.LastIndexOf('.') + 1);
        return SG.GetType(fullName) ?? URP.GetType(fullName)
            ?? SG.GetTypes().Concat(URP.GetTypes()).FirstOrDefault(t => t.Name == shortName)
            ?? throw new KitException($"Shader Graph type {fullName} not found");
    }

    public static string Describe(string graphPath)
    {
        try
        {
            object graph = Load(graphPath);
            var sb = new StringBuilder("{");
            sb.Append($"\"graph\":{Json(graphPath)}");
            AppendTargets(sb, graph);
            AppendInputs(sb, graph);
            sb.Append(",\"nodes\":[");
            bool first = true;
            foreach (object node in Nodes(graph))
            {
                sb.Append(first ? "" : ",");
                first = false;
                AppendNode(sb, graph, node);
            }
            sb.Append("]}");
            return sb.ToString();
        }
        catch (Exception e)
        {
            return Fail(e);
        }
    }

    public static string Apply(string graphPath, string opsPath)
    {
        try
        {
            // An open window would raise a modal "Graph has changed on disk" (stalling the CLI) and could save over us.
            string guid = AssetDatabase.AssetPathToGUID(graphPath);
            foreach (Object window in Resources.FindObjectsOfTypeAll(T("UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow")))
                if ((string)Get(window, "selectedGuid") == guid)
                    throw new KitException($"{graphPath} is open in a Shader Graph window: ask the user to save or close it first");
            object graph = Load(graphPath);
            var ops = ParseOps(File.ReadAllText(opsPath));
            if (ops.Count == 0)
                throw new KitException($"no ops in {opsPath}");
            var ids = new Dictionary<string, object>();
            var log = new List<string>();
            for (int i = 0; i < ops.Count; i++)
            {
                try
                {
                    log.Add(Run(graph, ops[i], ids));
                }
                catch (Exception e)
                {
                    throw new KitException($"op {i} ({ops[i].op}) failed, nothing saved: {Unwrap(e).Message}");
                }
            }
            Call(graph, "ValidateGraph");
            CallStatic(T("UnityEditor.ShaderGraph.FileUtilities"), "WriteShaderGraphToDisk", graphPath, graph);
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
            var sb = new StringBuilder("{\"saved\":");
            sb.Append(Json(graphPath)).Append(",\"ops\":[").Append(string.Join(",", log.Select(Json))).Append("],\"ids\":{");
            sb.Append(string.Join(",", ids.Select(p => $"{Json(p.Key)}:{Json(Id(p.Value))}")));
            // A sub graph has no shader of its own: Check a graph that uses it.
            sb.Append("},\"check\":").Append(graphPath.EndsWith(".shadergraph") ? Preview(graphPath, null, 32) : "null").Append('}');
            return sb.ToString();
        }
        catch (Exception e)
        {
            return Fail(e);
        }
    }

    // A new URP graph, like Assets > Create > Shader Graph > URP > Lit/Unlit. Shader name = <menuPath>/<file name>.
    public static string Create(string graphPath, string kind, string menuPath)
    {
        try
        {
            if (File.Exists(graphPath))
                throw new KitException($"{graphPath} already exists");
            bool lit = kind == "Lit";
            if (!lit && kind != "Unlit")
                throw new KitException("kind must be Lit or Unlit");
            object target = Activator.CreateInstance(T("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget"), true);
            Call(target, "TrySetActiveSubTarget", T(lit ? "UniversalLitSubTarget" : "UniversalUnlitSubTarget"));
            Type fields = T("UnityEditor.ShaderGraph.BlockFields");
            object Block(string stage, string name) => fields.GetNestedType(stage, AllStatic | BindingFlags.Public | BindingFlags.NonPublic).GetField(name).GetValue(null);
            var names = new List<(string, string)> { ("VertexDescription", "Position"), ("VertexDescription", "Normal"), ("VertexDescription", "Tangent"), ("SurfaceDescription", "BaseColor") };
            if (lit)
                names.AddRange(new[] { ("SurfaceDescription", "NormalTS"), ("SurfaceDescription", "Metallic"), ("SurfaceDescription", "Smoothness"), ("SurfaceDescription", "Emission"), ("SurfaceDescription", "Occlusion") });
            var targets = Array.CreateInstance(T("UnityEditor.ShaderGraph.Target"), 1);
            targets.SetValue(target, 0);
            var blocks = Array.CreateInstance(T("UnityEditor.ShaderGraph.BlockFieldDescriptor"), names.Count);
            for (int i = 0; i < names.Count; i++)
                blocks.SetValue(Block(names[i].Item1, names[i].Item2), i);

            object graph = Activator.CreateInstance(T("UnityEditor.ShaderGraph.GraphData"), true);
            Call(graph, "AddContexts");
            Call(graph, "InitializeOutputs", targets, blocks);
            Call(graph, "AddCategory", CallStatic(T("UnityEditor.ShaderGraph.CategoryData"), "DefaultCategory", new object[] { null }));
            Set(graph, "path", string.IsNullOrEmpty(menuPath) ? "Shader Graphs" : menuPath);
            CallStatic(T("UnityEditor.ShaderGraph.FileUtilities"), "WriteShaderGraphToDisk", graphPath, graph);
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
            return $"{{\"created\":{Json(graphPath)},\"check\":{Preview(graphPath, null, 32)}}}";
        }
        catch (Exception e)
        {
            return Fail(e);
        }
    }

    // Node classes for "add", with their Create Node menu path. filter matches class or menu text ("" lists all).
    public static string ListNodes(string filter)
    {
        try
        {
            string key = Norm(filter);
            var rows = NodeTypes()
                .Where(t => t.title.Length > 0 && (key.Length == 0 || Norm(t.type.Name).Contains(key) || Norm(string.Join("/", t.title)).Contains(key)))
                .OrderBy(t => string.Join("/", t.title))
                .Select(t => Json($"{t.type.Name}: {string.Join("/", t.title)}"));
            return "[" + string.Join(",", rows) + "]";
        }
        catch (Exception e)
        {
            return Fail(e);
        }
    }

    // Shaders compile lazily: an import alone doesn't surface HLSL errors, so this also renders one small frame.
    public static string Check(string graphPath)
    {
        try
        {
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
            return Preview(graphPath, null, 32);
        }
        catch (Exception e)
        {
            return Fail(e);
        }
    }

    public static string Preview(string path, string pngPath, int size)
    {
        Material material = null;
        bool temporary = false;
        bool async = ShaderUtil.allowAsyncCompilation;
        var preview = new PreviewRenderUtility();
        try
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path) ?? throw new KitException($"no shader or material at {path}");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                temporary = true;
            }
            ShaderUtil.allowAsyncCompilation = false;
            var mesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            preview.camera.fieldOfView = 30f;
            preview.camera.nearClipPlane = 0.1f;
            preview.camera.farClipPlane = 20f;
            preview.camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -4.2f), Quaternion.identity);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.18f, 0.18f, 0.2f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, -40f, 0f);
            preview.ambientColor = new Color(0.25f, 0.25f, 0.28f);
            preview.BeginStaticPreview(new Rect(0, 0, size, size));
            preview.DrawMesh(mesh, Matrix4x4.identity, material, 0);
            preview.Render(true);
            var texture = preview.EndStaticPreview();
            if (!string.IsNullOrEmpty(pngPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pngPath)) ?? ".");
                File.WriteAllBytes(pngPath, texture.EncodeToPNG());
            }
            Object.DestroyImmediate(texture);
            return $"{{\"graph\":{Json(path)},\"png\":{Json(string.IsNullOrEmpty(pngPath) ? null : pngPath)},\"shader\":{Json(material.shader.name)},\"messages\":{ShaderMessages(material.shader)}}}";
        }
        catch (Exception e)
        {
            return Fail(e);
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = async;
            preview.Cleanup();
            if (temporary && material != null)
                Object.DestroyImmediate(material);
        }
    }

    static string Run(object graph, Op op, Dictionary<string, object> ids)
    {
        switch (op.op)
        {
            case "add":
            {
                object node = op.node.StartsWith("property:") ? NewPropertyNode(graph, FindProperty(graph, op.node.Substring(9))) : NewNode(op.node);
                Place(graph, node, op);
                Remember(ids, op.id, node);
                return $"added {Id(node)} {Get(node, "name")}";
            }
            case "custom":
            {
                object node = NewCustomFunction(op);
                Place(graph, node, op);
                Remember(ids, op.id, node);
                return $"custom {op.name} ({op.file}) {Id(node)}";
            }
            case "property":
            {
                object property = NewProperty(graph, op);
                object node = NewPropertyNode(graph, property);
                Place(graph, node, op);
                Remember(ids, op.id, node);
                return $"property {Get(property, "referenceName")} + node {Id(node)}";
            }
            case "connect":
            {
                object from = FindSlot(graph, ids, op.from, false);
                object to = FindSlot(graph, ids, op.to, true);
                foreach (object edge in Edges(graph, to).ToList())
                    Call(graph, "RemoveEdge", edge);
                object made = Call(graph, "Connect", Get(from, "slotReference"), Get(to, "slotReference"));
                if (made == null)
                    throw new KitException($"Shader Graph refused {op.from} -> {op.to} (type mismatch or a cycle)");
                return $"connected {op.from} -> {op.to}";
            }
            case "disconnect":
            {
                object to = FindSlot(graph, ids, op.to, true);
                var edges = Edges(graph, to).ToList();
                foreach (object edge in edges)
                    Call(graph, "RemoveEdge", edge);
                return $"disconnected {edges.Count} from {op.to}";
            }
            case "set":
            {
                int at = op.port?.IndexOf(".@", StringComparison.Ordinal) ?? -1;
                if (at >= 0)
                {
                    object node = FindNode(graph, ids, op.port.Substring(0, at));
                    SetNodeSetting(node, op.port.Substring(at + 2), op.value);
                    return $"set {op.port} = {op.value}";
                }
                object slot = FindSlot(graph, ids, op.port, true);
                SetSlotValue(slot, op.value);
                return $"set {op.port} = {op.value}";
            }
            case "remove":
            {
                object node = FindNode(graph, ids, op.node);
                Call(graph, "RemoveNode", node);
                return $"removed {op.node}";
            }
            case "graph":
            {
                PropertyInfo prop = Prop(graph.GetType(), op.name);
                if (prop == null || !prop.CanWrite)
                    throw new KitException($"GraphData has no writable '{op.name}' (e.g. path = the shader's menu folder)");
                prop.SetValue(graph, Convert(op.value, prop.PropertyType));
                return $"graph {op.name} = {op.value}";
            }
            case "target":
            {
                string changed = SetTargetSetting(graph, op.name, op.value);
                // What the editor does after a settings change: add newly needed blocks, drop unused default ones.
                var blocks = Call(graph, "GetActiveBlocksForAllActiveTargets");
                Call(graph, "AddRemoveBlocksFromActiveList", blocks);
                Call(graph, "UpdateActiveBlocks", blocks);
                return $"target {op.name} = {op.value} on {changed}";
            }
            default:
                throw new KitException($"unknown op '{op.op}'");
        }
    }

    static object NewNode(string spec)
    {
        if (spec.StartsWith("subgraph:"))
        {
            string path = spec.Substring(9);
            var asset = AssetDatabase.LoadAssetAtPath(path, T("UnityEditor.ShaderGraph.SubGraphAsset")) ?? throw new KitException($"no sub graph at {path}");
            object node = Activator.CreateInstance(T("UnityEditor.ShaderGraph.SubGraphNode"), true);
            Set(node, "asset", asset);
            return node;
        }
        string key = Norm(spec);
        var types = NodeTypes().ToList();
        Type type = types.FirstOrDefault(t => t.type.Name == spec || t.type.Name == spec + "Node").type
            ?? types.FirstOrDefault(t => t.title.Length > 0 && Norm(t.title[^1]) == key).type
            ?? throw new KitException($"no Shader Graph node '{spec}' (class or menu name; ListNodes(filter) searches them)");
        return Activator.CreateInstance(type, true);
    }

    // A Custom Function node in File mode: the .hlsl declares void <name>_float(<inputs>, out <outputs>).
    static object NewCustomFunction(Op op)
    {
        if (string.IsNullOrEmpty(op.name) || string.IsNullOrEmpty(op.file) || string.IsNullOrEmpty(op.outputs))
            throw new KitException("custom needs name, file and outputs");
        string guid = AssetDatabase.AssetPathToGUID(op.file);
        if (string.IsNullOrEmpty(guid))
            throw new KitException($"no imported HLSL file at {op.file}");
        object node = Activator.CreateInstance(T("UnityEditor.ShaderGraph.CustomFunctionNode"), true);
        Set(node, "sourceType", Enum.Parse(T("UnityEditor.ShaderGraph.HlslSourceType"), "File"));
        Set(node, "functionName", op.name);
        Set(node, "functionSource", guid);
        int id = 0;
        foreach ((string list, bool input) in new[] { (op.inputs, true), (op.outputs, false) })
        {
            foreach (string decl in (list ?? "").Split(',').Select(d => d.Trim()).Where(d => d.Length > 0))
            {
                string[] parts = decl.Split(':');
                if (parts.Length != 2)
                    throw new KitException($"port '{decl}' must be Name:Type");
                Call(node, "AddSlot", NewSlot(parts[1].Trim(), id++, parts[0].Trim(), input), true, true);
            }
        }
        return node;
    }

    static object NewSlot(string kind, int id, string name, bool input)
    {
        string typeName = kind switch
        {
            "Float" => "Vector1MaterialSlot",
            "Vector2" => "Vector2MaterialSlot",
            "Vector3" => "Vector3MaterialSlot",
            "Vector4" => "Vector4MaterialSlot",
            "Boolean" => "BooleanMaterialSlot",
            "Texture2D" => input ? "Texture2DInputMaterialSlot" : "Texture2DMaterialSlot",
            "SamplerState" => "SamplerStateMaterialSlot",
            _ => throw new KitException($"port type '{kind}' (Float, Vector2, Vector3, Vector4, Boolean, Texture2D, SamplerState)"),
        };
        Type type = T("UnityEditor.ShaderGraph." + typeName);
        object slotType = Enum.Parse(T("UnityEditor.Graphing.SlotType"), input ? "Input" : "Output");
        foreach (ConstructorInfo ctor in type.GetConstructors(All).OrderByDescending(c => c.GetParameters().Length))
        {
            ParameterInfo[] ps = ctor.GetParameters();
            if (ps.Length == 0)
                continue;
            var args = new object[ps.Length];
            bool ok = true;
            for (int i = 0; i < ps.Length && ok; i++)
            {
                switch (ps[i].Name)
                {
                    case "slotId": args[i] = id; break;
                    case "displayName":
                    case "shaderOutputName": args[i] = name; break;
                    case "slotType": args[i] = slotType; break;
                    case "value": args[i] = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null; break;
                    default:
                        if (ps[i].HasDefaultValue)
                            args[i] = ps[i].DefaultValue;
                        else
                            ok = false;
                        break;
                }
            }
            if (ok)
                return ctor.Invoke(args);
        }
        throw new KitException($"no usable constructor on {typeName}");
    }

    static IEnumerable<(Type type, string[] title)> NodeTypes()
    {
        Type nodeBase = T("UnityEditor.ShaderGraph.AbstractMaterialNode");
        Type titleType = T("UnityEditor.ShaderGraph.TitleAttribute");
        foreach (Type t in SG.GetTypes().Concat(URP.GetTypes()))
        {
            if (t.IsAbstract || !nodeBase.IsAssignableFrom(t) || t.GetConstructor(All, null, Type.EmptyTypes, null) == null)
                continue;
            object title = t.GetCustomAttributes(titleType, false).FirstOrDefault();
            yield return (t, title == null ? Array.Empty<string>() : (string[])Get(title, "title"));
        }
    }

    static object NewProperty(object graph, Op op)
    {
        string kind = op.kind ?? "Float";
        string typeName = kind switch
        {
            "Float" => "Vector1ShaderProperty",
            "Color" or "HDRColor" => "ColorShaderProperty",
            "Vector4" => "Vector4ShaderProperty",
            "Texture2D" => "Texture2DShaderProperty",
            "Boolean" => "BooleanShaderProperty",
            _ => throw new KitException($"unknown property kind '{kind}'"),
        };
        object property = Activator.CreateInstance(T("UnityEditor.ShaderGraph." + typeName), true);
        Set(property, "displayName", op.name ?? op.reference);
        if (kind == "HDRColor")
            Set(property, "colorMode", Enum.Parse(T("UnityEditor.ShaderGraph.ColorMode"), "HDR"));
        if (!string.IsNullOrEmpty(op.value))
        {
            if (kind == "Texture2D")
                Set(Get(property, "value"), "texture", LoadTexture(op.value));
            else
                Set(property, "value", Convert(op.value, Prop(property.GetType(), "value").PropertyType));
        }
        Call(graph, "AddGraphInput", property, -1);
        if (!string.IsNullOrEmpty(op.reference))
            Call(property, "SetReferenceNameAndSanitizeForGraph", graph, op.reference);
        return property;
    }

    static object NewPropertyNode(object graph, object property)
    {
        object node = Activator.CreateInstance(T("UnityEditor.ShaderGraph.PropertyNode"), true);
        Set(node, "property", property);
        return node;
    }

    static void Place(object graph, object node, Op op)
    {
        object drawState = Get(node, "drawState");
        var rect = (Rect)Get(drawState, "position");
        rect.position = new Vector2(op.x, op.y);
        Set(drawState, "position", rect);
        Set(node, "drawState", drawState);
        Call(graph, "AddNode", node, true);
    }

    static void Remember(Dictionary<string, object> ids, string id, object node)
    {
        if (!string.IsNullOrEmpty(id))
            ids[id] = node;
    }

    // Serialized enum / bool / number fields a node class adds (Position: space), shown without the m_ prefix.
    static IEnumerable<(string name, string value)> NodeSettings(object node)
    {
        Type nodeBase = T("UnityEditor.ShaderGraph.AbstractMaterialNode");
        for (Type t = node.GetType(); t != null && t != nodeBase; t = t.BaseType)
        {
            foreach (FieldInfo f in t.GetFields(All | BindingFlags.DeclaredOnly))
            {
                if (!f.Name.StartsWith("m_") || !(f.FieldType.IsEnum || f.FieldType == typeof(bool) || f.FieldType == typeof(int) || f.FieldType == typeof(float)))
                    continue;
                if (f.GetCustomAttribute<SerializeField>() == null)
                    continue;
                yield return (char.ToLowerInvariant(f.Name[2]) + f.Name.Substring(3), Str(f.GetValue(node)));
            }
        }
    }

    // A node's own setting (Position's space, a sampler's type): a writable property, else its serialized m_<Name> field.
    static void SetNodeSetting(object node, string setting, string value)
    {
        PropertyInfo prop = Prop(node.GetType(), setting);
        if (prop != null && prop.CanWrite && (prop.PropertyType.IsPrimitive || prop.PropertyType.IsEnum || prop.PropertyType == typeof(string)))
        {
            prop.SetValue(node, Convert(value, prop.PropertyType));
            return;
        }
        string fieldName = setting.StartsWith("m_") ? setting : "m_" + char.ToUpperInvariant(setting[0]) + setting.Substring(1);
        for (Type t = node.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(fieldName, All | BindingFlags.DeclaredOnly);
            if (f == null)
                continue;
            f.SetValue(node, Convert(value, f.FieldType));
            return;
        }
        throw new KitException($"{node.GetType().Name} has no setting '{setting}' (nor field {fieldName})");
    }

    static void SetSlotValue(object slot, string value)
    {
        PropertyInfo texture = Prop(slot.GetType(), "texture");
        if (texture != null && texture.CanWrite)
        {
            texture.SetValue(slot, LoadTexture(value));
            return;
        }
        PropertyInfo prop = Prop(slot.GetType(), "value");
        if (prop == null || !prop.CanWrite)
            throw new KitException($"port {SlotName(slot)} ({slot.GetType().Name}) has no editable default");
        prop.SetValue(slot, Convert(value, prop.PropertyType));
    }

    // Sets it on every active target (and sub-target) that has it: vendor graphs often carry a Built-In target too.
    static string SetTargetSetting(object graph, string name, string value)
    {
        var changed = new List<string>();
        foreach (object target in (IEnumerable)Get(graph, "activeTargets"))
        {
            foreach (object owner in new[] { target, Get(target, "activeSubTarget") })
            {
                PropertyInfo prop = owner == null ? null : Prop(owner.GetType(), name);
                if (prop == null || !prop.CanWrite)
                    continue;
                prop.SetValue(owner, Convert(value, prop.PropertyType));
                changed.Add(owner.GetType().Name);
                break;
            }
        }
        if (changed.Count == 0)
            throw new KitException($"no writable target setting '{name}' (Describe lists them)");
        return string.Join(", ", changed);
    }

    static object FindNode(object graph, Dictionary<string, object> ids, string key)
    {
        if (ids.TryGetValue(key, out object node))
            return node;
        if (key.StartsWith("block:"))
        {
            string block = Norm(key.Substring(6));
            foreach (object n in Nodes(graph))
            {
                if (n.GetType().Name != "BlockNode")
                    continue;
                object descriptor = Get(n, "descriptor");
                if (Norm((string)Get(descriptor, "name")) == block || Norm($"{Get(descriptor, "tag")}.{Get(descriptor, "name")}") == block)
                    return n;
            }
            throw new KitException($"block {key} is not in the master stack (a target setting may enable it, e.g. alphaClip for AlphaClipThreshold)");
        }
        return Call(graph, "GetNodeFromId", key) ?? throw new KitException($"no node '{key}' (an id from this ops file, an objectId from Describe, or block:<Name>)");
    }

    static object FindSlot(object graph, Dictionary<string, object> ids, string spec, bool input)
    {
        int dot = spec?.LastIndexOf('.') ?? -1;
        if (spec != null && spec.StartsWith("block:") && dot < 6)
            dot = -1;
        object node = FindNode(graph, ids, dot < 0 ? spec : spec.Substring(0, dot));
        var slots = Slots(node).Where(s => (bool)Get(s, "isInputSlot") == input).ToList();
        if (dot < 0)
        {
            if (slots.Count == 1)
                return slots[0];
            throw new KitException($"{spec}: name a port ({string.Join(", ", slots.Select(s => SlotName(s)))})");
        }
        string port = spec.Substring(dot + 1);
        object match = int.TryParse(port, out int slotId)
            ? slots.FirstOrDefault(s => (int)Get(s, "id") == slotId)
            : slots.FirstOrDefault(s => Norm(SlotName(s)) == Norm(port) || Norm((string)Get(s, "shaderOutputName")) == Norm(port));
        return match ?? throw new KitException($"{spec}: no {(input ? "input" : "output")} port '{port}' ({string.Join(", ", slots.Select(s => SlotName(s)))})");
    }

    static object FindProperty(object graph, string reference)
    {
        foreach (object p in (IEnumerable)Get(graph, "properties"))
            if ((string)Get(p, "referenceName") == reference || (string)Get(p, "displayName") == reference)
                return p;
        throw new KitException($"no property '{reference}'");
    }

    static IEnumerable<object> Nodes(object graph)
    {
        MethodInfo m = graph.GetType().GetMethod("GetNodes", All).MakeGenericMethod(T("UnityEditor.ShaderGraph.AbstractMaterialNode"));
        return ((IEnumerable)m.Invoke(graph, null)).Cast<object>().ToList();
    }

    static IEnumerable<object> Slots(object node)
    {
        Type slotType = T("UnityEditor.ShaderGraph.MaterialSlot");
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(slotType));
        node.GetType().GetMethod("GetSlots", All).MakeGenericMethod(slotType).Invoke(node, new object[] { list });
        return list.Cast<object>().ToList();
    }

    static IEnumerable<object> Edges(object graph, object slot)
    {
        MethodInfo m = graph.GetType().GetMethods(All).First(x => x.Name == "GetEdges" && x.ReturnType != typeof(void) && x.GetParameters()[0].ParameterType.Name == "SlotReference");
        return ((IEnumerable)m.Invoke(graph, new[] { Get(slot, "slotReference") })).Cast<object>();
    }

    static object Load(string graphPath)
    {
        if (!File.Exists(graphPath))
            throw new KitException($"no graph at {graphPath}");
        object graph = Activator.CreateInstance(T("UnityEditor.ShaderGraph.GraphData"), true);
        Set(graph, "messageManager", Activator.CreateInstance(T("UnityEditor.Graphing.Util.MessageManager"), true));
        Set(graph, "assetGuid", AssetDatabase.AssetPathToGUID(graphPath));
        if (graphPath.EndsWith(".shadersubgraph"))
            Set(graph, "isSubGraph", true);
        MethodInfo deserialize = T("UnityEditor.ShaderGraph.Serialization.MultiJson").GetMethod("Deserialize", AllStatic).MakeGenericMethod(graph.GetType());
        deserialize.Invoke(null, new object[] { graph, File.ReadAllText(graphPath), null, false });
        Call(graph, "OnEnable");
        Call(graph, "ValidateGraph");
        return graph;
    }

    static void AppendTargets(StringBuilder sb, object graph)
    {
        sb.Append(",\"targets\":[");
        bool first = true;
        foreach (object target in (IEnumerable)Get(graph, "activeTargets"))
        {
            sb.Append(first ? "" : ",");
            first = false;
            object sub = Get(target, "activeSubTarget");
            sb.Append($"{{\"target\":{Json(target.GetType().Name)},\"settings\":{Settings(target)}");
            if (sub != null)
                sb.Append($",\"subTarget\":{Json(sub.GetType().Name)},\"subSettings\":{Settings(sub)}");
            sb.Append('}');
        }
        sb.Append(']');
    }

    static string Settings(object owner)
    {
        var parts = new List<string>();
        foreach (PropertyInfo p in owner.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanWrite || p.GetIndexParameters().Length > 0 || !(p.PropertyType.IsPrimitive || p.PropertyType.IsEnum))
                continue;
            parts.Add($"{Json(p.Name)}:{Json(Str(p.GetValue(owner)))}");
        }
        return "{" + string.Join(",", parts) + "}";
    }

    static void AppendInputs(StringBuilder sb, object graph)
    {
        sb.Append(",\"properties\":[");
        sb.Append(string.Join(",", ((IEnumerable)Get(graph, "properties")).Cast<object>().Select(p =>
            $"{{\"name\":{Json((string)Get(p, "displayName"))},\"reference\":{Json((string)Get(p, "referenceName"))},\"type\":{Json(p.GetType().Name.Replace("ShaderProperty", ""))},\"exposed\":{Json(Str(Get(p, "generatePropertyBlock")))},\"value\":{Json(PropertyValue(p))}}}")));
        sb.Append("],\"keywords\":[");
        sb.Append(string.Join(",", ((IEnumerable)Get(graph, "keywords")).Cast<object>().Select(k =>
            $"{{\"name\":{Json((string)Get(k, "displayName"))},\"reference\":{Json((string)Get(k, "referenceName"))},\"type\":{Json(Str(Get(k, "keywordType")))},\"definition\":{Json(Str(Get(k, "keywordDefinition")))},\"scope\":{Json(Str(Get(k, "keywordScope")))}}}")));
        sb.Append(']');
    }

    static void AppendNode(StringBuilder sb, object graph, object node)
    {
        var rect = (Rect)Get(Get(node, "drawState"), "position");
        string type = node.GetType().Name;
        sb.Append($"{{\"id\":{Json(Id(node))},\"type\":{Json(type)},\"name\":{Json((string)Get(node, "name"))},\"x\":{rect.x:0},\"y\":{rect.y:0}");
        if (type == "BlockNode")
            sb.Append($",\"block\":{Json("block:" + Get(Get(node, "descriptor"), "name"))}");
        if (type == "SubGraphNode")
            sb.Append($",\"subgraph\":{Json(AssetDatabase.GetAssetPath(Get(node, "asset") as Object))}");
        if (type == "PropertyNode")
            sb.Append($",\"property\":{Json((string)Get(Get(node, "property"), "referenceName"))}");
        var settings = NodeSettings(node).ToList();
        if (settings.Count > 0)
            sb.Append(",\"settings\":{").Append(string.Join(",", settings.Select(s => $"{Json(s.name)}:{Json(s.value)}"))).Append('}');
        sb.Append(",\"ports\":[");
        bool first = true;
        foreach (object slot in Slots(node))
        {
            sb.Append(first ? "" : ",");
            first = false;
            bool input = (bool)Get(slot, "isInputSlot");
            var links = Edges(graph, slot).Select(e =>
            {
                object other = Get(e, input ? "outputSlot" : "inputSlot");
                object otherNode = Get(other, "node");
                object otherSlot = Get(other, "slot");
                return $"{Id(otherNode)}.{SlotName(otherSlot)}";
            }).ToList();
            sb.Append($"{{\"id\":{Get(slot, "id")},\"name\":{Json(SlotName(slot))},\"dir\":{Json(input ? "in" : "out")},\"type\":{Json(Str(Get(slot, "concreteValueType")))}");
            if (input && links.Count == 0)
            {
                string value = Prop(slot.GetType(), "texture") != null ? AssetDatabase.GetAssetPath(Get(slot, "texture") as Object)
                    : Prop(slot.GetType(), "value") != null ? Str(Get(slot, "value")) : null;
                if (!string.IsNullOrEmpty(value))
                    sb.Append($",\"value\":{Json(value)}");
            }
            if (links.Count > 0)
                sb.Append($",\"links\":[{string.Join(",", links.Select(Json))}]");
            sb.Append('}');
        }
        sb.Append("]}");
    }

    static string ShaderMessages(Shader shader)
    {
        var msgs = ShaderUtil.GetShaderMessages(shader);
        return "[" + string.Join(",", msgs.Select(m => Json($"{m.severity}: {m.message} ({m.file}:{m.line})"))) + "]";
    }

    // JsonUtility can't see types compiled by `script run`, so the ops file is read by this minimal JSON parser.
    static List<Op> ParseOps(string text)
    {
        int i = 0;
        var root = ParseValue(text, ref i) as Dictionary<string, object> ?? throw new KitException("ops file must be a JSON object");
        if (!root.TryGetValue("ops", out object list) || list is not List<object> items)
            throw new KitException("ops file needs an \"ops\" array");
        var ops = new List<Op>();
        foreach (object item in items)
        {
            if (item is not Dictionary<string, object> d)
                throw new KitException("each op must be a JSON object");
            string S(string key) => d.TryGetValue(key, out object v) && v != null ? System.Convert.ToString(v, CultureInfo.InvariantCulture) : null;
            float F(string key) => d.TryGetValue(key, out object v) && v is double n ? (float)n : 0f;
            ops.Add(new Op
            {
                op = S("op"), id = S("id"), node = S("node"), from = S("from"), to = S("to"), port = S("port"),
                value = S("value"), kind = S("kind"), name = S("name"), reference = S("reference"), x = F("x"), y = F("y"),
                file = S("file"), inputs = S("inputs"), outputs = S("outputs"),
            });
        }
        return ops;
    }

    static object ParseValue(string s, ref int i)
    {
        SkipSpace(s, ref i);
        if (i >= s.Length)
            throw new KitException("unexpected end of ops file");
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>();
            i++;
            SkipSpace(s, ref i);
            if (s[i] == '}')
            {
                i++;
                return d;
            }
            while (true)
            {
                SkipSpace(s, ref i);
                string key = ParseString(s, ref i);
                SkipSpace(s, ref i);
                Expect(s, ref i, ':');
                d[key] = ParseValue(s, ref i);
                SkipSpace(s, ref i);
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                Expect(s, ref i, '}');
                return d;
            }
        }
        if (c == '[')
        {
            var list = new List<object>();
            i++;
            SkipSpace(s, ref i);
            if (s[i] == ']')
            {
                i++;
                return list;
            }
            while (true)
            {
                list.Add(ParseValue(s, ref i));
                SkipSpace(s, ref i);
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                Expect(s, ref i, ']');
                return list;
            }
        }
        if (c == '"')
            return ParseString(s, ref i);
        int start = i;
        while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0)
            i++;
        string word = s.Substring(start, i - start);
        return word switch
        {
            "true" => true,
            "false" => false,
            "null" => null,
            _ => double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) ? n : throw new KitException($"bad JSON value '{word}' at {start}"),
        };
    }

    static string ParseString(string s, ref int i)
    {
        Expect(s, ref i, '"');
        var sb = new StringBuilder();
        while (s[i] != '"')
        {
            if (s[i] == '\\')
            {
                i++;
                char e = s[i];
                sb.Append(e switch { 'n' => '\n', 't' => '\t', 'r' => '\r', 'u' => (char)System.Convert.ToInt32(s.Substring(i + 1, 4), 16), _ => e });
                if (e == 'u')
                    i += 4;
            }
            else
            {
                sb.Append(s[i]);
            }
            i++;
        }
        i++;
        return sb.ToString();
    }

    static void SkipSpace(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i]))
            i++;
    }

    static void Expect(string s, ref int i, char c)
    {
        if (i >= s.Length || s[i] != c)
            throw new KitException($"ops file: expected '{c}' at {i}");
        i++;
    }

    static PropertyInfo Prop(Type type, string name)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            PropertyInfo p = t.GetProperty(name, All | BindingFlags.DeclaredOnly);
            if (p != null)
                return p;
        }
        return null;
    }

    static object Get(object obj, string name)
    {
        if (obj == null)
            return null;
        PropertyInfo p = Prop(obj.GetType(), name);
        if (p != null)
            return p.GetValue(obj);
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, All | BindingFlags.DeclaredOnly);
            if (f != null)
                return f.GetValue(obj);
        }
        throw new KitException($"{obj.GetType().Name} has no member {name}");
    }

    static void Set(object obj, string name, object value)
    {
        PropertyInfo p = Prop(obj.GetType(), name);
        if (p != null && p.CanWrite)
        {
            p.SetValue(obj, value);
            return;
        }
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, All | BindingFlags.DeclaredOnly);
            if (f != null)
            {
                f.SetValue(obj, value);
                return;
            }
        }
        throw new KitException($"{obj.GetType().Name} has no writable member {name}");
    }

    static object Call(object obj, string name, params object[] args)
    {
        MethodInfo m = obj.GetType().GetMethods(All).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args.Length && !x.IsGenericMethodDefinition)
            ?? throw new KitException($"{obj.GetType().Name} has no method {name}({args.Length})");
        return Invoke(m, obj, args);
    }

    static object CallStatic(Type type, string name, params object[] args)
    {
        MethodInfo m = type.GetMethods(AllStatic).First(x => x.Name == name && x.GetParameters().Length == args.Length);
        return Invoke(m, null, args);
    }

    static object Invoke(MethodInfo m, object obj, object[] args)
    {
        try
        {
            return m.Invoke(obj, args);
        }
        catch (TargetInvocationException e)
        {
            throw Unwrap(e);
        }
    }

    static Exception Unwrap(Exception e) => e is TargetInvocationException t && t.InnerException != null ? Unwrap(t.InnerException) : e;

    static object Convert(string value, Type type)
    {
        if (type == typeof(string))
            return value;
        if (type == typeof(bool))
            return value.Trim().ToLowerInvariant() is "true" or "1";
        if (type.IsEnum)
            return Enum.Parse(type, value.Trim(), true);
        float[] f = (value ?? "").Split(',').Where(s => s.Trim().Length > 0).Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
        float At(int i) => i < f.Length ? f[i] : (i == 3 ? 1f : 0f);
        if (type == typeof(float))
            return At(0);
        if (type == typeof(int))
            return (int)At(0);
        if (type == typeof(Vector2))
            return new Vector2(At(0), At(1));
        if (type == typeof(Vector3))
            return new Vector3(At(0), At(1), At(2));
        if (type == typeof(Vector4))
            return new Vector4(At(0), At(1), At(2), At(3));
        if (type == typeof(Color))
            return new Color(At(0), At(1), At(2), At(3));
        throw new KitException($"can't convert '{value}' to {type.Name}");
    }

    static Texture LoadTexture(string path) => AssetDatabase.LoadAssetAtPath<Texture>(path) ?? throw new KitException($"no texture at {path}");

    static string Id(object node) => (string)Get(node, "objectId");

    static string PropertyValue(object property)
    {
        object value = Get(property, "value");
        return value != null && value.GetType().Name == "SerializableTexture" ? AssetDatabase.GetAssetPath(Get(value, "texture") as Object) : Str(value);
    }

    static string SlotName(object slot) => (string)Call(slot, "RawDisplayName");

    static string Norm(string s) => (s ?? "").Replace(" ", "").Replace("_", "").ToLowerInvariant();

    static string Str(object o) => o switch
    {
        null => null,
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        Vector4 v => $"{v.x:0.###},{v.y:0.###},{v.z:0.###},{v.w:0.###}",
        Vector3 v => $"{v.x:0.###},{v.y:0.###},{v.z:0.###}",
        Vector2 v => $"{v.x:0.###},{v.y:0.###}",
        Color c => $"{c.r:0.###},{c.g:0.###},{c.b:0.###},{c.a:0.###}",
        bool b => b ? "true" : "false",
        _ => o.ToString(),
    };

    static string Json(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";

    static string Fail(Exception e) => $"{{\"error\":{Json(Unwrap(e) is KitException ? Unwrap(e).Message : Unwrap(e).ToString())}}}";
}
