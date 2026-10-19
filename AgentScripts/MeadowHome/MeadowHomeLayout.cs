// Meadow Home terrain layout generator (run via `unity command run_script`, editor only, outside Assets/).
// Layout traced from docs/concepts/meadow-home-top-down-v13.png on a 1000 x 1000 m terrain (x east, z north, metres).
// Writes the macro heightmap + region masks and builds the MicroVerse height stack that uses them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JBooth.MicroVerseCore;
using StylizedWater3;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.VFX;
using RM = StylizedWater3.RiverModeler;

public static class MeadowHomeLayout
{
    public const string DataFolder = "Assets/_Game/Data/Environment/MeadowHome";
    public const string MapFolder = DataFolder + "/Heightmaps";
    public const float Size = 1000f;
    public const float MaxHeight = 300f;

    // ---------------- Layout data ----------------

    // Foot of the boundary mountains (inside = playable).
    public static readonly Vector2[] Playable = SmoothClosed(new[]
    {
        V(238,240), V(300,236), V(360,234), V(420,236), V(480,232), V(520,226), V(560,214), V(620,200), V(700,192), V(780,190),
        V(835,200), V(852,240), V(854,330), V(852,430), V(848,520), V(832,556), V(808,572), V(798,620), V(796,700), V(800,780),
        V(792,832), V(740,846), V(660,850), V(580,852), V(500,852), V(420,854), V(340,854), V(260,852), V(200,848), V(162,826),
        V(154,760), V(156,690), V(158,640), V(162,600), V(170,560), V(176,520), V(182,486), V(184,456), V(186,430), V(194,404),
        V(212,384), V(226,362), V(236,334), V(238,300),
    }, 4);

    public static readonly Vector2[] HighlandsArea = SmoothClosed(new[]
    {
        V(140,600), V(230,590), V(300,600), V(360,615), V(420,635), V(470,655), V(505,690), V(515,750), V(500,800), V(520,870), V(140,870),
    }, 4);

    public static readonly Vector2[] MeadowArea = SmoothClosed(new[]
    {
        V(236,236), V(520,228), V(545,250), V(530,268), V(505,276), V(470,286), V(430,298), V(390,312), V(350,330), V(310,350),
        V(275,368), V(245,385), V(236,330),
    }, 4);

    public static readonly Vector2[] SwampArea = SmoothClosed(new[]
    {
        V(528,752), V(533,790), V(600,803), V(680,795), V(748,770), V(784,725), V(790,668), V(772,628), V(735,612), V(690,616),
        V(650,626), V(605,642), V(568,668), V(540,700),
    }, 4);

    public const float SwampWater = 24.5f, SwampGround = 24.85f;
    public static readonly Vector2 HomeBase = V(326, 274);
    public const float HomeBaseHeight = 28.5f;
    public static readonly Vector2 Bridge = V(312, 362);
    public static readonly Vector2 Viewpoint = V(540, 242);

    public class Basin
    {
        public string name; public Vector2[] shore; public float level, depth, deepDistance, bankSlope; public Rect bounds;
    }

    public static readonly Basin[] Basins =
    {
        new Basin { name = "GreatLake", level = 19.5f, depth = 9.5f, deepDistance = 70f, bankSlope = 0.2f, shore = SmoothClosed(new[]
        {
            V(510,284), V(544,298), V(599,316), V(637,344), V(670,362), V(703,370), V(730,403), V(758,438), V(780,473), V(790,508),
            V(797,536), V(812,544), V(830,540), V(840,520), V(843,450), V(843,350), V(840,262), V(826,226), V(790,212), V(740,212),
            V(690,220), V(654,226), V(600,240), V(556,252), V(530,258), V(512,268),
        }, 4) },
        new Basin { name = "WaterfallPond", level = 23.0f, depth = 3.5f, deepDistance = 10f, bankSlope = 0.3f, shore = Ellipse(V(219,428), 22, 15, -35, 14, 11) },
        new Basin { name = "WestPond", level = 50.0f, depth = 2.5f, deepDistance = 8f, bankSlope = 0.35f, shore = Ellipse(V(192,640), 17, 14, 20, 12, 21) },
        new Basin { name = "NorthPond", level = 44.0f, depth = 2.5f, deepDistance = 8f, bankSlope = 0.35f, shore = Ellipse(V(386,806), 33, 12, 12, 16, 31) },
    };

    // Water lines: (x, z, surface height, half width). depth = deepest point below the surface.
    public class Line
    {
        public string name; public Vector4[] pts; public float depth, bankSlope; public bool fixedSlope; public Rect bounds;
    }

    public static readonly Line[] Lines =
    {
        new Line { name = "River", depth = 2.8f, bankSlope = 0.3f, pts = Smooth(new[]
        {
            P(232,412,23.0f,6), P(240,406,22.95f,6), P(266,385,22.7f,6.5f), P(290,371,22.45f,6.5f), P(312,362,22.2f,6.5f),
            P(348,339,21.85f,7), P(386,321,21.45f,7), P(430,303,21.0f,7.5f), P(468,289,20.5f,8), P(490,284,20.1f,8.5f),
            P(512,282,19.6f,9), P(535,292,19.5f,10),
        }, 6, 3.0f, 5) },
        new Line { name = "WestStream", depth = 0.9f, bankSlope = 0.45f, pts = Smooth(new[]
        {
            P(198,628,50.0f,1.6f), P(201,621,49.75f,1.6f), P(205,611,45.5f,1.7f), P(209,600,41.0f,1.8f), P(218,584,36.0f,1.8f), P(232,570,33.2f,2.0f),
            P(277,561,32.0f,2.2f), P(331,571,30.8f,2.4f), P(375,590,29.6f,2.4f), P(414,612,28.5f,2.5f), P(446,630,27.5f,2.5f),
            P(496,650,25.8f,2.6f), P(528,656,24.8f,2.6f), P(566,657,24.0f,2.8f),
        }, 6, 1.5f, 10) },
        new Line { name = "ForestStream", depth = 0.8f, bankSlope = 0.45f, pts = Smooth(new[]
        {
            P(331,571,30.8f,2.0f), P(352,550,30.0f,2.0f), P(375,531,29.2f,2.1f), P(424,508,27.8f,2.2f), P(468,490,26.6f,2.3f),
            P(517,470,25.4f,2.4f), P(555,444,24.2f,2.5f), P(599,420,22.9f,2.6f), P(637,397,21.6f,2.8f), P(659,373,20.3f,3.0f),
            P(672,356,19.5f,3.5f),
        }, 6, 1.5f, 12) },
        new Line { name = "NorthStream", depth = 0.7f, bankSlope = 0.45f, pts = Smooth(new[]
        {
            P(405,800,44.0f,1.5f), P(419,795,43.6f,1.6f), P(440,786,41.0f,1.6f), P(460,778,36.5f,1.8f), P(480,772,31.5f,1.9f),
            P(500,769,27.0f,2.0f), P(518,768,25.0f,2.2f), P(536,770,24.5f,2.4f),
        }, 6, 1.2f, 11) },
        new Line { name = "SwampOutlet", depth = 0.8f, bankSlope = 0.4f, pts = Smooth(new[]
        {
            P(556,690,24.5f,2.0f), P(562,672,24.25f,2.2f), P(566,657,24.0f,2.5f),
        }, 6, 0.0f, 11) },
        new Line { name = "MarginStream", depth = 1.0f, bankSlope = 0.4f, pts = Smooth(new[]
        {
            P(566,657,24.0f,2.8f), P(610,626,23.5f,3.0f), P(654,598,22.9f,3.0f), P(681,568,22.2f,3.2f), P(719,547,21.3f,3.2f),
            P(769,541,20.3f,3.5f), P(800,541,19.5f,4.0f),
        }, 6, 1.5f, 12) },
    };


    // Regional elevation anchors (x, z, height, radius) blended with Gaussian weights.
    static readonly Vector4[] Anchors =
    {
        // meadow
        A(262,300,26,40), A(300,330,25,35), A(326,274,28.5f,35), A(370,260,28,40), A(420,250,29,45), A(470,250,27.5f,40),
        A(500,262,25.5f,35), A(380,300,25.5f,35), A(440,285,25,35), A(330,245,31,30),
        // lake south strip
        A(530,240,26,30), A(600,215,25,30), A(700,205,25,30), A(790,205,26,30),
        // forest by the river and lake
        A(280,395,27,40), A(340,380,27.5f,40), A(400,355,27,45), A(460,330,25.5f,40), A(520,315,24,35), A(570,330,24,40),
        A(630,375,24.5f,40), A(690,395,25,35), A(735,440,25,35), A(770,500,25,35),
        // forest interior
        A(330,460,34,60), A(420,450,36,60), A(510,430,32,55), A(590,400,28,50), A(650,450,30,55), A(710,480,29,50),
        A(760,550,28,40), A(360,520,33,50), A(450,540,32,55), A(540,530,34,55), A(630,540,31,50), A(700,560,27,40),
        A(520,600,29,45), A(600,600,27,45), A(480,600,31,45), A(420,575,32,40), A(300,520,33,45), A(260,470,35,45),
        A(240,420,33,35), A(280,560,36,40), A(720,610,27,35), A(770,600,26,35),
        // highlands
        A(210,600,46,40), A(250,610,44,40), A(300,615,42,45), A(360,625,40,45), A(420,640,38,45), A(470,665,35,40),
        A(500,740,34,40), A(480,800,40,45), A(430,770,50,50), A(360,760,58,60), A(290,760,64,60), A(220,760,70,55),
        A(190,690,60,45), A(250,690,58,55), A(330,690,54,55), A(400,700,50,50), A(200,820,78,45), A(290,825,72,50),
        A(386,806,50,40), A(440,830,52,40), A(186,640,54,35),
        // north strip behind the swamp
        A(520,830,40,40), A(600,830,30,35), A(700,825,30,35), A(770,810,32,30),
    };

    // Extra knolls (x, z, height, radius) added on top of the regional field.
    static readonly Vector4[] Knolls =
    {
        A(410,470,9,32), A(642,526,8,26), A(669,462,7,24), A(519,443,7,28), A(753,583,8,26), A(335,520,6,28), A(590,560,7,30),
        A(470,420,6,30), A(720,500,5,25), A(300,440,5,30), A(420,262,2.5f,30), A(470,255,2,25), A(280,300,2,25), A(540,240,4,18),
    };

    // Lake islands (x, z, top above water, radius).
    static readonly Vector4[] Islands = { A(675,270,2.4f,12), A(726,247,1.8f,9), A(763,394,2.2f,11), A(808,428,1.6f,8) };

    // Rowlan detail stamps (Add, Y = 0, masked). The shader clamps stamp heights at 0, so Add can only raise:
    // Generate() lowers the macro by median * amplitude * mask instead, which makes each stamp zero-mean.
    public class DetailStamp { public string name, texture, mask; public Vector2 center; public float size, amplitude, median; }
    public static readonly DetailStamp[] Details =
    {
        new DetailStamp { name = "Highlands 09", texture = "Highlands/Heightmaps/Highlands 09.png", mask = "Highlands", center = V(315,730), size = 560, amplitude = 26, median = 0.64f },
        new DetailStamp { name = "Highlands 07", texture = "Highlands/Heightmaps/Highlands 07.png", mask = "Highlands", center = V(260,700), size = 300, amplitude = 10, median = 0.68f },
        new DetailStamp { name = "Forest Meadows 05", texture = "Meadows/Heightmaps/Meadows 05.png", mask = "Forest", center = V(540,470), size = 640, amplitude = 9, median = 0.45f },
        new DetailStamp { name = "Forest Meadows 16", texture = "Meadows/Heightmaps/Meadows 16.png", mask = "Forest", center = V(430,500), size = 300, amplitude = 4, median = 0.39f },
        new DetailStamp { name = "Meadow Meadows 26", texture = "Meadows/Heightmaps/Meadows 26.png", mask = "Meadow", center = V(390,285), size = 330, amplitude = 3.5f, median = 0.43f },
        new DetailStamp { name = "Swamp 12", texture = "Swamp/Heightmaps/Swamp 12.png", mask = "Swamp", center = V(660,705), size = 340, amplitude = 3.5f, median = 0.40f },
    };

    static float MaskValue(DetailStamp d, float x, float z)
    {
        float fade = SquareFade(d, x, z);
        if (fade <= 0) return 0;
        return RegionWeight(d, RegionWeights(x, z), x, z) * fade;
    }

    // Fade to zero over the outer 15% of the stamp footprint so a stamp never ends in a step.
    static float SquareFade(DetailStamp d, float x, float z)
    {
        float half = d.size * 0.5f;
        float edge = Mathf.Min(half - Mathf.Abs(x - d.center.x), half - Mathf.Abs(z - d.center.y));
        return edge <= 0 ? 0 : SmoothStep(0, 0.15f * d.size, edge);
    }

    static float RegionWeight(DetailStamp d, Vector3 w, float x, float z) =>
        d.mask == "Swamp" ? SwampMask(x, z) : d.mask == "Highlands" ? w.x : d.mask == "Forest" ? w.y : w.z;

    public static string MaskName(DetailStamp d) => "MH_Mask_" + d.name.Replace(" ", "");

    static float DetailOffset(float x, float z)
    {
        float off = 0; Vector3? w = null;
        foreach (var d in Details)
        {
            float fade = SquareFade(d, x, z);
            if (fade <= 0) continue;
            w ??= RegionWeights(x, z);
            off += d.median * d.amplitude * RegionWeight(d, w.Value, x, z) * fade;
        }
        return off;
    }

    // ---------------- Entry points ----------------

    // Generates the macro heightmap (res x res, R16) and region masks (maskRes, R16) and a colour preview.
    public static async Task<object> Generate(int res, int maskRes, string previewPath)
    {
        var t0 = DateTime.Now;
        EnsureFolder(MapFolder);
        var heights = new float[res * res];
        var baseMap = new float[res * res];
        var water = new float[res * res];
        await Task.Run(() => Parallel.For(0, res, j =>
        {
            for (int i = 0; i < res; i++)
            {
                float x = (i + 0.5f) / res * Size, z = (j + 0.5f) / res * Size;
                int k = j * res + i;
                heights[k] = HeightAt(x, z, out water[k]);
                baseMap[k] = Mathf.Max(0, heights[k] - DetailOffset(x, z));
            }
        }));
        WriteR16(MapFolder + "/MH_Base.png", baseMap, res, MaxHeight);

        var masks = Details.ToDictionary(d => MaskName(d), d => new float[maskRes * maskRes]);
        await Task.Run(() => Parallel.For(0, maskRes, j =>
        {
            for (int i = 0; i < maskRes; i++)
            {
                float x = (i + 0.5f) / maskRes * Size, z = (j + 0.5f) / maskRes * Size;
                Vector3? w = null;
                foreach (var d in Details)
                {
                    float fade = SquareFade(d, x, z);
                    if (fade <= 0) continue;
                    w ??= RegionWeights(x, z);
                    masks[MaskName(d)][j * maskRes + i] = RegionWeight(d, w.Value, x, z) * fade;
                }
            }
        }));
        foreach (var kv in masks) WriteR16(MapFolder + "/" + kv.Key + ".png", kv.Value, maskRes, 1f);

        AssetDatabase.Refresh();
        foreach (var name in new[] { "MH_Base" }.Concat(masks.Keys))
            ConfigureImporter(MapFolder + "/" + name + ".png", name == "MH_Base" ? res : maskRes);
        // region masks from the first layout version (one mask per region) are no longer used
        foreach (var stale in new[] { "Rim", "Highlands", "Forest", "Meadow", "Swamp" })
            AssetDatabase.DeleteAsset(MapFolder + "/MH_Mask_" + stale + ".png");

        if (!string.IsNullOrEmpty(previewPath)) WritePreview(previewPath, heights, water, res);
        float min = heights.Min(), max = heights.Max();
        return new { seconds = (DateTime.Now - t0).TotalSeconds, min, max, files = Directory.GetFiles(MapFolder, "*.png").Select(Path.GetFileName).ToArray() };
    }

    // Water surface heights for the water pass: basins and line knots.
    public static object WaterData()
    {
        return new
        {
            basins = Basins.Select(b => new { b.name, b.level, center = Centroid(b.shore) }).ToArray(),
            swamp = new { level = SwampWater, center = Centroid(SwampArea) },
            lines = Lines.Select(l => new { l.name, knots = l.pts.Select(p => $"{p.x:0},{p.y:0},{p.z:0.00},{p.w:0.0}").ToArray() }).ToArray()
        };
    }

    // ---------------- Verification (needs the terrain CPU heights synced: kit Rebuild) ----------------

