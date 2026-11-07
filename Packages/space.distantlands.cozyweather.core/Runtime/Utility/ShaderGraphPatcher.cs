//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    public static class ShaderGraphPatcher
    {
        // Subgraph GUID for the COZY fog integration sub graph. TODO: should be gotten rather than using a magic number in the future
        const string SubGraphGuid = "ae089e549d7a49547b05dce05a4fe98c";
        const long SubGraphFileId = -5475051401550479605;
        const int InputSlotId = -18521301;
        const int OutputSlotId = 1;

        public static void PatchShader(string assetPath, string connectedBlockNodeTitle)
        {
            string fullPath = Path.GetFullPath(assetPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"No file at path: {fullPath}");
                return;
            }

            string file = File.ReadAllText(fullPath);

            if (file.Contains(SubGraphGuid))
            {
                Debug.LogWarning("Shader Graph already appears to contain the COZY fog integration.");
                return;
            }

            string baseColorNodeId = GetBlockNodeNodeId(file, connectedBlockNodeTitle);
            string colorNodeId = GetMatchingId(file, baseColorNodeId);

            if (string.IsNullOrEmpty(colorNodeId) || string.IsNullOrEmpty(baseColorNodeId))
            {
                Debug.LogError("Could not find Block node and connected node.");
                return;
            }

            string nodeId = NewId();
            string inputSlotObjectId = NewId();
            string outputSlotObjectId = NewId();

            File.WriteAllText(fullPath, file);

            file = AddGraphNodeReference(file, nodeId);
            file = ReplaceInputToOutputEdge(file, colorNodeId, baseColorNodeId, nodeId);
            file += "\n\n" + BuildInputSlot(inputSlotObjectId);
            file += "\n\n" + BuildOutputSlot(outputSlotObjectId);
            file += "\n\n" + BuildSubGraphNode(nodeId, inputSlotObjectId, outputSlotObjectId);

            File.WriteAllText(fullPath, file, Encoding.UTF8);

#if UNITY_EDITOR
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
#endif

            Debug.Log($"Patched Shader Graph: {assetPath}");
        }

        static string AddGraphNodeReference(string file, string nodeId)
        {
            int nodesIndex = file.IndexOf("\"m_Nodes\": [", StringComparison.Ordinal);
            if (nodesIndex < 0)
                throw new Exception("Missing m_Nodes block.");

            int arrayStart = file.IndexOf('[', nodesIndex);
            int insertIndex = arrayStart + 1;

            string insert =
                "\n        {\n" +
                $"            \"m_Id\": \"{nodeId}\"\n" +
                "        },";

            return file.Insert(insertIndex, insert);
        }

        static string ReplaceInputToOutputEdge(string file, string upstreamNodeId, string downstreamNodeId, string subGraphNodeId)
        {
            string escapedUpstream = Regex.Escape(upstreamNodeId);
            string escapedDownstream = Regex.Escape(downstreamNodeId);

            string oldEdgePattern =
                @"\{\s*""m_OutputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id""\s*:\s*""" + escapedUpstream +
                @"""\s*\}\s*,\s*""m_SlotId""\s*:\s*(\d+)\s*\}\s*,\s*""m_InputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id""\s*:\s*""" + escapedDownstream +
                @"""\s*\}\s*,\s*""m_SlotId""\s*:\s*(\d+)\s*\}\s*\}";

            Match match = Regex.Match(file, oldEdgePattern);

            if (!match.Success)
                throw new Exception("Could not find existing edge between the specified nodes. The connection might not exist or the JSON format changed.");

            int originalOutputSlotId = int.Parse(match.Groups[1].Value);
            int originalInputSlotId = int.Parse(match.Groups[2].Value);

            string newEdges =
                BuildEdge(subGraphNodeId, OutputSlotId, downstreamNodeId, originalInputSlotId) + ",\n" +
                BuildEdge(upstreamNodeId, originalOutputSlotId, subGraphNodeId, InputSlotId);

            return Regex.Replace(file, oldEdgePattern, newEdges, RegexOptions.Singleline);
        }

        static string BuildEdge(string outputNode, int outputSlot, string inputNode, int inputSlot)
        {
            return
                "        {\n" +
                "            \"m_OutputSlot\": {\n" +
                "                \"m_Node\": {\n" +
                $"                    \"m_Id\": \"{outputNode}\"\n" +
                "                },\n" +
                $"                \"m_SlotId\": {outputSlot}\n" +
                "            },\n" +
                "            \"m_InputSlot\": {\n" +
                "                \"m_Node\": {\n" +
                $"                    \"m_Id\": \"{inputNode}\"\n" +
                "                },\n" +
                $"                \"m_SlotId\": {inputSlot}\n" +
                "            }\n" +
                "        }";
        }

        static string BuildInputSlot(string id) =>
$@"{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.Vector4MaterialSlot"",
    ""m_ObjectId"": ""{id}"",
    ""m_Id"": {InputSlotId},
    ""m_DisplayName"": ""InputColor"",
    ""m_SlotType"": 0,
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""_InputColor"",
    ""m_StageCapability"": 3,
    ""m_Value"": {{ ""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""w"": 1.0 }},
    ""m_DefaultValue"": {{ ""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""w"": 0.0 }},
    ""m_Labels"": []
}}";

        static string BuildOutputSlot(string id) =>
$@"{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.Vector4MaterialSlot"",
    ""m_ObjectId"": ""{id}"",
    ""m_Id"": {OutputSlotId},
    ""m_DisplayName"": ""BlendedColor"",
    ""m_SlotType"": 1,
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""BlendedColor"",
    ""m_StageCapability"": 3,
    ""m_Value"": {{ ""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""w"": 0.0 }},
    ""m_DefaultValue"": {{ ""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""w"": 0.0 }},
    ""m_Labels"": []
}}";

        static string BuildSubGraphNode(string nodeId, string inputSlotId, string outputSlotId) =>
$@"{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.SubGraphNode"",
    ""m_ObjectId"": ""{nodeId}"",
    ""m_Group"": {{ ""m_Id"": """" }},
    ""m_Name"": ""Blend Stylized Fog"",
    ""m_DrawState"": {{
        ""m_Expanded"": true,
        ""m_Position"": {{
            ""serializedVersion"": ""2"",
            ""x"": -323.3333435058594,
            ""y"": 200.0,
            ""width"": 230.00003051757813,
            ""height"": 97.33331298828125
        }}
    }},
    ""m_Slots"": [
        {{ ""m_Id"": ""{inputSlotId}"" }},
        {{ ""m_Id"": ""{outputSlotId}"" }}
    ],
    ""synonyms"": [],
    ""m_Precision"": 0,
    ""m_PreviewExpanded"": false,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 0,
    ""m_CustomColors"": {{ ""m_SerializableColors"": [] }},
    ""m_SerializedSubGraph"": ""{{\n    \""subGraph\"": {{\n        \""fileID\"": {SubGraphFileId},\n        \""guid\"": \""{SubGraphGuid}\"",\n        \""type\"": 3\n    }}\n}}"",
    ""m_PropertyGuids"": [""3854891f-1d98-45e8-91dc-346a3d2b6de9""],
    ""m_PropertyIds"": [{InputSlotId}],
    ""m_Dropdowns"": [],
    ""m_DropdownSelectedEntries"": []
}}";

        static string GetMatchingId(string file, string nodeId)
        {
            var matches = Regex.Matches(
                file,
                @"""m_OutputSlot""[\s\S]*?""m_Id""\s*:\s*""([^""]+)""[\s\S]*?""m_InputSlot""[\s\S]*?""m_Id""\s*:\s*""([^""]+)"""
            );

            foreach (Match match in matches)
            {
                Debug.Log(match.Value);
                string outputId = match.Groups[1].Value;
                string inputId = match.Groups[2].Value;

                if (outputId == nodeId)
                {
                    return inputId;
                }

                if (inputId == nodeId)
                {
                    return outputId;
                }
            }

            return "";
        }

        public static string GetBlockNodeNodeId(string jsonText, string nodeName)
        {
            string escapedNodeName = Regex.Escape(nodeName);
            string pattern = $@"""m_Type""\s*:\s*""UnityEditor\.ShaderGraph\.BlockNode"",\s*""m_ObjectId""\s*:\s*""([^""]+)""(?:(?!""m_ObjectId"").)*?""m_Name""\s*:\s*""{escapedNodeName}""";

            Match match = Regex.Match(jsonText, pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        public static void UnpatchShader(string assetPath, string connectedBlockNodeTitle)
        {
            string fullPath = Path.GetFullPath(assetPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"No file at path: {fullPath}");
                return;
            }

            string file = File.ReadAllText(fullPath);

            if (!file.Contains(SubGraphGuid))
            {
                Debug.LogWarning("Shader Graph does not appear to contain the COZY fog integration. Nothing to remove.");
                return;
            }

            // 1. Find the generated ID of the injected SubGraph
            string subGraphNodeId = GetInjectedNodeId(file);

            Debug.Log(subGraphNodeId);

            if (string.IsNullOrEmpty(subGraphNodeId))
            {
                Debug.LogError("Could not find the injected SubGraph node ID.");
                return;
            }

            Match inEdgeMatch = Regex.Match(file, @"[ \t]*\{\s*""m_OutputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id""\s*:\s*""([^""]+)""\s*\}\s*,\s*""m_SlotId""\s*:\s*(-?\d+)\s*\}\s*,\s*""m_InputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id""\s*:\s*""" + subGraphNodeId + @"""\s*\}\s*,\s*""m_SlotId""\s*:\s*(-?\d+)\s*\}\s*\}\,?\s*");
            Match outEdgeMatch = Regex.Match(file, @"\{\s*""m_OutputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id""\s*:\s*""" + subGraphNodeId + @"""\s*\}\s*,\s*""m_SlotId""\s*:\s*(-?\d+)\s*\}\s*,\s*""m_InputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id""\s*:\s*""([^""]+)""\s*\}\s*,\s*""m_SlotId""\s*:\s*(-?\d+)\s*\}\s*\}");

            if (!inEdgeMatch.Success || !outEdgeMatch.Success)
            {
                Debug.LogError("Could not find the original edges. The graph might have been heavily modified manually.");
                return;
            }

            Match nodeArrayMatch = Regex.Match(file, @"\s*\{\s*""m_Id""\s*:\s*""" + subGraphNodeId + @"""\s*\}\s*");
            Match nodeObjectMatch = Regex.Match(file, $@"\s*\{{\s*""m_SGVersion""[^{{}}]*""m_ObjectId"":\s*""{subGraphNodeId}""(?>[^{{}}]+|\{{(?<Depth>)|\}}(?<-Depth>))*(?(Depth)(?!))\}}", RegexOptions.Multiline);


            // 3. Extract original Upstream and Downstream node IDs/Slots
            string upstreamId = inEdgeMatch.Groups[1].Value;
            int upstreamSlot = int.Parse(inEdgeMatch.Groups[2].Value);

            string downstreamId = outEdgeMatch.Groups[2].Value;
            int downstreamSlot = int.Parse(outEdgeMatch.Groups[3].Value);

            string originalEdge = BuildEdge(upstreamId, upstreamSlot, downstreamId, downstreamSlot);

            // 4. Reconnect original edge and remove the injected ones
            file = file.Replace(outEdgeMatch.Value, originalEdge);
            file = file.Replace(inEdgeMatch.Value, "");

            file = Regex.Replace(
                file,
                @",?\s*\{\s*""m_Id""\s*:\s*""" + subGraphNodeId + @"""\s*\}\s*,(\r?\n)?",
                m => m.Value.TrimStart().StartsWith(",") ? "" : ","
            );
            file = file.Replace(nodeObjectMatch.Value, "");

            // // 6. Clean up trailing/double commas resulting from deleted JSON blocks
            file = Regex.Replace(file, @"\,\s*\,", ",");
            file = Regex.Replace(file, @"\[\s*\,", "[\n");
            file = Regex.Replace(file, @"\,\s*\]", "\n]");

            File.WriteAllText(fullPath, file, Encoding.UTF8);

#if UNITY_EDITOR
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
#endif

            Debug.Log($"Unpatched Shader Graph: {assetPath}");
        }

        public static bool Installed(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"No file at path: {fullPath}");
                return false;
            }

            string file = File.ReadAllText(fullPath);

            if (file.Contains(SubGraphGuid))
            {
                return true;
            }

            return false;
        }
        
        static string GetInjectedNodeId(string jsonText)
        {
            string escapedGuid = Regex.Escape(SubGraphGuid);

            // Look for the m_ObjectId closest to our known SubGraphGuid
            string pattern = @"""m_ObjectId""\s*:\s*""([^""]+)""(?:(?!""m_ObjectId"").)*?" + escapedGuid;
            Match match = Regex.Match(jsonText, pattern, RegexOptions.Singleline);

            if (match.Success)
                return match.Groups[1].Value;

            // Fallback: In case Unity reserialized the properties in reverse order
            pattern = escapedGuid + @"(?:(?!""m_ObjectId"").)*?""m_ObjectId""\s*:\s*""([^""]+)""";
            match = Regex.Match(jsonText, pattern, RegexOptions.Singleline);

            return match.Success ? match.Groups[1].Value : null;
        }

        static string NewId() => Guid.NewGuid().ToString("N");
    }
}