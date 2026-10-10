using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    [Serializable]
    public struct LayerSettings
    {

        public bool active;

        public TerrainLayer terrainLayer;

        public bool slopeEnabled;

        [MinMax(0f, 1f, true)]
        public Vector2 slopeRange;

        public void Reset()
        {
            active = true;
            terrainLayer = null;

            slopeEnabled = false;
            slopeRange = new Vector2(0f, 1f);
        }

        public void Apply(LayerSettingsTemplate template)
        {
            active = template.active;

            slopeEnabled = template.slopeEnabled;
            slopeRange = template.slopeRange;
        }
    }
}