    // Checks the built terrain against the layout intent and writes a JSON report (plus CSV samples) to outDir.
    public static async Task<object> Verify(string outDir)
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) throw new InvalidOperationException("No active terrain.");
        var td = terrain.terrainData;
        int res = td.heightmapResolution;
        var raw = td.GetHeights(0, 0, res, res);           // [z, x], normalised
        float sy = td.size.y, cell = td.size.x / (res - 1);
        float H(float x, float z)
        {
            float fx = Mathf.Clamp(x / cell, 0, res - 1.001f), fz = Mathf.Clamp(z / cell, 0, res - 1.001f);
            int ix = (int)fx, iz = (int)fz; float tx = fx - ix, tz = fz - iz;
            float a = Mathf.Lerp(raw[iz, ix], raw[iz, ix + 1], tx), b = Mathf.Lerp(raw[iz + 1, ix], raw[iz + 1, ix + 1], tx);
            return Mathf.Lerp(a, b, tz) * sy;
        }
        float Slope(float x, float z)
        {
            float dx = (H(x + 1, z) - H(x - 1, z)) / 2f, dz = (H(x, z + 1) - H(x, z - 1)) / 2f;
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
        }
        var report = new Dictionary<string, object>();
        var lines = new List<string>();

        // basins: depth, shoreline, leaks (flood fill at the water level from the basin interior, 1 m grid)
        var basinReports = new List<object>();
        foreach (var b in Basins.Select(x => (name: x.name, shore: x.shore, level: x.level, depth: x.depth)).Concat(new[] { (name: "Swamp", shore: SwampArea, level: SwampWater, depth: 2.5f) }))
        {
            var bb = Bounds(b.shore.Select(v => new Vector4(v.x, v.y, 0, 0)), 80);
            int x0 = Mathf.Max(1, (int)bb.xMin), x1 = Mathf.Min(998, (int)bb.xMax), z0 = Mathf.Max(1, (int)bb.yMin), z1 = Mathf.Min(998, (int)bb.yMax);
            int w = x1 - x0 + 1, hgt = z1 - z0 + 1;
            float minBed = float.MaxValue, sumDepth = 0; int inside = 0, wet = 0;
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    if (SignedDistance(new Vector2(x, z), b.shore) > -2) continue;
                    float h = H(x, z); inside++;
                    if (h < b.level) { wet++; sumDepth += b.level - h; }
                    minBed = Mathf.Min(minBed, h);
                }
            // rim check: ground in a band just outside the (noisy) shore must stay above the level, except at inlets/outlets
            float bandIn = b.name == "Swamp" ? 9 : 5, bandOut = bandIn + 8;
            int leak = 0, ringCells = 0; float ringMin = float.MaxValue; var leakPts = new List<string>();
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x, z);
                    float sd = SignedDistance(p, b.shore);
                    if (sd < bandIn || sd > bandOut) continue;
                    if (Lines.Any(l => LineEdge(p, l) < 6f) || Basins.Any(o => o.name != b.name && SignedDistance(p, o.shore) < 6f)) continue;
                    ringCells++;
                    float rel = H(x, z) - b.level;
                    ringMin = Mathf.Min(ringMin, rel);
                    if (rel < 0.05f) { leak++; if (leakPts.Count < 10) leakPts.Add($"({x},{z}) {rel:0.00}"); }
                }
            // shoreline: terrain relative to level on the shore polygon (should hover around 0)
            var shoreRel = b.shore.Select(v => H(v.x, v.y) - b.level).ToArray();
            basinReports.Add(new
            {
                b.name, b.level, designDepth = b.depth,
                maxDepth = Round(b.level - minBed), meanDepth = Round(wet > 0 ? sumDepth / wet : 0),
                wetFractionInside = Round(inside > 0 ? wet / (float)inside : 0),
                shoreMin = Round(shoreRel.Min()), shoreMax = Round(shoreRel.Max()), shoreMean = Round(shoreRel.Average()),
                ringCells, ringMinAboveLevel = Round(ringMin), leakCells = leak, leakSamples = leakPts,
            });
        }
        report["basins"] = basinReports;

        // water lines: cross sections every ~20 m
        var lineReports = new List<object>();
        foreach (var l in Lines)
        {
            var stations = new List<object>();
            float spillWorst = float.MaxValue, depthMin = float.MaxValue, depthMax = 0, bankSlopeMax = 0;
            float acc = 0;
            for (int i = 1; i < l.pts.Length - 1; i++)
            {
                acc += Vector2.Distance(new Vector2(l.pts[i].x, l.pts[i].y), new Vector2(l.pts[i - 1].x, l.pts[i - 1].y));
                if (acc < 20) continue;
                acc = 0;
                var c = new Vector2(l.pts[i].x, l.pts[i].y);
                var tan = (new Vector2(l.pts[i + 1].x, l.pts[i + 1].y) - new Vector2(l.pts[i - 1].x, l.pts[i - 1].y)).normalized;
                var n = new Vector2(-tan.y, tan.x);
                float s = l.pts[i].z, hw = l.pts[i].w;
                if (Basins.Any(bs => SignedDistance(c, bs.shore) < 0)) continue;          // inside a basin
                float depth = s - H(c.x, c.y);
                float Side(float sign, float off) { var q = c + n * sign * (hw + off); return H(q.x, q.y) - s; }
                float l2 = Side(1, 2), r2 = Side(-1, 2), l8 = Side(1, 8), r8 = Side(-1, 8), l20 = Side(1, 20), r20 = Side(-1, 20);
                float slopeL = Mathf.Atan(Mathf.Max(0, l8 - l2) / 6f) * Mathf.Rad2Deg, slopeR = Mathf.Atan(Mathf.Max(0, r8 - r2) / 6f) * Mathf.Rad2Deg;
                spillWorst = Mathf.Min(spillWorst, Mathf.Min(l2, r2));
                depthMin = Mathf.Min(depthMin, depth); depthMax = Mathf.Max(depthMax, depth);
                bankSlopeMax = Mathf.Max(bankSlopeMax, Mathf.Max(slopeL, slopeR));
                stations.Add($"({c.x:0},{c.y:0}) s={s:0.00} depth={depth:0.00} bank+2m L/R={l2:0.0}/{r2:0.0} +8m={l8:0.0}/{r8:0.0} +20m={l20:0.0}/{r20:0.0} slope={slopeL:0}/{slopeR:0}deg");
            }
            lineReports.Add(new { l.name, designDepth = l.depth, depthMin = Round(depthMin), depthMax = Round(depthMax), lowestBankAt2m = Round(spillWorst), bankSlopeMaxDeg = Round(bankSlopeMax), stations });
        }
        report["lines"] = lineReports;

        // regions: elevation and slope stats on a 4 m grid inside the playable area
        var regionStats = new Dictionary<string, List<(float h, float s)>>
        { { "Meadow", new() }, { "Forest", new() }, { "Highlands", new() }, { "Swamp", new() }, { "PlayableAll", new() }, { "RimBand", new() } };
        var drift = new List<float>();
        for (int z = 2; z < 1000; z += 4)
            for (int x = 2; x < 1000; x += 4)
            {
                var p = new Vector2(x, z);
                float h = H(x, z), sl = Slope(x, z);
                float dPlay = SignedDistance(p, Playable);
                if (dPlay > 0) { if (dPlay > 30) regionStats["RimBand"].Add((h, sl)); continue; }
                bool wetHere = Basins.Any(bs => SignedDistance(p, bs.shore) < 0) || Lines.Any(li => LineEdge(p, li) < 0);
                if (wetHere) { drift.Add(Mathf.Abs(h - HeightAt(x, z, out _))); continue; }
                regionStats["PlayableAll"].Add((h, sl));
                if (SignedDistance(p, SwampArea) < 0) regionStats["Swamp"].Add((h, sl));
                else if (SignedDistance(p, MeadowArea) < 0) regionStats["Meadow"].Add((h, sl));
                else if (SignedDistance(p, HighlandsArea) < 0) regionStats["Highlands"].Add((h, sl));
                else regionStats["Forest"].Add((h, sl));
            }
        report["regions"] = regionStats.ToDictionary(kv => kv.Key, kv =>
        {
            var hs = kv.Value.Select(v => v.h).OrderBy(v => v).ToArray();
            var ss = kv.Value.Select(v => v.s).OrderBy(v => v).ToArray();
            if (hs.Length == 0) return (object)"empty";
            float Q(float[] a, float q) => a[Mathf.Clamp((int)(q * a.Length), 0, a.Length - 1)];
            return new
            {
                samples = hs.Length, hMin = Round(hs[0]), hP10 = Round(Q(hs, .1f)), hP50 = Round(Q(hs, .5f)), hP90 = Round(Q(hs, .9f)), hMax = Round(hs[^1]),
                slopeP50 = Round(Q(ss, .5f)), slopeP90 = Round(Q(ss, .9f)), slopeP99 = Round(Q(ss, .99f)),
                over35deg = Round(ss.Count(v => v > 35) / (float)ss.Length), over45deg = Round(ss.Count(v => v > 45) / (float)ss.Length),
            };
        });
        report["macroDriftInWater"] = drift.Count == 0 ? "none" : (object)new { max = Round(drift.Max()), p99 = Round(drift.OrderBy(v => v).ElementAt((int)(drift.Count * 0.99f))) };

        // home pad and bridge
        float padMin = float.MaxValue, padMax = 0;
        for (float z = HomeBase.y - 17; z <= HomeBase.y + 17; z += 1) for (float x = HomeBase.x - 17; x <= HomeBase.x + 17; x += 1) { float h = H(x, z); padMin = Mathf.Min(padMin, h); padMax = Mathf.Max(padMax, h); }
        report["homePad"] = new { center = Round(H(HomeBase.x, HomeBase.y)), min34 = Round(padMin), max34 = Round(padMax), distanceToBridge = Round(Vector2.Distance(HomeBase, Bridge)) };
        report["bridge"] = new { riverSurface = 22.2f, banks = Enumerable.Range(-24, 13).Select(o => $"{o * 2}m:{H(Bridge.x + o * 2 * 0.55f, Bridge.y + o * 2 * 0.83f):0.0}").ToArray() };

        // lightweight CSV height grid (5 m) for offline review
        await Task.Yield();
        if (!string.IsNullOrEmpty(outDir))
        {
            Directory.CreateDirectory(outDir);
            var sb = new System.Text.StringBuilder();
            for (int z = 995; z >= 0; z -= 5) { sb.AppendLine(string.Join(",", Enumerable.Range(0, 200).Select(i => H(i * 5 + 2.5f, z + 2.5f).ToString("0.0")))); }
            File.WriteAllText(Path.Combine(outDir, "heights_5m_northUp.csv"), sb.ToString());
        }
        return report;
    }

    // How much the MicroVerse detail stamps changed the macro heights, per region (final - macro).
    public static object Delta()
    {
        var t = Terrain.activeTerrain;
        var regions = new Dictionary<string, List<float>> { { "Meadow", new() }, { "Forest", new() }, { "Highlands", new() }, { "Swamp", new() }, { "Outside", new() } };
        for (int z = 3; z < 1000; z += 6)
            for (int x = 3; x < 1000; x += 6)
            {
                var p = new Vector2(x, z);
                float d = t.SampleHeight(new Vector3(x, 0, z)) - HeightAt(x, z, out _);
                if (SignedDistance(p, Playable) > 30) regions["Outside"].Add(d);
                else if (SignedDistance(p, SwampArea) < -10) regions["Swamp"].Add(d);
                else if (SignedDistance(p, MeadowArea) < -10) regions["Meadow"].Add(d);
                else if (SignedDistance(p, HighlandsArea) < -10) regions["Highlands"].Add(d);
                else if (SignedDistance(p, Playable) < -10) regions["Forest"].Add(d);
            }
        return regions.ToDictionary(kv => kv.Key, kv =>
        {
            var a = kv.Value.OrderBy(v => v).ToArray();
            float Q(float q) => a[Mathf.Clamp((int)(q * a.Length), 0, a.Length - 1)];
            return $"n={a.Length} min={a[0]:0.00} p10={Q(.1f):0.00} p50={Q(.5f):0.00} p90={Q(.9f):0.00} max={a[^1]:0.00}";
        });
    }

    // Exaggerated hillshade of the synced terrain heights (to spot creases); writes a PNG, north up.
    public static object Hillshade(float x0, float z0, float size, int res, float exaggeration, string outPng, bool macro)
    {
        var t = Terrain.activeTerrain;
        var h = new float[res, res];
        float step = size / res;
        for (int j = 0; j < res; j++)
            for (int i = 0; i < res; i++)
                h[j, i] = macro ? HeightAt(x0 + i * step, z0 + j * step, out _) - DetailOffset(x0 + i * step, z0 + j * step) : t.SampleHeight(new Vector3(x0 + i * step, 0, z0 + j * step));
        var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
        var light = new Vector3(-0.6f, 0.5f, 0.6f).normalized;
        for (int j = 1; j < res - 1; j++)
            for (int i = 1; i < res - 1; i++)
            {
                float dx = (h[j, i + 1] - h[j, i - 1]) / (2 * step) * exaggeration, dz = (h[j + 1, i] - h[j - 1, i]) / (2 * step) * exaggeration;
                float shade = Mathf.Clamp01(Vector3.Dot(new Vector3(-dx, 1, -dz).normalized, light));
                tex.SetPixel(i, j, new Color(shade, shade, shade));
            }
        File.WriteAllBytes(outPng, tex.EncodeToPNG());
        return outPng;
    }

    static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    // Distance from p to a line's water edge (negative inside the channel).
    static float LineEdge(Vector2 p, Line l)
    {
        float best = float.MaxValue;
        for (int i = 0; i < l.pts.Length - 1; i++)
        {
            Vector4 a = l.pts[i], b = l.pts[i + 1];
            var ab = new Vector2(b.x - a.x, b.y - a.y);
            float t = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.y), ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            best = Mathf.Min(best, Vector2.Distance(p, new Vector2(a.x, a.y) + ab * t) - Mathf.Lerp(a.w, b.w, t));
        }
        return best;
    }

    // LineEdge with the carve's noisy half-width (see the channel carve), so masks and dressing follow the real waterline.
    // The height field keeps the plain LineEdge: changing it would move the generated terrain.
    public static float TrueLineEdge(Vector2 p, Line l)
    {
        float best = float.MaxValue;
        float noise = l.fixedSlope ? 1f : 1f + 0.15f * Fbm(p.x / 25f, p.y / 25f + 9f, 2);
        for (int i = 0; i < l.pts.Length - 1; i++)
        {
            Vector4 a = l.pts[i], b = l.pts[i + 1];
            var ab = new Vector2(b.x - a.x, b.y - a.y);
            float t = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.y), ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            best = Mathf.Min(best, Vector2.Distance(p, new Vector2(a.x, a.y) + ab * t) - Mathf.Lerp(a.w, b.w, t) * noise);
        }
        return best;
    }

    // ---------------- Height field ----------------

    public static float HeightAt(float x, float z, out float waterSurface)
    {
        var p = new Vector2(x, z);
        float dPlay = SignedDistance(p, Playable);                 // > 0 outside the playable area
        float wHigh = Weight(SignedDistance(p, HighlandsArea), 30);
        float wMeadow = Weight(SignedDistance(p, MeadowArea), 20);
        float wSwamp = Weight(SignedDistance(p, SwampArea) + 6f * Fbm(x / 30f, z / 30f + 3f, 3), 18);

        // regional field
        float sw = 1e-6f, sh = 30f * 1e-6f;
        foreach (var a in Anchors)
        {
            float dx = x - a.x, dz = z - a.y;
            float w = Mathf.Exp(-(dx * dx + dz * dz) / (a.w * a.w));
            sw += w; sh += w * a.z;
        }
        float h = sh / sw;
        foreach (var k in Knolls)
        {
            float dx = x - k.x, dz = z - k.y;
            h += k.z * Mathf.Exp(-(dx * dx + dz * dz) / (k.w * k.w));
        }

        // rolling ground: gentle fbm everywhere, ridged in the highlands
        float wx = x + 40f * Fbm(x / 260f + 11.3f, z / 260f - 4.1f, 3), wz = z + 40f * Fbm(x / 260f - 7.7f, z / 260f + 2.9f, 3);
        float roll = Fbm(wx / 150f, wz / 150f, 5);
        float ridge = 1f - Mathf.Abs(Fbm(wx / 110f + 3.1f, wz / 110f + 8.2f, 4));
        float wForest = Mathf.Clamp01(1 - wHigh - wMeadow - wSwamp);
        h += roll * (1.2f * wMeadow + 3.5f * wForest + 2.0f * wHigh) + (ridge - 0.55f) * 10f * wHigh;

        // swamp floor
        // swamp floor: pools (up to 2.6 m under the water level) where the noise is positive, low islands where negative
        float pool = Fbm(x / 55f + 2.2f, z / 55f, 3);
        float swampFloor = SwampGround - 2.6f * SmoothStep(0.0f, 0.3f, pool) + 0.7f * SmoothStep(0.05f, 0.35f, -pool) + 0.3f * Fbm(x / 13f, z / 13f + 5f, 2);
        // no swamp pools beside a lower stream: the swamp water would sit above that stream's surface
        float dLow = float.MaxValue;
        foreach (var l in Lines)
            for (int i = 0; i < l.pts.Length - 1; i++)
            {
                Vector4 a = l.pts[i], b = l.pts[i + 1];
                if (Mathf.Min(a.z, b.z) >= SwampWater - 0.2f) continue;
                var ab = new Vector2(b.x - a.x, b.y - a.y);
                float tt = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.y), ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                if (Mathf.Lerp(a.z, b.z, tt) >= SwampWater - 0.2f) continue;
                dLow = Mathf.Min(dLow, Vector2.Distance(p, new Vector2(a.x, a.y) + ab * tt) - Mathf.Lerp(a.w, b.w, tt));
            }
        swampFloor = Mathf.Lerp(Mathf.Max(swampFloor, SwampWater + 0.35f), swampFloor, SmoothStep(5, 16, dLow));
        h = Mathf.Lerp(h, swampFloor, wSwamp);

        // water: levees first, then caps (beds + banks)
        float levee = float.NegativeInfinity, cap = float.PositiveInfinity;
        waterSurface = float.NegativeInfinity;
        foreach (var b in Basins) WaterBasin(p, b, ref levee, ref cap, ref waterSurface);
        foreach (var l in Lines) WaterLine(p, l, ref levee, ref cap, ref waterSurface);
        if (wSwamp > 0.5f) waterSurface = Mathf.Max(waterSurface, SwampWater);
        h = Mathf.Max(h, levee);
        // smooth-min softens the bank tops, but it can dip 0.3 m below both inputs: clamp to the carve so banks
        // never sink under the water surface (that would leave a dry strip beside the river ribbon)
        if (!float.IsPositiveInfinity(cap)) h = Mathf.Max(SmoothMin(h, cap, 1.2f), Mathf.Min(h, cap) - 0.03f);

        // lake islands
        foreach (var isl in Islands)
        {
            float d = Vector2.Distance(p, new Vector2(isl.x, isl.y));
            float n = 1f + 0.25f * Fbm(x / 9f, z / 9f, 2);
            float top = 19.5f + isl.z - 9f * Mathf.Pow(Mathf.Max(0, d * n / isl.w - 0.35f), 1.6f);
            h = Mathf.Max(h, top);
        }

        return Mathf.Clamp(h, 0, MaxHeight - 1);
    }

    static void WaterBasin(Vector2 p, Basin b, ref float levee, ref float cap, ref float surface)
    {
        if (b.bounds.width == 0) b.bounds = Bounds(b.shore.Select(v => new Vector4(v.x, v.y, 0, 0)), 120);
        if (!b.bounds.Contains(p)) return;
        float d = SignedDistance(p, b.shore) + 3.5f * Fbm(p.x / 22f + b.level, p.y / 22f, 3);   // < 0 in the water
        if (d > 120) return;
        float slope = b.bankSlope * (0.75f + 0.6f * Mathf.Abs(Fbm(p.x / 35f + 1.7f, p.y / 35f, 2)));
        if (d < 0)
        {
            float t = -d;
            float bed = b.level + 0.05f - 0.9f * SmoothStep(0, 5, t) - (b.depth - 0.95f) * SmoothStep(3, b.deepDistance, t);   // continuous with the bank at the edge
            bed += 0.35f * Fbm(p.x / 18f, p.y / 18f, 2) * SmoothStep(2, 10, t);
            cap = Mathf.Min(cap, bed);
            surface = Mathf.Max(surface, b.level);
        }
        else
        {
            cap = Mathf.Min(cap, b.level + 0.05f + slope * d);
            if (d < 10) levee = Mathf.Max(levee, b.level + Mathf.Min(0.35f, slope * d) - (d > 6 ? (d - 6) * 10 : 0));
        }
    }

    static void WaterLine(Vector2 p, Line l, ref float levee, ref float cap, ref float surface)
    {
        if (l.bounds.width == 0) l.bounds = Bounds(l.pts, 150);
        if (!l.bounds.Contains(p)) return;
        float best = float.MaxValue; Vector4 hit = default;
        for (int i = 0; i < l.pts.Length - 1; i++)
        {
            Vector4 a = l.pts[i], b = l.pts[i + 1];
            var ab = new Vector2(b.x - a.x, b.y - a.y);
            float t = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.y), ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            var q = new Vector2(a.x, a.y) + ab * t;
            float d = Vector2.Distance(p, q);
            if (d < best) { best = d; hit = Vector4.Lerp(a, b, t); }
        }
        if (best > 150) return;
        float s = hit.z, w = hit.w * (l.fixedSlope ? 1f : 1f + 0.15f * Fbm(p.x / 25f, p.y / 25f + 9f, 2));
        if (best < w)
        {
            float u = best / w;
            // flat deep middle, easing up to a shallow shelf at the bank (a parabola is steepest right at the waterline)
            float bed = s - 0.05f - (l.depth - 0.05f) * (1 - SmoothStep(0.25f, 1f, u));
            cap = Mathf.Min(cap, bed);
            surface = Mathf.Max(surface, s);
        }
        else
        {
            float slope = l.fixedSlope ? l.bankSlope : l.bankSlope * (0.8f + 0.4f * Mathf.Abs(Fbm(p.x / 30f, p.y / 30f + 4f, 2)));
            float e = best - w;
            cap = Mathf.Min(cap, s - 0.05f + slope * e);
            if (e < 8) levee = Mathf.Max(levee, s + Mathf.Min(0.3f, slope * e) - (e > 5 ? (e - 5) * 10 : 0));
        }
    }

    // Distance from p to the nearest water edge (0 in water).
    static float WaterEdgeDistance(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var b in Basins) best = Mathf.Min(best, Mathf.Max(0, SignedDistance(p, b.shore)));
        foreach (var l in Lines)
        {
            for (int i = 0; i < l.pts.Length - 1; i++)
            {
                Vector4 a = l.pts[i], b = l.pts[i + 1];
                var ab = new Vector2(b.x - a.x, b.y - a.y);
                float t = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.y), ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                float d = Vector2.Distance(p, new Vector2(a.x, a.y) + ab * t) - Mathf.Lerp(a.w, b.w, t);
                best = Mathf.Min(best, Mathf.Max(0, d));
            }
        }
        return best;
    }

    // Region weights for the detail stamps (x = highlands, y = forest, z = meadow), faded away from water and the home pad.
    static Vector3 RegionWeights(float x, float z)
    {
        var p = new Vector2(x, z);
        float dPlay = SignedDistance(p, Playable);
        float inside = 1 - SmoothStep(-10, 30, dPlay);
        float wHigh = Weight(SignedDistance(p, HighlandsArea), 30);
        float wMeadow = Weight(SignedDistance(p, MeadowArea), 20);
        float wSwamp = Weight(SignedDistance(p, SwampArea) + 6f * Fbm(x / 30f, z / 30f + 3f, 3), 18);
        float wForest = Mathf.Clamp01(1 - wHigh - wMeadow - wSwamp);
        float waterFade = SmoothStep(3, 26, WaterEdgeDistance(p));
        float home = SmoothStep(22, 45, Vector2.Distance(p, HomeBase));
        return new Vector3(wHigh * waterFade * inside, wForest * waterFade * inside, wMeadow * waterFade * home * inside);
    }

    static float SwampMask(float x, float z)
    {
        var p = new Vector2(x, z);
        float inner = Weight(SignedDistance(p, SwampArea) + 8, 16);
        // keep the outlet and inflow mouths clear
        return inner;
    }

    // ---------------- MicroVerse stack ----------------

    const string ScenePath = "Assets/_Game/Scenes/Prototype/Prototype_Environment.unity";
    const string RowlanStamps = "Assets/_StoreAssets/Env/Rowlan/Terrain/Stamps/";

    public static object OpenScene()
    {
        var active = SceneManager.GetActiveScene();
        if (active.isDirty) throw new InvalidOperationException("Active scene " + active.path + " has unsaved changes.");
        // reopening the saved scene also drops the GPU Resident Drawer's stale copies of objects that scripts deleted
        var sc = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return new { scene = sc.path, roots = sc.GetRootGameObjects().Select(g => g.name).ToArray() };
    }

    // Rebuilds the "Height" group: macro base -> region detail -> gameplay pads (hierarchy = evaluation order).
    public static object BuildHeights()
    {
        var mv = MicroVerse.instance;
        if (mv == null) throw new InvalidOperationException("No MicroVerse; run the kit's Setup first.");
        var old = mv.transform.Find("Height");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var height = Group(mv.transform, "Height");
        string maps = MapFolder + "/";

        var b = Stamp(Group(height, "00 Base"), "MH_Base", LoadTex(maps + "MH_Base.png"), HeightStamp.CombineMode.Override, new Vector2(500, 500), Size, 0, MaxHeight);
        b.falloff.filterType = FalloffFilter.FilterType.Global;

        var detail = Group(height, "20 Region Detail");
        foreach (var d in Details)
        {
            var st = Stamp(detail, d.name, LoadTex(RowlanStamps + d.texture), HeightStamp.CombineMode.Add, d.center, d.size, 0, d.amplitude);
            Mask(st, LoadTex(maps + MaskName(d) + ".png"), d.center, d.size);
        }

        var pads = Group(height, "90 Gameplay Pads");
        var home = Stamp(pads, "Home Base Pad", null, HeightStamp.CombineMode.Override, HomeBase, 84, HomeBaseHeight, 1);
        home.falloff.filterType = FalloffFilter.FilterType.Range;
        home.falloff.falloffRange = new Vector2(0.55f, 1f);
        home.falloff.easing.blend = Easing.BlendShape.Smoothstep;

        foreach (var st in height.GetComponentsInChildren<Stamp>(true)) EditorUtility.SetDirty(st);
        EditorSceneManager.MarkSceneDirty(mv.gameObject.scene);
        mv.Invalidate();
        return new { stamps = height.GetComponentsInChildren<HeightStamp>(true).Select(st => st.name).ToArray() };
    }

    static HeightStamp Stamp(Transform parent, string name, Texture2D tex, HeightStamp.CombineMode mode, Vector2 c, float size, float y, float height)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Meadow Home stamp");
        go.transform.SetParent(parent, false);
        var hs = go.AddComponent<HeightStamp>();   // OnEnable may zero position.y: place after adding
        hs.stamp = tex;
        hs.mode = mode;
        hs.stampVersion = 1;
        hs.falloff.filterType = FalloffFilter.FilterType.Box;
        hs.falloff.falloffRange = new Vector2(0.8f, 1f);
        go.transform.position = new Vector3(c.x, y, c.y);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = new Vector3(size, height, size);
        return hs;
    }

    // Texture falloff sampling a terrain-space mask: uv' = (stampUV - 0.5) * size/1000 + 0.5 + (centre/1000 - 0.5).
    static void Mask(HeightStamp st, Texture2D mask, Vector2 c, float size)
    {
        st.falloff.filterType = FalloffFilter.FilterType.Texture;
        st.falloff.texture = mask;
        st.falloff.textureChannel = FalloffFilter.TextureChannel.R;
        st.falloff.textureParams = new Vector2(1, 0);
        st.falloff.textureRotationScale = new Vector4(0, size / Size, c.x / Size - 0.5f, c.y / Size - 0.5f);
        st.falloff.falloffRange = new Vector2(0.8f, 1f);
        st.falloff.clampTexture = true;
    }

    // Temporary blockout surface so slopes and water edges read in screenshots (replaced in the texturing pass).
    public static object BlockoutTextures(string grass, string rock, string shore)
    {
        var mv = MicroVerse.instance;
        var old = mv.transform.Find("Texture");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var g = Group(mv.transform, "Texture");
        var baseLayer = Tex(g, "Base Grass", grass);
        baseLayer.filterSet.falloffFilter.filterType = FalloffFilter.FilterType.Global;
        var sh = Tex(g, "Shore", shore);
        sh.filterSet.falloffFilter.filterType = FalloffFilter.FilterType.Global;
        sh.filterSet.heightFilter.enabled = true;
        sh.filterSet.heightFilter.range = new Vector2(0, 21.2f);
        sh.filterSet.heightFilter.smoothness = new Vector2(0, 1.2f);
        var r = Tex(g, "Rock Slopes", rock);
        r.filterSet.falloffFilter.filterType = FalloffFilter.FilterType.Global;
        r.filterSet.slopeFilter.enabled = true;
        r.filterSet.slopeFilter.range = new Vector2(34, 90);
        r.filterSet.slopeFilter.smoothness = new Vector2(6, 6);
        foreach (var st in g.GetComponentsInChildren<Stamp>(true)) EditorUtility.SetDirty(st);
        mv.Invalidate();
        return g.GetComponentsInChildren<TextureStamp>().Select(t => t.name + ": " + t.layer.name).ToArray();
    }

    static TextureStamp Tex(Transform parent, string name, string layerPath)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Meadow Home texture");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextureStamp>();
        t.layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath) ?? throw new ArgumentException("No layer " + layerPath);
        t.stampVersion = 1;
        t.filterSet.version = 2;
        go.transform.position = new Vector3(500, 0, 500);
        go.transform.localScale = new Vector3(Size, 1, Size);
        return t;
    }

    static Transform Group(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return t;
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Meadow Home group");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Texture2D LoadTex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new ArgumentException("Missing texture " + path);

    // Puts the Scene view camera exactly at (x, y, z) with pitch/yaw/fov (y above the terrain when aboveGround), for screenshots.
    public static object Look(float x, float y, float z, float pitch, float yaw, float fov, bool aboveGround)
    {
        var view = SceneView.lastActiveSceneView ?? throw new InvalidOperationException("No Scene view open.");
        if (aboveGround && Terrain.activeTerrain != null) y += Terrain.activeTerrain.SampleHeight(new Vector3(x, 0, z));
        view.orthographic = false;
        view.drawGizmos = false;
        var state = view.sceneViewState;
        state.showSkybox = true; state.showFog = true; state.showImageEffects = true; state.showFlares = true;
        var cs = view.cameraSettings;
        cs.fieldOfView = fov; cs.dynamicClip = false; cs.nearClip = 0.3f; cs.farClip = 4000f;
        // scene-view-only visibility (stored per user, not in the scene): keep the HUD canvas and player out of the shot
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "HUD" || root.name == "Player") SceneVisibilityManager.instance.Hide(root, true);
        var tmp = new GameObject("Look (temp)") { hideFlags = HideFlags.HideAndDontSave };
        tmp.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(pitch, yaw, 0));
        view.AlignViewToObject(tmp.transform);
        UnityEngine.Object.DestroyImmediate(tmp);
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
        return new { camera = new Vector3(x, y, z).ToString("0.0"), pitch, yaw, fov };
    }

    // ---------------- Boundary (models, not terrain) ----------------
    // One asset family per side, layered so that each layer rises above the one in front of it as seen from the yard,
    // the bridge, the ponds and the vista points:
    //   foot rocks at the edge (2-14 m above the ground) -> MicroVerse "Rim" tree belt -> ridges -> snow peaks.
    // The terrain is never raised; mountain meshes stand in for raised far ground. Invisible walls follow the playable edge.
    //   South: HIGHLANDS rocks, green M4 ridges, snow peaks.   West: Komorebi cliff terraces and snow plateaus (the waterfall's family).
    //   North: Grasslands slabs, flattened BK Meadows hills, Grasslands peaks.   East: Lakefell shore rocks, BK hills, a HIGHLANDS peak, BK far range.

    const string Env = "Assets/_StoreAssets/Env/";
    const string HLM = Env + "Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Mountains/";
    const string HLR = Env + "Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
    const string GLR = Env + "Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Rocks/";
    const string GLM = Env + "Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Other/";
    const string KNR = Env + "Raygeas/Komorebi Nature/Assets/Prefabs/";
    const string LFR = Env + "Raygeas/Lakefell Village/Assets/Prefabs/Nature Environment/";
    const string BKH = Env + "BK/PureNature_Meadows/Prefabs/Mountains/";
    const string BKF = Env + "BK/PureNature_Mountains/Prefabs/Mountains/";
    const string BoundaryFolder = DataFolder + "/Boundary";

    public enum Side { South, East, North, West }

    // top = height above the pivot and radius = footprint radius, both at scale 1
    public readonly struct Rock
    {
        public readonly string path; public readonly float top, radius;
        public Rock(string path, float top, float radius) { this.path = path; this.top = top; this.radius = radius; }
    }

    // One band of foot-rock clusters: distance beyond the edge, scale, crest above the local ground, rocks per cluster,
    // gap between clusters, share of skipped (bare) stretches, maximum tilt.
    public class RockBand { public Rock[] rocks; public float dMin, dMax, sMin, sMax, topMin, topMax, gapMin, gapMax, bare, tilt; public int nMin, nMax; }

    static readonly Rock[] HighlandRocks =
    {
        new(HLR + "Rock_1(Grass).prefab", 2.4f, 2.9f), new(HLR + "Rock_2(Grass).prefab", 2.1f, 3.2f), new(HLR + "Rock_1(Grass).prefab", 2.4f, 2.9f),
        new(HLR + "Rock_1.prefab", 2.4f, 2.9f), new(HLR + "Rock_2.prefab", 2.1f, 3.2f),
    };
    static readonly Rock[] KomorebiCliffs =
    {
        new(KNR + "Rocks Grass/Rock_1.prefab", 13.8f, 10.8f), new(KNR + "Rocks Grass/Rock_2.prefab", 8.2f, 8.6f), new(KNR + "Rocks Grass/Rock_3.prefab", 17.9f, 9.0f),
        new(KNR + "Rocks Grass/Rock_1.prefab", 13.8f, 10.8f), new(KNR + "Rocks Grass/Rock_2.prefab", 8.2f, 8.6f), new(KNR + "Rocks Grass/Rock_3.prefab", 17.9f, 9.0f),
        new(KNR + "Rocks/Rock_1_NoGrass.prefab", 13.8f, 10.8f), new(KNR + "Rocks/Rock_2_NoGrass.prefab", 8.2f, 8.6f), new(KNR + "Rocks/Rock_3_NoGrass.prefab", 17.9f, 9.0f),
    };
    static readonly Rock[] SnowPlateaus =
    {
        new(KNR + "Rocks Snow/Rock_1_Snow.prefab", 13.8f, 10.8f), new(KNR + "Rocks Snow/Rock_3_Snow.prefab", 17.9f, 9.0f), new(KNR + "Rocks Snow/Rock_2_Snow.prefab", 8.2f, 8.6f),
    };
    static readonly Rock[] GrasslandsSlabs =
    {
        new(GLR + "Clif_1.prefab", 11.2f, 9.8f), new(GLR + "Clif_2.prefab", 10.9f, 9.8f), new(GLR + "Clif_3.prefab", 9.2f, 10.5f),
        new(GLR + "Clif_1.prefab", 11.2f, 9.8f), new(GLR + "Clif_2.prefab", 10.9f, 9.8f), new(GLR + "Clif_3_(no_grass).prefab", 9.2f, 10.5f),
    };
    static readonly Rock[] LakefellRocks =
    {
        new(LFR + "Rock_1.prefab", 1.2f, 1.2f), new(LFR + "Rock_3.prefab", 2.2f, 1.9f), new(LFR + "Rock_3.prefab", 2.2f, 1.9f), new(LFR + "Rock_2.prefab", 1.0f, 1.2f),
    };

    static readonly Dictionary<Side, RockBand[]> FootRocks = new()
    {
        { Side.South, new[] { new RockBand { rocks = HighlandRocks, dMin = 2, dMax = 26, sMin = 1.5f, sMax = 3.0f, topMin = 2, topMax = 6, nMin = 2, nMax = 4, gapMin = 25, gapMax = 70, bare = 0.35f, tilt = 12 } } },
        { Side.West, new[]
        {
            new RockBand { rocks = KomorebiCliffs, dMin = 0, dMax = 40, sMin = 0.9f, sMax = 1.6f, topMin = 6, topMax = 14, nMin = 3, nMax = 6, gapMin = 30, gapMax = 60, bare = 0.15f, tilt = 6 },
            new RockBand { rocks = KomorebiCliffs, dMin = 40, dMax = 130, sMin = 1.6f, sMax = 2.6f, topMin = 18, topMax = 30, nMin = 3, nMax = 5, gapMin = 40, gapMax = 80, bare = 0.2f, tilt = 8 },
        } },
        { Side.North, new[] { new RockBand { rocks = GrasslandsSlabs, dMin = 2, dMax = 30, sMin = 1.2f, sMax = 2.5f, topMin = 3, topMax = 8, nMin = 2, nMax = 4, gapMin = 25, gapMax = 70, bare = 0.35f, tilt = 20 } } },
        { Side.East, new[] { new RockBand { rocks = LakefellRocks, dMin = 0, dMax = 18, sMin = 3, sMax = 7, topMin = 3, topMax = 8, nMin = 2, nMax = 4, gapMin = 30, gapMax = 80, bare = 0.6f, tilt = 10 } } },
    };

    // Ridges and peaks: (prefab, x, z, scale across, scale up, yaw, group). HIGHLANDS tiles are whole ranges with a low side
    // (M1 -Z, M2 +Z, M3 +X, M4 -Z); yaw = direction to the map - low side turns their gentle slopes to the player.
    // BK meshes are tiny (about 0.028 m across at scale 1), hence the large scales; flattened they read as green hills.
    static readonly (string path, float x, float z, float sxz, float sy, float yaw, string group)[] Backdrop =
    {
        (HLM + "Mountain_4.prefab", 190, -110, 0.40f, 0.60f, 180, "Ridges"),
        (HLM + "Mountain_4.prefab", 470, -140, 0.42f, 0.55f, 195, "Ridges"),
        (HLM + "Mountain_4.prefab", 760, -120, 0.38f, 0.65f, 165, "Ridges"),
        (HLM + "Mountain_1.prefab", 240, -400, 0.70f, 1.05f, 180, "Peaks"),
        (HLM + "Mountain_3.prefab", 600, -430, 0.68f, 1.00f, 270, "Peaks"),
        (HLM + "Mountain_2.prefab", 930, -330, 0.62f, 1.00f, 0, "Peaks"),
        (HLM + "Mountain_3.prefab", -260, -170, 0.62f, 1.00f, 315, "Peaks"),
        (HLM + "Mountain_2.prefab", -420, 520, 0.80f, 1.25f, 90, "Peaks"),
        (HLM + "Mountain_1.prefab", -300, 930, 0.70f, 1.10f, 315, "Peaks"),
        (BKH + "Mountain1.prefab", 300, 1230, 22000, 10500, 10, "Ridges"),
        (BKH + "Mountain2.prefab", 680, 1220, 22000, 9500, 200, "Ridges"),
        (GLM + "Mountain_1.prefab", 180, 1450, 1.3f, 1.7f, 40, "Peaks"),
        (GLM + "Mountain_2.prefab", 520, 1480, 1.4f, 1.9f, 130, "Peaks"),
        (GLM + "Mountain_1.prefab", 850, 1420, 1.2f, 1.5f, 250, "Peaks"),
        (BKH + "Mountain2.prefab", 1180, 300, 20000, 8000, 90, "Ridges"),
        (BKH + "Mountain1.prefab", 1150, 700, 20000, 7500, 300, "Ridges"),
        (HLM + "Mountain_3.prefab", 1320, 430, 0.60f, 1.00f, 180, "Peaks"),
        (BKF + "Mountain1.prefab", 1600, 150, 45000, 40000, 20, "Far Range"),
        (BKF + "Mountain2.prefab", 1650, 600, 45000, 38000, 110, "Far Range"),
        (BKF + "Mountain1.prefab", 1550, 1000, 42000, 42000, 250, "Far Range"),
    };

    // Gameplay spots the boundary rocks must leave clear: (centre, radius).
    static readonly (Vector2 c, float r)[] KeepOut =
    {
        (HomeBase, 38f),              // 30 x 30 fence + margin
        (Viewpoint, 14f),
        (new Vector2(228, 424), 24f), // waterfall pond east of the cliff line
        (new Vector2(190, 435), 26f), // waterfall lip and its source pool
        (Bridge, 30f),
    };

    static bool Blocked(Vector2 c, float radius)
    {
        foreach (var k in KeepOut) if (Vector2.Distance(c, k.c) < k.r + radius) return true;
        foreach (var l in Lines) if (LineEdge(c, l) < radius + 4f) return true;
        return false;
    }

    // Outward normal angle -> side; jitter blends the families around the corners.
    static Side SideOf(Vector2 n, float jitter)
    {
        float a = Mathf.Repeat(Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg + jitter + 360f, 360f);
        if (a >= 45 && a < 135) return Side.North;
        if (a >= 135 && a < 225) return Side.West;
        if (a >= 225 && a < 315) return Side.South;
        return Side.East;
    }

    // True when any above-ground vertex of the object lies closer than margin to (or inside) the playable area.
    static bool Intrudes(GameObject go, float margin)
    {
        var terrain = Terrain.activeTerrain;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var verts = mf.sharedMesh.vertices;
            var m = mf.transform.localToWorldMatrix;
            for (int k = 0; k < verts.Length; k += 3)
            {
                var w = m.MultiplyPoint3x4(verts[k]);
                if (w.x < -5 || w.z < -5 || w.x > Size + 5 || w.z > Size + 5) continue;   // off the terrain: always outside
                if (SignedDistance(new Vector2(w.x, w.z), Playable) > margin) continue;
                if (w.y > terrain.SampleHeight(w) - 0.5f) return true;
            }
        }
        return false;
    }

    // Copies of the mountain materials with their built-in haze retinted from teal to the scene's sky haze. These shaders
    // skip scene fog, so this emission is their only aerial perspective. Vendor materials stay untouched.
    static Material BoundaryMaterial(Material vendor)
    {
        EnsureFolder(BoundaryFolder);
        string dest = BoundaryFolder + "/MH_Boundary_" + (vendor.shader.name.Contains("Highlands") ? "HL_" : "GL_") + vendor.name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(dest);
        if (m == null) { m = new Material(vendor) { name = Path.GetFileNameWithoutExtension(dest) }; AssetDatabase.CreateAsset(m, dest); }
        else m.CopyPropertiesFromMaterial(vendor);
        bool ridge = vendor.name == "Mountain_4";
        if (vendor.shader.name.Contains("Highlands"))
            m.SetColor("_FogColor", ridge ? new Color(0.17f, 0.25f, 0.33f, 0) : new Color(0.30f, 0.42f, 0.55f, 0));
        else
        {
            m.SetColor("_FogColor", new Color(0.30f, 0.44f, 0.60f, 0));
            m.SetFloat("_Density", 0.25f);
        }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssetIfDirty(m);
        return m;
    }

    // Places the boundary (deterministic per seed); rebuilding replaces the previous "Boundary" root.
    public static object BuildBoundary(int seed)
    {
        var terrain = Terrain.activeTerrain ?? throw new InvalidOperationException("No terrain.");
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Environment") ?? new GameObject("Environment");
        Undo.RegisterCreatedObjectUndo(env, "Environment root");
        var old = env.transform.Find("Boundary");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("Boundary").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Boundary");
        root.SetParent(env.transform, false);
        var rng = new System.Random(seed);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float Ground(float x, float z) => terrain.SampleHeight(new Vector3(Mathf.Clamp(x, 0, Size), 0, Mathf.Clamp(z, 0, Size)));
        float GroundMin(float x, float z, float r)
        {
            float m = Ground(x, z);
            for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; m = Mathf.Min(m, Ground(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r)); }
            return m;
        }
        Transform Child(string name) { var t = new GameObject(name).transform; t.SetParent(root, false); return t; }
        GameObject Put(Transform parent, string path, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new ArgumentException("Missing prefab " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            return go;
        }

        // polygon arc-length sampler with outward normals
        var poly = Playable;
        var cum = new float[poly.Length + 1];
        for (int i = 0; i < poly.Length; i++) cum[i + 1] = cum[i] + Vector2.Distance(poly[i], poly[(i + 1) % poly.Length]);
        float perimeter = cum[poly.Length];
        (Vector2 p, Vector2 n) At(float s)
        {
            s = Mathf.Repeat(s, perimeter);
            int i = Array.FindLastIndex(cum, c => c <= s); if (i >= poly.Length) i = poly.Length - 1;
            var a = poly[i]; var b = poly[(i + 1) % poly.Length];
            var p = Vector2.Lerp(a, b, (s - cum[i]) / Mathf.Max(1e-3f, cum[i + 1] - cum[i]));
            var t = (b - a).normalized; var n = new Vector2(t.y, -t.x);
            if (SignedDistance(p + n * 2, poly) < 0) n = -n;          // make it point outward
            return (p, n);
        }

        // L0 foot rocks: clusters along the edge, each side from its own family, bare stretches between
        var foot = Child("Foot Rocks");
        var sideRoots = new Dictionary<Side, Transform>();
        foreach (Side side in Enum.GetValues(typeof(Side))) { var t = new GameObject(side.ToString()).transform; t.SetParent(foot, false); sideRoots[side] = t; }
        int rockCount = 0;
        for (int band = 0; band < 2; band++)
        {
            float s0 = R(0, 40);
            for (float s = s0; s < perimeter + s0;)
            {
                var (p, n) = At(s);
                var side = SideOf(n, R(-25, 25));
                var bands = FootRocks[side];
                if (band >= bands.Length) { s += 20; continue; }
                var spec = bands[band];
                float gap = R(spec.gapMin, spec.gapMax);
                if (rng.NextDouble() < spec.bare) { s += gap; continue; }
                int count = spec.nMin + rng.Next(spec.nMax - spec.nMin + 1);
                float along = 0, d = R(spec.dMin, spec.dMax);
                for (int k = 0; k < count; k++)
                {
                    var rock = spec.rocks[rng.Next(spec.rocks.Length)];
                    float sc = R(spec.sMin, spec.sMax), r = rock.radius * sc;
                    var (q, nq) = At(s + along);
                    along += r * R(0.9f, 1.4f);                                              // neighbours overlap
                    var c = q + nq * (d + R(-4, 4) + r * 0.55f);
                    if (SignedDistance(c, poly) < r * 0.5f) continue;                         // concave corners
                    int guard = 0;
                    for (; guard < 30 && Blocked(c, r * 0.85f); guard++) c += nq * 4f;      // home, viewpoint, pond, bridge, streams
                    float top = R(spec.topMin, spec.topMax);
                    if (Vector2.Distance(c, HomeBase) < 100f || Vector2.Distance(c, Viewpoint) < 60f) top = Mathf.Min(top, 3f);
                    float sy = sc * R(0.9f, 1.15f);
                    float ground = GroundMin(c.x, c.y, r * 0.6f);
                    var rot = Quaternion.Euler(R(-spec.tilt, spec.tilt), R(0, 360), R(-spec.tilt, spec.tilt));
                    var go = Put(sideRoots[side], rock.path, new Vector3(c.x, ground + top - rock.top * sy, c.y), rot, new Vector3(sc * R(0.9f, 1.1f), sy, sc * R(0.9f, 1.1f)));
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
                    rockCount++;
                }
                s += along + gap;
            }
        }

        // West L2: Komorebi snow plateaus behind the cliff terraces, tilted slabs sunk deep into the ground
        var plateaus = Child("Snow Plateaus");
        int plateauCount = 0;
        foreach (var (cx, cz, n) in new[] { (-95f, 330f, 4), (-150f, 500f, 5), (-110f, 660f, 4), (-170f, 800f, 4) })
            for (int k = 0; k < n; k++)
            {
                var rock = SnowPlateaus[rng.Next(SnowPlateaus.Length)];
                float sc = R(4f, 7f), sy = sc * R(0.85f, 1.1f);
                var c = new Vector2(cx + R(-45, 45), cz + R(-55, 55));
                // crest 120-160 m above the play ground, but never so high that the base lifts off the ground (height ~ 1.9 x top)
                float top = Mathf.Min(30f + R(120f, 160f), Ground(c.x, c.y) - 3f + rock.top * 1.9f * sy);
                var go = Put(plateaus, rock.path, new Vector3(c.x, top - rock.top * sy, c.y), Quaternion.Euler(R(-25, 25), R(0, 360), R(-25, 25)), new Vector3(sc, sy, sc * R(0.9f, 1.2f)));
                go.GetComponentsInChildren<Renderer>().ToList().ForEach(r => r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                plateauCount++;
            }

        // L2 ridges, L3 peaks, east far range: skirts sunk below the lowest ground they cover, pushed out if they reach the play area
        var groups = new Dictionary<string, Transform>();
        var materials = new Dictionary<Material, Material>();
        int mountainCount = 0;
        var centre = new Vector2(505, 520);
        foreach (var b in Backdrop)
        {
            if (!groups.TryGetValue(b.group, out var parent)) groups[b.group] = parent = Child(b.group);
            var go = Put(parent, b.path, Vector3.zero, Quaternion.Euler(0, b.yaw + R(-8, 8), 0), new Vector3(b.sxz, b.sy, b.sxz * (b.path.StartsWith(Env + "BK") ? R(0.95f, 1.25f) : 1f)));
            var c = new Vector2(b.x, b.z);
            var outward = (c - centre).normalized;
            bool placed = false;
            for (int attempt = 0; attempt < 24 && !placed; attempt++, c += outward * 15f)
            {
                go.transform.position = new Vector3(c.x, 0, c.y);
                var bounds = go.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, x) => { a.Encapsulate(x); return a; });
                float low = float.MaxValue;
                for (float x = bounds.min.x; x <= bounds.max.x; x += bounds.size.x / 8)
                    for (float z = bounds.min.z; z <= bounds.max.z; z += bounds.size.z / 8)
                        low = Mathf.Min(low, Ground(x, z));
                go.transform.position = new Vector3(c.x, low - 4f, c.y);
                placed = !Intrudes(go, 40f);
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !(mats[i].shader.name.Contains("Highlands/Background") || mats[i].shader.name.Contains("URP/Mountains"))) continue;
                    if (!materials.TryGetValue(mats[i], out var copy)) materials[mats[i]] = copy = BoundaryMaterial(mats[i]);
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            mountainCount++;
        }

        // haze banks at the feet of ridges and peaks (Komorebi demo: fog banks out to about 700 m)
        var haze = Child("Haze");
        int hazeCount = 0;
        foreach (var b in Backdrop.Where(b => b.group != "Far Range"))
        {
            var c = new Vector2(b.x, b.z);
            var toMap = (centre - c).normalized;
            for (int k = 0; k < 2; k++)
            {
                var q = c + toMap * R(120, 220) + new Vector2(-toMap.y, toMap.x) * R(-160, 160);
                if (SignedDistance(q, poly) < 150f) continue;
                Put(haze, KNR + "Particle Systems/Fog_1.prefab", new Vector3(q.x, Ground(q.x, q.y) + R(8, 30), q.y), Quaternion.Euler(0, R(0, 360), 0), new Vector3(R(8, 14), R(2.5f, 4f), R(8, 14)));
                hazeCount++;
            }
        }

        // safety net under the backdrop: a ground ring from the terrain border outward, so no gap between meshes shows the void
        var ring = Child("Backdrop Ground");
        var mesh = BackdropGroundMesh(Ground);
        var groundGo = new GameObject("Backdrop Ground Mesh");
        groundGo.transform.SetParent(ring, false);
        groundGo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, BoundaryFolder);
        var mr = groundGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = BackdropGroundMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(groundGo, StaticEditorFlags.BatchingStatic);

        // invisible walls along the playable edge: 'Ignore Raycast' so the player is stopped but shots and interaction rays pass
        var walls = Child("Invisible Walls");
        int wallLayer = LayerMask.NameToLayer("Ignore Raycast");
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Length];
            var mid = (a + b) * 0.5f;
            float len = Vector2.Distance(a, b);
            float g = Mathf.Min(Ground(a.x, a.y), Ground(b.x, b.y));
            var w = new GameObject("Wall " + i) { layer = wallLayer };
            w.transform.SetParent(walls, false);
            w.transform.SetPositionAndRotation(new Vector3(mid.x, g + 60f, mid.y), Quaternion.LookRotation(new Vector3(b.x - a.x, 0, b.y - a.y)));
            var box = w.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 140f, len + 1.5f);
            GameObjectUtility.SetStaticEditorFlags(w, StaticEditorFlags.BatchingStatic);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return new { rocks = rockCount, plateaus = plateauCount, mountains = mountainCount, haze = hazeCount, walls = poly.Length, perimeter };
    }

    // Ring from the terrain border out to 2.5 km: inner edge just under the border heights, sloping a few metres down outward.
    static Mesh BackdropGroundMesh(Func<float, float, float> ground)
    {
        var border = new List<Vector2>();
        const float step = 25f;
        for (float t = 0; t < Size; t += step) border.Add(new Vector2(t, 0));
        for (float t = 0; t < Size; t += step) border.Add(new Vector2(Size, t));
        for (float t = Size; t > 0; t -= step) border.Add(new Vector2(t, Size));
        for (float t = Size; t > 0; t -= step) border.Add(new Vector2(0, t));
        float minBorder = border.Min(b => ground(b.x, b.y));
        var rings = new[] { (0f, 0.6f), (60f, 2.5f), (400f, 6f), (2500f, 10f) };   // (distance beyond the border, drop below the border height)
        var c = new Vector2(Size / 2, Size / 2);
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        int n = border.Count;
        foreach (var (dist, drop) in rings)
            foreach (var b in border)
            {
                var dir = new Vector2(Mathf.Abs(b.x - c.x) >= Size / 2 - 0.1f ? Mathf.Sign(b.x - c.x) : 0, Mathf.Abs(b.y - c.y) >= Size / 2 - 0.1f ? Mathf.Sign(b.y - c.y) : 0);
                var p = b + dir * dist;
                float h = Mathf.Lerp(ground(b.x, b.y), minBorder, Mathf.Clamp01(dist / 400f)) - drop;
                verts.Add(new Vector3(p.x, h, p.y)); uvs.Add(p / 20f);
            }
        for (int r = 0; r < rings.Length - 1; r++)
            for (int i = 0; i < n; i++)
            {
                int a = r * n + i, b = r * n + (i + 1) % n, a2 = a + n, b2 = b + n;
                tris.AddRange(new[] { a, a2, b, b, a2, b2 });
            }
        var mesh = new Mesh { name = "MH_BackdropGround" };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        // the border winds counter-clockwise seen from above only if the normals point up; flip otherwise
        if (mesh.normals.Average(v => v.y) < 0) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material BackdropGroundMaterial()
    {
        EnsureFolder(BoundaryFolder);
        string dest = BoundaryFolder + "/MH_BackdropGround.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(dest);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MH_BackdropGround" }; AssetDatabase.CreateAsset(m, dest); }
        var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DataFolder + "/TerrainLayers/MH_Grass.terrainlayer");
        if (grass != null) { m.SetTexture("_BaseMap", grass.diffuseTexture); m.SetColor("_BaseColor", grass.diffuseRemapMax == Vector4.zero ? Color.white : (Color)grass.diffuseRemapMax); }
        m.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssetIfDirty(m);
        return m;
    }

    // Elevation angle of each boundary layer per 10-degree azimuth bin, from the key viewpoints. Flags bins where a layer
    // fails to rise at least `margin` degrees above the layers in front of it (e.g. peaks hidden behind ridges).
    public static object BoundaryAngles(float margin)
    {
        var env = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Environment");
        var root = env.transform.Find("Boundary");
        var layers = new[] { "Foot Rocks", "Snow Plateaus", "Ridges", "Peaks" };
        var views = new (string name, Vector3 p)[]
        {
            ("home", new Vector3(326, 30.2f, 274)), ("meadow", new Vector3(450, 27.1f, 262)), ("viewpoint", new Vector3(546, 22.5f, 246)),
            ("bridge", new Vector3(312, 24.8f, 372)), ("waterfall pond", new Vector3(232, 25.0f, 430)), ("west pond", new Vector3(200, 50.5f, 640)),
            ("highland vista", new Vector3(250, 69.8f, 760)), ("north pond", new Vector3(386, 43.3f, 806)), ("swamp", new Vector3(660, 25.4f, 700)),
            ("lake beach", new Vector3(790, 17.4f, 240)), ("forest", new Vector3(500, 35.5f, 500)),
        };
        // gather sampled vertices per layer once
        var pts = layers.ToDictionary(l => l, l => new List<Vector3>());
        foreach (var l in layers)
        {
            var t = root.Find(l);
            if (t == null) continue;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var lod = mf.GetComponentInParent<LODGroup>();
                if (lod != null && lod.GetLODs().Length > 0 && !lod.GetLODs()[0].renderers.Contains(mf.GetComponent<Renderer>())) continue;
                var v = mf.sharedMesh.vertices; var m = mf.transform.localToWorldMatrix;
                int stride = Mathf.Max(1, v.Length / 1500);
                for (int i = 0; i < v.Length; i += stride) pts[l].Add(m.MultiplyPoint3x4(v[i]));
            }
        }
        var report = new List<string>();
        foreach (var (name, p) in views)
        {
            var best = layers.ToDictionary(l => l, l => new float[36]);
            foreach (var l in layers) for (int i = 0; i < 36; i++) best[l][i] = -90;
            foreach (var l in layers)
                foreach (var w in pts[l])
                {
                    var d = w - p; float flat = new Vector2(d.x, d.z).magnitude;
                    if (flat < 1f || flat > 1450f) continue;   // player camera far clip 1500 m
                    int bin = (int)(Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f) / 10f) % 36;
                    best[l][bin] = Mathf.Max(best[l][bin], Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg);
                }
            var issues = new List<string>();
            for (int i = 0; i < 36; i++)
            {
                float front = Mathf.Max(best["Foot Rocks"][i], best["Snow Plateaus"][i]);
                float ridge = best["Ridges"][i], peak = best["Peaks"][i];
                if (peak > -90 && peak < Mathf.Max(front, ridge) + margin) issues.Add($"{i * 10}deg peak {peak:0.0} vs front {front:0.0} ridge {ridge:0.0}");
            }
            string Max(string l) => best[l].Max().ToString("0.0");
            report.Add($"{name}: max foot {Max("Foot Rocks")} plateau {Max("Snow Plateaus")} ridge {Max("Ridges")} peak {Max("Peaks")}; hidden peak bins: {(issues.Count == 0 ? "none" : string.Join("; ", issues.Take(8)))}");
        }
        return string.Join("\n", report);
    }

    // ---------------- Water surfaces (flat bodies) ----------------

    const string WaterFolder = DataFolder + "/Water";

    // Grid mesh (cell metres) covering a polygon grown by margin, at local Y 0 with the pivot at the polygon centroid.
    // The margin tucks the edge under the banks: outside the shore the terrain rises above the water level.
    public static Mesh FlatWaterMesh(string name, Vector2[] outline, float margin, float cell, Func<float, float, bool> wet)
    {
        var bb = Bounds(outline.Select(v => new Vector4(v.x, v.y, 0, 0)), margin + cell);
        var centre = Centroid(outline);
        int nx = Mathf.CeilToInt(bb.width / cell) + 1, nz = Mathf.CeilToInt(bb.height / cell) + 1;
        var inside = new bool[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                inside[i, j] = SignedDistance(new Vector2(bb.xMin + i * cell, bb.yMin + j * cell), outline) < margin;
        var verts = new List<Vector3>(); var index = new Dictionary<int, int>(); var tris = new List<int>();
        int V(int i, int j)
        {
            int key = j * nx + i;
            if (!index.TryGetValue(key, out int v)) { v = verts.Count; index[key] = v; verts.Add(new Vector3(bb.xMin + i * cell - centre.x, 0, bb.yMin + j * cell - centre.y)); }
            return v;
        }
        for (int i = 0; i < nx - 1; i++)
            for (int j = 0; j < nz - 1; j++)
            {
                // keep a quad when any corner is inside: the grown outline is covered without holes
                if (!(inside[i, j] || inside[i + 1, j] || inside[i, j + 1] || inside[i + 1, j + 1])) continue;
                float x0 = bb.xMin + i * cell, z0 = bb.yMin + j * cell;
                if (!(wet(x0, z0) || wet(x0 + cell, z0) || wet(x0, z0 + cell) || wet(x0 + cell, z0 + cell))) continue;   // dry: the terrain covers it
                int a = V(i, j), b = V(i + 1, j), c = V(i + 1, j + 1), d = V(i, j + 1);
                tris.AddRange(new[] { a, d, c, a, c, b });
            }
        var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(Enumerable.Repeat(Vector3.up, verts.Count).ToList());
        mesh.SetUVs(0, verts.Select(v => new Vector2(v.x, v.z)).ToList());
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh FlatWaterMeshPublic(string name, Vector2[] outline, float margin, float cell, Func<float, float, bool> wet)
        => SaveMesh(FlatWaterMesh(name, outline, margin, cell, wet));

    // Saves a mesh asset under the Water data folder (overwrites in place to keep GUIDs stable).
    static Mesh SaveMesh(Mesh mesh, string folder = WaterFolder)
    {
        EnsureFolder(folder);
        string path = folder + "/" + mesh.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        // Refill through the Mesh API: CopySerialized updates the CPU data but leaves the old GPU buffers on screen.
        existing.Clear();
        existing.indexFormat = mesh.indexFormat;
        existing.SetVertices(mesh.vertices);
        existing.SetNormals(mesh.normals);
        existing.SetUVs(0, mesh.uv);
        existing.SetTriangles(mesh.triangles, 0);
        existing.RecalculateBounds();
        existing.UploadMeshData(false);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssetIfDirty(existing);
        return existing;
    }

    // ---------------- Water (Stylized Water 3) ----------------
    // Flat bodies: WaterObject + generated mesh at the water level. Rivers/streams: SplineContainer + RiverModeler ribbons.

    const string SW3 = "Assets/_StoreAssets/Rendering/Staggart Creations/Stylized Water 3/";

    public static object WaterRendering()
    {
        var rp = UniversalRenderPipeline.asset;
        var feature = PipelineUtilities.GetRenderFeature<StylizedWaterRenderFeature>() as StylizedWaterRenderFeature;
        PipelineUtilities.RenderFeatureMissing<StylizedWaterRenderFeature>(out var missing);
        return new
        {
            pipeline = rp ? rp.name : "none",
            depthTexture = rp && rp.supportsCameraDepthTexture,
            opaqueTexture = rp && rp.supportsCameraOpaqueTexture,
            featureMissingOn = missing.Select(r => r.name).ToArray(),
            featureActive = feature != null && feature.isActive,
        };
    }

    // Copies of SW3 materials (vendor .mat files stay untouched), tinted toward Komorebi's turquoise water.
    public static object BuildWaterMaterials()
    {
        EnsureFolder(WaterFolder);
        var still = CopyMaterial(SW3 + "Materials/StylizedWater3_Smooth.mat", WaterFolder + "/MH_Water_Still.mat");
        still.SetColor("_BaseColor", new Color(0.00f, 0.41f, 0.50f, 1.00f));      // Komorebi Water_1 deep colour
        still.SetColor("_ShallowColor", new Color(0.10f, 0.78f, 0.75f, 0.60f));
        still.SetColor("_HorizonColor", new Color(0.62f, 0.96f, 0.95f, 0.12f));
        still.SetFloat("_HorizonDistance", 12f);
        still.SetFloat("_DepthVertical", 7f);
        still.SetFloat("_DepthHorizontal", 0.5f);
        still.SetFloat("_EdgeFade", 15f);
        still.SetFloat("_WaveTint", 0f);
        WaterToggle(still, "_IntersectionFoamOn", "_INTERSECTION_FOAM", true);
        still.SetFloat("_IntersectionSource", 0f);
        still.SetColor("_IntersectionColor", new Color(0.90f, 0.95f, 0.95f, 0.45f));
        still.SetFloat("_IntersectionLength", 1.25f);
        still.SetFloat("_IntersectionFalloff", 0.5f);
        still.SetFloat("_IntersectionClipping", 0.45f);
        still.SetFloat("_IntersectionTiling", 0.25f);
        still.SetFloat("_IntersectionSpeed", 0.1f);
        still.SetFloat("_FoamOn", 0f); still.SetFloat("_FoamDistanceOn", 0f);
        CoreUtils.SetKeyword(still, "_SURFACE_FOAM_SINGLE", false); CoreUtils.SetKeyword(still, "_SURFACE_FOAM_DUAL", false);
        WaterToggle(still, "_WavesOn", "_WAVES", false);
        WaterToggle(still, "_NormalMapOn", "_NORMALMAP", true);
        still.SetFloat("_NormalStrength", 0.25f);
        still.SetVector("_NormalTiling", new Vector4(0.25f, 0.25f, 0f, 0f));
        still.SetFloat("_NormalSpeed", 1.5f);
        WaterToggle(still, "_RefractionOn", "_REFRACTION", true);
        still.SetFloat("_RefractionStrength", 0.15f);
        still.SetFloat("_ShadingMode", 1f); CoreUtils.SetKeyword(still, "_ADVANCED_SHADING", true);

        var river = CopyMaterial(SW3 + "Materials/RiverModeler/StylizedWater3_SplineRiver (Default).mat", WaterFolder + "/MH_Water_River.mat");
        river.SetColor("_BaseColor", new Color(0.00f, 0.41f, 0.50f, 0.98f));
        river.SetColor("_ShallowColor", new Color(0.10f, 0.78f, 0.75f, 0.30f));
        river.SetFloat("_DepthVertical", 7f);
        river.SetFloat("_DepthHorizontal", 0.5f);
        WaterToggle(river, "_RiverModeOn", "_RIVER", true);
        river.SetFloat("_RiverModeLegacy", 0f);
        river.SetFloat("_ZWrite", 0f);
        river.SetFloat("_VertexColorTransparency", 1f);      // RiverModeler writes bank alpha and foam into vertex colours
        river.SetFloat("_VertexColorFoam", 1f);
        WaterToggle(river, "_IntersectionFoamOn", "_INTERSECTION_FOAM", true);
        river.SetFloat("_IntersectionSource", 2f);            // depth + vertex colour R (the bank gradient)
        river.SetColor("_IntersectionColor", new Color(1f, 1f, 1f, 0.22f));
        river.SetFloat("_IntersectionLength", 1.2f);          // ln(1.2) ~ 0.18 m: a soft lap line, not a white rim
        // sharp foam turns everything above the clip solid white; the ribbon's vertex-colour edge band then reads as a
        // continuous curb along both banks. A high clip keeps only the noise peaks.
        river.SetFloat("_IntersectionClipping", 0.6f);
        river.SetFloat("_ShadingMode", 1f); CoreUtils.SetKeyword(river, "_ADVANCED_SHADING", true);
        // calm, cozy flow: the vendor river materials run time at 16x, which reads as rapids at our river scale
        river.SetFloat("_Speed", 6f);
        river.SetFloat("_SlopeSpeed", 1.5f);
        river.SetFloat("_FoamSpeed", 0.7f);
        river.SetFloat("_NormalSpeed", 0.8f);
        still.SetFloat("_NormalSpeed", 1.0f);
        still.SetFloat("_NormalSubSpeed", -0.5f);

        foreach (var m in new[] { still, river }) { EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m); }
        return new[] { AssetDatabase.GetAssetPath(still), AssetDatabase.GetAssetPath(river) };
    }

    static void WaterToggle(Material m, string property, string keyword, bool on)
    {
        m.SetFloat(property, on ? 1f : 0f);       // SetFloat skips the [Toggle] drawer, so set the keyword too
        CoreUtils.SetKeyword(m, keyword, on);
    }

    static Material CopyMaterial(string source, string destination)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(destination);
        if (existing) return existing;
        var src = AssetDatabase.LoadAssetAtPath<Material>(source) ?? throw new FileNotFoundException(source);
        var copy = new Material(src) { name = Path.GetFileNameWithoutExtension(destination) };
        AssetDatabase.CreateAsset(copy, destination);
        return copy;
    }

    static Transform WaterGroup(string child)
    {
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Environment") ?? new GameObject("Environment");
        var water = env.transform.Find("Water");
        if (water == null) { water = new GameObject("Water").transform; water.SetParent(env.transform, false); }
        var old = water.Find(child);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject(child);
        Undo.RegisterCreatedObjectUndo(go, "Water");
        go.transform.SetParent(water, false);
        return go.transform;
    }

    // Ponds, lake and swamp: one WaterObject each, mesh kept to cells with at least one corner under water (edges tuck under the banks).
    public static object BuildFlatWater()
    {
        var terrain = Terrain.activeTerrain;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterFolder + "/MH_Water_Still.mat") ?? throw new InvalidOperationException("Run BuildWaterMaterials first.");
        var group = WaterGroup("Still Water");
        var bodies = Basins.Select(b => (b.name, b.shore, b.level)).Concat(new[] { (name: "Swamp", shore: SwampArea, level: SwampWater) });
        var report = new List<string>();
        foreach (var b in bodies)
        {
            // wet = low enough to hold this water, and not inside a lower river/stream channel (those carry their own ribbon)
            bool Wet(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) <= b.level - 0.02f && !LowerChannel(new Vector2(x, z), b.level);
            var mesh = FlatWaterMesh("MH_" + b.name, b.shore, 8f, 2f, Wet);
            mesh = SaveMesh(mesh);
            var wo = WaterObject.New(mat, mesh);
            Undo.RegisterCreatedObjectUndo(wo.gameObject, "Water");
            wo.name = b.name;
            wo.transform.SetParent(group, false);
            var c = Centroid(b.shore);
            wo.transform.position = new Vector3(c.x, b.level, c.y);   // transform Y = water level (SW3 height queries read it)
            report.Add($"{b.name}: level {b.level}, {mesh.vertexCount} verts");
        }
        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        return report;
    }

    // True when p is within 3 m of a river/stream whose surface there is lower than `level`.
    static bool LowerChannel(Vector2 p, float level)
    {
        foreach (var l in Lines)
            for (int i = 0; i < l.pts.Length - 1; i++)
            {
                Vector4 a = l.pts[i], b = l.pts[i + 1];
                var ab = new Vector2(b.x - a.x, b.y - a.y);
                float t = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.y), ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                float edge = Vector2.Distance(p, new Vector2(a.x, a.y) + ab * t) - Mathf.Lerp(a.w, b.w, t);
                if (edge < 3f && Mathf.Lerp(a.z, b.z, t) < level - 0.1f) return true;
            }
        return false;
    }

    // Rivers and streams as RiverModeler ribbons. Each line is trimmed to start/end a few metres inside the pond, lake or
    // swamp it connects to, so the faded ends blend into the flat water instead of stacking two transparent surfaces.
    public static object BuildRivers()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterFolder + "/MH_Water_River.mat") ?? throw new InvalidOperationException("Run BuildWaterMaterials first.");
        var vfx = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(SW3 + "Runtime/RiverModeler/VFX/River Foam VFX.vfx");
        var group = WaterGroup("Rivers");
        var built = new List<string>();
        var river = Lines.First(l => l.name == "River");
        built.Add(BuildRiverGroup(group, "River", new[] { Trim(river) }, mat, vfx, 16f, 2f, 2f, 0f, 1.5f, false));
        var streams = Lines.Where(l => l.name != "River").Select(Trim).Where(k => k.Length >= 2).ToArray();
        built.Add(BuildRiverGroup(group, "Streams", streams, mat, vfx, 5f, 0.75f, 1f, 0.15f, 0.75f, true));
        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        return built;
    }

    // Drops knots that lie more than `overlap` metres inside a flat water body (ponds, lake, swamp), keeping one knot inside.
    static Vector4[] Trim(Line l)
    {
        const float overlap = 3f;
        bool Deep(Vector4 k)
        {
            var p = new Vector2(k.x, k.y);
            return Basins.Any(b => SignedDistance(p, b.shore) < -overlap) || SignedDistance(p, SwampArea) < -overlap;
        }
        int first = 0, last = l.pts.Length - 1;
        while (first < last && Deep(l.pts[first + 1]) && Deep(l.pts[first])) first++;
        while (last > first && Deep(l.pts[last - 1]) && Deep(l.pts[last])) last--;
        // every carved knot: a decimated spline drifts off the channel on curves and exposes a strip of bed
        return l.pts.Skip(first).Take(last - first + 1).ToArray();
    }

    static string BuildRiverGroup(Transform parent, string name, Vector4[][] lines, Material mat, VisualEffectAsset vfx,
        float baseWidth, float widthEdge, float minEdge, float displacement, float widthFalloff, bool cascadeParticles)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Water");
        go.transform.SetParent(parent, false);                       // identity: spline knots are world positions
        var container = go.AddComponent<SplineContainer>();
        container.Splines = lines.Select(k => RiverSpline(k, baseWidth)).ToList();
        var m = go.AddComponent<RM.RiverModeler>();
        m.material = mat;
        m.foamVFX = vfx;
        m.rebuildOnStart = false;
        var s = m.settings;
        s.geometry.baseWidth = baseWidth;
        s.geometry.minEdgeDistance = minEdge;
        s.geometry.widthEdgeDistance = widthEdge;
        s.geometry.simplifyStraight = 25f;
        s.geometry.roundedEnds = false;
        s.geometry.displacementStrength = displacement;
        s.geometry.displacementSlopeInfluence = 0f;
        s.geometry.displacementScale = 0.1f;
        s.geometry.lodLevels = 4;
        s.geometry.lodReduction = 50f;
        s.geometry.lodSelectionSlope = 0.5f;
        s.geometry.twistCorrection = true;
        s.foam.displacementFoam = 1f;
        s.foam.cascadeAngleThreshold = 8f;
        s.foam.cascadeAngleFalloff = 8f;
        s.foam.enableCascadeParticles = cascadeParticles;
        s.foam.cascadeParticleSize = 1f;
        s.foam.enableSplashParticles = false;
        s.foam.velocityStrength = 0.45f;   // foam VFX drift, matches the slower material flow
        s.output.maxSegmentLength = 100f;
        int segments = Mathf.Max(2, Mathf.CeilToInt(container.Splines.Max(sp => sp.GetLength()) / s.output.maxSegmentLength));
        s.transparency.startGradientFalloff = 3f / segments;       // applied per segment: ~3 m visible fade at each end
        s.transparency.endGradientFalloff = 3f / segments;
        s.transparency.widthGradientFalloff = widthFalloff;
        s.output.boxTriggers = false;
        s.output.collider = false;
        s.audio.enable = false;                                     // BroAudio owns audio
        s.audio.profile = null;
        m.SetSplineContainer(container);                            // last: from here every spline edit rebuilds
        m.Rebuild();
        // SetProperties runs before the first build creates the segments, leaving them all at sortingOrder 0
        for (int i = 0; i < m.rivers.Count; i++)
            if (m.rivers[i]) m.rivers[i].SetProperties(mat, i);
        EditorUtility.SetDirty(container);
        EditorUtility.SetDirty(m);
        return $"{name}: {m.rivers.Count} splines, {m.rivers.Sum(r => r ? r.Segments.Count : 0)} segments";
    }

    static Spline RiverSpline(Vector4[] knots, float baseWidth)
    {
        // knots are (x, z, surface y, half width)
        var spline = new Spline(knots.Select(k => new float3(k.x, k.z, k.y)), TangentMode.AutoSmooth, false);
        var scale = new SplineData<float4> { PathIndexUnit = PathIndexUnit.Distance, DefaultValue = new float4(1f) };
        for (int i = 0; i < knots.Length; i++)
        {
            float distance = spline.ConvertIndexUnit(i, PathIndexUnit.Knot, PathIndexUnit.Distance);
            // x: width / baseWidth. Same width noise as the carved channel, plus 0.8 m so the edges tuck under the banks.
            float half = knots[i].w * (1f + 0.15f * Fbm(knots[i].x / 25f, knots[i].y / 25f + 9f, 2)) + 0.8f;
            scale.Add(distance, new float4(half * 2f / baseWidth, 1f, 1f, 1f));   // y: turbulence
        }
        spline.SetFloat4Data(RM.RiverModeler.SCALE_DATA_KEY, scale);
        return spline;
    }

    // ---------------- Texturing ----------------
    // Terrain-space masks (whole-terrain stamps, so stamp UV = terrain UV) + MicroVerse texture stamps.
    // Slope filter values are MicroVerse units, not degrees: for this 1000 x 300 x 1000 terrain at 25-80 m,
    // S 32 ~ 18 deg, 36 ~ 21, 40 ~ 24, 45 ~ 27, 52 ~ 33, 54 ~ 35, 57 ~ 38 (derived from the normal-map shader).

    const string LayerFolder = DataFolder + "/TerrainLayers";
    const string RaygeasEnv = "Assets/_StoreAssets/Env/Raygeas/";

    // Footpaths (x, z), traced from the concept; width is set by the trail mask.
    public static readonly Vector2[][] Trails =
    {
        new[] { V(338,292), V(335,312), V(324,336), V(311,352) },                                                  // home gate -> bridge
        new[] { V(316,371), V(324,392), V(344,428), V(372,462), V(404,489), V(440,506), V(480,532), V(520,560), V(552,592), V(574,620) }, // forest -> swamp fringe
        new[] { V(344,428), V(318,468), V(298,506), V(286,540), V(278,566), V(268,592), V(248,614), V(216,628) },   // highlands spur -> west pond
        new[] { V(352,262), V(392,253), V(440,249), V(488,246), V(528,244) },                                       // meadow -> lakeside viewpoint
        new[] { V(372,462), V(420,446), V(470,430), V(520,410), V(565,392), V(600,378), V(626,362) },               // forest -> north lake shore
    };

    // Lake beach coves (sand instead of pebbles): (centre, radius).
    static readonly (Vector2 c, float r)[] SandCoves = { (V(540,262), 42f), (V(612,236), 30f), (V(700,214), 34f), (V(222,412), 16f) };

    public static float SandWeight(Vector2 p)
    {
        float sand = 0;
        foreach (var cove in SandCoves)
            sand = Mathf.Max(sand, 1 - SmoothStep(cove.r * 0.55f, cove.r, Vector2.Distance(p, cove.c) + 6f * Fbm(p.x / 14f, p.y / 14f, 2)));
        return sand;
    }

    public static float TrailDistancePublic(Vector2 p) => TrailDistance(p);

    static float TrailDistance(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var t in Trails)
            for (int i = 0; i < t.Length - 1; i++)
            {
                var ab = t[i + 1] - t[i];
                float u = Mathf.Clamp01(Vector2.Dot(p - t[i], ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, t[i] + ab * u));
            }
        return best;
    }

    // Writes MH_Tex_* masks (res x res, R16, north up like the base map).
    public static async Task<object> GenerateTextureMasks(int res)
    {
        var t0 = DateTime.Now;
        var names = new[] { "Meadow", "Highlands", "Swamp", "Shore", "Sand", "Bed", "Channel", "Bank", "BankPebble", "Trails" };
        var maps = names.ToDictionary(n => n, n => new float[res * res]);
        await Task.Run(() => Parallel.For(0, res, j =>
        {
            for (int i = 0; i < res; i++)
            {
                float x = (i + 0.5f) / res * Size, z = (j + 0.5f) / res * Size;
                var p = new Vector2(x, z);
                int k = j * res + i;
                maps["Meadow"][k] = Weight(SignedDistance(p, MeadowArea) + 8f * Fbm(x / 40f, z / 40f + 7f, 3), 14);
                maps["Highlands"][k] = Weight(SignedDistance(p, HighlandsArea) + 10f * Fbm(x / 50f + 3f, z / 50f, 3), 24);
                maps["Swamp"][k] = Weight(SignedDistance(p, SwampArea) + 6f * Fbm(x / 30f, z / 30f + 3f, 3), 14);

                // water bodies: same noisy shoreline as the height field
                float shore = 0, bed = 0, sand = 0;
                float groundH = HeightAt(x, z, out _);
                foreach (var b in Basins)
                {
                    if (b.bounds.width == 0) b.bounds = Bounds(b.shore.Select(v => new Vector4(v.x, v.y, 0, 0)), 120);
                    if (!b.bounds.Contains(p)) continue;
                    float d = SignedDistance(p, b.shore) + 3.5f * Fbm(x / 22f + b.level, z / 22f, 3);
                    if (d > 12f) continue;
                    // by height, so islands and coves follow the real waterline
                    float rel = groundH - b.level;
                    float band = (1 - SmoothStep(0.15f + 0.25f * Fbm(x / 9f, z / 9f, 2), 0.6f, rel)) * SmoothStep(-2.4f, -1.2f, rel);
                    shore = Mathf.Max(shore, band);
                    bed = Mathf.Max(bed, 1 - SmoothStep(-1.9f, -1.1f, rel));
                }
                foreach (var cove in SandCoves)
                    sand = Mathf.Max(sand, 1 - SmoothStep(cove.r * 0.55f, cove.r, Vector2.Distance(p, cove.c) + 6f * Fbm(x / 14f, z / 14f, 2)));
                maps["Shore"][k] = shore;
                maps["Bed"][k] = bed;
                maps["Sand"][k] = shore * sand;

                // rivers and streams: pebbly bed, dirt banks
                float edge = float.MaxValue;
                foreach (var l in Lines) edge = Mathf.Min(edge, TrueLineEdge(p, l));
                float wobble = 1.2f * Fbm(x / 7f + 2f, z / 7f, 2);
                maps["Channel"][k] = 1 - SmoothStep(-1.6f, 0.1f + 0.4f * wobble, edge);
                // banks: a thin ragged wet band that breaks up, so grass reaches the water on long stretches
                float patch = SmoothStep(-0.15f, 0.3f, Fbm(x / 16f, z / 16f + 40f, 3));
                float bankBand = SmoothStep(-0.6f, 0.2f, edge) * (1 - SmoothStep(0.5f + 0.5f * wobble, 1.4f + 1.0f * Mathf.Abs(wobble) + 1.2f * patch, edge));
                maps["Bank"][k] = bankBand * (0.35f + 0.65f * patch);
                maps["BankPebble"][k] = bankBand * SmoothStep(0.1f, 0.35f, Fbm(x / 9f, z / 9f + 77f, 2));

                // trails: ~2 m core with ragged soft edges, kept off the water
                float tw = 1.0f + 0.35f * Fbm(x / 6f, z / 6f + 11f, 2);
                maps["Trails"][k] = (1 - SmoothStep(tw, tw + 1.4f, TrailDistance(p))) * SmoothStep(0f, 1.5f, edge);
            }
        }));
        foreach (var kv in maps) WriteR16(MapFolder + "/MH_Tex_" + kv.Key + ".png", kv.Value, res, 1f);
        AssetDatabase.Refresh();
        foreach (var n in names) ConfigureImporter(MapFolder + "/MH_Tex_" + n + ".png", res);
        return new { seconds = (DateTime.Now - t0).TotalSeconds, masks = names };
    }

    // Copies of the vendor layers (owned tile size and tint), plus a new rock layer from Komorebi's rock detail texture.
    public static object BuildTerrainLayers()
    {
        EnsureFolder(LayerFolder);
        string gl = RaygeasEnv + "Grasslands - Stylized Nature/Assets/Terrain Layers/";
        var made = new List<string>
        {
            CopyLayer(gl + "Grass.terrainlayer", "MH_Grass", 3f, Vector4.one),
            CopyLayer(gl + "Grass_Blue.terrainlayer", "MH_GrassCool", 3f, Vector4.one),
            CopyLayer(gl + "Mud.terrainlayer", "MH_Dirt", 3f, Vector4.one),
            CopyLayer(gl + "Grass_Yellow.terrainlayer", "MH_GrassMeadow", 3f, new Vector4(0.95f, 0.92f, 1.0f, 1f)),    // about #609330, the BK Meadows lawn green
            CopyLayer(gl + "MudStones.terrainlayer", "MH_Pebbles", 3f, Vector4.one),
            CopyLayer(gl + "Mud.terrainlayer", "MH_WetMud", 3f, new Vector4(0.44f, 0.47f, 0.37f, 1f)),
            CopyLayer(gl + "Sand.terrainlayer", "MH_Sand", 4f, Vector4.one),
        };
        // pebbles: Komorebi's grey stone layer (the Grasslands MudStones read brown next to the grey Komorebi rocks)
        var kStones = AssetDatabase.LoadAssetAtPath<TerrainLayer>(RaygeasEnv + "Komorebi Nature/Assets/Terrain Layers/Stones_1.terrainlayer");
        var pebbles = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + "/MH_Pebbles.terrainlayer");
        if (kStones != null && pebbles != null)
        {
            pebbles.diffuseTexture = kStones.diffuseTexture; pebbles.normalMapTexture = kStones.normalMapTexture; pebbles.maskMapTexture = kStones.maskMapTexture;
            pebbles.normalScale = kStones.normalScale; pebbles.tileSize = kStones.tileSize; pebbles.diffuseRemapMax = kStones.diffuseRemapMax;
            EditorUtility.SetDirty(pebbles); AssetDatabase.SaveAssetIfDirty(pebbles);
        }
        string rock = LayerFolder + "/MH_Rock.terrainlayer";
        var rl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(rock);
        if (rl == null) { rl = new TerrainLayer(); AssetDatabase.CreateAsset(rl, rock); }
        string kr = RaygeasEnv + "Komorebi Nature/Assets/Textures/Rocks/";
        rl.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(kr + "Rock_Detail_1_Albedo.png");
        rl.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(kr + "Rock_Detail_1_Normal.png");
        rl.tileSize = new Vector2(10, 10);
        rl.diffuseRemapMax = new Vector4(0.40f, 0.45f, 0.49f, 1f);   // tint toward the Komorebi rock meshes (~#787F84)
        rl.normalScale = 1f;
        EditorUtility.SetDirty(rl); AssetDatabase.SaveAssetIfDirty(rl);
        made.Add(rock);
        return made;
    }

    static string CopyLayer(string source, string name, float tile, Vector4 tint)
    {
        string dest = LayerFolder + "/" + name + ".terrainlayer";
        if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(dest) == null && !AssetDatabase.CopyAsset(source, dest)) throw new ArgumentException("Copy failed " + source);
        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(dest);
        l.name = name;
        l.tileSize = new Vector2(tile, tile);
        l.diffuseRemapMax = tint;
        EditorUtility.SetDirty(l); AssetDatabase.SaveAssetIfDirty(l);
        return dest;
    }

    // Rebuilds the "Texture" group. Hierarchy order matters: the lower stamp wins.
    public static object BuildTextures()
    {
        var mv = MicroVerse.instance;
        var old = mv.transform.Find("Texture");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var g = Group(mv.transform, "Texture");
        TerrainLayer L(string n) => AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + "/" + n + ".terrainlayer") ?? throw new ArgumentException("Missing layer " + n + "; run BuildTerrainLayers");
        Texture2D M(string n) => LoadTex(MapFolder + "/MH_Tex_" + n + ".png");

        Stamp(g, "01 Base Grass", L("MH_Grass"));
        var cool = Stamp(g, "02 Cool Grass Patches", L("MH_GrassCool"));
        NoiseOn(cool.filterSet.weightNoise, 2, 22, 3f, 0.2f);       // weight = saturate(1 + (n - balance) * amplitude), n ~ +-0.7: ~40% cover
        var dirt = Stamp(g, "03 Forest Floor Dirt", L("MH_Dirt"));
        dirt.filterSet.weight = 0.6f;
        Range(dirt.filterSet.heightFilter, 0, 62, 0, 4);
        Range(dirt.filterSet.slopeFilter, 0, 45, 0, 6);
        NoiseOn(dirt.filterSet.weightNoise, 2, 45, 3f, 0.42f);
        Masked(Stamp(g, "04 Meadow Base", L("MH_Grass")), M("Meadow"));
        var meadow = Masked(Stamp(g, "05 Meadow Warm Grass", L("MH_GrassMeadow")), M("Meadow"));
        Range(meadow.filterSet.slopeFilter, 0, 32, 0, 8);
        NoiseOn(meadow.filterSet.weightNoise, 2, 14, 3f, 0.0f);
        var scree = Masked(Stamp(g, "06 Highland Scree", L("MH_Pebbles")), M("Highlands"));
        Range(scree.filterSet.slopeFilter, 40, 56, 5, 5);
        NoiseOn(scree.filterSet.weightNoise, 2, 35, 2.5f, 0.35f);
        var swampGrass = Masked(Stamp(g, "07a Swamp Grass", L("MH_GrassCool")), M("Swamp"));
        NoiseOn(swampGrass.filterSet.weightNoise, 2, 30, 2.5f, -0.2f);
        var swamp = Masked(Stamp(g, "07 Swamp Mud", L("MH_WetMud")), M("Swamp"));
        Range(swamp.filterSet.heightFilter, 0, SwampWater + 0.12f, 0, 0.18f);
        Masked(Stamp(g, "08 Water Beds", L("MH_WetMud")), M("Bed"));
        var shore = Masked(Stamp(g, "09 Shore Pebbles", L("MH_Pebbles")), M("Shore"));
        Range(shore.filterSet.slopeFilter, 0, 57, 0, 5);
        Masked(Stamp(g, "10 Beach Coves", L("MH_Sand")), M("Sand"));
        Masked(Stamp(g, "11 Channel Beds", L("MH_WetMud")), M("Channel"));   // dark bed: a pale pebble shelf read as a concrete curb through the water
        var bank = Masked(Stamp(g, "12 Channel Banks", L("MH_WetMud")), M("Bank"));
        Masked(Stamp(g, "12b Bank Pebbles", L("MH_Pebbles")), M("BankPebble"));
        NoiseOn(bank.filterSet.weightNoise, 2, 60, 1.2f, 0f);
        var rock = Stamp(g, "13 Steep Rock", L("MH_Rock"));
        Range(rock.filterSet.slopeFilter, 54, 95, 5, 0);
        NoiseOn(rock.filterSet.weightNoise, 2, 60, 0.5f, 0f);
        var shelves = Masked(Stamp(g, "14 Highland Rock Shelves", L("MH_Rock")), M("Highlands"));
        Range(shelves.filterSet.slopeFilter, 48, 95, 4, 0);
        NoiseOn(shelves.filterSet.weightNoise, 2, 25, 2f, 0.2f);
        Masked(Stamp(g, "15 Trails", L("MH_Dirt")), M("Trails"));

        foreach (var st in g.GetComponentsInChildren<Stamp>(true)) EditorUtility.SetDirty(st);
        mv.Invalidate(null, MicroVerse.InvalidateType.Splats);
        return g.GetComponentsInChildren<TextureStamp>().Select(t => t.name + ": " + t.layer.name).ToArray();
    }

    static TextureStamp Stamp(Transform parent, string name, TerrainLayer layer)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Meadow Home texture");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextureStamp>();
        t.layer = layer;
        t.stampVersion = 1;
        t.filterSet.version = 2;
        t.filterSet.falloffFilter.filterType = FalloffFilter.FilterType.Global;
        go.transform.position = new Vector3(Size / 2, 0, Size / 2);
        go.transform.localScale = new Vector3(Size, 1, Size);
        return t;
    }

    // Whole-terrain stamp limited by a terrain-space mask (stamp UV = terrain UV).
    static TextureStamp Masked(TextureStamp t, Texture2D mask)
    {
        var f = t.filterSet.falloffFilter;
        f.filterType = FalloffFilter.FilterType.Texture;
        f.texture = mask;
        f.textureChannel = FalloffFilter.TextureChannel.R;
        f.textureParams = new Vector2(1, 0);
        f.textureRotationScale = new Vector4(0, 1, 0, 0);
        f.falloffRange = new Vector2(0.8f, 1f);
        f.clampTexture = true;
        return t;
    }

    static void Range(FilterSet.Filter f, float lo, float hi, float sLo, float sHi)
    {
        f.enabled = true;
        f.filterType = FilterSet.Filter.FilterType.Simple;
        f.range = new Vector2(lo, hi);
        f.smoothness = new Vector2(sLo, sHi);
    }

    static void NoiseOn(JBooth.MicroVerseCore.Noise n, int type, float frequency, float amplitude, float balance)
    {
        n.noiseType = (JBooth.MicroVerseCore.Noise.NoiseType)type;
        n.noiseSpace = JBooth.MicroVerseCore.Noise.NoiseSpace.World;
        n.frequency = frequency;
        n.amplitude = amplitude;
        n.balance = balance;
    }

    // Local water surface around p for dressing: (surface, kind) with kind 0 = lake/pond/swamp, 1 = river, 2 = stream.
    // Considers water within 6 m of p; -inf when there is none.
    public static (float surface, int kind) WaterSurfaceAt(Vector2 p)
    {
        float best = float.MaxValue, surface = float.NegativeInfinity; int kind = -1;
        foreach (var b in Basins)
        {
            if (b.bounds.width == 0) b.bounds = Bounds(b.shore.Select(v => new Vector4(v.x, v.y, 0, 0)), 120);
            if (!b.bounds.Contains(p)) continue;
            float d = SignedDistance(p, b.shore) + 3.5f * Fbm(p.x / 22f + b.level, p.y / 22f, 3);
            if (d < 6f && d < best) { best = d; surface = b.level; kind = 0; }
        }
        float ds = SignedDistance(p, SwampArea);
        if (ds < 6f && ds < best) { best = ds; surface = SwampWater; kind = 0; }
        foreach (var l in Lines)
        {
            float e = float.MaxValue; float s = 0;
            for (int i = 0; i < l.pts.Length - 1; i++)
            {
                Vector4 a = l.pts[i], b = l.pts[i + 1];
                var ab = new Vector2(b.x - a.x, b.y - a.y);
                float t = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.y), ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                float d = Vector2.Distance(p, new Vector2(a.x, a.y) + ab * t) - Mathf.Lerp(a.w, b.w, t);
                if (d < e) { e = d; s = Mathf.Lerp(a.z, b.z, t); }
            }
            if (e < 6f && e < best) { best = e; surface = s; kind = l.name == "River" ? 1 : 2; }
        }
        return (surface, kind);
    }

    // Distance to the nearest lake/pond shoreline, inside or outside the water.
    public static float BasinShoreDistance(Vector2 p) => Basins.Min(b => Mathf.Abs(SignedDistance(p, b.shore)));

    // Region weights for vegetation (no water fade): x forest, y meadow, z highlands, w swamp.
    public static Vector4 VegWeights(Vector2 p)
    {
        float wHigh = Weight(SignedDistance(p, HighlandsArea) + 10f * Fbm(p.x / 50f + 3f, p.y / 50f, 3), 24);
        float wMeadow = Weight(SignedDistance(p, MeadowArea) + 8f * Fbm(p.x / 40f, p.y / 40f + 7f, 3), 14);
        float wSwamp = Weight(SignedDistance(p, SwampArea) + 6f * Fbm(p.x / 30f, p.y / 30f + 3f, 3), 14);
        float wForest = Mathf.Clamp01(1 - wHigh - wMeadow - wSwamp);
        return new Vector4(wForest, wMeadow, wHigh, wSwamp);
    }

    // Distance to the real waterline (noisy basin shores and channel widths) for masks and dressing.
    public static float WaterEdgeDistancePublic(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var b in Basins) best = Mathf.Min(best, Mathf.Max(0, SignedDistance(p, b.shore) + 3.5f * Fbm(p.x / 22f + b.level, p.y / 22f, 3)));
        foreach (var l in Lines) best = Mathf.Min(best, Mathf.Max(0, TrueLineEdge(p, l)));
        return best;
    }

    public static void WriteMask(string path, float[] data, int res)
    {
        WriteR16(path, data, res, 1f);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(path, res);
    }

    public static void EnsureFolderPublic(string path) => EnsureFolder(path);

    // ---------------- Output ----------------

    static void WriteR16(string path, float[] values, int res, float scale)
    {
        var data = new ushort[values.Length];
        for (int k = 0; k < values.Length; k++) data[k] = (ushort)Mathf.Clamp(Mathf.RoundToInt(values[k] / scale * 65535f), 0, 65535);
        var png = ImageConversion.EncodeArrayToPNG(data, GraphicsFormat.R16_UNorm, (uint)res, (uint)res);
        File.WriteAllBytes(path, png);
    }

    static void ConfigureImporter(string path, int res)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.SingleChannel;
        var ts = new TextureImporterSettings();
        imp.ReadTextureSettings(ts);
        ts.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
        imp.SetTextureSettings(ts);
        imp.sRGBTexture = false;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.isReadable = false;
        imp.npotScale = TextureImporterNPOTScale.None;
        var def = imp.GetDefaultPlatformTextureSettings();
        def.maxTextureSize = Mathf.NextPowerOfTwo(res);
        def.format = TextureImporterFormat.R16;
        def.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SetPlatformTextureSettings(def);
        var sa = imp.GetPlatformTextureSettings("Standalone");
        sa.overridden = true; sa.maxTextureSize = def.maxTextureSize; sa.format = TextureImporterFormat.R16; sa.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SetPlatformTextureSettings(sa);
        // not SaveAndReimport: only this importer is written, other dirty assets in memory stay untouched
        AssetDatabase.WriteImportSettingsIfDirty(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    static void WritePreview(string path, float[] h, float[] water, int res)
    {
        int o = 1024, step = res / o;
        var tex = new Texture2D(o, o, TextureFormat.RGB24, false);
        var light = new Vector3(-0.5f, 0.75f, 0.45f).normalized;
        float cell = Size / res * step;
        for (int j = 0; j < o; j++)
            for (int i = 0; i < o; i++)
            {
                int x = Mathf.Clamp(i * step, 1, res - 2), y = Mathf.Clamp(j * step, 1, res - 2);
                float c = h[y * res + x];
                float dx = (h[y * res + Mathf.Min(res - 1, x + step)] - h[y * res + Mathf.Max(0, x - step)]) / (2 * cell);
                float dz = (h[Mathf.Min(res - 1, y + step) * res + x] - h[Mathf.Max(0, y - step) * res + x]) / (2 * cell);
                var n = new Vector3(-dx, 1, -dz).normalized;
                float shade = Mathf.Clamp01(Vector3.Dot(n, light)) * 0.85f + 0.25f;
                Color col = c < 32 ? Color.Lerp(new Color(0.45f, 0.7f, 0.3f), new Color(0.35f, 0.55f, 0.25f), (c - 18) / 14f)
                    : c < 70 ? Color.Lerp(new Color(0.35f, 0.55f, 0.25f), new Color(0.6f, 0.55f, 0.4f), (c - 32) / 38f)
                    : Color.Lerp(new Color(0.6f, 0.55f, 0.45f), Color.white, (c - 70) / 180f);
                col *= shade;
                float ws = water[y * res + x];
                if (ws > c) col = Color.Lerp(new Color(0.3f, 0.65f, 0.85f), new Color(0.05f, 0.25f, 0.55f), Mathf.Clamp01((ws - c) / 8f));
                // contour every 5 m
                if (Mathf.Repeat(c, 5f) < 0.25f && ws <= c) col *= 0.8f;
                tex.SetPixel(i, j, col);
            }
        // playable outline + home base + bridge
        DrawPoly(tex, Playable, o, Color.red);
        Dot(tex, HomeBase, o, Color.yellow, 4);
        Dot(tex, Bridge, o, Color.magenta, 3);
        Dot(tex, Viewpoint, o, Color.cyan, 3);
        File.WriteAllBytes(path, tex.EncodeToPNG());
    }

    static void DrawPoly(Texture2D t, Vector2[] poly, int o, Color c)
    {
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i] / Size * o; var b = poly[(i + 1) % poly.Length] / Size * o;
            int n = (int)Vector2.Distance(a, b) + 1;
            for (int k = 0; k <= n; k++) { var q = Vector2.Lerp(a, b, k / (float)n); t.SetPixel((int)q.x, (int)q.y, c); }
        }
    }

    static void Dot(Texture2D t, Vector2 p, int o, Color c, int r)
    {
        var q = p / Size * o;
        for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) t.SetPixel((int)q.x + x, (int)q.y + y, c);
    }

    // ---------------- Math helpers ----------------

    static Vector2 V(float x, float z) => new Vector2(x, z);
    static Vector4 P(float x, float z, float s, float w) => new Vector4(x, z, s, w);
    static Vector4 A(float x, float z, float h, float r) => new Vector4(x, z, h, r);

    static Vector2[] Ellipse(Vector2 c, float rx, float rz, float rotDeg, int seed, int n)
    {
        var pts = new Vector2[24];
        float rot = rotDeg * Mathf.Deg2Rad;
        for (int i = 0; i < pts.Length; i++)
        {
            float a = i / (float)pts.Length * Mathf.PI * 2;
            float wobble = 1 + 0.12f * Mathf.Sin(a * 3 + seed) + 0.07f * Mathf.Sin(a * 5 + seed * 1.7f);
            var v = new Vector2(Mathf.Cos(a) * rx * wobble, Mathf.Sin(a) * rz * wobble);
            pts[i] = c + new Vector2(v.x * Mathf.Cos(rot) - v.y * Mathf.Sin(rot), v.x * Mathf.Sin(rot) + v.y * Mathf.Cos(rot));
        }
        return pts;
    }

    static Vector2 Centroid(Vector2[] poly) => new Vector2(poly.Average(v => v.x), poly.Average(v => v.y));

    static Rect Bounds(IEnumerable<Vector4> pts, float pad)
    {
        float x0 = pts.Min(v => v.x), x1 = pts.Max(v => v.x), z0 = pts.Min(v => v.y), z1 = pts.Max(v => v.y);
        return Rect.MinMaxRect(x0 - pad, z0 - pad, x1 + pad, z1 + pad);
    }

    static float LineDistance(Vector2 p, Line l) => LineDistance(p, l, out _);

    // Distance to the line and the normalised position along it (by knot index).
    static float LineDistance(Vector2 p, Line l, out float along)
    {
        float best = float.MaxValue; along = 0;
        for (int i = 0; i < l.pts.Length - 1; i++)
        {
            var a = new Vector2(l.pts[i].x, l.pts[i].y);
            var ab = new Vector2(l.pts[i + 1].x, l.pts[i + 1].y) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            float d = Vector2.Distance(p, a + ab * t);
            if (d < best) { best = d; along = (i + t) / (l.pts.Length - 1); }
        }
        return best;
    }

    // Catmull-Rom through the knots (xz), linear surface height and width, plus a gentle meander that is zero at both ends.
    static Vector4[] Smooth(Vector4[] knots, int sub, float meander, int seed)
    {
        var pts = new List<Vector4>();
        for (int i = 0; i < knots.Length - 1; i++)
        {
            Vector4 k0 = knots[Mathf.Max(0, i - 1)], k1 = knots[i], k2 = knots[i + 1], k3 = knots[Mathf.Min(knots.Length - 1, i + 2)];
            for (int s = 0; s < sub; s++)
            {
                float t = s / (float)sub;
                var xz = CatmullRom(new Vector2(k0.x, k0.y), new Vector2(k1.x, k1.y), new Vector2(k2.x, k2.y), new Vector2(k3.x, k3.y), t);
                pts.Add(new Vector4(xz.x, xz.y, Mathf.Lerp(k1.z, k2.z, t), Mathf.Lerp(k1.w, k2.w, t)));
            }
        }
        pts.Add(knots[knots.Length - 1]);
        if (meander <= 0) return pts.ToArray();
        var arc = new float[pts.Count];
        for (int i = 1; i < pts.Count; i++) arc[i] = arc[i - 1] + Vector2.Distance(new Vector2(pts[i].x, pts[i].y), new Vector2(pts[i - 1].x, pts[i - 1].y));
        var result = new Vector4[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[Mathf.Max(0, i - 1)]; var b = pts[Mathf.Min(pts.Count - 1, i + 1)];
            var tangent = new Vector2(b.x - a.x, b.y - a.y).normalized;
            var normal = new Vector2(-tangent.y, tangent.x);
            float env = Mathf.Sin(Mathf.PI * arc[i] / arc[arc.Length - 1]);
            var off = normal * (meander * env * Noise(arc[i] / 45f + seed * 3.7f, seed * 1.3f));
            result[i] = new Vector4(pts[i].x + off.x, pts[i].y + off.y, pts[i].z, pts[i].w);
        }
        return result;
    }

    static Vector2[] SmoothClosed(Vector2[] knots, int sub)
    {
        var pts = new List<Vector2>();
        int n = knots.Length;
        for (int i = 0; i < n; i++)
            for (int s = 0; s < sub; s++)
                pts.Add(CatmullRom(knots[(i - 1 + n) % n], knots[i], knots[(i + 1) % n], knots[(i + 2) % n], s / (float)sub));
        return pts.ToArray();
    }

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3);
    }

    static float Weight(float signedDistance, float blend) => 1 - SmoothStep(-blend, blend, signedDistance);

    public static float SmoothStep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3 - 2 * t);
    }

    static float SmoothMin(float a, float b, float k)
    {
        float h = Mathf.Max(k - Mathf.Abs(a - b), 0) / k;
        return Mathf.Min(a, b) - h * h * k * 0.25f;
    }

    // Signed distance to a closed polygon: negative inside.
    public static float SignedDistance(Vector2 p, Vector2[] poly)
    {
        float d = float.MaxValue; bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], b = poly[i];
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            d = Mathf.Min(d, (p - (a + ab * t)).sqrMagnitude);
            if (((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)) inside = !inside;
        }
        d = Mathf.Sqrt(d);
        return inside ? -d : d;
    }

    static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
        }
    }

    static float Gradient(int ix, int iy, float fx, float fy)
    {
        float a = Hash(ix, iy) * Mathf.PI * 2;
        return Mathf.Cos(a) * fx + Mathf.Sin(a) * fy;
    }

    // Gradient noise in about [-1, 1].
    public static float Noise(float x, float y)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        float u = fx * fx * fx * (fx * (fx * 6 - 15) + 10), v = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
        float n00 = Gradient(ix, iy, fx, fy), n10 = Gradient(ix + 1, iy, fx - 1, fy);
        float n01 = Gradient(ix, iy + 1, fx, fy - 1), n11 = Gradient(ix + 1, iy + 1, fx - 1, fy - 1);
        return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v) * 1.6f;
    }

    public static float Fbm(float x, float y, int octaves)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            sum += Noise(x, y) * amp; norm += amp;
            x = x * 2.03f + 17.1f; y = y * 2.03f - 9.3f; amp *= 0.5f;
        }
        return sum / norm;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var split = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}

