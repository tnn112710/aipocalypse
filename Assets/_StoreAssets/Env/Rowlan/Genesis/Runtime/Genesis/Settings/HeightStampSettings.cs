using JBooth.MicroVerseCore;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    [Serializable]
    public struct HeightStampSettings
    {
        public enum FalloffType
        {
            Range,
            Box,
        }

        public bool active;

        [Range(0f,1f)]
        public float weight;

        public Texture2D texture;

        public FalloffType falloffType;

        [MinMax(0f,1f,true)]
        public Vector2 positionRangeX;

        [MinMax(0f, 1f, true)]
        public Vector2 positionRangeY;

        [MinMax(0f, 1f, true)]
        public Vector2 positionRangeZ;

        [MinMax(0f, 360f, true)]
        public Vector2 rotationRange;

        [MinMax(0f, 1f, true)]
        public Vector2 scaleRangeWidth;

        [MinMax(0f, 1f, true)]
        public Vector2 scaleRangeHeight;

        public void Reset()
        {
            active = true;

            texture = null;

            weight = 1f;

            falloffType = FalloffType.Range;

            positionRangeX = new Vector2(0f, 1f);
            positionRangeY = new Vector2(0f, 0.2f);
            positionRangeZ = new Vector2(0f, 1f);

            rotationRange = new Vector2( 0f, 360f);

            scaleRangeWidth = new Vector2(0.5f, 0.1f);
            scaleRangeHeight = new Vector2(1f, 0.3f);

        }

        public void Apply( StampSettingsTemplate template)
        {
            // template data
            active = template.active;
            weight = template.weight;
            falloffType = template.falloffType;

            positionRangeX = template.positionRangeX;
            positionRangeY = template.positionRangeY;
            positionRangeZ = template.positionRangeZ;

            rotationRange = template.rotationRange;

            scaleRangeWidth = template.scaleRangeWidth;
            scaleRangeHeight = template.scaleRangeHeight;
        }
    }
}