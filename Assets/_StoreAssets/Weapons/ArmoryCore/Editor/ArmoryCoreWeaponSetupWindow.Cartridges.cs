using System;
using System.Collections.Generic;
using System.Linq;
using ArmoryCore;
using UnityEditor;
using UnityEngine;
using Graph = ArmoryCoreEditor.ArmoryCoreGraph;

namespace ArmoryCoreEditor
{
    // «Cartridges»: every cartridge of the project is set up here and compared on shared ballistic charts on the right.
    public sealed partial class ArmoryCoreWeaponSetupWindow
    {
        private static readonly Graph.Spec ChartRangeScale = new Graph.Spec(25f, 1500f, "m", 1f, "0", true).Captions("close", "far").Limits(5f, 5000f)
            .With(Graph.M(50f, "pistol"), Graph.M(300f, "rifle"), Graph.M(800f, "sniper"));
        private static readonly Color Others = new Color(0.6f, 0.62f, 0.66f, 0.55f);
        private static readonly Color Copper = new Color(0.85f, 0.52f, 0.30f);
        private static readonly Color Lead = new Color(0.62f, 0.60f, 0.58f);
        private static readonly Color Olive = new Color(0.42f, 0.48f, 0.32f);
        private static Graph.Tile[] templateChips;

        [SerializeField] private int tab;
        [SerializeField] private ArmoryCoreAmmunitionDefinition edited;
        [SerializeField] private float chartRange = 300f;
        private Vector2 cartridgeScroll;
        private Editor ammunitionEditor;
        private readonly Dictionary<ArmoryCoreAmmunitionDefinition, (float range, int version, ArmoryCoreFlight.Sample[] flight)> flights =
            new Dictionary<ArmoryCoreAmmunitionDefinition, (float, int, ArmoryCoreFlight.Sample[])>();

        private static Graph.Tile[] TemplateChips
        {
            get
            {
                if (templateChips != null)
                    return templateChips;
                templateChips = ArmoryCoreCartridgeLibrary.Templates
                    .Select(t => new Graph.Tile(t.File, null, $"{t.Title}: {t.Grams:0.#} g, ⌀ {t.Millimeters:0.##} mm, {t.Velocity:0} m/s.") { Color = Graph.Warn })
                    .ToArray();
                return templateChips;
            }
        }

        private static Graph.Tile CartridgeTile(ArmoryCoreAmmunitionDefinition ammunition)
        {
            return new Graph.Tile(ArmoryCoreCartridgeLibrary.Name(ammunition), ArmoryCoreCartridgeLibrary.Summary(ammunition), AssetDatabase.GetAssetPath(ammunition))
            {
                Color = Graph.Accent,
                Icon = ammunition.IsRocket
                    ? (Action<Rect, Color>)((r, c) => ArmoryCoreAmmunitionDefinitionEditor.DrawRocket(r, c))
                    : (r, c) => ArmoryCoreAmmunitionDefinitionEditor.DrawBullet(r, ammunition.noseShape, 0.4f, c)
            };
        }

        private void OpenCartridge(ArmoryCoreAmmunitionDefinition cartridge)
        {
            edited = cartridge;
            tab = 1;
            GUI.FocusControl(null);
            Repaint();
        }

