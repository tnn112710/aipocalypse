using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Play-mode review of gun handling (WeaponsTest): hold any gun, fit parts, drive a virtual gamepad (move, aim, fire),
// read the aim state, capture the game view, and tune the hip pose (Hip, then SaveHip). Arguments are a JSON array:
//   unity command script run --file AgentScripts/Weapons/AimReview.cs --entry AimReview.Hold --args '["AK47"]'
public static class AimReview
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    // The hip barrel meets the screen centre this far out (WEAPONS.md, Poses).
    const float Converge = 20f;
    // Block-outs that copy a base gun's hip (GunBlockoutSetup.Guns, Reference); AK74M is a variant and inherits.
    static readonly Dictionary<string, string[]> BlockOuts = new()
    {
        ["AK47"] = new[] { "M4", "SCAR", "RPD" },
        ["Pistol"] = new[] { "Glock17", "DesertEagle" },
    };

    // Background play, enemies idle, pause menu closed.
    public static object Prepare()
    {
        Application.runInBackground = true;
        foreach (var brain in Object.FindObjectsByType<MB13.Aipocalypse.Core.EnemySystem.EnemyBrain>(FindObjectsInactive.Exclude))
            brain.enabled = false;
        Unpause();
        return Info();
    }

    // A fresh copy of the gun asset straight into the hands.
    public static object Hold(string gun)
    {
        var rig = Player.GetComponentInChildren<EquipmentRig>(true);
        var equipment = Field<Equipment>(rig, "_equipment");
        var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>($"Assets/_Game/Content/Weapons/{gun}/{gun}.asset");
        if (definition == null)
            return "no gun asset " + gun;
        equipment.Switcher.EquipImmediately(definition.CreateInstance());
        return "holding " + gun;
    }

    // Fits a part from _Shared/Attachments by folder name; "none" removes the optic.
    public static object Fit(string part)
    {
        if (!(Player.HeldItem?.Item is GunInstance gun))
            return "no gun held";
        if (part == "none")
        {
            gun.Detach(GunSlot.Optic);
            return "optic removed";
        }
        var definition = AssetDatabase.LoadAssetAtPath<AttachmentDefinition>($"Assets/_Game/Content/Weapons/_Shared/Attachments/{part}/{part}.asset");
        if (definition == null)
            return "no part " + part;
        return $"fit {part}: {gun.TryAttach(definition.CreateInstance(), out _)}";
    }

    public static object Aim(bool on) => Pad(0f, 0f, on, false);

    // Sets the virtual gamepad to this state: left stick (move), left trigger (aim), west button (fire).
    public static object Pad(float moveX, float moveY, bool aim, bool fire) => Press(moveX, moveY, aim, fire, false);

    // Presses crouch (east button; it toggles) with everything else released; the next Pad call lets go of it.
    public static object Crouch() => Press(0f, 0f, false, false, true);

    // Reuses the pad (removing one mid-press leaves its actions held) and applies the state at once: a queued event alone
    // wasn't always picked up while the editor drives play mode.
    static object Press(float moveX, float moveY, bool aim, bool fire, bool crouch)
    {
        Gamepad pad = Gamepad.all.Count > 0 ? Gamepad.all[0] : InputSystem.AddDevice<Gamepad>();
        var state = new GamepadState { leftStick = new Vector2(moveX, moveY), leftTrigger = aim ? 1f : 0f };
        if (fire)
            state = state.WithButton(GamepadButton.West);
        if (crouch)
            state = state.WithButton(GamepadButton.East);
        InputSystem.QueueStateEvent(pad, state);
        InputSystem.Update();
        return $"pad move=({moveX},{moveY}) aim={aim} fire={fire} crouch={crouch}";
    }

    // Removes the virtual gamepads (input back to keyboard and mouse).
    public static object Release()
    {
        foreach (InputDevice device in InputSystem.devices.ToArray())
            if (device is Gamepad)
                InputSystem.RemoveDevice(device);
        return "pads removed";
    }

    public static object Stand(float x, float z, float yaw)
    {
        Player.Teleport(new Vector3(x, 0.03f, z), yaw);
        return $"player at {Player.transform.position} yaw {yaw:F0}";
    }

    public static object Info()
    {
        HeldItem held = Player.HeldItem;
        string gun = held == null ? "nothing"
            : $"{held.name} aim={held.AimWeight:F2} zoom={held.Zoom:F2} wobble={held.Wobble:F2}° spread={held.Spread:F2}°";
        return $"{gun} dot={Player.CrosshairPosition} gap={Player.CrosshairSpread:F3} screen={Screen.width}x{Screen.height}";
    }

    public static object Unpause()
    {
        var menu = Object.FindAnyObjectByType<PauseMenu>();
        if (menu != null && menu.IsOpen)
            menu.Close();
        EditorApplication.isPaused = false;
        return "unpaused";
    }

    // Closes the pause menu (it opens when the editor loses focus), then saves the game view at the end of this frame.
    public static object Capture(string path)
    {
        Unpause();
        ScreenCapture.CaptureScreenshot(path);
        return "capturing " + path;
    }

    // Moves the held gun's Hip pose live (rig space, metres) and turns it so the barrel meets the screen centre 20 m out,
    // rolled `roll` degrees from upright; then reports where it lands (HipInfo). Lost on exit unless saved with SaveHip.
    public static object Hip(float x, float y, float z, float roll)
    {
        if (!(Player.HeldItem is HeldGun gun))
            return "no gun held";
        Transform hip = Field<Transform>(gun, "_hipPose");
        Transform pivot = Field<Transform>(gun, "_posePivot");
        Transform muzzle = Field<Transform>(gun, "_muzzle");
        Transform rig = gun.GetComponentInParent<EquipmentRig>().transform;
        Vector3 target = hip.parent.InverseTransformPoint(rig.position + rig.forward * Converge);
        Vector3 muzzlePosition = pivot.InverseTransformPoint(muzzle.position);
        Vector3 muzzleForward = pivot.InverseTransformDirection(muzzle.forward);
        var position = new Vector3(x, y, z);
        Quaternion rotation = hip.localRotation;
        // Turning moves the muzzle a little, so a few passes settle it.
        for (int i = 0; i < 8; i++)
            rotation = Quaternion.FromToRotation(rotation * muzzleForward, target - (position + rotation * muzzlePosition)) * rotation;
        Vector3 barrel = rotation * muzzleForward;
        rotation = Quaternion.AngleAxis(roll - Roll(rotation, barrel), barrel) * rotation;
        hip.SetLocalPositionAndRotation(position, rotation);
        return HipInfo();
    }

    // The held gun's Hip pose and where its muzzle lands on screen (held lens), % across / % down at this aspect.
    public static object HipInfo()
    {
        if (!(Player.HeldItem is HeldGun gun))
            return "no gun held";
        Transform hip = Field<Transform>(gun, "_hipPose");
        Transform pivot = Field<Transform>(gun, "_posePivot");
        Transform muzzle = Field<Transform>(gun, "_muzzle");
        Transform rig = gun.GetComponentInParent<EquipmentRig>().transform;
        float fov = Object.FindAnyObjectByType<FirstPersonView>().GetFieldOfView(Camera.main);
        float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        Vector3 local = hip.localPosition + hip.localRotation * pivot.InverseTransformPoint(muzzle.position);
        Vector3 v = rig.InverseTransformPoint(hip.parent.TransformPoint(local));
        float across = 50f + 50f * v.x / (v.z * tan * Screen.width / Screen.height);
        float down = 50f - 50f * v.y / (v.z * tan);
        float roll = Roll(hip.localRotation, hip.localRotation * pivot.InverseTransformDirection(muzzle.forward));
        return $"{gun.name} hip {hip.localPosition.ToString("F3")} roll {roll:F1} muzzle {across:F1}% across {down:F1}% down";
    }

    // Writes the held gun's live Hip pose into its prefab (Hip and PosePivot), and shifts the block-outs that copy it by the
    // same change, as GunBlockoutSetup would on a rebuild.
    public static object SaveHip()
    {
        if (!(Player.HeldItem is HeldGun held))
            return "no gun held";
        string gun = held.Item.Definition.name;
        Transform live = Field<Transform>(held, "_hipPose");
        Vector3 delta = WriteHip(gun, live.localPosition, live.localRotation, null);
        string note = "saved " + gun;
        if (BlockOuts.TryGetValue(gun, out string[] blockOuts))
            foreach (string blockOut in blockOuts)
            {
                WriteHip(blockOut, default, live.localRotation, delta);
                note += ", " + blockOut;
            }
        return note;
    }

    // Edit mode: gives a gun prefab its Sprint pose, turned by (pitch, yaw, roll) and placed so its right grip sits at (x, y, z)
    // in view space (metres from the eye). Long guns 10, -40, -30, (0.22, -0.2, 0.32); pistols 35, -10, -10, (0.12, -0.24, 0.45).
    public static object SetSprint(string gun, float pitch, float yaw, float roll, float x, float y, float z)
    {
        if (Application.isPlaying)
            return "leave Play mode first";
        string path = $"Assets/_Game/Content/Weapons/{gun}/{gun}.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var held = new SerializedObject(root.GetComponent<HeldGun>());
        var sprint = (Transform)held.FindProperty("_sprintPose").objectReferenceValue;
        var pivot = (Transform)held.FindProperty("_posePivot").objectReferenceValue;
        Vector3 grip = pivot.InverseTransformPoint(root.GetComponent<ItemHands>().GetGrip(HandSide.Right).transform.position);
        // The gun's root sits at the player's Hand point, which the hand set's previews use too.
        Vector3 item = AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset").PreviewItemPosition;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, roll);
        Vector3 position = new Vector3(x, y, z) - item - rotation * grip;
        sprint.SetLocalPositionAndRotation(position, rotation);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return $"{gun}: sprint pose at {position.ToString("F3")}, turned ({pitch}, {yaw}, {roll})";
    }

    // Edit mode: gives a gun prefab its Work pose (HeldGun: where it is held while reloading or clearing a jam at the hip),
    // the Hip pose moved by this much (rig space, metres) and pitched / rolled by these degrees.
    public static object SetWork(string gun, float x, float y, float z, float pitch, float roll)
    {
        if (Application.isPlaying)
            return "leave Play mode first";
        string path = $"Assets/_Game/Content/Weapons/{gun}/{gun}.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform hip = root.transform.Find("Poses/Hip"), work = root.transform.Find("Poses/Work");
        if (work == null)
        {
            work = new GameObject("Work").transform;
            work.SetParent(hip.parent, false);
        }
        work.SetLocalPositionAndRotation(hip.localPosition + new Vector3(x, y, z), hip.localRotation * Quaternion.Euler(pitch, 0f, roll));
        var held = new SerializedObject(root.GetComponent<HeldGun>());
        held.FindProperty("_workPose").objectReferenceValue = work;
        held.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return $"{gun}: work pose at hip + ({x}, {y}, {z}), pitch {pitch}, roll {roll}";
    }

    // Sets a prefab's Hip (and PosePivot, which mirrors it) to `position`, or moves it by `shift`; returns the move.
    static Vector3 WriteHip(string gun, Vector3 position, Quaternion rotation, Vector3? shift)
    {
        string path = $"Assets/_Game/Content/Weapons/{gun}/{gun}.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform hip = root.transform.Find("Poses/Hip");
        Vector3 before = hip.localPosition;
        Vector3 after = shift.HasValue ? before + shift.Value : position;
        hip.SetLocalPositionAndRotation(after, rotation);
        root.transform.Find("PosePivot").SetLocalPositionAndRotation(after, rotation);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return after - before;
    }

    // Degrees the gun is rolled about its barrel, from upright.
    static float Roll(Quaternion rotation, Vector3 barrel) => Vector3.SignedAngle(
        Vector3.ProjectOnPlane(Vector3.up, barrel), Vector3.ProjectOnPlane(rotation * Vector3.up, barrel), barrel);

    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Any).GetValue(target);

    static Player Player => Object.FindAnyObjectByType<Player>();
}
