using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis.Migration
{
    [Serializable]
    public class AnalyzerData : ScriptableObject
    {
        public string packageId;
        public List<AssetData> assetDataList;
    }
}