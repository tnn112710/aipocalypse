//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Holds parameters modified by the FX system and then sent to the GPU. 
    /// Not for any FX type explicitly tied to a module (e.g. ClimateFX or WindFX). These should be modified in the individual modules
    /// </summary>
    public struct FXParameters
    {
        public float cumulusCoverage;
        public float cirrusCoverage;
        public float cirrostratusCoverage;
        public float cumulonimbusCoverage;
        public float contrailsCoverage;
        public float altocumulusCoverage;
        public float borderCloudsCoverage;


        public float fogCoverage;
        public float fogHeightCoverage;

        public float filterSaturation;
        public float filterValue;
        public Color filterColor;
        public Color sunFilter;
        public Color cloudFilter;

        public FXParameters(bool empty = false)
        {
            if (empty)
            {
                cumulusCoverage = -1;
                cirrusCoverage = -1;
                cirrostratusCoverage = -1;
                cumulonimbusCoverage = -1;
                contrailsCoverage = -1;
                altocumulusCoverage = -1;
                borderCloudsCoverage = -1;
                fogCoverage = -1;
                fogHeightCoverage = -1;
                filterSaturation = -1;
                filterValue = -1;
                filterColor = Color.black;
                sunFilter = Color.black;
                cloudFilter = Color.black;
                return;
            }
            cumulusCoverage = 0;
            cirrusCoverage = 0;
            cirrostratusCoverage = 0;
            cumulonimbusCoverage = 0;
            contrailsCoverage = 0;
            altocumulusCoverage = 0;
            borderCloudsCoverage = 0;
            fogCoverage = 1;
            fogHeightCoverage = 1;
            filterSaturation = 0;
            filterValue = 0;
            filterColor = Color.white;
            sunFilter = Color.white;
            cloudFilter = Color.white;
        }

        public void Copy(FXParameters parameters)
        {
            cumulusCoverage = parameters.cumulusCoverage;
            cirrusCoverage = parameters.cirrusCoverage;
            cirrostratusCoverage = parameters.cirrostratusCoverage;
            cumulonimbusCoverage = parameters.cumulonimbusCoverage;
            contrailsCoverage = parameters.contrailsCoverage;
            altocumulusCoverage = parameters.altocumulusCoverage;
            borderCloudsCoverage = parameters.borderCloudsCoverage;
            fogCoverage = parameters.fogCoverage;
            fogHeightCoverage = parameters.fogHeightCoverage;
            filterSaturation = parameters.filterSaturation;
            filterValue = parameters.filterValue;
            filterColor = parameters.filterColor;
            sunFilter = parameters.sunFilter;
            cloudFilter = parameters.cloudFilter;
        }

        public void Reset()
        {
            cumulusCoverage = 0;
            cirrusCoverage = 0;
            cirrostratusCoverage = 0;
            cumulonimbusCoverage = 0;
            contrailsCoverage = 0;
            altocumulusCoverage = 0;
            borderCloudsCoverage = 0;
            fogCoverage = 1;
            fogHeightCoverage = 1;
            filterSaturation = 0;
            filterValue = 0;
            filterColor = Color.white;
            sunFilter = Color.white;
            cloudFilter = Color.white;
        }
    }
}