        private void DrawCartridgesTab()
        {
            var all = ArmoryCoreCartridgeLibrary.All;
            if (!edited)
                edited = definition && definition.ammunition ? definition.ammunition : all.FirstOrDefault();
            EditorGUILayout.LabelField(new GUIContent("Project cartridges", "Pick a cartridge to set it up below; on the right it is compared with the others."), EditorStyles.miniBoldLabel);
            if (all.Count == 0)
                Graph.Hint("The project has no cartridges yet: create the first one from a real cartridge below.");
            var index = all.IndexOf(edited);
            var picked = Graph.Choice(index, all.Select(CartridgeTile).ToList(), 2, 40f);
            if (picked != index && picked >= 0)
            {
                edited = all[picked];
                GUI.FocusControl(null);
            }

            EditorGUILayout.LabelField(new GUIContent("New from a real cartridge", "Typical mass, calibre, speed and bullet shape of a factory load."), EditorStyles.miniBoldLabel);
            var template = Graph.Choice(-1, TemplateChips, 3, 24f);
            if (template >= 0)
                NewCartridge(ArmoryCoreCartridgeLibrary.Templates[template], null);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (ArmoryCoreSetupGUI.Button("Empty cartridge…", true, "A new cartridge with the default values"))
                    NewCartridge(null, null);
                if (ArmoryCoreSetupGUI.Button("Copy of selected…", edited, "A separate cartridge with the same values: the original stays as it is"))
                    NewCartridge(null, edited);
                if (ArmoryCoreSetupGUI.Button("Show", edited, "Ping the asset in the Project window"))
                    EditorGUIUtility.PingObject(edited);
            }
            if (!edited)
                return;
            GUILayout.Space(4f);
            var users = ArmoryCoreCartridgeLibrary.WeaponsUsing(edited);
            Graph.Line(users > 1
                    ? $"Shared asset «{edited.name}»: changes apply at once to all {users} weapon definitions that fire it."
                    : $"Asset «{edited.name}»: changes are saved in the cartridge.",
                users > 1 ? Graph.Warn : Graph.Muted);
            Editor.CreateCachedEditor(edited, typeof(ArmoryCoreAmmunitionDefinitionEditor), ref ammunitionEditor);
            var editor = (ArmoryCoreAmmunitionDefinitionEditor)ammunitionEditor;
            editor.Embedded = true;
            editor.VelocityMultiplier = definition && definition.ammunition == edited ? definition.muzzleVelocityMultiplier : 1f;
            editor.OnInspectorGUI();
        }

        private void NewCartridge(ArmoryCoreCartridgeLibrary.Template template, ArmoryCoreAmmunitionDefinition source)
        {
            var made = ArmoryCoreCartridgeLibrary.Create(template, source, ArmoryCoreCartridgeLibrary.FolderOf(edited));
            if (!made)
                return;
            edited = made;
            SetStatus("Created the cartridge «" + ArmoryCoreCartridgeLibrary.Name(made) + "».");
        }

        private void DrawCartridgeFooter()
        {
            var line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(line, new Color(0f, 0f, 0f, 0.35f));
            GUILayout.Space(3f);
            var loaded = definition ? definition.ammunition : null;
            ArmoryCoreSetupGUI.Issues("weapon.window.cartridge.issues", null, edited && loaded == edited ? "The definition fires this cartridge" : null);
            var label = !definition ? "Pick a definition in the header to load it"
                : loaded == edited ? "The definition already fires this cartridge"
                : "Load «" + ArmoryCoreWeaponSteps.Name(definition) + "» with this cartridge";
            if (ArmoryCoreSetupGUI.PrimaryButton(label, edited && definition && loaded != edited && !EditorApplication.isPlaying, 30f,
                    "Assign the cartridge to the weapon definition. Ctrl+Z undoes."))
            {
                var data = new SerializedObject(definition);
                data.FindProperty("ammunition").objectReferenceValue = edited;
                data.ApplyModifiedProperties();
                SetStatus("«" + ArmoryCoreWeaponSteps.Name(definition) + "» now fires «" + ArmoryCoreCartridgeLibrary.Name(edited) + "».");
            }
            status = ArmoryCoreSetupGUI.Status(status, statusError);
            GUILayout.Space(3f);
        }

        // ---------- Ballistic comparison ----------

        /// <summary>Cartridges of the same kind as the edited one: bullets are compared with bullets, rockets with rockets.</summary>
        private List<ArmoryCoreAmmunitionDefinition> Compared()
        {
            var rockets = edited && edited.IsRocket;
            return ArmoryCoreCartridgeLibrary.All.Where(a => a.IsRocket == rockets).ToList();
        }

        private ArmoryCoreFlight.Sample[] Flight(ArmoryCoreAmmunitionDefinition ammunition)
        {
            var version = EditorUtility.GetDirtyCount(ammunition);
            if (flights.TryGetValue(ammunition, out var cached) && Mathf.Approximately(cached.range, chartRange) && cached.version == version)
                return cached.flight;
            var flight = ArmoryCoreFlight.Simulate(ammunition, ammunition.muzzleVelocityMetersPerSecond, chartRange, 41);
            flights[ammunition] = (chartRange, version, flight);
            return flight;
        }

