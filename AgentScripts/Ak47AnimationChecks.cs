using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.WeaponSystem;
using Object = UnityEngine.Object;
using Cysharp.Threading.Tasks;

public static class Ak47AnimationChecks
{
    public static System.Threading.Tasks.Task<object> TraceLive(string action = "ReloadingEmpty") => TraceLiveAsync(action).AsTask();

    static async UniTask<object> TraceLiveAsync(string action)
    {
        var held = Object.FindAnyObjectByType<HeldGun>();
        if (held == null) throw new InvalidOperationException("Equip AK47 before tracing.");
        var animation = held.GetComponentInChildren<GunAnimation>();
        var animator = animation.GetComponent<Animator>();
        var root = animation.transform;
        var motion = root.Find("Motion");
        var magazine = root.Find("Motion/Weapon/Magazine");
        var replacement = root.Find("Motion/Weapon/ReplacementMagazine");
        string weapon = held.Gun.Item.Gun.name;
        var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/_Game/_Core/WeaponSystem/Animations/" + weapon + "/" + weapon + ".overrideController");
        var clip = overrides[action];
        var magY = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("Motion/Weapon/Magazine", typeof(Transform), "m_LocalPosition.y"));
        var motionY = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("Motion", typeof(Transform), "m_LocalPosition.y"));
        var hands = Object.FindAnyObjectByType<Aipocalypse.Core.HandSystem.FirstPersonHands>();
        var leftHand = hands.GetComponentsInChildren<Aipocalypse.Core.HandSystem.HandRig>().Single(h => h.Side == Aipocalypse.Core.HandSystem.HandSide.Left);
        var leftTarget = held.Hands.GetDefault(Aipocalypse.Core.HandSystem.HandSide.Left).transform;
        var samples = new List<object>();
        held.Gun.Interrupt();
        held.Gun.Item.SetJammed(action == "Repairing");
        held.Gun.Item.SetRounds(action == "ReloadingEmpty" ? 0 : action == "Cycling" ? held.Gun.Item.Gun.Magazine.Size : Mathf.Max(1, held.Gun.Item.Gun.Magazine.Size - 2));
        held.Gun.Tick(new GunInput { IsReady = true, CanFire = true, CanAim = true, Reload = action == "Reloading", TriggerHeld = action == "Cycling", TriggerPressed = action == "Cycling" }, 0f);
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        float start = Time.realtimeSinceStartup;
        float nextSample = 0f;
        float maxPartError = 0f;
        float maxHandError = 0f;
        int observedFrames = 0;
        bool completed = false;
        for (int i = 0; i < 2000; i++)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, held.destroyCancellationToken);
            if (held.Gun.Action == GunAction.None)
            {
                completed = observedFrames > 0;
                break;
            }
            observedFrames++;
            float t = animation.Progress * clip.length;
            maxPartError = Mathf.Max(maxPartError, Mathf.Abs(motion.localPosition.y - motionY.Evaluate(t)));
            if (magazine != null) maxPartError = Mathf.Max(maxPartError, Mathf.Abs(magazine.localPosition.y - magY.Evaluate(t)));
            foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform) && b.propertyName.StartsWith("m_LocalPosition")))
            {
                var part = root.Find(binding.path);
                int axis = "xyz".IndexOf(binding.propertyName[binding.propertyName.Length - 1]);
                maxPartError = Mathf.Max(maxPartError, Mathf.Abs(part.localPosition[axis] - AnimationUtility.GetEditorCurve(clip, binding).Evaluate(t)));
            }
            maxHandError = Mathf.Max(maxHandError, Vector3.Distance(leftHand.GripPoint.position, leftTarget.position));
            if (animation.Progress >= nextSample)
            {
                nextSample += 0.1f;
                samples.Add(new { frame = Time.frameCount, state = animation.State, progress = animation.Progress,
                    action = held.Gun.Action.ToString(), actionProgress = held.Gun.ActionProgress,
                    motion = motion.localPosition.ToString("F4"), rotation = motion.localEulerAngles.ToString("F1"),
                    mag = magazine != null ? magazine.localPosition.ToString("F4") : "none", visible = magazine != null && magazine.GetComponent<Renderer>().enabled,
                    animatorState = animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    animatorTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime });
            }
            if (Time.realtimeSinceStartup - start > 8f)
                break;
        }
        if (!completed || maxPartError > 0.0001f || maxHandError > 0.0001f)
            throw new Exception($"End-of-frame animation check failed: complete={completed}, part error={maxPartError}, hand error={maxHandError}.");
        if (magazine != null && (!magazine.GetComponent<Renderer>().enabled || replacement.GetComponent<Renderer>().enabled))
            throw new Exception("Reload visibility did not reset after completion.");
        return new { weapon, action, observedFrames, completed, maxPartError, maxHandError, samples };
    }

    public static object EquipLive(string weapon = "AK47")
    {
        var player = Object.FindAnyObjectByType<Player>();
        if (player == null) throw new InvalidOperationException("No player in the running scene.");
        var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>("Assets/_Game/Data/Items/" + weapon + ".asset");
        var item = Enumerable.Range(0, player.Equipment.Hotbar.Count).Select(i => player.Equipment.Hotbar[i]).OfType<GunInstance>().FirstOrDefault(i => i.Gun == definition) ?? new GunInstance(definition);
        player.Ammo.Add(definition.Magazine.AmmoType, 90);
        bool picked = player.Equipment.Hotbar.IndexOf(item) >= 0 || player.Equipment.TryPickUp(item, out _);
        player.Equipment.Switcher.EquipImmediately(item);
        return new { picked, slot = player.Equipment.SelectedIndex };
    }

    public static object LiveAction(string action)
    {
        var held = Object.FindAnyObjectByType<HeldGun>();
        if (held == null) throw new InvalidOperationException("No held gun.");
        if (action == "Reloading") held.Gun.Item.SetRounds(12);
        if (action == "ReloadingEmpty") held.Gun.Item.SetRounds(0);
        if (action == "Repairing") held.Gun.Item.SetJammed(true);
        bool fire = action == "Firing";
        held.Tick(new Aipocalypse.Core.EquipmentSystem.HeldItemInput {
            IsReady = true, UseHeld = fire, UsePressed = fire, ReloadPressed = action == "Reloading"
        }, 1f / 60f);
        return LiveStatus();
    }

    public static object LiveStatus()
    {
        var held = Object.FindAnyObjectByType<HeldGun>();
        var animation = held.GetComponentInChildren<GunAnimation>();
        return new { action = held.Gun.Action.ToString(), state = animation.State, progress = animation.Progress,
            rounds = held.Gun.Item.Rounds, jammed = held.Gun.Item.IsJammed,
            drops = Object.FindObjectsByType<PooledEffect>().Count(e => e.name == "AK47_Magazine") };
    }

    public static object Proportions()
    {
        var item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/_Core/WeaponSystem/Prefabs/Guns/AK47.prefab"));
        var set = AssetDatabase.LoadAssetAtPath<Aipocalypse.Core.HandSystem.HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
        var hand = Object.Instantiate(set.Get(Aipocalypse.Core.HandSystem.HandSide.Right));
        try
        {
            var model = item.transform.Find("PosePivot/RecoilPivot/AnimationRoot/Motion/Weapon");
            var meshes = model.GetComponentsInChildren<MeshFilter>().Where(m => m.name != "ReplacementMagazine").ToArray();
            Bounds gun = new Bounds(model.position, Vector3.zero);
            foreach (var mesh in meshes) gun.Encapsulate(mesh.GetComponent<Renderer>().bounds);
            var renderer = hand.GetComponentInChildren<SkinnedMeshRenderer>();
            Vector3 openSize = renderer.bounds.size;
            var grip = model.Find("Grip").GetComponent<MeshRenderer>();
            return new { gunBoundsMetres = gun.size.ToString("F3"), gunLengthMetres = gun.size.z,
                handBoundsMetres = openSize.ToString("F3"), handScale = item.GetComponent<Aipocalypse.Core.HandSystem.ItemHands>().HandScale,
                gunGripBoundsMetres = grip.bounds.size.ToString("F3") };
        }
        finally { Object.DestroyImmediate(hand.gameObject); Object.DestroyImmediate(item); }
    }

    public static object LiveFrame(string action, float progress)
    {
        EditorApplication.isPaused = true;
        var held = Object.FindAnyObjectByType<HeldGun>();
        held.Gun.Interrupt();
        var input = new Aipocalypse.Core.EquipmentSystem.HeldItemInput { IsReady = true };
        var definition = held.Gun.Item.Gun;
        held.Gun.Item.SetJammed(action == "Repairing");
        held.Gun.Item.SetRounds(action == "ReloadingEmpty" ? 0 : 12);
        if (action == "Reloading") input.ReloadPressed = true;
        if (action == "Firing") { input.UseHeld = true; input.UsePressed = true; }
        held.Tick(input, 0.001f);
        input.ReloadPressed = false;
        input.UseHeld = false;
        input.UsePressed = false;
        float duration = action == "Repairing" ? definition.Jam.ClearTime : action == "Firing" ? definition.Fire.SecondsPerShot : definition.Magazine.ReloadTime;
        held.Tick(input, duration * progress);
        held.LateTick();
        Object.FindAnyObjectByType<Aipocalypse.Core.HandSystem.FirstPersonHands>().Tick(held.Hands, 1f / 60f);
        return LiveStatus();
    }

    public static object LiveVerify()
    {
        LiveFrame("ReloadingEmpty", 0.5f);
        var held = Object.FindAnyObjectByType<HeldGun>();
        var animation = held.GetComponentInChildren<GunAnimation>();
        int drops = Object.FindObjectsByType<PooledEffect>().Count(e => e.name == "AK47_Magazine");
        if (drops != 1) throw new Exception("Expected one cosmetic magazine after release.");
        var ready = new Aipocalypse.Core.EquipmentSystem.HeldItemInput { IsReady = true };
        held.Tick(ready, 0.05f);
        if (Object.FindObjectsByType<PooledEffect>().Count(e => e.name == "AK47_Magazine") != drops)
            throw new Exception("Magazine released twice in one reload.");
        held.Tick(ready, 1.1f);
        if (held.Gun.Item.Rounds != 30 || held.Gun.Action != GunAction.None || animation.State != GunAnimation.Idle)
            throw new Exception("Empty reload did not complete and restore idle.");
        LiveFrame("Repairing", 0.6f);
        held.Tick(ready, 0.7f);
        if (held.Gun.Item.IsJammed || animation.State != GunAnimation.Idle)
            throw new Exception("Repairing did not clear the jam.");
        held.Hide();
        held.Show();
        if (animation.State != GunAnimation.Equipped)
            throw new Exception("Re-equipping did not restart Equipped.");
        var player = Object.FindAnyObjectByType<Player>();
        var mp5 = new GunInstance(AssetDatabase.LoadAssetAtPath<GunDefinition>("Assets/_Game/Data/Items/MP5.asset"));
        player.Equipment.TryPickUp(mp5, out _);
        player.Equipment.Switcher.EquipImmediately(mp5);
        var other = Object.FindAnyObjectByType<HeldGun>();
        Object.FindAnyObjectByType<Aipocalypse.Core.HandSystem.FirstPersonHands>().Tick(other.Hands, 1f / 60f);
        if (other.GetComponentInChildren<GunAnimation>() == null || Mathf.Abs(other.Hands.HandScale - 1.12f) > 0.001f)
            throw new Exception("The other converted gun did not retain its animation and hand size.");
        var handRig = Object.FindAnyObjectByType<Aipocalypse.Core.HandSystem.FirstPersonHands>();
        if (handRig.GetComponentsInChildren<Aipocalypse.Core.HandSystem.HandRig>().Any(h => Mathf.Abs(h.transform.localScale.x - 1.12f) > 0.001f))
            throw new Exception("Shared hands did not keep the converted gun's scale.");
        return new { reloadCompleted = true, magazineReleasedOnce = true, jamCleared = true, reequipped = true,
            otherGunClipsPresent = true, otherGunHandScale = other.Hands.HandScale };
    }

    public static object Verify(string fixture = "GunAnimationTests")
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Aipocalypse.Tests.EditMode");
        var type = assembly.GetType("Aipocalypse.Tests." + fixture, true);
        var results = new List<object>();
        int passed = 0;
        int failed = 0;
        foreach (var method in type.GetMethods())
        {
            foreach (var attribute in method.GetCustomAttributes(false))
            {
                string name = attribute.GetType().FullName;
                if (name != "NUnit.Framework.TestAttribute" && name != "NUnit.Framework.TestCaseAttribute") continue;
                object[] args = name.EndsWith("TestCaseAttribute") ? (object[])attribute.GetType().GetProperty("Arguments").GetValue(attribute) : Array.Empty<object>();
                object suite = Activator.CreateInstance(type);
                string label = method.Name + "(" + string.Join(", ", args) + ")";
                try
                {
                    type.GetMethod("SetUp")?.Invoke(suite, null);
                    method.Invoke(suite, args);
                    passed++;
                    results.Add(new { name = label, passed = true });
                }
                catch (Exception e)
                {
                    failed++;
                    var error = e is TargetInvocationException ? e.InnerException : e;
                    results.Add(new { name = label, passed = false, message = error.Message, stack = error.StackTrace });
                }
                finally { type.GetMethod("TearDown")?.Invoke(suite, null); }
            }
        }
        return new { passed, failed, results };
    }
}