// ---------------- Landmarks (second class: run_script compiles one file, and these need the layout data) ----------------
// Fenced home base (Lakefell house), campfire, yard props, river bridge, trail sign. Rebuilding replaces Environment/Landmarks.
public static class Landmarks
{
    const string LV = "Assets/_StoreAssets/Env/Raygeas/Lakefell Village/Assets/Prefabs/";
    const string HL = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/";
    const string GL = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/";
    const string KN = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/";

    static readonly Vector2 Home = new Vector2(326, 274);
    const float FenceHalf = 15f;
    static readonly Vector2 HouseAt = new Vector2(319.5f, 267f);
    const float HouseYaw = 45f;                       // door (local +Z) faces north-east, toward the campfire and the gate
    static readonly Vector2 Campfire = new Vector2(329.5f, 277.5f);
    const float GateX = 334f, GateWidth = 3.6f;      // gate in the north fence, near the north-east corner

    static Terrain terrain;
    static System.Random rng;
    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y));

    public static object Build(int seed)
    {
        terrain = Terrain.activeTerrain ?? throw new InvalidOperationException("No terrain.");
        rng = new System.Random(seed);
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Environment") ?? new GameObject("Environment");
        var root = env.transform.Find("Landmarks");
        if (root == null) { root = new GameObject("Landmarks").transform; root.SetParent(env.transform, false); Undo.RegisterCreatedObjectUndo(root.gameObject, "Landmarks"); }
        foreach (var n in new[] { "Home Base", "River Bridge", "Trail Signs" })   // the waterfall has its own builder
        {
            var old = root.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        var log = new List<string>();

        // ---- home base ----
        var home = Child(root, "Home Base");
        var house = Put(home, LV + "Buildings/House_1.prefab", HouseAt, HouseYaw, 1f, -0.05f);
        log.Add($"house at {house.transform.position:F1}");
        BuildFence(Child(home, "Fence"), log);

        var fire = Child(home, "Campfire");
        Put(fire, HL + "Camp/Bonfire.prefab", Campfire, R(0, 360), 1.3f, -0.02f);
        var fx = Put(fire, HL + "FX/Fire.prefab", Campfire, 0, 1f, 0.05f);
        foreach (var a in fx.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;   // BroAudio owns audio
        foreach (var l in fx.GetComponentsInChildren<Light>(true)) l.shadows = LightShadows.None;
        for (int i = 0; i < 9; i++)                                                            // stone ring
        {
            float ang = i / 9f * Mathf.PI * 2 + R(-0.12f, 0.12f);
            var p = Campfire + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R(0.85f, 0.95f);
            Put(fire, KN + "Rocks/SmallStone_" + (1 + rng.Next(4)) + ".prefab", p, R(0, 360), R(0.55f, 0.75f), -0.08f);
        }
        Seat(fire, LV + "Environment/Bench_1.prefab", 205, 2.5f);
        Seat(fire, LV + "Environment/Bench_1.prefab", 295, 2.5f);
        Seat(fire, LV + "Environment/Bench_1.prefab", 40, 2.5f);
        Put(fire, LV + "Environment/Log_2.prefab", Campfire + new Vector2(2.9f, -2.6f), 75, 1f, 0);   // firewood pile
        Put(fire, HL + "Camp/Log_1.prefab", Campfire + new Vector2(1.6f, -1.2f), R(0, 360), 1f, 0);
        Put(fire, HL + "Camp/Log_3.prefab", Campfire + new Vector2(1.9f, -0.9f), R(0, 360), 1f, 0);

        // door dressing: flower pots either side of the entrance stairs
        var yard = Child(home, "Yard Props");
        Vector2 Local(float x, float z) => HouseAt + Rot(new Vector2(x, z), HouseYaw);
        Put(yard, LV + "Environment/Pot_1_Red_Flowers_1.prefab", Local(-1.5f, 4.6f), R(0, 360), 1f, 0);
        Put(yard, LV + "Environment/Pot_1_Blue_Flowers_1.prefab", Local(1.5f, 4.7f), R(0, 360), 1f, 0);
        Put(yard, LV + "Environment/Pot_2_Red_Flowers_1.prefab", Local(2.2f, 4.3f), R(0, 360), 1f, 0);
        Put(yard, LV + "Environment/Flowers_Flowerpot_1.prefab", Local(-4.0f, 4.5f), HouseYaw, 1f, 0);
        // woodpile against the east gable
        for (int i = 0; i < 4; i++) Put(yard, LV + "Environment/Log_2.prefab", Local(7.3f, -2.4f + i * 0.95f), HouseYaw + 90 + R(-4, 4), 1f, 0);
        for (int i = 0; i < 3; i++) Put(yard, LV + "Environment/Log_1.prefab", Local(7.3f, -1.9f + i * 0.95f), HouseYaw + 90 + R(-4, 4), 1f, 0.38f);
        Put(yard, LV + "Environment/Stump_1.prefab", Local(8.6f, 1.8f), R(0, 360), 1.3f, 0);
        // storage corner by the west gable
        Put(yard, LV + "Environment/Barrel_1.prefab", Local(-7.0f, 2.2f), R(0, 360), 1f, 0);
        Put(yard, LV + "Environment/Barrel_1.prefab", Local(-7.1f, 1.3f), R(0, 360), 1f, 0);
        Put(yard, LV + "Environment/Box_1.prefab", Local(-7.2f, 0.2f), HouseYaw + R(-8, 8), 1f, 0);
        Put(yard, LV + "Environment/Sack_3.prefab", Local(-6.6f, -0.6f), R(0, 360), 1f, 0);
        Put(yard, LV + "Environment/Sack_2.prefab", Local(-7.3f, -1.2f), R(0, 360), 1f, 0);
        Put(yard, LV + "Environment/Cart_1.prefab", new Vector2(Home.x - 11.5f, Home.y + 9.5f), 110, 1f, 0);
        Put(yard, LV + "Environment/Chest_1.prefab", Local(4.8f, 3.4f), HouseYaw + 180, 1f, 0);
        Put(yard, LV + "Environment/Trough_1.prefab", new Vector2(Home.x + 10.5f, Home.y - 10.0f), 30, 1f, 0);
        Put(yard, LV + "Environment/StreetLamp_1.prefab", new Vector2(GateX - GateWidth / 2 - 0.9f, Home.y + FenceHalf - 1.2f), 180, 1f, 0);

        // ---- bridge over the river, square to the flow ----
        var bridgeRoot = Child(root, "River Bridge");
        var (pos, along) = RiverPoint(new Vector2(312, 362));
        var axis = new Vector2(-along.y, along.x);              // across the river
        float yaw = Mathf.Atan2(axis.x, axis.y) * Mathf.Rad2Deg;
        const float scale = 1.25f;                              // 20 m span lands on the banks
        float half = 16.1f * scale * 0.5f - 1.2f;
        float bankA = Ground(pos + axis * half), bankB = Ground(pos - axis * half);
        var bridge = Put(bridgeRoot, GL + "Environment/Bridge.prefab", pos, yaw, scale, 0);
        bridge.transform.position = new Vector3(pos.x, Mathf.Max(bankA, bankB) - 0.15f, pos.y);
        log.Add($"bridge at {bridge.transform.position:F1} yaw {yaw:0} banks {bankA:0.0}/{bankB:0.0} river {pos}");

        // trail sign at the forest fork
        Put(Child(root, "Trail Signs"), LV + "Environment/SignPost_1.prefab", new Vector2(339, 421), 200, 1f, 0);

        foreach (var go in root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).Where(g => g.GetComponentInParent<StylizedWater3.WaterObject>() == null))
            if (go.GetComponentInChildren<ParticleSystem>(true) == null && go.GetComponent<Light>() == null)
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
        EditorSceneManager.MarkSceneDirty(scene);
        return log;
    }

    // ---- waterfall (Komorebi kit on rock meshes; recipe scaled from the Komorebi demo's falls) ----
    const string KNP = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/";

    // (px, pz) = where the sheet meets the pond (pivot), heading = flow direction (deg), top = source water surface, pond = pond surface.
    public static object BuildWaterfall(float px, float pz, float heading, float top, float pond)
    {
        terrain = Terrain.activeTerrain;
        rng = new System.Random(11);
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().First(g => g.name == "Environment");
        var lm = env.transform.Find("Landmarks") ?? Child(env.transform, "Landmarks");
        var old = lm.Find("Waterfall");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = Child(lm, "Waterfall");
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Waterfall");

        float a = heading * Mathf.Deg2Rad;
        var f = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        var right = new Vector2(f.y, -f.x);
        var P = new Vector2(px, pz);
        float sy = (top - pond) / (6.47f - 0.27f), sx = 1.15f, sz = 3f;
        var log = new List<string>();

        // falling sheet: Waterfall_1 with a material copy drawn after the SW3 water (both default to queue 3000)
        var fall = Spawn(root, KNP + "Nature/Waterfall_1.prefab", new Vector3(P.x, pond - 0.27f * sy, P.y), Quaternion.Euler(0.8f, heading, 0), new Vector3(sx, sy, sz));
        var mat = WaterfallMaterial();
        foreach (var r in fall.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = mat;
        log.Add($"sheet pivot {fall.transform.position:F2} scale ({sx}, {sy:0.00}, {sz}) approach top {fall.transform.position.y + 6.47f * sy:0.00}");

        // lip slab: flat-topped NoGrass rock, its top ~1 m under the source water, centre ~14 m upstream
        float slabScale = 1.25f;
        var slabC = P - f * (11.5f * slabScale);
        Spawn(root, KNP + "Rocks/Rock_1_NoGrass.prefab", new Vector3(slabC.x, top - 1.0f - 13.78f * slabScale, slabC.y), Quaternion.Euler(0, heading - 143.3f, 0), Vector3.one * slabScale);
        // a second NoGrass slab behind it, a little higher, so the source pool sits in a rocky basin
        var slab2 = P - f * 30f + right * 4f;
        Spawn(root, KNP + "Rocks/Rock_1_NoGrass.prefab", new Vector3(slab2.x, top + 0.6f - 13.78f * 1.3f, slab2.y), Quaternion.Euler(0, heading + 60f, 0), Vector3.one * 1.3f);

        // notch walls: grass-topped rocks either side, inner faces ~4.5 m from the sheet, tops 3-9 m above the source
        var leftWall = P + f * 0.5f - right * 14.5f;
        Spawn(root, KNP + "Rocks Grass/Rock_1.prefab", new Vector3(leftWall.x, top + 3.0f - 13.78f * 1.05f, leftWall.y), Quaternion.Euler(R(-3, 3), heading + 25f, R(-3, 3)), Vector3.one * 1.05f);
        var leftUp = P - f * 6f - right * 17f;
        Spawn(root, KNP + "Rocks Grass/Rock_1.prefab", new Vector3(leftUp.x, top + 9.5f - 13.78f * 0.95f, leftUp.y), Quaternion.Euler(R(-3, 3), heading - 70f, R(-3, 3)), Vector3.one * 0.95f);
        var rightWall = P - f * 3f + right * 15.5f;
        Spawn(root, KNP + "Rocks Grass/Rock_3.prefab", new Vector3(rightWall.x, top + 4.5f - 17.9f * 1.15f, rightWall.y), Quaternion.Euler(R(-3, 3), heading + 160f, R(-3, 3)), new Vector3(1.35f, 1.15f, 1.35f));
        var rightLow = P + f * 7f + right * 12f;
        Spawn(root, KNP + "Rocks Grass/Rock_2.prefab", new Vector3(rightLow.x, pond + 2.2f - 8.2f * 1.1f, rightLow.y), Quaternion.Euler(R(-4, 4), heading + 95f, R(-4, 4)), Vector3.one * 1.1f);
        var leftLow = P + f * 8f - right * 11f;
        Spawn(root, KNP + "Rocks Grass/Rock_2.prefab", new Vector3(leftLow.x, pond + 1.4f - 8.2f * 0.9f, leftLow.y), Quaternion.Euler(R(-4, 4), heading - 40f, R(-4, 4)), Vector3.one * 0.9f);

        // foot of the fall: ripples, splash, fog, and Komorebi's blue light sunk in the pool floor
        var rip = P - f * (0.52f * sz);
        Spawn(root, KNP + "Nature/Waterfall_Ripples_1.prefab", new Vector3(rip.x, pond + 0.05f, rip.y), Quaternion.Euler(0, heading + 180f, 0), Vector3.one * 1.9f);
        var spl = P + f * 0.1f + right * 0.25f;
        Spawn(root, KNP + "Particle Systems/Waterfall_Splash_1.prefab", new Vector3(spl.x, pond - 0.35f, spl.y), Quaternion.Euler(0, heading, 0), Vector3.one * 1.35f);
        var fogAt = P + f * 2.4f;
        Spawn(root, KNP + "Particle Systems/Fog_1.prefab", new Vector3(fogAt.x, pond + 0.7f, fogAt.y), Quaternion.Euler(0, R(0, 360), 0), new Vector3(1.7f, 0.56f, 1.7f));
        var lightPos = P + f * 7.5f;
        var lightGo = new GameObject("Pool Glow (point light)");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.position = new Vector3(lightPos.x, Ground(lightPos) - 0.4f, lightPos.y);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(0.44f, 0.75f, 1f); light.intensity = 40f; light.range = 26f; light.shadows = LightShadows.None;

        // source pool on top of the lip (flat SW3 water); its downstream end sits under the faded approach of the sheet
        var src = new[] { P - f * 3.5f - right * 3.6f, P - f * 3.5f + right * 3.6f, P - f * 26f + right * 7f, P - f * 30f, P - f * 26f - right * 7f };
        var stillMat = AssetDatabase.LoadAssetAtPath<Material>(MeadowHomeLayout.DataFolder + "/Water/MH_Water_Still.mat");
        if (stillMat != null)
        {
            var c = src.Aggregate(Vector2.zero, (acc, v) => acc + v) / src.Length;
            var mesh = MeadowHomeLayout.FlatWaterMeshPublic("MH_WaterfallSource", src, 0.5f, 1f, (x, z) => true);
            var wo = StylizedWater3.WaterObject.New(stillMat, mesh);
            wo.name = "Waterfall Source Pool";
            wo.transform.SetParent(root, false);
            wo.transform.position = new Vector3(c.x, top, c.y);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        return log;
    }

    static Material WaterfallMaterial()
    {
        string dest = MeadowHomeLayout.DataFolder + "/Water/MH_Waterfall.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(dest);
        if (m == null)
        {
            AssetDatabase.CopyAsset("Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Materials/Nature/Waterfall_1.mat", dest);
            m = AssetDatabase.LoadAssetAtPath<Material>(dest);
        }
        if (m.HasProperty("_QueueOffset")) m.SetFloat("_QueueOffset", 1f);   // URP "Sorting Priority": after SW3 water (3000)
        m.SetFloat("_Flow_Speed_Multiplier", 0.42f);   // vendor 0.6: a gentler, cozier fall
        m.SetFloat("_Vertex_Flow_Speed", 1.0f);
        m.renderQueue = 3001;
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
        return m;
    }

    static GameObject Spawn(Transform parent, string path, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new ArgumentException("Missing prefab " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        return go;
    }

    static void BuildFence(Transform parent, List<string> log)
    {
        string[] panels = { LV + "Environment/Fence_1.prefab", LV + "Environment/Fence_2.prefab", LV + "Environment/Fence_3.prefab" };
        var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) }.Select(c => Home + c * FenceHalf).ToArray();
        int count = 0;
        for (int side = 0; side < 4; side++)
        {
            var a = corners[side]; var b = corners[(side + 1) % 4];
            float len = Vector2.Distance(a, b);
            var dir = (b - a) / len;
            int n = Mathf.RoundToInt(len / 2.25f);
            float seg = len / n;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - 90;   // panel length runs along local X
            for (int i = 0; i < n; i++)
            {
                var mid = a + dir * (seg * (i + 0.5f));
                bool north = side == 2;
                if (north && Mathf.Abs(mid.x - GateX) < GateWidth / 2) continue;   // gate opening
                var p = Put(parent, panels[rng.Next(panels.Length)], mid, yaw + (rng.Next(2) == 0 ? 0 : 180), 1f, -0.05f);
                p.transform.localScale = new Vector3(seg / 2.25f, 1f, 1f);
                count++;
            }
            for (int i = 0; i <= n; i++)
            {
                var at = a + dir * (seg * i);
                if (side == 2 && Mathf.Abs(at.x - GateX) < GateWidth / 2 - 0.2f) continue;
                Put(parent, LV + "Environment/FenceEnd_1.prefab", at, R(0, 360), 1.15f, -0.05f);
            }
        }
        log.Add($"fence: {count} panels around {FenceHalf * 2} m square, gate at x {GateX}");
    }

    static void Seat(Transform parent, string path, float angleDeg, float radius)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        var p = Campfire + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
        Put(parent, path, p, angleDeg + 90 + R(-6, 6), 1f, 0);   // long side faces the fire
    }

    static GameObject Put(Transform parent, string path, Vector2 at, float yaw, float scale, float yOffset)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new ArgumentException("Missing prefab " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(new Vector3(at.x, Ground(at) + yOffset, at.y), Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static Vector2 Rot(Vector2 v, float yawDeg)
    {
        float a = yawDeg * Mathf.Deg2Rad;
        return new Vector2(v.x * Mathf.Cos(a) + v.y * Mathf.Sin(a), -v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));
    }

    // Closest point on the river polyline (the same smoothed knots the terrain was carved with) and the flow direction there.
    static (Vector2 pos, Vector2 along) RiverPoint(Vector2 near)
    {
        var river = MeadowHomeLayout.Lines.First(l => l.name == "River").pts.Select(k => new Vector2(k.x, k.y)).ToArray();
        float best = float.MaxValue; Vector2 bp = near, bd = Vector2.right;
        for (int i = 0; i < river.Length - 1; i++)
        {
            var a = river[i]; var b = river[i + 1];
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(near - a, ab) / ab.sqrMagnitude);
            var q = a + ab * t;
            float d = Vector2.Distance(near, q);
            if (d < best) { best = d; bp = q; bd = ab.normalized; }
        }
        return (bp, bd);
    }
}

