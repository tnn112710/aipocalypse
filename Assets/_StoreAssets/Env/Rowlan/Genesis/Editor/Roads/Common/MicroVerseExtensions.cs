#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;

namespace Rowlan.Genesis.Roads
{
    public static class MicroVerseExtensions
    {
        /// <summary>
        /// Iterate through all MicroVerse road systems and update them.
        /// </summary>
        /// <param name="microVerse"></param>
        public static void ClearCacheAndUpdateRoadSystems(this MicroVerse microVerse)
        {
            if (!microVerse)
                return;

            RoadSystem[] roadSystems = microVerse.GetComponentsInChildren<RoadSystem>();

            foreach (RoadSystem roadSystem in roadSystems)
            {
                ClearCacheAndUpdateSystem(roadSystem);
            }
        }

        /// <summary>
        /// Clear the cache of the roadsystem and update it by recreating all roads, intersections, connections, etc.
        /// </summary>
        /// <param name="roadSystem"></param>
        public static void ClearCacheAndUpdateSystem( this RoadSystem roadSystem)
        {
            roadSystem.ReGenerateRoads();

            Intersection[] intersections = roadSystem.GetComponentsInChildren<Intersection>();
            foreach (var intersection in intersections)
            {
                intersection.UpdateConnections(roadSystem);
            }

            roadSystem.UpdateAll();
        }

    }
}
#endif