        private void DrawBallistics()
        {
            EditorGUILayout.LabelField("Ballistics: the selected cartridge and the others", EditorStyles.boldLabel);
            var all = Compared();
            if (all.Count == 0)
            {
                ArmoryCoreSetupGUI.Empty("No cartridges", "Create a cartridge from a real one in the tab on the left.");
                return;
            }
            chartRange = Graph.Scale(new GUIContent("Chart range", "How far the speed, energy and drop curves go."), chartRange, ChartRangeScale);
            var area = GUILayoutUtility.GetRect(200f, 2000f, 300f, 2000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var lineup = Mathf.Clamp(area.height * 0.22f, 90f, 150f);
            var chartHeight = (area.height - lineup - 12f) * 0.5f;
            var energyRect = new Rect(area.x, area.y, area.width, chartHeight - 4f);
            var dropRect = new Rect(area.x, energyRect.yMax + 8f, area.width, chartHeight - 4f);
            var lineupRect = new Rect(area.x, dropRect.yMax + 8f, area.width, area.yMax - dropRect.yMax - 8f);

            var maxEnergy = all.Max(a => a.KineticEnergy(a.muzzleVelocityMetersPerSecond));
            var minEnergy = Mathf.Max(1f, all.Min(a => a.KineticEnergy(Last(Flight(a)).Speed)) * 0.7f);
            Chart(energyRect, all, "Energy, J (log scale)", (a, s) => a.KineticEnergy(s.Speed), minEnergy, maxEnergy * 1.1f, true, v => $"{v:0}");
            var maxDrop = Mathf.Max(0.05f, edited ? Last(Flight(edited)).Drop * 1.4f : all.Max(a => Last(Flight(a)).Drop));
            Chart(dropRect, all, "Drop below the bore line, cm", (a, s) => s.Drop, 0f, maxDrop, false, v => $"{v * 100f:0}", true);
            DrawLineup(lineupRect, all);
        }

        private static ArmoryCoreFlight.Sample Last(ArmoryCoreFlight.Sample[] flight) => flight[flight.Length - 1];

        private void Chart(Rect rect, List<ArmoryCoreAmmunitionDefinition> all, string title,
            Func<ArmoryCoreAmmunitionDefinition, ArmoryCoreFlight.Sample, float> value, float low, float high, bool log, Func<float, string> format, bool downward = false)
        {
            Graph.Panel(rect);
            var plot = new Rect(rect.x + 44f, rect.y + 20f, rect.width - 150f, rect.height - 38f);
            float Y(float v)
            {
                var t = log
                    ? Mathf.InverseLerp(Mathf.Log(low), Mathf.Log(high), Mathf.Log(Mathf.Max(v, low * 0.5f)))
                    : Mathf.InverseLerp(low, high, v);
                return downward ? Mathf.Lerp(plot.y, plot.yMax, t) : Mathf.Lerp(plot.yMax, plot.y, t);
            }
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), title, EditorStyles.miniBoldLabel);
            for (var i = 0; i <= 5; i++)
            {
                var x = plot.x + plot.width * i / 5f;
                Graph.FillRect(new Rect(x, plot.y, 1f, plot.height), Graph.GridColor);
                GUI.Label(new Rect(x - 30f, plot.yMax + 2f, 60f, 14f), $"{chartRange * i / 5f:0} m", EditorStyles.centeredGreyMiniLabel);
                var v = log ? Mathf.Exp(Mathf.Lerp(Mathf.Log(low), Mathf.Log(high), i / 5f)) : Mathf.Lerp(low, high, i / 5f);
                var y = downward ? Mathf.Lerp(plot.y, plot.yMax, i / 5f) : Mathf.Lerp(plot.yMax, plot.y, i / 5f);
                Graph.FillRect(new Rect(plot.x, y, plot.width, 1f), Graph.GridColor);
                GUI.Label(new Rect(rect.x + 2f, y - 7f, 40f, 14f), format(v), EditorStyles.miniLabel);
            }
            var labels = new List<float>();
            foreach (var ammunition in all.OrderBy(a => a == edited))
            {
                var chosen = ammunition == edited;
                var flight = Flight(ammunition);
                var points = flight.Select(s => new Vector2(
                    plot.x + plot.width * s.Distance / chartRange,
                    Mathf.Clamp(Y(value(ammunition, s)), plot.y - 2f, plot.yMax + 2f))).ToArray();
                Graph.Polyline(points, chosen ? Graph.Accent : Others, chosen ? 2.5f : 1.2f);
                if (points.Length == 0)
                    continue;
                // Names at the end of each curve, nudged apart so they do not overlap.
                var end = points[points.Length - 1];
                var labelY = end.y;
                foreach (var taken in labels.OrderBy(l => l))
                {
                    if (Mathf.Abs(labelY - taken) < 12f)
                        labelY = taken + 12f;
                }
                labels.Add(labelY);
                var reaches = Last(flight).Distance >= chartRange - 0.5f;
                var x = reaches ? plot.xMax + 6f : end.x + 4f;
                GUI.Label(new Rect(x, labelY - 7f, rect.xMax - x - 2f, 14f), ArmoryCoreCartridgeLibrary.Name(ammunition), chosen ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);
                if (!chosen)
                    continue;
                var last = Last(flight);
                GUI.Label(new Rect(plot.x + 4f, plot.y + (downward ? plot.height - 16f : 0f), plot.width, 14f),
                    reaches
                        ? $"{ArmoryCoreCartridgeLibrary.Name(ammunition)}: at {chartRange:0} m — {format(value(ammunition, last))}"
                        : $"{ArmoryCoreCartridgeLibrary.Name(ammunition)}: stops at {last.Distance:0} m",
                    EditorStyles.miniBoldLabel);
            }
        }

        /// <summary>All projectiles side by side at true scale; click one to set it up.</summary>
        private void DrawLineup(Rect rect, List<ArmoryCoreAmmunitionDefinition> all)
        {
            Graph.Panel(rect);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), "True scale against each other — click one to set it up", EditorStyles.miniBoldLabel);
            float Length(ArmoryCoreAmmunitionDefinition a) => a.IsRocket ? a.rocketLengthMeters * 1000f : a.BulletLengthMillimeters;
            const float Gap = 18f;
            var total = all.Sum(Length);
            var scale = Mathf.Min((rect.width - 20f - Gap * all.Count) / Mathf.Max(0.001f, total),
                (rect.height - 54f) / Mathf.Max(0.001f, all.Max(a => a.projectileDiameterMillimeters)));
            var x = rect.x + 10f;
            var middle = rect.y + 20f + (rect.height - 54f) * 0.5f + 4f;
            var e = Event.current;
            foreach (var ammunition in all)
            {
                var width = Length(ammunition) * scale;
                var height = ammunition.projectileDiameterMillimeters * scale;
                var body = new Rect(x, middle - height * 0.5f, width, height);
                var chosen = ammunition == edited;
                if (chosen)
                    Graph.Frame(new Rect(body.x - 4f, body.y - 4f, body.width + 8f, body.height + 8f), Graph.Accent, 2f);
                if (ammunition.IsRocket)
                {
                    ArmoryCoreAmmunitionDefinitionEditor.DrawRocket(body, Olive, true);
                }
                else
                {
                    var hardness = Mathf.InverseLerp(0.5f, 2.5f, ammunition.penetrationMultiplier);
                    ArmoryCoreAmmunitionDefinitionEditor.DrawBullet(body, ammunition.noseShape,
                        ammunition.BulletLengthMillimeters / ammunition.projectileDiameterMillimeters > 2.5f ? 0.4f : 0.5f,
                        Color.Lerp(Copper, Lead, hardness * 0.6f), true);
                }
                var caption = new Rect(x - Gap * 0.5f, rect.yMax - 30f, width + Gap, 28f);
                GUI.Label(caption, ammunition.name, chosen ? EditorStyles.miniBoldLabel : EditorStyles.centeredGreyMiniLabel);
                var hit = new Rect(x - 4f, rect.y + 18f, width + 8f, rect.height - 20f);
                EditorGUIUtility.AddCursorRect(hit, MouseCursor.Link);
                if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
                {
                    edited = ammunition;
                    GUI.FocusControl(null);
                    e.Use();
                    Repaint();
                }
                x += width + Gap;
            }
        }
    }
}