// ---------------- Water-edge dressing (third class; same reason as Landmarks) ----------------
// Shore stone clusters and lily groups, deterministic from a seed. Rebuilding replaces Environment/Dressing/<group>.
public static class Dressing
{
    const string KN = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/";
    static Terrain terrain;
    static System.Random rng;
    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static float H(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y));

    public static object BuildWaterEdges(int seed)
    {
        terrain = Terrain.activeTerrain;
        rng = new System.Random(seed);
        var root = Group("Water Edges");
        var stones = new GameObject("Shore Stones").transform; stones.SetParent(root, false);
        var lilies = new GameObject("Lilies").transform; lilies.SetParent(root, false);
        var pebbles = new GameObject("Pebbles").transform; pebbles.SetParent(root, false);
        var placed = new List<Vector3>();   // x, z, radius
        bool Free(Vector2 p, float r) => placed.All(q => (new Vector2(q.x, q.y) - p).sqrMagnitude > (q.z + r) * (q.z + r));
        int nStones = 0, nLilies = 0, nPebbles = 0;

        // shoreline samples: points where the terrain crosses the local water surface
        var shore = new List<(Vector2 p, float s, float scale)>();
        for (int i = 0; i < 260000 && shore.Count < 9000; i++)
        {
            var p = new Vector2(R(160, 860), R(190, 860));
            var (s, kind) = MeadowHomeLayout.WaterSurfaceAt(p);
            if (float.IsNegativeInfinity(s)) continue;
            float h = H(p);
            if (h < s - 0.45f || h > s + 0.35f) continue;
            if (kind != 0) continue;                                     // rivers and streams: BuildBanks walks their banks
            float scale = 1f;
            shore.Add((p, s, scale));
        }

        // stone clusters: 1 anchor + 3-12 companions within ~6 m, every 25-45 m of shoreline on average
        var centres = new List<Vector2>();
        foreach (var c in shore.OrderBy(_ => rng.Next()))
        {
            if (centres.Any(q => (q - c.p).sqrMagnitude < 26f * 26f)) continue;
            if (rng.NextDouble() < 0.35) { centres.Add(c.p); continue; }   // leave some shore bare
            centres.Add(c.p);
            int n = 3 + rng.Next(10);
            var members = shore.Where(q => (q.p - c.p).sqrMagnitude < 7f * 7f).OrderBy(_ => rng.Next()).Take(n + 1).ToList();
            for (int k = 0; k < members.Count; k++)
            {
                var m = members[k];
                float sc = (k == 0 ? R(2.6f, 4.4f) : R(1.3f, 2.8f)) * m.scale;
                float rad = 0.45f * sc;
                if (!Free(m.p, rad * 0.8f)) continue;
                string path = KN + "Rocks/SmallStone_" + (1 + rng.Next(4)) + ".prefab";
                var go = Put(stones, path, new Vector3(m.p.x, H(m.p) - R(0.05f, 0.35f) * sc * 0.4f, m.p.y), Quaternion.Euler(R(-18, 18), R(0, 360), R(-18, 18)), sc);
                placed.Add(new Vector3(m.p.x, m.p.y, rad));
                nStones++;
                // a few pebbles around each stone
                for (int j = 0; j < 2 + rng.Next(3); j++)
                {
                    var q = m.p + new Vector2(R(-2.2f, 2.2f), R(-2.2f, 2.2f)) * m.scale;
                    float qs = R(0.6f, 1.2f) * m.scale;
                    if (!Free(q, 0.2f * qs)) continue;
                    Put(pebbles, KN + "Rocks/SmallStone_" + (5 + rng.Next(2)) + ".prefab", new Vector3(q.x, H(q) - 0.04f, q.y), Quaternion.Euler(R(-6, 6), R(0, 360), R(-6, 6)), qs);
                    nPebbles++;
                }
            }
        }

        // lilies: calm water 0.9-2.2 m deep, as in the Komorebi demo, so the hanging stems end in the bed; clustered,
        // none near the waterfall plunge. The river mouth is slow enough for a few groups.
        var fallAt = new Vector2(206, 433);
        foreach (var (name, centre, radius, count) in new[] { ("pond", new Vector2(222, 426), 24f, 7), ("swamp", new Vector2(660, 705), 140f, 34), ("lake", new Vector2(640, 330), 170f, 16), ("westpond", new Vector2(192, 640), 18f, 4), ("northpond", new Vector2(386, 806), 34f, 6), ("mouth", new Vector2(512, 291), 24f, 10) })
        {
            int made = 0;
            for (int i = 0; i < 4000 && made < count; i++)
            {
                var p = centre + new Vector2(R(-radius, radius), R(-radius, radius));
                var (s, kind) = MeadowHomeLayout.WaterSurfaceAt(p);
                if (float.IsNegativeInfinity(s) || (kind != 0 && !(name == "mouth" && kind == 1))) continue;
                float depth = s - H(p);
                if (depth < 0.9f || depth > 2.2f) continue;
                if (name == "mouth" && MeadowHomeLayout.WaterEdgeDistancePublic(p) > 0) continue;
                if ((p - fallAt).sqrMagnitude < 12f * 12f) continue;
                if (name == "lake" && MeadowHomeLayout.BasinShoreDistance(p) > 22f) continue;   // keep to the coves
                if (!Free(p, 1.6f)) continue;
                int g = 1 + rng.Next(4);
                Put(lilies, KN + "Nature/WaterLily_Group_" + g + ".prefab", new Vector3(p.x, s + 0.015f, p.y), Quaternion.Euler(0, R(0, 360), 0), R(0.8f, 1.0f));
                placed.Add(new Vector3(p.x, p.y, 1.4f));
                made++; nLilies++;
                if (rng.NextDouble() < 0.5)   // a flower next to some groups
                {
                    var q = p + new Vector2(R(-1.6f, 1.6f), R(-1.6f, 1.6f));
                    float dq = s - H(q);
                    if (dq > 0.9f && dq < 2.2f) Put(lilies, KN + "Nature/WaterLily_Flower_1.prefab", new Vector3(q.x, s + 0.02f, q.y), Quaternion.Euler(0, R(0, 360), 0), R(0.9f, 1.2f));
                }
            }
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return new { shoreSamples = shore.Count, stones = nStones, pebbles = nPebbles, lilies = nLilies };
    }

    // River and stream banks, walked along each water line (vendor demos line 33-145 stones per 100 m of bank; we had ~7).
    // Each bank sample gets a character c: +1 cut bank (outer bend, steep, rock-lined), -1 bar (inner bend, low, few stones).
    // Lining stones come in runs with gaps; set-back boulders, stones in the channel, driftwood, a rock wall at the bridge,
    // and two fallen-log footbridges where trails cross streams.
    public static object BuildBanks(int seed)
    {
        terrain = Terrain.activeTerrain;
        rng = new System.Random(seed);
        const string HL = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
        const string GV = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Vegetation/";
        var root = Group("Bank Rocks");
        Transform Sub(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }
        var lining = Sub("Lining"); var boulders = Sub("Boulders"); var inChannel = Sub("In Channel"); var wood = Sub("Driftwood"); var hero = Sub("Hero");
        var td = terrain.terrainData;
        Vector3 Normal(Vector2 p) => td.GetInterpolatedNormal(p.x / td.size.x, p.y / td.size.z);
        Quaternion OnGround(Vector2 p, float tilt) => Quaternion.FromToRotation(Vector3.up, Normal(p)) * Quaternion.Euler(R(-tilt, tilt), R(0, 360), R(-tilt, tilt));
        var bridge = MeadowHomeLayout.Bridge;
        int nLining = 0, nBoulder = 0, nChannel = 0, nWood = 0;

        string[] knSmall = { KN + "Rocks/SmallStone_1.prefab", KN + "Rocks/SmallStone_2.prefab", KN + "Rocks/SmallStone_3.prefab" };
        string[] hlStones = { HL + "Stone_2.prefab", HL + "Stone_3.prefab", HL + "Stone_4.prefab", HL + "Stone_4.prefab" };
        string KnSmall() => rng.NextDouble() < 0.08 ? KN + "Rocks/SmallStone_4.prefab" : knSmall[rng.Next(knSmall.Length)];

        for (int li = 0; li < MeadowHomeLayout.Lines.Length; li++)
        {
            var l = MeadowHomeLayout.Lines[li];
            bool river = l.name == "River";
            var pts = l.pts;
            var cum = new float[pts.Length];
            for (int i = 1; i < pts.Length; i++) cum[i] = cum[i - 1] + Vector2.Distance(new Vector2(pts[i - 1].x, pts[i - 1].y), new Vector2(pts[i].x, pts[i].y));
            float total = cum[pts.Length - 1];
            (Vector2 c, Vector2 tan, float w, float surf) At(float s)
            {
                s = Mathf.Clamp(s, 0, total);
                int i = Mathf.Clamp(Array.FindLastIndex(cum, v => v <= s), 0, pts.Length - 2);
                float t = (s - cum[i]) / Mathf.Max(1e-3f, cum[i + 1] - cum[i]);
                var a = pts[i]; var b = pts[i + 1];
                var v4 = Vector4.Lerp(a, b, t);
                return (new Vector2(v4.x, v4.y), new Vector2(b.x - a.x, b.y - a.y).normalized, v4.w, v4.z);   // pts = (x, z, surface, half width)
            }
            float Heading(float s) { var (_, tan, _, _) = At(s); return Mathf.Atan2(tan.y, tan.x); }

            foreach (int side in new[] { 1, -1 })
            {
                bool inRun = rng.NextDouble() < 0.5; float left = R(2, 8);
                float s = R(1, 3);
                while (s < total - 2)
                {
                    var (c, tan, w, surf) = At(s);
                    var n = new Vector2(-tan.y, tan.x) * side;                       // from the channel centre toward this bank
                    // real edge: march out until the ground rises above the water
                    float e = w; bool found = false;
                    for (float d = 0.3f * w; d < w * 2.2f + 3f; d += 0.25f) if (H(c + n * d) > surf) { e = d; found = true; break; }
                    var edgeP = c + n * e;
                    float width = 2 * w;
                    // bank character from bend direction plus alternating bars
                    float win = Mathf.Max(6f, 1.5f * width);
                    float dh = Mathf.DeltaAngle(Heading(s - win) * Mathf.Rad2Deg, Heading(s + win) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                    float kappa = dh / (2 * win);
                    float bend = Mathf.Clamp(-side * kappa * width / 0.2f, -1, 1);
                    float bar = side * Mathf.Clamp(2.5f * MeadowHomeLayout.Fbm(s / (3.5f * width) + 3.1f, 7.7f + li, 2), -1, 1);
                    float cc = Mathf.Clamp(0.6f * bend + 0.9f * bar, -1, 1);
                    if (river && Vector2.Distance(edgeP, new Vector2(320, 364)) < 25f) cc = 1f;                                   // cut bank at the bridge
                    if (river && edgeP.x > 483 && edgeP.x < 529 && edgeP.y > 288) cc = -1f;                                     // reed bar at the mouth
                    bool skip = !found || Vector2.Distance(edgeP, bridge) < 9f || MeadowHomeLayout.BasinShoreDistance(edgeP) < 4f || MeadowHomeLayout.TrailDistancePublic(edgeP) < 1.8f;
                    bool highland = MeadowHomeLayout.VegWeights(edgeP).z > 0.5f;
                    float density = cc > 0.3f ? (river ? 60 : 70) : cc < -0.3f ? (river ? 10 : 20) : (river ? 40 : 45);   // stones per 100 m

                    // runs 4-14 m with 1-5 m gaps (longer gaps on bars)
                    float step = 100f / (density / 0.72f);
                    if (!skip && inRun)
                    {
                        float u = (float)rng.NextDouble();
                        float d = u < 0.2f ? R(-2.5f, -1f) : u < 0.7f ? R(-1f, 0.4f) : u < 0.9f ? R(0.4f, 2f) : R(2f, 4f);
                        if (!river) d *= 0.6f;
                        var p = edgeP + n * d + tan * R(-0.6f, 0.6f);
                        if (highland && !river)
                        {
                            float sc = R(0.8f, 1.7f);
                            Put(lining, hlStones[rng.Next(hlStones.Length)], new Vector3(p.x, H(p) - 0.5f * sc / 1.2f, p.y), OnGround(p, 3), sc);
                        }
                        else
                        {
                            float sc = river ? R(1.8f, 3.0f) : R(1.3f, 2.2f);
                            Put(lining, KnSmall(), new Vector3(p.x, H(p) - 0.06f * sc - R(0.02f, 0.1f), p.y), OnGround(p, 5), sc);
                        }
                        nLining++;
                    }
                    left -= step;
                    if (left <= 0) { inRun = !inRun; left = inRun ? R(4, 14) : (cc < -0.3f ? R(4, 12) : R(1, 5)); }

                    // set-back boulders: a few per 100 m, more on cut banks
                    float bDensity = cc > 0.3f ? (river ? 4 : 3) : cc < -0.3f ? (river ? 0.5f : 1) : 2;
                    if (!skip && rng.NextDouble() < bDensity * step / 100f)
                    {
                        if (river || !highland)
                        {
                            var p = edgeP + n * R(-1f, 1.5f);
                            string[] big = { KN + "Rocks/Stone_3_NoGrass.prefab", KN + "Rocks/Stone_4_NoGrass.prefab", KN + "Rocks/Stone_7_NoGrass.prefab" };
                            float[] tops = { 1.7f, 1.4f, 2.7f };
                            int k = rng.Next(big.Length);
                            float sc = R(0.5f, 0.9f);
                            Put(boulders, big[k], new Vector3(p.x, H(p) - 0.42f * tops[k] * sc, p.y), Quaternion.FromToRotation(Vector3.up, Normal(p)) * Quaternion.Euler(R(-13, 13), R(0, 360), R(-13, 13)), sc);
                        }
                        else
                        {
                            var p = edgeP + n * R(1.5f, 7f);
                            float sc = R(0.8f, 1.4f);
                            Put(boulders, HL + (rng.Next(2) == 0 ? "Rock_1.prefab" : "Rock_2.prefab"), new Vector3(p.x, H(p) - 1.4f * sc * 0.5f, p.y), OnGround(p, 22), sc);
                        }
                        nBoulder++;
                    }

                    // driftwood: more on bars
                    float wDensity = cc < -0.3f ? 3 : 1;
                    if (!skip && rng.NextDouble() < wDensity * step / 100f)
                    {
                        var p = edgeP + n * (river ? R(-0.5f, 1.5f) : R(0f, 4f));
                        string[] logs = river ? new[] { HL + "Branch_2.prefab", HL + "Branches_2.prefab", GV + "Big_Branch_1.prefab" }
                                              : new[] { HL + "Branch_1.prefab", HL + "Branch_2.prefab", HL + "Branch_3.prefab", HL + "Branches_1.prefab", HL + "Branches_2.prefab", HL + "Log_1.prefab" };
                        Put(wood, logs[rng.Next(logs.Length)], new Vector3(p.x, H(p) - 0.05f, p.y), OnGround(p, 4), R(0.9f, 1.4f));
                        nWood++;
                    }
                    s += step;
                }
            }

            // stones in the channel: river ~6 per 100 m (biased below the bridge), streams ~15
            int want = Mathf.RoundToInt(total / 100f * (river ? 6 : 15));
            for (int i = 0, made = 0; i < want * 30 && made < want; i++)
            {
                float s = river && rng.NextDouble() < 0.4 ? R(100, 220) : R(3, total - 3);
                var (c, tan, w, surf) = At(s);
                var p = c + new Vector2(-tan.y, tan.x) * R(-0.85f, 0.85f) * w;
                if (Vector2.Distance(p, bridge) < 9f || MeadowHomeLayout.BasinShoreDistance(p) < 4f) continue;
                float depth = surf - H(p);
                bool highland = MeadowHomeLayout.VegWeights(p).z > 0.5f;
                if (river ? (depth < 0.3f || depth > 1.2f) : (depth < 0.15f || depth > 0.8f)) continue;
                if (highland && !river)
                {
                    float sc = R(1.0f, 1.6f);
                    Put(inChannel, hlStones[rng.Next(hlStones.Length)], new Vector3(p.x, H(p) - 0.3f * sc, p.y), OnGround(p, 8), sc);
                }
                else
                {
                    float sc = river ? R(2.5f, 3.5f) : R(1.6f, 2.4f);
                    Put(inChannel, KnSmall(), new Vector3(p.x, H(p) - 0.04f * sc, p.y), OnGround(p, 8), sc);
                }
                made++; nChannel++;
            }
        }

        // cut bank at the bridge: a grass-topped rock wall on the outer bank just downstream, toe boulders below it
        var wallAt = new[] { new Vector2(326, 368), new Vector2(334, 364), new Vector2(342, 360) };
        var wallKinds = new[] { (KN + "Rocks Grass/Stone_1.prefab", 1.6f), (KN + "Rocks Grass/Stone_2.prefab", 3.2f), (KN + "Rocks Grass/Stone_5.prefab", 3.9f) };
        foreach (var q in wallAt)
        {
            var (s, kind) = MeadowHomeLayout.WaterSurfaceAt(q);
            if (float.IsNegativeInfinity(s)) continue;
            var (path, top) = wallKinds[rng.Next(wallKinds.Length)];
            float sc = R(0.45f, 0.7f), sy = sc * R(0.8f, 0.9f);
            var go = Put(hero, path, new Vector3(q.x, s + R(1.0f, 2.0f) - top * sy, q.y), Quaternion.Euler(R(-12, 12), R(0, 360), R(-12, 12)), sc);
            go.transform.localScale = new Vector3(sc, sy, sc * R(0.9f, 1.2f));
        }

        // fallen-log footbridges where the forest->swamp and highland-spur trails cross streams
        foreach (var (lineName, near) in new[] { ("ForestStream", new Vector2(434.1f, 503.2f)), ("WestStream", new Vector2(279.4f, 561.6f)) })
        {
            var l = MeadowHomeLayout.Lines.First(x => x.name == lineName);
            int best = 0;
            for (int i = 1; i < l.pts.Length; i++) if ((new Vector2(l.pts[i].x, l.pts[i].y) - near).sqrMagnitude < (new Vector2(l.pts[best].x, l.pts[best].y) - near).sqrMagnitude) best = i;
            var a = l.pts[Mathf.Max(0, best - 1)]; var b = l.pts[Mathf.Min(l.pts.Length - 1, best + 1)];
            var c = new Vector2(l.pts[best].x, l.pts[best].y); float surf = l.pts[best].z;
            var tan = new Vector2(b.x - a.x, b.y - a.y).normalized; var n = new Vector2(-tan.y, tan.x);
            float half = 3.6f;
            var e1 = c + n * half; var e2 = c - n * half;
            float h1 = H(e1), h2 = H(e2);
            float yaw = Mathf.Atan2(-n.y, n.x) * Mathf.Rad2Deg;                  // the log's long axis (local X) across the flow
            float pitch = Mathf.Atan2(h1 - h2, 2 * half) * Mathf.Rad2Deg;
            float y = Mathf.Max(surf + 0.45f, (h1 + h2) * 0.5f + 0.25f);
            var log = Put(hero, KN + "Nature/Log_2.prefab", new Vector3(c.x, y + 0.4f, c.y), Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, pitch), 1.05f);
            foreach (var end in new[] { e1, e2 })
                for (int k = 0; k < 2; k++)
                {
                    var p = end + new Vector2(R(-0.8f, 0.8f), R(-0.8f, 0.8f));
                    Put(hero, KnSmall(), new Vector3(p.x, H(p) - 0.1f, p.y), OnGround(p, 6), R(1.4f, 2.0f));
                }
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return new { lining = nLining, boulders = nBoulder, inChannel = nChannel, driftwood = nWood };
    }

    // Meadow as in the BK Meadows demo: low stones half-sunk in the lawn in clusters of 3-7, and a few lone hero broadleafs.
    public static object BuildMeadow(int seed)
    {
        terrain = Terrain.activeTerrain;
        rng = new System.Random(seed);
        const string BKR = "Assets/_StoreAssets/Env/BK/PureNature_Meadows/Prefabs/Rocks/";
        const string GV = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Vegetation/";
        var stonesRoot = Group("Meadow Stones");
        var treesRoot = Group("Meadow Trees");
        var td = terrain.terrainData;
        var home = MeadowHomeLayout.HomeBase;
        var trees = td.treeInstances.Select(t => new Vector2(t.position.x * td.size.x, t.position.z * td.size.z))
            .Where(q => MeadowHomeLayout.SignedDistance(q, MeadowHomeLayout.MeadowArea) < 30).ToList();
        Quaternion OnGround(Vector2 p, float follow) =>
            Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, td.GetInterpolatedNormal(p.x / td.size.x, p.y / td.size.z), follow)) * Quaternion.Euler(0, R(0, 360), 0);
        bool Ok(Vector2 p, float treeGap, float trailGap)
        {
            if (MeadowHomeLayout.SignedDistance(p, MeadowHomeLayout.MeadowArea) > -6) return false;
            if (MeadowHomeLayout.WaterEdgeDistancePublic(p) < 6f) return false;
            if (MeadowHomeLayout.TrailDistancePublic(p) < trailGap) return false;
            if (Mathf.Max(Mathf.Abs(p.x - home.x), Mathf.Abs(p.y - home.y)) < 24f) return false;
            return trees.All(t => (t - p).sqrMagnitude > treeGap * treeGap);
        }

        // lone hero trees first, so the stones keep their distance from them too
        int nTrees = 0;
        foreach (var spot in new[] { new Vector2(268, 300), new Vector2(410, 288), new Vector2(470, 272), new Vector2(285, 340), new Vector2(505, 262) })
        {
            var p = spot;
            for (int tries = 0; tries < 60 && !(Ok(p, 9f, 8f) && Vector2.Distance(p, home) > 40f); tries++) p = spot + new Vector2(R(-15, 15), R(-15, 15));
            if (!(Ok(p, 9f, 8f) && Vector2.Distance(p, home) > 40f)) continue;
            Put(treesRoot, GV + (rng.Next(2) == 0 ? "Broadleaf_Large_1.prefab" : "Broadleaf_Large_2.prefab"), new Vector3(p.x, H(p) - 0.2f, p.y), Quaternion.Euler(0, R(0, 360), 0), R(0.9f, 1.1f));
            trees.Add(p);
            nTrees++;
        }

        var kinds = new[] { (BKR + "Stone1.prefab", 50), (BKR + "Stone2.prefab", 20), (BKR + "Stone3.prefab", 15), (BKR + "Stone4.prefab", 15) };
        string Pick() { int r = rng.Next(100); foreach (var (path, w) in kinds) { if (r < w) return path; r -= w; } return kinds[0].Item1; }
        var centres = new List<Vector2>();
        int nStones = 0;
        for (int i = 0; i < 8000 && centres.Count < 18; i++)
        {
            var c = new Vector2(R(236, 545), R(222, 345));
            if (!Ok(c, 12f, 3f) || centres.Any(q => (q - c).sqrMagnitude < 22f * 22f)) continue;
            centres.Add(c);
            int k = 3 + rng.Next(5);
            for (int j = 0; j < k; j++)
            {
                var p = j == 0 ? c : c + new Vector2(R(-3, 3), R(-3, 3));
                if (!Ok(p, 8f, 3f)) continue;
                Put(stonesRoot, Pick(), new Vector3(p.x, H(p), p.y), OnGround(p, 0.5f), (j == 0 ? R(1.0f, 1.3f) : R(0.7f, 1.1f)));
                nStones++;
            }
        }
        // two or three larger grass-topped stones, half sunk
        int nBig = 0;
        for (int i = 0; i < 3000 && nBig < 3; i++)
        {
            var p = new Vector2(R(250, 530), R(225, 340));
            if (!Ok(p, 12f, 4f) || centres.Any(q => (q - p).sqrMagnitude < 15f * 15f)) continue;
            centres.Add(p);
            Put(stonesRoot, "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/Rocks Grass/Stone_" + (3 + rng.Next(2)) + ".prefab", new Vector3(p.x, H(p) - R(0.6f, 0.9f), p.y), OnGround(p, 0.6f), R(0.8f, 1.1f));
            nBig++;
        }

        foreach (var t in stonesRoot.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        EditorSceneManager.MarkSceneDirty(stonesRoot.gameObject.scene);
        return new { heroTrees = nTrees, stoneClusters = centres.Count - nBig, stones = nStones, bigStones = nBig };
    }

    static Transform Group(string name)
    {
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().First(g => g.name == "Environment");
        var dressing = env.transform.Find("Dressing");
        if (dressing == null) { dressing = new GameObject("Dressing").transform; dressing.SetParent(env.transform, false); }
        var old = dressing.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var t = new GameObject(name).transform;
        t.SetParent(dressing, false);
        Undo.RegisterCreatedObjectUndo(t.gameObject, name);
        return t;
    }

    static GameObject Put(Transform parent, string path, Vector3 pos, Quaternion rot, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new ArgumentException("Missing prefab " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }
}

// ---------------- Rock outcrops: high ground made of rock meshes (fourth class) ----------------
// Highland ledges (rock tops flush with the uphill ground), the lakeside viewpoint, forest/meadow boulders.
public static class Outcrops
{
    const string RG = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/Rocks Grass/";
    const string HF = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
    static Terrain terrain;
    static System.Random rng;
    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static float H(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y));

    // (prefab, footprint radius at scale 1, local top above pivot, local bottom below pivot)
    static readonly (string path, float radius, float top, float bottom)[] Cliffs =
    {
        (RG + "Rock_1.prefab", 10.8f, 13.8f, 12.7f),
        (RG + "Rock_2.prefab", 8.6f, 8.2f, 8.6f),
        (RG + "Rock_3.prefab", 9.0f, 17.9f, 15.7f),
    };
    static readonly (string path, float radius, float top)[] Boulders =
    {
        (RG + "Stone_1.prefab", 3.2f, 1.6f), (RG + "Stone_2.prefab", 4.0f, 3.2f), (RG + "Stone_3.prefab", 2.3f, 1.7f), (RG + "Stone_4.prefab", 1.8f, 1.4f),
        (RG + "Stone_5.prefab", 3.5f, 3.9f), (RG + "Stone_6.prefab", 3.3f, 3.5f), (RG + "Stone_7.prefab", 2.7f, 2.7f), (RG + "Stone_8.prefab", 3.6f, 3.2f),
    };

    static Vector2 Gradient(Vector2 p)
    {
        float dx = (H(p + Vector2.right * 2) - H(p - Vector2.right * 2)) / 4f, dz = (H(p + Vector2.up * 2) - H(p - Vector2.up * 2)) / 4f;
        return new Vector2(dx, dz);   // points uphill
    }

    static bool Clear(Vector2 p, float r)
    {
        if (MeadowHomeLayout.SignedDistance(p, MeadowHomeLayout.Playable) > -r * 0.4f) return false;   // keep inside the walkable area
        var (s, kind) = MeadowHomeLayout.WaterSurfaceAt(p);
        if (!float.IsNegativeInfinity(s)) return false;                                               // not in or by water
        if (MeadowHomeLayout.TrailDistancePublic(p) < r + 3f) return false;                           // keep trails open
        if (Vector2.Distance(p, new Vector2(326, 274)) < 40f + r) return false;                       // home base
        return true;
    }

    public static object Build(int seed)
    {
        terrain = Terrain.activeTerrain;
        rng = new System.Random(seed);
        var root = Group("Rock Outcrops");
        var ledges = Child(root, "Highland Ledges");
        var boulders = Child(root, "Boulders");
        var viewpoint = Child(root, "Lakeside Viewpoint");
        var placed = new List<Vector3>();
        bool Free(Vector2 p, float r) => placed.All(q => (new Vector2(q.x, q.y) - p).sqrMagnitude > (q.z + r) * (q.z + r));
        int nLedge = 0, nBoulder = 0;

        // highland ledges: on 10-32 deg slopes, a short row of 2-3 rocks along the contour, tops flush with the uphill ground
        for (int i = 0; i < 9000 && nLedge < 18; i++)
        {
            var p = new Vector2(R(150, 520), R(590, 860));
            if (MeadowHomeLayout.SignedDistance(p, MeadowHomeLayout.HighlandsArea) > -6f) continue;
            var g = Gradient(p);
            float slope = Mathf.Atan(g.magnitude) * Mathf.Rad2Deg;
            if (slope < 12f || slope > 34f) continue;
            var up = g.normalized; var along = new Vector2(-up.y, up.x);
            int count = 2 + rng.Next(3);
            var kinds = Enumerable.Range(0, count).Select(_ => Cliffs[rng.Next(Cliffs.Length)]).ToArray();
            float sc = R(0.7f, 1.15f);
            float width = kinds.Sum(k => k.radius * sc * 1.5f);
            if (!Clear(p, width * 0.6f) || !Free(p, width * 0.6f)) continue;
            float offset = -width / 2;
            foreach (var k in kinds)
            {
                float r = k.radius * sc;
                var c = p + along * (offset + r * 0.8f) + up * R(-1f, 1f);
                offset += r * 1.5f;
                float top = H(c + up * r * 0.35f) + R(0.2f, 0.8f);                 // flush with the ground uphill
                float drop = top - H(c - up * r * 0.8f);                            // visible cliff height downhill
                if (drop < 3.5f) top += 3.5f - drop;
                var go = Put(ledges, k.path, new Vector3(c.x, top - k.top * sc, c.y), Quaternion.Euler(R(-3, 3), R(0, 360), R(-3, 3)), sc);
                placed.Add(new Vector3(c.x, c.y, r));
            }
            nLedge++;
        }

        // lakeside viewpoint (concept): a raised rocky promontory by the lake's south-west shore
        var vp = new Vector2(546, 246);
        float vpGround = H(vp);
        Put(viewpoint, Cliffs[1].path, new Vector3(vp.x, vpGround + 3.2f - Cliffs[1].top * 0.55f, vp.y), Quaternion.Euler(0, 35, 0), 0.55f);
        Put(viewpoint, Cliffs[0].path, new Vector3(vp.x + 6.5f, vpGround + 1.4f - Cliffs[0].top * 0.42f, vp.y - 4.5f), Quaternion.Euler(0, 160, 0), 0.42f);
        Put(viewpoint, Boulders[1].path, new Vector3(vp.x - 5.5f, vpGround - 0.4f, vp.y + 3.5f), Quaternion.Euler(0, 80, 0), 1.0f);
        Put(viewpoint, Boulders[6].path, new Vector3(vp.x - 3.0f, vpGround - 0.2f, vp.y - 5.0f), Quaternion.Euler(0, 210, 0), 0.8f);
        placed.Add(new Vector3(vp.x, vp.y, 12f));

        // boulder clusters: forest-heavy, a few on meadow edges; 1 large + 1-3 smaller within ~5 m
        for (int i = 0; i < 8000 && nBoulder < 95; i++)
        {
            var p = new Vector2(R(170, 840), R(200, 840));
            bool meadow = MeadowHomeLayout.SignedDistance(p, MeadowHomeLayout.MeadowArea) < 0;
            bool highland = MeadowHomeLayout.SignedDistance(p, MeadowHomeLayout.HighlandsArea) < 0;
            if (meadow && rng.NextDouble() > 0.25) continue;                     // sparse in the meadow
            if (MeadowHomeLayout.SignedDistance(p, MeadowHomeLayout.SwampArea) < 10) continue;
            if (!Clear(p, 5f) || !Free(p, 5f)) continue;
            var big = Boulders[rng.Next(Boulders.Length)];
            float sc = R(0.8f, highland ? 1.6f : 1.3f);
            var g = Gradient(p);
            var rot = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, new Vector3(-g.x, 1, -g.y).normalized, 0.6f)) * Quaternion.Euler(0, R(0, 360), 0);
            Put(boulders, big.path, new Vector3(p.x, H(p) - R(0.2f, 0.5f) * big.top * sc, p.y), rot, sc);
            placed.Add(new Vector3(p.x, p.y, big.radius * sc));
            int smalls = 1 + rng.Next(3);
            for (int j = 0; j < smalls; j++)
            {
                var b = Boulders[2 + rng.Next(2)];
                var q = p + new Vector2(R(-1, 1), R(-1, 1)).normalized * (big.radius * sc + R(0.5f, 2.5f));
                if (!Clear(q, 2f)) continue;
                float s2 = R(0.5f, 0.9f);
                Put(boulders, b.path, new Vector3(q.x, H(q) - R(0.15f, 0.4f) * b.top * s2, q.y), Quaternion.Euler(R(-8, 8), R(0, 360), R(-8, 8)), s2);
            }
            nBoulder++;
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return new { ledgeRows = nLedge, boulderClusters = nBoulder, viewpoint = vp.ToString() };
    }

    static Transform Group(string name)
    {
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().First(g => g.name == "Environment");
        var old = env.transform.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var t = new GameObject(name).transform;
        t.SetParent(env.transform, false);
        Undo.RegisterCreatedObjectUndo(t.gameObject, name);
        return t;
    }

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static GameObject Put(Transform parent, string path, Vector3 pos, Quaternion rot, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new ArgumentException("Missing prefab " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }
}

