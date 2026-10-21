using System.Linq;
using System.Text;
using UnityEngine;
public static class DetailStats
{
    public static object Run()
    {
        var t = Terrain.activeTerrain; var d = t.terrainData;
        var sb = new StringBuilder();
        sb.AppendLine($"res {d.detailResolution} scatter {d.detailScatterMode} dist {t.detailObjectDistance} density {t.detailObjectDensity}");
        var protos = d.detailPrototypes;
        for (int i = 0; i < protos.Length; i++)
        {
            var layer = d.GetDetailLayer(0, 0, d.detailWidth, d.detailHeight, i);
            long sum = 0; int cells = 0;
            foreach (var v in layer) { sum += v; if (v > 0) cells++; }
            var p = protos[i];
            sb.AppendLine($"{i,2} {(p.prototype ? p.prototype.name : "tex")} cells {cells} avg {(cells > 0 ? sum / (float)cells : 0):0.0} density {p.density} w {p.minWidth}-{p.maxWidth} mode {p.renderMode} inst {p.useInstancing} valid {p.Validate(out string err)} {err}");
        }
        sb.AppendLine("trees " + d.treeInstanceCount + " protos " + d.treePrototypes.Length);
        var counts = d.treeInstances.GroupBy(ti => ti.prototypeIndex).OrderBy(g => g.Key).Select(g => (d.treePrototypes[g.Key].prefab ? d.treePrototypes[g.Key].prefab.name : "?") + ":" + g.Count());
        sb.AppendLine(string.Join(", ", counts));
        return sb.ToString();
    }
}
