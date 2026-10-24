// World Map terrain layout generator (run via `unity command run_script`, editor only, outside Assets/).
// Layout traced from docs/concepts/world-map-top-down-v13.png on a 1000 x 1000 m terrain (x east, z north, metres).
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

public static class WorldMapLayout
{
    public const string MapFolder = "Assets/_Game/Maps/WorldMap";
    public const string KitFolder = "Assets/_Game/Environment";
    public const string HeightmapFolder = MapFolder + "/Terrain/Heightmaps";
    public const float Size = 1000f;
    public const float MaxHeight = 300f;

    // ---------------- Layout data ----------------

    // Foot of the boundary mountains (inside = playable).
    public static readonly Vector2[] Playable = SmoothClosed(new[]
    {
        V(234,334), V(232,290), V(230,245), V(238,214), V(262,200), V(310,194), V(370,192), V(430,194), V(485,190), V(520,172),
        V(548,138), V(584,104), V(630,80), V(690,68), V(760,66), V(830,72), V(884,88), V(924,116), V(948,160), V(958,220), V(963,290),
        V(964,360), V(962,440), V(958,520), V(954,600), V(950,680), V(947,760), V(942,830), V(930,880), V(906,918), V(870,936),
        V(820,942), V(750,943), V(680,940), V(612,930), V(558,910), V(515,884), V(474,864), V(420,856), V(340,854), V(260,852),
        V(200,848), V(162,826), V(154,760), V(156,690), V(158,640), V(162,600), V(170,560), V(176,520), V(182,486), V(184,456),
        V(186,430), V(194,404), V(212,384), V(226,362),
    }, 4);

    public static readonly Vector2[] HighlandsArea = SmoothClosed(new[]
    {
        V(140,600), V(230,590), V(300,600), V(360,615), V(420,635), V(470,655), V(505,690), V(515,750), V(500,800), V(520,870), V(140,870),
    }, 4);

    // A round meadow from the home pond along the river to the lake, south to the foot of the near range (about 8 m inside
    // the playable edge); the home sits on a rise at its west end.
    public static readonly Vector2[] MeadowArea = SmoothClosed(new[]
    {
        V(244,372), V(238,330), V(238,275), V(239,242), V(243,224), V(254,212), V(272,206), V(310,202), V(370,200), V(430,202),
        V(470,201), V(492,199), V(505,214), V(512,240), V(500,256), V(470,272), V(430,286), V(390,302), V(350,320), V(310,338), V(275,358),
    }, 4);

    public static readonly Vector2[] SwampArea = SmoothClosed(new[]
    {
        V(545,782), V(558,835), V(586,872), V(630,900), V(695,908), V(755,896), V(800,866), V(820,820), V(824,765), V(812,718),
        V(786,688), V(748,670), V(705,668), V(662,674), V(622,688), V(578,706), V(546,738),
    }, 4);

