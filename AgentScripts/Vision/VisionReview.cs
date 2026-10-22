using System.Linq;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using UnityEditor;
using UnityEngine;

// Play-mode helpers for the player's vision light (use with AgentScripts/DayNight/Review.cs for the hour and view).
public static class VisionReview
{
    static Player Player => Object.FindObjectsByType<Player>().First();

    public static object State()
    {
        Player player = Player;
        PlayerVision vision = player.Vision;
        var light = player.transform.Find("VisionLight").GetComponent<Light>();
        return $"player {player.transform.position} belt {player.Equipment?.Belt?.Definition.name ?? "-"} | vision radius {vision?.Radius:F2} intensity {vision?.Intensity:F2} " +
               $"| light on {light.enabled} range {light.range:F2} int {light.intensity:F2} col {light.color} pos {light.transform.localPosition} | frame {Time.frameCount}";
    }

    // Lets Play mode run (look transitions, darkness fade) and reports.
    public static async System.Threading.Tasks.Task<string> Wait(float seconds)
    {
        await System.Threading.Tasks.Task.Delay((int)(seconds * 1000f));
        return (string)State();
    }

    // Stops PlayerVision driving the light and sets it by hand, to try values.
    public static object Manual(float x, float y, float z, float intensity, float range, float r, float g, float b)
    {
        Player player = Player;
        typeof(Player).GetProperty(nameof(Player.Vision)).SetValue(player, null);
        var light = player.transform.Find("VisionLight").GetComponent<Light>();
        light.enabled = true;
        light.transform.localPosition = new Vector3(x, y, z);
        light.intensity = intensity;
        light.range = range;
        light.color = new Color(r, g, b);
        return State();
    }

    public static object Give(string itemAsset)
    {
        var item = AssetDatabase.FindAssets($"{itemAsset} t:{nameof(ItemDefinition)}", new[] { "Assets/_Game/Content" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .First(i => i.name == itemAsset);
        Player.Equipment.TryPickUp(item.CreateInstance(), out _);
        return State();
    }

    // A lantern pickup on the ground in front of the player.
    public static object SpawnPickup(float distance)
    {
        Player player = Player;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Items/Lantern/Lantern.prefab");
        Vector3 at = player.transform.position + player.transform.forward * distance + Vector3.up * 2f;
        if (Physics.Raycast(at, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            at = hit.point;
        GameObject pickup = Object.Instantiate(prefab, at, Quaternion.identity);
        pickup.name = prefab.name;
        return $"pickup at {at}";
    }

    // What E does on the lantern pickup: the prompt, then the interaction.
    public static object PickUp()
    {
        Player player = Player;
        WorldItem pickup = Object.FindObjectsByType<WorldItem>().First(w => w.Item?.Definition is LanternDefinition);
        pickup.TryGetPrompt(player.gameObject, out var prompt);
        pickup.Interact(player.gameObject);
        return $"prompt '{prompt.Verb} {prompt.Target}' → " + State();
    }

    // Puts a lantern on the belt, or takes it off.
    public static object Wear(bool wear)
    {
        Equipment equipment = Player.Equipment;
        if (wear)
        {
            var lantern = AssetDatabase.LoadAssetAtPath<LanternDefinition>("Assets/_Game/Content/Items/Lantern/Lantern.asset");
            equipment.TryPickUp(lantern.CreateInstance(), out _);
        }
        else
        {
            typeof(Equipment).GetProperty(nameof(Equipment.Belt)).SetValue(equipment, null);
        }
        return State();
    }
}