// ---------------- Vegetation stamps (MicroVerse TreeStamp / DetailStamp) ----------------
public static class Veg
{
    const string Poisson = "Packages/com.jbooth.microverse/Data/microverse_default_poissondisk.poissondisk";

    public static Transform Group(string path)
    {
        var mv = MicroVerse.instance;
        var t = mv.transform;
        foreach (var part in path.Split('/'))
        {
            var c = t.Find(part);
            if (c == null) { c = new GameObject(part).transform; c.SetParent(t, false); Undo.RegisterCreatedObjectUndo(c.gameObject, part); }
            t = c;
        }
        return t;
    }

    public static void Clear(string path)
    {
        var t = MicroVerse.instance.transform.Find(path);
        if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
    }

    // Whole-terrain (or centred square) tree stamp. prefabs/weights pair up; scale = (min, max) uniform.
    public static TreeStamp Trees(Transform parent, string name, string[] prefabs, float[] weights, Vector2 scale, float density, uint seed, Vector2 centre, float size)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var s = go.AddComponent<TreeStamp>();
        s.stampVersion = 1; s.version = 1;
        s.filterSet.version = 2;
        s.poissonDisk = AssetDatabase.LoadAssetAtPath<Texture2D>(Poisson);
        s.poissonDiskStrength = 1f;
        s.density = density;
        s.seed = seed;
        for (int i = 0; i < prefabs.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i]) ?? throw new ArgumentException("Missing " + prefabs[i]);
            s.prototypes.Add(new TreePrototypeSerializable { prefab = prefab, bendFactor = 0, navMeshLod = 0 });
            var r = new TreeStamp.Randomization();
            r.disabled = false; r.scaleMultiplierAtBoundaries = 1; r.weight = weights[i]; r.weightRange = new Vector2(0, 99999);
            r.randomRotation = true; r.lockScaleWidthHeight = true; r.scaleHeightRange = scale; r.scaleWidthRange = scale;
            s.randomizations.Add(r);
        }
        s.filterSet.falloffFilter.filterType = FalloffFilter.FilterType.Global;
        go.transform.position = new Vector3(centre.x, 0, centre.y);
        go.transform.localScale = new Vector3(size, 1, size);
        return s;
    }

    // Detail stamp from a prefab (mesh detail, instanced) - one prototype per stamp.
    public static DetailStamp Detail(Transform parent, string name, string prefab, Vector2 width, Vector2 height, float coverage, float density, Vector2 centre, float size)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var s = go.AddComponent<DetailStamp>();
        s.stampVersion = 1;
        s.filterSet.version = 2;
        var p = s.prototype;
        p.prototype = AssetDatabase.LoadAssetAtPath<GameObject>(prefab) ?? throw new ArgumentException("Missing " + prefab);
        p.prototypeTexture = null;
        p.usePrototypeMesh = true;
        p.useInstancing = true;
        p.renderMode = DetailRenderMode.VertexLit;
        p.minWidth = width.x; p.maxWidth = width.y; p.minHeight = height.x; p.maxHeight = height.y;
        p.alignToGround = 0.6f; p.positionJitter = 0.7f;
        p.noiseSpread = 0.15f; p.noiseSeed = name.GetHashCode() & 0xffff;
        p.targetCoverage = coverage; p.density = density; p.useDensityScaling = true;
        p.healthyColor = Color.white; p.dryColor = Color.white;
        s.filterSet.falloffFilter.filterType = FalloffFilter.FilterType.Global;
        go.transform.position = new Vector3(centre.x, 0, centre.y);
        go.transform.localScale = new Vector3(size, 1, size);
        return s;
    }

    public static void Box(FilterSet f, float edge)
    {
        f.falloffFilter.filterType = FalloffFilter.FilterType.Box;
        f.falloffFilter.falloffRange = new Vector2(0.8f, edge);
    }

    public static void Mask(FilterSet f, Texture2D mask)
    {
        var ff = f.falloffFilter;
        ff.filterType = FalloffFilter.FilterType.Texture;
        ff.texture = mask; ff.textureChannel = FalloffFilter.TextureChannel.R; ff.textureParams = new Vector2(1, 0);
        ff.textureRotationScale = new Vector4(0, 1, 0, 0); ff.falloffRange = new Vector2(0.8f, 1f); ff.clampTexture = true;
    }

    public static void Slope(FilterSet f, float lo, float hi, float sLo, float sHi)
    {
        f.slopeFilter.enabled = true; f.slopeFilter.range = new Vector2(lo, hi); f.slopeFilter.smoothness = new Vector2(sLo, sHi);
    }

    public static void Height(FilterSet f, float lo, float hi, float sLo, float sHi)
    {
        f.heightFilter.enabled = true; f.heightFilter.range = new Vector2(lo, hi); f.heightFilter.smoothness = new Vector2(sLo, sHi);
    }

    // offset shifts the pattern: stamps sharing frequency/amplitude/balance otherwise get identical drifts
    public static void Noise(JBooth.MicroVerseCore.Noise n, int type, float frequency, float amplitude, float balance, float offset = 0)
    {
        n.noiseType = (JBooth.MicroVerseCore.Noise.NoiseType)type; n.noiseSpace = JBooth.MicroVerseCore.Noise.NoiseSpace.World;
        n.frequency = frequency; n.amplitude = amplitude; n.balance = balance; n.offset = offset;
    }

    // Small engineering test: one pine patch and one grass patch, to check rendering before the full pass.
    public static object Test()
    {
        Clear("Vegetation Test");
        var g = Group("Vegetation Test");
        const string KN = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/Nature/";
        const string HF = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
        var t = Trees(g, "Test Pines", new[] { KN + "PineTree_1.prefab", KN + "PineTree_2.prefab", KN + "PineTree_3.prefab", HF + "Fir_1.prefab", HF + "Fir_2.prefab" }, new[] { 50f, 50f, 40f, 30f, 30f }, new Vector2(0.8f, 1.25f), 1.0f, 7, new Vector2(470, 470), 120);
        Box(t.filterSet, 0.6f);
        var d = Detail(g, "Test Grass", KN + "Grass_1.prefab", new Vector2(0.8f, 1.3f), new Vector2(0.8f, 1.3f), 0.6f, 1f, new Vector2(350, 300), 50);
        Box(d.filterSet, 0.6f);
        foreach (var st in g.GetComponentsInChildren<Stamp>(true)) EditorUtility.SetDirty(st);
        MicroVerse.instance.Invalidate();
        return "test stamps added";
    }
}

