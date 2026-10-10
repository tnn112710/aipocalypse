using UnityEngine;

namespace TextStudio
{
    /// <summary>Projects a posed glyph's canonical em axes into output pixels, including perspective and nonuniform deformation.</summary>
    internal readonly struct TextStudio2DSourceProjection
    {
        readonly Matrix4x4 _localToClip;
        readonly Vector2 _pixels;
        readonly bool _overlay;
        internal readonly bool IsSceneView;

        internal TextStudio2DSourceProjection(Matrix4x4 localToClip, Vector2 outputPixels, bool overlay = false, bool sceneView = false)
        { _localToClip = localToClip; _pixels = outputPixels; _overlay = overlay; IsSceneView = sceneView; }

        internal void AddFingerprint(ref TextStudioDeterministicHash64 hash)
        {
            for (int index = 0; index < 16; index++) hash.Add(_localToClip[index]);
            hash.Add(_pixels.x); hash.Add(_pixels.y); hash.Add(_overlay); hash.Add(IsSceneView);
        }

        internal bool TryCreateContourProjection(in TextStudio2DTMPGlyphPlane plane, out TextStudioPixelContourProjection projection)
        {
            projection = default;
            Vector3 axisX = (plane.BottomRight - plane.BottomLeft) / plane.EmSize.x;
            Vector3 axisY = (plane.TopLeft - plane.BottomLeft) / plane.EmSize.y;
            float span = Mathf.Max((plane.BottomRight - plane.BottomLeft).magnitude, (plane.TopLeft - plane.BottomLeft).magnitude);
            if ((plane.TopRight - plane.TopLeft - plane.BottomRight + plane.BottomLeft).magnitude > Mathf.Max(.000001f, span * .00001f)) return false;
            Vector4 center = Clip((plane.BottomLeft + plane.TopRight) * .5f);
            if (!Finite(center) || center.w <= .000001f || !_overlay && Clipped(center)) return false;
            Vector2 emCenter = plane.EmMinimum + plane.EmSize * .5f;
            Vector4 x = _localToClip * new Vector4(axisX.x, axisX.y, axisX.z, 0);
            Vector4 y = _localToClip * new Vector4(axisY.x, axisY.y, axisY.z, 0);
            Vector4 denominator = new Vector4(x.w, y.w, 0, 0) / center.w;
            denominator.w = 1 - denominator.x * emCenter.x - denominator.y * emCenter.y;
            Vector2 scale = _pixels * (_overlay ? 1 : .5f);
            Vector4 horizontal = (new Vector4(x.x, y.x, 0, 0) - denominator * center.x) * (scale.x / center.w);
            Vector4 vertical = (new Vector4(x.y, y.y, 0, 0) - denominator * center.y) * (scale.y / center.w);
            horizontal.w = -horizontal.x * emCenter.x - horizontal.y * emCenter.y;
            vertical.w = -vertical.x * emCenter.x - vertical.y * emCenter.y;
            Vector2 direction = new Vector2(horizontal.x, vertical.x);
            float determinant = horizontal.x * vertical.y - horizontal.y * vertical.x;
            if (!float.IsFinite(determinant) || Mathf.Abs(determinant) < .000001f || direction.sqrMagnitude < .000001f) return false;
            direction.Normalize();
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * Mathf.Sign(determinant);
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.SetRow(0, horizontal * direction.x + vertical * direction.y);
            matrix.SetRow(1, horizontal * perpendicular.x + vertical * perpendicular.y);
            matrix.SetRow(3, denominator);
            projection = new TextStudioPixelContourProjection(matrix);
            return true;
        }

        internal bool Measure(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight,
            Vector2 spanEm, out float maximum, out float minimum)
            => Measure(bottomLeft, topLeft, topRight, bottomRight, spanEm, out maximum, out minimum, out _);

        internal bool Measure(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight,
            Vector2 spanEm, out float maximum, out float minimum, out float localPixelsPerUnit)
        {
            localPixelsPerUnit = 0;
            maximum = 0; minimum = float.PositiveInfinity;
            if (spanEm.x <= .000001f || spanEm.y <= .000001f) return false;
            Vector4 a = Clip(bottomLeft), b = Clip(topLeft), c = Clip(topRight), d = Clip(bottomRight);
            if (!Finite(a) || !Finite(b) || !Finite(c) || !Finite(d)) return false;
            if (!_overlay)
            {
                // Camera.projectionMatrix uses Unity's CPU clip convention, with depth in [-w, w].
                if (a.z < -a.w && b.z < -b.w && c.z < -c.w && d.z < -d.w ||
                    a.z > a.w && b.z > b.w && c.z > c.w && d.z > d.w) return false;
                if (Clipped(a) || Clipped(b) || Clipped(c) || Clipped(d))
                {
                    // A near-plane crossing can have unbounded magnification. Keep the best available field.
                    maximum = localPixelsPerUnit = 65536; minimum = 0; return true;
                }
            }
            if (a.w <= .000001f || b.w <= .000001f || c.w <= .000001f || d.w <= .000001f) return false;
            Vector4 bottom = (d - a) / spanEm.x, top = (c - b) / spanEm.x;
            Vector4 left = (b - a) / spanEm.y, right = (c - d) / spanEm.y;
            Include(Derivative(a, bottom), Derivative(a, left), ref maximum, ref minimum);
            Include(Derivative(b, top), Derivative(b, left), ref maximum, ref minimum);
            Include(Derivative(c, top), Derivative(c, right), ref maximum, ref minimum);
            Include(Derivative(d, bottom), Derivative(d, right), ref maximum, ref minimum);
            Vector4 localX = _localToClip.GetColumn(0), localY = _localToClip.GetColumn(1);
            float unusedMinimum = float.PositiveInfinity;
            Include(Derivative(a, localX), Derivative(a, localY), ref localPixelsPerUnit, ref unusedMinimum);
            Include(Derivative(b, localX), Derivative(b, localY), ref localPixelsPerUnit, ref unusedMinimum);
            Include(Derivative(c, localX), Derivative(c, localY), ref localPixelsPerUnit, ref unusedMinimum);
            Include(Derivative(d, localX), Derivative(d, localY), ref localPixelsPerUnit, ref unusedMinimum);
            return float.IsFinite(maximum) && maximum > 0;
        }

        internal float MeasureOutputScale(Vector3 position)
        {
            Vector4 clip = Clip(position);
            if (!Finite(clip) || clip.w <= .000001f || !_overlay && Clipped(clip)) return 0;
            float maximum = 0, minimum = float.PositiveInfinity;
            Include(Derivative(clip, _localToClip.GetColumn(0)), Derivative(clip, _localToClip.GetColumn(1)), ref maximum, ref minimum);
            return float.IsFinite(maximum) ? maximum : 0;
        }

        Vector4 Clip(Vector3 position) => _localToClip * new Vector4(position.x, position.y, position.z, 1);
        static bool Finite(Vector4 clip) => float.IsFinite(clip.x) && float.IsFinite(clip.y) && float.IsFinite(clip.z) && float.IsFinite(clip.w);
        static bool Clipped(Vector4 clip) => clip.w <= .000001f || clip.z < -clip.w || clip.z > clip.w;

        Vector2 Derivative(Vector4 clip, Vector4 direction)
        {
            Vector2 derivative = new Vector2(direction.x * clip.w - clip.x * direction.w,
                direction.y * clip.w - clip.y * direction.w) / (clip.w * clip.w);
            return Vector2.Scale(derivative, _pixels) * (_overlay ? 1 : .5f);
        }

        static void Include(Vector2 x, Vector2 y, ref float maximum, ref float minimum)
        {
            // Singular values of the screen-space Jacobian capture skew and tilted/nonuniform glyph axes.
            float xx = Vector2.Dot(x, x), yy = Vector2.Dot(y, y), xy = Vector2.Dot(x, y);
            float trace = xx + yy;
            float discriminant = Mathf.Sqrt(Mathf.Max(0, (xx - yy) * (xx - yy) + 4 * xy * xy));
            maximum = Mathf.Max(maximum, Mathf.Sqrt(Mathf.Max(0, (trace + discriminant) * .5f)));
            minimum = Mathf.Min(minimum, Mathf.Sqrt(Mathf.Max(0, (trace - discriminant) * .5f)));
        }

        internal static void PoseQuad(in TextStudioFrame frame, in TextStudioVisualGlyph glyph,
            out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d, out Vector2 spanEm)
        {
            a = glyph.BoundsMin; b = new Vector3(glyph.BoundsMin.x, glyph.BoundsMax.y);
            c = glyph.BoundsMax; d = new Vector3(glyph.BoundsMax.x, glyph.BoundsMin.y);
            float em = glyph.LocalEmSize > 0 ? glyph.LocalEmSize : frame.GetEffectUnitScale(glyph.TextUnitIndex);
            spanEm = (glyph.BoundsMax - glyph.BoundsMin) / Mathf.Max(.000001f, em);
            Vector3 center = (a + c) * .5f;
            int unit = glyph.TextUnitIndex;
            EvaluatedGlyphState state;
            GlyphPlacement placement = default;
            bool hasPlacement = false;
            TextStudioCornerOffsets corners;
            float effectScale;
            if (frame.GlyphStates.IsCreated && unit >= 0 && unit < frame.GlyphStateCount)
            {
                TextStudioGlyphInstanceState composed = frame.GlyphStates[unit];
                state = EvaluatedGlyphState.Identity;
                state.positionOffset = composed.position - composed.basePosition;
                state.rotation = composed.rotation; state.scale = composed.scale;
                state.skew = composed.skew; state.pivotOffset = composed.pivotOffset;
                corners = composed.cornerOffsets; effectScale = 1;
            }
            else
            {
                state = frame.Animated.IsCreated && unit >= 0 && unit < frame.AnimatedCount ? frame.Animated[unit] : EvaluatedGlyphState.Identity;
                hasPlacement = frame.Placements.IsCreated && unit >= 0 && unit < frame.PlacementCount;
                if (hasPlacement) placement = frame.Placements[unit];
                corners = state.cornerOffsets; effectScale = frame.GetEffectUnitScale(unit);
            }
            TextStudioGlyphQuadTransform transform = TextStudioGlyphQuadTransformer.CreateFromQuad(a, b, c, d, center,
                in state, in placement, hasPlacement, effectScale, 1);
            a = transform.ApplyPosition(a, corners.GetCanonicalVertexOffset(0), effectScale);
            b = transform.ApplyPosition(b, corners.GetCanonicalVertexOffset(1), effectScale);
            c = transform.ApplyPosition(c, corners.GetCanonicalVertexOffset(2), effectScale);
            d = transform.ApplyPosition(d, corners.GetCanonicalVertexOffset(3), effectScale);
        }
    }
}
