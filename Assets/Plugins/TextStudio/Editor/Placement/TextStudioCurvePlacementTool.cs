using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace TextStudio.Editor
{
    /// <summary>Edits one custom profile in the text's local layout space.</summary>
    [EditorTool("Edit Text Curve", typeof(TextStudioText))]
    internal sealed class TextStudioCurvePlacementTool : EditorTool
    {
        readonly Vector3[] _points = new Vector3[65];
        int _activeMatch;

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView || target is not TextStudioText hub ||
                hub.PlacementMode != PlacementMode.Curve) return;
            int matchCount = hub.CurveGuideCount;
            if (matchCount <= 0)
            {
                DrawMessage("Text layout is unavailable. Refresh the text, then edit the curve.");
                return;
            }
            _activeMatch = Mathf.Clamp(_activeMatch, 0, matchCount - 1);
            DrawMatchPicker(matchCount);
            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            try
            {
                Handles.matrix = hub.transform.localToWorldMatrix;
                int lineCount = Mathf.Max(1, hub.Plan.LineCount);
                for (int match = 0; match < matchCount; match++)
                for (int line = 0; line < lineCount; line++)
                {
                    if (!hub.TryGetCurvePlacementGuide(match, line, out TextStudioCurvePlacementGuide guide))
                        continue;
                    Handles.color = match == _activeMatch
                        ? new Color(.28f, .8f, 1f, 1f) : new Color(.45f, .65f, .8f, .45f);
                    for (int sample = 0; sample < _points.Length; sample++)
                        _points[sample] = guide.Evaluate(Mathf.Lerp(guide.SegmentMin,
                            guide.SegmentMax, (float)sample / (_points.Length - 1)), out _);
                    Handles.DrawAAPolyLine(2f, _points);
                }
                if (hub.TryGetCurvePlacementGuide(_activeMatch, -1, out TextStudioCurvePlacementGuide active))
                    DrawKeys(hub, in active);
            }
            finally
            {
                Handles.matrix = previousMatrix;
                Handles.color = previousColor;
            }
        }

        static void DrawMessage(string message)
        {
            Handles.BeginGUI();
            GUI.Label(new Rect(16f, 16f, 520f, 24f), message, EditorStyles.helpBox);
            Handles.EndGUI();
        }

        void DrawMatchPicker(int count)
        {
            Handles.BeginGUI();
            if (GUI.Button(new Rect(16f, 16f, 28f, 24f), "‹"))
                _activeMatch = (_activeMatch + count - 1) % count;
            GUI.Label(new Rect(48f, 16f, 120f, 24f), "Match " + (_activeMatch + 1) + " / " + count,
                EditorStyles.helpBox);
            if (GUI.Button(new Rect(172f, 16f, 28f, 24f), "›"))
                _activeMatch = (_activeMatch + 1) % count;
            Handles.EndGUI();
        }

        static void DrawKeys(TextStudioText hub, in TextStudioCurvePlacementGuide guide)
        {
            SerializedObject serialized = new SerializedObject(hub);
            CurvePlacement settings = serialized.FindProperty("_curvePlacement").boxedValue as CurvePlacement;
            if (settings == null || settings.Curve.Definition.CurveType != CurveType.Custom)
            {
                DrawMessage("Choose Custom as the curve shape to edit knots and tangents here.");
                return;
            }
            if (Mathf.Abs(guide.Height) < .00001f || guide.DomainMax - guide.DomainMin < .00001f)
            {
                DrawMessage("A nonzero height and text width are needed for Scene editing.");
                return;
            }
            AnimationCurve curve = settings.Curve.Definition.CustomCurve;
            Keyframe[] keys = curve.keys;
            float width = guide.DomainMax - guide.DomainMin;
            for (int index = 0; index < keys.Length; index++)
            {
                Keyframe key = keys[index];
                Vector3 point = new Vector3(Mathf.Lerp(guide.DomainMin, guide.DomainMax, key.time),
                    guide.Baseline + guide.Height * key.value, guide.Depth);
                Vector3 worldPoint = hub.transform.TransformPoint(point);
                float size = HandleUtility.GetHandleSize(worldPoint) * .025f;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(point, size, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    key.time = Mathf.Clamp01((moved.x - guide.DomainMin) / width);
                    key.value = (moved.y - guide.Baseline) / guide.Height;
                    curve.MoveKey(index, key);
                    ApplyCurve(hub, settings, curve, "Move Curve Knot");
                    break;
                }
                if (DrawTangent(hub, settings, curve, guide, index, key, point, size, true) ||
                    DrawTangent(hub, settings, curve, guide, index, key, point, size, false))
                    break;
            }
        }

        static bool DrawTangent(TextStudioText hub, CurvePlacement settings, AnimationCurve curve,
            in TextStudioCurvePlacementGuide guide, int index, Keyframe key, Vector3 point,
            float keySize, bool outgoing)
        {
            float direction = outgoing ? 1f : -1f;
            float length = .08f;
            float tangent = outgoing ? key.outTangent : key.inTangent;
            if (!float.IsFinite(tangent)) tangent = 0f;
            Vector3 endpoint = point + new Vector3(direction * length * (guide.DomainMax - guide.DomainMin),
                direction * length * tangent * guide.Height, 0f);
            Handles.color = new Color(.98f, .72f, .25f, 1f);
            Handles.DrawLine(point, endpoint);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(endpoint, keySize * .65f, Vector3.zero,
                Handles.RectangleHandleCap);
            if (!EditorGUI.EndChangeCheck()) return false;
            float dx = (moved.x - point.x) / (guide.DomainMax - guide.DomainMin);
            if (Mathf.Abs(dx) < .00001f) return false;
            float slope = ((moved.y - point.y) / guide.Height) / dx;
            if (!float.IsFinite(slope)) return false;
            if (outgoing) key.outTangent = slope;
            else key.inTangent = slope;
            curve.MoveKey(index, key);
            ApplyCurve(hub, settings, curve, "Edit Curve Tangent");
            return true;
        }

        static void ApplyCurve(TextStudioText hub, CurvePlacement settings, AnimationCurve curve,
            string undoLabel)
        {
            TextStudioCurveDefinition definition = new TextStudioCurveDefinition(CurveType.Custom, curve);
            TextStudioCurveProfile profile = settings.Curve.Profile;
            if (profile != null)
            {
                Undo.RecordObject(profile, undoLabel);
                profile.SetDefinition(in definition);
                EditorUtility.SetDirty(profile);
            }
            else
            {
                Undo.RecordObject(hub, undoLabel);
                hub.ConfigureCurvePlacement(in definition, settings.Height, settings.Selection,
                    settings.Mapping);
                PrefabUtility.RecordPrefabInstancePropertyModifications(hub);
                EditorUtility.SetDirty(hub);
            }
            SceneView.RepaintAll();
        }
    }
}