// ---------------- Vegetation pass (masks, variants, MicroVerse stack, clearing, wind) ----------------
public static class Vegetation
{
    const string Folder = MeadowHomeLayout.DataFolder + "/Vegetation";
    const string Maps = MeadowHomeLayout.MapFolder + "/";
    const string KN = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/Nature/";
    const string HF = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
    const string GV = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Vegetation/";
    const string BKP = "Assets/_StoreAssets/Env/BK/PureNature_Meadows/Prefabs/Plants/";
    const string Layers = MeadowHomeLayout.DataFolder + "/TerrainLayers/";

    // ---- masks from the analytic layout (terrain space, north up) ----
    public static async Task<object> GenerateMasks(int res)
    {
        var t0 = DateTime.Now;
        string[] names = { "Forest", "Meadow", "Highlands", "Swamp", "Foothills", "Wild", "Reeds", "WaterEdge", "MeadowFlowers", "Hedge", "Rim", "RimBroadleaf", "RimShort", "RimBushes" };
        var maps = names.ToDictionary(n => n, n => new float[res * res]);
        var home = MeadowHomeLayout.HomeBase;
        var centre = new Vector2(505, 520);
        await Task.Run(() => Parallel.For(0, res, j =>
        {
            for (int i = 0; i < res; i++)
            {
                float x = (i + 0.5f) / res * MeadowHomeLayout.Size, z = (j + 0.5f) / res * MeadowHomeLayout.Size;
                var p = new Vector2(x, z);
                int k = j * res + i;
                var w = MeadowHomeLayout.VegWeights(p);   // x forest, y meadow, z highlands, w swamp (no water fade)
                float dPlay = MeadowHomeLayout.SignedDistance(p, MeadowHomeLayout.Playable);
                float inside = 1 - MeadowHomeLayout.SmoothStep(-8, -1, dPlay);
                float dWater = MeadowHomeLayout.WaterEdgeDistancePublic(p);
                var (surface, kind) = MeadowHomeLayout.WaterSurfaceAt(p);
                float ground = MeadowHomeLayout.HeightAt(x, z, out _);
                bool wet = !float.IsNegativeInfinity(surface) && ground < surface - 0.02f;
                float reach = 0.5f + 0.5f * MeadowHomeLayout.Fbm(x / 14f, z / 14f + 19f, 2);   // grass reaches the water on some stretches
                float landFade = wet ? 0 : MeadowHomeLayout.SmoothStep(0.15f + 0.9f * reach, 1.2f + 1.8f * reach, dWater);
                float treeFade = wet ? 0 : MeadowHomeLayout.SmoothStep(5f, 12f, dWater);
                maps["Forest"][k] = w.x * inside * treeFade;
                maps["Meadow"][k] = w.y * inside * landFade;
                maps["Highlands"][k] = w.z * inside * treeFade;
                maps["Swamp"][k] = w.w * inside;
                maps["Foothills"][k] = MeadowHomeLayout.SmoothStep(-48, -30, dPlay) * (1 - MeadowHomeLayout.SmoothStep(-7, -2, dPlay)) * treeFade * (1 - w.w);
                maps["Wild"][k] = inside * landFade * (1 - w.y) * (1 - w.w * 0.7f);
                float depth = float.IsNegativeInfinity(surface) ? -9 : surface - ground;
                // lakes, ponds, swamp, the river away from the bridge, and the slow margin stream below the swamp
                bool reedWater = kind == 0 || (kind == 1 && Vector2.Distance(p, MeadowHomeLayout.Bridge) > 12f) || (kind == 2 && x > 556 && z > 530 && z < 700);
                maps["Reeds"][k] = reedWater ? MeadowHomeLayout.SmoothStep(-0.35f, -0.05f, depth) * (1 - MeadowHomeLayout.SmoothStep(0.5f, 0.85f, depth)) * inside * (1 - MeadowHomeLayout.SandWeight(p)) : 0;
                maps["WaterEdge"][k] = wet ? 0 : MeadowHomeLayout.SmoothStep(0.3f, 1.2f, dWater) * (1 - MeadowHomeLayout.SmoothStep(4f, 7f, dWater)) * inside;

                // meadow flowers keep off the yard and the trail edges, so those stay plain lawn
                float trail = MeadowHomeLayout.TrailDistancePublic(p);
                float yard = Mathf.Max(Mathf.Abs(x - home.x), Mathf.Abs(z - home.y));
                // a wide, ragged round fade: a square cut makes flower drifts run parallel to the fence like a planted border
                float yardFade = MeadowHomeLayout.SmoothStep(22f, 38f, Vector2.Distance(p, home) + 6f * MeadowHomeLayout.Fbm(x / 12f, z / 12f + 33f, 2));
                maps["MeadowFlowers"][k] = maps["Meadow"][k] * yardFade * MeadowHomeLayout.SmoothStep(1.6f, 3.0f, trail);
                // hedgerows: the meadow's outer rim (against the southern foot) and both sides of the viewpoint path, with gaps
                float edgeBand = MeadowHomeLayout.SmoothStep(0.12f, 0.3f, w.y) * (1 - MeadowHomeLayout.SmoothStep(0.45f, 0.65f, w.y));
                float pathBand = MeadowHomeLayout.SmoothStep(2.6f, 3.2f, trail) * (1 - MeadowHomeLayout.SmoothStep(5.0f, 5.8f, trail)) * w.y;
                float gaps = MeadowHomeLayout.SmoothStep(-0.05f, 0.05f, MeadowHomeLayout.Fbm(x / 22f, z / 22f + 51f, 2));
                maps["Hedge"][k] = Mathf.Max(edgeBand * (z < 300 ? 1 : 0.3f), pathBand * (z < 262 ? 1 : 0)) * gaps * inside * landFade * MeadowHomeLayout.SmoothStep(18f, 24f, yard);

                // tree belt outside the playable edge (behind the foot rocks, in front of the ridges)
                float rim = MeadowHomeLayout.SmoothStep(4f, 10f, dPlay) * (1 - MeadowHomeLayout.SmoothStep(150f, 175f, dPlay)) * treeFade;
                float a = Mathf.Atan2(z - centre.y, x - centre.x);
                float wS = Mathf.Max(0, -Mathf.Sin(a)), wE = Mathf.Max(0, Mathf.Cos(a)), wN = Mathf.Max(0, Mathf.Sin(a));
                float broad = Mathf.Clamp01((0.25f * wS * wS + 0.35f * wE * wE + (x > 450 ? 0.6f : 0.1f) * wN * wN) / Mathf.Max(0.3f, wS * wS + wE * wE + wN * wN + Mathf.Max(0, -Mathf.Cos(a)) * Mathf.Max(0, -Mathf.Cos(a)))
                    + 0.2f * MeadowHomeLayout.Fbm(x / 60f, z / 60f + 9f, 2));
                // behind the home the belt stays short so the ridges and peaks show over it
                float shortTrees = (1 - MeadowHomeLayout.SmoothStep(80f, 140f, Vector2.Distance(p, home))) * (1 - MeadowHomeLayout.SmoothStep(35f, 70f, dPlay));
                maps["Rim"][k] = rim * (1 - broad) * (1 - shortTrees);
                maps["RimBroadleaf"][k] = rim * broad * (1 - shortTrees);
                maps["RimShort"][k] = rim * shortTrees;
                maps["RimBushes"][k] = MeadowHomeLayout.SmoothStep(2f, 5f, dPlay) * (1 - MeadowHomeLayout.SmoothStep(30f, 45f, dPlay)) * treeFade;
            }
        }));
        foreach (var kv in maps) MeadowHomeLayout.WriteMask(Maps + "MH_Veg_" + kv.Key + ".png", kv.Value, res);
        return new { seconds = (DateTime.Now - t0).TotalSeconds, names };
    }

