//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    public static class HlslPatcher
    {
        private const string FogInclude = "#include \"Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Fog/CozyFogFunctions.hlsl\"";
        private const string Marker = " // COZY HLSL patch";
        private const string OriginalIncludeMarker = " // COZY HLSL original: ";
        private static readonly Regex IgnoredText = new Regex(@"//[^\r\n]*|/\*[\s\S]*?\*/|""(?:\\.|[^""\\])*""", RegexOptions.Compiled);
        private static readonly Regex FogCall = new Regex(@"\bMixFog\s*\(", RegexOptions.Compiled);
        private static readonly Regex ReturnStatement = new Regex(@"\breturn\b", RegexOptions.Compiled);

        public static bool Installed(string assetPath, string blendLine)
        {
            if (!ValidateBlendLine(blendLine) || !TryRead(assetPath, out string file))
                return false;

            if (!TryFindReturns(file, assetPath, out List<int> returns))
                return false;

            if (!HasFogInclude(file))
                return false;

            foreach (int index in returns)
                if (!HasBlendBeforeReturn(file, index, blendLine))
                    return false;
            return true;
        }

        public static void PatchShader(string assetPath, string blendLine)
        {
            if (!ValidateBlendLine(blendLine) || !TryRead(assetPath, out string file) ||
                !TryFindReturns(file, assetPath, out List<int> returns))
                return;

            string newline = file.Contains("\r\n") ? "\r\n" : "\n";
            for (int i = returns.Count - 1; i >= 0; i--)
            {
                int index = returns[i];
                if (HasBlendBeforeReturn(file, index, blendLine))
                    continue;

                int lineStart = file.LastIndexOf('\n', index - 1) + 1;
                string indentation = file.Substring(lineStart, index - lineStart);
                if (!string.IsNullOrWhiteSpace(indentation) && indentation.Length != 0)
                {
                    Debug.LogError($"Cannot insert a fog blend before an inline return in {assetPath}.");
                    return;
                }
                file = file.Insert(lineStart, indentation + blendLine + Marker + newline);
            }

            if (!HasFogInclude(file, true))
            {
                Match include = Regex.Match(file, @"(?m)^[ \t]*#\s*include\s+""[^""]+""[^\r\n]*(?:\r?\n|$)");
                if (!include.Success)
                {
                    Debug.LogError($"Cannot find an include in {assetPath}.");
                    return;
                }
                string prefix = include.Value.EndsWith("\n", StringComparison.Ordinal) ? "" : newline;
                file = file.Insert(include.Index + include.Length, prefix + FogInclude + Marker + newline);
            }
            else
            {
                file = Regex.Replace(file, @"(?mi)^([ \t]*)" + Regex.Escape(FogInclude) + @"(?=[ \t\r]*(?://|$))",
                    match => match.Value == match.Groups[1].Value + FogInclude
                        ? match.Value
                        : match.Groups[1].Value + FogInclude + OriginalIncludeMarker + match.Value.TrimStart());
            }

            Write(assetPath, file);
        }

        public static void UnpatchShader(string assetPath, string blendLine)
        {
            if (!ValidateBlendLine(blendLine) || !TryRead(assetPath, out string file) ||
                !TryFindReturns(file, assetPath, out List<int> returns))
                return;

            string blendPattern = @"(?m)^[ \t]*" + Regex.Escape(blendLine) + @"(?:[ \t]*// COZY HLSL patch)?[ \t]*(?:\r?\n|$)";
            for (int i = returns.Count - 1; i >= 0; i--)
            {
                int index = returns[i];
                MatchCollection matches = Regex.Matches(file.Substring(0, index), blendPattern);
                if (matches.Count == 0)
                    continue;
                Match match = matches[matches.Count - 1];
                if (string.IsNullOrWhiteSpace(file.Substring(match.Index + match.Length, index - match.Index - match.Length)))
                    file = file.Remove(match.Index, match.Length);
            }

            file = Regex.Replace(file, @"(?m)^([ \t]*)" + Regex.Escape(FogInclude) + Regex.Escape(OriginalIncludeMarker) +
                @"(#include ""[^""]+"")", match => match.Groups[1].Value + match.Groups[2].Value);

            // Preserve a patch-owned include if other fog calls still need it.
            string code = IgnoredText.Replace(file, match => Regex.Replace(match.Value, @"[^\r\n]", " "));
            if (!Regex.IsMatch(code, @"\bBlendStylizedFog(?:_float)?\s*\("))
                file = Regex.Replace(file, @"(?mi)^[ \t]*" + Regex.Escape(FogInclude) + Regex.Escape(Marker) + @"[ \t]*(?:\r?\n|$)", "");
            Write(assetPath, file);
        }

        private static bool ValidateBlendLine(string blendLine)
        {
            if (!string.IsNullOrWhiteSpace(blendLine) && blendLine.IndexOfAny(new[] { '\r', '\n' }) < 0)
                return true;
            Debug.LogError("The HLSL fog blend must be a single non-empty line.");
            return false;
        }

        private static bool TryRead(string assetPath, out string file)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"No file at path: {fullPath}");
                file = null;
                return false;
            }
            file = File.ReadAllText(fullPath);
            return true;
        }

        private static bool HasFogInclude(string file, bool ignoreCase = false)
        {
            RegexOptions options = RegexOptions.Multiline | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            return Regex.IsMatch(file, @"^[ \t]*" + Regex.Escape(FogInclude) + @"[ \t]*(?://[^\r\n]*)?\r?$", options);
        }

        private static bool HasBlendBeforeReturn(string file, int returnIndex, string blendLine)
        {
            string prefix = file.Substring(0, returnIndex);
            return Regex.IsMatch(prefix, @"(?m)^[ \t]*" + Regex.Escape(blendLine) + @"(?:[ \t]*// COZY HLSL patch)?[ \t]*(?:\r?\n)[ \t\r\n]*$");
        }

        private static bool TryFindReturns(string file, string assetPath, out List<int> returns)
        {
            returns = new List<int>();
            string code = IgnoredText.Replace(file, match => Regex.Replace(match.Value, @"[^\r\n]", " "));
            MatchCollection calls = FogCall.Matches(code);
            if (calls.Count == 0)
            {
                Debug.LogError($"No MixFog call found in {assetPath}.");
                return false;
            }

            foreach (Match call in calls)
            {
                int depth = 0;
                int returnIndex = -1;
                Match nextReturn = ReturnStatement.Match(code, call.Index + call.Length);
                for (int i = call.Index + call.Length; i < code.Length; i++)
                {
                    if (code[i] == '{') depth++;
                    if (code[i] == '}') depth--;
                    if (depth < 0) break;
                    if (nextReturn.Success && i == nextReturn.Index)
                    {
                        if (depth == 0)
                        {
                            returnIndex = i;
                            break;
                        }
                        Debug.LogError($"A conditional return follows a MixFog call in {assetPath}; patch it manually.");
                        return false;
                    }
                }
                if (returnIndex < 0)
                {
                    Debug.LogError($"No return in the same scope as a MixFog call in {assetPath}.");
                    return false;
                }
                if (!returns.Contains(returnIndex))
                    returns.Add(returnIndex);
            }
            returns.Sort();
            return true;
        }

        private static void Write(string assetPath, string file)
        {
            if (File.ReadAllText(assetPath) == file)
                return;
            byte[] bytes = File.ReadAllBytes(assetPath);
            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
            File.WriteAllText(assetPath, file, new UTF8Encoding(hasBom));
#if UNITY_EDITOR
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
#endif
            Debug.Log($"Updated HLSL fog integration: {assetPath}");
        }
    }
}
