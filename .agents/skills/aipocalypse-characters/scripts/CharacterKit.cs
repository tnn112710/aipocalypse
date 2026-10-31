// Vendor-neutral character helpers for `unity command script run`. Nothing here touches the open scene.
//
//   Describe(prefabPath)                          renderers, bones, blendshapes, animator facts (JSON)
//   Snapshot(prefabPath, pngPath, clipPath, time, yaw, size, meshLod = -1)
//                                                 offscreen render of a prefab, optionally posed by a humanoid clip
//                                                 at `time` seconds ("" = bind pose); view the PNG to check the look.
//                                                 meshLod >= 0 forces that Mesh LOD level (check generated LODs)
//   ClipInfo(fbxOrAnimPath)                       clips in a model/anim file: length, loop, root-motion bake flags,
//                                                 average root speed (m/s; 0 when baked into pose) and turn rate
//   BuildLocomotionController(specPath)           AnimatorController with the project parameter contract from a spec
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterKit
{
    public static string Describe(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return Fail($"no prefab at {prefabPath}");
        var sb = new StringBuilder("{");
        sb.Append($"\"prefab\":{Json(prefabPath)},\"transforms\":{prefab.GetComponentsInChildren<Transform>(true).Length}");
        foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
        {
            Avatar avatar = animator.avatar;
            sb.Append($",\"animator\":{{\"path\":{Json(PathOf(animator.transform, prefab.transform))},\"avatar\":{Json(avatar ? AssetDatabase.GetAssetPath(avatar) + ":" + avatar.name : null)},\"human\":{B(avatar && avatar.isHuman)},\"controller\":{Json(animator.runtimeAnimatorController ? AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) : null)},\"culling\":{Json(animator.cullingMode.ToString())},\"rootMotion\":{B(animator.applyRootMotion)},\"hasTransformHierarchy\":{B(animator.hasTransformHierarchy)}}}");
        }
        sb.Append(",\"renderers\":[");
        bool first = true;
        foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
        {
            sb.Append(first ? "" : ",");
            first = false;
            sb.Append($"{{\"path\":{Json(PathOf(r.transform, prefab.transform))},\"type\":{Json(r.GetType().Name)},\"materials\":{JsonList(r.sharedMaterials.Select(m => m ? m.name + " (" + m.shader.name + ")" : "null"))},\"shadows\":{Json(r.shadowCastingMode.ToString())}");
            if (r is SkinnedMeshRenderer smr)
            {
                Mesh mesh = smr.sharedMesh;
                int weighted = 0;
                if (mesh != null)
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                        if (smr.GetBlendShapeWeight(i) != 0f)
                            weighted++;
                sb.Append($",\"mesh\":{Json(mesh ? AssetDatabase.GetAssetPath(mesh) : null)},\"vertices\":{(mesh ? mesh.vertexCount : 0)},\"bones\":{smr.bones.Length},\"uniqueBones\":{smr.bones.Distinct().Count()},\"blendShapes\":{(mesh ? mesh.blendShapeCount : 0)},\"nonZeroBlendShapeWeights\":{weighted},\"updateWhenOffscreen\":{B(smr.updateWhenOffscreen)},\"motionVectors\":{B(smr.skinnedMotionVectors)},\"readable\":{B(mesh && mesh.isReadable)},\"boundsSize\":{Json(smr.localBounds.size.ToString("F2"))}");
            }
            sb.Append('}');
        }
        return sb.Append("]}").ToString();
    }

    public static string Snapshot(string prefabPath, string pngPath, string clipPath, float time, float yaw, int size, int meshLod = -1)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return Fail($"no prefab at {prefabPath}");
        AnimationClip clip = string.IsNullOrEmpty(clipPath) ? null : LoadClip(clipPath);
        if (!string.IsNullOrEmpty(clipPath) && clip == null)
            return Fail($"no clip at {clipPath}");

        var preview = new PreviewRenderUtility();
        try
        {
            // Straight into the preview scene (the active scene would get dirty), unpacked so its hierarchy can change.
            GameObject instance = preview.InstantiatePrefabInScene(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            var animator = instance.GetComponentInChildren<Animator>();
            // An optimized hierarchy only skins while an Animator updates, which never happens in a preview scene: restore
            // the bones on this throwaway instance (the prefab stays optimized).
            if (animator != null && !animator.hasTransformHierarchy)
                AnimatorUtility.DeoptimizeTransformHierarchy(animator.gameObject);
            if (clip != null && animator != null)
                clip.SampleAnimation(animator.gameObject, time);
            if (meshLod >= 0)
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
                    r.forceMeshLod = (short)meshLod;

            Bounds bounds = WorldBounds(instance);
            // Height-led framing: bind-pose (T-pose) bounds are wide, a diagonal would leave the character small.
            float radius = Mathf.Max(bounds.extents.y * 1.1f, bounds.extents.z, 0.5f);
            Camera camera = preview.camera;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.36f, 0.4f, 1f);
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.position = bounds.center + new Vector3(0f, radius * 0.2f, distance);
            camera.transform.LookAt(bounds.center);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, 160f, 0f);
            preview.lights[1].intensity = 0.6f;
            preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);

            var rect = new Rect(0, 0, size, size);
            preview.BeginStaticPreview(rect);
            preview.Render(true);
            Texture2D image = preview.EndStaticPreview();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pngPath)));
            File.WriteAllBytes(pngPath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            return $"{{\"ok\":true,\"png\":{Json(pngPath)},\"bounds\":{Json(bounds.size.ToString("F2"))},\"clip\":{Json(clip ? clip.name : null)}}}";
        }
        catch (Exception e)
        {
            return Fail(e.GetType().Name + ": " + e.Message);
        }
        finally
        {
            preview.Cleanup();
        }
    }

    public static string ClipInfo(string path)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
        if (clips.Count == 0)
            return Fail($"no clips in {path}");
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        var sb = new StringBuilder("[");
        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            float speed = clip.length > 0f ? clip.averageSpeed.magnitude : 0f;
            sb.Append(i == 0 ? "" : ",");
            sb.Append($"{{\"name\":{Json(clip.name)},\"length\":{clip.length:F3},\"loop\":{B(settings.loopTime)},\"human\":{B(clip.humanMotion)},\"averageSpeed\":{speed:F3},\"averageAngularSpeedDeg\":{clip.averageAngularSpeed * Mathf.Rad2Deg:F1},\"bakeRotation\":{B(settings.loopBlendOrientation)},\"bakeY\":{B(settings.loopBlendPositionY)},\"bakeXZ\":{B(settings.loopBlendPositionXZ)},\"additiveReference\":{Json(settings.additiveReferencePoseClip ? settings.additiveReferencePoseClip.name : null)}}}");
        }
        sb.Append(']');
        if (importer != null)
            sb.Insert(0, $"{{\"animationType\":{Json(importer.animationType.ToString())},\"avatarSource\":{Json(importer.sourceAvatar ? AssetDatabase.GetAssetPath(importer.sourceAvatar) : null)},\"clips\":").Append('}');
        return sb.ToString();
    }

    // Builds (or rebuilds in place, keeping the GUID) a locomotion AnimatorController with the project's parameter contract
    // from a spec file of `key = value` lines (# comment lines, or " #" after a value). Clip refs: "file.fbx" or "file.fbx#ClipName".
    //   controller = Assets/_Game/Content/<Kind>/<Name>/<Name>.controller
    //   neutral    = Assets/.../<Name>_AdditiveNeutral.anim   generated: first frame of headLook.left, a zero-delta centre
    //   move       = <clip> @ <m/s>                         one per line, idle at 0 (thresholds = the clips' authored speeds)
    //   fidget     = <clip>                                 idle variations, played on the Fidget trigger by FidgetIndex
    //   headLook.left|right|up|down = <clip>                additive, 2D on LookX/LookY
    //   bodyLook.left|right|up|down = <clip>                additive, same parameters, layer weight bodyLook.weight
    //   lean.left|right = <clip>                            additive, 1D on Lean
    //   hit.front|back|left|right = <clip>                  hit reactions on the Hit trigger by HitIndex 0..3 (where the hit
    //   heavy.front|back|left|right = <clip>                came from), heavy ones at HitIndex 4..7 (missing = the light clip),
    //   hit.mask = upper | full                             in an override layer; upper (default) keeps the legs walking
    //   death = <clip>                                      one per line, base layer, from any state on Dead by DeathIndex
    //   attack = <clip>                                     one per line, base layer (full body, tagged Attack), from any
    //                                                       state on the Attack trigger by AttackIndex, back to locomotion
    //   appear = <clip>                                     one clip, base layer, played once on the Appear trigger when
    //                                                       spawned, back to locomotion near its end or once it walks
    //   celebrate = <clip>                                  one per line, base layer, from Locomotion while the Celebrate bool
    //                                                       is on, by CelebrateIndex; repeats until it goes off or it walks
    //   combatIdle = <clip>                                 idle while fighting: a second blend tree (this idle, the same
    //                                                       moves) entered on the Combat bool; swings return to it
    //   grip = <clip>                                       a held pose (closed hand round a weapon) in an override layer
    //   grip.mask = <AvatarMask asset>                      what the grip layer covers (e.g. a right-hand mask)
    public static string BuildLocomotionController(string specPath)
    {
        if (!File.Exists(specPath))
            return Fail($"no spec at {specPath}");
        var spec = new List<KeyValuePair<string, string>>();
        foreach (string raw in File.ReadAllLines(specPath))
        {
            // Comments: whole lines starting with #, or " #" after a value ("file.fbx#Clip" is a clip reference).
            string line = raw.Trim();
            if (line.StartsWith("#"))
                continue;
            int comment = line.IndexOf(" #", StringComparison.Ordinal);
            if (comment >= 0)
                line = line.Substring(0, comment).Trim();
            int eq = line.IndexOf('=');
            if (eq > 0)
                spec.Add(new KeyValuePair<string, string>(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim()));
        }
        string Get(string key) => spec.FirstOrDefault(p => p.Key == key).Value;
        string controllerPath = Get("controller");
        if (string.IsNullOrEmpty(controllerPath) || !controllerPath.StartsWith("Assets/_Game/"))
            return Fail("controller must be under Assets/_Game/");

        var missing = new List<string>();
        AnimationClip Clip(string reference)
        {
            if (string.IsNullOrEmpty(reference))
                return null;
            AnimationClip clip = LoadClip(reference);
            if (clip == null)
                missing.Add(reference);
            return clip;
        }

        var moves = spec.Where(p => p.Key == "move").Select(p =>
        {
            string[] parts = p.Value.Split('@');
            return (clip: Clip(parts[0].Trim()), speed: parts.Length > 1 ? float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0f);
        }).ToList();
        var fidgets = spec.Where(p => p.Key == "fidget").Select(p => Clip(p.Value)).ToList();
        string[] directions = { "left", "right", "up", "down" };
        Vector2[] positions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        var headLook = directions.Select(d => Clip(Get("headLook." + d))).ToArray();
        var bodyLook = directions.Select(d => Clip(Get("bodyLook." + d))).ToArray();
        var lean = new[] { Clip(Get("lean.left")), Clip(Get("lean.right")) };
        string[] sides = { "front", "back", "left", "right" };
        var hits = sides.Select(d => Clip(Get("hit." + d))).ToArray();
        var heavyHits = sides.Select(d => Clip(Get("heavy." + d))).ToArray();
        var deaths = spec.Where(p => p.Key == "death").Select(p => Clip(p.Value)).ToList();
        var attacks = spec.Where(p => p.Key == "attack").Select(p => Clip(p.Value)).ToList();
        AnimationClip appear = Clip(Get("appear"));
        var celebrations = spec.Where(p => p.Key == "celebrate").Select(p => Clip(p.Value)).ToList();
        AnimationClip combatIdle = Clip(Get("combatIdle"));
        AnimationClip grip = Clip(Get("grip"));
        AvatarMask gripMask = null;
        if (grip != null && (gripMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(Get("grip.mask") ?? "")) == null)
            missing.Add("grip.mask " + Get("grip.mask"));
        if (missing.Count > 0)
            return Fail("clips not found: " + string.Join(", ", missing));
        if (moves.Count == 0)
            return Fail("at least one move clip (idle @ 0) is needed");

        AnimationClip neutral = null;
        AnimationClip neutralSource = headLook.FirstOrDefault(c => c != null) ?? lean.FirstOrDefault(c => c != null);
        if (neutralSource != null)
        {
            string neutralPath = Get("neutral") ?? controllerPath.Replace(".controller", "_AdditiveNeutral.anim");
            neutral = SaveStaticPose(neutralSource, neutralPath);
        }

        AnimatorController controller = ResetController(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Fidget", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("FidgetIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("LookX", AnimatorControllerParameterType.Float);
        controller.AddParameter("LookY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Lean", AnimatorControllerParameterType.Float);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("HitIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("DeathIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("Combat", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Appear", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Celebrate", AnimatorControllerParameterType.Bool);
        controller.AddParameter("CelebrateIndex", AnimatorControllerParameterType.Int);

        controller.AddLayer("Base Layer");
        AnimatorControllerLayer[] baseLayers = controller.layers;
        baseLayers[0].defaultWeight = 1f;
        controller.layers = baseLayers;
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree moveTree, 0);
        moveTree.blendType = BlendTreeType.Simple1D;
        moveTree.blendParameter = "Speed";
        moveTree.useAutomaticThresholds = false;
        foreach (var move in moves.OrderBy(m => m.speed))
            moveTree.AddChild(move.clip, move.speed);
        AnimatorStateMachine root = controller.layers[0].stateMachine;
        root.defaultState = locomotion;
        for (int i = 0; i < fidgets.Count; i++)
        {
            AnimatorState fidget = root.AddState(fidgets[i].name, new Vector3(300f, 120f * i, 0f));
            fidget.motion = fidgets[i];
            AnimatorStateTransition enter = locomotion.AddTransition(fidget);
            enter.hasExitTime = false;
            enter.duration = 0.25f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Fidget");
            enter.AddCondition(AnimatorConditionMode.Equals, i, "FidgetIndex");
            enter.AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");
            AnimatorStateTransition done = fidget.AddTransition(locomotion);
            done.hasExitTime = true;
            done.exitTime = 0.9f;
            done.duration = 0.25f;
            // Setting off mid-fidget cuts it short.
            AnimatorStateTransition interrupted = fidget.AddTransition(locomotion);
            interrupted.hasExitTime = false;
            interrupted.duration = 0.2f;
            interrupted.AddCondition(AnimatorConditionMode.Greater, 0.3f, "Speed");
        }

        for (int i = 0; i < deaths.Count; i++)
        {
            AnimatorState death = root.AddState("Death_" + i, new Vector3(600f, 120f * i, 0f));
            death.motion = deaths[i];
            AnimatorStateTransition fall = root.AddAnyStateTransition(death);
            fall.hasExitTime = false;
            fall.duration = 0.15f;
            fall.canTransitionToSelf = false;
            fall.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            fall.AddCondition(AnimatorConditionMode.Equals, i, "DeathIndex");
            // Revived (a pooled enemy reused without a reset).
            AnimatorStateTransition up = death.AddTransition(locomotion);
            up.hasExitTime = false;
            up.duration = 0.2f;
            up.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        }

        AnimatorState combat = null;
        if (combatIdle != null)
        {
            combat = controller.CreateBlendTreeInController("Combat", out BlendTree combatTree, 0);
            combatTree.blendType = BlendTreeType.Simple1D;
            combatTree.blendParameter = "Speed";
            combatTree.useAutomaticThresholds = false;
            foreach (var move in moves.OrderBy(m => m.speed))
                combatTree.AddChild(move.speed <= 0f ? combatIdle : move.clip, move.speed);
            root.defaultState = locomotion;
            AnimatorStateTransition engage = locomotion.AddTransition(combat);
            engage.hasExitTime = false;
            engage.duration = 0.25f;
            engage.AddCondition(AnimatorConditionMode.If, 0f, "Combat");
            AnimatorStateTransition calm = combat.AddTransition(locomotion);
            calm.hasExitTime = false;
            calm.duration = 0.4f;
            calm.AddCondition(AnimatorConditionMode.IfNot, 0f, "Combat");
        }

        for (int i = 0; i < attacks.Count; i++)
        {
            AnimatorState attack = root.AddState("Attack_" + i, new Vector3(900f, 120f * i, 0f));
            attack.motion = attacks[i];
            attack.tag = "Attack";
            AnimatorStateTransition swing = root.AddAnyStateTransition(attack);
            swing.hasExitTime = false;
            swing.duration = 0.1f;
            swing.canTransitionToSelf = false;
            swing.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            swing.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
            AnimatorStateTransition done = attack.AddTransition(combat ?? locomotion);
            done.hasExitTime = true;
            done.exitTime = 0.9f;
            done.duration = 0.15f;
            if (combat == null)
                continue;
            done.AddCondition(AnimatorConditionMode.If, 0f, "Combat");
            AnimatorStateTransition calm = attack.AddTransition(locomotion);
            calm.hasExitTime = true;
            calm.exitTime = 0.9f;
            calm.duration = 0.15f;
            calm.AddCondition(AnimatorConditionMode.IfNot, 0f, "Combat");
        }

        if (appear != null)
        {
            AnimatorState arrive = root.AddState("Appear", new Vector3(300f, -150f, 0f));
            arrive.motion = appear;
            AnimatorStateTransition start = locomotion.AddTransition(arrive);
            start.hasExitTime = false;
            start.duration = 0.1f;
            start.AddCondition(AnimatorConditionMode.If, 0f, "Appear");
            AnimatorStateTransition done = arrive.AddTransition(locomotion);
            done.hasExitTime = true;
            done.exitTime = 0.9f;
            done.duration = 0.25f;
            // Setting off before the clip ends cuts it short.
            AnimatorStateTransition interrupted = arrive.AddTransition(locomotion);
            interrupted.hasExitTime = false;
            interrupted.duration = 0.25f;
            interrupted.AddCondition(AnimatorConditionMode.Greater, 0.3f, "Speed");
        }

        for (int i = 0; i < celebrations.Count; i++)
        {
            AnimatorState dance = root.AddState("Celebrate_" + i, new Vector3(300f, -300f - 120f * i, 0f));
            dance.motion = celebrations[i];
            AnimatorStateTransition begin = locomotion.AddTransition(dance);
            begin.hasExitTime = false;
            begin.duration = 0.3f;
            begin.AddCondition(AnimatorConditionMode.If, 0f, "Celebrate");
            begin.AddCondition(AnimatorConditionMode.Equals, i, "CelebrateIndex");
            begin.AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");
            AnimatorStateTransition again = dance.AddTransition(dance);
            again.hasExitTime = true;
            again.exitTime = 0.95f;
            again.duration = 0.2f;
            again.AddCondition(AnimatorConditionMode.If, 0f, "Celebrate");
            AnimatorStateTransition stop = dance.AddTransition(locomotion);
            stop.hasExitTime = false;
            stop.duration = 0.25f;
            stop.AddCondition(AnimatorConditionMode.IfNot, 0f, "Celebrate");
            AnimatorStateTransition walk = dance.AddTransition(locomotion);
            walk.hasExitTime = false;
            walk.duration = 0.25f;
            walk.AddCondition(AnimatorConditionMode.Greater, 0.3f, "Speed");
        }

        AddDirectionalLayer(controller, "HeadLook", headLook, positions, neutral, 1f);
        float bodyWeight = float.TryParse(Get("bodyLook.weight"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w) ? w : 0.5f;
        AddDirectionalLayer(controller, "BodyLook", bodyLook, positions, neutral, bodyWeight);
        if (lean[0] != null && lean[1] != null && neutral != null)
        {
            int index = AddAdditiveLayer(controller, "Lean", 1f);
            controller.CreateBlendTreeInController("Lean", out BlendTree tree, index);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Lean";
            tree.useAutomaticThresholds = false;
            tree.AddChild(lean[0], -1f);
            tree.AddChild(neutral, 0f);
            tree.AddChild(lean[1], 1f);
        }
        int hitStates = AddHitLayer(controller, hits, heavyHits, Get("hit.mask") != "full");
        if (grip != null)
        {
            controller.AddLayer("Grip");
            AnimatorControllerLayer[] layers = controller.layers;
            int index = layers.Length - 1;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index].defaultWeight = 1f;
            layers[index].avatarMask = gripMask;
            controller.layers = layers;
            AnimatorState hold = controller.layers[index].stateMachine.AddState("Grip");
            hold.motion = grip;
        }

        EditorUtility.SetDirty(controller);
        // Only what this wrote: SaveAssets() would also write unrelated dirty assets (e.g. the WorldMap terrain).
        AssetDatabase.SaveAssetIfDirty(controller);
        if (neutral != null)
            AssetDatabase.SaveAssetIfDirty(neutral);
        return $"{{\"ok\":true,\"controller\":{Json(controllerPath)},\"moves\":{moves.Count},\"fidgets\":{fidgets.Count},\"hits\":{hitStates},\"deaths\":{deaths.Count},\"attacks\":{attacks.Count},\"appear\":{B(appear != null)},\"celebrations\":{celebrations.Count},\"combatIdle\":{B(combatIdle != null)},\"layers\":{JsonList(controller.layers.Select(l => $"{l.name} ({l.blendingMode}, {l.defaultWeight})"))},\"neutral\":{Json(neutral ? AssetDatabase.GetAssetPath(neutral) : null)}}}";
    }

    // Override layer "Hit": an empty default state, and Hit_<index> entered from any state on Hit + HitIndex (a new hit
    // restarts it), back to empty near its end. `upper` masks it to the spine, head and arms.
    static int AddHitLayer(AnimatorController controller, AnimationClip[] light, AnimationClip[] heavy, bool upper)
    {
        if (light.All(c => c == null) && heavy.All(c => c == null))
            return 0;
        controller.AddLayer("Hit");
        AnimatorControllerLayer[] layers = controller.layers;
        int index = layers.Length - 1;
        layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[index].defaultWeight = 1f;
        if (upper)
        {
            var mask = new AvatarMask { name = "UpperBody" };
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, true);
            foreach (AvatarMaskBodyPart part in new[] { AvatarMaskBodyPart.Root, AvatarMaskBodyPart.LeftLeg, AvatarMaskBodyPart.RightLeg, AvatarMaskBodyPart.LeftFootIK, AvatarMaskBodyPart.RightFootIK })
                mask.SetHumanoidBodyPartActive(part, false);
            AssetDatabase.AddObjectToAsset(mask, controller);
            layers[index].avatarMask = mask;
        }
        controller.layers = layers;

        AnimatorStateMachine machine = controller.layers[index].stateMachine;
        AnimatorState empty = machine.AddState("Empty", new Vector3(300f, -120f, 0f));
        machine.defaultState = empty;
        int count = 0;
        for (int i = 0; i < 8; i++)
        {
            AnimationClip clip = i < 4 ? light[i] : heavy[i - 4] ?? light[i - 4];
            if (clip == null)
                continue;
            count++;
            AnimatorState state = machine.AddState("Hit_" + i, new Vector3(300f + 250f * (i / 4), 80f * (i % 4), 0f));
            state.motion = clip;
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.08f;
            enter.canTransitionToSelf = true;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            enter.AddCondition(AnimatorConditionMode.Equals, i, "HitIndex");
            AnimatorStateTransition done = state.AddTransition(empty);
            done.hasExitTime = true;
            done.exitTime = 0.85f;
            done.duration = 0.25f;
            // A hit while it blends out still lands (any-state transitions count as the source's).
            done.interruptionSource = TransitionInterruptionSource.Source;
        }
        return count;
    }

    static void AddDirectionalLayer(AnimatorController controller, string name, AnimationClip[] clips, Vector2[] positions, AnimationClip neutral, float weight)
    {
        if (neutral == null || clips.All(c => c == null))
            return;
        int index = AddAdditiveLayer(controller, name, weight);
        controller.CreateBlendTreeInController(name, out BlendTree tree, index);
        tree.blendType = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter = "LookX";
        tree.blendParameterY = "LookY";
        tree.AddChild(neutral, Vector2.zero);
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null)
                tree.AddChild(clips[i], positions[i]);
    }

    static int AddAdditiveLayer(AnimatorController controller, string name, float weight)
    {
        controller.AddLayer(name);
        AnimatorControllerLayer[] layers = controller.layers;
        int index = layers.Length - 1;
        layers[index].blendingMode = AnimatorLayerBlendingMode.Additive;
        layers[index].defaultWeight = weight;
        controller.layers = layers;
        return index;
    }

    // Empties an existing controller (keeping its GUID, so prefabs stay wired) or creates a new one with no layers.
    static AnimatorController ResetController(string path)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        }
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            if (sub != controller && sub != null)
                Object.DestroyImmediate(sub, true);
        controller.parameters = new AnimatorControllerParameter[0];
        controller.layers = new AnimatorControllerLayer[0];
        return controller;
    }

    // A one-frame copy of the clip's first frame: as an additive child it adds nothing (its delta to its own reference is 0).
    static AnimationClip SaveStaticPose(AnimationClip source, string path)
    {
        var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path), frameRate = source.frameRate };
        float end = 1f / Mathf.Max(1f, source.frameRate);
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            float value = AnimationUtility.GetEditorCurve(source, binding).Evaluate(0f);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, end, value));
        }
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        EditorUtility.CopySerialized(clip, existing);
        existing.name = clip.name;
        return existing;
    }

    static AnimationClip LoadClip(string path)
    {
        // "file.fbx" (first clip) or "file.fbx#ClipName"
        string[] parts = path.Split('#');
        var clips = AssetDatabase.LoadAllAssetsAtPath(parts[0]).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"));
        return parts.Length > 1 ? clips.FirstOrDefault(c => c.name == parts[1]) : clips.FirstOrDefault();
    }

    static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static string PathOf(Transform t, Transform root) => t == root ? "" : (t.parent == root ? t.name : PathOf(t.parent, root) + "/" + t.name);
    static string B(bool b) => b ? "true" : "false";
    static string Fail(string message) => $"{{\"ok\":false,\"error\":{Json(message)}}}";
    static string Json(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string JsonList(IEnumerable<string> items) => "[" + string.Join(",", items.Select(Json)) + "]";
}
