using System;

namespace TextStudio
{
    /// <summary>Separable exact Euclidean distances to thresholded texel centers. Pure managed code, safe on a worker.</summary>
    internal static class TextStudioSilhouetteDistanceTransform
    {
        const float Far = 1e20f;

        internal static float[] Generate(byte[] alpha, int width, int height, float stepX, float stepY, float threshold, float range)
        {
            if (alpha == null || width <= 0 || height <= 0 || (long)width * height != alpha.Length)
                throw new ArgumentException("The alpha tile dimensions do not match its storage.");
            if (!float.IsFinite(stepX) || !float.IsFinite(stepY) || stepX <= 0 || stepY <= 0 ||
                !float.IsFinite(range) || range <= 0 || !float.IsFinite(threshold) || threshold < 0 || threshold > 1)
                throw new ArgumentOutOfRangeException(nameof(range));
            float[] inside = new float[alpha.Length];
            float[] outside = new float[alpha.Length];
            for (int index = 0; index < alpha.Length; index++)
            {
                // A zero threshold must never turn transparent atlas padding into ink.
                bool ink = alpha[index] > 0 && alpha[index] / 255f >= threshold;
                inside[index] = ink ? 0 : Far;
                outside[index] = ink ? Far : 0;
            }
            int dimension = Math.Max(width, height);
            float[] input = new float[dimension];
            float[] output = new float[dimension];
            int[] sites = new int[dimension];
            float[] boundaries = new float[dimension + 1];
            Transform(inside, width, height, stepX, stepY, input, output, sites, boundaries);
            Transform(outside, width, height, stepX, stepY, input, output, sites, boundaries);
            // The binary contour lies half a texel from a center. Raster error is bounded by a texel diagonal.
            float halfTexel = .5f * Math.Min(stepX, stepY);
            for (int index = 0; index < alpha.Length; index++)
            {
                bool ink = alpha[index] > 0 && alpha[index] / 255f >= threshold;
                float distance = ink ? (float)Math.Sqrt(outside[index]) - halfTexel : halfTexel - (float)Math.Sqrt(inside[index]);
                inside[index] = .5f + Math.Clamp(distance / (2 * range), -.5f, .5f);
            }
            return inside;
        }

        static void Transform(float[] data, int width, int height, float stepX, float stepY,
            float[] input, float[] output, int[] sites, float[] boundaries)
        {
            for (int y = 0; y < height; y++)
            {
                Array.Copy(data, y * width, input, 0, width);
                TransformLine(input, output, width, stepX * stepX, sites, boundaries);
                Array.Copy(output, 0, data, y * width, width);
            }
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++) input[y] = data[y * width + x];
                TransformLine(input, output, height, stepY * stepY, sites, boundaries);
                for (int y = 0; y < height; y++) data[y * width + x] = output[y];
            }
        }

        static void TransformLine(float[] input, float[] output, int count, float scale, int[] sites, float[] boundaries)
        {
            int last = -1;
            for (int position = 0; position < count; position++)
            {
                if (input[position] >= Far) continue;
                float boundary = float.NegativeInfinity;
                while (last >= 0)
                {
                    int site = sites[last];
                    boundary = ((input[position] - input[site]) / scale + (float)position * position - (float)site * site) / (2 * (position - site));
                    if (boundary > boundaries[last]) break;
                    last--;
                }
                if (last < 0) boundary = float.NegativeInfinity;
                sites[++last] = position;
                boundaries[last] = boundary;
                boundaries[last + 1] = float.PositiveInfinity;
            }
            if (last < 0) { for (int index = 0; index < count; index++) output[index] = Far; return; }
            int envelope = 0;
            for (int position = 0; position < count; position++)
            {
                while (boundaries[envelope + 1] < position) envelope++;
                float distance = position - sites[envelope];
                output[position] = distance * distance * scale + input[sites[envelope]];
            }
        }
    }
}