    // ---- clear masks from the scene: rock footprints, yard, campfire, bridge, trails ----
    public static async Task<object> GenerateClearMasks(int res)
    {
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().First(g => g.name == "Environment");
        var rocks = new List<Vector3>();   // x, z, radius
        foreach (var path in new[] { "Rock Outcrops", "Landmarks/Waterfall", "Boundary/Foot Rocks", "Dressing/Meadow Stones", "Dressing/Bank Rocks", "Dressing/Water Edges" })
        {
            var root = env.transform.Find(path);
            if (root == null) continue;
            foreach (var lod in root.GetComponentsInChildren<LODGroup>())
            {
                var rs = lod.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.size.y < 1.2f) continue;
                rocks.Add(new Vector3(b.center.x, b.center.z, 0.42f * Mathf.Max(b.size.x, b.size.z)));
            }
        }
        var props = new List<Vector3>();
        var home = env.transform.Find("Landmarks/Home Base");
        if (home != null)
            foreach (var r in home.GetComponentsInChildren<Renderer>())
                if (r.bounds.size.y > 0.25f && !r.name.StartsWith("Fence")) props.Add(new Vector3(r.bounds.center.x, r.bounds.center.z, 0.5f * Mathf.Max(r.bounds.size.x, r.bounds.size.z)));
        var bridge = env.transform.Find("Landmarks/River Bridge");
        Bounds? bridgeB = null;
        if (bridge != null) { var rs = bridge.GetComponentsInChildren<Renderer>(); if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); bridgeB = b; } }
        var homeC = MeadowHomeLayout.HomeBase;
        var trees = new float[res * res]; var details = new float[res * res];
        await Task.Run(() => Parallel.For(0, res, j =>
        {
            for (int i = 0; i < res; i++)
            {
                float x = (i + 0.5f) / res * MeadowHomeLayout.Size, z = (j + 0.5f) / res * MeadowHomeLayout.Size;
                var p = new Vector2(x, z);
                float ct = 0, cd = 0;
                foreach (var r in rocks)
                {
                    float d = Vector2.Distance(p, new Vector2(r.x, r.y));
                    ct = Mathf.Max(ct, 1 - MeadowHomeLayout.SmoothStep(r.z + 1.5f, r.z + 3.5f, d));
                    cd = Mathf.Max(cd, 1 - MeadowHomeLayout.SmoothStep(r.z * 0.7f, r.z * 0.85f, d));
                }
                foreach (var r in props) cd = Mathf.Max(cd, 1 - MeadowHomeLayout.SmoothStep(r.z * 0.8f, r.z + 0.3f, Vector2.Distance(p, new Vector2(r.x, r.y))));
                // fenced yard: no trees within 24 m of the centre; trimmed lawn handled by the meadow masks
                ct = Mathf.Max(ct, 1 - MeadowHomeLayout.SmoothStep(22f, 28f, Mathf.Max(Mathf.Abs(x - homeC.x), Mathf.Abs(z - homeC.y))));
                if (bridgeB.HasValue)
                {
                    var b = bridgeB.Value;
                    float dx = Mathf.Max(0, Mathf.Abs(x - b.center.x) - b.extents.x), dz = Mathf.Max(0, Mathf.Abs(z - b.center.z) - b.extents.z);
                    float db = Mathf.Sqrt(dx * dx + dz * dz);
                    ct = Mathf.Max(ct, 1 - MeadowHomeLayout.SmoothStep(6f, 9f, db));
                    cd = Mathf.Max(cd, 1 - MeadowHomeLayout.SmoothStep(0.2f, 1.2f, db));
                }
                float tr = MeadowHomeLayout.TrailDistancePublic(p);
                ct = Mathf.Max(ct, 1 - MeadowHomeLayout.SmoothStep(3.0f, 4.5f, tr));
                cd = Mathf.Max(cd, 1 - MeadowHomeLayout.SmoothStep(0.9f, 1.6f, tr));
                trees[j * res + i] = ct; details[j * res + i] = cd;
            }
        }));
        MeadowHomeLayout.WriteMask(Maps + "MH_Veg_ClearTrees.png", trees, res);
        MeadowHomeLayout.WriteMask(Maps + "MH_Veg_ClearDetails.png", details, res);
        return new { rocks = rocks.Count, props = props.Count, bridge = bridgeB.HasValue };
    }

    // ---- prefab variants: capsule colliders on the tall firs (terrain trees ignore MeshColliders) ----
    public static object MakeVariants()
    {
        MeadowHomeLayout.EnsureFolderPublic(Folder + "/Prefabs");
        var made = new List<string>();
        foreach (var (name, height, radius) in new[] { ("Fir_Tall_1", 11.5f, 0.35f), ("Fir_Tall_2", 15.1f, 0.4f), ("Fir_Tall_3", 17.3f, 0.45f) })
        {
            string dest = Folder + "/Prefabs/" + name + "_Capsule.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(dest) != null) { made.Add(dest + " (exists)"); continue; }
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(HF + name + ".prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            var cap = inst.AddComponent<CapsuleCollider>();
            cap.radius = radius; cap.height = height * 0.55f; cap.center = new Vector3(0, height * 0.275f, 0); cap.direction = 1;
            PrefabUtility.SaveAsPrefabAsset(inst, dest);   // saving a connected instance creates a variant of the vendor prefab
            UnityEngine.Object.DestroyImmediate(inst);
            made.Add(dest);
        }
        return made;
    }

    // Unity's detail system rejects LODGroups; these prefabs keep the mesh and LODGroup on one root.
    // Make plain mesh prefabs (vendor mesh and material, no LODGroup) under _Game for them.
    static string DetailPrefab(string vendorPath)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(vendorPath);
        string pack = vendorPath.Contains("HIGHLANDS") ? "HL" : vendorPath.Contains("Komorebi") ? "KN" : vendorPath.Contains("Grasslands") ? "GL" : "BK";
        string dest = Folder + "/Details/" + pack + "_" + name + "_Detail.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dest) != null) return dest;
        MeadowHomeLayout.EnsureFolderPublic(Folder + "/Details");
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(vendorPath) ?? throw new ArgumentException("Missing " + vendorPath);
        var lod = src.GetComponent<LODGroup>();
        Renderer r = lod != null && lod.GetLODs().Length > 0 ? lod.GetLODs()[0].renderers.FirstOrDefault(x => x != null) : src.GetComponentInChildren<MeshRenderer>();
        var mf = r.GetComponent<MeshFilter>();
        var go = new GameObject(pack + "_" + name + "_Detail");
        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = r.sharedMaterials;
        mr.shadowCastingMode = r.shadowCastingMode;
        PrefabUtility.SaveAsPrefabAsset(go, dest);
        UnityEngine.Object.DestroyImmediate(go);
        return dest;
    }

    // ---- MicroVerse stack ----
    public static object Build()
    {
        var mv = MicroVerse.instance;
        var old = mv.transform.Find("Vegetation");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var test = mv.transform.Find("Vegetation Test");
        if (test != null) Undo.DestroyObjectImmediate(test.gameObject);
        var root = Veg.Group("Vegetation");
        var trees = Veg.Group("Vegetation/Trees");
        var bushes = Veg.Group("Vegetation/Bushes");
        var ground = Veg.Group("Vegetation/Ground Cover");
        var clear = Veg.Group("Vegetation/Clearing");
        Texture2D M(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Maps + "MH_Veg_" + n + ".png") ?? throw new ArgumentException("Missing mask " + n);
        string FT(int i) => Folder + "/Prefabs/Fir_Tall_" + i + "_Capsule.prefab";
        var c = new Vector2(500, 500);
        uint seed = 100;
        TreeStamp T(Transform p, string name, string[] prefabs, float[] weights, Vector2 scale, float density, string mask)
        {
            var s = Veg.Trees(p, name, prefabs, weights, scale, density, seed++, c, 1000);
            Veg.Mask(s.filterSet, M(mask));
            return s;
        }
        DetailStamp D(string name, string prefab, Vector2 w, Vector2 h, float density, string mask)
        {
            var s = Veg.Detail(ground, name, prefab, w, h, 1f, density, c, 1000);
            Veg.Mask(s.filterSet, M(mask));
            return s;
        }

        // canopy: forest stands interlock with clearings (same stand noise, inverted for clearing cover)
        // Forest and foothills are Komorebi pines with Grasslands broadleaf groves; HIGHLANDS firs read cyan and
        // off-style next to them, so they only grow in the highlands.
        var groves = T(trees, "Forest Broadleaf Groves", new[] { GV + "Broadleaf_Medium_1.prefab", GV + "Broadleaf_Medium_2.prefab", GV + "Broadleaf_Small_2.prefab", GV + "Birch_Medium_1.prefab", GV + "Birch_Medium_2.prefab", GV + "Birch_Small_1.prefab" },
            new[] { 26f, 26f, 22f, 9f, 9f, 8f }, new Vector2(0.85f, 1.15f), 3.0f, "Forest");   // mostly green; birches are the autumn accents
        // tree stamps keep each candidate with probability = filter weight, so a high noise balance thins the groves
        Veg.Height(groves.filterSet, 0, 45, 0, 6); Veg.Slope(groves.filterSet, 0, 40, 0, 6); Veg.Noise(groves.filterSet.weightNoise, 2, 24, 3f, 0.05f);
        var pines = T(trees, "Forest Pines", new[] { KN + "PineTree_3.prefab", KN + "PineTree_4.prefab", KN + "PineTree_5.prefab", KN + "PineTree_6.prefab", KN + "PineTree_1.prefab", KN + "PineTree_2.prefab" },
            new[] { 25f, 25f, 20f, 15f, 8f, 7f }, new Vector2(0.75f, 1.15f), 3.6f, "Forest");
        Veg.Height(pines.filterSet, 0, 60, 0, 8); Veg.Slope(pines.filterSet, 0, 46, 0, 6); Veg.Noise(pines.filterSet.weightNoise, 2, 13, 3f, -0.05f);
        var young = T(trees, "Forest Young Pines", new[] { KN + "PineTree_1.prefab", KN + "PineTree_2.prefab", KN + "PineTree_3.prefab" }, new[] { 40f, 35f, 25f }, new Vector2(0.45f, 0.7f), 2.2f, "Forest");
        Veg.Slope(young.filterSet, 0, 46, 0, 6); Veg.Noise(young.filterSet.weightNoise, 2, 13, -3f, 0.15f);   // inverted stand noise: stand edges and clearings
        var dead = T(trees, "Forest Dead Trees", new[] { GV + "Broadleaf_Dead_1.prefab", GV + "Broadleaf_Dead_2.prefab", GV + "Birch_Dead_1.prefab" }, new[] { 35f, 35f, 30f }, new Vector2(0.75f, 1.05f), 0.55f, "Forest");
        Veg.Slope(dead.filterSet, 0, 46, 0, 6);
        var foot = T(trees, "Foothill Pines", new[] { KN + "PineTree_4.prefab", KN + "PineTree_5.prefab", KN + "PineTree_6.prefab", KN + "PineTree_3.prefab" }, new[] { 28f, 28f, 22f, 22f }, new Vector2(0.85f, 1.2f), 3.4f, "Foothills");
        Veg.Slope(foot.filterSet, 0, 50, 0, 6); Veg.Noise(foot.filterSet.weightNoise, 2, 18, 3f, 0.0f);
        var hfirs = T(trees, "Highland Firs", new[] { HF + "Fir_1.prefab", HF + "Fir_2.prefab", HF + "Fir_3.prefab", FT(1), FT(2) }, new[] { 20f, 20f, 20f, 20f, 20f }, new Vector2(0.75f, 1.2f), 4.6f, "Highlands");
        Veg.Slope(hfirs.filterSet, 0, 48, 0, 6); Veg.Noise(hfirs.filterSet.weightNoise, 2, 20, 3f, 0.05f);
        var hdead = T(trees, "Highland Dead Firs", new[] { HF + "Fir_Dead_1.prefab", HF + "Fir_Dead_2.prefab", HF + "Fir_Tall_Dead_1.prefab" }, new[] { 35f, 35f, 30f }, new Vector2(0.8f, 1.2f), 0.7f, "Highlands");
        Veg.Slope(hdead.filterSet, 0, 46, 0, 6);
        // meadow trees as in the BK Meadows demo: mostly singles and groups of 2-3, round broadleafs, birches as accents
        var edge = T(trees, "Meadow Edge Trees", new[] { GV + "Broadleaf_Medium_1.prefab", GV + "Broadleaf_Medium_2.prefab", GV + "Broadleaf_Small_2.prefab", GV + "Birch_Medium_1.prefab", GV + "Birch_Medium_2.prefab", GV + "Birch_Small_1.prefab" },
            new[] { 22f, 22f, 20f, 12f, 12f, 12f }, new Vector2(0.85f, 1.15f), 2.2f, "Meadow");
        Veg.Slope(edge.filterSet, 0, 27, 0, 6); Veg.Noise(edge.filterSet.weightNoise, 2, 12.6f, 6f, 0.15f);
        // tree belt outside the playable edge: tall pines and broadleafs, short trees behind the home so the ridges show
        var rimP = T(trees, "Rim Pines", new[] { KN + "PineTree_3.prefab", KN + "PineTree_4.prefab", KN + "PineTree_5.prefab", KN + "PineTree_6.prefab", KN + "PineTree_2.prefab" },
            new[] { 22f, 24f, 20f, 18f, 16f }, new Vector2(0.85f, 1.25f), 2.4f, "Rim");
        Veg.Slope(rimP.filterSet, 0, 50, 0, 6); Veg.Noise(rimP.filterSet.weightNoise, 2, 9, 3f, -0.1f);
        var rimB = T(trees, "Rim Broadleafs", new[] { GV + "Broadleaf_Large_1.prefab", GV + "Broadleaf_Large_2.prefab", GV + "Birch_Large_1.prefab", GV + "Broadleaf_Medium_1.prefab", GV + "Broadleaf_Medium_2.prefab" },
            new[] { 25f, 25f, 15f, 18f, 17f }, new Vector2(0.85f, 1.2f), 2.0f, "RimBroadleaf");
        Veg.Slope(rimB.filterSet, 0, 45, 0, 6); Veg.Noise(rimB.filterSet.weightNoise, 2, 9, 3f, -0.1f, 0.35f);
        var rimS = T(trees, "Rim Short Trees", new[] { KN + "PineTree_1.prefab", KN + "PineTree_2.prefab", GV + "Broadleaf_Small_2.prefab", GV + "Birch_Small_1.prefab", GV + "Broadleaf_Medium_1.prefab" },
            new[] { 25f, 15f, 25f, 15f, 20f }, new Vector2(0.6f, 0.85f), 1.8f, "RimShort");
        Veg.Slope(rimS.filterSet, 0, 45, 0, 6); Veg.Noise(rimS.filterSet.weightNoise, 2, 14, 3f, 0.0f);
        var sdead = T(trees, "Swamp Dead Trees", new[] { GV + "Birch_Dead_1.prefab", GV + "Birch_Dead_2.prefab", GV + "Broadleaf_Dead_1.prefab", GV + "Broadleaf_Dead_2.prefab" }, new[] { 27f, 27f, 23f, 23f }, new Vector2(0.7f, 1.1f), 2.4f, "Swamp");
        Veg.Height(sdead.filterSet, MeadowHomeLayout.SwampWater - 0.6f, 26.5f, 0.2f, 1f);
        var sliving = T(trees, "Swamp Living Trees", new[] { GV + "Birch_Small_1.prefab", GV + "Birch_Small_2.prefab", GV + "Broadleaf_Small_2.prefab", KN + "PineTree_1.prefab" }, new[] { 30f, 30f, 20f, 20f }, new Vector2(0.65f, 0.95f), 1.7f, "Swamp");
        Veg.Height(sliving.filterSet, MeadowHomeLayout.SwampWater + 0.6f, 40f, 0.4f, 1f);

        // bushes
        var mb = T(bushes, "Meadow Flowering Bushes", new[] { KN + "Bush_1_Flowers.prefab", KN + "Bush_2_Flowers.prefab", KN + "Bush_3_Flowers.prefab", KN + "Bush_4_Flowers.prefab", KN + "Bush_1.prefab", KN + "Bush_2.prefab" },
            new[] { 18f, 18f, 17f, 17f, 15f, 15f }, new Vector2(0.85f, 1.15f), 4.5f, "Meadow");
        Veg.Slope(mb.filterSet, 0, 35, 0, 6); Veg.Noise(mb.filterSet.weightNoise, 2, 35, 6f, 0.2f);   // clumps of 2-4, not a sprinkle
        var hedge = T(bushes, "Meadow Hedgerows", new[] { GV + "Bush_Large.prefab", GV + "Bush_Medium.prefab", KN + "Bush_1.prefab", KN + "Bush_2.prefab" }, new[] { 35f, 30f, 18f, 17f }, new Vector2(0.75f, 1.1f), 8f, "Hedge");
        Veg.Slope(hedge.filterSet, 0, 35, 0, 6);
        var rimBu = T(bushes, "Rim Bushes", new[] { KN + "Bush_1.prefab", KN + "Bush_2.prefab", KN + "Bush_3.prefab", GV + "Bush_Large.prefab", GV + "Bush_Medium.prefab" }, new[] { 22f, 22f, 18f, 20f, 18f }, new Vector2(0.8f, 1.2f), 3.0f, "RimBushes");
        Veg.Noise(rimBu.filterSet.weightNoise, 2, 30, 3f, 0.2f);
        var fb = T(bushes, "Forest Understory", new[] { KN + "Bush_1.prefab", KN + "Bush_2.prefab", KN + "Bush_3.prefab", KN + "Bush_4.prefab", KN + "Bush_2_Flowers.prefab" }, new[] { 24f, 24f, 20f, 17f, 15f }, new Vector2(0.7f, 1.1f), 3.0f, "Forest");
        Veg.Slope(fb.filterSet, 0, 42, 0, 6); Veg.Noise(fb.filterSet.weightNoise, 2, 30, 3f, 0.2f);
        var footb = T(bushes, "Foothill Bushes", new[] { KN + "Bush_1.prefab", KN + "Bush_2.prefab", KN + "Bush_2_Flowers.prefab", KN + "Bush_3.prefab" }, new[] { 25f, 25f, 25f, 25f }, new Vector2(0.8f, 1.2f), 3.0f, "Foothills");
        Veg.Noise(footb.filterSet.weightNoise, 2, 30, 3f, 0.25f);
        var hb = T(bushes, "Highland Bushes", new[] { HF + "Bush_1.prefab", HF + "Bush_2.prefab", HF + "Bush_3.prefab", HF + "Dead_Bush_1.prefab", HF + "Dead_Bush_2.prefab" }, new[] { 22f, 22f, 22f, 17f, 17f }, new Vector2(0.6f, 1.0f), 2.6f, "Highlands");
        Veg.Slope(hb.filterSet, 0, 42, 0, 6);
        var sb = T(bushes, "Swamp Bushes", new[] { KN + "Bush_1.prefab", KN + "Bush_2.prefab", KN + "Bush_3.prefab" }, new[] { 35f, 35f, 30f }, new Vector2(0.6f, 1.0f), 2.2f, "Swamp");
        Veg.Height(sb.filterSet, MeadowHomeLayout.SwampWater + 0.3f, 40f, 0.3f, 1f);

        // ground cover (one prototype per stamp). Komorebi demo values: Grass_1 density 1.7, w 1.0-1.2, h 0.9-1.2.
        var grassLayers = new[] { "MH_Grass", "MH_GrassCool", "MH_GrassMeadow" };
        // Meadow after the BK Pure Nature Meadows demo: a short bright lawn, flowers in hard-edged drifts of one to three
        // species with plain lawn between (high noise amplitude = crisp drifts), barley tufts, tall grass only at tree feet.
        void Proto(DetailStamp s, float align, float jitter, float spread) { s.prototype.alignToGround = align; s.prototype.positionJitter = jitter; s.prototype.noiseSpread = spread; }
        var lawnA = D("Meadow Lawn A", BKP + "Grass1.prefab", new Vector2(1.0f, 1.5f), new Vector2(1.0f, 1.6f), 2.0f, "Meadow"); TexFilter(lawnA.filterSet, grassLayers); Proto(lawnA, 1, 0.3f, 2.5f);
        var lawnB = D("Meadow Lawn B", BKP + "Grass2.prefab", new Vector2(1.0f, 1.5f), new Vector2(1.0f, 1.5f), 1.8f, "Meadow"); TexFilter(lawnB.filterSet, grassLayers); Proto(lawnB, 1, 0.3f, 1.8f);
        var mgl = D("Meadow Tall Grass", DetailPrefab(KN + "Grass_1_Large.prefab"), new Vector2(0.9f, 1.15f), new Vector2(0.9f, 1.2f), 0.9f, "Meadow"); TexFilter(mgl.filterSet, grassLayers);
        mgl.maxDistanceFromTree = 7f; mgl.filterSet.weight = 0.7f; Veg.Noise(mgl.filterSet.weightNoise, 2, 30, 6f, 0.10f);
        // heads speckle the lawn in loose patches (BK foreground), fields and lavender form large drifts, gorse adds yellow accents
        var daisy = D("Meadow Daisies", BKP + "Daisy.prefab", new Vector2(1.0f, 1.6f), new Vector2(1.5f, 3.5f), 1.5f, "MeadowFlowers"); TexFilter(daisy.filterSet, grassLayers); Proto(daisy, 1, 0.7f, 1.6f);
        daisy.minDistanceFromTree = 4f; Veg.Noise(daisy.filterSet.weightNoise, 2, 30, 6f, 0.12f, 0f);
        var blue = D("Meadow Blue Heads", BKP + "CommonBlue.prefab", new Vector2(1.0f, 1.6f), new Vector2(1.5f, 3.5f), 1.2f, "MeadowFlowers"); TexFilter(blue.filterSet, grassLayers); Proto(blue, 1, 0.7f, 1.0f);
        blue.minDistanceFromTree = 4f; Veg.Noise(blue.filterSet.weightNoise, 2, 30, 6f, 0.12f, 0.15f);
        var pink = D("Meadow Pink Heads", BKP + "CommonPink.prefab", new Vector2(1.0f, 1.6f), new Vector2(1.0f, 3.0f), 1.0f, "MeadowFlowers"); TexFilter(pink.filterSet, grassLayers); Proto(pink, 1, 0.7f, 0.5f);
        pink.minDistanceFromTree = 4f; Veg.Noise(pink.filterSet.weightNoise, 2, 30, 6f, 0.14f, 0.45f);
        var orange = D("Meadow Orange Field", BKP + "FlowerMeadow1.prefab", new Vector2(0.7f, 1.1f), new Vector2(0.7f, 1.1f), 1.0f, "MeadowFlowers"); TexFilter(orange.filterSet, grassLayers); Proto(orange, 0.5f, 0.7f, 0.4f);
        Veg.Slope(orange.filterSet, 0, 20, 0, 4); Veg.Noise(orange.filterSet.weightNoise, 2, 20, 10f, 0.17f, 2f);
        var blueField = D("Meadow Blue Field", BKP + "FlowerMeadow3.prefab", new Vector2(0.7f, 1.1f), new Vector2(0.7f, 1.1f), 0.8f, "MeadowFlowers"); TexFilter(blueField.filterSet, grassLayers); Proto(blueField, 0.5f, 0.7f, 0.55f);
        Veg.Slope(blueField.filterSet, 0, 20, 0, 4); Veg.Noise(blueField.filterSet.weightNoise, 2, 20, 10f, 0.17f, 2f);
        var lavender = D("Meadow Lavender", BKP + "Lavender.prefab", new Vector2(0.8f, 1.3f), new Vector2(0.8f, 1.3f), 2.0f, "MeadowFlowers"); TexFilter(lavender.filterSet, grassLayers); Proto(lavender, 0, 0.7f, 0.6f);
        lavender.minDistanceFromTree = 5f; Veg.Slope(lavender.filterSet, 0, 27, 0, 6); Veg.Noise(lavender.filterSet.weightNoise, 2, 16, 8f, 0.12f, 5f);
        var gorse = D("Meadow Gorse", BKP + "Gorse1.prefab", new Vector2(0.8f, 1.2f), new Vector2(0.8f, 1.2f), 0.8f, "MeadowFlowers"); TexFilter(gorse.filterSet, grassLayers); Proto(gorse, 0, 0.7f, 2.4f);
        gorse.minDistanceFromTree = 5f; Veg.Noise(gorse.filterSet.weightNoise, 2, 45, 10f, 0.2f, 3f);
        var barley = D("Meadow Barley Tufts", BKP + "GrassMeadows3.prefab", new Vector2(0.6f, 0.9f), new Vector2(0.5f, 0.8f), 0.8f, "Meadow"); TexFilter(barley.filterSet, grassLayers); Proto(barley, 0, 0.7f, 0.5f);
        barley.filterSet.weight = 0.7f; Veg.Noise(barley.filterSet.weightNoise, 2, 90, 12f, 0.21f, 7f);
        var wg = D("Wild Grass", KN + "Grass_1.prefab", new Vector2(0.9f, 1.15f), new Vector2(0.75f, 1.05f), 1.3f, "Wild"); TexFilter(wg.filterSet, grassLayers);
        Veg.Slope(wg.filterSet, 0, 46, 0, 6);
        var wt = D("Wild Tufts", DetailPrefab(KN + "Grass_1_Large.prefab"), new Vector2(0.9f, 1.2f), new Vector2(0.9f, 1.2f), 0.7f, "Wild"); TexFilter(wt.filterSet, grassLayers);
        Veg.Noise(wt.filterSet.weightNoise, 2, 35, 2.5f, 0.15f);
        var wf = D("Wild Flowers", DetailPrefab(HF + "Flowers_1.prefab"), new Vector2(0.9f, 1.1f), new Vector2(0.9f, 1.1f), 1.0f, "Wild"); TexFilter(wf.filterSet, grassLayers);
        Veg.Noise(wf.filterSet.weightNoise, 2, 30, 3f, 0.42f);
        var cl = D("Clovers", KN + "Clovers_1.prefab", new Vector2(0.9f, 1.2f), new Vector2(0.9f, 1.2f), 1.4f, "Wild");
        Veg.Noise(cl.filterSet.weightNoise, 2, 45, 3f, 0.3f);
        var f1 = D("Ferns A", KN + "Fern_1.prefab", new Vector2(0.8f, 1.2f), new Vector2(0.8f, 1.2f), 1.0f, "Forest"); f1.maxDistanceFromTree = 7f;
        Veg.Noise(f1.filterSet.weightNoise, 2, 45, 3f, 0.25f);
        var f2 = D("Ferns B", KN + "Fern_3.prefab", new Vector2(0.8f, 1.2f), new Vector2(0.8f, 1.2f), 1.0f, "Forest"); f2.maxDistanceFromTree = 7f;
        Veg.Noise(f2.filterSet.weightNoise, 2, 45, 3f, 0.25f);
        var lv = D("Needle Litter", KN + "Fallen_Leaves_1.prefab", new Vector2(0.8f, 1.3f), new Vector2(0.8f, 1.3f), 1.2f, "Forest"); lv.maxDistanceFromTree = 5.5f;
        var rd = D("Reeds", BKP + "Reeds.prefab", new Vector2(0.8f, 1.3f), new Vector2(0.9f, 1.4f), 1.8f, "Reeds");
        Veg.Noise(rd.filterSet.weightNoise, 2, 70, 3.5f, 0.38f);   // clumps, not a hedge
        // Komorebi paints its three ferns together along the water
        var wefe = D("Waterside Ferns", KN + "Fern_2.prefab", new Vector2(0.8f, 1.1f), new Vector2(0.8f, 1.1f), 1.0f, "WaterEdge");
        Veg.Noise(wefe.filterSet.weightNoise, 2, 45, 3f, 0.35f);
        var wefe1 = D("Waterside Ferns B", KN + "Fern_1.prefab", new Vector2(0.8f, 1.1f), new Vector2(0.8f, 1.1f), 0.8f, "WaterEdge");
        Veg.Noise(wefe1.filterSet.weightNoise, 2, 45, 3f, 0.35f, 0.4f);
        var wefe3 = D("Waterside Ferns C", KN + "Fern_3.prefab", new Vector2(0.8f, 1.1f), new Vector2(0.8f, 1.1f), 0.8f, "WaterEdge");
        Veg.Noise(wefe3.filterSet.weightNoise, 2, 45, 3f, 0.35f, 0.8f);
        var wefa = D("Waterside Farfugium", KN + "Farfugium_2.prefab", new Vector2(1.0f, 1.3f), new Vector2(1.0f, 1.3f), 0.6f, "WaterEdge");
        Veg.Noise(wefa.filterSet.weightNoise, 2, 45, 3f, 0.45f);
        var sc = D("Highland Scree", HF + "Stones.prefab", new Vector2(0.5f, 0.9f), new Vector2(0.5f, 0.9f), 0.35f, "Highlands");
        Veg.Noise(sc.filterSet.weightNoise, 2, 40, 3f, 0.35f);   // scattered patches, not a carpet
        TexFilter(sc.filterSet, new[] { "MH_Pebbles", "MH_Rock", "MH_Dirt" });
        var sm = D("Swamp Marsh Grass", DetailPrefab(KN + "Grass_1_Large.prefab"), new Vector2(0.9f, 1.3f), new Vector2(1.0f, 1.4f), 1.2f, "Swamp");
        Veg.Height(sm.filterSet, MeadowHomeLayout.SwampWater + 0.05f, 30f, 0.15f, 1f);

        // clearing: removes spawns of the stamps above
        var ct = Clear(clear, "Clear Trees", M("ClearTrees"), true, false);
        var cd = Clear(clear, "Clear Details", M("ClearDetails"), false, true);

        foreach (var st in root.GetComponentsInChildren<Stamp>(true)) EditorUtility.SetDirty(st);
        EditorSceneManager.MarkSceneDirty(mv.gameObject.scene);
        mv.Invalidate();
        return new
        {
            trees = trees.GetComponentsInChildren<TreeStamp>().Length,
            bushes = bushes.GetComponentsInChildren<TreeStamp>().Length,
            details = ground.GetComponentsInChildren<DetailStamp>().Length,
        };
    }

    static void TexFilter(FilterSet f, string[] layers)
    {
        f.textureFilterEnabled = true;
        f.otherTextureWeight = 0;
        f.textureFilters = layers.Select(n => new FilterSet.TextureFilter
        {
            layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Layers + n + ".terrainlayer") ?? throw new ArgumentException("Missing layer " + n),
            weight = 1, amplitude = 1, balance = 0,
        }).ToList();
    }

    static ClearStamp Clear(Transform parent, string name, Texture2D mask, bool trees, bool details)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var s = go.AddComponent<ClearStamp>();
        s.stampVersion = 1;
        s.filterSet.version = 2;
        s.clearTrees = trees; s.clearDetails = details; s.clearObjects = false;
        Veg.Mask(s.filterSet, mask);
        go.transform.position = new Vector3(500, 0, 500);
        go.transform.localScale = new Vector3(1000, 1, 1000);
        return s;
    }

    // Raygeas wind drivers: one for the Komorebi graphs, one shared driver for Grasslands/Lakefell/HIGHLANDS shaders.
    public static object Wind()
    {
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().First(g => g.name == "Environment");
        var old = env.transform.Find("Wind");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("Wind").transform;
        root.SetParent(env.transform, false);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Wind");
        var dir = Quaternion.Euler(0, 55, 0);   // gentle breeze from the south-west, across the meadow toward the lake
        foreach (var path in new[] { "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/Wind_Controller.prefab", "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Wind Settings.prefab" })
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), root);
            go.transform.SetPositionAndRotation(new Vector3(326, 40, 274), dir);
        }
        FoliageGlobals();
        EditorSceneManager.MarkSceneDirty(scene);
        return root.GetComponentsInChildren<MonoBehaviour>().Select(m => m.GetType().FullName).ToArray();
    }

    // BK plants (meadow lawn and flowers, reeds) need their shader globals set, or they fade out beyond ~50 m.
    public static object FoliageGlobals()
    {
        var env = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Environment");
        var wind = env.transform.Find("Wind");
        if (wind == null) { wind = new GameObject("Wind").transform; wind.SetParent(env.transform, false); }
        var old = wind.Find("BK Foliage Globals");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject("BK Foliage Globals");
        Undo.RegisterCreatedObjectUndo(go, "BK Foliage Globals");
        go.transform.SetParent(wind, false);
        var type = Type.GetType("Aipocalypse.Core.EnvironmentSystem.BKFoliageGlobals, Aipocalypse.Core") ?? throw new InvalidOperationException("BKFoliageGlobals not compiled");
        go.AddComponent(type);
        EditorSceneManager.MarkSceneDirty(env.scene);
        return go.name;
    }
}