    public const float SwampWater = 24.5f, SwampGround = 24.85f;
    // the home pond and the river sit at the lake's level: one flat water height for underwater rendering
    public const float LakeLevel = 19.5f;
    // The home pond's shore, islands and dressing frame were traced at the fall (206, 433), heading 100, scale 0.88 (about
    // 82 x 66 m). PondXf turns that shape about the fall to PondHeading and stretches it to about 2.5 times its area:
    // PondAlong along the flow, PondLeft on the north (left) bank, PondRight on the south bank, so it grows into the forest
    // and stays clear of the bridge and the trails.
    public static readonly Vector2 PondFall = V(206, 433);
    public const float TracedHeading = 100f, PondHeading = 85f, PondAlong = 1.2f, PondLeft = 3.3f, PondRight = 1.3f;
    static Vector2 Dir(float heading) => V(Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad));
    public static Vector2 TracedForward => Dir(TracedHeading);
    public static Vector2 TracedSide => V(TracedForward.y, -TracedForward.x);   // right of the flow: the south bank
    public static Vector2 PondForward => Dir(PondHeading);
    public static Vector2 PondSide => V(PondForward.y, -PondForward.x);
    public static Vector2 PondXf(Vector2 old)
    {
        var d = old - PondFall;
        float s = Vector2.Dot(d, TracedForward), t = Vector2.Dot(d, TracedSide);
        return PondFall + PondForward * (s * PondAlong) + PondSide * (t * (t < 0 ? PondLeft : PondRight));
    }
    public static Vector2 PondXfInverse(Vector2 p)
    {
        var d = p - PondFall;
        float s = Vector2.Dot(d, PondForward), t = Vector2.Dot(d, PondSide);
        return PondFall + TracedForward * (s / PondAlong) + TracedSide * (t / (t < 0 ? PondLeft : PondRight));
    }
    static Vector2 PondXf(float x, float z) => PondXf(V(x, z));
    // a radius at an old pond position, scaled with the local stretch
    public static float PondR(Vector2 old, float r) => r * Mathf.Sqrt(PondAlong * (Vector2.Dot(old - PondFall, TracedSide) < 0 ? PondLeft : PondRight));
    public static readonly Vector2 HomeBase = V(267, 282);
    public const float HomeBaseHeight = 29.5f;
    public static readonly Vector2 Bridge = V(312, 362);
    public static readonly Vector2 Viewpoint = V(526, 196);

    // Home yard frame: local +Z (house door, gate, spawn view) faces bearing HomeYaw, toward the meadow and the bridge.
    public const float HomeYaw = 50f;
    public static readonly Vector2 FenceHalf = V(17f, 13.5f);         // half extents along local X, Z (the drawn 34 x 27 m yard)
    public const float FencePanel = 2.25f;                            // Lakefell Fence_1-3 length
    public static readonly int GateSlot = BridgeGateSlot();           // panel slot of the +Z fence, counted from its +X corner
    public static readonly float GateLocalX = FenceHalf.x - FenceSeg(2 * FenceHalf.x) * (GateSlot + 0.5f);
    public static readonly Vector2 HomeGate = Yard(GateLocalX, FenceHalf.y);
    public static readonly Vector2 HomeSpawn = HomeGate - 5f * (Bridge - HomeGate).normalized;

    // Unity yaw: local +X -> (cos, -sin), local +Z -> (sin, cos).
    public static Vector2 Rot(Vector2 v, float yawDeg)
    {
        float a = yawDeg * Mathf.Deg2Rad;
        return new Vector2(v.x * Mathf.Cos(a) + v.y * Mathf.Sin(a), -v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));
    }
    public static Vector2 Yard(float x, float z) => HomeBase + Rot(new Vector2(x, z), HomeYaw);
    public static Vector2 YardLocal(Vector2 p) => Rot(p - HomeBase, -HomeYaw);
    public static float FenceSeg(float length) => length / Mathf.Max(1, Mathf.RoundToInt(length / FencePanel));

    // Signed distance to the fence rectangle (negative inside).
    public static float FenceDistance(Vector2 p)
    {
        var l = YardLocal(p);
        float dx = Mathf.Abs(l.x) - FenceHalf.x, dz = Mathf.Abs(l.y) - FenceHalf.y;
        return dx > 0 || dz > 0 ? new Vector2(Mathf.Max(dx, 0), Mathf.Max(dz, 0)).magnitude : Mathf.Max(dx, dz);
    }

    // The front-fence slot that the gate->bridge line crosses, kept off the corner slots.
    static int BridgeGateSlot()
    {
        var b = YardLocal(Bridge);
        int n = Mathf.RoundToInt(2 * FenceHalf.x / FencePanel);
        return Mathf.Clamp(Mathf.FloorToInt((FenceHalf.x - FenceHalf.y * b.x / b.y) / FenceSeg(2 * FenceHalf.x)), 1, n - 2);
    }

    const string HomeGL = RaygeasEnv + "Grasslands - Stylized Nature/Assets/Prefabs/Vegetation/";
    const string HomeKN = RaygeasEnv + "Komorebi Nature/Assets/Prefabs/Nature/";
    // Trees around the home base in yard-local metres (Yard(x, z)); the bridge, east and waterfall views stay open
    public static readonly (Vector2 at, string prefab, float scale, float yaw, float sink)[] HomeTrees =
    {
        (V(5, -19), HomeGL + "Broadleaf_Large_1.prefab", 0.85f, 250f, 0.2f),    // H1 behind the house, seen from the bridge
        (V(-9, -21), HomeGL + "Broadleaf_Medium_2.prefab", 0.95f, 80f, 0.2f),   // H2
        (V(-22, -6), HomeGL + "Birch_Medium_1.prefab", 0.9f, 210f, 0.2f),       // H3 autumn birch, west
        (V(23, -1), HomeGL + "Broadleaf_Medium_2.prefab", 1.0f, 300f, 0.2f),    // H4 shade tree, east gable side
        (V(20, 12), HomeGL + "Broadleaf_Small_2.prefab", 1.0f, 160f, 0.2f),     // H5 right edge of the east view
    };
    public static readonly (Vector2 at, string prefab, float scale, float yaw)[] HomeBushes =
    {
        (V(-18.6f, -15.6f), HomeKN + "Bush_2_Flowers.prefab", 1.0f, 20f), (V(-16.2f, -16.6f), HomeKN + "Bush_1.prefab", 0.9f, 140f),
        (V(18.6f, -15.4f), HomeGL + "Bush_Medium.prefab", 1.0f, 70f), (V(16.4f, -16.4f), HomeKN + "Bush_3_Flowers.prefab", 0.85f, 250f),
        (V(19.0f, 15.6f), HomeKN + "Bush_1_Flowers.prefab", 1.0f, 300f), (V(-24.5f, -1.5f), HomeGL + "Bush_Medium.prefab", 0.9f, 190f),
    };
    // spike drifts across the sight lines from the spawn: (bearing from HomeSpawn, distance, half length, half width); long axis = bearing + 90
    public static readonly (float brg, float dist, float len, float wid)[] SpikeDrifts =
    {
        (0f, 26f, 10f, 4f), (55f, 27f, 9f, 4f), (118f, 28f, 10f, 4.5f), (292f, 27f, 12f, 4f), (168f, 30f, 10f, 4f),
    };

    public class Basin
    {
        public string name; public Vector2[] shore; public float level, depth, deepDistance, bankSlope; public Rect bounds;
    }

    public static readonly Basin[] Basins =
    {
        // a round lake: the forest-side shore (north-west) as before, a wide arc to the south and east; the river mouth is a
        // blunt opening as wide as the flared channel (a pointed tip read as a wedge of water)
        new Basin { name = "GreatLake", level = LakeLevel, depth = 10f, deepDistance = 90f, bankSlope = 0.2f, shore = SmoothClosed(new[]
        {
            V(512,309), V(530,316), V(552,316), V(576,318), V(599,322), V(637,344), V(670,362), V(703,370), V(730,403), V(758,438),
            V(780,473), V(790,508), V(797,536), V(812,548), V(832,550), V(858,532), V(884,502), V(904,462), V(918,412), V(920,372),
            V(906,352), V(888,336), V(896,318), V(916,300), V(908,252), V(884,206), V(852,166), V(814,138), V(772,124), V(742,104),
            V(705,96), V(660,104), V(622,112), V(590,130), V(566,154), V(545,185), V(534,220), V(522,248), V(506,262),
        }, 4) },
        // the home pond: the Komorebi demo pond's shore traced from its terrain (research4/komorebi_pond.md), scaled 0.88 and
        // laid along our waterfall (206, 433) heading 100 and stretched by PondXf: a funnel from the fall, a back basin with two
        // islets, two arms around the central island and two front bays; the river leaves from the right-front bay. Steeper
        // banks than the other basins: a gentler cone from this big, low pond cut the West Stream banks and the West Pond rim.
        new Basin { name = "WaterfallPond", level = LakeLevel, depth = 4.8f, deepDistance = 13f, bankSlope = 0.5f, shore = SmoothClosed(new[]
        {
            V(203.0f,432.0f), V(204.1f,427.5f), V(215.3f,410.5f), V(230.4f,396.9f), V(242.9f,390.0f), V(250.8f,390.0f), V(263.4f,386.2f), V(275.5f,388.1f),
            V(281.4f,391.3f), V(283.4f,395.9f), V(283.5f,400.9f), V(282.9f,404.9f), V(279.0f,413.5f), V(280.3f,416.3f), V(284.7f,420.0f), V(285.6f,424.1f),
            V(283.8f,433.1f), V(279.9f,438.6f), V(270.3f,438.5f), V(259.6f,441.9f), V(246.3f,451.5f), V(237.7f,452.6f), V(231.7f,450.6f), V(224.7f,442.9f),
            V(219.6f,443.5f), V(211.9f,441.5f), V(204.4f,437.3f),
        }.Select(v => PondXf(v)).ToArray(), 4) },
        new Basin { name = "WestPond", level = 50.0f, depth = 3.0f, deepDistance = 10f, bankSlope = 0.35f, shore = Ellipse(V(196,640), 28, 23, 20, 12, 21) },
        new Basin { name = "NorthPond", level = 44.0f, depth = 3.0f, deepDistance = 10f, bankSlope = 0.35f, shore = Ellipse(V(386,806), 52, 20, 12, 16, 31) },
    };

    // Water lines: knots (x, z, surface height, half width) smoothed with a gentle meander. Width and depth vary along the
    // line with noise (pools and runs: deeper where narrower), so no river reads as a uniform canal. depth = mean deepest
    // point below the surface. narrowAt pinches the channel to narrowTo of its width (the river at the bridge).
    public class Line
    {
        public string name; public Vector4[] knots; public float depth, bankSlope, meander; public int seed; public bool fixedSlope; public Rect bounds;
        public Vector2 narrowAt; public float narrowTo = 1f;
        public int endsInBasin = -1;   // bit 0: first knot in a lake or pond, bit 1: last knot (computed on first use)
        volatile Vector4[] pts_; float[] depth_;

        public Vector4[] pts
        {
            get
            {
                var p = pts_;
                if (p != null) return p;
                lock (this) { if (pts_ == null) Build(); return pts_; }
            }
        }

        // Deepest point below the surface at segment i, fraction t.
        public float DepthAt(int i, float t)
        {
            _ = pts;
            return Mathf.Lerp(depth_[i], depth_[Mathf.Min(i + 1, depth_.Length - 1)], t);
        }

        void Build()
        {
            var p = Smooth(knots, 6, meander, seed);
            var arc = new float[p.Length];
            for (int i = 1; i < p.Length; i++) arc[i] = arc[i - 1] + Vector2.Distance(new Vector2(p[i - 1].x, p[i - 1].y), new Vector2(p[i].x, p[i].y));
            float total = arc[^1];
            var d = new float[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                float env = SmoothStep(0, 35, Mathf.Min(arc[i], total - arc[i]));   // ends keep their designed width (mouths, pond outlets)
                float wv = Mathf.Clamp(1f + env * (0.26f * Noise(arc[i] / 85f + seed * 2.31f, seed * 0.71f) + 0.1f * Noise(arc[i] / 27f + seed * 1.13f, seed * 1.93f + 5f)), 0.7f, 1.38f);
                if (narrowTo < 1f) wv *= Mathf.Lerp(narrowTo, 1f, SmoothStep(12, 45, Vector2.Distance(new Vector2(p[i].x, p[i].y), narrowAt)));
                p[i].w *= wv;
                d[i] = depth * Mathf.Clamp(1f + env * (0.5f * (1f - wv) + 0.15f * Noise(arc[i] / 45f + seed * 3.7f, seed * 2.9f + 11f)), 0.7f, 1.35f);
            }
            depth_ = d;
            pts_ = p;   // last: readers test pts_
        }
    }

    // Rivers about twice and streams about 2.5 times their first width, each about 0.3-0.5 m deeper.
    public static readonly Line[] Lines =
    {
        // starts in the stretched pond's right-front bay (the old first knot moved by PondXf) and keeps its old course from
        // (292, 384), so it still crosses the bridge square to the trails
        new Line { name = "River", depth = 4.5f, bankSlope = 0.3f, meander = 4f, seed = 5, narrowAt = V(312, 362), narrowTo = 0.85f, knots = new[]
        {
            P(PondXf(264,402).x,PondXf(264,402).y,LakeLevel,12), P(292,384,LakeLevel,15), P(312,362,LakeLevel,16.2f), P(348,339,LakeLevel,17.4f), P(386,321,LakeLevel,18),
            P(430,303,LakeLevel,18.6f), P(468,289,LakeLevel,19.8f), P(490,284,LakeLevel,21), P(508,283,LakeLevel,22.5f), P(524,285,LakeLevel,23.5f), P(542,289,LakeLevel,24.5f),
        } },
        new Line { name = "WestStream", depth = 1.2f, bankSlope = 0.45f, meander = 2.5f, seed = 10, knots = new[]
        {
            P(198,628,50.0f,4), P(201,621,50.0f,4), P(205,611,45.5f,4.2f), P(209,600,41.0f,4.5f), P(218,584,36.0f,4.5f), P(232,570,33.2f,5),
            P(277,561,32.0f,5.5f), P(331,571,30.8f,6), P(375,590,29.6f,6), P(414,612,28.5f,6.2f), P(446,630,27.5f,6.2f),
            P(496,650,25.8f,6.5f), P(528,656,24.8f,6.5f), P(566,657,24.0f,7),
        } },
        new Line { name = "ForestStream", depth = 1.1f, bankSlope = 0.45f, meander = 2.5f, seed = 12, knots = new[]
        {
            P(331,571,30.8f,5), P(352,550,30.0f,5), P(375,531,29.2f,5.2f), P(424,508,27.8f,5.5f), P(468,490,26.6f,5.8f),
            P(517,470,25.4f,6), P(555,444,24.2f,6.2f), P(599,420,22.9f,6.5f), P(637,397,21.6f,7), P(659,373,20.3f,7.5f),
            P(672,356,19.6f,8.5f), P(684,342,19.5f,9.5f),
        } },
        new Line { name = "NorthStream", depth = 1.0f, bankSlope = 0.45f, meander = 2f, seed = 11, knots = new[]
        {
            P(405,800,44.0f,3.8f), P(419,795,44.0f,4), P(440,786,43.0f,4), P(450,782,40.0f,4.2f), P(460,778,36.5f,4.5f), P(480,772,31.5f,4.8f),
            P(500,769,27.0f,5), P(518,768,25.0f,5.5f), P(540,772,24.6f,6), P(562,777,24.5f,6.2f),
        } },
        new Line { name = "SwampOutlet", depth = 1.1f, bankSlope = 0.4f, meander = 1.5f, seed = 11, knots = new[]
        {
            P(586,730,24.5f,5), P(579,707,24.4f,5.2f), P(572,683,24.25f,5.8f), P(566,657,24.0f,6.2f),
        } },
        new Line { name = "MarginStream", depth = 1.3f, bankSlope = 0.4f, meander = 2.5f, seed = 12, knots = new[]
        {
            P(566,657,24.0f,7), P(610,626,23.5f,7.5f), P(654,598,22.9f,7.5f), P(690,566,22.1f,8), P(716,534,21.0f,8.5f),
            P(738,506,20.1f,9), P(760,476,19.6f,10), P(778,456,19.5f,11), P(794,440,19.5f,12),
        } },
        // the lake's outflow: north from the lake's north-east tip between the swamp and the east edge, off the north edge
        new Line { name = "Outlet", depth = 2.7f, bankSlope = 0.3f, meander = 5f, seed = 13, knots = new[]
        {
            P(818,520,19.5f,16), P(828,556,19.48f,15), P(846,600,19.42f,15), P(866,648,19.34f,15.5f), P(884,700,19.26f,16),
            P(896,756,19.18f,16), P(902,812,19.08f,16), P(906,866,18.96f,16), P(910,912,18.84f,16), P(916,950,18.68f,16.5f),
            P(922,980,18.55f,17), P(928,1006,18.42f,17),
        } },
    };

    // Lines drawn as wide rivers (river ribbon and bank dressing), the others are streams.
    public static bool IsRiver(Line l) => l.name == "River" || l.name == "Outlet";


    // Regional elevation anchors (x, z, height, radius) blended with Gaussian weights.
    static readonly Vector4[] Anchors =
    {
        // meadow
        A(267,282,26.5f,55), A(302,346,23.5f,18), A(370,260,28,40), A(420,250,29,45), A(470,250,27.5f,40),
        A(500,262,25.5f,35), A(380,300,25.5f,35), A(440,285,25,35), A(330,245,29.5f,30),
        // meadow south, rising gently to the foothills
        A(280,200,27,40), A(350,190,27.5f,45), A(430,185,27,45), A(500,200,25.5f,35), A(300,135,30,35), A(390,125,30.5f,40),
        A(470,130,29,35), A(540,128,27,30),
        // lake south beach and rim
        A(600,85,24.5f,30), A(700,75,24.5f,30), A(800,78,24.5f,30), A(880,98,25,30), A(940,150,25.5f,30), A(560,60,27,35),
        A(650,25,28,40), A(800,25,28,40), A(930,50,28,40),
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
        A(505,838,35,36), A(560,950,36,40), A(640,962,32,38), A(730,966,31,38), A(810,958,30,35), A(660,935,29,28), A(760,925,29,28),
        // forest east of the swamp and north-east of the lake, either side of the outlet
        A(640,655,28,30), A(700,648,27.5f,30), A(760,640,26.5f,28), A(822,712,26.5f,22), A(850,770,28,26), A(852,850,29,28),
        A(930,760,27,32), A(935,850,29,32), A(960,930,31,35), A(870,580,26,30), A(925,600,26.5f,30), A(820,650,27.5f,26), A(930,700,27,30),
        // east lake shore
        A(966,300,24.5f,34), A(968,450,25,34), A(964,600,25.5f,34), A(990,230,27,40), A(995,380,28,40), A(995,520,28,40), A(990,660,29,40),
    };

    // Extra knolls (x, z, height, radius) added on top of the regional field.
    static readonly Vector4[] Knolls =
    {
        A(410,470,9,32), A(642,526,8,26), A(669,462,7,24), A(519,443,7,28), A(745,612,7,26), A(335,520,6,28), A(590,560,7,30),
        A(470,420,6,30), A(720,500,5,25), A(300,440,5,30), A(522,200,3,16),
        A(856,805,5,24), A(450,175,2,30),
    };

    // Rolling meadow (x, z, height, radius): hills (+) and hollows (-) added to the field; see HeightAt.
    static readonly Vector4[] MeadowHills =
    {
        A(258,272,3,50),       // Home Rise, behind the house: the yard sits on its front shoulder, the ground falls away past the gate
        A(298,226,3.5f,30),    // South Knoll
        A(350,270,-2.5f,30),   // Meadow Hollow (the old yard)
        A(384,228,5,36),       // Middle Knoll
        A(420,262,-2.5f,28),   // Middle Hollow
        A(442,214,3,26),       // East Knoll: hides the viewpoint until the trail rounds it
        A(484,226,-2,24),      // Lakeside Hollow
        A(382,286,-1.5f,22),   // River Hollow: opens the lake band from the yard
    };

    // Islands (x, z, top above the water, radius) in the lake and the home pond; the water level comes from the basin
    // that holds them.
    public static readonly Vector4[] Islands =
    {
        A(690,240,3.0f,26), A(810,190,2.2f,18), A(860,380,2.6f,22), A(760,330,1.2f,13), A(620,170,0.8f,14),
        // home pond (Komorebi demo, traced at the old pond frame and moved by PondXf): the central island as two overlapping
        // bumps along the flow, islets W and NE at the back, the flower islet in the left-front bay (the demo's sits in the
        // right-front bay, where our river leaves)
        PondIsland(250.9f,419.7f,2.1f,12.6f), PondIsland(265.2f,417.5f,1.9f,11.8f), PondIsland(230.7f,414.5f,1.1f,8.3f), PondIsland(240.7f,438.5f,0.8f,7.3f), PondIsland(276f,428f,0.6f,6f),
    };

    static Vector4 PondIsland(float x, float z, float top, float r) { var p = PondXf(x, z); return A(p.x, p.y, top, PondR(V(x, z), r)); }

    static float[] islandLevels;

    public static float IslandLevel(int i)
    {
        islandLevels ??= Islands.Select(isl => Basins.FirstOrDefault(b => SignedDistance(new Vector2(isl.x, isl.y), b.shore) < 0)?.level ?? 19.5f).ToArray();
        return islandLevels[i];
    }

    // Rowlan detail stamps (Add, Y = 0, masked). The shader clamps stamp heights at 0, so Add can only raise:
    // Generate() lowers the macro by median * amplitude * mask instead, which makes each stamp zero-mean.
    public class DetailStamp { public string name, texture, mask; public Vector2 center; public float size, amplitude, median; }
    public static readonly DetailStamp[] Details =
    {
        new DetailStamp { name = "Highlands 09", texture = "Highlands/Heightmaps/Highlands 09.png", mask = "Highlands", center = V(315,730), size = 560, amplitude = 26, median = 0.64f },
        new DetailStamp { name = "Highlands 07", texture = "Highlands/Heightmaps/Highlands 07.png", mask = "Highlands", center = V(260,700), size = 300, amplitude = 10, median = 0.68f },
        new DetailStamp { name = "Forest Meadows 05", texture = "Meadows/Heightmaps/Meadows 05.png", mask = "Forest", center = V(540,470), size = 640, amplitude = 9, median = 0.45f },
        new DetailStamp { name = "Forest Meadows 16", texture = "Meadows/Heightmaps/Meadows 16.png", mask = "Forest", center = V(430,500), size = 300, amplitude = 4, median = 0.39f },
        new DetailStamp { name = "Forest NE Meadows 05", texture = "Meadows/Heightmaps/Meadows 05.png", mask = "Forest", center = V(860,790), size = 300, amplitude = 6, median = 0.45f },
        new DetailStamp { name = "Forest E Meadows 16", texture = "Meadows/Heightmaps/Meadows 16.png", mask = "Forest", center = V(900,580), size = 240, amplitude = 4, median = 0.39f },
        new DetailStamp { name = "Meadow Meadows 26", texture = "Meadows/Heightmaps/Meadows 26.png", mask = "Meadow", center = V(380,262), size = 380, amplitude = 3.5f, median = 0.43f },
        new DetailStamp { name = "Swamp 12", texture = "Swamp/Heightmaps/Swamp 12.png", mask = "Swamp", center = V(685,790), size = 380, amplitude = 3.5f, median = 0.40f },
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

    public static string MaskName(DetailStamp d) => "Mask_" + d.name.Replace(" ", "");

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
        EnsureFolder(HeightmapFolder);
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
        WriteR16(HeightmapFolder + "/Base.png", baseMap, res, MaxHeight);

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
        foreach (var kv in masks) WriteR16(HeightmapFolder + "/" + kv.Key + ".png", kv.Value, maskRes, 1f);

        AssetDatabase.Refresh();
        foreach (var name in new[] { "Base" }.Concat(masks.Keys))
            ConfigureImporter(HeightmapFolder + "/" + name + ".png", name == "Base" ? res : maskRes);
        // region masks from the first layout version (one mask per region) are no longer used
        foreach (var stale in new[] { "Rim", "Highlands", "Forest", "Meadow", "Swamp" })
            AssetDatabase.DeleteAsset(HeightmapFolder + "/Mask_" + stale + ".png");

        if (!string.IsNullOrEmpty(previewPath)) WritePreview(previewPath, heights, water, res);
        float min = heights.Min(), max = heights.Max();
        return new { seconds = (DateTime.Now - t0).TotalSeconds, min, max, files = Directory.GetFiles(HeightmapFolder, "*.png").Select(Path.GetFileName).ToArray() };
    }

    // Layout preview that writes no asset: shaded heights, water and region outlines (north up, res a multiple of 1024).
    public static async Task<object> Preview(int res, string path)
    {
        var heights = new float[res * res];
        var water = new float[res * res];
        await Task.Run(() => Parallel.For(0, res, j =>
        {
            for (int i = 0; i < res; i++)
            {
                float x = (i + 0.5f) / res * Size, z = (j + 0.5f) / res * Size;
                heights[j * res + i] = HeightAt(x, z, out water[j * res + i]);
            }
        }));
        WritePreview(path, heights, water, res);
        return path;
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
            float bandIn = b.name == "Swamp" ? 9 : 3, bandOut = b.name == "Swamp" ? 17 : 6;
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
        for (float lz = -FenceHalf.y; lz <= FenceHalf.y; lz += 1) for (float lx = -FenceHalf.x; lx <= FenceHalf.x; lx += 1) { var q = Yard(lx, lz); float h = H(q.x, q.y); padMin = Mathf.Min(padMin, h); padMax = Mathf.Max(padMax, h); }
        report["homePad"] = new { center = Round(H(HomeBase.x, HomeBase.y)), minYard = Round(padMin), maxYard = Round(padMax), distanceToBridge = Round(Vector2.Distance(HomeBase, Bridge)) };
        report["bridge"] = new { riverSurface = LakeLevel, banks = Enumerable.Range(-24, 13).Select(o => $"{o * 2}m:{H(Bridge.x + o * 2 * 0.55f, Bridge.y + o * 2 * 0.83f):0.0}").ToArray() };

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
        float hills = 0;
        foreach (var k in MeadowHills)
        {
            float dx = x - k.x, dz = z - k.y;
            hills += k.z * Mathf.Exp(-(dx * dx + dz * dz) / (k.w * k.w));
        }
        // a raised field near water lets the bank caps draw long straight slopes: hills fade out 25-55 m from water, hollows 6-25 m
        if (Mathf.Abs(hills) > 0.01f) h += hills * (hills > 0 ? SmoothStep(25, 55, WaterEdgeDistance(p)) : SmoothStep(6, 25, WaterEdgeDistance(p)));

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

        // islands in the lake and the home pond
        for (int i = 0; i < Islands.Length; i++)
        {
            var isl = Islands[i];
            float d = Vector2.Distance(p, new Vector2(isl.x, isl.y));
            if (d > isl.w * 2f) continue;
            float n = 1f + 0.25f * Fbm(x / 9f, z / 9f, 2);
            float top = IslandLevel(i) + isl.z - 9f * Mathf.Pow(Mathf.Max(0, d * n / isl.w - 0.35f), 1.6f);
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
            // a lip 0.35 m above the water to 6 m out, then falling away at about 19 deg, so a pond on a slope sits in a bowl
            if (d < 30) levee = Mathf.Max(levee, b.level + Mathf.Min(0.35f, slope * d) - (d > 6 ? (d - 6) * 0.35f : 0));
        }
    }

    static void WaterLine(Vector2 p, Line l, ref float levee, ref float cap, ref float surface)
    {
        if (!LineBounds(l).Contains(p)) return;
        float best = float.MaxValue; Vector4 hit = default; int hitI = 0; float hitT = 0;
        var pts = l.pts;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector4 a = pts[i], b = pts[i + 1];
            var ab = new Vector2(b.x - a.x, b.y - a.y);
            float t = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.y), ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            var q = new Vector2(a.x, a.y) + ab * t;
            float d = Vector2.Distance(p, q);
            if (d < best) { best = d; hit = Vector4.Lerp(a, b, t); hitI = i; hitT = t; }
        }
        if (best > 150) return;
        float s = hit.z, w = hit.w * (l.fixedSlope ? 1f : 1f + 0.15f * Fbm(p.x / 25f, p.y / 25f + 9f, 2));
        if (best < w)
        {
            float u = best / w;
            // flat deep middle, easing up to a shallow shelf at the bank (a parabola is steepest right at the waterline)
            float bed = s - 0.05f - (l.DepthAt(hitI, hitT) - 0.05f) * MouthTaper(p, l) * (1 - SmoothStep(0.25f, 1f, u));
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

    // Channel depth factor: 0 at a line end that lies inside a lake or pond, 1 from ~28 m (rivers) or ~10 m (streams) back.
    // The bed then meets the basin's shelf (the deeper of the two wins) instead of ending in a pit or cutting a trench
    // through the shallows.
    static float MouthTaper(Vector2 p, Line l)
    {
        if (l.endsInBasin < 0)
        {
            bool In(Vector4 k) => Basins.Any(b => SignedDistance(new Vector2(k.x, k.y), b.shore) < -3f);
            l.endsInBasin = (In(l.pts[0]) ? 1 : 0) | (In(l.pts[^1]) ? 2 : 0);   // benign race: every thread computes the same value
        }
        if (l.endsInBasin == 0) return 1f;
        float length = Mathf.Clamp(l.depth * 10f, 8f, 28f), t = 1f;
        if ((l.endsInBasin & 1) != 0) t = Mathf.Min(t, SmoothStep(0, length, Vector2.Distance(p, new Vector2(l.pts[0].x, l.pts[0].y))));
        if ((l.endsInBasin & 2) != 0) t = Mathf.Min(t, SmoothStep(0, length, Vector2.Distance(p, new Vector2(l.pts[^1].x, l.pts[^1].y))));
        return t;
    }

    static Rect LineBounds(Line l)
    {
        if (l.bounds.width == 0) l.bounds = Bounds(l.pts, 150);
        return l.bounds;
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
        // keep the stamps off water edges and the swamp rim: a stamp dip there drops the bank below the water surface
        float waterFade = SmoothStep(3, 26, WaterEdgeDistance(p)) * SmoothStep(6f, 30f, SignedDistance(p, SwampArea));
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

    const string ScenePath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
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
        string maps = HeightmapFolder + "/";

        var b = Stamp(Group(height, "00 Base"), "Base", LoadTex(maps + "Base.png"), HeightStamp.CombineMode.Override, new Vector2(500, 500), Size, 0, MaxHeight);
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
        Undo.RegisterCreatedObjectUndo(go, "World Map stamp");
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
        Undo.RegisterCreatedObjectUndo(go, "World Map texture");
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
        Undo.RegisterCreatedObjectUndo(go, "World Map group");
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
    // Like the concept, the valley is enclosed by grey rocky mountains right at its edge, layered so the far snow peaks still
    // show over them: low foot rocks (0-12 m out) -> near range massifs with pines (row A 50-100 m out, back row 150-200 m)
    // -> snow peaks -> far range. The terrain is never raised; meshes stand in for raised ground. Invisible walls follow
    // the playable edge. One pack per side: south and the lake's south shore ANGRY MESH granite domes and mountains (grey
    // first-party material copies), east and north Grasslands cliff massifs, north-west and west Komorebi stones (the
    // waterfall's pack), snow from Komorebi's snow variants; peaks HIGHLANDS and Grasslands, far range BK Mountains.

    const string Env = "Assets/_StoreAssets/Env/";
    const string HLM = Env + "Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Mountains/";
    const string HLR = Env + "Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
    const string GLR = Env + "Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Rocks/";
    const string GLM = Env + "Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Other/";
    const string KNR = Env + "Raygeas/Komorebi Nature/Assets/Prefabs/";
    const string LFR = Env + "Raygeas/Lakefell Village/Assets/Prefabs/Nature Environment/";
    const string BKH = Env + "BK/PureNature_Meadows/Prefabs/Mountains/";
    const string BKF = Env + "BK/PureNature_Mountains/Prefabs/Mountains/";
    const string BoundaryFolder = KitFolder + "/Mountains";
    const string BackdropFolder = MapFolder + "/Backdrop";
    const string KNM = Env + "Raygeas/Komorebi Nature/Assets/Materials/";

    // Near-range Komorebi stones that face the home and the meadow; the waterfall flank (W-A32) stays bare.
    static readonly HashSet<string> GrassCapped = new() { "W-A33", "W-A35" };

    // Komorebi Rocks/*_NoGrass prefabs only override materials of Rocks Grass/*: swap each LOD's material for its grass-topped twin.
    static void KomorebiGrassTop(GameObject go)
    {
        foreach (var rend in go.GetComponentsInChildren<Renderer>())
            rend.sharedMaterials = rend.sharedMaterials.Select(m => m == null || !m.name.Contains("_NoGrass") ? m
                : AssetDatabase.LoadAssetAtPath<Material>(KNM + "Rocks Grass/" + m.name.Replace("_NoGrass", "") + ".mat") ?? m).ToArray();
    }

    public const string RockFolder = KitFolder + "/Rocks";
    public static string HighlandGrassRock(int i) => RockFolder + "/HL_Rock_" + i + "_Grass.prefab";

    // HIGHLANDS Rock_1/2(Grass) with the Grasslands lawn texture as coverage: the pack's own coverage grass is teal.
    public static void MakeHighlandGrassRocks()
    {
        const string hl = Env + "Raygeas/HIGHLANDS - Stylized Environment/Assets/";
        var vendor = AssetDatabase.LoadAssetAtPath<Material>(hl + "Materials/Forest/Stones_Rocks(Grass).mat") ?? throw new ArgumentException("Missing Stones_Rocks(Grass).mat");
        var lawn = AssetDatabase.LoadAssetAtPath<Texture2D>(Env + "Raygeas/Grasslands - Stylized Nature/Assets/Textures/Terrain/Grass_AlbedoSmoothness.png");
        var copy = MaterialCopy(vendor, RockFolder + "/HL_Stones_Rocks_Grass.mat", m => { m.SetTexture("_CoverageAlbedo", lawn); m.SetColor("_CoverageColor", new Color(0.95f, 0.95f, 0.95f, 0)); m.enableInstancing = true; });
        for (int i = 1; i <= 2; i++) MaterialVariant(hl + "Prefabs/Forest/Rock_" + i + "(Grass).prefab", HighlandGrassRock(i), new Dictionary<Material, Material> { { vendor, copy } });
    }

    const string GLV = Env + "Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Vegetation/";
    static readonly string[] FeetTrees = { KNR + "Nature/PineTree_1.prefab", KNR + "Nature/PineTree_2.prefab", GLV + "Broadleaf_Small_2.prefab", GLV + "Birch_Small_1.prefab" };
    static readonly string[] FeetBushes = { GLV + "Bush_Large.prefab", GLV + "Bush_Medium.prefab", KNR + "Nature/Bush_1.prefab", KNR + "Nature/Bush_2.prefab", KNR + "Nature/Bush_3.prefab" };

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
        // most grass-topped: first-party variants with a lawn-textured coverage layer (the pack's own grass renders teal)
        new(HighlandGrassRock(1), 2.4f, 2.9f), new(HLR + "Rock_2.prefab", 2.1f, 3.2f), new(HighlandGrassRock(1), 2.4f, 2.9f),
        new(HLR + "Rock_1.prefab", 2.4f, 2.9f), new(HighlandGrassRock(2), 2.1f, 3.2f),
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
        { Side.South, new[] { new RockBand { rocks = HighlandRocks, dMin = 2, dMax = 12, sMin = 1.5f, sMax = 3.0f, topMin = 2, topMax = 6, nMin = 2, nMax = 4, gapMin = 25, gapMax = 70, bare = 0.35f, tilt = 12 } } },
        { Side.West, new[] { new RockBand { rocks = KomorebiCliffs, dMin = 0, dMax = 12, sMin = 0.6f, sMax = 1.0f, topMin = 3, topMax = 7, nMin = 2, nMax = 4, gapMin = 30, gapMax = 60, bare = 0.25f, tilt = 6 } } },
        { Side.North, new[] { new RockBand { rocks = GrasslandsSlabs, dMin = 2, dMax = 12, sMin = 1.2f, sMax = 2.2f, topMin = 2, topMax = 6, nMin = 2, nMax = 4, gapMin = 25, gapMax = 70, bare = 0.35f, tilt = 20 } } },
        { Side.East, new[] { new RockBand { rocks = LakefellRocks, dMin = 0, dMax = 12, sMin = 3, sMax = 7, topMin = 3, topMax = 6, nMin = 2, nMax = 4, gapMin = 30, gapMax = 80, bare = 0.6f, tilt = 10 } } },
    };

    // Ridges and peaks: (prefab, x, z, scale across, scale up, yaw, group). HIGHLANDS tiles are whole ranges with a low side
    // (M1 -Z, M2 +Z, M3 +X, M4 -Z); yaw = direction to the map - low side turns their gentle slopes to the player.
    // BK meshes are tiny (about 0.028 m across at scale 1), hence the large scales; flattened they read as green hills.
    static readonly (string path, float x, float z, float sxz, float sy, float yaw, string group)[] Backdrop =
    {
        (HLM + "Mountain_1.prefab", 240, -400, 0.70f, 1.05f, 180, "Peaks"),
        (HLM + "Mountain_3.prefab", 600, -430, 0.68f, 1.00f, 270, "Peaks"),
        (HLM + "Mountain_2.prefab", 930, -330, 0.62f, 1.00f, 0, "Peaks"),
        (HLM + "Mountain_3.prefab", -260, -170, 0.62f, 1.00f, 315, "Peaks"),
        (HLM + "Mountain_2.prefab", -420, 520, 0.80f, 1.25f, 90, "Peaks"),
        (HLM + "Mountain_1.prefab", -300, 930, 0.70f, 1.10f, 315, "Peaks"),
        (GLM + "Mountain_1.prefab", 180, 1450, 1.3f, 1.7f, 40, "Peaks"),
        (GLM + "Mountain_2.prefab", 520, 1480, 1.4f, 1.9f, 130, "Peaks"),
        (GLM + "Mountain_1.prefab", 850, 1420, 1.2f, 1.5f, 250, "Peaks"),
        (HLM + "Mountain_3.prefab", 1320, 430, 0.60f, 1.00f, 180, "Peaks"),
        (BKF + "Mountain1.prefab", 1600, 150, 45000, 40000, 20, "Far Range"),
        (BKF + "Mountain2.prefab", 1650, 600, 45000, 38000, 110, "Far Range"),
        (BKF + "Mountain1.prefab", 1550, 1000, 42000, 42000, 250, "Far Range"),
    };

    const string AMR = Env + "ANGRY MESH/Stylized Pack - Meadow Environment/Prefabs/Rocks/Rocks - Summer/";
    const string AMM = Env + "ANGRY MESH/Stylized Pack - Meadow Environment/Prefabs/Background/Background - Summer/";

    // Near range anchors: (id, x, z, footprint radius, crest height above the ground, yaw, yaw jitter, prefab). Ids: side
    // (S south, L lake south, E east, N north, W west), row (A near, F fill knob, B back). South rows sit 80 m further north
    // than in the research table, with the meadow's south edge.
    static readonly (string id, float x, float z, float r, float h, float yaw, float jit, string prefab)[] NearRange =
    {
        ("S-A1", 215, 175, 46, 41, 42, 8, AMR + "P_Rock_01_Summer.prefab"),
        ("S-A2", 318, 138, 42, 40, 16, -15, AMR + "P_Rock_03_Summer.prefab"),
        ("S-A3", 412, 130, 48, 42, 352, 22, AMR + "P_Rock_04_Summer.prefab"),
        ("S-A4", 488, 132, 40, 34, 335, -14, AMR + "P_Rock_01_Summer.prefab"),
        ("S-B0", 40, 180, 70, 59, 62, 21, AMM + "P_Mountain_03_Summer.prefab"),
        ("S-B1", 175, 65, 80, 80, 35, -13, KNR + "Rocks Snow/Stone_5_Snow Variant.prefab"),
        ("S-B2", 300, 25, 82, 68, 13, 20, AMM + "P_Mountain_03_Summer.prefab"),
        ("S-B3", 505, 20, 80, 57, 340, -12, AMM + "P_Mountain_03_Summer.prefab"),
        ("L-A5", 565, 62, 46, 41, 28, 19, AMR + "P_Rock_01_Summer.prefab"),
        ("L-A6", 640, 8, 52, 49, 14, -11, AMR + "P_Rock_02_Summer.prefab"),
        ("L-A7", 722, 0, 48, 46, 0, 18, AMR + "P_Rock_03_Summer.prefab"),
        ("L-A8", 818, 4, 52, 49, 343, -10, AMR + "P_Rock_01_Summer.prefab"),
        ("L-A9", 905, 32, 50, 48, 328, 17, AMR + "P_Rock_02_Summer.prefab"),
        ("L-B4", 610, -85, 88, 88, 15, -9, AMM + "P_Mountain_04_Summer.prefab"),
        ("L-B5", 722, -100, 82, 86, 0, 16, AMM + "P_Mountain_03_Summer.prefab"),
        ("L-B6", 935, -72, 95, 105, 332, -8, KNR + "Rocks Snow/Stone_5_Snow Variant.prefab"),
        ("E-A10", 990, 110, 44, 40, 316, 15, GLR + "Clif_1_(no_grass).prefab"),
        ("E-A11", 1018, 205, 46, 44, 298, -22, GLR + "Clif_2_(no_grass).prefab"),
        ("E-A12", 1030, 255, 40, 36, 287, 14, GLR + "Clif_3_(no_grass).prefab"),
        ("E-A13", 1030, 405, 50, 50, 253, -21, GLR + "Clif_1_(no_grass).prefab"),
        ("E-A14", 1022, 490, 46, 44, 237, 13, GLR + "Clif_2_(no_grass).prefab"),
        ("E-B7", 1090, 60, 90, 94, 311, -20, AMM + "P_Mountain_03_Summer.prefab"),
        ("E-B8", 1130, 175, 85, 94, 294, 12, AMM + "P_Mountain_04_Summer.prefab"),
        ("E-B9", 1150, 470, 95, 105, 249, -19, KNR + "Rocks Snow/Stone_5_Snow Variant.prefab"),
        ("E-A15", 1018, 575, 48, 46, 296, 11, GLR + "Clif_3_(no_grass).prefab"),
        ("E-A16", 1012, 662, 46, 44, 279, -18, GLR + "Clif_1_(no_grass).prefab"),
        ("E-A17", 1006, 750, 48, 46, 259, 10, GLR + "Clif_2_(no_grass).prefab"),
        ("E-A18", 992, 838, 40, 36, 239, -17, GLR + "Clif_3_(no_grass).prefab"),
        ("E-A19", 1000, 950, 44, 42, 224, 9, GLR + "Clif_1_(no_grass).prefab"),
        ("E-B10", 1135, 640, 95, 100, 279, -16, AMM + "P_Mountain_03_Summer.prefab"),
        ("E-B11", 1110, 830, 90, 94, 250, 8, AMM + "P_Mountain_04_Summer.prefab"),
        ("N-A20", 852, 1018, 38, 36, 215, -15, GLR + "Clif_1.prefab"),
        ("N-A21", 800, 1000, 44, 42, 208, 22, GLR + "Clif_2.prefab"),
        ("N-A22", 690, 998, 44, 42, 180, -14, GLR + "Clif_3.prefab"),
        ("N-A23", 585, 980, 42, 40, 151, 21, GLR + "Clif_1.prefab"),
        ("N-B12", 885, 1115, 90, 94, 211, -13, AMM + "P_Mountain_01_Summer.prefab"),
        ("N-B13", 690, 1105, 70, 74, 180, 20, AMM + "P_Mountain_01_Summer.prefab"),
        ("N-B14", 538, 1088, 72, 72, 153, -12, AMM + "P_Mountain_01_Summer.prefab"),
        ("N-A24", 470, 928, 42, 36, 220, 19, GLR + "Clif_2.prefab"),
        ("N-A25", 385, 918, 44, 37, 199, -11, KNR + "Rocks Grass/Stone_2.prefab"),
        ("N-A26", 300, 920, 46, 39, 169, 18, KNR + "Rocks Grass/Stone_5.prefab"),
        ("N-A27", 215, 915, 44, 37, 143, -10, KNR + "Rocks Grass/Stone_8.prefab"),
        ("N-A28", 135, 885, 46, 30, 123, 17, KNR + "Rocks Grass/Stone_2.prefab"),
        ("N-B15", 400, 1015, 80, 76, 195, -9, KNR + "Rocks Grass/Stone_5.prefab"),
        ("N-B16", 240, 1005, 85, 85, 160, 16, KNR + "Rocks Snow/Stone_5_Snow Variant.prefab"),
        ("N-B17", 75, 955, 85, 85, 127, -8, KNR + "Rocks Grass/Stone_5.prefab"),
        ("W-A29", 98, 795, 50, 41, 122, 15, KNR + "Rocks/Stone_1_NoGrass.prefab"),
        ("W-A30", 102, 645, 46, 34, 89, -22, KNR + "Rocks Grass/Stone_2.prefab"),
        ("W-A31", 112, 552, 46, 41, 66, 14, KNR + "Rocks/Stone_5_NoGrass.prefab"),
        ("W-B18", 10, 815, 90, 90, 117, -21, KNR + "Rocks Grass/Stone_5.prefab"),
        ("W-B19", 5, 570, 80, 80, 76, 13, KNR + "Rocks Grass/Stone_2.prefab"),
        ("W-A32", 133, 493, 40, 38, 51, -20, KNR + "Rocks/Rock_2_NoGrass.prefab"),
        ("W-A33", 158, 372, 42, 40, 104, 12, KNR + "Rocks/Rock_2_NoGrass.prefab"),
        ("W-A34", 172, 292, 46, 35, 76, -19, KNR + "Rocks Grass/Stone_6.prefab"),
        ("W-A35", 172, 222, 42, 38, 56, 11, KNR + "Rocks/Stone_8_NoGrass.prefab"),
        ("W-B20", 40, 440, 80, 77, 111, -18, KNR + "Rocks Snow/Stone_5_Snow Variant.prefab"),
        ("W-B21", 75, 250, 75, 75, 73, 10, KNR + "Rocks Grass/Stone_5.prefab"),
        ("S-F1", 195, 239, 30, 24, 57, -17, AMR + "P_Rock_04_Summer.prefab"),
        ("S-F2", 271, 168, 30, 24, 30, 9, AMR + "P_Rock_05_Summer.prefab"),
        ("L-F3", 940, 81, 30, 24, 319, -16, AMR + "P_Rock_04_Summer.prefab"),
        ("E-F4", 987, 162, 30, 24, 309, 8, GLR + "Clif_2_(no_grass).prefab"),
        ("N-F5", 746, 979, 30, 24, 197, -15, GLR + "Clif_3.prefab"),
        ("N-F6", 641, 973, 30, 24, 165, 22, GLR + "Clif_1.prefab"),
        ("N-F7", 535, 940, 30, 24, 134, -14, GLR + "Clif_2.prefab"),
        ("W-F8", 118, 722, 30, 24, 109, 21, KNR + "Rocks Grass/Stone_1.prefab"),
    };

    // LOD0 footprint radius (vertices at least 1 m above the lowest), height, lowest point and summit (xz) at scale 1.
    static readonly Dictionary<string, (float R, float Hm, float minY, Vector2 summit)> massifInfo = new();
    static (float R, float Hm, float minY, Vector2 summit) MassifInfo(string path)
    {
        if (massifInfo.TryGetValue(path, out var info)) return info;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new ArgumentException("Missing prefab " + path);
        var pts = new List<Vector3>();
        var lod = prefab.GetComponentInChildren<LODGroup>();
        var lod0 = lod != null && lod.GetLODs().Length > 0 ? lod.GetLODs()[0].renderers : null;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || (lod0 != null && !lod0.Contains(mf.GetComponent<Renderer>()))) continue;
            var m = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            pts.AddRange(mf.sharedMesh.vertices.Select(v => m.MultiplyPoint3x4(v)));
        }
        float minY = pts.Min(p => p.y), maxY = pts.Max(p => p.y);
        var top = pts.First(p => p.y == maxY);
        float R = Mathf.Sqrt(pts.Where(p => p.y >= minY + 1f).Max(p => p.x * p.x + p.z * p.z));
        info = (R, maxY - minY, minY, new Vector2(top.x, top.z));
        massifInfo[path] = info;
        return info;
    }

    // No visible vertex of a near massif may come within `margin` of the playable edge, 37 m of the outlet river or 25 m of
    // the waterfall's pool (vertices under the terrain do not count).
    static bool MassifClear(GameObject go, float margin, Line outlet)
    {
        var terrain = Terrain.activeTerrain;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var verts = mf.sharedMesh.vertices;
            var m = mf.transform.localToWorldMatrix;
            int stride = Mathf.Max(1, verts.Length / 4000);
            for (int k = 0; k < verts.Length; k += stride)
            {
                var w = m.MultiplyPoint3x4(verts[k]);
                var p = new Vector2(w.x, w.z);
                bool onTerrain = w.x >= 0 && w.z >= 0 && w.x <= Size && w.z <= Size;
                if (onTerrain && w.y < terrain.SampleHeight(w) - 0.5f) continue;
                if (onTerrain && SignedDistance(p, Playable) < margin) return false;
                if (LineDistance(p, outlet) < 37f || Vector2.Distance(p, new Vector2(190, 435)) < 25f) return false;
            }
        }
        return true;
    }

    // First-party copies of the ANGRY MESH materials (vendor tint beige / mauve): rocks keep a grass top layer, mountains none.
    static Material AngryMeshMaterial(Material vendor, bool mountain)
    {
        EnsureFolder(BoundaryFolder);
        string dest = BoundaryFolder + "/AM_" + vendor.name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(dest);
        if (m == null) { m = new Material(vendor) { name = Path.GetFileNameWithoutExtension(dest) }; AssetDatabase.CreateAsset(m, dest); }
        else m.CopyPropertiesFromMaterial(vendor);
        if (mountain)
        {
            m.SetFloat("_BaseAlbedoDesaturation", 0.7f); m.SetFloat("_BaseAlbedoBrightness", 1.3f);
            m.SetFloat("_EnableTopLayerBlend", 0f);
            foreach (var kw in m.shaderKeywords.Where(k => k.Contains("TOPLAYER")).ToArray()) m.DisableKeyword(kw);
        }
        else
        {
            m.SetColor("_BaseAlbedoColor", new Color(0.51f, 0.51f, 0.48f, 0)); m.SetFloat("_BaseAlbedoBrightness", 1.1f);
            // grass caps on faces under ~50 degrees (the pack's meadow grass is close to our lawn); the top layer reads the
            // ASPTopLayerGlobals values. The domes are scaled x14-25 and the layer is mesh-UV mapped, hence the raised tiling.
            m.SetFloat("_EnableTopLayerBlend", 1f); m.EnableKeyword("_ENABLETOPLAYERBLEND_ON");
            m.SetFloat("_TopLayerUVScale", 20f); m.SetFloat("_TopLayerOffset", 0.3f);
        }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssetIfDirty(m);
        return m;
    }

    // Gameplay spots the boundary rocks must leave clear: (centre, radius).
    static readonly (Vector2 c, float r)[] KeepOut =
    {
        (HomeBase, 38f),              // rotated 34 x 27 fence (corner 21.7 m) + margin
        (Viewpoint, 14f),
        (new Vector2(247, 420), 50f), // home pond east of the cliff line
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
        string dest = BoundaryFolder + "/" + (vendor.shader.name.Contains("Highlands") ? "HL_" : "GL_") + vendor.name + ".mat";
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
        var topLayerGlobals = Type.GetType("Aipocalypse.Core.EnvironmentSystem.ASPTopLayerGlobals, Aipocalypse.Core") ?? throw new InvalidOperationException("ASPTopLayerGlobals not compiled");
        MakeHighlandGrassRocks();
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
                    if (Vector2.Distance(c, HomeBase) < 100f) KomorebiGrassTop(go);   // the capped rocks behind the home show only their tops
                    rockCount++;
                }
                s += along + gap;
            }
        }

        // outlet gorge: big Grasslands slabs on both banks where the outlet river leaves the playable area and one across its
        // end at the terrain corner, so the river seems to run on between rocks instead of stopping at the terrain edge
        var gorge = Child("Outlet Gorge");
        var outlet = Lines.First(l => l.name == "Outlet").pts;
        var arc = new float[outlet.Length];
        for (int i = 1; i < outlet.Length; i++) arc[i] = arc[i - 1] + Vector2.Distance(new Vector2(outlet[i - 1].x, outlet[i - 1].y), new Vector2(outlet[i].x, outlet[i].y));
        int exit = Array.FindIndex(outlet, k => SignedDistance(new Vector2(k.x, k.y), poly) > 0);
        Vector4 OutletAt(float s) { int i = Mathf.Clamp(Array.FindLastIndex(arc, a => a <= s), 0, outlet.Length - 2); return Vector4.Lerp(outlet[i], outlet[i + 1], (s - arc[i]) / Mathf.Max(1e-3f, arc[i + 1] - arc[i])); }
        Vector2 OutletTangent(float s) { var a = OutletAt(s - 3); var b = OutletAt(s + 3); return new Vector2(b.x - a.x, b.y - a.y).normalized; }
        void Slab(Vector2 c, float scale, float top)
        {
            var rock = GrasslandsSlabs[rng.Next(GrasslandsSlabs.Length)];
            float sy = scale * R(0.9f, 1.1f);
            var go = Put(gorge, rock.path, new Vector3(c.x, GroundMin(c.x, c.y, rock.radius * scale * 0.5f) + top - rock.top * sy, c.y), Quaternion.Euler(R(-8, 8), R(0, 360), R(-8, 8)), new Vector3(scale, sy, scale * R(0.9f, 1.15f)));
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }
        if (exit > 0)
        {
            foreach (float along in new[] { 22f, 48f, 74f })
                foreach (int side in new[] { 1, -1 })
                {
                    float s = arc[exit] + along + R(-5, 5);
                    if (s > arc[^1]) continue;
                    var k = OutletAt(s); var t = OutletTangent(s); var n = new Vector2(-t.y, t.x) * side;
                    float scale = R(1.8f, 2.5f);
                    Slab(new Vector2(k.x, k.y) + n * (k.w + 4f + 9.8f * scale * 0.5f), scale, R(9f, 16f));
                }
            var end = outlet[^1]; var te = OutletTangent(arc[^1] - 4);
            Slab(new Vector2(end.x, end.y) + te * 22f, 3.0f, 22f);
        }

        // near range (after the concept: a valley enclosed by grey rocky mountains right at its edge; anchors from
        // scratchpad research6/brief.md): massifs 50-100 m beyond the edge (row A, crests 30-50 m above the ground), fill
        // knobs between them, and a back row 150-200 m out (crests 57-105 m, a few snow-dusted). Each is scaled to its
        // footprint radius and crest height from the prefab's measured LOD0 mesh, sunk below the lowest ground around it,
        // pushed out (then shrunk) until no visible part comes within 6 m (back row 40 m) of the playable edge, and kept
        // clear of the outlet river and the waterfall pool. Pines grow on the slopes that face the valley.
        var near = Child("Near Range");
        var feet = Child("Rock Feet");                  // not under Near Range: GenerateClearMasks would read these plants as rocks
        var feetRng = new System.Random(seed + 7919);   // own sequence: the near range, peaks and haze keep their layout
        var nearPines = new GameObject("Pines").transform; nearPines.SetParent(near, false);
        var outletLine = Lines.First(l => l.name == "Outlet");
        var nearMats = new Dictionary<Material, Material>();
        var uses = new Dictionary<string, int>();
        string lastPrefab = null;
        int nearCount = 0, nearSkipped = 0, pineCount = 0;
        var mapCentre = new Vector2(505, 520);
        foreach (var a0 in NearRange)
        {
            bool back = a0.id.Contains("-B");
            // the research table's heights kept every far peak clear but read as low knobs behind the tree belt: taller and
            // wider (so neighbours overlap into a range), as the concept's mountains loom over the valley
            var a = (a0.id, a0.x, a0.z, r: a0.r * 1.15f, h: a0.h * (back ? 1.15f : 1.3f), a0.yaw, a0.jit, a0.prefab);
            string path = a.prefab;
            var info = MassifInfo(path);
            uses.TryGetValue(path, out int used); uses[path] = used + 1;
            var anchor = new Vector2(a.x, a.z);
            var outward = (anchor - mapCentre).normalized;
            float shrink = 1f;
            GameObject go = null;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                float r = a.r * shrink;
                float sxz = r / info.R;
                float sx = sxz * R(0.85f, 1.15f) * (used % 2 == 1 ? -1f : 1f), sz = sxz * R(0.85f, 1.15f);   // mirror every second use
                float ground = Ground(anchor.x, anchor.y);
                float low = ground;
                for (int k = 0; k < 16; k++)
                {
                    float ang = k * Mathf.PI / 8;
                    foreach (float f in new[] { 0.35f, 0.7f, 1f }) low = Mathf.Min(low, Ground(anchor.x + Mathf.Cos(ang) * r * f, anchor.y + Mathf.Sin(ang) * r * f));
                }
                float baseY = low - Mathf.Max(4f, 0.08f * a.h);
                float sy = (ground + a.h - baseY) / info.Hm;
                var rot = Quaternion.Euler(0, a.yaw + a.jit, 0);
                var summit = rot * new Vector3(info.summit.x * sx, 0, info.summit.y * sz);
                go = Put(near, path, new Vector3(anchor.x - summit.x, baseY - info.minY * sy, anchor.y - summit.z), rot, new Vector3(sx, sy, sz));
                go.name = a.id + " " + go.name;
                if (MassifClear(go, back ? 40f : 6f, outletLine)) break;
                if (attempt < 10) anchor += outward * 3f;
                else shrink *= 0.95f;
                if (attempt == 13) { UnityEngine.Object.DestroyImmediate(go); go = null; }
            }
            if (go == null) { nearSkipped++; continue; }
            if (path == lastPrefab) go.transform.Rotate(0, 137f, 0);   // never the same face twice in a row
            lastPrefab = path;
            // by material, not prefab path: the mirror (uses) and 137-degree turn (lastPrefab) rules are keyed by path
            if (GrassCapped.Contains(a.id)) KomorebiGrassTop(go);
            // first-party copies for the ANGRY MESH meshes (vendor tint is beige / mauve; grass caps on the rocks)
            foreach (var rend in go.GetComponentsInChildren<Renderer>())
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mats = rend.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].shader.name.StartsWith("ANGRYMESH")) continue;
                    if (!nearMats.TryGetValue(mats[i], out var copy)) nearMats[mats[i]] = copy = AngryMeshMaterial(mats[i], path.Contains("Mountain"));
                    mats[i] = copy;
                }
                rend.sharedMaterials = mats;
            }
            // pines on the slopes facing the valley (row A and fill knobs), in clumps, from raycasts on the massif itself
            if (!back)
            {
                Physics.SyncTransforms();
                var cols = go.GetComponentsInChildren<Collider>();
                var facing = (mapCentre - new Vector2(go.transform.position.x, go.transform.position.z)).normalized;
                float faceAng = Mathf.Atan2(facing.y, facing.x);
                for (int clump = 0; clump < 5; clump++)
                {
                    float ang = faceAng + R(-1.7f, 1.7f), rho = R(0.45f, 0.95f) * a.r * shrink;
                    var cc = new Vector2(a.x, a.z) + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rho;
                    int want = 5 + rng.Next(8);
                    for (int t = 0, made = 0; t < want * 4 && made < want; t++)
                    {
                        var q = cc + new Vector2(R(-7, 7), R(-7, 7));
                        if (SignedDistance(q, poly) < 8f) continue;
                        if (!Physics.Raycast(new Vector3(q.x, 400, q.y), Vector3.down, out var hit, 600f)) continue;
                        if (!cols.Contains(hit.collider) || hit.normal.y < 0.7f || hit.point.y > Ground(q.x, q.y) + 0.6f * a.h) continue;
                        var pine = Put(nearPines, KNR + "Nature/PineTree_" + (3 + rng.Next(4)) + ".prefab", hit.point - Vector3.up * 0.3f, Quaternion.Euler(0, R(0, 360), 0), Vector3.one * R(0.8f, 1.2f));
                        foreach (var rend in pine.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        foreach (var c in pine.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                        made++; pineCount++;
                    }
                }
            }
            // young trees and bushes where a massif facing the meadow meets the ground on its valley side
            if (!back && SignedDistance(anchor, MeadowArea) < 160f)
            {
                var massif = go.GetComponentsInChildren<Collider>();
                var toValley = (mapCentre - anchor).normalized;
                float valleyAng = Mathf.Atan2(toValley.y, toValley.x);
                for (int k = 0; k < 26; k++)
                {
                    float ang = valleyAng + ((float)feetRng.NextDouble() * 2f - 1f) * 1.3f;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    float out0 = 0.25f;
                    for (; out0 < 1.8f; out0 += 0.03f)   // walk out from the summit until the ray misses the massif: its foot on this bearing
                    {
                        var q0 = anchor + dir * (out0 * a.r * shrink);
                        if (!Physics.Raycast(new Vector3(q0.x, 400, q0.y), Vector3.down, out var h0, 600f) || !massif.Contains(h0.collider)) break;
                    }
                    var qf = anchor + dir * (out0 * a.r * shrink + (float)feetRng.NextDouble() * 7f);
                    if (SignedDistance(qf, poly) < 4f) continue;
                    bool tree = feetRng.NextDouble() < 0.4;
                    string plantPath = tree ? FeetTrees[feetRng.Next(FeetTrees.Length)] : FeetBushes[feetRng.Next(FeetBushes.Length)];
                    float ps = tree ? 0.55f + 0.35f * (float)feetRng.NextDouble() : 0.9f + 0.4f * (float)feetRng.NextDouble();
                    var plant = Put(feet, plantPath, new Vector3(qf.x, Ground(qf.x, qf.y) - 0.15f, qf.y), Quaternion.Euler(0, (float)feetRng.NextDouble() * 360f, 0), Vector3.one * ps);
                    foreach (var rend in plant.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    foreach (var col in plant.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(col);
                }
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);   // beyond the walls; AM mountains carry 14k-triangle mesh colliders
            nearCount++;
        }

        // peaks and the east far range: skirts sunk below the lowest ground they cover, pushed out if they reach the play area
        var groups = new Dictionary<string, Transform>();
        var materials = new Dictionary<Material, Material>();
        int mountainCount = 0;
        var centre = new Vector2(505, 520);
        var placedAt = new Dictionary<int, Vector2>();
        for (int bi = 0; bi < Backdrop.Length; bi++)
        {
            var b = Backdrop[bi];
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
            placedAt[bi] = new Vector2(go.transform.position.x, go.transform.position.z);
            mountainCount++;
        }

        // haze banks at the feet of ridges and peaks (Komorebi demo: fog banks out to about 700 m)
        var haze = Child("Haze");
        int hazeCount = 0;
        for (int bi = 0; bi < Backdrop.Length; bi++)
        {
            if (Backdrop[bi].group == "Far Range") continue;
            var c = placedAt[bi];
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
        groundGo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, BackdropFolder);
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

        // the ANGRY MESH rock copies' grass top layer multiplies by the pack's shader globals
        Child("ASP Top Layer Globals").gameObject.AddComponent(topLayerGlobals);
        EditorSceneManager.MarkSceneDirty(scene);
        return new { rocks = rockCount, nearRange = nearCount, nearSkipped, nearPines = pineCount, mountains = mountainCount, haze = hazeCount, walls = poly.Length, perimeter };
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
        // a low percentile, not the minimum: the outlet's channel crosses the border and would drag the whole ring down
        var borderHeights = border.Select(b => ground(b.x, b.y)).OrderBy(h => h).ToArray();
        float minBorder = borderHeights[borderHeights.Length / 10];
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
        var mesh = new Mesh { name = "BackdropGround" };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        // the border winds counter-clockwise seen from above only if the normals point up; flip otherwise
        if (mesh.normals.Average(v => v.y) < 0) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material BackdropGroundMaterial()
    {
        EnsureFolder(BackdropFolder);
        string dest = BackdropFolder + "/BackdropGround.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(dest);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BackdropGround" }; AssetDatabase.CreateAsset(m, dest); }
        var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + "/Terrain_Grass.terrainlayer");
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
        var layers = new[] { "Foot Rocks", "Near Range", "Peaks" };
        var terrain = Terrain.activeTerrain;
        Vector3 Eye(float x, float z) => new Vector3(x, Mathf.Max(terrain.SampleHeight(new Vector3(x, 0, z)), WaterSurfaceAt(new Vector2(x, z)).surface) + 1.7f, z);
        var views = new (string name, Vector3 p)[]
        {
            ("home", new Vector3(HomeSpawn.x, HomeBaseHeight + 1.7f, HomeSpawn.y)), ("meadow", new Vector3(450, 27.1f, 262)), ("viewpoint", Eye(Viewpoint.x, Viewpoint.y)),
            ("bridge", new Vector3(312, 24.8f, 372)), ("waterfall pond", new Vector3(232, 25.0f, 430)), ("west pond", new Vector3(200, 50.5f, 640)),
            ("highland vista", new Vector3(250, 69.8f, 760)), ("north pond", new Vector3(386, 43.3f, 806)), ("swamp", Eye(690, 790)),
            ("lake beach", Eye(760, 90)), ("forest", new Vector3(500, 35.5f, 500)), ("east beach", Eye(953, 400)), ("outlet", Eye(862, 780)),
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
                float front = best["Foot Rocks"][i];
                float ridge = best["Near Range"][i], peak = best["Peaks"][i];
                if (peak > -90 && peak < Mathf.Max(front, ridge) + margin) issues.Add($"{i * 10}deg peak {peak:0.0} vs front {front:0.0} ridge {ridge:0.0}");
            }
            string Max(string l) => best[l].Max().ToString("0.0");
            report.Add($"{name}: max foot {Max("Foot Rocks")} near {Max("Near Range")} peak {Max("Peaks")}; hidden peak bins: {(issues.Count == 0 ? "none" : string.Join("; ", issues.Take(8)))}");
        }
        return string.Join("\n", report);
    }

    // ---------------- Water surfaces (flat bodies) ----------------

    const string WaterFolder = KitFolder + "/Water";
    const string WaterMeshFolder = MapFolder + "/Water";

    // Grid mesh (cell metres) covering a polygon grown by margin, at local Y 0 with the pivot at the polygon centroid.
    // The margin tucks the edge under the banks: outside the shore the terrain rises above the water level. Only cells
    // connected to the interior through wet nodes are kept, so low ground beyond the rim never gets a sheet of water.
    // feather (0..1) goes to the green vertex channel, which the water material reads as transparency (-1 = untouched).
    public static Mesh FlatWaterMesh(string name, Vector2[] outline, float margin, float cell, Func<float, float, bool> wet, Func<float, float, float> feather = null)
    {
        var bb = Bounds(outline.Select(v => new Vector4(v.x, v.y, 0, 0)), margin + cell);
        var centre = Centroid(outline);
        int nx = Mathf.CeilToInt(bb.width / cell) + 1, nz = Mathf.CeilToInt(bb.height / cell) + 1;
        var sd = new float[nx, nz];
        var wetNode = new bool[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                float x = bb.xMin + i * cell, z = bb.yMin + j * cell;
                sd[i, j] = SignedDistance(new Vector2(x, z), outline);
                wetNode[i, j] = sd[i, j] < margin && wet(x, z);
            }
        var reached = new bool[nx, nz];
        var queue = new Queue<(int, int)>();
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                if (wetNode[i, j] && sd[i, j] < -cell) { reached[i, j] = true; queue.Enqueue((i, j)); }
        while (queue.Count > 0)
        {
            var (i, j) = queue.Dequeue();
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = i + di, b = j + dj;
                if (a < 0 || b < 0 || a >= nx || b >= nz || reached[a, b] || !wetNode[a, b]) continue;
                reached[a, b] = true; queue.Enqueue((a, b));
            }
        }
        var verts = new List<Vector3>(); var colors = new List<Color>(); var index = new Dictionary<int, int>(); var tris = new List<int>();
        int V(int i, int j)
        {
            int key = j * nx + i;
            if (!index.TryGetValue(key, out int v))
            {
                v = verts.Count; index[key] = v;
                float x = bb.xMin + i * cell, z = bb.yMin + j * cell;
                verts.Add(new Vector3(x - centre.x, 0, z - centre.y));
                colors.Add(new Color(0, feather == null ? -1f : Mathf.Lerp(-1f, 1f, feather(x, z)), 0, 1));
            }
            return v;
        }
        for (int i = 0; i < nx - 1; i++)
            for (int j = 0; j < nz - 1; j++)
            {
                // keep a quad when any corner is reached: the wet area is covered without holes, its edge tucks under the banks
                if (!(reached[i, j] || reached[i + 1, j] || reached[i, j + 1] || reached[i + 1, j + 1])) continue;
                int a = V(i, j), b = V(i + 1, j), c = V(i + 1, j + 1), d = V(i, j + 1);
                tris.AddRange(new[] { a, d, c, a, c, b });
            }
        var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(Enumerable.Repeat(Vector3.up, verts.Count).ToList());
        mesh.SetUVs(0, verts.Select(v => new Vector2(v.x, v.z)).ToList());
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh FlatWaterMeshPublic(string name, Vector2[] outline, float margin, float cell, Func<float, float, bool> wet)
        => SaveMesh(FlatWaterMesh(name, outline, margin, cell, wet));

    // Saves a mesh asset under the Water data folder (overwrites in place to keep GUIDs stable).
    static Mesh SaveMesh(Mesh mesh, string folder = WaterMeshFolder)
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
        if (mesh.colors.Length == mesh.vertexCount) existing.SetColors(mesh.colors);
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

    // Copies of SW3 materials (vendor .mat files stay untouched), one palette per kind of water: lake and highland ponds
    // (Still), rivers and streams, the home pond (Komorebi's look) and the swamp.
    public static object BuildWaterMaterials()
    {
        EnsureFolder(WaterFolder);
        var still = CopyMaterial(SW3 + "Materials/StylizedWater3_Smooth.mat", WaterFolder + "/Water_Still.mat");
        still.SetColor("_BaseColor", new Color(0.02f, 0.26f, 0.32f, 1.00f));
        still.SetColor("_ShallowColor", new Color(0.30f, 0.66f, 0.58f, 0.30f));
        still.SetColor("_HorizonColor", new Color(0.58f, 0.78f, 0.88f, 0.15f));   // the scene fog colour
        still.SetFloat("_HorizonDistance", 10f);
        still.SetFloat("_DepthVertical", 4f);
        still.SetFloat("_DepthHorizontal", 0.35f);
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
        still.SetFloat("_VertexColorTransparency", 1f);
        still.SetFloat("_ReflectionStrength", 1f);
        still.SetFloat("_ReflectionFresnel", 4f);
        still.SetFloat("_ReflectionDistortion", 0.12f);
        still.SetFloat("_CausticsBrightness", 1f);
        still.SetFloat("_SparkleIntensity", 3f);   // the flat meshes feather out under river ribbons (green channel)

        var river = CopyMaterial(SW3 + "Materials/RiverModeler/StylizedWater3_SplineRiver (Default).mat", WaterFolder + "/Water_River.mat");
        // shaded like the still water (horizon, no waves, refraction, reflections, translucency) with its own clearer
        // palette; where a ribbon fades out over a lake or pond its end fade blends the two colours
        river.SetColor("_BaseColor", new Color(0.06f, 0.36f, 0.38f, 1.00f));
        river.SetColor("_ShallowColor", new Color(0.45f, 0.80f, 0.72f, 0.18f));
        river.SetColor("_HorizonColor", new Color(0.60f, 0.82f, 0.88f, 0.10f));
        foreach (var f in new[] { "_HorizonDistance", "_DepthVertical", "_DepthHorizontal", "_RefractionStrength", "_ReflectionFresnel", "_ReflectionDistortion",
            "_ScreenSpaceReflectionsEnabled", "_SunReflectionSize", "_SunReflectionDistortion", "_ShadowStrength", "_EdgeFade" })
            if (river.HasProperty(f) && still.HasProperty(f)) river.SetFloat(f, still.GetFloat(f));
        WaterToggle(river, "_WavesOn", "_WAVES", false);
        river.SetFloat("_WaveTint", 0f);
        WaterToggle(river, "_TranslucencyOn", "_TRANSLUCENCY", still.GetFloat("_TranslucencyOn") > 0.5f);
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
        river.SetFloat("_DepthVertical", 3f);
        river.SetFloat("_DepthHorizontal", 0.3f);
        river.SetFloat("_ReflectionFresnel", 5f);
        river.SetFloat("_ReflectionDistortion", 0.25f);
        still.SetFloat("_NormalSpeed", 1.0f);
        still.SetFloat("_NormalSubSpeed", -0.5f);

        // the home pond keeps Komorebi's Water_1 look (warm cream shallows, teal deep) with the lake's reflections
        var pond = CopyMaterial(SW3 + "Materials/StylizedWater3_Smooth.mat", WaterFolder + "/Water_Pond.mat");
        pond.CopyPropertiesFromMaterial(still);
        pond.SetColor("_ShallowColor", new Color(0.98f, 0.93f, 0.84f, 0.45f));
        pond.SetFloat("_DepthVertical", 5.7f);
        pond.SetColor("_BaseColor", new Color(0.00f, 0.41f, 0.50f, 1.00f));
        pond.SetColor("_HorizonColor", new Color(0.62f, 0.96f, 0.95f, 0.12f));
        pond.SetFloat("_HorizonDistance", 12f);
        pond.SetFloat("_DepthHorizontal", 0.5f);
        pond.SetFloat("_CausticsBrightness", 2f);

        // swamp: murky tea over the mud, nearly opaque from 1 m, a dark mirror at grazing angles, no caustics
        var swamp = CopyMaterial(SW3 + "Materials/StylizedWater3_Smooth.mat", WaterFolder + "/Water_Swamp.mat");
        swamp.CopyPropertiesFromMaterial(still);
        swamp.SetColor("_BaseColor", new Color(0.13f, 0.17f, 0.07f, 1.00f));
        swamp.SetColor("_ShallowColor", new Color(0.40f, 0.38f, 0.17f, 0.80f));
        swamp.SetColor("_HorizonColor", new Color(0.50f, 0.58f, 0.45f, 0.10f));
        swamp.SetColor("_IntersectionColor", new Color(0.55f, 0.55f, 0.42f, 0.35f));
        swamp.SetFloat("_DepthVertical", 12f);
        swamp.SetFloat("_DepthHorizontal", 2.5f);
        swamp.SetFloat("_EdgeFade", 20f);
        swamp.SetFloat("_ColorAbsorption", 0.1f);
        swamp.SetFloat("_RefractionStrength", 0.05f);
        swamp.SetFloat("_NormalStrength", 0.12f);
        swamp.SetFloat("_NormalSpeed", 0.4f);
        swamp.SetFloat("_ReflectionFresnel", 3f);
        swamp.SetFloat("_ReflectionDistortion", 0.08f);
        swamp.SetFloat("_SparkleIntensity", 0.5f);
        WaterToggle(swamp, "_CausticsOn", "_CAUSTICS", false);

        foreach (var m in new[] { still, river, pond, swamp }) { WaterReflectionsOff(m); EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m); }
        return new[] { still, river, pond, swamp }.Select(m => AssetDatabase.GetAssetPath(m)).ToArray();
    }

    // Performance: no environment reflections (sky, probes, SSR). The sun's specular highlight is lighting and stays.
    public static void WaterReflectionsOff(Material m)
    {
        m.SetFloat("_EnvironmentReflectionsOn", 0f);
        CoreUtils.SetKeyword(m, "_ENVIRONMENTREFLECTIONS_OFF", true);   // [ToggleOff] drawer: off = keyword on
        m.SetFloat("_ScreenSpaceReflectionsEnabled", 0f);
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
        int sibling = old != null ? old.GetSiblingIndex() : -1;
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject(child);
        Undo.RegisterCreatedObjectUndo(go, "Water");
        go.transform.SetParent(water, false);
        if (sibling >= 0) go.transform.SetSiblingIndex(sibling);
        return go.transform;
    }

    // Ponds, lake and swamp: one WaterObject each, mesh kept to cells with at least one corner under water (edges tuck under the banks).
    public static object BuildFlatWater()
    {
        var terrain = Terrain.activeTerrain;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterFolder + "/Water_Still.mat") ?? throw new InvalidOperationException("Run BuildWaterMaterials first.");
        var pondMat = AssetDatabase.LoadAssetAtPath<Material>(WaterFolder + "/Water_Pond.mat");
        var swampMat = AssetDatabase.LoadAssetAtPath<Material>(WaterFolder + "/Water_Swamp.mat");
        var group = WaterGroup("Still Water");
        var bodies = Basins.Select(b => (b.name, b.shore, b.level)).Concat(new[] { (name: "Swamp", shore: SwampArea, level: SwampWater) });
        var report = new List<string>();
        foreach (var b in bodies)
        {
            // wet = low enough to hold this water and, beyond the shore, not inside a lower river/stream channel nor up the
            // channel of a line that runs on into this body (that line's ribbon carries the water; a flat sheet running up the
            // river ended in an arc across it). Inside the shore the basin's surface always holds: the lower-channel test
            // reaches a wide outflow's half width + 3 m and cut the home pond's sheet in a hard grid staircase 8 m inside.
            var feeding = Lines.Where(l => SignedDistance(new Vector2(l.pts[0].x, l.pts[0].y), b.shore) < -5f || SignedDistance(new Vector2(l.pts[^1].x, l.pts[^1].y), b.shore) < -5f).ToArray();
            bool Wet(float x, float z)
            {
                var p = new Vector2(x, z);
                if (terrain.SampleHeight(new Vector3(x, 0, z)) > b.level - 0.02f) return false;
                if (SignedDistance(p, b.shore) <= 0) return true;
                return !LowerChannel(p, b.level) && !feeding.Any(l => LineBounds(l).Contains(p) && TrueLineEdge(p, l) < 0.5f);
            }
            // fade the sheet out under a channel that runs on into it, near the shore crossing: the ribbon is opaque there,
            // and neither surface then ends in a hard edge the other shows through
            float Feather(float x, float z)
            {
                var p = new Vector2(x, z);
                float e = float.MaxValue;
                foreach (var l in feeding) if (LineBounds(l).Contains(p)) e = Mathf.Min(e, TrueLineEdge(p, l));
                return e > 2f ? 0 : (1 - SmoothStep(-1f, 1.5f, e)) * SmoothStep(-9f, -1f, SignedDistance(p, b.shore));
            }
            var mesh = FlatWaterMesh(b.name, b.shore, 8f, 2f, Wet, Feather);
            mesh = SaveMesh(mesh);
            var bodyMat = b.name == "WaterfallPond" && pondMat != null ? pondMat : b.name == "Swamp" && swampMat != null ? swampMat : mat;
            var wo = WaterObject.New(bodyMat, mesh);
            Undo.RegisterCreatedObjectUndo(wo.gameObject, "Water");
            wo.name = b.name;
            wo.transform.SetParent(group, false);
            // ribbons must draw over flat water where they fade out; the river material's queue (3002) does that today, this
            // keeps it true if the queue changes
            foreach (var r in wo.GetComponentsInChildren<Renderer>()) r.sortingOrder = -10;
            var c = Centroid(b.shore);
            wo.transform.position = new Vector3(c.x, b.level, c.y);   // transform Y = water level (SW3 height queries read it)
            report.Add($"{b.name}: level {b.level}, {mesh.vertexCount} verts");
        }
        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        return report;
    }

    // SW3 underwater rendering: one UnderwaterArea per flat water level (the home pond and river share the lake's level but
    // keep their own palette), still water drawn on both faces so its surface shows from below, and an underside sheet for
    // the river: the vendor shader turns underwater shading off in river mode, so the ribbon is invisible from below.
    public static object BuildUnderwater()
    {
        var terrain = Terrain.activeTerrain;
        var still = AssetDatabase.LoadAssetAtPath<Material>(WaterFolder + "/Water_Still.mat");
        var pond = AssetDatabase.LoadAssetAtPath<Material>(WaterFolder + "/Water_Pond.mat");
        var swamp = AssetDatabase.LoadAssetAtPath<Material>(WaterFolder + "/Water_Swamp.mat");
        foreach (var m in new[] { still, pond, swamp })
        {
            m.SetFloat("_Cull", 0);   // Both
            EditorUtility.SetDirty(m);
        }
        var under = MaterialCopy(pond, WaterFolder + "/Water_RiverUnderside.mat", m => m.SetFloat("_Cull", 1));   // back faces only
        AssetDatabase.SaveAssets();

        var group = WaterGroup("Underwater");
        var report = new List<string>();

        // river underside: the channel outside the pond and lake shores, where the ground lies under the water
        var river = Lines.First(l => l.name == "River");
        var pts = river.pts;
        var left = new List<Vector2>(); var right = new List<Vector2>();
        for (int i = 0; i < pts.Length; i++)
        {
            var a = new Vector2(pts[Mathf.Max(0, i - 1)].x, pts[Mathf.Max(0, i - 1)].y);
            var b = new Vector2(pts[Mathf.Min(pts.Length - 1, i + 1)].x, pts[Mathf.Min(pts.Length - 1, i + 1)].y);
            var t = (b - a).normalized; var n = new Vector2(-t.y, t.x);
            var c = new Vector2(pts[i].x, pts[i].y);
            float w = pts[i].w * 1.4f + 6f;   // the carve widens the channel up to ~1.38x
            left.Add(c + n * w); right.Add(c - n * w);
        }
        right.Reverse();
        var outline = left.Concat(right).ToArray();
        var basins = Basins.Where(bs => bs.name == "WaterfallPond" || bs.name == "GreatLake").ToArray();
        bool Wet(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) < LakeLevel - 0.02f
                                      && basins.All(bs => SignedDistance(new Vector2(x, z), bs.shore) > 0f);
        var mesh = SaveMesh(FlatWaterMesh("RiverUnderside", outline, 0f, 2f, Wet));
        var sheet = new GameObject("River Underside", typeof(MeshFilter), typeof(MeshRenderer));
        Undo.RegisterCreatedObjectUndo(sheet, "Water");
        sheet.transform.SetParent(group, false);
        var oc = Centroid(outline);
        sheet.transform.position = new Vector3(oc.x, LakeLevel, oc.y);
        sheet.layer = LayerMask.NameToLayer("Water");
        sheet.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = sheet.GetComponent<MeshRenderer>();
        mr.sharedMaterial = under;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        report.Add($"River Underside: {mesh.vertexCount} verts");

        // areas: shore (or channel) bounds grown by 10 m, from 15 m under the surface to 3 m above it
        void Area(string name, Rect r, float level, Material mat, float fogDensity)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Water");
            go.transform.SetParent(group, false);
            go.layer = LayerMask.NameToLayer("Water");
            go.transform.position = new Vector3(r.center.x, level, r.center.y);
            var box = go.AddComponent<BoxCollider>();
            var area = go.AddComponent<StylizedWater3.UnderwaterRendering.UnderwaterArea>();
            // after AddComponent: the area's Reset() resizes the collider to its 100 x 50 x 100 m default
            box.isTrigger = true;
            box.size = new Vector3(r.width, 18f, r.height);
            box.center = new Vector3(0, -6f, 0);
            area.boxCollider = box;
            area.waterLevelSource = StylizedWater3.UnderwaterRendering.UnderwaterArea.WaterLevelSource.FixedValue;
            area.waterLevel = level;
            area.waterMaterial = mat;
            area.shadingSettings ??= new StylizedWater3.UnderwaterRendering.UnderwaterArea.ShadingSettings();
            area.shadingSettings.fogDensity = fogDensity;
            area.FindShaders();
            report.Add($"{name}: level {level}, box {r.width:0} x {r.height:0} m");
        }
        Rect Of(IEnumerable<Vector2> v, float pad)
        {
            var l = v.ToList();
            return Rect.MinMaxRect(l.Min(p => p.x) - pad, l.Min(p => p.y) - pad, l.Max(p => p.x) + pad, l.Max(p => p.y) + pad);
        }
        var home = Basins.First(bs => bs.name == "WaterfallPond");
        var lake = Basins.First(bs => bs.name == "GreatLake");
        Area("Home Pond + River", Of(home.shore.Concat(outline), 10f), LakeLevel, pond, 0.25f);
        Area("Great Lake", Of(lake.shore, 10f), LakeLevel, still, 0.25f);
        foreach (var bs in Basins.Where(bs => bs.name == "WestPond" || bs.name == "NorthPond")) Area(bs.name, Of(bs.shore, 10f), bs.level, still, 0.25f);
        Area("Swamp", Of(SwampArea, 10f), SwampWater, swamp, 1.2f);
        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        return report;
    }

    // One realtime probe over the Great Lake, rendered once when it first becomes visible: the lake reflects its shores, the
    // near range and the peaks (fogged like the scene) instead of the sky cubemap; SSR takes over wherever the reflected
    // object is on screen. Planar reflections would re-render the whole terrain (trees included) every frame.
    public static object BuildLakeReflection()
    {
        var lake = Basins.First(b => b.name == "GreatLake");
        var min = new Vector2(lake.shore.Min(v => v.x), lake.shore.Min(v => v.y));
        var max = new Vector2(lake.shore.Max(v => v.x), lake.shore.Max(v => v.y));
        var c = (min + max) * 0.5f;
        var group = WaterGroup("Lake Reflection");
        group.position = new Vector3(c.x, lake.level + 3f, c.y);   // capture point 3 m above the water
        var probe = group.gameObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.resolution = 256;
        probe.hdr = true;
        probe.boxProjection = false;                                 // the peaks are far: an infinite projection fits them
        probe.size = new Vector3(max.x - min.x + 20f, 40f, max.y - min.y + 20f);
        probe.center = new Vector3(0f, 7f, 0f);                      // lake floor to the shore canopy
        probe.blendDistance = 10f;
        probe.importance = 1;
        probe.cullingMask = ~LayerMask.GetMask("Water", "UI");
        probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        probe.shadowDistance = 150f;
        probe.nearClipPlane = 0.3f;
        probe.farClipPlane = 1500f;
        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        return $"lake probe at {group.position:F1}, box {probe.size:F0}";
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

    // Rivers and streams as RiverModeler ribbons. Where a line runs on into a lake, pond or swamp, its ribbon stops a few
    // metres past the shore and fades out over that water (rivers 13 m, streams 5 m), so the two surfaces blend.
    // West and Margin streams meet end to end and are one ribbon, so their junction with the swamp outlet stays opaque; the
    // forest stream and the swamp outlet fade in and out over it. (Linking their knots made SW3 blend away a whole segment.)
    public static object BuildRivers()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterFolder + "/Water_River.mat") ?? throw new InvalidOperationException("Run BuildWaterMaterials first.");
        var vfx = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(SW3 + "Runtime/RiverModeler/VFX/River Foam VFX.vfx");
        var group = WaterGroup("Rivers");
        var built = new List<string>();
        Line L(string n) => Lines.First(l => l.name == n);
        // the river starts well inside the home pond and fades in over 8 m, so its opaque part covers the pond sheet's grid edge across the outlet
        built.Add(BuildRiverGroup(group, "River", new[] { Trim(L("River").pts, 13f, 16f) }, mat, vfx, 30f, 2.5f, 2f, 0f, 2f, false, 8f, 13f, 100f));
        built.Add(BuildRiverGroup(group, "Outlet", new[] { Trim(L("Outlet").pts, 16f, 3f) }, mat, vfx, 30f, 2.5f, 2f, 0f, 2f, false, 13f, 4f, 100f));
        var westMargin = Trim(L("WestStream").pts.Concat(L("MarginStream").pts.Skip(1)).ToArray(), 8f, 8f);
        var streams = new[] { westMargin, Trim(L("ForestStream").pts, 8f, 8f), Trim(L("NorthStream").pts, 8f, 8f), Trim(L("SwampOutlet").pts, 8f, 8f) };
        // one segment pair per stream (SW3 splits every spline into max(2, length / maxSegmentLength) segments and scales the
        // tip fade by that count), so the fades are the same 5 m on every stream
        built.Add(BuildRiverGroup(group, "Streams", streams, mat, vfx, 12f, 1.5f, 1f, 0.15f, 1f, true, 5f, 5f, 1000f));
        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        return built;
    }

    // Ribbon knots for a line. Where an end lies inside a lake, pond or swamp, the ribbon stops keepStart/keepEnd metres
    // (along the line) past the shoreline crossing. Distance to the nearest shore is no good here: the flared river mouth
    // runs along the north shore and stayed "near the shore" 36 m out into the lake.
    static Vector4[] Trim(Vector4[] pts, float keepStart, float keepEnd)
    {
        bool Inside(Vector4 k) { var p = new Vector2(k.x, k.y); return Basins.Any(b => SignedDistance(p, b.shore) < 0) || SignedDistance(p, SwampArea) < 0; }
        var arc = new float[pts.Length];
        for (int i = 1; i < pts.Length; i++) arc[i] = arc[i - 1] + Vector2.Distance(new Vector2(pts[i - 1].x, pts[i - 1].y), new Vector2(pts[i].x, pts[i].y));
        int first = 0, last = pts.Length - 1;
        if (Inside(pts[^1]))
        {
            int c = pts.Length - 1;
            while (c > 0 && Inside(pts[c - 1])) c--;                                  // first knot in the water: the crossing
            while (last > c && arc[last - 1] >= arc[c] + keepEnd) last--;
        }
        if (Inside(pts[0]))
        {
            int c = 0;
            while (c < pts.Length - 1 && Inside(pts[c + 1])) c++;
            while (first < c && arc[first + 1] <= arc[c] - keepStart) first++;
        }
        // every carved knot in between: a decimated spline drifts off the channel on curves and exposes a strip of bed
        return pts.Skip(first).Take(last - first + 1).ToArray();
    }

    // startFade/endFade: metres of alpha fade at the first and last knot.
    static string BuildRiverGroup(Transform parent, string name, Vector4[][] lines, Material mat, VisualEffectAsset vfx,
        float baseWidth, float widthEdge, float minEdge, float displacement, float widthFalloff, bool cascadeParticles, float startFade, float endFade,
        float maxSegmentLength)
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
        s.output.maxSegmentLength = maxSegmentLength;
        // SW3 evaluates the tip fade per segment against the whole spline's t, so one unit spans `segments` metres
        // (its own count: max(2, rounded strip length / max segment length), RiverModeler.Rebuild)
        float strip = Mathf.Clamp(minEdge, 0.1f, maxSegmentLength);
        int segments = Mathf.Max(2, Mathf.CeilToInt(Mathf.Round(container.Splines.Max(sp => sp.GetLength()) / strip) * strip / Mathf.Max(maxSegmentLength, strip)));
        s.transparency.startGradientFalloff = startFade / segments;
        s.transparency.endGradientFalloff = endFade / segments;
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

    const string LayerFolder = KitFolder + "/TerrainLayers";
    const string RaygeasEnv = "Assets/_StoreAssets/Env/Raygeas/";

    // Footpaths (x, z), traced from the concept; width is set by the trail mask.
    public static readonly Vector2[][] Trails =
    {
        new[] { HomeGate, Yard(GateLocalX, FenceHalf.y + 4f), V(281,309), V(286.5f,322), V(292,334), V(297,345.3f), V(301.8f,350.7f) },   // home gate -> bridge
        new[] { V(316,371), V(324,392), V(344,428), V(372,462), V(404,489), V(440,506), V(480,532), V(520,560), V(552,592), V(574,620), V(588,644), V(598,668), V(606,692) }, // forest -> swamp fringe
        new[] { V(344,428), V(318,468), V(298,506), V(286,540), V(278,566), V(268,592), V(248,612), V(232,614) },   // highlands spur -> west pond
        new[] { HomeGate, Yard(GateLocalX, FenceHalf.y + 4f), V(292,294.5f), V(312,287.5f), V(332,276), V(352,262), V(392,252), V(440,240), V(480,224), V(512,206) },   // home gate -> lakeside viewpoint
        new[] { V(372,462), V(420,446), V(470,430), V(520,410), V(565,392), V(600,378), V(626,362) },               // forest -> north lake shore
    };

    // Lake beach coves (sand instead of pebbles): (centre, radius). None at the river mouth: a pale sandbar there made the
    // dark river channel look as if it stopped short of the lake.
    static readonly (Vector2 c, float r)[] SandCoves =
    {
        (V(548,172), 24f), (V(700,106), 40f), (V(830,136), 32f), (V(922,330), 30f), (V(612,128), 26f), (PondXf(282,433), PondR(V(282,433), 17f)), (PondXf(262,418), PondR(V(262,418), 9f)), (V(620,170), 14f),
    };

    public static float SandWeight(Vector2 p)
    {
        float sand = 0;
        foreach (var cove in SandCoves)
            sand = Mathf.Max(sand, 1 - SmoothStep(cove.r * 0.55f, cove.r, Vector2.Distance(p, cove.c) + 6f * Fbm(p.x / 14f, p.y / 14f, 2)));
        return sand;
    }

    public static float TrailDistancePublic(Vector2 p) => TrailDistance(p);

    // Half-width of a trail's bare core: 0.9-1.7 m (p5-p95), fraying into the grass in tongues about 3 m long.
    public static float TrailHalfWidth(float x, float z) => Mathf.Max(0.5f, 1.3f + 0.45f * Fbm(x / 10f, z / 10f + 31f, 2) + 0.9f * Fbm(x / 3f, z / 3f + 11f, 2));

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

    // Writes Tex_* masks (res x res, R16, north up like the base map).
    public static async Task<object> GenerateTextureMasks(int res)
    {
        var t0 = DateTime.Now;
        var names = new[] { "Meadow", "Highlands", "Swamp", "Shore", "Sand", "Bed", "Channel", "Bank", "BankPebble", "Trails", "TrailWear", "TrailCentre" };
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
                float shore = 0, bed = 0, sand = 0, inBasin = 1;
                float groundH = HeightAt(x, z, out _);
                foreach (var b in Basins)
                {
                    if (b.bounds.width == 0) b.bounds = Bounds(b.shore.Select(v => new Vector4(v.x, v.y, 0, 0)), 120);
                    if (!b.bounds.Contains(p)) continue;
                    float d = SignedDistance(p, b.shore) + 3.5f * Fbm(x / 22f + b.level, z / 22f, 3);
                    inBasin = Mathf.Min(inBasin, SmoothStep(-6f, 1f, d));
                    if (d > 12f) continue;
                    // by height, so islands and coves follow the real waterline
                    float rel = groundH - b.level;
                    float band = (1 - SmoothStep(0.15f + 0.25f * Fbm(x / 9f, z / 9f, 2), 0.6f, rel)) * SmoothStep(-2.4f, -1.2f, rel);
                    shore = Mathf.Max(shore, band);
                    bed = Mathf.Max(bed, 1 - SmoothStep(-1.9f, -1.1f, rel));
                }
                foreach (var cove in SandCoves)
                    sand = Mathf.Max(sand, 1 - SmoothStep(cove.r * 0.55f, cove.r, Vector2.Distance(p, cove.c) + 6f * Fbm(x / 14f, z / 14f, 2)));
                // pebbles in patches only: grass meets the water on most of the shore, as in the Raygeas demos
                float pebblePatch = SmoothStep(-0.05f, 0.2f, Fbm(x / 16f, z / 16f + 61f, 2));
                maps["Shore"][k] = shore * pebblePatch;
                maps["Bed"][k] = bed;
                maps["Sand"][k] = shore * sand;

                // rivers and streams: pebbly bed, dirt banks
                float edge = float.MaxValue;
                foreach (var l in Lines) edge = Mathf.Min(edge, TrueLineEdge(p, l));
                float wobble = 1.2f * Fbm(x / 7f + 2f, z / 7f, 2);
                // channel textures stop at the shore (inBasin): a dark mud finger through the pale shelf marked where the river ended
                maps["Channel"][k] = (1 - SmoothStep(-1.6f, 0.1f + 0.4f * wobble, edge)) * inBasin;
                // banks: a thin ragged wet band that breaks up, so grass reaches the water on long stretches
                float patch = SmoothStep(-0.15f, 0.3f, Fbm(x / 16f, z / 16f + 40f, 3));
                float bankBand = SmoothStep(-0.6f, 0.2f, edge) * (1 - SmoothStep(0.5f + 0.5f * wobble, 1.4f + 1.0f * Mathf.Abs(wobble) + 1.2f * patch, edge)) * inBasin;
                maps["Bank"][k] = bankBand * (0.35f + 0.65f * patch);
                maps["BankPebble"][k] = bankBand * SmoothStep(0.1f, 0.35f, Fbm(x / 9f, z / 9f + 77f, 2));

                // trails: a frayed bare core, a patchy trampled verge and darker wear down the middle; kept off the water
                float td = TrailDistance(p), hw = TrailHalfWidth(x, z), dry = SmoothStep(0f, 1.5f, edge);
                float core = 1 - SmoothStep(hw - 0.2f, hw + 0.8f, td);
                maps["Trails"][k] = core * dry;
                maps["TrailWear"][k] = (1 - SmoothStep(hw + 0.3f, hw + 3.0f, td)) * (1 - core) * dry;
                maps["TrailCentre"][k] = core * (1 - SmoothStep(0.2f, 0.75f, td / hw)) * SmoothStep(-0.1f, 0.25f, Fbm(x / 5f, z / 5f + 83f, 2)) * dry;
            }
        }));
        foreach (var kv in maps) WriteR16(HeightmapFolder + "/Tex_" + kv.Key + ".png", kv.Value, res, 1f);
        AssetDatabase.Refresh();
        foreach (var n in names) ConfigureImporter(HeightmapFolder + "/Tex_" + n + ".png", res);
        return new { seconds = (DateTime.Now - t0).TotalSeconds, masks = names };
    }

    // Copies of the vendor layers (owned tile size and tint), plus a new rock layer from Komorebi's rock detail texture.
    public static object BuildTerrainLayers()
    {
        EnsureFolder(LayerFolder);
        string gl = RaygeasEnv + "Grasslands - Stylized Nature/Assets/Terrain Layers/";
        var made = new List<string>
        {
            CopyLayer(gl + "Grass.terrainlayer", "Terrain_Grass", 3f, Vector4.one),
            CopyLayer(gl + "Grass_Blue.terrainlayer", "Terrain_GrassCool", 3f, Vector4.one),
            CopyLayer(gl + "Mud.terrainlayer", "Terrain_Dirt", 3f, Vector4.one),
            CopyLayer(gl + "Grass_Yellow.terrainlayer", "Terrain_GrassMeadow", 3f, new Vector4(0.95f, 0.92f, 1.0f, 1f)),    // about #609330, the BK Meadows lawn green
            CopyLayer(gl + "MudStones.terrainlayer", "Terrain_Pebbles", 3f, Vector4.one),
            CopyLayer(gl + "Mud.terrainlayer", "Terrain_WetMud", 3f, new Vector4(0.44f, 0.47f, 0.37f, 1f)),
            CopyLayer(gl + "Sand.terrainlayer", "Terrain_Sand", 4f, Vector4.one),
        };
        // pebbles: Komorebi's grey stone layer (the Grasslands MudStones read brown next to the grey Komorebi rocks)
        var kStones = AssetDatabase.LoadAssetAtPath<TerrainLayer>(RaygeasEnv + "Komorebi Nature/Assets/Terrain Layers/Stones_1.terrainlayer");
        var pebbles = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + "/Terrain_Pebbles.terrainlayer");
        if (kStones != null && pebbles != null)
        {
            pebbles.diffuseTexture = kStones.diffuseTexture; pebbles.normalMapTexture = kStones.normalMapTexture; pebbles.maskMapTexture = kStones.maskMapTexture;
            pebbles.normalScale = kStones.normalScale; pebbles.tileSize = kStones.tileSize; pebbles.diffuseRemapMax = kStones.diffuseRemapMax;
            EditorUtility.SetDirty(pebbles); AssetDatabase.SaveAssetIfDirty(pebbles);
        }
        string rock = LayerFolder + "/Terrain_Rock.terrainlayer";
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
        Texture2D M(string n) => LoadTex(HeightmapFolder + "/Tex_" + n + ".png");

        Stamp(g, "01 Base Grass", L("Terrain_Grass"));
        var cool = Stamp(g, "02 Cool Grass Patches", L("Terrain_GrassCool"));
        NoiseOn(cool.filterSet.weightNoise, 2, 22, 3f, 0.2f);       // weight = saturate(1 + (n - balance) * amplitude), n ~ +-0.7: ~40% cover
        var dirt = Stamp(g, "03 Forest Floor Dirt", L("Terrain_Dirt"));
        dirt.filterSet.weight = 0.6f;
        Range(dirt.filterSet.heightFilter, 0, 62, 0, 4);
        Range(dirt.filterSet.slopeFilter, 0, 45, 0, 6);
        NoiseOn(dirt.filterSet.weightNoise, 2, 45, 3f, 0.42f);
        Masked(Stamp(g, "04 Meadow Base", L("Terrain_Grass")), M("Meadow"));
        var meadow = Masked(Stamp(g, "05 Meadow Warm Grass", L("Terrain_GrassMeadow")), M("Meadow"));
        Range(meadow.filterSet.slopeFilter, 0, 32, 0, 8);
        NoiseOn(meadow.filterSet.weightNoise, 2, 14, 3f, 0.0f);
        var meadowCool = Masked(Stamp(g, "05b Meadow Cool Patches", L("Terrain_GrassCool")), M("Meadow"));   // blue-green patches 10-30 m, as in the Grasslands demo
        Range(meadowCool.filterSet.slopeFilter, 0, 32, 0, 8);
        NoiseOn(meadowCool.filterSet.weightNoise, 2, 16, 8f, 0.14f);
        var scree = Masked(Stamp(g, "06 Highland Scree", L("Terrain_Pebbles")), M("Highlands"));
        Range(scree.filterSet.slopeFilter, 40, 56, 5, 5);
        NoiseOn(scree.filterSet.weightNoise, 2, 35, 2.5f, 0.35f);
        var swampGrass = Masked(Stamp(g, "07a Swamp Grass", L("Terrain_GrassCool")), M("Swamp"));
        NoiseOn(swampGrass.filterSet.weightNoise, 2, 30, 2.5f, -0.2f);
        var swamp = Masked(Stamp(g, "07 Swamp Mud", L("Terrain_WetMud")), M("Swamp"));
        Range(swamp.filterSet.heightFilter, 0, SwampWater + 0.12f, 0, 0.18f);
        Masked(Stamp(g, "08 Water Beds", L("Terrain_WetMud")), M("Bed"));
        var shore = Masked(Stamp(g, "09 Shore Pebbles", L("Terrain_Pebbles")), M("Shore"));
        Range(shore.filterSet.slopeFilter, 0, 57, 0, 5);
        Masked(Stamp(g, "10 Beach Coves", L("Terrain_Sand")), M("Sand"));
        Masked(Stamp(g, "11 Channel Beds", L("Terrain_WetMud")), M("Channel"));   // dark bed: a pale pebble shelf read as a concrete curb through the water
        var bank = Masked(Stamp(g, "12 Channel Banks", L("Terrain_WetMud")), M("Bank"));
        Masked(Stamp(g, "12b Bank Pebbles", L("Terrain_Pebbles")), M("BankPebble"));
        NoiseOn(bank.filterSet.weightNoise, 2, 60, 1.2f, 0f);
        var rock = Stamp(g, "13 Steep Rock", L("Terrain_Rock"));
        Range(rock.filterSet.slopeFilter, 54, 95, 5, 0);
        NoiseOn(rock.filterSet.weightNoise, 2, 60, 0.5f, 0f);
        var shelves = Masked(Stamp(g, "14 Highland Rock Shelves", L("Terrain_Rock")), M("Highlands"));
        Range(shelves.filterSet.slopeFilter, 48, 95, 4, 0);
        NoiseOn(shelves.filterSet.weightNoise, 2, 25, 2f, 0.2f);
        var verge = Masked(Stamp(g, "15a Trail Verge", L("Terrain_Dirt")), M("TrailWear"));
        verge.filterSet.weight = 0.55f;
        NoiseOn(verge.filterSet.weightNoise, 2, 450, 6f, 0.05f);   // ~2 m patches of thin, trampled grass
        Masked(Stamp(g, "15 Trails", L("Terrain_Dirt")), M("Trails"));
        var wear = Masked(Stamp(g, "15b Trail Centre Wear", L("Terrain_WetMud")), M("TrailCentre"));   // last, so it claims its share before Trails
        wear.filterSet.weight = 0.3f;

        foreach (var st in g.GetComponentsInChildren<Stamp>(true)) EditorUtility.SetDirty(st);
        mv.Invalidate(null, MicroVerse.InvalidateType.Splats);
        return g.GetComponentsInChildren<TextureStamp>().Select(t => t.name + ": " + t.layer.name).ToArray();
    }

    static TextureStamp Stamp(Transform parent, string name, TerrainLayer layer)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "World Map texture");
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
            if (e < 6f && e < best) { best = e; surface = s; kind = IsRiver(l) ? 1 : 2; }
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

    // Distance to the real waterline (noisy basin shores, islands and channel widths) for masks and dressing.
    public static float WaterEdgeDistancePublic(Vector2 p)
    {
        float best = BasinEdgeDistance(p);
        foreach (var l in Lines) best = Mathf.Min(best, Mathf.Max(0, TrueLineEdge(p, l)));
        return best;
    }

    // Radius at which an island meets its water (the island profile in HeightAt without its shoreline noise).
    public static float IslandWaterline(int i) => Islands[i].w * (0.35f + Mathf.Pow(Islands[i].z / 9f, 1f / 1.6f));

    // Index of the island whose land holds p, or -1.
    public static int IslandAt(Vector2 p)
    {
        for (int i = 0; i < Islands.Length; i++)
            if (Vector2.Distance(p, new Vector2(Islands[i].x, Islands[i].y)) < IslandWaterline(i)) return i;
        return -1;
    }

    // Distance from p to the nearest lake or pond waterline, 0 in the water. On an island it is the distance to the island's
    // own waterline, so islands get shore grass, flowers and bushes like any bank (they used to count as open water).
    public static float BasinEdgeDistance(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var b in Basins)
        {
            if (b.bounds.width == 0) b.bounds = Bounds(b.shore.Select(v => new Vector4(v.x, v.y, 0, 0)), 120);
            if (!b.bounds.Contains(p)) continue;
            float sd = SignedDistance(p, b.shore) + 3.5f * Fbm(p.x / 22f + b.level, p.y / 22f, 3);
            if (sd < 0)
            {
                int i = IslandAt(p);
                sd = i >= 0 && IslandLevel(i) == b.level ? IslandWaterline(i) - Vector2.Distance(p, new Vector2(Islands[i].x, Islands[i].y)) : 0;
            }
            best = Mathf.Min(best, sd);
        }
        return best;
    }

    public static void WriteMask(string path, float[] data, int res)
    {
        WriteR16(path, data, res, 1f);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(path, res);
    }

    public static void EnsureFolderPublic(string path) => EnsureFolder(path);
    public static Transform WaterGroupPublic(string child) => WaterGroup(child);

    // _Game copy of a vendor material, refreshed from the vendor and re-tuned on every call; vendor files stay untouched.
    public static Material MaterialCopy(Material vendor, string dest, Action<Material> tune)
    {
        EnsureFolder(dest.Substring(0, dest.LastIndexOf('/')));
        var m = AssetDatabase.LoadAssetAtPath<Material>(dest);
        if (m == null) { m = new Material(vendor) { name = Path.GetFileNameWithoutExtension(dest) }; AssetDatabase.CreateAsset(m, dest); }
        else m.CopyPropertiesFromMaterial(vendor);
        tune(m);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssetIfDirty(m);
        return m;
    }

    // Vendor-prefab variant with materials swapped on every renderer (all LODs); an existing variant keeps its GUID.
    public static string MaterialVariant(string src, string dest, Dictionary<Material, Material> swap)
    {
        EnsureFolder(dest.Substring(0, dest.LastIndexOf('/')));
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dest) == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(src ?? "") ?? throw new ArgumentException("Missing " + (src ?? dest));
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.SaveAsPrefabAsset(inst, dest);   // a connected instance saves as a variant of the vendor prefab
            UnityEngine.Object.DestroyImmediate(inst);
        }
        var root = PrefabUtility.LoadPrefabContents(dest);
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && swap.TryGetValue(m, out var c) ? c : m).ToArray();
        PrefabUtility.SaveAsPrefabAsset(root, dest);
        PrefabUtility.UnloadPrefabContents(root);
        return dest;
    }

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
        // playable outline, regions, trails + home base + bridge
        DrawPoly(tex, Playable, o, Color.red);
        DrawPoly(tex, SwampArea, o, new Color(0.55f, 0.32f, 0.12f));
        DrawPoly(tex, HighlandsArea, o, Color.white);
        DrawPoly(tex, MeadowArea, o, Color.yellow);
        foreach (var t in Trails)
            for (int k = 0; k < t.Length - 1; k++) DrawPoly(tex, new[] { t[k], t[k + 1] }, o, new Color(0.9f, 0.5f, 0.2f));
        DrawPoly(tex, new[] { Yard(-FenceHalf.x, -FenceHalf.y), Yard(FenceHalf.x, -FenceHalf.y), Yard(FenceHalf.x, FenceHalf.y), Yard(-FenceHalf.x, FenceHalf.y) }, o, Color.yellow);
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

    static readonly Vector2 HouseAt = WorldMapLayout.Yard(1.5f, -4f);
    const float HouseYaw = WorldMapLayout.HomeYaw;                  // door (local +Z) faces the gate and the bridge
    static readonly Vector2 Campfire = WorldMapLayout.Yard(3.5f, 6.5f);
    const float GateOpen = 105f;                                      // leaf swung out from its closed line (0 = closed, 90 = straight out)

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
        Seat(fire, LV + "Environment/Bench_1.prefab", HouseYaw + 160, 2.5f);
        Seat(fire, LV + "Environment/Bench_1.prefab", HouseYaw + 250, 2.5f);
        Seat(fire, LV + "Environment/Bench_1.prefab", HouseYaw + 355, 2.5f);
        Put(fire, LV + "Environment/Log_2.prefab", Campfire + WorldMapLayout.Rot(new Vector2(3.9f, 0.2f), HouseYaw), HouseYaw + 30, 1f, 0);   // firewood pile
        Put(fire, HL + "Camp/Log_1.prefab", Campfire + WorldMapLayout.Rot(new Vector2(1.98f, 0.28f), HouseYaw), R(0, 360), 1f, 0);
        Put(fire, HL + "Camp/Log_3.prefab", Campfire + WorldMapLayout.Rot(new Vector2(1.98f, 0.71f), HouseYaw), R(0, 360), 1f, 0);

        // door dressing: flower pots either side of the entrance stairs
        var yard = Child(home, "Yard Props");
        Vector2 Local(float x, float z) => HouseAt + WorldMapLayout.Rot(new Vector2(x, z), HouseYaw);
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
        Put(yard, LV + "Environment/Cart_1.prefab", WorldMapLayout.Yard(-11.5f, 8.5f), WorldMapLayout.HomeYaw + 110, 1f, 0);
        Put(yard, LV + "Environment/Chest_1.prefab", Local(4.8f, 3.4f), HouseYaw + 180, 1f, 0);
        Put(yard, LV + "Environment/Trough_1.prefab", WorldMapLayout.Yard(10.5f, -10f), WorldMapLayout.HomeYaw + 30, 1f, 0);
        Put(yard, LV + "Environment/StreetLamp_1.prefab", WorldMapLayout.Yard(WorldMapLayout.GateLocalX - WorldMapLayout.FenceSeg(2 * WorldMapLayout.FenceHalf.x) / 2 - 0.9f, WorldMapLayout.FenceHalf.y - 1.2f), WorldMapLayout.HomeYaw + 180, 1f, 0);

        Bridge(root, log);

        // trail sign at the forest fork
        Put(Child(root, "Trail Signs"), LV + "Environment/SignPost_1.prefab", new Vector2(339, 421), 200, 1f, 0);

        foreach (var go in root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).Where(g => g.GetComponentInParent<StylizedWater3.WaterObject>() == null))
            if (go.GetComponentInChildren<ParticleSystem>(true) == null && go.GetComponent<Light>() == null)
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
        EditorSceneManager.MarkSceneDirty(scene);
        return log;
    }

    // Rebuilds only the bridge (after the river changes width).
    public static object BuildBridge()
    {
        terrain = Terrain.activeTerrain ?? throw new InvalidOperationException("No terrain.");
        var root = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Environment").transform.Find("Landmarks");
        var old = root.Find("River Bridge");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var log = new List<string>();
        Bridge(root, log);
        foreach (var t in root.Find("River Bridge").GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return log;
    }

    // The bridge over the river, square to the flow.
    static void Bridge(Transform root, List<string> log)
    {
        var bridgeRoot = Child(root, "River Bridge");
        var (pos, along, width) = RiverPoint(WorldMapLayout.Bridge);
        var axis = new Vector2(-along.y, along.x);              // across the river
        float yaw = Mathf.Atan2(axis.x, axis.y) * Mathf.Rad2Deg;
        // the 16.1 m Grasslands bridge stretched along its span (local Z) to land 2 m up each bank; deck and rails keep 1.3
        float span = 2f * (width * 1.15f + 0.8f) + 4f;
        float spanScale = Mathf.Max(1.3f, span / 16.1f);
        float half = 16.1f * spanScale * 0.5f - 1.2f;
        float bankA = Ground(pos + axis * half), bankB = Ground(pos - axis * half);
        var bridge = Put(bridgeRoot, GL + "Environment/Bridge.prefab", pos, yaw, 1.3f, 0);
        bridge.transform.localScale = new Vector3(1.3f, 1.3f, spanScale);
        bridge.transform.position = new Vector3(pos.x, Mathf.Max(bankA, bankB) - 0.15f, pos.y);
        log.Add($"bridge at {bridge.transform.position:F1} yaw {yaw:0} span {span:0.0} m banks {bankA:0.0}/{bankB:0.0} river {pos}");
    }

    // ---- waterfall (Komorebi kit on rock meshes; recipe scaled from the Komorebi demo's falls) ----
    const string KNP = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/";
    const string SW3P = "Assets/_StoreAssets/Rendering/Staggart Creations/Stylized Water 3/Prefabs/Particles/";
    const string BKFX = "Assets/_StoreAssets/Env/BK/PureNature_AsianValley/Prefabs/Fx/";

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
        float sy = (top - pond) / (6.47f - 0.27f), sx = 1.6f, sz = 3f;
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

        // foot of the fall: ripples, splash, impact puffs, mist, rings, fog, and Komorebi's blue light sunk in the pool floor
        var rip = P - f * (0.52f * sz);
        Spawn(root, KNP + "Nature/Waterfall_Ripples_1.prefab", new Vector3(rip.x, pond + 0.05f, rip.y), Quaternion.Euler(0, heading + 180f, 0), Vector3.one * 2.3f);
        var spl = P + f * 0.1f + right * 0.25f;
        Spawn(root, KNP + "Particle Systems/Waterfall_Splash_1.prefab", new Vector3(spl.x, pond - 0.35f, spl.y), Quaternion.Euler(0, heading, 0), Vector3.one * 1.35f * sx / 1.15f);
        // SW3 puffs and the BK mist are transparent at queue 3000 with no depth write: sortingOrder 1 draws them after the sheet (3001)
        var puffs = Spawn(root, SW3P + "Waterfall Impact Splashes.prefab", new Vector3(spl.x, pond + 0.15f, spl.y), Quaternion.Euler(0, heading, 0), Vector3.one).GetComponent<ParticleSystem>();
        var puffShape = puffs.shape; puffShape.scale = new Vector3(3.83f * sx * 1.05f, 2.5f, 1f);
        var puffMain = puffs.main; puffMain.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        var puffRate = puffs.emission; puffRate.rateOverTime = 30f;
        var mistAt = P + f * 1.8f;
        var mist = Spawn(root, BKFX + "WaterSplash.prefab", new Vector3(mistAt.x, pond + 0.6f, mistAt.y), Quaternion.Euler(-90f, heading, 0), Vector3.one * 0.6f).GetComponent<ParticleSystem>();
        var mistMain = mist.main;
        mistMain.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);
        mistMain.startColor = new Color(1f, 1f, 1f, 0.35f);
        var mistRate = mist.emission; mistRate.rateOverTime = 15f;
        foreach (var ps in new[] { puffs, mist })
        {
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.sortingOrder = 1;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pr);
        }
        foreach (float side in new[] { -0.3f, 0.3f })
        {
            var ringAt = P + f * 1.2f + right * (side * 3.83f * sx);
            Spawn(root, SW3P + "WaterRipples_Stationairy.prefab", new Vector3(ringAt.x, pond + 0.03f, ringAt.y), Quaternion.identity, Vector3.one * 4f);
        }
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
        var stillMat = AssetDatabase.LoadAssetAtPath<Material>(WorldMapLayout.KitFolder + "/Water/Water_Still.mat");
        if (stillMat != null)
        {
            var c = src.Aggregate(Vector2.zero, (acc, v) => acc + v) / src.Length;
            var mesh = WorldMapLayout.FlatWaterMeshPublic("WaterfallSource", src, 0.5f, 1f, (x, z) => true);
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
        string dest = WorldMapLayout.KitFolder + "/Water/Water_Waterfall.mat";
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
        var half = WorldMapLayout.FenceHalf;
        var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) }.Select(c => WorldMapLayout.Yard(c.x * half.x, c.y * half.y)).ToArray();
        int count = 0, gate = WorldMapLayout.GateSlot;
        for (int side = 0; side < 4; side++)
        {
            var a = corners[side]; var b = corners[(side + 1) % 4];
            float len = Vector2.Distance(a, b);
            var dir = (b - a) / len;
            int n = Mathf.RoundToInt(len / WorldMapLayout.FencePanel);
            float seg = len / n;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - 90;   // panel length runs along local X
            bool front = side == 2;                                        // local +Z side, from the +X corner to the -X corner
            for (int i = 0; i < n; i++)
            {
                if (front && i == gate) continue;
                var p = Put(parent, panels[rng.Next(panels.Length)], a + dir * (seg * (i + 0.5f)), yaw + (rng.Next(2) == 0 ? 0 : 180), 1f, -0.05f);
                p.transform.localScale = new Vector3(seg / WorldMapLayout.FencePanel, 1f, 1f);
                count++;
            }
            for (int i = 0; i < n; i++)                                      // the next side's first post closes each corner
            {
                bool jamb = front && (i == gate || i == gate + 1);
                Put(parent, LV + "Environment/FenceEnd_1.prefab", a + dir * (seg * i), jamb ? yaw : R(0, 360), jamb ? 1.35f : 1.15f, -0.05f);
            }
            if (front) BuildGate(parent, a, dir, seg);
        }
        log.Add($"fence: {count} panels, {half.x * 2} x {half.y * 2} m yard turned {WorldMapLayout.HomeYaw}, gate in slot {gate} (local x {WorldMapLayout.GateLocalX:0.00})");
    }

    // Open board gate: a Lakefell railing leaf on a hinge just outside the +X jamb, swung out GateOpen degrees.
    static void BuildGate(Transform parent, Vector2 a, Vector2 dir, float seg)
    {
        var gate = Child(parent, "Gate");
        var hingeAt = a + dir * (seg * WorldMapLayout.GateSlot + 0.03f) + WorldMapLayout.Rot(Vector2.up, WorldMapLayout.HomeYaw) * 0.2f;
        float sx = (seg - 0.2f) / 1.606f, yaw = WorldMapLayout.HomeYaw + 180 + GateOpen;   // 1.606 m = Extension_1_Fence mesh length
        var tip = hingeAt + WorldMapLayout.Rot(Vector2.right, yaw) * (0.02f + 1.606f * sx);
        var hinge = Child(gate, "Hinge");
        hinge.SetPositionAndRotation(new Vector3(hingeAt.x, Mathf.Max(Ground(hingeAt), Ground(tip)) + 0.08f, hingeAt.y), Quaternion.Euler(0, yaw, 0));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LV + "Modules/Extension_1_Fence.prefab") ?? throw new ArgumentException("Missing gate leaf");
        var leaf = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hinge);
        leaf.transform.SetLocalPositionAndRotation(new Vector3(0.02f + 0.803f * sx, 0, 0), Quaternion.identity);
        leaf.transform.localScale = new Vector3(sx, 1.25f, 1.3f);
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

    // Closest point on the river polyline (the same smoothed knots the terrain was carved with), the flow direction and half width there.
    static (Vector2 pos, Vector2 along, float halfWidth) RiverPoint(Vector2 near)
    {
        var river = WorldMapLayout.Lines.First(l => l.name == "River").pts;
        float best = float.MaxValue, bw = 0; Vector2 bp = near, bd = Vector2.right;
        for (int i = 0; i < river.Length - 1; i++)
        {
            var a = new Vector2(river[i].x, river[i].y); var b = new Vector2(river[i + 1].x, river[i + 1].y);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(near - a, ab) / ab.sqrMagnitude);
            var q = a + ab * t;
            float d = Vector2.Distance(near, q);
            if (d < best) { best = d; bp = q; bd = ab.normalized; bw = Mathf.Lerp(river[i].w, river[i + 1].w, t); }
        }
        return (bp, bd, bw);
    }
}

