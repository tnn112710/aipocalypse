using System.Collections.Generic;
using UnityEngine;
using Graph = ArmoryCoreEditor.ArmoryCoreGraph;

namespace ArmoryCoreEditor
{
    /// <summary>
    /// Drawn icons of the setup tiles, muzzle to the right. Actions are side views of the typical weapon two heights wide;
    /// feeds and fire modes are square pictures of the rounds.
    /// </summary>
    internal static class ArmoryCoreMechanismIcons
    {
        // ---------- Actions: side views of the typical weapon, the worked part brighter ----------

        /// <summary>Assault rifle: stock, receiver, curved magazine and handguard.</summary>
        public static void ClosedBolt(Rect r, Color color)
        {
            var body = Graph.Shade(color, 0.8f);
            Graph.Fill(body, P(r, 0f, 0.3f), P(r, 0.2f, 0.36f), P(r, 0.2f, 0.56f), P(r, 0f, 0.72f));
            Graph.Fill(body, P(r, 0.32f, 0.5f), P(r, 0.41f, 0.5f), P(r, 0.37f, 0.8f), P(r, 0.28f, 0.8f));
            Graph.Fill(body, P(r, 0.47f, 0.5f), P(r, 0.57f, 0.5f), P(r, 0.63f, 0.84f), P(r, 0.53f, 0.86f));
            Graph.Fill(body, Box(r, 0.58f, 0.34f, 0.86f, 0.48f));
            Graph.Fill(body, Box(r, 0.86f, 0.38f, 1f, 0.44f));
            Graph.Fill(color, Box(r, 0.19f, 0.28f, 0.59f, 0.5f));
            Graph.Fill(Graph.Shade(color, 0.45f), Box(r, 0.36f, 0.33f, 0.52f, 0.4f));
        }

        /// <summary>Machine gun on its bipod with a box of belted rounds under the receiver.</summary>
        public static void OpenBolt(Rect r, Color color)
        {
            var body = Graph.Shade(color, 0.8f);
            Graph.Fill(body, P(r, 0f, 0.28f), P(r, 0.18f, 0.32f), P(r, 0.18f, 0.52f), P(r, 0f, 0.66f));
            Graph.Fill(body, P(r, 0.24f, 0.46f), P(r, 0.32f, 0.46f), P(r, 0.28f, 0.74f), P(r, 0.2f, 0.74f));
            Graph.Fill(body, Box(r, 0.56f, 0.32f, 1f, 0.39f));
            var width = Mathf.Max(1.5f, r.height * 0.06f);
            Graph.Line(P(r, 0.84f, 0.38f), P(r, 0.78f, 0.9f), body, width);
            Graph.Line(P(r, 0.84f, 0.38f), P(r, 0.92f, 0.9f), body, width);
            Graph.Fill(color, Box(r, 0.17f, 0.24f, 0.57f, 0.46f));
            Graph.Fill(color, Box(r, 0.36f, 0.46f, 0.54f, 0.78f));
        }

        /// <summary>Scoped rifle with its bolt handle: the action is worked by hand.</summary>
        public static void Manual(Rect r, Color color)
        {
            var body = Graph.Shade(color, 0.8f);
            Graph.Fill(body, P(r, 0f, 0.4f), P(r, 0.3f, 0.44f), P(r, 0.3f, 0.6f), P(r, 0f, 0.74f));
            Graph.Fill(body, Box(r, 0.28f, 0.42f, 0.8f, 0.55f));
            Graph.Fill(body, Box(r, 0.8f, 0.44f, 1f, 0.5f));
            Graph.Fill(body, Box(r, 0.3f, 0.14f, 0.64f, 0.27f));
            Graph.Fill(body, Box(r, 0.36f, 0.27f, 0.4f, 0.42f));
            Graph.Fill(body, Box(r, 0.54f, 0.27f, 0.58f, 0.42f));
            Graph.Line(P(r, 0.3f, 0.46f), P(r, 0.24f, 0.74f), color, Mathf.Max(2f, r.height * 0.09f));
            Graph.Disc(P(r, 0.235f, 0.78f), r.height * 0.11f, color);
        }

        /// <summary>Pump shotgun: the fore-end under the barrel slides back and forth.</summary>
        public static void Pump(Rect r, Color color)
        {
            var body = Graph.Shade(color, 0.8f);
            Graph.Fill(body, P(r, 0f, 0.3f), P(r, 0.2f, 0.34f), P(r, 0.2f, 0.52f), P(r, 0f, 0.68f));
            Graph.Fill(body, Box(r, 0.19f, 0.28f, 0.44f, 0.5f));
            Graph.Fill(body, Box(r, 0.44f, 0.28f, 1f, 0.36f));
            Graph.Fill(body, Box(r, 0.44f, 0.38f, 0.92f, 0.46f));
            Graph.Fill(color, Box(r, 0.52f, 0.34f, 0.78f, 0.54f));
            Arrow(r, color, P(r, 0.64f, 0.78f), P(r, 0.4f, 0.78f));
            Arrow(r, color, P(r, 0.66f, 0.78f), P(r, 0.9f, 0.78f));
        }

