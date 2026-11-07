
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Resound DJ", order = 361)]
    public class CozyResoundDJ : ScriptableObject
    {
        public List<ResoundTrack> tracks;

        public ResoundTrack GetRandomTrack()
        {
            if (tracks.Count == 0)
            {
                Debug.LogError("No tracks in the current DJ");
                return null;
            }

            return tracks[Random.Range(0, tracks.Count - 1)];
        }
    }
}