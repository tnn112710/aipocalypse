//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    public class CozySearchable : PropertyAttribute
    {
        public string[] keywords;
        public bool deepSearch;

        public CozySearchable(params string[] keywords)
        {

            this.keywords = keywords;
            this.deepSearch = false;

        }
        public CozySearchable(bool deepSearch, params string[] keywords)
        {

            this.keywords = keywords;
            this.deepSearch = deepSearch;

        }
    }
}