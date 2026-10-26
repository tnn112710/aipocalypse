// Play-mode probe for enemy damage, knockback and death (enter Play first, runInBackground on):
//   script run --file AgentScripts/Characters/Goblin/ProbeCombat.cs --entry ProbeCombat.Hit --args '["Goblin 1", 30, 6]'
// Hit: a ray from the player's camera to the goblin's chest through DamageService.DealHit (the path bullets and swings use).
// Watch: samples hp, knockback, animator states. Fire: aims the player at a goblin with an AK47 and holds the trigger
// through a virtual mouse. Revive: disables and re-enables a goblin (dead or sunk), which revives it.
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using VContainer;
using Object = UnityEngine.Object;

public static class ProbeCombat
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public static string Hit(string goblin, float damage, float force)
    {
        EnemyBrain brain = Find(goblin);
        if (brain == null)
            return "no " + goblin;
        Camera camera = Camera.main;
        Vector3 chest = brain.transform.position + Vector3.up * 0.9f;
        Vector3 direction = (chest - camera.transform.position).normalized;
        if (!Physics.Raycast(camera.transform.position, direction, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            return "ray hit nothing";
        var damageService = Scope.Resolve<DamageService>();
        var info = new DamageInfo(damage, DamageType.Gunshot, hit.point, direction * force, Player.gameObject);
        IDamageable target = damageService.DealHit(hit, info);
        return $"ray hit {hit.collider.name} (layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}) -> {(target as Component)?.name ?? "not damageable"} | {Line(brain)}";
    }

    // Hit, then samples every `every` seconds for `seconds` in the same call (frame-accurate, no command gap).
    public static async Task<string> Strike(string goblin, float damage, float force, float seconds, float every)
    {
        var sb = new StringBuilder(Hit(goblin, damage, force)).AppendLine();
        EnemyBrain brain = Find(goblin);
        float next = 0f, start = Time.realtimeSinceStartup;
        await Seconds(seconds, () =>
        {
            float t = Time.realtimeSinceStartup - start;
            if (t < next)
                return;
            next += every;
            sb.AppendLine($"+{t:F2}s {Line(brain)}");
        });
        return sb.ToString();
    }

    public static async Task<string> Watch(string goblin, float seconds, float every)
    {
        EnemyBrain brain = Find(goblin);
        if (brain == null)
            return "no " + goblin;
        var sb = new StringBuilder();
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            sb.AppendLine($"t={Time.time:F2} {Line(brain)}");
            await Task.Delay((int)(every * 1000));
        }
        return sb.ToString();
    }

    public static string State()
    {
        var sb = new StringBuilder();
        foreach (EnemyBrain brain in All().OrderBy(b => b.name))
            sb.AppendLine(Line(brain));
        sb.AppendLine($"registered {Scope.Resolve<EnemyRegistry>().Count}, bodies moving {Scope.Resolve<EnemyBodyManager>().Count}");
        return sb.ToString();
    }

    public static string Revive(string goblin)
    {
        EnemyBrain brain = Find(goblin);
        if (brain == null)
            return "no " + goblin;
        brain.gameObject.SetActive(false);
        brain.gameObject.SetActive(true);
        return Line(brain);
    }

    // Stands the player `distance` m from the goblin, gives and selects `item` (a gun or melee weapon), then aims at its chest
    // and holds the attack button.
    public static async Task<string> Fire(string goblin, float distance, float seconds, string item = "AK47")
    {
        EnemyBrain brain = Find(goblin);
        if (brain == null)
            return "no " + goblin;
        Player player = Player;
        Vector3 at = brain.transform.position - brain.transform.forward * -distance;
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = at + Vector3.up * 0.2f;
        controller.enabled = true;
        var definition = AssetDatabase.FindAssets(item + " t:" + nameof(ItemDefinition), new[] { "Assets/_Game/Content" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).First(i => i.name == item);
        ItemInstance gun = definition.CreateInstance();
        player.Equipment.TryPickUp(gun, out _);
        player.Equipment.Select(player.Equipment.Hotbar.IndexOf(gun));
        player.Ammo.Infinite = true;

        object look = typeof(Player).GetField("_look", Private).GetValue(player);
        MethodInfo resetPitch = look.GetType().GetMethod("ResetPitch");
        var settings = InputSystem.settings;
        var background = settings.backgroundBehavior;
        var editorInput = settings.editorInputBehaviorInPlayMode;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var mouse = InputSystem.AddDevice<Mouse>("ProbeMouse");
        string before = Line(brain);
        try
        {
            await Seconds(1.2f, null);
            await Seconds(seconds, () =>
            {
                Vector3 chest = brain.transform.position + Vector3.up * 0.9f;
                Vector3 eye = Camera.main.transform.position;
                Vector3 flat = Vector3.ProjectOnPlane(chest - eye, Vector3.up);
                player.transform.rotation = Quaternion.LookRotation(flat);
                float pitch = -Mathf.Atan2(chest.y - eye.y, flat.magnitude) * Mathf.Rad2Deg;
                resetPitch.Invoke(look, new object[] { pitch });
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, !brain.IsDead));
            });
        }
        finally
        {
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.RemoveDevice(mouse);
            settings.backgroundBehavior = background;
            settings.editorInputBehaviorInPlayMode = editorInput;
            player.Ammo.Infinite = false;
        }
        return $"before: {before}\nafter:  {Line(brain)}";
    }

    static string Line(EnemyBrain brain)
    {
        EnemyBody body = brain.Body;
        Animator animator = brain.Animation != null ? brain.Animation.Animator : null;
        string states = "";
        if (animator != null && animator.isActiveAndEnabled)
        {
            states = $" base={StateName(animator, 0)}";
            int hitLayer = animator.GetLayerIndex("Hit");
            if (hitLayer >= 0)
                states += $" hit={StateName(animator, hitLayer)}";
        }
        bool colliders = brain.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger);
        return $"{brain.name} active={brain.gameObject.activeInHierarchy} hp={body?.Health?.Current:F0}/{body?.Health?.Max:F0} dead={brain.IsDead} " +
               $"pos={brain.transform.position:F2} speed={Speed(brain):F2} colliders={colliders} " +
               $"visualY={body?.Visual?.localPosition.y:F2}{states}";
    }

    static float Speed(EnemyBrain brain)
    {
        try { return brain.GetComponent<EnemyMover>().Velocity.magnitude; }
        catch (NullReferenceException) { return -1f; }
    }

    static string StateName(Animator animator, int layer)
    {
        var clips = animator.GetCurrentAnimatorClipInfo(layer);
        var info = animator.GetCurrentAnimatorStateInfo(layer);
        return (clips.Length > 0 ? clips[0].clip.name : "empty") + $"@{info.normalizedTime:F2}";
    }

    static EnemyBrain Find(string name) => All().FirstOrDefault(b => b.name == name);

    // Includes disabled (sunk) enemies of the open scenes.
    static EnemyBrain[] All() => Resources.FindObjectsOfTypeAll<EnemyBrain>()
        .Where(b => b.gameObject.scene.IsValid() && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(b.gameObject.scene)).ToArray();

    static Player Player => Object.FindObjectsByType<Player>().First();
    static IObjectResolver Scope => Object.FindAnyObjectByType<GameLifetimeScope>().Container;

    // Steps on EditorApplication.update (an await continuation would run inside the PlayerLoop).
    static Task Seconds(float seconds, Action everyFrame)
    {
        var done = new TaskCompletionSource<bool>();
        float end = Time.realtimeSinceStartup + seconds;
        int frame = -1;
        void Tick()
        {
            if (Time.frameCount == frame)
                return;
            frame = Time.frameCount;
            everyFrame?.Invoke();
            if (Time.realtimeSinceStartup < end)
                return;
            EditorApplication.update -= Tick;
            done.TrySetResult(true);
        }
        EditorApplication.update += Tick;
        return done.Task;
    }
}
