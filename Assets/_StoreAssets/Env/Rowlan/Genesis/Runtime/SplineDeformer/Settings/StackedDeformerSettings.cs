#if __MICROVERSE_SPLINES__

using UnityEngine;

namespace Rowlan.Genesis.SplineDeformer
{
    public class StackedDeformerSettings : ScriptableObject
    {

        public class Source
        {
            public bool previewVisible = true;
        }

        public class Subdivision
        {
            public float maxSegmentLength = 50;
            public int maxSubdivisions = 1;
            public bool previewVisible = true;
        }


        public class TangentNoise
        {
            public float noiseAmplitude = 30f;
            public int noiseSeed = 10;
            public bool previewVisible = true;
        }

        public class ChaikinSmoothing
        {
            public int chaikinIterations = 2;
            public bool previewVisible = true;
        }


        public class Reduction
        {
            public int maxKnots = 20; // default maximum knots
            public bool previewVisible = true;
        }


        public class Target
        {
            public bool previewVisible = true;
        }

        public bool previewAutoUpdate = true;

        public Source source;

        public Subdivision subdivision;
        public TangentNoise tangentNoise;
        public ChaikinSmoothing chaikinSmoothing;
        public Reduction reduction;
        public Target target;

        public void Reset()
        {
            source = new Source();

            subdivision = new Subdivision();
            tangentNoise = new TangentNoise();
            chaikinSmoothing = new ChaikinSmoothing();
            reduction = new Reduction();
            target = new Target();
        }

    }
}
#endif