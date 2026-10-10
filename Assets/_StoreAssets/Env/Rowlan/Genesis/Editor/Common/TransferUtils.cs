using JBooth.MicroVerseCore;
using System.Collections.Generic;

namespace Rowlan.Genesis
{
    public class TransferUtils
    {
        /// <summary>
        /// Take the transform of the stamp and transfer the noise of the stamp's filterset to all the siblings.
        /// eg if you have a detail stamp transform parent with several grass detail stamp children you can transfer the settings from one grass detail stamp to all the others
        /// </summary>
        /// <param name="transform"></param>
        public static void TransferNoise(Stamp source, List<Stamp> targets)
        {

            foreach (Stamp stamp in targets)
            {
                FilterSet currentFilterSet = source.GetFilterSet();

                if (currentFilterSet == null)
                    continue;

                FilterSet otherFilterSet = stamp.GetFilterSet();

                if (otherFilterSet == null)
                    continue;

                // clone and transfer data
                otherFilterSet.weightNoise = currentFilterSet.weightNoise.Clone() as Noise;

                otherFilterSet.weight2Noise = currentFilterSet.weight2Noise.Clone() as Noise;
                otherFilterSet.weight2NoiseOp = currentFilterSet.weight2NoiseOp;

                otherFilterSet.weight3Noise = currentFilterSet.weight3Noise.Clone() as Noise;
                otherFilterSet.weight3NoiseOp = currentFilterSet.weight3NoiseOp;

            }

            MicroVerse.instance.Invalidate();
        }

        public static void TransferDensity(Stamp source, List<Stamp> targets)
        {
#if __MICROVERSE_VEGETATION__

            foreach (Stamp stamp in targets)
            {
                if (source is DetailStamp && stamp is DetailStamp)
                {
                    DetailStamp current = (DetailStamp)source;
                    DetailStamp sibling = (DetailStamp)stamp;

                    sibling.prototype.density = current.prototype.density;

                }
            }

            MicroVerse.instance.Invalidate();
#endif
        }

        public static void TransferSize(Stamp source, List<Stamp> targets)
        {
#if __MICROVERSE_VEGETATION__

            foreach (Stamp stamp in targets)
            {
                if (source is DetailStamp && stamp is DetailStamp)
                {
                    DetailStamp current = (DetailStamp)source;
                    DetailStamp target = (DetailStamp)stamp;

                    target.prototype.minWidth = current.prototype.minWidth;
                    target.prototype.maxWidth = current.prototype.maxWidth;
                    target.prototype.minHeight = current.prototype.minHeight;
                    target.prototype.maxHeight = current.prototype.maxHeight;

                }
            }

            MicroVerse.instance.Invalidate();
#endif
        }

        public static void TransferDistance(Stamp source, List<Stamp> targets)
        {
#if __MICROVERSE_VEGETATION__

            foreach (Stamp stamp in targets)
            {
                if (source is DetailStamp && stamp is DetailStamp)
                {
                    DetailStamp current = (DetailStamp)source;
                    DetailStamp target = (DetailStamp)stamp;

                    target.minDistanceFromTree = current.minDistanceFromTree;
                    target.maxDistanceFromTree = current.maxDistanceFromTree;

                    target.minDistanceFromObject = current.minDistanceFromObject;
                    target.maxDistanceFromObject = current.maxDistanceFromObject;

                    target.minDistanceFromParent = current.minDistanceFromParent;
                    target.maxDistanceFromParent = current.maxDistanceFromParent;
                }
            }

            MicroVerse.instance.Invalidate();
#endif
        }

        /// <summary>
        /// Take the filters of the stamp and transfer them to all the siblings.
        /// </summary>
        /// <param name="transform"></param>
        public static void TransferFilters(Stamp source, List<Stamp> targets)
        {

            foreach (Stamp stamp in targets)
            {
                FilterSet currentFilterSet = source.GetFilterSet();

                if (currentFilterSet == null)
                    continue;

                FilterSet otherFilterSet = stamp.GetFilterSet();

                if (otherFilterSet == null)
                    continue;

                // clone and transfer data
                otherFilterSet.heightFilter = CloneUtils.Clone(currentFilterSet.heightFilter);
                otherFilterSet.slopeFilter = CloneUtils.Clone(currentFilterSet.slopeFilter);
                otherFilterSet.angleFilter = CloneUtils.Clone(currentFilterSet.angleFilter);
                otherFilterSet.curvatureFilter = CloneUtils.Clone(currentFilterSet.curvatureFilter);
                otherFilterSet.flowFilter = CloneUtils.Clone(currentFilterSet.flowFilter);

                // texture filter is a special case
                otherFilterSet.textureFilterEnabled = currentFilterSet.textureFilterEnabled;
                otherFilterSet.otherTextureWeight = currentFilterSet.otherTextureWeight;
                otherFilterSet.textureFilters = CloneUtils.Clone(currentFilterSet.textureFilters);

            }

            MicroVerse.instance.Invalidate();
        }

    }
}