        /// <summary>Double-barrelled shotgun broken open on its hinge.</summary>
        public static void BreakAction(Rect r, Color color)
        {
            var body = Graph.Shade(color, 0.8f);
            Graph.Fill(body, P(r, 0f, 0.26f), P(r, 0.3f, 0.32f), P(r, 0.3f, 0.5f), P(r, 0f, 0.7f));
            Graph.Fill(body, Box(r, 0.28f, 0.3f, 0.42f, 0.56f));
            var breech = new Vector2(0.42f, 0.36f);
            var angle = 18f * Mathf.Deg2Rad;
            // The rect is twice as wide as it is tall: in units of the rect a slope looks twice as steep.
            var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 2f).normalized;
            var across = new Vector2(-along.y * 0.5f, along.x) * 0.2f;
            var muzzle = breech + along * 0.58f;
            Graph.Fill(color, P(r, breech), P(r, muzzle), P(r, muzzle + across), P(r, breech + across));
            Graph.Disc(P(r, 0.42f, 0.56f), r.height * 0.07f, Graph.Shade(color, 0.5f));
        }

        // ---------- Feed ----------

        /// <summary>Receiver with the box magazine dropping out of it.</summary>
        public static void DetachableMagazine(Rect rect, Color color)
        {
            var r = Square(rect);
            Graph.Fill(Graph.Shade(color, 0.75f), Box(r, 0.02f, 0.18f, 0.98f, 0.34f));
            Graph.Fill(color, P(r, 0.3f, 0.44f), P(r, 0.56f, 0.44f), P(r, 0.62f, 0.9f), P(r, 0.38f, 0.9f));
            Round(r, Graph.Shade(color, 0.55f), 0.33f, 0.5f, 0.08f);
            Arrow(r, color, P(r, 0.8f, 0.44f), P(r, 0.8f, 0.9f));
        }

        /// <summary>A fixed magazine well with rounds stacked inside and the next round going in from above.</summary>
        public static void InternalMagazine(Rect rect, Color color)
        {
            var r = Square(rect);
            var dim = Graph.Shade(color, 0.75f);
            var width = Width(r);
            Graph.Line(P(r, 0.16f, 0.42f), P(r, 0.16f, 0.94f), dim, width);
            Graph.Line(P(r, 0.66f, 0.42f), P(r, 0.66f, 0.94f), dim, width);
            Graph.Line(P(r, 0.16f, 0.94f), P(r, 0.66f, 0.94f), dim, width);
            Round(r, Graph.Shade(color, 0.7f), 0.22f, 0.62f, 0.16f);
            Round(r, Graph.Shade(color, 0.7f), 0.22f, 0.82f, 0.16f);
            Round(r, color, 0.22f, 0.14f, 0.16f);
            Arrow(r, color, P(r, 0.84f, 0.06f), P(r, 0.84f, 0.5f));
        }

        /// <summary>One round going straight into the open chamber.</summary>
        public static void SingleRound(Rect rect, Color color)
        {
            var r = Square(rect);
            Barrel(r, color, 0.58f);
            Round(r, color, 0.02f, 0.5f, 0.2f);
            Arrow(r, color, P(r, 0.4f, 0.5f), P(r, 0.56f, 0.5f));
        }

        /// <summary>Rounds standing side by side, held together by the links of a belt.</summary>
        public static void Belt(Rect rect, Color color)
        {
            var r = Square(rect);
            Graph.Fill(Graph.Shade(color, 0.6f), Box(r, 0.02f, 0.58f, 0.98f, 0.7f));
            for (var i = 0; i < 4; i++)
                Upright(r, color, 0.06f + i * 0.24f, 0.86f, 0.16f);
        }

        // ---------- Fire modes ----------

        /// <summary>A padlock: the trigger is locked.</summary>
        public static void Safe(Rect rect, Color color)
        {
            var r = Square(rect);
            var shackle = new List<Vector2>();
            for (var i = 0; i <= 12; i++)
            {
                var angle = Mathf.Lerp(180f, 360f, i / 12f) * Mathf.Deg2Rad;
                shackle.Add(P(r, 0.5f + Mathf.Cos(angle) * 0.18f, 0.48f + Mathf.Sin(angle) * 0.22f));
            }
            Graph.Polyline(shackle, color, Width(r) * 1.3f);
            Graph.Fill(color, Box(r, 0.24f, 0.46f, 0.76f, 0.88f));
            Graph.Disc(P(r, 0.5f, 0.62f), r.height * 0.06f, Graph.Shade(color, 0.4f));
            Graph.Fill(Graph.Shade(color, 0.4f), Box(r, 0.48f, 0.62f, 0.52f, 0.76f));
        }

