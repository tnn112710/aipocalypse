using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    [Serializable]
    public struct PathSettings
    {
        public enum Shape
        {
            Line,
            Rectangle,
            RoundedRectangle,
            Circle,
            Random
        }

        public bool active;

        public Shape shape;

        // currently hidden, closed is determined by the shape for now; line isn't closed, while circle is
        // will change later once we add custom points
        [HideInInspector]
        public bool closed;

        [Tooltip("The relative scale compared to the total terrain bounds")]
        [Range(0f, 1f)]
        public float scale;

        [MinMax(-20, 20, true)]
        public Vector2 deformationAngle;

        [Range(0,5)]
        public int subdivisionIterations;

        [Tooltip("The relative distance from one not to the other at which a new subdivision knot is placed. 0.5f means middle")]
        [MinMax(0.2f, 0.8f, true)]
        public Vector2 subdivisionRange;

        public TerrainLayer terrainLayer;

        public void Reset()
        {
            active = true;
            shape = Shape.Circle;
            closed = false;
            scale = 0.6f;
            deformationAngle = new Vector2(-20f, 20f);
            subdivisionIterations = 2;
            subdivisionRange = new Vector2(0.5f, 0.5f);
            terrainLayer = null;
        }

        public void Apply(PathSettingsTemplate template)
        {
            active = template.active;

            shape = template.shape;
            closed = template.closed;
            scale = template.scale;
            deformationAngle = template.deformationAngle;
            subdivisionIterations = template.subdivisionIterations;
            subdivisionRange = template.subdivisionRange;

        }
    }
}