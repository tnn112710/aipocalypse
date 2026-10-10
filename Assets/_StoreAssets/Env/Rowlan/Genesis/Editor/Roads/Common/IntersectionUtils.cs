#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class IntersectionUtils
    {
        private const float raycastStartOffsetY = 1000f;

        public static void AutoConnect( RoadSystem roadSystem, Intersection intersection, Intersection targetIntersection, Intersection.ConnectionPoint sourcePoint, Intersection.ConnectionPoint targetPoint)
        {
            if( sourcePoint.road != null || targetPoint.road != null )
            {
                // Debug.Log($"Already connected, {sourcePoint.road} <-> {targetPoint.road}");
                return;
            }

            // note: same as the spawn code, except for the knot calculation
            IntersectionEditor editor = Editor.CreateEditor(intersection) as IntersectionEditor;
            Road road = editor.CreateRoad(roadSystem, intersection, sourcePoint, false);
            Editor.DestroyImmediate(editor);

            SplineContainer splineContainer = road.splineContainer;
            Spline spline = splineContainer.Spline;

            float3 forward = sourcePoint.connector.transform.forward;
            float size = sourcePoint.connector.config.entries[0].size;

            #region connection calculation

            // remove auto-generated knots (were created in IntersectionEditor)
            spline.Clear();

            // start knot
            spline.Add(new BezierKnot(new Vector3(0, 0, 0), -forward * size * 0.4f, forward * size * 0.4f, Quaternion.identity));

            // end knot
            Vector3 targetWorldPos = targetPoint.connector.transform.position;
            Vector3 sourceLocalPos = splineContainer.transform.worldToLocalMatrix.MultiplyPoint(targetWorldPos); // convert world to spline's local coordinate
            spline.Add(new BezierKnot(sourceLocalPos, -forward * size * 0.4f, forward * size * 0.4f, targetPoint.connector.transform.rotation));

            #endregion connection calculation

            spline.SetTangentMode(1, TangentMode.AutoSmooth);
            splineContainer.RemoveSplineAt(0);
            splineContainer.AddSpline(spline);

            // anchor road to target
            targetPoint.front = false;
            targetPoint.container = splineContainer;
            targetPoint.road = road;

            // update source intersection
            UpdateIntersection(roadSystem, intersection);

            // update the target as well for spline alignment
            UpdateIntersection(roadSystem, targetIntersection);
        }

        public static void ClearSpawnedRoads(RoadSystem roadSystem, Intersection intersection)
        {

            // undo doesn't work because the road is spawned as separate object in a different hierarchy
            // Undo.RegisterCompleteObjectUndo(target, "Clear Spawned Roads");

            foreach (Intersection.ConnectionPoint cp in intersection.connectionPoints)
            {
                if (cp == null || cp.road == null)
                    continue;

                GameObject.DestroyImmediate(cp.road.gameObject);
            }

            UpdateIntersection(roadSystem, intersection);
        }

        /// <summary>
        /// Get the intersection of the connection point
        /// </summary>
        /// <param name="connectionPoint"></param>
        /// <returns></returns>
        public static Intersection GetIntersection( Intersection.ConnectionPoint connectionPoint)
        {
            return connectionPoint?.connector.GetComponentInParent<Intersection>();
        }

        /// <summary>
        /// Get the connection point to which the road is connected
        /// </summary>
        /// <param name="roadSystem"></param>
        /// <param name="road"></param>
        /// <param name="front"></param>
        /// <returns></returns>
        public static Intersection.ConnectionPoint GetConnectionPoint(Road road, bool front)
        {
            RoadSystem roadSystem = road.GetComponentInParent<RoadSystem>();
            return GetConnectionPoint( roadSystem, road, front );
        }

        /// <summary>
        /// Get the connection point to which the road is connected
        /// </summary>
        /// <param name="roadSystem"></param>
        /// <param name="road"></param>
        /// <param name="front"></param>
        /// <returns></returns>
        public static Intersection.ConnectionPoint GetConnectionPoint(RoadSystem roadSystem, Road road, bool front)
        {
            Intersection[] intersections = GetAllIntersections( roadSystem);
            foreach(Intersection intersection in intersections)
            {
                foreach (Intersection.ConnectionPoint cp in intersection.connectionPoints)
                {
                    if (cp != null && cp.road == road && cp.front == front)
                        return cp;
                }
            }

            return null;
        }

        /// <summary>
        /// Get all intersections of the road system
        /// </summary>
        /// <param name="roadSystem"></param>
        /// <returns></returns>
        public static Intersection[] GetAllIntersections(RoadSystem roadSystem)
        {
            return roadSystem.GetComponentsInChildren<Intersection>();
        }

        public static void UpdateIntersection(RoadSystem roadSystem, Intersection intersection)
        {
            intersection.UpdateConnections();

            // update connections on the road as well
            foreach (Intersection.ConnectionPoint cp in intersection.connectionPoints)
            {
                if (cp == null || cp.road == null)
                    continue;

                cp.road.UpdateConnections(roadSystem, false);
            }


        }

        public static void AlignToGround(RoadSystem roadSystem, Intersection intersection, LayerMask layer, float positionOffsetY)
        {
            Vector3 raycastPosition = intersection.transform.position;

            // raycast from top
            raycastPosition.y += raycastStartOffsetY;

            RaycastHit[] raycastHits = Physics.RaycastAll(raycastPosition, Vector3.down, Mathf.Infinity, layer);
            foreach(RaycastHit hit in raycastHits)
            {
                // update position, but ignore "self" for collision detection
                if (hit.collider.gameObject != intersection.gameObject)
                {
                    Vector3 position = hit.point;
                    position.y += positionOffsetY;
                    intersection.transform.position = position;

                    UpdateIntersection(roadSystem, intersection);
                    break;

                }
            }
        }

        public static void RandomPosition(RoadSystem roadSystem, Intersection intersection, float min, float max)
        {
            float offsetX = UnityEngine.Random.Range(min, max);
            float offsetZ = UnityEngine.Random.Range(min, max);

            Vector3 position = intersection.transform.localPosition;

            position.x += offsetX;
            position.z += offsetZ;

            intersection.transform.localPosition = position;

            UpdateIntersection(roadSystem, intersection);
        }


        public static void RandomRotation(RoadSystem roadSystem, Intersection intersection, float min, float max )
        {
            float offsetY = UnityEngine.Random.Range(min, max);

            Vector3 rotation = intersection.transform.localEulerAngles;
            rotation.y += offsetY;

            intersection.transform.localRotation = Quaternion.Euler(rotation);

            UpdateIntersection(roadSystem, intersection);
        }

        public static void RandomHeight(RoadSystem roadSystem, Intersection intersection, float min, float max)
        {
            float offsetY = UnityEngine.Random.Range(min, max);

            Vector3 position = intersection.transform.localPosition;

            position.y += offsetY;

            // currently only going higher. we don't have a reference point for height yet, will come later
            //if (position.y < 0.1)
            //    position.y = 0.1f;

            intersection.transform.localPosition = position;

            UpdateIntersection(roadSystem, intersection);
        }

    }
}
#endif