        /// <summary>One round: one shot per pull.</summary>
        public static void Semi(Rect rect, Color color)
        {
            var r = Square(rect);
            Round(r, color, 0.18f, 0.5f, 0.26f);
        }

        /// <summary>Three rounds bracketed together: a fixed burst per pull.</summary>
        public static void Burst(Rect rect, Color color)
        {
            var r = Square(rect);
            for (var i = 0; i < 3; i++)
                Round(r, color, 0.02f + i * 0.33f, 0.44f, 0.14f);
            var width = Width(r) * 0.8f;
            Graph.Line(P(r, 0.03f, 0.7f), P(r, 0.97f, 0.7f), color, width);
            Graph.Line(P(r, 0.03f, 0.6f), P(r, 0.03f, 0.72f), color, width);
            Graph.Line(P(r, 0.97f, 0.6f), P(r, 0.97f, 0.72f), color, width);
        }

        /// <summary>A stream of rounds fading behind speed lines: fires while the trigger is held.</summary>
        public static void Auto(Rect rect, Color color)
        {
            var r = Square(rect);
            for (var i = 0; i < 3; i++)
                Graph.Line(P(r, 0.02f, 0.3f + i * 0.2f), P(r, 0.2f, 0.3f + i * 0.2f), Graph.Shade(color, 0.6f), Width(r) * 0.7f);
            for (var i = 0; i < 4; i++)
                Round(r, Graph.Shade(color, Mathf.Lerp(0.45f, 1f, i / 3f)), 0.24f + i * 0.19f, 0.5f, 0.12f);
        }

        // ---------- Parts ----------

        /// <summary>The icon is drawn in a square so the parts keep their proportions in any tile.</summary>
        private static Rect Square(Rect rect)
        {
            var size = Mathf.Min(rect.width, rect.height);
            return new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
        }

        private static Vector2 P(Rect r, float x, float y) => new Vector2(r.x + r.width * x, r.y + r.height * y);

        private static Vector2 P(Rect r, Vector2 unit) => P(r, unit.x, unit.y);

        private static Rect Box(Rect r, float x0, float y0, float x1, float y1)
        {
            var a = P(r, x0, y0);
            var b = P(r, x1, y1);
            return new Rect(a.x, a.y, b.x - a.x, b.y - a.y);
        }

        private static float Width(Rect r) => Mathf.Max(1.5f, r.height * 0.07f);

        /// <summary>The barrel as an open tube from <paramref name="breech"/> to the right edge, so a chambered round shows inside.</summary>
        private static void Barrel(Rect r, Color color, float breech)
        {
            var dim = Graph.Shade(color, 0.75f);
            var width = Width(r);
            Graph.Line(P(r, breech, 0.37f), P(r, 1f, 0.37f), dim, width);
            Graph.Line(P(r, breech, 0.63f), P(r, 1f, 0.63f), dim, width);
            Graph.Line(P(r, breech, 0.37f), P(r, breech, 0.63f), dim, width);
        }

        /// <summary>A cartridge lying along the bore: case, then the bullet's point.</summary>
        private static void Round(Rect r, Color color, float x, float y, float height = 0.18f)
        {
            var half = height * 0.5f;
            var length = height * 1.5f;
            Graph.Fill(color, Box(r, x, y - half, x + length, y + half));
            Graph.Fill(color, P(r, x + length, y - half), P(r, x + length + height * 0.8f, y), P(r, x + length, y + half));
        }

        /// <summary>A cartridge standing on its base at <paramref name="bottom"/>, bullet up.</summary>
        private static void Upright(Rect r, Color color, float x, float bottom, float width)
        {
            var length = width * 2.4f;
            Graph.Fill(color, Box(r, x, bottom - length, x + width, bottom));
            Graph.Fill(color, P(r, x, bottom - length), P(r, x + width * 0.5f, bottom - length - width * 1.1f), P(r, x + width, bottom - length));
        }

        private static void Arrow(Rect r, Color color, Vector2 from, Vector2 to)
        {
            var direction = (to - from).normalized;
            Graph.Line(from, to - direction * r.height * 0.08f, color, Width(r));
            Head(r, color, from, to);
        }

        private static void Head(Rect r, Color color, Vector2 from, Vector2 to)
        {
            var direction = (to - from).normalized;
            var side = new Vector2(-direction.y, direction.x) * r.height * 0.1f;
            var back = to - direction * r.height * 0.16f;
            Graph.Fill(color, to, back + side, back - side);
        }
    }
}
