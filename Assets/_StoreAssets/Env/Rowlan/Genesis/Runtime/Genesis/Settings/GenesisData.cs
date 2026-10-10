using JBooth.MicroVerseCore;
using System;
using System.Collections.Generic;
using UnityEngine;
using static JBooth.MicroVerseCore.FilterSet;

namespace Rowlan.Genesis
{
    [CreateAssetMenu(fileName = ProjectSetup.GenesisSettings_FileName, menuName = ProjectSetup.GenesisSettings_MenuName)]
    [Serializable]
    public class GenesisData : ScriptableObject
    {
        public enum HeightStampPlacement
        {
            Random,
            Grid
        }

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        /// <summary>
        /// User has to confirm this to continue, just as a precaution
        /// </summary>
        [Tooltip("This wizard is designed to recreate aspects of MicroVerse or even an entire MicroVerse. It will delete and replace your current settings! Please confirm to continue")]
        public bool replaceExisting = false;

        [Tooltip("Show confirmation popup before performing MicroVerse recreation")]
        public bool confirmationPopup = true;

        public TerrainSettings terrainSettings = new TerrainSettings();

        public HeightStampPlacement heightStampPlacement = HeightStampPlacement.Random;

        public HeightStampSettings[] heightStamps = new HeightStampSettings[0];

        public LayerSettings[] layerSettings = new LayerSettings[0];

#if __MICROVERSE_SPLINES__
        public PathSettings[] pathSettings = new PathSettings[0];
#endif

#if __MICROVERSE_VEGETATION__
        public TreeSettings treeSettings = new TreeSettings();
#endif

        [Serializable]
        public struct TerrainSettings
        {
            [Tooltip("Total Terrain Width")]
            public int totalWidth;
             
            [Tooltip("Total Terrain Length")]
            public int totalLength;

            [Tooltip("Terrain Height")]
            public int totalHeight;

            public int tilesX;
            public int tilesZ;

            public Vector3 startPosition;

            public int groupingID;
            public bool autoConnect;

            public int pixelError;
            public int baseMapDistance;

            public bool drawInstanced;

            public int heightmapResolution;
            public int controlTextureResolution;
            public int baseTextureResolution;

            public int detailResolution;
            public int detailResolutionPerPatch;

            public bool clearExisting;
            public string assetDirectory;
            public bool enableGuid;

            public void Reset()
            {
                totalWidth = 1000;
                totalLength = 1000;
                totalHeight = 600;

                tilesX = 1;
                tilesZ = 1;

                startPosition = Vector3.zero;

                groupingID = 0;
                autoConnect = true;

                pixelError = 1;
                baseMapDistance = 1000;

                drawInstanced = true;

                heightmapResolution = 2049;
                controlTextureResolution = 2048;
                baseTextureResolution = 2048;

                detailResolution = 1024;
                detailResolutionPerPatch = 32;

                clearExisting = true;
                assetDirectory = ProjectSetup.TERRAIN_DATA_DIRECTORY;
                enableGuid = true;


            }
        }

#if __MICROVERSE_VEGETATION__
        [Serializable]
        public struct TreeSettings
        {
            public TreeStamp[] treePlacement;

            public void Reset()
            {
                treePlacement = new TreeStamp[0];
            }
        }
#endif

        public void Reset()
        {
            helpBoxVisible = false;

            terrainSettings.Reset();

            heightStamps = new HeightStampSettings[0];

            layerSettings = new LayerSettings[0];

#if __MICROVERSE_SPLINES__
            pathSettings = new PathSettings[0];
#endif

#if __MICROVERSE_VEGETATION__
            treeSettings.Reset();
#endif
        }
    }
}