// ---------------- Water-edge dressing (third class; same reason as Landmarks) ----------------
// Lake, pond, swamp, bank, home pond and meadow dressing, deterministic from a seed. Rebuilding replaces Environment/Dressing/<group>.
public static class Dressing
{
    const string KN = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/";
    static Terrain terrain;
    static System.Random rng;
    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static float H(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0, p.y));

    // Lake, pond and swamp shores (vendor-demo measurements in scratchpad research5/lake_shore_islands.md). The lake and the
    // highland ponds are walked along their real waterline and each stretch is classed as cove, straight, point or beach:
    // stones come in groups of 2-5 with about half of each stretch bare (Grasslands, ref 7), coves get more stones, reeds
    // and lilies, points get a few big rocks standing in the water, beaches only pebbles and driftwood. Lilies (and two
    // Grasslands lotus clusters) grow in clusters, mostly in coves; a few hero trees lean over the water from coves and
    // points. The swamp keeps loose stone clusters and scattered lilies. The home pond has its own builder (BuildHomePond).
    public static object BuildWaterEdges(int seed)
    {
        terrain = Terrain.activeTerrain;
        rng = new System.Random(seed);
        const string GLR = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Rocks/";
        const string GV = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Vegetation/";
        const string HLF = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
        var root = Group("Water Edges");
        Transform Sub(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }
        var stones = Sub("Shore Stones"); var lilies = Sub("Lilies"); var pebbles = Sub("Pebbles"); var trees = Sub("Shore Trees"); var wood = Sub("Driftwood");
        var placed = new List<Vector3>();   // x, z, radius
        bool Free(Vector2 p, float r) => placed.All(q => (new Vector2(q.x, q.y) - p).sqrMagnitude > (q.z + r) * (q.z + r));
        Quaternion Tilted(float lo, float hi) => Quaternion.Euler(R(-1, 1) * R(lo, hi), R(0, 360), R(-1, 1) * R(lo, hi));
        Quaternion Upright() => Quaternion.Euler(0, R(0, 360), 0);
        int nStones = 0, nLilies = 0, nPebbles = 0, nTrees = 0, nWood = 0;
        var fallAt = new Vector2(206, 433);

        // ---- walked shores: (point on the waterline, direction into the water, class, arc position) ----
        float LandFraction(Vector2 p, float level)
        {
            int land = 0;
            for (int k = 0; k < 12; k++) { float a = k * Mathf.PI / 6; if (H(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 15f) > level) land++; }
            return land / 12f;
        }
        var walked = new List<(Vector2 p, Vector2 n, string body, float level, char cls, float arc)>();
        foreach (var b in WorldMapLayout.Basins.Where(b => b.name != "WaterfallPond"))
        {
            var poly = b.shore;
            var cum = new float[poly.Length + 1];
            for (int i = 0; i < poly.Length; i++) cum[i + 1] = cum[i] + Vector2.Distance(poly[i], poly[(i + 1) % poly.Length]);
            for (float s = 0; s < cum[^1]; s += 1.5f)
            {
                int i = Mathf.Clamp(Array.FindLastIndex(cum, c => c <= s), 0, poly.Length - 1);
                var a = poly[i]; var bb = poly[(i + 1) % poly.Length];
                var q = Vector2.Lerp(a, bb, (s - cum[i]) / Mathf.Max(1e-3f, cum[i + 1] - cum[i]));
                var tan = (bb - a).normalized; var outward = new Vector2(tan.y, -tan.x);
                if (WorldMapLayout.SignedDistance(q + outward * 2f, poly) < 0) outward = -outward;
                for (float d = -9f; d <= 9f; d += 0.25f)
                {
                    var w = q + outward * d;
                    if (H(w) <= b.level) continue;
                    if (d > -9f && WorldMapLayout.WaterSurfaceAt(w).kind == 0)          // not a river or stream mouth
                    {
                        float lf = LandFraction(w, b.level);
                        char cls = WorldMapLayout.SandWeight(w) > 0.4f ? 'b' : lf > 0.6f ? 'c' : lf < 0.4f ? 'p' : 's';
                        walked.Add((w, -outward, b.name, b.level, cls, s));
                    }
                    break;
                }
            }
        }

        // ---- stone groups (lake: Grasslands round stones as anchors; highland ponds: HIGHLANDS stones) ----
        var centres = new List<Vector2>();
        foreach (var (p, n, body, level, cls, arc) in walked.OrderBy(_ => rng.Next()))
        {
            bool highland = body != "GreatLake";
            float spacing = highland ? R(8, 12) : R(12, 16);
            if (centres.Any(c => (c - p).sqrMagnitude < spacing * spacing)) continue;
            centres.Add(p);
            double skip = cls == 'c' ? 0.2 : cls == 'b' ? 1.0 : cls == 'p' ? 0.6 : highland ? 0.3 : 0.45;
            if (rng.NextDouble() < skip) continue;
            if (cls == 'p' && !highland)
            {
                // points: 1-3 big rocks standing in 0.3-1.5 m of water
                for (int k = 0, t = 0; k < 1 + rng.Next(3) && t < 30; t++)
                {
                    var q = p + n * R(1f, 6f) + new Vector2(-n.y, n.x) * R(-4, 4);
                    float depth = level - H(q);
                    if (depth < 0.3f || depth > 1.5f || !Free(q, 1.8f)) continue;
                    bool kn = rng.Next(2) == 0;
                    float sc = kn ? R(0.9f, 1.4f) : R(1.3f, 1.8f);
                    string path = kn ? KN + "Rocks Grass/Stone_" + new[] { 2, 5, 6, 8 }[rng.Next(4)] + ".prefab" : GLR + (rng.Next(2) == 0 ? "Stone_9.prefab" : "Stone_7.prefab");
                    Put(stones, path, new Vector3(q.x, H(q) - R(0.3f, 0.6f) * sc, q.y), Tilted(4, 14), sc);
                    placed.Add(new Vector3(q.x, q.y, 1.6f * sc)); k++; nStones++;
                }
                continue;
            }
            int count = cls == 'c' ? 3 + rng.Next(4) : 2 + rng.Next(4);
            for (int k = 0, t = 0; k < count && t < 40; t++)
            {
                double u = rng.NextDouble();
                float off = u < 0.55 ? R(-0.5f, 0.6f) : u < 0.85 ? R(1f, 4f) : R(-3f, -0.5f);   // + = into the water
                var q = p + n * off + new Vector2(-n.y, n.x) * R(-2.5f, 2.5f);
                string path; float sc;
                if (highland) { path = HLF + "Stone_" + (2 + rng.Next(3)) + ".prefab"; sc = R(1.0f, 1.6f); }
                else if (k == 0 && rng.NextDouble() < 0.6) { path = GLR + "Stone_" + (7 + rng.Next(3)) + ".prefab"; sc = R(0.7f, 1.3f); }
                else { path = KN + "Rocks/SmallStone_" + (1 + rng.Next(3)) + ".prefab"; sc = R(1.5f, 2.8f); }
                float r = 0.4f * sc;
                if (!Free(q, r * 0.8f)) continue;
                Put(stones, path, new Vector3(q.x, H(q) - R(0.15f, 0.35f) * (path.Contains("Small") ? 1f : sc), q.y), Tilted(9, 20), sc);
                placed.Add(new Vector3(q.x, q.y, r)); k++; nStones++;
            }
            // a pebble group in coves and on beaches
            if (cls == 'c' || rng.NextDouble() < 0.3)
                for (int j = 0; j < 5 + rng.Next(7); j++)
                {
                    var q = p + new Vector2(R(-2.5f, 2.5f), R(-2.5f, 2.5f));
                    float qs = R(1.2f, 1.6f);
                    if (!Free(q, 0.2f * qs)) continue;
                    Put(pebbles, highland ? KN + "Rocks/SmallStone_" + (5 + rng.Next(2)) + ".prefab" : GLR + "Small_Stone_" + (6 + rng.Next(3)) + ".prefab", new Vector3(q.x, H(q) - 0.04f, q.y), Tilted(0, 6), qs);
                    nPebbles++;
                }
        }

        // ---- hero shore trees: one per ~200 m of lake shore, 2-5 m from the water, on coves and points ----
        var lakeShore = walked.Where(w => w.body == "GreatLake" && w.cls != 'b').OrderBy(w => w.cls == 's' ? 1 : 0).ThenBy(_ => rng.Next()).ToList();   // coves and points first
        var heroes = new List<Vector2>();
        foreach (var (p, n, body, level, cls, arc) in lakeShore)
        {
            if (heroes.Count >= 8) break;
            if (heroes.Any(h => (h - p).sqrMagnitude < 90f * 90f)) continue;
            var q = p - n * R(2f, 5f);
            if (H(q) < level + 0.3f || WorldMapLayout.TrailDistancePublic(q) < 3f || !Free(q, 3f)) continue;
            int kind = rng.Next(10);
            string path = kind < 4 ? KN + "Nature/PineTree_4.prefab" : kind < 8 ? GV + "Broadleaf_Medium_2.prefab" : KN + "Nature/Sakura_" + (1 + rng.Next(2)) + ".prefab";
            // lean the crown out over the water
            var lean = Quaternion.AngleAxis(R(4f, 9f), new Vector3(n.y, 0, -n.x)) * Upright();
            Put(trees, path, new Vector3(q.x, H(q) - 0.2f, q.y), lean, R(1.0f, 1.25f));
            heroes.Add(p); placed.Add(new Vector3(q.x, q.y, 3f)); nTrees++;
        }

        // ---- driftwood: one spot per ~250 m of lake shore, a log and branches, half in the water ----
        var spots = new List<Vector2>();
        foreach (var (p, n, body, level, cls, arc) in walked.Where(w => w.body == "GreatLake" && w.cls != 'c').OrderBy(_ => rng.Next()))
        {
            if (spots.Count >= 6) break;
            if (spots.Any(s => (s - p).sqrMagnitude < 200f * 200f) || !Free(p, 3f)) continue;
            spots.Add(p);
            var q = p + n * R(-1f, 1f);
            Put(wood, HLF + "Log_" + (1 + rng.Next(2)) + ".prefab", new Vector3(q.x, H(q) - 0.1f, q.y), Quaternion.Euler(R(-3, 3), R(0, 360), R(-3, 3)), R(0.9f, 1.2f));
            for (int k = 0; k < 3 + rng.Next(3); k++)
            {
                var b = p + n * R(-2f, 6f) + new Vector2(-n.y, n.x) * R(-4f, 4f);
                Put(wood, GV + new[] { "Branch_1.prefab", "Big_Branch_1.prefab", "Big_Branch_2.prefab", "Branch_2.prefab" }[rng.Next(4)], new Vector3(b.x, Mathf.Max(H(b) - 0.05f, level - 0.15f), b.y), Quaternion.Euler(R(-4, 4), R(0, 360), R(-4, 4)), R(0.8f, 1.1f));
            }
            placed.Add(new Vector3(p.x, p.y, 4f)); nWood++;
        }

        // ---- lilies in clusters: 2-7 m out in 0.9-2.2 m of water, 60% in coves; two lake clusters are Grasslands lotus ----
        void Clusters(string body, int wanted, int lotusClusters, int minGroups, int maxGroups)
        {
            var pool = walked.Where(w => w.body == body).ToList();
            if (pool.Count == 0) return;
            var made = new List<Vector2>();
            for (int tries = 0; tries < 4000 && made.Count < wanted; tries++)
            {
                var (p, n, _, level, cls, _) = pool[rng.Next(pool.Count)];
                if (cls != 'c' && rng.NextDouble() < 0.6) continue;
                var c = p + n * R(2f, 7f);
                float depth = level - H(c);
                if (depth < 0.9f || depth > 2.2f || WorldMapLayout.IslandAt(c) >= 0 || made.Any(m => (m - c).sqrMagnitude < 25f * 25f) || !Free(c, 1f)) continue;
                bool lotus = made.Count < lotusClusters;
                made.Add(c);
                int count = minGroups + rng.Next(maxGroups - minGroups + 1);
                float radius = R(3f, 6f);
                for (int k = 0, t = 0; k < count && t < 120; t++)
                {
                    var q = c + new Vector2(R(-radius, radius), R(-radius, radius));
                    float dq = level - H(q);
                    if (dq < 0.8f || dq > 2.4f || !Free(q, 1.25f)) continue;
                    string path = lotus ? GV + "Lotus_Group_" + (1 + rng.Next(2)) + ".prefab" : KN + "Nature/WaterLily_Group_" + (rng.NextDouble() < 0.55 ? 2 : 3) + ".prefab";
                    Put(lilies, path, new Vector3(q.x, level + 0.028f, q.y), Upright(), R(1.0f, 1.2f));
                    placed.Add(new Vector3(q.x, q.y, 1.3f)); k++; nLilies++;
                }
            }
        }
        Clusters("GreatLake", 13, 2, 3, 8);
        Clusters("WestPond", 3, 0, 3, 6);
        Clusters("NorthPond", 4, 0, 3, 6);

        // ---- swamp: loose stone clusters on its sampled shore, lilies in calm open water; and a few at the river mouth ----
        var swampShore = new List<Vector2>();
        for (int i = 0; i < 120000 && swampShore.Count < 2500; i++)
        {
            var p = new Vector2(R(530, 840), R(650, 920));
            if (WorldMapLayout.SignedDistance(p, WorldMapLayout.SwampArea) > 8f) continue;
            float h = H(p);
            if (h < WorldMapLayout.SwampWater - 0.45f || h > WorldMapLayout.SwampWater + 0.35f) continue;
            swampShore.Add(p);
        }
        var swampCentres = new List<Vector2>();
        foreach (var c in swampShore.OrderBy(_ => rng.Next()))
        {
            if (swampCentres.Any(q => (q - c).sqrMagnitude < 26f * 26f)) continue;
            swampCentres.Add(c);
            if (rng.NextDouble() < 0.4) continue;
            foreach (var m in swampShore.Where(q => (q - c).sqrMagnitude < 6f * 6f).OrderBy(_ => rng.Next()).Take(3 + rng.Next(6)))
            {
                float sc = R(1.3f, 2.6f);
                if (!Free(m, 0.35f * sc)) continue;
                Put(stones, KN + "Rocks/SmallStone_" + (1 + rng.Next(4)) + ".prefab", new Vector3(m.x, H(m) - R(0.05f, 0.3f), m.y), Tilted(0, 18), sc);
                placed.Add(new Vector3(m.x, m.y, 0.35f * sc)); nStones++;
            }
        }
        foreach (var (name, centre, radius, count, kindOk) in new[] { ("swamp", new Vector2(688, 790), 150f, 40, 0), ("mouth", new Vector2(520, 290), 30f, 8, 1) })
        {
            for (int i = 0, made = 0; i < 4000 && made < count; i++)
            {
                var p = centre + new Vector2(R(-radius, radius), R(-radius, radius));
                var (s, kind) = WorldMapLayout.WaterSurfaceAt(p);
                if (float.IsNegativeInfinity(s) || (kind != 0 && kind != kindOk)) continue;
                if (name == "swamp" && WorldMapLayout.SignedDistance(p, WorldMapLayout.SwampArea) > 0) continue;
                float depth = s - H(p);
                if (depth < 0.9f || depth > 2.2f || !Free(p, 1.6f)) continue;
                if (name == "mouth" && WorldMapLayout.WaterEdgeDistancePublic(p) > 0) continue;
                Put(lilies, KN + "Nature/WaterLily_Group_" + (1 + rng.Next(4)) + ".prefab", new Vector3(p.x, s + 0.015f, p.y), Upright(), R(0.8f, 1.0f));
                placed.Add(new Vector3(p.x, p.y, 1.4f));
                made++; nLilies++;
            }
        }

        // lake reed beds: runs of BK cattails standing in the shallows, mostly in coves, some along straight shore, none on
        // points, beaches or by the viewpoint; half the beds get a lily cluster just outside them. GameObjects, not the
        // detail stamp, so they carry past the detail distance.
        var reedRoot = Sub("Reed Beds");
        string[] reedKinds = { "Assets/_StoreAssets/Env/BK/PureNature_Meadows/Prefabs/Plants/Reeds.prefab", "Assets/_StoreAssets/Env/BK/PureNature_Mountains/Prefabs/Plants/Reeds.prefab" };
        var beds = new List<(Vector2 p, Vector2 n, float level)>();
        bool inBed = false; float bedLeft = R(3, 12);
        int nReeds = 0;
        foreach (var (p, n, body, level, cls, arc) in walked.Where(w => w.body == "GreatLake"))
        {
            bedLeft -= 1.5f;
            if (bedLeft <= 0)
            {
                if (inBed) { inBed = false; bedLeft = R(10, 35); }
                else if (rng.NextDouble() < (cls == 'c' ? 0.85 : cls == 's' ? 0.4 : 0.0)) { inBed = true; bedLeft = R(6, 18); beds.Add((p, n, level)); }
                else bedLeft = R(3, 8);
            }
            if (!inBed || cls == 'b' || cls == 'p') continue;
            if (Vector2.Distance(p, WorldMapLayout.Viewpoint) < 18f || WorldMapLayout.TrailDistancePublic(p) < 2.5f) continue;
            for (int k = 0; k < (cls == 'c' ? 2 : 1); k++)
            {
                var q = p + n * R(-0.3f, 2.5f) + new Vector2(-n.y, n.x) * R(-0.75f, 0.75f);
                float depth = level - H(q);
                if (depth < -0.15f || depth > 0.7f || !Free(q, 0.45f)) continue;
                Put(reedRoot, reedKinds[rng.NextDouble() < 0.7 ? 0 : 1], new Vector3(q.x, H(q) - 0.05f, q.y), Quaternion.Euler(R(-4, 4), R(0, 360), R(-4, 4)), R(1.1f, 1.6f));
                placed.Add(new Vector3(q.x, q.y, 0.45f)); nReeds++;
            }
        }
        foreach (var (p, n, level) in beds)
        {
            if (rng.NextDouble() < 0.5) continue;
            var c = p + n * R(3.5f, 6f);
            if (WorldMapLayout.IslandAt(c) >= 0) continue;
            for (int k = 0, t = 0, want = 3 + rng.Next(4); k < want && t < 40; t++)
            {
                var q = c + new Vector2(R(-3f, 3f), R(-3f, 3f));
                float d = level - H(q);
                if (d < 0.6f || d > 1.8f || !Free(q, 1.25f)) continue;
                Put(lilies, KN + "Nature/WaterLily_Group_" + (rng.NextDouble() < 0.55 ? 2 : 3) + ".prefab", new Vector3(q.x, level + 0.028f, q.y), Upright(), R(0.9f, 1.15f));
                placed.Add(new Vector3(q.x, q.y, 1.3f)); k++; nLilies++;
            }
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        var classes = walked.GroupBy(w => w.cls).ToDictionary(g => g.Key.ToString(), g => g.Count());
        return new { shoreSamples = walked.Count, classes, stones = nStones, pebbles = nPebbles, lilies = nLilies, heroTrees = nTrees, driftwood = nWood, reeds = nReeds, reedBeds = beds.Count };
    }

    // River and stream banks, walked along each water line metre by metre. Each bank sample gets a bend character cc
    // (+1 cut bank: outer bend, rock-lined; -1 bar: inner bend, low, few stones) and a stream character (rocky highland,
    // boulder-lined, calm lotus; see the walk). Plus a rock wall on the cut bank by the bridge and three fallen-log
    // footbridges where trails cross streams.
    public static object BuildBanks(int seed)
    {
        terrain = Terrain.activeTerrain;
        rng = new System.Random(seed);
        const string HL = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
        const string GV = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Vegetation/";
        var root = Group("Bank Rocks");
        WorldMapLayout.MakeHighlandGrassRocks();
        Transform Sub(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }
        var lining = Sub("Lining"); var boulders = Sub("Boulders"); var inChannel = Sub("In Channel"); var wood = Sub("Driftwood"); var hero = Sub("Hero"); var lotus = Sub("Lotus");
        const string GLR = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Rocks/";
        const string BKAV = "Assets/_StoreAssets/Env/BK/PureNature_AsianValley/Prefabs/Plants/";   // lotus and astilbe (BK/Grass, needs BKFoliageGlobals)
        // Grasslands round boulders: (path, mesh bottom, mesh top) at scale 1 - their pivots are not at the bottom
        var glBoulders = new[] { (GLR + "Stone_7.prefab", -0.58f, 1.58f), (GLR + "Stone_9.prefab", -0.94f, 1.70f), (GLR + "Stone_2.prefab", -0.13f, 0.99f), (GLR + "Stone_8.prefab", -0.67f, 1.40f) };
        var td = terrain.terrainData;
        Vector3 Normal(Vector2 p) => td.GetInterpolatedNormal(p.x / td.size.x, p.y / td.size.z);
        Quaternion OnGround(Vector2 p, float tilt) => Quaternion.FromToRotation(Vector3.up, Normal(p)) * Quaternion.Euler(R(-tilt, tilt), R(0, 360), R(-tilt, tilt));
        var bridge = WorldMapLayout.Bridge;
        int nLining = 0, nBoulder = 0, nChannel = 0, nWood = 0, nLotus = 0;

        string[] knSmall = { KN + "Rocks/SmallStone_1.prefab", KN + "Rocks/SmallStone_2.prefab", KN + "Rocks/SmallStone_3.prefab" };
        string[] hlStones = { HL + "Stone_2.prefab", HL + "Stone_3.prefab", HL + "Stone_4.prefab", HL + "Stone_4.prefab" };
        string KnSmall() => rng.NextDouble() < 0.08 ? KN + "Rocks/SmallStone_4.prefab" : knSmall[rng.Next(knSmall.Length)];

        for (int li = 0; li < WorldMapLayout.Lines.Length; li++)
        {
            var l = WorldMapLayout.Lines[li];
            bool river = WorldMapLayout.IsRiver(l);
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

            // character per stretch (scratchpad research5/stream_dressing.md): A rocky highland stream (steep or in the
            // highlands, HIGHLANDS stones), B boulder-lined stream (Grasslands round boulders, half of them in the channel),
            // C calm lotus stream (flat: BK lotus runs along the banks, astilbe on them). The river keeps its cut banks and
            // bars plus a few boulders in the channel; the outlet is a calm wide river with lotus on its inner bends.
            float SlopeAt(float s) { var (_, _, _, s0) = At(s - 6); var (_, _, _, s1) = At(s + 6); return (s0 - s1) / 12f; }
            char Character(float s, Vector2 at)
            {
                if (l.name == "River") return 'R';
                if (l.name == "Outlet") return 'O';
                float slope = SlopeAt(s);
                if (slope > 0.05f || WorldMapLayout.VegWeights(at).z > 0.5f) return 'A';
                return slope < 0.01f ? 'C' : 'B';
            }

            foreach (int side in new[] { 1, -1 })
            {
                bool inRun = rng.NextDouble() < 0.5; float left = R(2, 8);
                bool lotusRun = rng.NextDouble() < 0.4; float lotusLeft = R(5, 30);
                const float step = 1f;
                for (float s = R(1, 3); s < total - 2; s += step)
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
                    float bar = side * Mathf.Clamp(2.5f * WorldMapLayout.Fbm(s / (3.5f * width) + 3.1f, 7.7f + li, 2), -1, 1);
                    float cc = Mathf.Clamp(0.6f * bend + 0.9f * bar, -1, 1);
                    if (l.name == "River" && Vector2.Distance(edgeP, new Vector2(320, 364)) < 25f) cc = 1f;                    // cut bank at the bridge
                    if (l.name == "River" && edgeP.x > 483 && edgeP.x < 529 && edgeP.y > 288) cc = -1f;                      // reed bar at the mouth
                    bool skip = !found || Vector2.Distance(edgeP, bridge) < 9f + w * 0.5f || WorldMapLayout.BasinShoreDistance(edgeP) < 4f || WorldMapLayout.TrailDistancePublic(edgeP) < 1.8f;
                    if (skip) continue;
                    char ch = Character(s, edgeP);
                    float bendMul = cc > 0.3f ? 1.25f : cc < -0.3f ? 0.5f : 1f;
                    bool P(float perMetre) => rng.NextDouble() < perMetre * step;

                    // lotus runs (calm stretches, the outlet's inner bends): 10-40 m runs on ~40% of the bank, 1.5 m in from it
                    bool lotusHere = ch == 'C' || (ch == 'O' && cc < -0.3f);
                    if (lotusHere)
                    {
                        lotusLeft -= step;
                        if (lotusLeft <= 0) { lotusRun = !lotusRun; lotusLeft = lotusRun ? R(10, 40) : R(15, 60); }
                        if (lotusRun && P(ch == 'C' ? 0.7f : 0.5f))
                        {
                            var q = edgeP - n * R(0.7f, 2.5f) + tan * R(-0.4f, 0.4f);
                            if (surf - H(q) > 0.15f)
                            {
                                int r100 = rng.Next(100);
                                int k = r100 < 22 ? 5 : r100 < 43 ? 2 : r100 < 62 ? 1 : r100 < 79 ? 3 : r100 < 96 ? 4 : 6;
                                float sc = k == 2 || k == 3 || k == 4 ? R(1.8f, 3.6f) : R(1.2f, 2.0f);
                                Put(lotus, BKAV + "Lotus_" + k + ".prefab", new Vector3(q.x, surf, q.y), Quaternion.Euler(0, R(0, 360), 0), sc);
                                nLotus++;
                            }
                        }
                        if (lotusRun && P(0.3f))
                        {
                            var q = edgeP + n * R(0.5f, 4f);
                            if (H(q) > surf + 0.1f) { Put(lotus, BKAV + "Astilb_" + (1 + rng.Next(3)) + ".prefab", new Vector3(q.x, H(q) - 0.05f, q.y), Quaternion.Euler(0, R(0, 360), 0), R(1.0f, 1.6f)); nLotus++; }
                        }
                    }

                    switch (ch)
                    {
                        case 'A':
                        {
                            if (P(0.9f * bendMul))
                            {
                                double u = rng.NextDouble();
                                float d = u < 0.22 ? -R(0.5f, Mathf.Max(0.6f, 0.7f * w)) : u < 0.74 ? R(-0.3f, 1f) : R(1f, 8f);
                                var q = edgeP + n * d + tan * R(-0.4f, 0.4f);
                                int r100 = rng.Next(100);
                                string path = HL + "Stone_" + (r100 < 40 ? 4 : r100 < 69 ? 3 : r100 < 96 ? 2 : 1) + ".prefab";
                                float sc = R(0.9f, 2.0f);
                                Put(lining, path, new Vector3(q.x, H(q) - R(0.25f, 0.5f), q.y), OnGround(q, 8), sc);
                                nLining++;
                            }
                            if (P(cc < -0.3f ? 0.7f : 0.33f))
                            {
                                var q = edgeP + n * R(-0.5f, 2f) + tan * R(-0.5f, 0.5f);
                                Put(lining, HL + "Stones.prefab", new Vector3(q.x, H(q) - 0.05f, q.y), OnGround(q, 4), R(1.0f, 1.6f));
                                nLining++;
                            }
                            if (P(0.075f))
                            {
                                var q = edgeP + n * R(2.3f, 7.5f);
                                float sc = R(0.6f, 1.0f);
                                Put(boulders, new[] { HL + "Rock_1.prefab", HL + "Rock_2.prefab", WorldMapLayout.HighlandGrassRock(1), WorldMapLayout.HighlandGrassRock(2) }[rng.Next(4)], new Vector3(q.x, H(q) - 0.7f * 3f * sc, q.y), OnGround(q, 15), sc);
                                nBoulder++;
                            }
                            if (side == 1 && SlopeAt(s) > 0.05f && P(0.1f))
                            {
                                var q = c + n * R(-0.3f, 0.3f) * w;
                                Put(inChannel, HL + (rng.Next(2) == 0 ? "Stone_1.prefab" : "Stone_2.prefab"), new Vector3(q.x, H(q) - 0.4f, q.y), OnGround(q, 10), R(1.5f, 2.2f));
                                nChannel++;
                            }
                            break;
                        }
                        case 'B':
                        case 'O':
                        case 'R':
                        {
                            // round grey boulders (Grasslands): about half stand in the channel 15-50% of the way to the centre,
                            // tops within 0.9 m of the surface; the rest sit on the bank, half sunk
                            float rate = ch == 'B' ? 0.5f * bendMul : ch == 'O' ? (cc > 0.3f ? 0.3f : 0.06f) : 0.2f;
                            if (P(rate))
                            {
                                var (path, minY, maxY) = glBoulders[rng.Next(glBoulders.Length)];
                                float sc = R(0.65f, 1.4f);
                                if (rng.NextDouble() < 0.56)
                                {
                                    var q = edgeP - n * (R(0.15f, 0.5f) * w);
                                    float y = Mathf.Min(surf + R(-0.9f, 0.9f) - maxY * sc, H(q) - minY * sc - 0.15f);
                                    if (surf - H(q) > 0.2f) { Put(inChannel, path, new Vector3(q.x, y, q.y), Quaternion.Euler(R(-12, 12), R(0, 360), R(-12, 12)), sc); nChannel++; }
                                }
                                else
                                {
                                    var q = edgeP + n * R(-0.3f, 2f);
                                    Put(boulders, path, new Vector3(q.x, H(q) - minY * sc - R(0.25f, 0.5f) * (maxY - minY) * sc, q.y), OnGround(q, 14), sc);
                                    nBoulder++;
                                }
                            }
                            if (ch == 'B' && P(0.4f))
                            {
                                var q = rng.NextDouble() < 0.4 ? edgeP - n * R(0.2f, 0.4f * w) : edgeP + n * R(0f, 1.5f);
                                Put(lining, GLR + "Small_Stone_" + new[] { 7, 8, 6 }[rng.Next(3)] + ".prefab", new Vector3(q.x, H(q) - 0.05f, q.y), OnGround(q, 6), R(1.3f, 1.5f));
                                nLining++;
                            }
                            if (ch == 'R')
                            {
                                // the river's own lining: runs of Komorebi stones (4-14 m) with gaps, dense on cut banks, sparse on bars
                                float density = cc > 0.3f ? 0.6f : cc < -0.3f ? 0.1f : 0.4f;
                                if (inRun && P(density / 0.72f))
                                {
                                    float u = (float)rng.NextDouble();
                                    float d = u < 0.2f ? R(-2.5f, -1f) : u < 0.7f ? R(-1f, 0.4f) : u < 0.9f ? R(0.4f, 2f) : R(2f, 4f);
                                    var q = edgeP + n * d + tan * R(-0.6f, 0.6f);
                                    float sc = R(1.8f, 3.0f);
                                    Put(lining, KnSmall(), new Vector3(q.x, H(q) - 0.06f * sc - R(0.02f, 0.1f), q.y), OnGround(q, 5), sc);
                                    nLining++;
                                }
                                left -= step;
                                if (left <= 0) { inRun = !inRun; left = inRun ? R(4, 14) : (cc < -0.3f ? R(4, 12) : R(1, 5)); }
                                float bDensity = cc > 0.3f ? 0.04f : cc < -0.3f ? 0.005f : 0.02f;
                                if (P(bDensity))
                                {
                                    var q = edgeP + n * R(-1f, 1.5f);
                                    string[] big = { KN + "Rocks/Stone_3_NoGrass.prefab", KN + "Rocks/Stone_4_NoGrass.prefab", KN + "Rocks/Stone_7_NoGrass.prefab" };
                                    float[] tops = { 1.7f, 1.4f, 2.7f };
                                    int k = rng.Next(big.Length);
                                    float sc = R(0.5f, 0.9f);
                                    Put(boulders, big[k], new Vector3(q.x, H(q) - 0.42f * tops[k] * sc, q.y), Quaternion.FromToRotation(Vector3.up, Normal(q)) * Quaternion.Euler(R(-13, 13), R(0, 360), R(-13, 13)), sc);
                                    nBoulder++;
                                }
                            }
                            // driftwood: more on bars
                            if (ch != 'O' && P(cc < -0.3f ? 0.03f : 0.01f))
                            {
                                var q = edgeP + n * (ch == 'R' ? R(-0.5f, 1.5f) : R(0f, 4f));
                                string[] logs = ch == 'R' ? new[] { HL + "Branch_2.prefab", HL + "Branches_2.prefab", GV + "Big_Branch_1.prefab" }
                                                          : new[] { HL + "Branch_1.prefab", HL + "Branch_2.prefab", HL + "Branch_3.prefab", HL + "Branches_1.prefab", HL + "Branches_2.prefab", HL + "Log_1.prefab" };
                                Put(wood, logs[rng.Next(logs.Length)], new Vector3(q.x, H(q) - 0.05f, q.y), OnGround(q, 4), R(0.9f, 1.4f));
                                nWood++;
                            }
                            break;
                        }
                        case 'C':
                            if (P(0.08f))
                            {
                                var q = edgeP + n * R(0f, 1.5f);
                                Put(lining, GLR + "Small_Stone_" + new[] { 7, 8, 6 }[rng.Next(3)] + ".prefab", new Vector3(q.x, H(q) - 0.05f, q.y), OnGround(q, 6), R(1.2f, 1.5f));
                                nLining++;
                            }
                            break;
                    }
                }
            }

            // the river's stones in the channel: ~6 per 100 m, biased below the bridge
            if (l.name == "River")
            {
                int want = Mathf.RoundToInt(total / 100f * 6);
                for (int i = 0, made = 0; i < want * 30 && made < want; i++)
                {
                    float s = rng.NextDouble() < 0.4 ? R(100, 220) : R(3, total - 3);
                    var (c, tan, w, surf) = At(s);
                    var p = c + new Vector2(-tan.y, tan.x) * R(-0.85f, 0.85f) * w;
                    if (Vector2.Distance(p, bridge) < 9f + w * 0.5f || WorldMapLayout.BasinShoreDistance(p) < 4f) continue;
                    float depth = surf - H(p);
                    if (depth < 0.3f || depth > 1.2f) continue;
                    Put(inChannel, KnSmall(), new Vector3(p.x, H(p) - 0.04f * 3f, p.y), OnGround(p, 8), R(2.5f, 3.5f));
                    made++; nChannel++;
                }
            }
        }

        // cut bank at the bridge: a grass-topped rock wall on the outer bank just downstream, toe boulders below it
        var riverPts = WorldMapLayout.Lines.First(x => x.name == "River").pts;
        int bi = 0;
        for (int i = 1; i < riverPts.Length; i++) if ((new Vector2(riverPts[i].x, riverPts[i].y) - bridge).sqrMagnitude < (new Vector2(riverPts[bi].x, riverPts[bi].y) - bridge).sqrMagnitude) bi = i;
        var wallAt = new List<Vector2>();
        for (int k = 3; k <= 7; k += 2)   // 15-35 m downstream, on the north (outer) bank
        {
            int i = Mathf.Min(riverPts.Length - 2, bi + k);
            var tan = new Vector2(riverPts[i + 1].x - riverPts[i].x, riverPts[i + 1].y - riverPts[i].y).normalized;
            wallAt.Add(new Vector2(riverPts[i].x, riverPts[i].y) + new Vector2(-tan.y, tan.x) * (riverPts[i].w + 1.5f));
        }
        var wallKinds = new[] { (KN + "Rocks Grass/Stone_1.prefab", 1.6f), (KN + "Rocks Grass/Stone_2.prefab", 3.2f), (KN + "Rocks Grass/Stone_5.prefab", 3.9f) };
        foreach (var q in wallAt)
        {
            var (s, kind) = WorldMapLayout.WaterSurfaceAt(q);
            if (float.IsNegativeInfinity(s)) continue;
            var (path, top) = wallKinds[rng.Next(wallKinds.Length)];
            float sc = R(0.45f, 0.7f), sy = sc * R(0.8f, 0.9f);
            var go = Put(hero, path, new Vector3(q.x, s + R(1.0f, 2.0f) - top * sy, q.y), Quaternion.Euler(R(-12, 12), R(0, 360), R(-12, 12)), sc);
            go.transform.localScale = new Vector3(sc, sy, sc * R(0.9f, 1.2f));
        }

        // fallen-log footbridges where the forest->swamp trail (twice) and the highland-spur trail cross streams
        foreach (var (lineName, near) in new[] { ("ForestStream", new Vector2(434.1f, 503.2f)), ("WestStream", new Vector2(279.4f, 561.6f)), ("MarginStream", new Vector2(586f, 643f)) })
        {
            var l = WorldMapLayout.Lines.First(x => x.name == lineName);
            int best = 0;
            for (int i = 1; i < l.pts.Length; i++) if ((new Vector2(l.pts[i].x, l.pts[i].y) - near).sqrMagnitude < (new Vector2(l.pts[best].x, l.pts[best].y) - near).sqrMagnitude) best = i;
            var a = l.pts[Mathf.Max(0, best - 1)]; var b = l.pts[Mathf.Min(l.pts.Length - 1, best + 1)];
            var c = new Vector2(l.pts[best].x, l.pts[best].y); float surf = l.pts[best].z;
            var tan = new Vector2(b.x - a.x, b.y - a.y).normalized; var n = new Vector2(-tan.y, tan.x);
            float half = l.pts[best].w + 1.6f;                                     // bank to bank
            var e1 = c + n * half; var e2 = c - n * half;
            float h1 = H(e1), h2 = H(e2);
            float yaw = Mathf.Atan2(-n.y, n.x) * Mathf.Rad2Deg;                  // the log's long axis (local X) across the flow
            float pitch = Mathf.Atan2(h1 - h2, 2 * half) * Mathf.Rad2Deg;
            float y = Mathf.Max(surf + 0.45f, (h1 + h2) * 0.5f + 0.25f);
            var log = Put(hero, KN + "Nature/Log_2.prefab", new Vector3(c.x, y + 0.4f, c.y), Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, pitch), 1.05f);
            float sx = 1.05f * half / 3.6f;                                             // Log_2 spans 3.6 m each side at 1.05
            log.transform.localScale = new Vector3(sx, 1.05f * Mathf.Lerp(1f, sx / 1.05f, 0.35f), 1.05f * Mathf.Lerp(1f, sx / 1.05f, 0.35f));
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
        return new { lining = nLining, boulders = nBoulder, inChannel = nChannel, driftwood = nWood, lotusAndAstilbe = nLotus };
    }

    // Trees and bush clumps around the home base, from the yard-local tables in WorldMapLayout.
    public static object BuildHomeTrees()
    {
        terrain = Terrain.activeTerrain;
        var td = terrain.terrainData;
        var root = Group("Home Trees");   // never under Landmarks/Home Base: the clear-mask pass would mow around each canopy
        foreach (var t in WorldMapLayout.HomeTrees)
        {
            var p = WorldMapLayout.Yard(t.at.x, t.at.y);
            float slope = Vector3.Angle(Vector3.up, td.GetInterpolatedNormal(p.x / td.size.x, p.y / td.size.z));
            Put(root, t.prefab, new Vector3(p.x, H(p) - t.sink - 0.015f * slope, p.y), Quaternion.Euler(0, t.yaw, 0), t.scale);
        }
        foreach (var b in WorldMapLayout.HomeBushes)
        {
            var p = WorldMapLayout.Yard(b.at.x, b.at.y);
            Put(root, b.prefab, new Vector3(p.x, H(p) - 0.1f, p.y), Quaternion.Euler(0, b.yaw, 0), b.scale);
        }
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return new { trees = WorldMapLayout.HomeTrees.Length, bushes = WorldMapLayout.HomeBushes.Length };
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
        var home = WorldMapLayout.HomeBase;
        var trees = td.treeInstances.Select(t => new Vector2(t.position.x * td.size.x, t.position.z * td.size.z))
            .Where(q => WorldMapLayout.SignedDistance(q, WorldMapLayout.MeadowArea) < 30).ToList();
        trees.AddRange(WorldMapLayout.HomeTrees.Select(t => WorldMapLayout.Yard(t.at.x, t.at.y)));
        Quaternion OnGround(Vector2 p, float follow) =>
            Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, td.GetInterpolatedNormal(p.x / td.size.x, p.y / td.size.z), follow)) * Quaternion.Euler(0, R(0, 360), 0);
        bool Ok(Vector2 p, float treeGap, float trailGap)
        {
            if (WorldMapLayout.SignedDistance(p, WorldMapLayout.MeadowArea) > -6) return false;
            if (WorldMapLayout.WaterEdgeDistancePublic(p) < 6f) return false;
            if (WorldMapLayout.TrailDistancePublic(p) < trailGap) return false;
            if (WorldMapLayout.FenceDistance(p) < 9f) return false;
            return trees.All(t => (t - p).sqrMagnitude > treeGap * treeGap);
        }

        // lone hero trees first, so the stones keep their distance from them too
        int nTrees = 0;
        foreach (var spot in new[] { new Vector2(340, 250), new Vector2(410, 268), new Vector2(470, 250), new Vector2(285, 340), new Vector2(300, 228), new Vector2(470, 232) })
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
        for (int i = 0; i < 12000 && centres.Count < 26; i++)
        {
            var c = new Vector2(R(236, 520), R(205, 345));
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
        for (int i = 0; i < 3000 && nBig < 4; i++)
        {
            var p = new Vector2(R(250, 510), R(210, 340));
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

    // The home pond after the Komorebi demo (measured in scratchpad research4/komorebi_pond.md): a near-continuous ring of
    // grey stones, half of them breaking the surface, around every shore; a few boulders in the water; lily clusters beside
    // the island, the islets and the front bays (none near the fall); pines, Sakura and flowering bushes on the island and
    // islets; Sakura and pines on the banks, bushes on the waterfall rocks, rock terraces on the back-right and a small
    // outcrop framing the river outlet. Positions use the demo's flow frame (s downstream, t to the right) scaled by 0.88.
    public static object BuildHomePond(int seed)
    {
        terrain = Terrain.activeTerrain;
        rng = new System.Random(seed);
        Physics.SyncTransforms();
        var pond = WorldMapLayout.Basins.First(b => b.name == "WaterfallPond");
        float level = pond.level;
        var root = Group("Home Pond");
        Transform Sub(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }
        var stonesRoot = Sub("Shore Stones"); var inWater = Sub("In Water"); var lilyRoot = Sub("Lilies"); var islRoot = Sub("Islands"); var banks = Sub("Banks");
        var td = terrain.terrainData;
        Quaternion Tilted(float lo, float hi) => Quaternion.Euler(R(-1, 1) * R(lo, hi), R(0, 360), R(-1, 1) * R(lo, hi));
        Quaternion Upright() => Quaternion.Euler(0, R(0, 360), 0);
        var fall = WorldMapLayout.PondFall;
        var f = WorldMapLayout.TracedForward; var right = WorldMapLayout.TracedSide;
        // (s, t) in the traced demo frame: W0 at the old size (behind the fall), W stretched with the pond
        Vector2 W0(float s, float t) => fall + f * (0.88f * s) + right * (0.88f * t);
        Vector2 W(float s, float t) => WorldMapLayout.PondXf(W0(s, t));
        float Sof(Vector2 p) => Vector2.Dot(WorldMapLayout.PondXfInverse(p) - fall, f) / 0.88f;
        float Tof(Vector2 p) => Vector2.Dot(WorldMapLayout.PondXfInverse(p) - fall, right) / 0.88f;
        var river = WorldMapLayout.Lines.First(l => l.name == "River");
        bool InOutflow(Vector2 p) => WorldMapLayout.TrueLineEdge(p, river) < 3f;
        var placed = new List<Vector3>();
        bool Free(Vector2 p, float r) => placed.All(q => (new Vector2(q.x, q.y) - p).sqrMagnitude > (q.z + r) * (q.z + r));
        string Stone() { int r = rng.Next(100); return KN + "Rocks/SmallStone_" + (r < 24 ? 1 : r < 61 ? 2 : r < 97 ? 3 : 4) + ".prefab"; }
        var pondIslands = Enumerable.Range(0, WorldMapLayout.Islands.Length)
            .Where(i => WorldMapLayout.SignedDistance(new Vector2(WorldMapLayout.Islands[i].x, WorldMapLayout.Islands[i].y), pond.shore) < 0).ToArray();
        bool OnLand(Vector2 p, float above) => H(p) > level + above;
        int nStones = 0, nLilies = 0, nPlants = 0;

        // ---- waterline samples (point on the real waterline, normal pointing into the water) ----
        var shore = new List<(Vector2 p, Vector2 n, bool island)>();
        var poly = pond.shore;
        var cum = new float[poly.Length + 1];
        for (int i = 0; i < poly.Length; i++) cum[i + 1] = cum[i] + Vector2.Distance(poly[i], poly[(i + 1) % poly.Length]);
        for (float s = 0; s < cum[^1]; s += R(1.1f, 2.5f))
        {
            int i = Mathf.Clamp(Array.FindLastIndex(cum, c => c <= s), 0, poly.Length - 1);
            var a = poly[i]; var b = poly[(i + 1) % poly.Length];
            var q = Vector2.Lerp(a, b, (s - cum[i]) / Mathf.Max(1e-3f, cum[i + 1] - cum[i]));
            var tan = (b - a).normalized; var outward = new Vector2(tan.y, -tan.x);
            if (WorldMapLayout.SignedDistance(q + outward * 2f, poly) < 0) outward = -outward;
            for (float d = -7f; d <= 7f; d += 0.25f)          // from the water side out: first ground above the water
                if (H(q + outward * d) > level) { if (d > -7f) shore.Add((q + outward * d, -outward, false)); break; }
        }
        foreach (int i in pondIslands)
        {
            var c = new Vector2(WorldMapLayout.Islands[i].x, WorldMapLayout.Islands[i].y);
            float rw = WorldMapLayout.IslandWaterline(i);
            for (float arc = 0; arc < 2 * Mathf.PI * (rw + 2); arc += R(1.1f, 2.5f))
            {
                float ang = arc / (rw + 2);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                for (float d = 0; d < rw + 7f; d += 0.25f)
                    if (H(c + dir * d) < level) { if (WorldMapLayout.IslandAt(c + dir * d) < 0 || d > 0) shore.Add((c + dir * d, dir, true)); break; }
            }
        }

        // ---- 1. stone ring: ~8 per 10 m, 5-7% left bare in short gaps; 55% straddle the surface ----
        float gap = 0;
        foreach (var (p, n, island) in shore)
        {
            if (gap > 0) { gap -= 1.8f; continue; }
            if (rng.NextDouble() < 0.03) { gap = R(2, 5); continue; }
            if (InOutflow(p)) continue;
            double u = rng.NextDouble();
            float off = u < 0.30 ? R(1f, 3f) : u < 0.52 ? R(0f, 1f) : u < 0.70 ? R(-1f, 0f) : R(-3f, -1f);   // + = into the water
            var q = p + n * off;
            float sc = rng.NextDouble() < 0.03 ? R(3.5f, 4.5f) : R(1.5f, 3.0f);
            if (!Free(q, 0.3f * sc)) continue;
            Put(stonesRoot, Stone(), new Vector3(q.x, H(q) - R(0.15f, 0.35f), q.y), Tilted(9, 25), sc);
            placed.Add(new Vector3(q.x, q.y, 0.35f * sc)); nStones++;
        }
        // deeper stones (tops under the surface) and set-back stones on the banks
        for (int k = 0, tries = 0; k < 35 && tries < 600; tries++)
        {
            var (p, n, _) = shore[rng.Next(shore.Count)];
            var q = p + n * R(2f, 4f);
            if (InOutflow(q) || level - H(q) < 0.8f || !Free(q, 0.8f)) continue;
            Put(stonesRoot, Stone(), new Vector3(q.x, H(q) - R(0.2f, 0.4f), q.y), Tilted(9, 25), R(1.5f, 2.6f));
            placed.Add(new Vector3(q.x, q.y, 0.8f)); k++; nStones++;
        }
        for (int k = 0, tries = 0; k < 40 && tries < 600; tries++)
        {
            var (p, n, island) = shore[rng.Next(shore.Count)];
            var q = p - n * R(3f, island ? 5f : 12f);
            if (!OnLand(q, 0.2f) || InOutflow(q) || WorldMapLayout.TrailDistancePublic(q) < 2.5f || !Free(q, 0.9f)) continue;
            Put(stonesRoot, Stone(), new Vector3(q.x, H(q) - R(0.2f, 0.35f), q.y), Tilted(9, 25), R(2.0f, 2.8f));
            placed.Add(new Vector3(q.x, q.y, 0.9f)); k++; nStones++;
        }

        // ---- 2. boulders in the water: three break the surface, the rest show through it ----
        for (int k = 0, tries = 0; k < 6 && tries < 800; tries++)
        {
            var (p, n, _) = shore[rng.Next(shore.Count)];
            var q = p + n * R(1f, 10f);
            float depth = level - H(q);
            if (depth < 1f || depth > 2.8f || InOutflow(q) || Vector2.Distance(q, fall) < 10f || !Free(q, 2f)) continue;
            bool breaks = k < 3;
            string path = breaks ? KN + "Rocks/Stone_2_NoGrass.prefab" : KN + (rng.Next(2) == 0 ? "Rocks/Stone_1_NoGrass.prefab" : "Rocks/Stone_7_NoGrass.prefab");
            float sc = breaks ? R(0.85f, 1.1f) : R(1.0f, 1.4f);
            float top = breaks ? level + R(0.5f, 1.0f) : level - R(0.8f, 2.0f);
            float meshTop = breaks ? 3.2f : 1.6f;                          // pivot to top at scale 1 (Stone_2 / Stone_1, 7)
            Put(inWater, path, new Vector3(q.x, Mathf.Min(top - meshTop * sc, H(q) - 0.2f + 0.6f * meshTop * sc), q.y), Tilted(4, 14), sc);
            placed.Add(new Vector3(q.x, q.y, 2f)); k++;
        }

        // ---- 3. lilies: 7-9 clusters of 3-11 groups, 2.5-7 m from a shore, none within ~36 m of the fall, 60% right arm ----
        int clusters = 0;
        var lilyCentres = new List<Vector2>();
        for (int tries = 0; tries < 3000 && clusters < 9; tries++)
        {
            var (p, n, island) = shore[rng.Next(shore.Count)];
            var c = p + n * R(2.5f, 7f);
            float depth = level - H(c);
            if (depth < 0.8f || depth > 2.2f || Sof(c) < 41f || InOutflow(c)) continue;
            if (Tof(c) < 0 && rng.NextDouble() < 0.4) continue;            // weight toward the wider right arm
            if (!Free(c, 1.5f) || lilyCentres.Any(o => (o - c).sqrMagnitude < 12f * 12f)) continue;
            lilyCentres.Add(c);
            clusters++;
            int count = 3 + rng.Next(9);
            float radius = R(2.5f, 6.5f);
            var group = new List<Vector2>();
            for (int k = 0, t2 = 0; k < count && t2 < 200; t2++)
            {
                var q = c + new Vector2(R(-radius, radius), R(-radius, radius));
                float dq = level - H(q);
                if (dq < 0.7f || dq > 2.6f || InOutflow(q) || !Free(q, 0.6f) || group.Any(g => (g - q).sqrMagnitude < 2.6f * 2.6f)) continue;
                Put(lilyRoot, KN + "Nature/WaterLily_Group_" + (rng.NextDouble() < 0.55 ? 2 : 3) + ".prefab", new Vector3(q.x, level + 0.028f, q.y), Upright(), R(1.0f, 1.1f));
                group.Add(q); k++; nLilies++;
            }
            foreach (var g in group) placed.Add(new Vector3(g.x, g.y, 1.4f));
        }

        // ---- 4. island and islets ----
        GameObject Plant(Transform parent, string path, Vector2 at, float scale, float sink = 0.15f)
        {
            nPlants++;
            placed.Add(new Vector3(at.x, at.y, 1.2f));
            return Put(parent, KN + path, new Vector3(at.x, H(at) - sink, at.y), Upright(), scale);
        }
        Vector2 OnIsland(Vector2 want, float minAbove, float jitter)
        {
            for (int t = 0; t < 40; t++)
            {
                var q = want + new Vector2(R(-jitter, jitter), R(-jitter, jitter));
                if (OnLand(q, minAbove)) return q;
            }
            return new Vector2(float.NaN, 0);
        }
        void Try(Transform parent, string path, Vector2 want, float scale, float minAbove, float jitter)
        {
            var q = OnIsland(want, minAbove, jitter);
            if (!float.IsNaN(q.x)) Plant(parent, path, q, scale);
        }
        var central = Sub2(islRoot, "Central Island");
        Try(central, "Nature/PineTree_4.prefab", W(55, 7), R(1.2f, 1.3f), 1.2f, 2f);
        Try(central, "Nature/PineTree_3.prefab", W(47, 3), 1.0f, 0.8f, 2f);
        Try(central, "Nature/Sakura_2.prefab", W(70, 4), 0.95f, 0.9f, 2f);
        for (int k = 0; k < 3; k++) Try(central, "Nature/Bush_" + (k == 1 ? 3 : 1) + "_Flowers.prefab", W(65, 8), R(0.92f, 1.02f), 1.0f, 2.5f);
        for (int k = 0; k < 4; k++) Try(central, "Nature/Bush_" + (1 + rng.Next(3)) + "_Flowers.prefab", W(R(62, 76), R(0, 12)), R(0.92f, 1.02f), 0.5f, 1f);
        for (int k = 0; k < 6; k++) Try(central, "Nature/Bush_" + (1 + rng.Next(3)) + ".prefab", W(R(45, 60), R(-2, 15)), R(0.97f, 1.1f), 0.5f, 1f);
        for (int k = 0; k < 10; k++) Try(central, "Nature/Fern_" + (1 + rng.Next(3)) + ".prefab", W(R(44, 78), R(-4, 16)), R(0.9f, 1.2f), 0.25f, 1f);
        for (int k = 0; k < 4; k++) Try(central, "Nature/Farfugium_" + (1 + rng.Next(3)) + ".prefab", W(R(50, 76), R(-3, 15)), R(0.9f, 1.1f), 0.3f, 1f);
        var isletW = Sub2(islRoot, "Islet W");
        Try(isletW, "Nature/Sakura_5.prefab", WorldMapLayout.PondXf(new Vector2(230.7f, 414.5f)), 1.0f, 0.6f, 1f);
        for (int k = 0; k < 4; k++) Try(isletW, "Nature/Fern_" + (1 + rng.Next(3)) + ".prefab", WorldMapLayout.PondXf(new Vector2(230.7f, 414.5f)), R(0.8f, 1.1f), 0.2f, 4f);
        var isletNE = Sub2(islRoot, "Islet NE");
        Try(isletNE, "Nature/Sakura_3.prefab", WorldMapLayout.PondXf(new Vector2(240.7f, 438.5f)), 1.0f, 0.4f, 1f);
        for (int k = 0; k < 3; k++) Try(isletNE, "Nature/Fern_" + (1 + rng.Next(3)) + ".prefab", WorldMapLayout.PondXf(new Vector2(240.7f, 438.5f)), R(0.8f, 1.1f), 0.15f, 3.5f);
        var isletS = Sub2(islRoot, "Flower Islet");
        Try(isletS, "Nature/Bush_3_Flowers.prefab", WorldMapLayout.PondXf(new Vector2(276f, 428f)), 1.0f, 0.3f, 0.8f);
        Try(isletS, "Nature/Bush_1_Flowers.prefab", WorldMapLayout.PondXf(new Vector2(276f, 428f)), 1.1f, 0.1f, 3f);
        for (int k = 0; k < 8; k++) Try(isletS, "Nature/Farfugium_" + (1 + rng.Next(3)) + ".prefab", WorldMapLayout.PondXf(new Vector2(276f, 428f)), R(0.9f, 1.1f), 0.08f, 3.2f);
        for (int k = 0; k < 2; k++) Try(isletS, "Nature/Salvia_Group_1.prefab", WorldMapLayout.PondXf(new Vector2(276f, 428f)), R(0.9f, 1.1f), 0.1f, 2.5f);

        // ---- 5. banks ----
        // a point on the bank `back` metres past the waterline, walking from a point in the water along dir
        Vector2 Bank(Vector2 from, Vector2 dir, float back)
        {
            for (float d = 0; d < 80f; d += 0.5f) if (H(from + dir * d) > level + 0.2f) return from + dir * (d + back);
            return new Vector2(float.NaN, 0);
        }
        void BankPlant(string path, Vector2 at, float scale)
        {
            if (float.IsNaN(at.x) || InOutflow(at) || WorldMapLayout.TrailDistancePublic(at) < 3f || !Free(at, 1.5f)) return;
            Plant(banks, path, at, scale);
        }
        BankPlant("Nature/Sakura_1.prefab", Bank(W(52, -20), -WorldMapLayout.PondSide, R(2, 5)), R(1.0f, 1.15f));          // left arm (north)
        BankPlant("Nature/Sakura_2.prefab", Bank(W(58, 30), WorldMapLayout.PondSide, R(2, 5)), R(1.1f, 1.25f));            // right arm (south)
        BankPlant("Nature/Sakura_5.prefab", Bank(W(72, 30), WorldMapLayout.PondSide, 12), 0.9f);
        for (int k = 0, tries = 0; k < 7 && tries < 1500; tries++)                                         // pines on the sides and front
        {
            float s = R(30, 100), t = rng.Next(2) == 0 ? R(-60, -30) : R(38, 62);
            var q = W(s, t);
            float dw = WorldMapLayout.BasinEdgeDistance(q);
            if (dw < 5f || dw > 20f || !OnLand(q, 0.5f) || InOutflow(q) || WorldMapLayout.TrailDistancePublic(q) < 3.5f || !Free(q, 4f)) continue;
            Plant(banks, "Nature/PineTree_" + new[] { 1, 2, 3, 5, 6 }[rng.Next(5)] + ".prefab", q, R(0.9f, 1.1f));
            k++;
        }
        for (int k = 0, tries = 0; k < 10 && tries < 400; tries++)                                         // bushes at the water, front and left beach
        {
            var (p, n, island) = shore[rng.Next(shore.Count)];
            if (island || Sof(p) < 40f) continue;
            var q = p - n * R(1.5f, 8f);
            if (!OnLand(q, 0.15f) || InOutflow(q) || WorldMapLayout.TrailDistancePublic(q) < 2.5f || !Free(q, 1.3f)) continue;
            Plant(banks, "Nature/Bush_" + (1 + rng.Next(3)) + (rng.NextDouble() < 0.5 ? "_Flowers" : "") + ".prefab", q, R(0.9f, 1.1f));
            k++;
        }
        // the pink frame behind the fall and plain bushes on the waterfall rocks: drop onto whatever is there (rock or ground)
        var lm = GameObject.Find("Environment/Landmarks/Waterfall");
        bool Drop(Vector2 at, out Vector3 hitPoint)
        {
            hitPoint = default;
            if (!Physics.Raycast(new Vector3(at.x, 300f, at.y), Vector3.down, out var hit, 400f)) return false;
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.75f) return false;
            hitPoint = hit.point;
            return true;
        }
        int frame = 0;
        for (int tries = 0; tries < 200 && frame < 4; tries++)
        {
            var q = W0(R(-33, -14), R(-22, 22));
            if (!Drop(q, out var hp) || hp.y < level + 10f || !Free(q, 5f)) continue;
            Put(banks, KN + "Nature/Sakura_" + (1 + frame) + ".prefab", hp - Vector3.up * 0.2f, Upright(), R(1.15f, 1.3f));
            placed.Add(new Vector3(q.x, q.y, 5f)); frame++; nPlants++;
        }
        int rockBushes = 0;
        if (lm != null)
        {
            var rb = lm.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, x) => { a.Encapsulate(x); return a; });
            for (int tries = 0; tries < 600 && rockBushes < 24; tries++)
            {
                var q = new Vector2(R(rb.min.x, rb.max.x), R(rb.min.z, rb.max.z));
                if (!Drop(q, out var hp) || hp.y < level + 4f || hp.y < H(q) + 1.5f || !Free(q, 1.2f)) continue;   // on a rock top, not the ground
                Put(banks, KN + "Nature/Bush_" + (1 + rng.Next(4)) + ".prefab", hp - Vector3.up * 0.15f, Upright(), R(0.9f, 1.1f));
                placed.Add(new Vector3(q.x, q.y, 1.2f)); rockBushes++; nPlants++;
            }
        }
        // back-right terraces and the outcrop framing the river outlet
        for (int k = 0, tries = 0; k < 6 && tries < 200; tries++)
        {
            var q = W(R(7, 30), R(42, 58));
            if (!Free(q, 4f) || InOutflow(q)) continue;
            float sc = R(0.45f, 0.7f);
            string path = KN + (rng.Next(3) == 0 ? "Rocks/Rock_3_NoGrass.prefab" : "Rocks/Rock_1_NoGrass.prefab");
            float meshTop = path.Contains("Rock_3") ? 17.9f : 13.8f;
            Put(banks, path, new Vector3(q.x, H(q) + R(4f, 11f) - meshTop * sc, q.y), Quaternion.Euler(R(-4, 4), R(0, 360), R(-4, 4)), sc);
            placed.Add(new Vector3(q.x, q.y, 9f * sc)); k++;
        }
        for (int k = 0, tries = 0; k < 6 && tries < 200; tries++)
        {
            var q = W(R(98, 108), R(14, 26));
            if (InOutflow(q) || WorldMapLayout.TrueLineEdge(q, river) < 5f || WorldMapLayout.TrailDistancePublic(q) < 3f || !Free(q, 2.5f)) continue;
            float sc = R(0.3f, 0.5f);
            Put(banks, KN + "Rocks Grass/Rock_1.prefab", new Vector3(q.x, H(q) + R(0.5f, 2.0f) - 13.8f * sc, q.y), Quaternion.Euler(R(-5, 5), R(0, 360), R(-5, 5)), sc);
            placed.Add(new Vector3(q.x, q.y, 9f * sc)); k++;
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return new { shoreSamples = shore.Count, stones = nStones, lilyClusters = clusters, lilies = nLilies, plants = nPlants, frameSakura = frame, rockBushes };
    }

    static Transform Sub2(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
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
        if (WorldMapLayout.SignedDistance(p, WorldMapLayout.Playable) > -r * 0.4f) return false;   // keep inside the walkable area
        var (s, kind) = WorldMapLayout.WaterSurfaceAt(p);
        if (!float.IsNegativeInfinity(s)) return false;                                               // not in or by water
        if (WorldMapLayout.TrailDistancePublic(p) < r + 3f) return false;                           // keep trails open
        if (Vector2.Distance(p, WorldMapLayout.HomeBase) < 40f + r) return false;                     // home base
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
            if (WorldMapLayout.SignedDistance(p, WorldMapLayout.HighlandsArea) > -6f) continue;
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
        var vp = WorldMapLayout.Viewpoint;
        float vpGround = H(vp);
        Put(viewpoint, Cliffs[1].path, new Vector3(vp.x, vpGround + 3.2f - Cliffs[1].top * 0.55f, vp.y), Quaternion.Euler(0, 35, 0), 0.55f);
        Put(viewpoint, Cliffs[0].path, new Vector3(vp.x + 6.5f, vpGround + 1.4f - Cliffs[0].top * 0.42f, vp.y - 4.5f), Quaternion.Euler(0, 160, 0), 0.42f);
        Put(viewpoint, Boulders[1].path, new Vector3(vp.x - 5.5f, vpGround - 0.4f, vp.y + 3.5f), Quaternion.Euler(0, 80, 0), 1.0f);
        Put(viewpoint, Boulders[6].path, new Vector3(vp.x - 3.0f, vpGround - 0.2f, vp.y - 5.0f), Quaternion.Euler(0, 210, 0), 0.8f);
        placed.Add(new Vector3(vp.x, vp.y, 12f));

        // boulder clusters: forest-heavy, a few on meadow edges; 1 large + 1-3 smaller within ~5 m
        for (int i = 0; i < 10000 && nBoulder < 115; i++)
        {
            var p = new Vector2(R(170, 960), R(70, 940));
            bool meadow = WorldMapLayout.SignedDistance(p, WorldMapLayout.MeadowArea) < 0;
            bool highland = WorldMapLayout.SignedDistance(p, WorldMapLayout.HighlandsArea) < 0;
            if (meadow && rng.NextDouble() > 0.25) continue;                     // sparse in the meadow
            if (WorldMapLayout.SignedDistance(p, WorldMapLayout.SwampArea) < 10) continue;
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
    const string Folder = WorldMapLayout.KitFolder + "/Vegetation";
    const string Maps = WorldMapLayout.HeightmapFolder + "/";
    const string KN = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/Nature/";
    const string HF = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
    const string GV = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Vegetation/";
    const string BKP = "Assets/_StoreAssets/Env/BK/PureNature_Meadows/Prefabs/Plants/";
    const string KR = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/Rocks/";
    const string Layers = WorldMapLayout.KitFolder + "/TerrainLayers/";

    // ---- masks from the analytic layout (terrain space, north up) ----
    public static async Task<object> GenerateMasks(int res)
    {
        var t0 = DateTime.Now;
        string[] names = { "Forest", "Meadow", "Highlands", "Swamp", "Foothills", "Wild", "Reeds", "WaterEdge", "MeadowFlowers", "Hedge", "Rim", "RimBroadleaf", "RimShort", "RimBushes", "LakeShore", "LakeTrees", "LakeLip", "LakeFlowers",
            "MeadowTrees", "Yard", "MeadowLawn", "MeadowTall", "MeadowCover", "MeadowSpikes", "TrailEdge", "TrailPebbles" };
        var maps = names.ToDictionary(n => n, n => new float[res * res]);
        var home = WorldMapLayout.HomeBase;
        var homeTrees = WorldMapLayout.HomeTrees.Select(t => WorldMapLayout.Yard(t.at.x, t.at.y)).ToArray();
        var centre = new Vector2(505, 520);
        await Task.Run(() => Parallel.For(0, res, j =>
        {
            for (int i = 0; i < res; i++)
            {
                float x = (i + 0.5f) / res * WorldMapLayout.Size, z = (j + 0.5f) / res * WorldMapLayout.Size;
                var p = new Vector2(x, z);
                int k = j * res + i;
                var w = WorldMapLayout.VegWeights(p);   // x forest, y meadow, z highlands, w swamp (no water fade)
                float dPlay = WorldMapLayout.SignedDistance(p, WorldMapLayout.Playable);
                float inside = 1 - WorldMapLayout.SmoothStep(-8, -1, dPlay);
                float dWater = WorldMapLayout.WaterEdgeDistancePublic(p);
                var (surface, kind) = WorldMapLayout.WaterSurfaceAt(p);
                float ground = WorldMapLayout.HeightAt(x, z, out _);
                bool wet = !float.IsNegativeInfinity(surface) && ground < surface - 0.02f;
                float reach = 0.5f + 0.5f * WorldMapLayout.Fbm(x / 14f, z / 14f + 19f, 2);   // grass reaches the water on some stretches
                float landFade = wet ? 0 : WorldMapLayout.SmoothStep(0.15f + 0.9f * reach, 1.2f + 1.8f * reach, dWater);
                // islands are dressed by Finishing.Islands; terrain trees stay off them
                float treeFade = wet || WorldMapLayout.IslandAt(p) >= 0 ? 0 : WorldMapLayout.SmoothStep(5f, 12f, dWater);
                // the meadow runs to the foot rocks: no forest or foothill trees in it or within 10-22 m of its rim
                float openMeadow = 1 - WorldMapLayout.SmoothStep(10f, 22f, WorldMapLayout.SignedDistance(p, WorldMapLayout.MeadowArea));
                maps["Forest"][k] = w.x * inside * treeFade * (1 - openMeadow);
                maps["Meadow"][k] = w.y * inside * landFade;
                maps["MeadowTrees"][k] = maps["Meadow"][k] * WorldMapLayout.SmoothStep(32f, 44f, -dPlay);   // meadow trees keep 32-44 m off the foot rocks
                maps["Highlands"][k] = w.z * inside * treeFade;
                maps["Swamp"][k] = w.w * inside;
                // none where the meadow runs up to the edge, so the near range shows from the meadow (concept)
                maps["Foothills"][k] = WorldMapLayout.SmoothStep(-48, -30, dPlay) * (1 - WorldMapLayout.SmoothStep(-7, -2, dPlay)) * treeFade * (1 - w.w) * (1 - openMeadow);
                // wild grass runs on 1-30 m past the playable edge, so the lawn doesn't end at the invisible wall and the foot rocks
                // stand in grass ("Wild" feeds detail stamps only)
                float verge = WorldMapLayout.SmoothStep(-6f, -1f, dPlay) * (1 - WorldMapLayout.SmoothStep(20f, 30f, dPlay)) * landFade * (1 - w.w);
                maps["Wild"][k] = Mathf.Max(inside * landFade * (1 - w.y) * (1 - w.w * 0.7f), verge);
                float depth = float.IsNegativeInfinity(surface) ? -9 : surface - ground;
                // lakes, ponds, swamp, the rivers (not by the bridge), and the slow margin stream and swamp outlet
                bool reedWater = kind == 0 || (kind == 1 && Vector2.Distance(p, WorldMapLayout.Bridge) > 12f) || (kind == 2 && x > 556 && z > 530 && z < 745);
                maps["Reeds"][k] = reedWater ? WorldMapLayout.SmoothStep(-0.35f, -0.05f, depth) * (1 - WorldMapLayout.SmoothStep(0.5f, 0.85f, depth)) * inside * (1 - WorldMapLayout.SandWeight(p)) : 0;
                maps["WaterEdge"][k] = wet ? 0 : WorldMapLayout.SmoothStep(0.3f, 1.2f, dWater) * (1 - WorldMapLayout.SmoothStep(4f, 7f, dWater)) * inside;

                // meadow flowers keep off the yard and the trail edges, so those stay plain lawn
                float trail = WorldMapLayout.TrailDistancePublic(p);
                // trail verges: tufts in clumps where the bare core frays out, pebbles on the core and just past it
                float hw = WorldMapLayout.TrailHalfWidth(x, z), onLand = wet ? 0 : WorldMapLayout.SmoothStep(0.5f, 2f, dWater) * inside;
                maps["TrailEdge"][k] = WorldMapLayout.SmoothStep(hw - 0.1f, hw + 0.4f, trail) * (1 - WorldMapLayout.SmoothStep(hw + 1.0f, hw + 1.8f, trail))
                    * WorldMapLayout.SmoothStep(-0.1f, 0.15f, WorldMapLayout.Fbm(x / 3f, z / 3f + 67f, 2)) * onLand
                    * WorldMapLayout.SmoothStep(28f, 34f, Vector2.Distance(p, WorldMapLayout.HomeSpawn));   // mown lawn and bridge sight line by the spawn
                maps["TrailPebbles"][k] = (1 - WorldMapLayout.SmoothStep(hw + 0.1f, hw + 1.0f, trail)) * WorldMapLayout.SmoothStep(0.1f, 0.3f, WorldMapLayout.Fbm(x / 6f, z / 6f + 29f, 2))
                    * onLand * WorldMapLayout.SmoothStep(16f, 20f, Vector2.Distance(p, WorldMapLayout.Bridge));   // none under the bridge deck ends
                float fd = WorldMapLayout.FenceDistance(p);
                // view lanes from the spawn: the gate->bridge sight line and the east wedge (river mouth, lake band, far range)
                var toP = p - WorldMapLayout.HomeSpawn; float dS = toP.magnitude;
                float brg = (Mathf.Atan2(toP.x, toP.y) * Mathf.Rad2Deg + 360f) % 360f;
                float bridgeLane = (1 - WorldMapLayout.SmoothStep(4f, 8f, Seg(p, WorldMapLayout.HomeSpawn, WorldMapLayout.Bridge))) * WorldMapLayout.SmoothStep(6f, 9f, dS);
                float eastLane = WorldMapLayout.SmoothStep(74f, 80f, brg) * (1 - WorldMapLayout.SmoothStep(96f, 102f, brg)) * WorldMapLayout.SmoothStep(8f, 12f, dS) * (1 - WorldMapLayout.SmoothStep(70f, 85f, dS));
                float lane = Mathf.Max(bridgeLane, eastLane);
                // the line to the bridge deck clears the flat pad by only 0.4-0.6 m at 15-22 m: mown until the ground falls away
                float mown = bridgeLane * (1 - WorldMapLayout.SmoothStep(28f, 34f, dS));
                float yardIn = 1 - WorldMapLayout.SmoothStep(-0.4f, 0.2f, fd);
                float trailFade = WorldMapLayout.SmoothStep(1.6f, 3.0f, trail);
                float gateClear = WorldMapLayout.SmoothStep(3.5f, 5.5f, Vector2.Distance(p, WorldMapLayout.HomeGate));
                float meadow = maps["Meadow"][k];
                float shortLawn = Mathf.Max(yardIn, mown);
                maps["Yard"][k] = meadow * shortLawn;
                maps["MeadowLawn"][k] = meadow * (1 - shortLawn);
                // knee-high clumps: 10-25 m drifts, a ragged band along both sides of the fence (not in the gate), rings at the home trees' feet
                float blobs = WorldMapLayout.SmoothStep(0.17f, 0.33f, WorldMapLayout.Fbm(x / 20f, z / 20f + 71f, 2)) * (1 - yardIn);
                float fence = (fd < 0 ? 1 - WorldMapLayout.SmoothStep(0.5f, 1.1f, -fd) : 1 - WorldMapLayout.SmoothStep(1.5f, 3.5f, fd))
                    * WorldMapLayout.SmoothStep(-0.15f, 0.05f, WorldMapLayout.Fbm(x / 6f, z / 6f + 7f, 2));
                float feet = 0;
                foreach (var ht in homeTrees) { float dt = Vector2.Distance(p, ht); feet = Mathf.Max(feet, WorldMapLayout.SmoothStep(0.8f, 1.5f, dt) * (1 - WorldMapLayout.SmoothStep(4f, 6.5f, dt))); }
                feet *= WorldMapLayout.SmoothStep(-0.2f, 0.1f, WorldMapLayout.Fbm(x / 4f, z / 4f + 3f, 2)) * (1 - yardIn);
                maps["MeadowTall"][k] = meadow * Mathf.Max(blobs, Mathf.Max(fence, feet)) * trailFade * (1 - lane) * gateClear;
                // dark leafy patches 2-6 m on about 10 % of the lawn (yard included: clover patches)
                maps["MeadowCover"][k] = meadow * WorldMapLayout.SmoothStep(0.25f, 0.42f, WorldMapLayout.Fbm(x / 7f, z / 7f + 91f, 2)) * WorldMapLayout.SmoothStep(0.9f, 1.6f, trail);
                // upright spikes: drifts laid across the sight lines from the spawn, plus a low sprinkle beyond 50-70 m
                float spikes = 0;
                for (int s = 0; s < WorldMapLayout.SpikeDrifts.Length; s++)
                {
                    var dr = WorldMapLayout.SpikeDrifts[s];
                    float a0 = dr.brg * Mathf.Deg2Rad, ang = (dr.brg + 90f) * Mathf.Deg2Rad;
                    var c0 = WorldMapLayout.HomeSpawn + dr.dist * new Vector2(Mathf.Sin(a0), Mathf.Cos(a0));
                    var ax = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)); var q = p - c0;
                    float u = Vector2.Dot(q, ax) / dr.len, v = (q.x * ax.y - q.y * ax.x) / dr.wid;
                    spikes = Mathf.Max(spikes, 1 - WorldMapLayout.SmoothStep(0.65f, 1.15f, Mathf.Sqrt(u * u + v * v) + 0.3f * WorldMapLayout.Fbm(x / 5f, z / 5f + 13f * s, 2)));
                }
                float far = 0.4f * WorldMapLayout.SmoothStep(0.22f, 0.40f, WorldMapLayout.Fbm(x / 35f, z / 35f + 23f, 2)) * WorldMapLayout.SmoothStep(50f, 70f, dS);
                maps["MeadowSpikes"][k] = meadow * (1 - yardIn) * Mathf.Max(spikes, far) * trailFade * (1 - lane) * gateClear;
                // a wide, ragged round fade: a square cut makes flower drifts run parallel to the fence like a planted border
                float yardFade = WorldMapLayout.SmoothStep(22f, 38f, Vector2.Distance(p, home) + 6f * WorldMapLayout.Fbm(x / 12f, z / 12f + 33f, 2));
                float near = WorldMapLayout.SmoothStep(1f, 9f, fd + 4f * WorldMapLayout.Fbm(x / 9f, z / 9f + 41f, 2));
                maps["MeadowFlowers"][k] = maps["Meadow"][k] * Mathf.Max(yardFade, near * (1 - eastLane)) * (1 - bridgeLane) * (1 - yardIn) * trailFade;
                // hedgerows: the meadow's outer rim (against the southern foot) and both sides of the viewpoint path, with gaps
                float edgeBand = WorldMapLayout.SmoothStep(0.12f, 0.3f, w.y) * (1 - WorldMapLayout.SmoothStep(0.45f, 0.65f, w.y));
                float pathBand = WorldMapLayout.SmoothStep(2.6f, 3.2f, trail) * (1 - WorldMapLayout.SmoothStep(5.0f, 5.8f, trail)) * w.y;
                float gaps = WorldMapLayout.SmoothStep(-0.05f, 0.05f, WorldMapLayout.Fbm(x / 22f, z / 22f + 51f, 2));
                maps["Hedge"][k] = Mathf.Max(edgeBand * (z < 300 ? 1 : 0.3f), pathBand * (z < 262 ? 1 : 0)) * gaps * inside * landFade * WorldMapLayout.SmoothStep(3f, 9f, fd);

                // tree belt outside the playable edge (behind the foot rocks, in front of the ridges)
                float rim = WorldMapLayout.SmoothStep(4f, 10f, dPlay) * (1 - WorldMapLayout.SmoothStep(150f, 175f, dPlay)) * treeFade;
                float a = Mathf.Atan2(z - centre.y, x - centre.x);
                float wS = Mathf.Max(0, -Mathf.Sin(a)), wE = Mathf.Max(0, Mathf.Cos(a)), wN = Mathf.Max(0, Mathf.Sin(a));
                float broad = Mathf.Clamp01((0.25f * wS * wS + 0.35f * wE * wE + (x > 450 ? 0.6f : 0.1f) * wN * wN) / Mathf.Max(0.3f, wS * wS + wE * wE + wN * wN + Mathf.Max(0, -Mathf.Cos(a)) * Mathf.Max(0, -Mathf.Cos(a)))
                    + 0.2f * WorldMapLayout.Fbm(x / 60f, z / 60f + 9f, 2));
                // the first 30-55 m of the belt stays short so the near range's rock rises over it (and behind the home the
                // ridges and peaks show)
                float shortTrees = Mathf.Max(1 - WorldMapLayout.SmoothStep(30f, 55f, dPlay), (1 - WorldMapLayout.SmoothStep(80f, 140f, Vector2.Distance(p, home))) * (1 - WorldMapLayout.SmoothStep(35f, 70f, dPlay)));
                maps["Rim"][k] = rim * (1 - broad) * (1 - shortTrees);
                maps["RimBroadleaf"][k] = rim * broad * (1 - shortTrees);
                maps["RimShort"][k] = rim * shortTrees;
                maps["RimBushes"][k] = WorldMapLayout.SmoothStep(2f, 5f, dPlay) * (1 - WorldMapLayout.SmoothStep(30f, 45f, dPlay)) * treeFade;

                // lake and pond shores (and islands), after the vendor demos (scratchpad research5/lake_shore_islands.md):
                // a lip of lush grass and clover 0.3-3 m from the water, bushes in clumps and flowers from 3 m, trees from 7 m.
                // Coves (land on most of a 15 m disc around the point) get the most bushes, trees and reeds, points the least.
                float dBasin = WorldMapLayout.BasinEdgeDistance(p);
                float beach = WorldMapLayout.SandWeight(p);
                float cove = 0.5f;
                bool lakeWater = wet && kind == 0 && WorldMapLayout.SignedDistance(p, WorldMapLayout.Basins[0].shore) < 0;
                if ((!wet && dBasin < 28f) || lakeWater)
                {
                    int land = 0;
                    for (int a12 = 0; a12 < 12; a12++) { float ang = a12 * Mathf.PI / 6; if (WorldMapLayout.BasinEdgeDistance(p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 15f) > 0) land++; }
                    cove = WorldMapLayout.SmoothStep(0.35f, 0.7f, land / 12f);
                }
                float shoreBase = wet ? 0 : inside * (1 - beach) * (1 - w.w);
                maps["LakeLip"][k] = shoreBase * WorldMapLayout.SmoothStep(0.2f, 0.8f, dBasin) * (1 - WorldMapLayout.SmoothStep(2.5f, 3.5f, dBasin));
                maps["LakeShore"][k] = shoreBase * WorldMapLayout.SmoothStep(2f, 3.5f, dBasin) * (1 - WorldMapLayout.SmoothStep(9f, 13f, dBasin)) * Mathf.Lerp(0.15f, 1f, cove);
                maps["LakeFlowers"][k] = shoreBase * WorldMapLayout.SmoothStep(2.5f, 3.5f, dBasin) * (1 - WorldMapLayout.SmoothStep(9f, 12f, dBasin));
                maps["LakeTrees"][k] = WorldMapLayout.IslandAt(p) >= 0 ? 0 : shoreBase * WorldMapLayout.SmoothStep(7f, 10f, dBasin) * (1 - WorldMapLayout.SmoothStep(22f, 28f, dBasin)) * (1 - 0.7f * w.y) * (0.6f + 0.4f * cove);
                // reeds on the lake only in coves (open green banks stay clear, as in ref 7 and the Raygeas demos)
                if (lakeWater) maps["Reeds"][k] *= WorldMapLayout.SmoothStep(0.3f, 0.6f, cove);
            }
        }));
        foreach (var kv in maps) WorldMapLayout.WriteMask(Maps + "Veg_" + kv.Key + ".png", kv.Value, res);
        return new { seconds = (DateTime.Now - t0).TotalSeconds, names };
    }

    // ---- clear masks from the scene: rock footprints, yard, campfire, bridge, trails ----
    public static async Task<object> GenerateClearMasks(int res)
    {
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().First(g => g.name == "Environment");
        var rocks = new List<Vector3>();   // x, z, radius
        foreach (var path in new[] { "Rock Outcrops", "Landmarks/Waterfall", "Boundary/Foot Rocks", "Boundary/Near Range", "Boundary/Outlet Gorge", "Dressing/Meadow Stones", "Dressing/Bank Rocks", "Dressing/Water Edges", "Dressing/Home Pond" })
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
        var trees = new float[res * res]; var details = new float[res * res];
        await Task.Run(() => Parallel.For(0, res, j =>
        {
            for (int i = 0; i < res; i++)
            {
                float x = (i + 0.5f) / res * WorldMapLayout.Size, z = (j + 0.5f) / res * WorldMapLayout.Size;
                var p = new Vector2(x, z);
                float ct = 0, cd = 0;
                foreach (var r in rocks)
                {
                    float d = Vector2.Distance(p, new Vector2(r.x, r.y));
                    ct = Mathf.Max(ct, 1 - WorldMapLayout.SmoothStep(r.z + 1.5f, r.z + 3.5f, d));
                    cd = Mathf.Max(cd, 1 - WorldMapLayout.SmoothStep(r.z * 0.7f, r.z * 0.85f, d));
                }
                foreach (var r in props) cd = Mathf.Max(cd, 1 - WorldMapLayout.SmoothStep(r.z * 0.8f, r.z + 0.3f, Vector2.Distance(p, new Vector2(r.x, r.y))));
                // fenced yard: no trees within 7-13 m of the fence; trimmed lawn handled by the meadow masks
                ct = Mathf.Max(ct, 1 - WorldMapLayout.SmoothStep(7f, 13f, WorldMapLayout.FenceDistance(p)));
                // no tree crowns on the spawn->bridge sight line
                ct = Mathf.Max(ct, (1 - WorldMapLayout.SmoothStep(8f, 11f, Seg(p, WorldMapLayout.HomeSpawn, WorldMapLayout.Bridge))) * WorldMapLayout.SmoothStep(8f, 10f, Vector2.Distance(p, WorldMapLayout.HomeSpawn)));
                if (bridgeB.HasValue)
                {
                    var b = bridgeB.Value;
                    float dx = Mathf.Max(0, Mathf.Abs(x - b.center.x) - b.extents.x), dz = Mathf.Max(0, Mathf.Abs(z - b.center.z) - b.extents.z);
                    float db = Mathf.Sqrt(dx * dx + dz * dz);
                    ct = Mathf.Max(ct, 1 - WorldMapLayout.SmoothStep(6f, 9f, db));
                    cd = Mathf.Max(cd, 1 - WorldMapLayout.SmoothStep(0.2f, 1.2f, db));
                }
                float tr = WorldMapLayout.TrailDistancePublic(p), hw = WorldMapLayout.TrailHalfWidth(x, z);
                ct = Mathf.Max(ct, 1 - WorldMapLayout.SmoothStep(3.0f, 4.5f, tr));
                cd = Mathf.Max(cd, 1 - WorldMapLayout.SmoothStep(hw - 0.35f, hw + 0.05f, tr));   // blades stop where the frayed dirt core does
                trees[j * res + i] = ct; details[j * res + i] = cd;
            }
        }));
        WorldMapLayout.WriteMask(Maps + "Veg_ClearTrees.png", trees, res);
        WorldMapLayout.WriteMask(Maps + "Veg_ClearDetails.png", details, res);
        return new { rocks = rocks.Count, props = props.Count, bridge = bridgeB.HasValue };
    }

    static float Seg(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); return Vector2.Distance(p, a + ab * t); }

    // ---- prefab variants: capsule colliders on the tall firs (terrain trees ignore MeshColliders) ----
    public static object MakeVariants()
    {
        WorldMapLayout.EnsureFolderPublic(Folder + "/Prefabs");
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
        WorldMapLayout.EnsureFolderPublic(Folder + "/Details");
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

    // Plain detail prefab (vendor mesh) with a _Game copy of its material, re-tuned on every run; vendor files stay untouched.
    static string TintedDetail(string vendorPrefab, string name, Action<Material> tune, bool shadows)
    {
        string matDir = Folder + "/Materials", detDir = Folder + "/Details";
        WorldMapLayout.EnsureFolderPublic(matDir); WorldMapLayout.EnsureFolderPublic(detDir);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(vendorPrefab) ?? throw new ArgumentException("Missing " + vendorPrefab);
        var r = src.GetComponentInChildren<MeshRenderer>();
        string matPath = matDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(r.sharedMaterial); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.CopyPropertiesFromMaterial(r.sharedMaterial);
        tune(mat); EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
        string dest = detDir + "/" + name + "_Detail.prefab";
        var go = new GameObject(name + "_Detail");
        go.AddComponent<MeshFilter>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        PrefabUtility.SaveAsPrefabAsset(go, dest);
        UnityEngine.Object.DestroyImmediate(go);
        return dest;
    }

    // Same mean as BK's _Grass.mat (no tone step where the details end), with a wider sunlit/cool spread in 10-15 m blobs.
    static void MeadowGrass(Material m) { m.SetColor("_Color01", new Color(0.58f, 0.72f, 0.30f)); m.SetColor("_Color02", new Color(0.27f, 0.51f, 0.18f)); m.SetFloat("_NoiseTiling", 0.04f); }
    static void TallGrass(Material m) { m.SetColor("_Color01", new Color(0.66f, 0.73f, 0.33f)); m.SetColor("_Color02", new Color(0.46f, 0.60f, 0.22f)); m.SetFloat("_NoiseTiling", 0.04f); }

    // Detail draw distance and the matching BK fade distance (keep bk = 2x detail).
    public static object DrawDistances(float detail, float bk)
    {
        var t = Terrain.activeTerrain ?? throw new InvalidOperationException("No terrain.");
        Undo.RecordObject(t, "Detail distance");
        t.detailObjectDistance = detail;   // QualitySettings overrides are off, so the per-terrain value is used
        t.detailObjectDensity = 1f;
        EditorUtility.SetDirty(t);
        var type = Type.GetType("Aipocalypse.Core.EnvironmentSystem.BKFoliageGlobals, Aipocalypse.Core") ?? throw new InvalidOperationException("BKFoliageGlobals not compiled");
        var g = UnityEngine.Object.FindAnyObjectByType(type) ?? throw new InvalidOperationException("Run FoliageGlobals first");
        var so = new SerializedObject(g);
        so.FindProperty("_renderDistance").floatValue = bk;
        so.ApplyModifiedProperties();      // OnValidate re-applies the shader global
        EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
        return new { t.detailObjectDistance, bk, global = Shader.GetGlobalFloat("GrassRenderDist") };
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
        Texture2D M(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Maps + "Veg_" + n + ".png") ?? throw new ArgumentException("Missing mask " + n);
        string FT(int i) => Folder + "/Prefabs/Fir_Tall_" + i + "_Capsule.prefab";
        var c = new Vector2(500, 500);
        uint seed = 100;
        string L1 = TintedDetail(BKP + "Grass1.prefab", "BK_Grass1_Meadow", MeadowGrass, false);
        string L2 = TintedDetail(BKP + "Grass2.prefab", "BK_Grass2_Meadow", MeadowGrass, false);
        string G3 = TintedDetail(BKP + "Grass3.prefab", "BK_Grass3_Tall", TallGrass, true);
        string SO = TintedDetail(BKP + "Sorrel.prefab", "BK_Sorrel_Dark", m => { m.SetColor("_Color01", new Color(0.30f, 0.52f, 0.17f)); m.SetColor("_Color02", new Color(0.25f, 0.45f, 0.16f)); }, true);
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
            new[] { 22f, 22f, 20f, 12f, 12f, 12f }, new Vector2(0.85f, 1.15f), 2.2f, "MeadowTrees");
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
        // lake and pond shores (ref: Raygeas key art): broadleafs and pines leaning over the water in loose groups
        var shoreTrees = T(trees, "Shore Trees", new[] { GV + "Broadleaf_Medium_1.prefab", GV + "Broadleaf_Medium_2.prefab", GV + "Broadleaf_Large_1.prefab", GV + "Birch_Medium_1.prefab", KN + "PineTree_4.prefab", KN + "PineTree_5.prefab" },
            new[] { 24f, 24f, 10f, 12f, 15f, 15f }, new Vector2(0.85f, 1.2f), 2.8f, "LakeTrees");
        Veg.Slope(shoreTrees.filterSet, 0, 40, 0, 6); Veg.Noise(shoreTrees.filterSet.weightNoise, 2, 16, 3f, 0.1f, 0.6f);
        var sdead = T(trees, "Swamp Dead Trees", new[] { GV + "Birch_Dead_1.prefab", GV + "Birch_Dead_2.prefab", GV + "Broadleaf_Dead_1.prefab", GV + "Broadleaf_Dead_2.prefab" }, new[] { 27f, 27f, 23f, 23f }, new Vector2(0.7f, 1.1f), 2.4f, "Swamp");
        Veg.Height(sdead.filterSet, WorldMapLayout.SwampWater - 0.6f, 26.5f, 0.2f, 1f);
        var sliving = T(trees, "Swamp Living Trees", new[] { GV + "Birch_Small_1.prefab", GV + "Birch_Small_2.prefab", GV + "Broadleaf_Small_2.prefab", KN + "PineTree_1.prefab" }, new[] { 30f, 30f, 20f, 20f }, new Vector2(0.65f, 0.95f), 1.7f, "Swamp");
        Veg.Height(sliving.filterSet, WorldMapLayout.SwampWater + 0.6f, 40f, 0.4f, 1f);

        // bushes
        var mb = T(bushes, "Meadow Flowering Bushes", new[] { KN + "Bush_1_Flowers.prefab", KN + "Bush_2_Flowers.prefab", KN + "Bush_3_Flowers.prefab", KN + "Bush_4_Flowers.prefab", KN + "Bush_1.prefab", KN + "Bush_2.prefab" },
            new[] { 18f, 18f, 17f, 17f, 15f, 15f }, new Vector2(0.85f, 1.15f), 4.5f, "Meadow");
        Veg.Slope(mb.filterSet, 0, 35, 0, 6); Veg.Noise(mb.filterSet.weightNoise, 2, 35, 6f, 0.2f);   // clumps of 2-4, not a sprinkle
        var hedge = T(bushes, "Meadow Hedgerows", new[] { GV + "Bush_Large.prefab", GV + "Bush_Medium.prefab", KN + "Bush_1.prefab", KN + "Bush_2.prefab" }, new[] { 35f, 30f, 18f, 17f }, new Vector2(0.75f, 1.1f), 8f, "Hedge");
        Veg.Slope(hedge.filterSet, 0, 35, 0, 6);
        var shoreBu = T(bushes, "Shore Bushes", new[] { GV + "Bush_Large.prefab", GV + "Bush_Medium.prefab", KN + "Bush_1.prefab", KN + "Bush_2.prefab", KN + "Bush_1_Flowers.prefab", KN + "Bush_3_Flowers.prefab" },
            new[] { 30f, 30f, 12f, 12f, 8f, 8f }, new Vector2(0.8f, 1.15f), 14f, "LakeShore");   // clumps of 3-5 with crowns touching
        Veg.Slope(shoreBu.filterSet, 0, 40, 0, 6); Veg.Noise(shoreBu.filterSet.weightNoise, 2, 16, 8f, 0.16f, 1.3f);
        var rimBu = T(bushes, "Rim Bushes", new[] { KN + "Bush_1.prefab", KN + "Bush_2.prefab", KN + "Bush_3.prefab", GV + "Bush_Large.prefab", GV + "Bush_Medium.prefab" }, new[] { 22f, 22f, 18f, 20f, 18f }, new Vector2(0.8f, 1.2f), 3.0f, "RimBushes");
        Veg.Noise(rimBu.filterSet.weightNoise, 2, 30, 3f, 0.2f);
        var fb = T(bushes, "Forest Understory", new[] { KN + "Bush_1.prefab", KN + "Bush_2.prefab", KN + "Bush_3.prefab", KN + "Bush_4.prefab", KN + "Bush_2_Flowers.prefab" }, new[] { 24f, 24f, 20f, 17f, 15f }, new Vector2(0.7f, 1.1f), 3.0f, "Forest");
        Veg.Slope(fb.filterSet, 0, 42, 0, 6); Veg.Noise(fb.filterSet.weightNoise, 2, 30, 3f, 0.2f);
        var footb = T(bushes, "Foothill Bushes", new[] { KN + "Bush_1.prefab", KN + "Bush_2.prefab", KN + "Bush_2_Flowers.prefab", KN + "Bush_3.prefab" }, new[] { 25f, 25f, 25f, 25f }, new Vector2(0.8f, 1.2f), 3.0f, "Foothills");
        Veg.Noise(footb.filterSet.weightNoise, 2, 30, 3f, 0.25f);
        var hb = T(bushes, "Highland Bushes", new[] { HF + "Bush_1.prefab", HF + "Bush_2.prefab", HF + "Bush_3.prefab", HF + "Dead_Bush_1.prefab", HF + "Dead_Bush_2.prefab" }, new[] { 22f, 22f, 22f, 17f, 17f }, new Vector2(0.6f, 1.0f), 2.6f, "Highlands");
        Veg.Slope(hb.filterSet, 0, 42, 0, 6);
        var sb = T(bushes, "Swamp Bushes", new[] { KN + "Bush_1.prefab", KN + "Bush_2.prefab", KN + "Bush_3.prefab" }, new[] { 35f, 35f, 30f }, new Vector2(0.6f, 1.0f), 2.2f, "Swamp");
        Veg.Height(sb.filterSet, WorldMapLayout.SwampWater + 0.3f, 40f, 0.3f, 1f);

        // ground cover (one prototype per stamp). Komorebi demo values: Grass_1 density 1.7, w 1.0-1.2, h 0.9-1.2.
        var grassLayers = new[] { "Terrain_Grass", "Terrain_GrassCool", "Terrain_GrassMeadow" };
        // Meadow after the BK Pure Nature Meadows demo: a short bright lawn, flowers in hard-edged drifts of one to three
        // species with plain lawn between (high noise amplitude = crisp drifts), barley tufts, tall grass only at tree feet.
        void Proto(DetailStamp s, float align, float jitter, float spread) { s.prototype.alignToGround = align; s.prototype.positionJitter = jitter; s.prototype.noiseSpread = spread; }
        // lawn tips 0.4-0.8 m outside the fence so blades hide the ground at 20 m (eye 1.7 m); a mown lawn in the yard and the bridge lane
        var lawnA = D("Meadow Lawn A", L1, new Vector2(1.2f, 2.0f), new Vector2(1.6f, 2.8f), 2.0f, "MeadowLawn"); TexFilter(lawnA.filterSet, grassLayers); Proto(lawnA, 1, 0.3f, 2.5f);
        var lawnB = D("Meadow Lawn B", L2, new Vector2(1.2f, 2.0f), new Vector2(1.6f, 2.6f), 1.8f, "MeadowLawn"); TexFilter(lawnB.filterSet, grassLayers); Proto(lawnB, 1, 0.3f, 1.8f);
        var yardA = D("Yard Lawn A", L1, new Vector2(1.0f, 1.5f), new Vector2(1.2f, 1.8f), 2.2f, "Yard"); TexFilter(yardA.filterSet, grassLayers); Proto(yardA, 1, 0.3f, 2.5f);
        var yardB = D("Yard Lawn B", L2, new Vector2(1.0f, 1.5f), new Vector2(1.1f, 1.6f), 1.8f, "Yard"); TexFilter(yardB.filterSet, grassLayers); Proto(yardB, 1, 0.3f, 1.8f);
        var mgl = D("Meadow Tall Grass", G3, new Vector2(0.7f, 1.1f), new Vector2(0.8f, 1.2f), 1.6f, "Meadow"); TexFilter(mgl.filterSet, grassLayers); Proto(mgl, 0.6f, 0.6f, 0.9f);
        mgl.maxDistanceFromTree = 7f; mgl.filterSet.weight = 0.7f; Veg.Noise(mgl.filterSet.weightNoise, 2, 30, 6f, 0.10f);
        // heads speckle the lawn in loose patches (BK foreground), fields and lavender form large drifts, gorse adds yellow accents
        var daisy = D("Meadow Daisies", BKP + "Daisy.prefab", new Vector2(1.0f, 1.6f), new Vector2(2.5f, 5.0f), 1.5f, "MeadowFlowers"); TexFilter(daisy.filterSet, grassLayers); Proto(daisy, 1, 0.7f, 1.6f);
        daisy.minDistanceFromTree = 4f; Veg.Noise(daisy.filterSet.weightNoise, 2, 30, 6f, 0.12f, 0f);
        var blue = D("Meadow Blue Heads", BKP + "CommonBlue.prefab", new Vector2(1.0f, 1.6f), new Vector2(2.5f, 5.0f), 1.2f, "MeadowFlowers"); TexFilter(blue.filterSet, grassLayers); Proto(blue, 1, 0.7f, 1.0f);
        blue.minDistanceFromTree = 4f; Veg.Noise(blue.filterSet.weightNoise, 2, 30, 6f, 0.12f, 0.15f);
        var pink = D("Meadow Pink Heads", BKP + "CommonPink.prefab", new Vector2(1.0f, 1.6f), new Vector2(2.0f, 4.0f), 1.0f, "MeadowFlowers"); TexFilter(pink.filterSet, grassLayers); Proto(pink, 1, 0.7f, 0.5f);
        pink.minDistanceFromTree = 4f; Veg.Noise(pink.filterSet.weightNoise, 2, 30, 6f, 0.14f, 0.45f);
        var orange = D("Meadow Orange Field", BKP + "FlowerMeadow1.prefab", new Vector2(0.7f, 1.1f), new Vector2(0.7f, 1.1f), 1.0f, "MeadowFlowers"); TexFilter(orange.filterSet, grassLayers); Proto(orange, 0.5f, 0.7f, 0.4f);
        Veg.Slope(orange.filterSet, 0, 20, 0, 4); Veg.Noise(orange.filterSet.weightNoise, 2, 20, 10f, 0.17f, 2f);
        var blueField = D("Meadow Blue Field", BKP + "FlowerMeadow3.prefab", new Vector2(0.7f, 1.1f), new Vector2(0.7f, 1.1f), 0.8f, "MeadowFlowers"); TexFilter(blueField.filterSet, grassLayers); Proto(blueField, 0.5f, 0.7f, 0.55f);
        Veg.Slope(blueField.filterSet, 0, 20, 0, 4); Veg.Noise(blueField.filterSet.weightNoise, 2, 20, 10f, 0.17f, 6.5f);   // off the orange field's cells
        var lavender = D("Meadow Lavender", BKP + "Lavender.prefab", new Vector2(0.8f, 1.3f), new Vector2(0.8f, 1.3f), 2.0f, "MeadowFlowers"); TexFilter(lavender.filterSet, grassLayers); Proto(lavender, 0, 0.7f, 0.6f);
        lavender.minDistanceFromTree = 5f; Veg.Slope(lavender.filterSet, 0, 27, 0, 6); Veg.Noise(lavender.filterSet.weightNoise, 2, 16, 8f, 0.12f, 5f);
        var gorse = D("Meadow Gorse", BKP + "Gorse1.prefab", new Vector2(0.8f, 1.2f), new Vector2(0.8f, 1.2f), 0.8f, "MeadowFlowers"); TexFilter(gorse.filterSet, grassLayers); Proto(gorse, 0, 0.7f, 2.4f);
        gorse.minDistanceFromTree = 5f; Veg.Noise(gorse.filterSet.weightNoise, 2, 45, 10f, 0.2f, 3f);
        var barley = D("Meadow Barley Tufts", BKP + "GrassMeadows3.prefab", new Vector2(0.7f, 1.0f), new Vector2(0.8f, 1.15f), 0.8f, "MeadowLawn"); TexFilter(barley.filterSet, grassLayers); Proto(barley, 0, 0.7f, 0.5f);
        barley.filterSet.weight = 0.7f; Veg.Noise(barley.filterSet.weightNoise, 2, 90, 12f, 0.21f, 7f);
        // knee-high unmown clumps, dark leafy patches and upright spikes (violet salvia, lupins, white wild carrot) in drifts
        var tallClumps = D("Meadow Tall Clumps", G3, new Vector2(0.6f, 1.0f), new Vector2(0.6f, 1.0f), 2.0f, "MeadowTall"); TexFilter(tallClumps.filterSet, grassLayers); Proto(tallClumps, 0.6f, 0.6f, 0.9f);
        var sorrel = D("Meadow Sorrel", SO, new Vector2(1.0f, 2.0f), new Vector2(2.0f, 3.5f), 2.5f, "MeadowCover"); TexFilter(sorrel.filterSet, grassLayers); Proto(sorrel, 1, 0.5f, 0.6f);
        var salvia = D("Meadow Violet Spikes", KN + "Salvia_Group_1.prefab", new Vector2(0.6f, 1.0f), new Vector2(0.6f, 0.85f), 1.2f, "MeadowSpikes"); TexFilter(salvia.filterSet, grassLayers); Proto(salvia, 0.3f, 0.8f, 1.0f);
        Veg.Slope(salvia.filterSet, 0, 40, 0, 6);
        var lupins = D("Meadow Lupins", BKP + "Lupin2.prefab", new Vector2(0.7f, 1.1f), new Vector2(0.8f, 1.15f), 0.5f, "MeadowSpikes"); TexFilter(lupins.filterSet, grassLayers); Proto(lupins, 0, 0.8f, 0.6f);
        Veg.Noise(lupins.filterSet.weightNoise, 2, 60, 4f, 0f, 4f);
        var carrot = D("Meadow Wild Carrot", BKP + "Carot1.prefab", new Vector2(0.6f, 0.9f), new Vector2(0.7f, 1.0f), 0.45f, "MeadowSpikes"); TexFilter(carrot.filterSet, grassLayers); Proto(carrot, 0.3f, 0.8f, 0.8f);
        Veg.Noise(carrot.filterSet.weightNoise, 2, 60, 4f, 0f, 8f);
        var wg = D("Wild Grass", KN + "Grass_1.prefab", new Vector2(0.9f, 1.15f), new Vector2(0.75f, 1.05f), 1.3f, "Wild"); TexFilter(wg.filterSet, grassLayers);
        Veg.Slope(wg.filterSet, 0, 46, 0, 6);
        var wt = D("Wild Tufts", DetailPrefab(KN + "Grass_1_Large.prefab"), new Vector2(0.9f, 1.2f), new Vector2(0.9f, 1.2f), 0.7f, "Wild"); TexFilter(wt.filterSet, grassLayers);
        Veg.Noise(wt.filterSet.weightNoise, 2, 35, 2.5f, 0.15f);
        var pathTufts = D("Trail Edge Tufts", DetailPrefab(KN + "Grass_1_Large.prefab"), new Vector2(0.6f, 0.85f), new Vector2(0.9f, 1.3f), 1.1f, "TrailEdge"); TexFilter(pathTufts.filterSet, grassLayers); Proto(pathTufts, 0.3f, 0.8f, 0.6f);
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
        // lush shore grass and orange flowers down to the water (ref 7)
        var shoreGrass = D("Shore Grass", KN + "Grass_1.prefab", new Vector2(1.0f, 1.3f), new Vector2(1.0f, 1.35f), 2.0f, "LakeLip"); TexFilter(shoreGrass.filterSet, grassLayers);
        var shoreClover = D("Shore Clovers", KN + "Clovers_1.prefab", new Vector2(0.9f, 1.2f), new Vector2(0.9f, 1.2f), 1.0f, "LakeLip");
        Veg.Noise(shoreClover.filterSet.weightNoise, 2, 30, 3f, 0.15f, 2.2f);
        var shoreFl = D("Shore Flowers", BKP + "FlowerMeadow1.prefab", new Vector2(0.7f, 1.1f), new Vector2(0.7f, 1.1f), 0.9f, "LakeFlowers"); TexFilter(shoreFl.filterSet, grassLayers);
        Proto(shoreFl, 0.5f, 0.7f, 0.5f); Veg.Noise(shoreFl.filterSet.weightNoise, 2, 18, 8f, 0.16f, 9f);
        var sc = D("Highland Scree", HF + "Stones.prefab", new Vector2(0.5f, 0.9f), new Vector2(0.5f, 0.9f), 0.35f, "Highlands");
        Veg.Noise(sc.filterSet.weightNoise, 2, 40, 3f, 0.35f);   // scattered patches, not a carpet
        TexFilter(sc.filterSet, new[] { "Terrain_Pebbles", "Terrain_Rock", "Terrain_Dirt" });
        var sm = D("Swamp Marsh Grass", DetailPrefab(KN + "Grass_1_Large.prefab"), new Vector2(0.9f, 1.3f), new Vector2(1.0f, 1.4f), 1.2f, "Swamp");
        Veg.Height(sm.filterSet, WorldMapLayout.SwampWater + 0.05f, 30f, 0.15f, 1f);

        // clearing: removes spawns of the stamps above
        var ct = Clear(clear, "Clear Trees", M("ClearTrees"), true, false);
        var cd = Clear(clear, "Clear Details", M("ClearDetails"), false, true);
        // pebbles lie on the bare core, which Clear Details empties: only clear stamps after a detail stamp clear it
        var trailDressing = Veg.Group("Vegetation/Trail Dressing");
        foreach (var (label, path, dens) in new[] { ("Trail Pebbles A", KR + "SmallStone_5.prefab", 0.15f), ("Trail Pebbles B", KR + "SmallStone_6.prefab", 0.1f) })
        {
            var pb = Veg.Detail(trailDressing, label, path, new Vector2(0.28f, 0.4f), new Vector2(0.25f, 0.35f), 1f, dens, c, 1000);
            Veg.Mask(pb.filterSet, M("TrailPebbles"));
            TexFilter(pb.filterSet, new[] { "Terrain_Dirt", "Terrain_WetMud", "Terrain_Pebbles" });
            Proto(pb, 1f, 0.8f, 0.5f);
        }

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

    // Lake islands as four archetypes (scratchpad research5/lake_shore_islands.md, from the Komorebi, Grasslands and AZURE
    // demos), picked by size and height:
    //   wooded isle (land radius >= 16 m): a hero broadleaf, a loose ring of broadleafs and pines with the side facing the
    //     meadow left open, bush clumps, a rocky point, a stone ring with one bare beach arc, a driftwood point, lily arcs;
    //   pine knoll (top > 1.3 m): a big pine, a pine at the shore and a Sakura, bush clumps, big rocks standing in the water
    //     on one side, a continuous stone ring;
    //   flower islet (top 1.0-1.3 m): one Sakura at the centre, flowering bushes, Farfugium, a dense stone ring;
    //   sand bar (top <= 1.0 m): no trees, a log half in the water, rocks off the shore, a ring broken by a beach arc.
    // Grass and shore flowers come from the vegetation masks; terrain trees stay off islands.
    public static object Islands(int seed)
    {
        const string GV = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Vegetation/";
        const string GLR = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Rocks/";
        const string HLF = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/";
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
        int n = 0;
        GameObject Put(Transform parent, string path, Vector3 pos, Quaternion rot, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * scale;
            n++;
            return go;
        }
        Quaternion Upright() => Quaternion.Euler(0, R(0, 360), 0);
        Quaternion Tilted(float a) => Quaternion.Euler(R(-a, a), R(0, 360), R(-a, a));
        Vector2 Dir(float ang) => new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        float AngleDiff(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
        var view = WorldMapLayout.HomeBase;
        var homePond = WorldMapLayout.Basins.First(b => b.name == "WaterfallPond").shore;

        for (int ii = 0; ii < WorldMapLayout.Islands.Length; ii++)
        {
            float level = WorldMapLayout.IslandLevel(ii);
            var isl = WorldMapLayout.Islands[ii];
            if (WorldMapLayout.SignedDistance(new Vector2(isl.x, isl.y), homePond) < 0) continue;   // the home pond's islands: Dressing.BuildHomePond
            var c = new Vector2(isl.x, isl.y);
            float rw = WorldMapLayout.IslandWaterline(ii);
            char kind = rw >= 16f ? 'A' : isl.z > 1.3f ? 'B' : isl.z > 1.0f ? 'C' : 'D';
            float amount = kind == 'A' && rw < 20f ? 0.65f : 1f;
            var g = new GameObject($"Island {ii} ({(kind == 'A' ? "wooded" : kind == 'B' ? "pine knoll" : kind == 'C' ? "flower islet" : "sand bar")})").transform;
            g.SetParent(root, false);
            var placed = new List<Vector3>();
            bool Free(Vector2 p, float r) => placed.All(q => (new Vector2(q.x, q.y) - p).sqrMagnitude > (q.z + r) * (q.z + r));
            bool Land(Vector2 p, float above) => H(p) > level + above;
            float viewAng = Mathf.Atan2(view.y - c.y, view.x - c.x);       // the side people see it from
            float a0 = viewAng + Mathf.PI + R(-0.8f, 0.8f);                 // rocky point / beach directions are random around the far side
            float beachAng = a0 + Mathf.PI + R(-0.6f, 0.6f);
            bool Place(string path, Vector2 p, float scale, float keep, float sink = 0.15f, Quaternion? rot = null)
            {
                if (!Free(p, keep)) return false;
                Put(g, path, new Vector3(p.x, H(p) - sink, p.y), rot ?? Upright(), scale);
                placed.Add(new Vector3(p.x, p.y, keep));
                return true;
            }
            bool Ring(string path, float rMin, float rMax, float scale, float above, float keep, float avoidAng = float.NaN, float avoidWidth = 0, float sink = 0.15f)
            {
                for (int t = 0; t < 50; t++)
                {
                    float ang = R(0, 2 * Mathf.PI);
                    if (!float.IsNaN(avoidAng) && AngleDiff(ang, avoidAng) < avoidWidth) continue;
                    var p = c + Dir(ang) * rw * R(rMin, rMax);
                    if (Land(p, above) && Place(path, p, scale, keep, sink)) return true;
                }
                return false;
            }
            void Clump(string[] paths, float[] weights, int count, float rMin, float rMax, float above)
            {
                for (int t = 0; t < 40; t++)
                {
                    var cc = c + Dir(R(0, 2 * Mathf.PI)) * rw * R(rMin, rMax);
                    if (!Land(cc, above)) continue;
                    for (int k = 0, t2 = 0; k < count && t2 < 40; t2++)
                    {
                        var p = cc + new Vector2(R(-2.5f, 2.5f), R(-2.5f, 2.5f));
                        if (!Land(p, above * 0.5f)) continue;
                        float w = R(0, weights.Sum()); int pick = 0; for (; pick < weights.Length - 1 && w > weights[pick]; pick++) w -= weights[pick];
                        if (Place(paths[pick], p, R(0.85f, 1.1f), 0.75f)) k++;
                    }
                    return;
                }
            }
            string[] bushes = { GV + "Bush_Large.prefab", GV + "Bush_Medium.prefab", KN + "Nature/Bush_1.prefab", KN + "Nature/Bush_2.prefab", KN + "Nature/Bush_3.prefab" };
            float[] bushW = { 30, 30, 13, 13, 14 };
            string[] flowering = { KN + "Nature/Bush_1_Flowers.prefab", KN + "Nature/Bush_3_Flowers.prefab" };
            float[] flowW = { 1, 1 };

            if (kind == 'A')
            {
                var hero = c + Dir(viewAng + Mathf.PI) * rw * R(0.15f, 0.3f);
                Place(GV + (rng.Next(2) == 0 ? "Broadleaf_Large_1.prefab" : "Broadleaf_Large_2.prefab"), hero, R(1.1f, 1.25f), 5f, 0.2f);
                int ringTrees = Mathf.RoundToInt(R(5, 8) * amount);
                for (int k = 0; k < ringTrees; k++)
                {
                    int r = rng.Next(10);
                    string path = r < 4 ? GV + "Broadleaf_Medium_" + (1 + rng.Next(2)) + ".prefab" : r < 9 ? KN + "Nature/PineTree_" + (3 + rng.Next(3)) + ".prefab" : (k == 0 ? KN + "Nature/Sakura_" + (1 + rng.Next(2)) + ".prefab" : GV + "Birch_Medium_1.prefab");
                    Ring(path, 0.5f, 0.85f, path.Contains("Pine") ? R(1.0f, 1.25f) : R(0.9f, 1.15f), 0.8f, 3.5f, viewAng, Mathf.PI / 3f, 0.2f);
                }
                int clumps = Mathf.RoundToInt(R(5, 7) * amount);
                for (int k = 0; k < clumps; k++) Clump(k == 0 ? flowering : bushes, k == 0 ? flowW : bushW, 3 + rng.Next(4), 0.35f, 0.8f, 0.4f);
                for (int k = 0; k < 8 * amount; k++) Ring(KN + "Nature/Fern_" + (1 + rng.Next(3)) + ".prefab", 0.4f, 0.85f, R(0.9f, 1.2f), 0.25f, 0.9f, sink: 0.1f);
                for (int k = 0; k < 4 * amount; k++) Ring(KN + "Nature/Farfugium_" + (1 + rng.Next(3)) + ".prefab", 0.45f, 0.85f, R(0.9f, 1.1f), 0.25f, 0.9f, sink: 0.1f);
            }
            else if (kind == 'B')
            {
                Place(KN + "Nature/PineTree_4.prefab", c + Dir(R(0, 2 * Mathf.PI)) * rw * R(0f, 0.3f), R(1.2f, 1.3f), 4f, 0.2f);
                var shoreAng = R(0, 2 * Mathf.PI);
                Place(KN + "Nature/PineTree_3.prefab", c + Dir(shoreAng) * Mathf.Max(0f, rw - 4f), 1.0f, 3f, 0.2f);
                Place(KN + "Nature/Sakura_2.prefab", c + Dir(shoreAng + Mathf.PI) * Mathf.Max(0f, rw - 5f), 0.95f, 3f, 0.2f);
                for (int k = 0; k < 3; k++) Clump(k == 0 ? flowering : bushes, k == 0 ? flowW : bushW, 3 + rng.Next(2), 0.3f, 0.75f, 0.4f);
                for (int k = 0; k < 6; k++) Ring(KN + "Nature/Fern_" + (1 + rng.Next(3)) + ".prefab", 0.3f, 0.9f, R(0.9f, 1.1f), 0.2f, 0.9f, sink: 0.1f);
            }
            else if (kind == 'C')
            {
                Place(KN + "Nature/Sakura_" + (rng.Next(2) == 0 ? 3 : 5) + ".prefab", c + new Vector2(R(-1, 1), R(-1, 1)), 1.0f, 3f, 0.2f);
                for (int k = 0; k < 3; k++) Ring(flowering[rng.Next(2)], 0.45f, 0.8f, R(0.95f, 1.1f), 0.2f, 1.1f);
                Clump(bushes, bushW, 3 + rng.Next(3), 0.3f, 0.6f, 0.3f);
                for (int k = 0; k < 12; k++) Ring(KN + "Nature/Farfugium_" + (1 + rng.Next(3)) + ".prefab", 0.2f, 0.85f, R(0.9f, 1.1f), 0.12f, 0.7f, sink: 0.08f);
            }
            else
            {
                // a log half in the water on the beach side, a bush or two at most
                var logAt = c + Dir(beachAng) * rw * R(0.85f, 1.05f);
                Put(g, HLF + "Log_" + (1 + rng.Next(2)) + ".prefab", new Vector3(logAt.x, Mathf.Max(H(logAt), level - 0.25f) - 0.1f, logAt.y), Quaternion.Euler(R(-3, 3), R(0, 360), R(-3, 3)), R(1.0f, 1.3f));
                placed.Add(new Vector3(logAt.x, logAt.y, 2.5f));
                Ring(KN + "Nature/Bush_" + (1 + rng.Next(2)) + ".prefab", 0.1f, 0.5f, R(0.7f, 0.9f), 0.25f, 1.2f);
                for (int k = 0; k < 5; k++)
                {
                    float ang = a0 + R(-1.2f, 1.2f);
                    var p = c + Dir(ang) * (rw + R(2f, 5f));
                    float depth = level - H(p);
                    if (depth < 0.3f || depth > 2.2f || !Free(p, 1.5f)) continue;
                    var (path, minY, maxY) = new[] { (GLR + "Stone_2.prefab", -0.13f, 0.99f), (GLR + "Stone_7.prefab", -0.58f, 1.58f), (GLR + "Stone_9.prefab", -0.94f, 1.70f) }[rng.Next(3)];
                    float sc = R(1.1f, 1.5f);
                    Put(g, path, new Vector3(p.x, Mathf.Min(level + R(-1f, 0.6f) - maxY * sc, H(p) - minY * sc - 0.1f), p.y), Tilted(10), sc);
                    placed.Add(new Vector3(p.x, p.y, 1.2f * sc));
                }
            }

            // rocky point (wooded isle and pine knoll): grass-topped rocks standing in 0.3-1.5 m of water on one side
            if (kind == 'A' || kind == 'B')
                for (int k = 0, t = 0; k < (kind == 'B' ? 4 : 3) && t < 40; t++)
                {
                    var p = c + Dir(a0 + R(-0.35f, 0.35f)) * (rw + R(-1.5f, 2.5f));
                    float depth = level - H(p);
                    if (depth > 1.5f || !Free(p, 2.2f)) continue;
                    bool gl = kind == 'B' && rng.Next(3) == 0;
                    float sc = gl ? R(1.5f, 1.8f) : R(0.9f, 1.4f);
                    string path = gl ? GLR + "Stone_9.prefab" : KN + "Rocks Grass/Stone_" + new[] { 2, 5, 6, 8 }[rng.Next(4)] + ".prefab";
                    Put(g, path, new Vector3(p.x, Mathf.Max(H(p), level - 1.2f) - R(0.4f, 0.8f) * sc, p.y), Tilted(10), sc);
                    placed.Add(new Vector3(p.x, p.y, 2f * sc)); k++;
                }

            // stone ring at the waterline: wooded 5-6 per 10 m, knoll/islet/bar 8-11; the beach arc (wooded, bar) and the
            // rocky point are left out
            float spacing = kind == 'A' ? 1.8f : 1.1f;
            float beachWidth = kind == 'A' ? Mathf.PI * 0.25f : kind == 'D' ? 18f / Mathf.Max(1f, rw) * 0.5f : 0f;
            float circ = 2 * Mathf.PI * rw;
            for (float s = 0; s < circ; s += R(spacing * 0.7f, spacing * 1.3f))
            {
                float ang = s / rw;
                if (beachWidth > 0 && AngleDiff(ang, beachAng) < beachWidth) continue;
                if ((kind == 'A' || kind == 'B') && AngleDiff(ang, a0) < 0.4f) continue;
                var dir = Dir(ang);
                Vector2 wl = c + dir * rw;
                for (float d = rw * 0.5f; d < rw + 6f; d += 0.25f) if (H(c + dir * d) < level) { wl = c + dir * d; break; }
                var p = wl + dir * R(-1.0f, 2.5f);
                float sc = R(1.5f, 2.8f);
                if (!Free(p, 0.3f * sc)) continue;
                Put(g, KN + "Rocks/SmallStone_" + (1 + rng.Next(3)) + ".prefab", new Vector3(p.x, H(p) - R(0.15f, 0.35f), p.y), Tilted(20), sc);
                placed.Add(new Vector3(p.x, p.y, 0.3f * sc));
            }

            // driftwood point on the wooded isle's beach arc: a log and branches, half in the water
            if (kind == 'A')
            {
                var dp = c + Dir(beachAng) * rw;
                Put(g, HLF + "Log_" + (1 + rng.Next(2)) + ".prefab", new Vector3(dp.x, Mathf.Max(H(dp), level - 0.2f) - 0.1f, dp.y), Quaternion.Euler(R(-3, 3), R(0, 360), R(-3, 3)), R(0.9f, 1.2f));
                for (int k = 0; k < 4; k++)
                {
                    var b = dp + Dir(beachAng + R(-0.4f, 0.4f)) * R(-2f, 6f) + new Vector2(R(-2, 2), R(-2, 2));
                    Put(g, GV + new[] { "Branch_1.prefab", "Big_Branch_1.prefab", "Big_Branch_2.prefab" }[rng.Next(3)], new Vector3(b.x, Mathf.Max(H(b) - 0.05f, level - 0.15f), b.y), Quaternion.Euler(R(-4, 4), R(0, 360), R(-4, 4)), R(0.8f, 1.1f));
                }
            }

            // lily arcs on the lee side (toward the beach), 3-6 m out in 0.8-2.4 m of water
            int arcs = kind == 'A' ? 3 : kind == 'D' ? 1 : 2, perArc = kind == 'A' ? 7 : kind == 'B' ? 6 : 5;
            for (int a = 0, t = 0; a < arcs && t < 60; t++)
            {
                float ang = beachAng + R(-1.2f, 1.2f);
                int made = 0;
                for (int k = 0; k < perArc * 3 && made < perArc; k++)
                {
                    var q = c + Dir(ang + R(-0.25f, 0.25f)) * (rw + R(3f, 6f));
                    float dq = level - H(q);
                    if (dq < 0.8f || dq > 2.4f || !Free(q, 1.25f)) continue;
                    string path = kind == 'D' ? GV + "Lotus_Group_" + (1 + rng.Next(2)) + ".prefab" : KN + "Nature/WaterLily_Group_" + (rng.NextDouble() < 0.55 ? 2 : 3) + ".prefab";
                    Put(g, path, new Vector3(q.x, level + 0.028f, q.y), Upright(), R(1.0f, 1.15f));
                    placed.Add(new Vector3(q.x, q.y, 1.25f)); made++;
                }
                if (made > 0) a++;
            }
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return new { objects = n };
    }

    // Player starts inside the gate, looking through it toward the bridge.
    public static object PlayerSpawn()
    {
        var player = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        if (player == null) return "no Player root";
        var terrain = Terrain.activeTerrain;
        var s = WorldMapLayout.HomeSpawn; var d = WorldMapLayout.Bridge - s;
        var at = new Vector3(s.x, 0, s.y);
        at.y = terrain.SampleHeight(at) + 0.1f;
        Undo.RecordObject(player.transform, "Player spawn");
        player.transform.SetPositionAndRotation(at, Quaternion.Euler(0, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0));
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

// Enemy navigation: one tiled recast graph over the whole map, baked into a startup cache so play never scans. Water is
// kept out by a navmesh-only volume: the scan skips the underwater areas (they are triggers) and would otherwise walk the
// beds. Run BuildWaterVolume after the water or terrain changes and Bake after anything that adds, moves or removes
// colliders (dressing, landmarks, boundary, trees): the cache is a snapshot.
public static class Navigation
{
    const string NavigationPrefab = "Assets/_Game/_Core/EnemySystem/Prefabs/Navigation.prefab";
    const string Folder = WorldMapLayout.MapFolder + "/Navigation";
    const string CachePath = Folder + "/WorldMapNavmesh.bytes";
    const float VolumeDepth = 30f;

    // Columns from VolumeDepth under the surface up to it wherever the ground lies under water (the dressing masks' wet
    // test), merged into runs along X. The renderer is disabled: A* only needs it to find the mesh (forceRenderingOff still
    // drew it magenta, it has no material). With wadeStreams
    // the streams (1-1.3 m deep, crossed on foot like the player) stay walkable: blocking them cuts the land between them
    // off from the rest of the map.
    public static async Task<object> BuildWaterVolume(float cell, bool wadeStreams)
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) throw new InvalidOperationException("No active terrain.");
        var data = terrain.terrainData;
        int hr = data.heightmapResolution;
        float[,] h = data.GetHeights(0, 0, hr, hr);
        Vector3 size = data.size, origin = terrain.transform.position;
        float Ground(float x, float z)
        {
            float u = Mathf.Clamp01((x - origin.x) / size.x) * (hr - 1), v = Mathf.Clamp01((z - origin.z) / size.z) * (hr - 1);
            int i = Mathf.Min((int)u, hr - 2), j = Mathf.Min((int)v, hr - 2);
            float fu = u - i, fv = v - j;
            float a = Mathf.Lerp(h[j, i], h[j, i + 1], fu), b = Mathf.Lerp(h[j + 1, i], h[j + 1, i + 1], fu);
            return origin.y + Mathf.Lerp(a, b, fv) * size.y;
        }

        WorldMapLayout.WaterSurfaceAt(Vector2.zero);   // builds the lazy basin bounds and line points before the threads read them
        float maxLevel = WorldMapLayout.Basins.Max(b => b.level);
        maxLevel = Mathf.Max(maxLevel, WorldMapLayout.SwampWater);
        foreach (var l in WorldMapLayout.Lines) maxLevel = Mathf.Max(maxLevel, l.pts.Max(p => p.z));

        int n = Mathf.CeilToInt(WorldMapLayout.Size / cell);
        var surface = new float[n * n];
        await Task.Run(() => Parallel.For(0, n, j =>
        {
            for (int i = 0; i < n; i++)
            {
                float x = (i + 0.5f) * cell, z = (j + 0.5f) * cell;
                float ground = Ground(x, z);
                float s = float.NegativeInfinity;
                if (ground < maxLevel)
                {
                    var (level, kind) = WorldMapLayout.WaterSurfaceAt(new Vector2(x, z));
                    if (!float.IsNegativeInfinity(level) && ground < level - 0.02f && !(wadeStreams && kind == 2)) s = level;
                }
                surface[j * n + i] = s;
            }
        }));

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        void Quad(float x0, float x1, float z0, float z1, float y)
        {
            int b = vertices.Count;
            vertices.Add(new Vector3(x0, y, z0)); vertices.Add(new Vector3(x0, y, z1)); vertices.Add(new Vector3(x1, y, z1)); vertices.Add(new Vector3(x1, y, z0));
            triangles.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        int runs = 0, wet = 0;
        for (int j = 0; j < n; j++)
        {
            int i = 0;
            while (i < n)
            {
                float s = surface[j * n + i];
                if (float.IsNegativeInfinity(s)) { i++; continue; }
                int start = i;
                while (i < n && !float.IsNegativeInfinity(surface[j * n + i]) && Mathf.Abs(surface[j * n + i] - s) < 0.05f) i++;
                wet += i - start;
                float x0 = start * cell, x1 = i * cell, z0 = j * cell, z1 = (j + 1) * cell;
                Quad(x0, x1, z0, z1, s);
                Quad(x0, x1, z0, z1, s - VolumeDepth);
                runs++;
            }
        }

        var mesh = new Mesh { name = "NavmeshWater", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        WorldMapLayout.EnsureFolderPublic(Folder);
        string meshPath = Folder + "/NavmeshWater.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
        else
        {
            existing.Clear();
            existing.indexFormat = IndexFormat.UInt32;
            existing.SetVertices(vertices);
            existing.SetTriangles(triangles, 0);
            existing.RecalculateBounds();
            mesh = existing;
        }
        AssetDatabase.SaveAssetIfDirty(mesh);

        var group = WorldMapLayout.WaterGroupPublic("Navmesh");
        var go = new GameObject("Navmesh Water", typeof(MeshFilter), typeof(MeshRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Navigation");
        go.transform.SetParent(group, false);
        go.layer = LayerMask.NameToLayer("Water");
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.enabled = false;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var modifier = go.AddComponent<Pathfinding.RecastNavmeshModifier>();
        modifier.includeInScan = Pathfinding.RecastNavmeshModifier.ScanInclusion.AlwaysInclude;
        modifier.geometrySource = Pathfinding.RecastNavmeshModifier.GeometrySource.MeshFilter;
        modifier.mode = Pathfinding.RecastNavmeshModifier.Mode.UnwalkableSurface;
        modifier.solid = true;
        modifier.dynamic = false;
        EditorSceneManager.MarkSceneDirty(go.scene);
        return new { cell, wadeStreams, wetCells = wet, wetArea = wet * cell * cell, runs, vertices = vertices.Count, maxLevel };
    }

    // Scans the scene's Navigation (the prefab instance, added if missing) as one recast graph and writes the startup cache.
    public static object Bake(float cellSize)
    {
        if (UnityEngine.Object.FindObjectsByType<Pathfinding.RecastNavmeshModifier>().All(m => m.name != "Navmesh Water"))
            throw new InvalidOperationException("No Navmesh Water; run Navigation.BuildWaterVolume first.");
        var terrain = Terrain.activeTerrain;
        var scene = SceneManager.GetActiveScene();
        var astar = UnityEngine.Object.FindObjectsByType<AstarPath>(FindObjectsInactive.Include).SingleOrDefault();
        if (astar == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NavigationPrefab), scene);
            Undo.RegisterCreatedObjectUndo(go, "Navigation");
            astar = go.GetComponent<AstarPath>();
        }

        astar.data.DeserializeGraphs();
        foreach (var old in astar.data.graphs.Where(g => g != null).ToArray()) astar.data.RemoveGraph(old);
        var graph = (Pathfinding.RecastGraph)astar.data.AddGraph(typeof(Pathfinding.RecastGraph));
        graph.name = "WorldMap";
        Vector3 size = terrain.terrainData.size;
        graph.forcedBoundsCenter = terrain.transform.position + new Vector3(size.x, WorldMapLayout.MaxHeight, size.z) * 0.5f;
        graph.forcedBoundsSize = new Vector3(size.x, WorldMapLayout.MaxHeight + 20f, size.z);
        graph.cellSize = cellSize;
        graph.useTiles = true;
        graph.editorTileSize = 128;
        graph.characterRadius = 0.5f;   // the enemies' FollowerEntity radius
        graph.walkableHeight = 2f;
        graph.walkableClimb = 0.4f;
        graph.maxSlope = 45f;           // the player's slope limit
        graph.collectionSettings.layerMask = 1 << LayerMask.NameToLayer("Default");
        graph.collectionSettings.rasterizeTerrain = true;
        graph.collectionSettings.rasterizeTrees = true;
        graph.collectionSettings.rasterizeColliders = true;
        graph.collectionSettings.rasterizeMeshes = false;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        astar.Scan();
        watch.Stop();

        int nodes = 0, walkable = 0;
        var areas = new Dictionary<uint, int>();
        graph.GetNodes(node =>
        {
            nodes++;
            if (!node.Walkable) return;
            walkable++;
            areas[node.Area] = areas.TryGetValue(node.Area, out int c) ? c + 1 : 1;
        });
        var spawn = WorldMapLayout.HomeSpawn;
        var home = astar.GetNearest(new Vector3(spawn.x, terrain.SampleHeight(new Vector3(spawn.x, 0, spawn.y)), spawn.y), Pathfinding.NearestNodeConstraint.Walkable);
        int homeArea = home.node != null ? areas[home.node.Area] : 0;

        astar.data.SetData(astar.data.SerializeGraphs());
        WorldMapLayout.EnsureFolderPublic(Folder);
        File.WriteAllBytes(CachePath, astar.data.SerializeGraphs(new Pathfinding.Serialization.SerializeSettings { nodes = true }));
        AssetDatabase.ImportAsset(CachePath);
        astar.data.file_cachedStartup = AssetDatabase.LoadAssetAtPath<TextAsset>(CachePath);
        astar.data.cacheStartup = true;
        EditorUtility.SetDirty(astar);
        PrefabUtility.RecordPrefabInstancePropertyModifications(astar);
        EditorSceneManager.MarkSceneDirty(scene);

        var big = areas.Values.OrderByDescending(c => c).Take(5).ToArray();
        return new
        {
            scanSeconds = Math.Round(watch.Elapsed.TotalSeconds, 2),
            tiles = $"{graph.tileXCount}x{graph.tileZCount}",
            nodes, walkable,
            areas = areas.Count,
            largestAreas = big,
            homeAreaShare = Math.Round(homeArea / (double)Math.Max(1, walkable), 3),
            cacheKB = new FileInfo(CachePath).Length / 1024,
        };
    }
}
