#if __MICROVERSE_SPLINES__
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.SplineDeformer
{
    public class StackedDeformerBatchProcessor
    {
        /// <summary>
        /// All points of all spline containers, a list per container. Used for grouped preview rendering
        /// </summary>
        public class PointsData
        {
            public List<List<Vector3>> original = new List<List<Vector3>>();
            public List<List<Vector3>> subdivision = new List<List<Vector3>>();
            public List<List<Vector3>> noise = new List<List<Vector3>>();
            public List<List<Vector3>> chaikin = new List<List<Vector3>>();
            public List<List<Vector3>> final = new List<List<Vector3>>();

        }

        /// <summary>
        /// Poinst in world position as required by the preview painter, ie with relative offset between the spline areas.
        /// This is used as cached data for performance reasons for painting the preview over and over in the inspector.
        /// </summary>
        public PointsData worldPoints = new PointsData();

        /// <summary>
        /// Map spline container to stacked deformer processor
        /// </summary>
        private Dictionary<SplineContainer, StackedDeformerProcessor> containerProcessorMap = new Dictionary<SplineContainer, StackedDeformerProcessor>();

        public void ProcessStack(SplineContainer[] splineContainers, StackedDeformerSettings settings, int splineIndex)
        {
            containerProcessorMap.Clear();

            foreach(SplineContainer container in splineContainers)
            {
                StackedDeformerProcessor processor = new StackedDeformerProcessor();

                if (container == null || container.Splines.Count == 0 || splineIndex >= container.Splines.Count)
                    continue;

                Spline spline = container[splineIndex];

                List<Vector3> positions = spline.Select(knot => (Vector3)knot.Position).ToList();

                processor.ProcessStack(positions, settings);

                containerProcessorMap.Add(container, processor);
            }

            CreatePointGroups();
        }

        public SplineContainer[] GetSplineContainers()
        {
            return containerProcessorMap.Keys.ToArray();
        }

        public StackedDeformerProcessor GetProcessor(SplineContainer container)
        {
            return containerProcessorMap[container];
        }

        /// <summary>
        /// Merge all the points into a list of list of world points, ie groups of polygons so they can be drawn by the polygon preview with their distance to each other (ie no overlapping by giving them all the same position)
        /// </summary>
        private void CreatePointGroups()
        {
            worldPoints = new PointsData();

            foreach (var item in containerProcessorMap)
            {
                // world: considering spline container transform
                worldPoints.original.Add(item.Value.pointStack.originalPoints.Select( point => item.Key.transform.TransformPoint(point)).ToList());
                worldPoints.subdivision.Add(item.Value.pointStack.subdivisionPoints.Select(point => item.Key.transform.TransformPoint(point)).ToList());
                worldPoints.noise.Add(item.Value.pointStack.noisePoints.Select(point => item.Key.transform.TransformPoint(point)).ToList());
                worldPoints.chaikin.Add(item.Value.pointStack.chaikinPoints.Select(point => item.Key.transform.TransformPoint(point)).ToList());
                worldPoints.final.Add(item.Value.pointStack.finalPoints.Select(point => item.Key.transform.TransformPoint(point)).ToList());

            }
        }
    }
}
#endif