// ---------------- Finishing touches (islands, player spawn) ----------------
public static class Finishing
{
    const string KN = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/";

    // Lake islands: a framing pine or two, bushes, a stone ring (terrain trees skip them: too close to water).
    public static object Islands(int seed)
    {
        var terrain = Terrain.activeTerrain;
        var rng = new System.Random(seed);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float H(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y));
        var env = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Environment");
        var dressing = env.transform.Find("Dressing") ?? new GameObject("Dressing").transform;
        dressing.SetParent(env.transform, false);
        var old = dressing.Find("Islands");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("Islands").transform; root.SetParent(dressing, false);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Islands");
        GameObject Put(string path, Vector2 at, float yOff, Vector3 euler, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), root);
            go.transform.SetPositionAndRotation(new Vector3(at.x, H(at) + yOff, at.y), Quaternion.Euler(euler));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }
        int n = 0;
        foreach (var (x, z, top, radius) in new[] { (675f, 270f, 2.4f, 12f), (726f, 247f, 1.8f, 9f), (763f, 394f, 2.2f, 11f), (808f, 428f, 1.6f, 8f) })
        {
            var c = new Vector2(x, z);
            int pines = radius > 10 ? 2 : 1;
            for (int i = 0; i < pines; i++)
            {
                var p = c + new Vector2(R(-1, 1), R(-1, 1)) * radius * 0.2f;
                Put(KN + "Nature/PineTree_" + (3 + rng.Next(2)) + ".prefab", p, -0.2f, new Vector3(0, R(0, 360), 0), R(0.6f, 0.85f)); n++;
            }
            for (int i = 0; i < 3 + rng.Next(2); i++)
            {
                float a = R(0, Mathf.PI * 2);
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * R(0.25f, 0.45f);
                string bush = rng.NextDouble() < 0.5 ? "Nature/Bush_" + (1 + rng.Next(2)) + "_Flowers.prefab" : "Nature/Bush_" + (1 + rng.Next(3)) + ".prefab";
                Put(KN + bush, p, -0.1f, new Vector3(0, R(0, 360), 0), R(0.8f, 1.05f)); n++;
            }
            int stones = 8 + rng.Next(6);
            for (int i = 0; i < stones; i++)
            {
                float a = i / (float)stones * Mathf.PI * 2 + R(-0.2f, 0.2f);
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * R(0.5f, 0.68f);
                float sc = R(1.4f, 3.2f);
                Put(KN + "Rocks/SmallStone_" + (1 + rng.Next(4)) + ".prefab", p, -0.12f * sc, new Vector3(R(-15, 15), R(0, 360), R(-15, 15)), sc); n++;
            }
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return new { objects = n };
    }

    // Player starts by the campfire, looking out of the gate toward the bridge.
    public static object PlayerSpawn()
    {
        var player = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        if (player == null) return "no Player root";
        var terrain = Terrain.activeTerrain;
        var at = new Vector3(333.5f, 0, 281.5f);
        at.y = terrain.SampleHeight(at) + 0.1f;
        Undo.RecordObject(player.transform, "Player spawn");
        player.transform.SetPositionAndRotation(at, Quaternion.Euler(0, 345f, 0));
        EditorSceneManager.MarkSceneDirty(player.scene);
        return player.transform.position.ToString("F2");
    }

    // Soft sky-bounce fill from the north: lifts north-facing cliffs that the late-morning sun leaves black. No shadows.
    public static object FillLight(float intensity)
    {
        var scene = SceneManager.GetActiveScene();
        var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Fill Light (sky bounce)");
        var go = existing ?? new GameObject("Fill Light (sky bounce)");
        if (existing == null) Undo.RegisterCreatedObjectUndo(go, "Fill light");
        var light = go.GetComponent<Light>();
        if (light == null) light = go.AddComponent<Light>();   // Unity's fake null: ?? would skip this
        light.type = LightType.Directional;
        light.color = new Color(0.62f, 0.72f, 0.92f);
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        go.transform.rotation = Quaternion.Euler(28f, 165f, 0f);
        EditorSceneManager.MarkSceneDirty(scene);
        return go.name;
    }

    public static object Ambient(float er, float eg, float eb, float gr, float gg, float gb)
    {
        RenderSettings.ambientEquatorColor = new Color(er, eg, eb);
        RenderSettings.ambientGroundColor = new Color(gr, gg, gb);
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return RenderSettings.ambientEquatorColor.ToString();
    }
}
