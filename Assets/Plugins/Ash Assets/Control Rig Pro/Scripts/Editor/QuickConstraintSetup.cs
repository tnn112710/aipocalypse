using UnityEngine;
using UnityEditor;
using UnityEngine.Animations.Rigging;
using System.Collections.Generic;
using System.Linq;
using AshTools.ControlRigPro.Constraints;

namespace AshTools.ControlRigPro
{
    public static class QuickConstraintSetup
    {
        private const string MENU_BASE = "CONTEXT/Transform/Control Rig Pro/";
        private const string GAMEOBJECT_MENU = "GameObject/Control Rig Pro/";
        private const string TOOLS_MENU = "Tools/Control Rig Pro/Quick Setup/";
        
        [MenuItem(MENU_BASE + "Add 2-Bone IK", false, 100)]
        [MenuItem(GAMEOBJECT_MENU + "Add 2-Bone IK", false, 100)]
        private static void Add2BoneIK(MenuCommand command)
        {
            // Check if we have exactly 3 objects selected
            if (Selection.transforms.Length == 3)
            {
                // For GameObject menu with multi-selection, only process on first object
                if (command != null && command.context != null)
                {
                    if (command.context as Transform != Selection.transforms[0])
                        return;
                }
                
                var bones = OrderBonesByHierarchy(Selection.transforms);
                if (bones.Count == 3)
                {
                    CreateTwoBoneIKSetup(bones[0], bones[1], bones[2]);
                    Debug.Log($"Created 2-Bone IK: {bones[0].name} → {bones[1].name} → {bones[2].name}");
                    return;
                }
            }
            
            // Fallback to single selection
            var tipBone = command.context as Transform ?? Selection.activeTransform;
            if (tipBone == null)
            {
                EditorUtility.DisplayDialog("2-Bone IK", "Select 3 objects: Root, Mid, Tip (use Ctrl+Click)\nOr select a single tip bone with parent hierarchy", "OK");
                return;
            }
            
            var midBone = tipBone.parent;
            if (midBone == null)
            {
                EditorUtility.DisplayDialog("Error", "Selected bone needs a parent (mid bone)", "OK");
                return;
            }
            
            var rootBone = midBone.parent;
            if (rootBone == null)
            {
                EditorUtility.DisplayDialog("Error", "Selected bone needs a grandparent (root bone)", "OK");
                return;
            }
            
            CreateTwoBoneIKSetup(rootBone, midBone, tipBone);
        }
        
        [MenuItem(MENU_BASE + "Add 2-Bone IK", true)]
        [MenuItem(GAMEOBJECT_MENU + "Add 2-Bone IK", true)]
        private static bool Validate2BoneIK()
        {
            // Valid if 3 bones selected
            if (Selection.transforms.Length == 3)
                return true;
            
            // Or if single bone with proper hierarchy
            return Selection.activeTransform != null && 
                   Selection.activeTransform.parent != null && 
                   Selection.activeTransform.parent.parent != null;
        }
        
        [MenuItem(MENU_BASE + "Add Multi-Parent Constraint", false, 101)]
        [MenuItem(GAMEOBJECT_MENU + "Add Multi-Parent Constraint", false, 101)]
        private static void AddMultiParent(MenuCommand command)
        {
            var bone = command.context as Transform ?? Selection.activeTransform;
            if (bone == null)
            {
                EditorUtility.DisplayDialog("Multi-Parent Constraint", "Please select a bone in the hierarchy", "OK");
                return;
            }
            
            CreateMultiParentSetup(bone);
        }
        
        [MenuItem(MENU_BASE + "Add Multi-Aim Constraint", false, 102)]
        [MenuItem(GAMEOBJECT_MENU + "Add Multi-Aim Constraint", false, 102)]
        private static void AddMultiAim(MenuCommand command)
        {
            var bone = command.context as Transform ?? Selection.activeTransform;
            if (bone == null)
            {
                EditorUtility.DisplayDialog("Multi-Aim Constraint", "Please select a bone in the hierarchy", "OK");
                return;
            }
            
            CreateMultiAimSetup(bone);
        }
        
        [MenuItem(MENU_BASE + "Add Twist Chain", false, 103)]
        [MenuItem(GAMEOBJECT_MENU + "Add Twist Chain", false, 103)]
        private static void AddTwistChain(MenuCommand command)
        {
            // Check if we have exactly 2 objects selected
            if (Selection.transforms.Length == 2)
            {
                // For GameObject menu with multi-selection, only process on first object
                if (command != null && command.context != null)
                {
                    if (command.context as Transform != Selection.transforms[0])
                        return;
                }
                
                // Rest of the 2-selection logic continues below
            }
            
            if (Selection.gameObjects.Length == 2)
            {
                Transform root = null;
                Transform tip = null;
                
                // Determine which is root and which is tip based on hierarchy
                Transform first = Selection.gameObjects[0].transform;
                Transform second = Selection.gameObjects[1].transform;
                
                // Check if one is parent of the other
                if (IsParentOf(first, second))
                {
                    root = first;
                    tip = second;
                }
                else if (IsParentOf(second, first))
                {
                    root = second;
                    tip = first;
                }
                else
                {
                    // Neither is parent of the other, use hierarchy depth
                    int depthFirst = GetDepth(first);
                    int depthSecond = GetDepth(second);
                    
                    if (depthFirst < depthSecond)
                    {
                        root = first;
                        tip = second;
                    }
                    else
                    {
                        root = second;
                        tip = first;
                    }
                }
                
                CreateTwistChainSetup(root, tip);
                Debug.Log($"Created Twist Chain: {root.name} → {tip.name}");
                return;
            }
            
            // Fallback to single selection
            var tipBone = command.context as Transform ?? Selection.activeTransform;
            if (tipBone == null)
            {
                EditorUtility.DisplayDialog("Twist Chain", "Select 2 objects: Root (Ctrl+Click) Tip\nOr select a single bone with parent", "OK");
                return;
            }
            
            var rootBone = tipBone.parent;
            if (rootBone == null)
            {
                EditorUtility.DisplayDialog("Error", "Selected bone needs a parent for twist chain", "OK");
                return;
            }
            
            CreateTwistChainSetup(rootBone, tipBone);
        }
        
        [MenuItem(MENU_BASE + "Add Chain IK", false, 104)]
        [MenuItem(GAMEOBJECT_MENU + "Add Chain IK", false, 104)]
        private static void AddChainIK(MenuCommand command)
        {
            // Check if we have exactly 2 objects selected
            if (Selection.transforms.Length == 2)
            {
                // For GameObject menu with multi-selection, only process on first object
                if (command != null && command.context != null)
                {
                    if (command.context as Transform != Selection.transforms[0])
                        return;
                }
                
                // Rest of the 2-selection logic continues below
            }
            
            if (Selection.gameObjects.Length == 2)
            {
                Transform root = null;
                Transform tip = null;
                
                // Determine which is root and which is tip based on hierarchy
                Transform first = Selection.gameObjects[0].transform;
                Transform second = Selection.gameObjects[1].transform;
                
                // Check if one is parent of the other
                if (IsParentOf(first, second))
                {
                    root = first;
                    tip = second;
                }
                else if (IsParentOf(second, first))
                {
                    root = second;
                    tip = first;
                }
                else
                {
                    // Neither is parent of the other, use hierarchy depth
                    int depthFirst = GetDepth(first);
                    int depthSecond = GetDepth(second);
                    
                    if (depthFirst < depthSecond)
                    {
                        root = first;
                        tip = second;
                    }
                    else
                    {
                        root = second;
                        tip = first;
                    }
                }
                
                CreateChainIKSetup(root, tip);
                Debug.Log($"Created Chain IK: {root.name} → {tip.name}");
                return;
            }
            
            // Fallback to single selection
            var tipBone = command.context as Transform ?? Selection.activeTransform;
            if (tipBone == null)
            {
                EditorUtility.DisplayDialog("Chain IK", "Select 2 objects: Root (Ctrl+Click) Tip\nOr select a single bone with parents", "OK");
                return;
            }
            
            var rootBone = FindChainRoot(tipBone);
            if (rootBone == tipBone)
            {
                EditorUtility.DisplayDialog("Error", "Selected bone needs parent bones for chain IK", "OK");
                return;
            }
            
            CreateChainIKSetup(rootBone, tipBone);
        }
        
        [MenuItem(MENU_BASE + "Add Damped Transform", false, 105)]
        [MenuItem(GAMEOBJECT_MENU + "Add Damped Transform", false, 105)]
        private static void AddDampedTransform(MenuCommand command)
        {
            var bone = command.context as Transform ?? Selection.activeTransform;
            if (bone == null)
            {
                EditorUtility.DisplayDialog("Damped Transform", "Please select a bone in the hierarchy", "OK");
                return;
            }
            
            CreateDampedTransformSetup(bone);
        }
        
        [MenuItem(MENU_BASE + "Add Override Transform", false, 106)]
        [MenuItem(GAMEOBJECT_MENU + "Add Override Transform", false, 106)]
        private static void AddOverrideTransform(MenuCommand command)
        {
            var bone = command.context as Transform ?? Selection.activeTransform;
            if (bone == null)
            {
                EditorUtility.DisplayDialog("Override Transform", "Please select a bone in the hierarchy", "OK");
                return;
            }
            
            CreateOverrideTransformSetup(bone);
        }
        
        [MenuItem(MENU_BASE + "Add Multi-Rotation", false, 107)]
        [MenuItem(GAMEOBJECT_MENU + "Add Multi-Rotation", false, 107)]
        private static void AddMultiRotation(MenuCommand command)
        {
            var bone = command.context as Transform ?? Selection.activeTransform;
            if (bone == null)
            {
                EditorUtility.DisplayDialog("Multi-Rotation", "Please select a bone in the hierarchy", "OK");
                return;
            }
            
            CreateMultiRotationSetup(bone);
        }
        
        [MenuItem(MENU_BASE + "Add Multi-Position", false, 108)]
        [MenuItem(GAMEOBJECT_MENU + "Add Multi-Position", false, 108)]
        private static void AddMultiPosition(MenuCommand command)
        {
            var bone = command.context as Transform ?? Selection.activeTransform;
            if (bone == null)
            {
                EditorUtility.DisplayDialog("Multi-Position", "Please select a bone in the hierarchy", "OK");
                return;
            }
            
            CreateMultiPositionSetup(bone);
        }
        
        [MenuItem(MENU_BASE + "Add Jiggle Bone", false, 109)]
        [MenuItem(GAMEOBJECT_MENU + "Add Jiggle Bone", false, 109)]
        private static void AddJiggleBone(MenuCommand command)
        {
            // Support multi-selection for multiple jiggle bones
            if (Selection.transforms.Length > 1)
            {
                // For GameObject menu with multi-selection, only process on first object
                if (command != null && command.context != null)
                {
                    if (command.context as Transform != Selection.transforms[0])
                        return;
                }
                
                foreach (var bone in Selection.transforms)
                {
                    CreateJiggleBoneSetup(bone);
                }
                Debug.Log($"Created Jiggle Bone constraints for {Selection.transforms.Length} bones");
                return;
            }
            
            // Single selection
            var selectedBone = command.context as Transform ?? Selection.activeTransform;
            if (selectedBone == null)
            {
                EditorUtility.DisplayDialog("Jiggle Bone", "Please select a bone in the hierarchy", "OK");
                return;
            }
            
            CreateJiggleBoneSetup(selectedBone);
        }
        
        [MenuItem(MENU_BASE + "Add Jiggle Chain", false, 110)]
        [MenuItem(GAMEOBJECT_MENU + "Add Jiggle Chain", false, 110)]
        private static void AddJiggleChain(MenuCommand command)
        {
            // Check if we have multiple bones selected
            if (Selection.transforms.Length >= 2)
            {
                // For GameObject menu with multi-selection, only process on first object
                if (command != null && command.context != null)
                {
                    if (command.context as Transform != Selection.transforms[0])
                        return;
                }
                
                var bones = OrderBonesByHierarchy(Selection.transforms);
                if (bones.Count >= 2)
                {
                    CreateJiggleChainSetup(bones[0], bones[bones.Count - 1]);
                    Debug.Log($"Created Jiggle Chain: {bones[0].name} → {bones[bones.Count - 1].name}");
                    return;
                }
            }
            
            // Single selection - find chain from selection to tip
            var startBone = command?.context as Transform ?? Selection.activeTransform;
            if (startBone == null)
            {
                EditorUtility.DisplayDialog("Jiggle Chain", 
                    "Select multiple bones (Ctrl+Click) or a single bone with children", "OK");
                return;
            }
            
            // Find the tip bone (deepest child in chain)
            Transform tipBone = FindChainTip(startBone);
            if (tipBone == startBone)
            {
                EditorUtility.DisplayDialog("Jiggle Chain", 
                    "Selected bone needs children for a jiggle chain", "OK");
                return;
            }
            
            CreateJiggleChainSetup(startBone, tipBone);
        }
        
        [MenuItem(MENU_BASE + "Add Hand Finger Controls", false, 111)]
        [MenuItem(GAMEOBJECT_MENU + "Add Hand Finger Controls", false, 111)]
        private static void AddHandFingerControls(MenuCommand command)
        {
            var handBone = command.context as Transform ?? Selection.activeTransform;
            if (handBone == null)
            {
                EditorUtility.DisplayDialog("Hand Finger Controls", 
                    "Select a hand or wrist bone to set up finger controls", "OK");
                return;
            }
            
            CreateHandFingerSetup(handBone);
        }
        
        [MenuItem(MENU_BASE + "Add Tail Constraint", false, 112)]
        [MenuItem(GAMEOBJECT_MENU + "Add Tail Constraint", false, 112)]
        private static void AddTailConstraint(MenuCommand command)
        {
            // Unity calls this once per object when using GameObject menu with multi-selection
            // We use a static flag to track if we've already processed this frame
            if (Selection.transforms.Length >= 2)
            {
                // For GameObject menu with multi-selection
                if (command != null && command.context != null)
                {
                    // Only process on the first selected object
                    if (command.context as Transform != Selection.transforms[0])
                        return;
                }
                
                var bones = OrderBonesByHierarchy(Selection.transforms);
                if (bones.Count >= 2)
                {
                    CreateTailConstraintSetup(bones);
                    Debug.Log($"Created Tail Constraint for {bones.Count} bones");
                    return;
                }
            }
            
            // Single selection - find chain from selection to tip
            var startBone = command?.context as Transform ?? Selection.activeTransform;
            if (startBone == null)
            {
                EditorUtility.DisplayDialog("Tail Constraint", 
                    "Select multiple tail bones (Ctrl+Click) or a single tail base bone", "OK");
                return;
            }
            
            // Find all children in the tail chain
            var tailBones = new List<Transform> { startBone };
            Transform current = startBone;
            while (current.childCount > 0)
            {
                // Follow the first child (assumes single chain)
                current = current.GetChild(0);
                tailBones.Add(current);
            }
            
            if (tailBones.Count < 2)
            {
                EditorUtility.DisplayDialog("Tail Constraint", 
                    "Need at least 2 bones for a tail constraint", "OK");
                return;
            }
            
            CreateTailConstraintSetup(tailBones);
        }
        
        private static void AutoCreateConstraintForChain(LimbChain chain)
        {
            if (chain.Bones.Count < 2) return;
            
            switch (chain.Type)
            {
                case LimbType.Arm:
                case LimbType.Leg:
                    if (chain.Bones.Count >= 3)
                    {
                        CreateTwoBoneIKSetup(chain.Bones[0], chain.Bones[1], chain.Bones[2]);
                    }
                    break;
                    
                case LimbType.Wing:
                    CreateWingSetup(chain.Bones);
                    break;
                    
                case LimbType.Tail:
                case LimbType.Tentacle:
                    CreateChainIKSetup(chain.Bones[0], chain.Bones[chain.Bones.Count - 1]);
                    break;
                    
                case LimbType.Spine:
                    CreateTwistChainSetup(chain.Bones[0], chain.Bones[chain.Bones.Count - 1]);
                    break;
                    
                case LimbType.Neck:
                    CreateMultiAimSetup(chain.Bones[chain.Bones.Count - 1]);
                    break;
                    
                case LimbType.Finger:
                case LimbType.Toe:
                    foreach (var bone in chain.Bones)
                    {
                        CreateMultiRotationSetup(bone);
                    }
                    break;
            }
        }
        
        private static void CreateHandFingerSetup(Transform handBone)
        {
            var character = GetCharacterRoot(handBone);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            bool isLeft = handBone.name.ToLower().Contains("left") || handBone.name.ToLower().StartsWith("l_") || 
                         handBone.name.ToLower().StartsWith("l ") || handBone.position.x < 0;
            bool isRight = handBone.name.ToLower().Contains("right") || handBone.name.ToLower().StartsWith("r_") || 
                          handBone.name.ToLower().StartsWith("r ") || handBone.position.x > 0;
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            
            GameObject handRigGroup = new GameObject($"{sidePrefix} Hand Finger Rig");
            handRigGroup.transform.parent = rigRoot.transform;
            handRigGroup.transform.position = handBone.position;
            handRigGroup.transform.rotation = handBone.rotation;
            
            // Add RigTransform component to ensure proper rig evaluation
            handRigGroup.AddComponent<RigTransform>();
            
            var fingerChains = DetectFingerChains(handBone);
            
            if (fingerChains.Count == 0)
            {
                EditorUtility.DisplayDialog("No Fingers Found", 
                    "Could not detect finger bones. Make sure the selected bone is a hand/wrist with finger children.", "OK");
                GameObject.DestroyImmediate(handRigGroup);
                return;
            }
            
            // Add parent constraint to follow hand bone
            var parentConstraint = handRigGroup.AddComponent<UnityEngine.Animations.ParentConstraint>();
            parentConstraint.AddSource(new UnityEngine.Animations.ConstraintSource
            {
                sourceTransform = handBone,
                weight = 1f
            });
            parentConstraint.constraintActive = true;
            parentConstraint.locked = true;
            parentConstraint.translationAtRest = handRigGroup.transform.localPosition;
            parentConstraint.rotationAtRest = handRigGroup.transform.localRotation.eulerAngles;
            
            GameObject fingerControlsGroup = new GameObject("Finger Controls");
            fingerControlsGroup.transform.parent = handRigGroup.transform;
            fingerControlsGroup.transform.localPosition = Vector3.zero;
            fingerControlsGroup.transform.localRotation = Quaternion.identity;
            
            int totalControls = 0;
            
            foreach (var finger in fingerChains)
            {
                if (finger.Value.Count < 2) continue;
                
                Transform rootBone = finger.Value[0];
                Transform tipBone = finger.Value[finger.Value.Count - 1];
                
                GameObject fingerConstraint = new GameObject($"{finger.Key} Chain");
                fingerConstraint.transform.parent = fingerControlsGroup.transform;
                fingerConstraint.transform.localPosition = Vector3.zero;
                fingerConstraint.transform.localRotation = Quaternion.identity;
                
                GameObject target = new GameObject($"{finger.Key} Target");
                target.transform.parent = fingerConstraint.transform;
                target.transform.position = tipBone.position;
                target.transform.localRotation = Quaternion.identity;
                
                var constraint = fingerConstraint.AddComponent<ChainIKConstraint>();
                constraint.data.root = rootBone;
                constraint.data.tip = tipBone;
                constraint.data.target = target.transform;
                constraint.data.maintainTargetPositionOffset = false;
                constraint.data.maintainTargetRotationOffset = true;
                constraint.data.chainRotationWeight = 1f;
                constraint.data.tipRotationWeight = 1f;
                
                AddRigEffectors(rigRoot, target.transform);
                totalControls++;
            }
            
            RefreshRigBuilder(rigBuilder);
            Selection.activeGameObject = handRigGroup;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created {totalControls} Chain IK controls for {sidePrefix} hand fingers with parent constraint to {handBone.name}");
        }
        
        private static void CreateTailConstraintSetup(List<Transform> tailBones)
        {
            if (tailBones.Count < 2) return;
            
            var character = GetCharacterRoot(tailBones[0]);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string tailName = GetCleanBoneName(tailBones[0].name);
            GameObject tailRigGroup = new GameObject($"{tailName} Damped Tail");
            tailRigGroup.transform.parent = rigRoot.transform;
            tailRigGroup.transform.localPosition = Vector3.zero;
            tailRigGroup.transform.localRotation = Quaternion.identity;
            
            // Add RigTransform for proper evaluation
            tailRigGroup.AddComponent<RigTransform>();
            
            // Create damped transform for each bone following the previous one
            for (int i = 1; i < tailBones.Count; i++) // Start from second bone
            {
                Transform bone = tailBones[i];
                Transform sourceBone = tailBones[i - 1]; // Previous bone is the source
                
                // Progressive damping - less damping at base, more at tip
                float dampingFactor = Mathf.Lerp(0.2f, 0.8f, (float)i / (tailBones.Count - 1));
                
                GameObject dampedObj = new GameObject($"damp{i}");
                dampedObj.transform.parent = tailRigGroup.transform;
                dampedObj.transform.localPosition = Vector3.zero;
                dampedObj.transform.localRotation = Quaternion.identity;
                
                var constraint = dampedObj.AddComponent<DampedTransform>();
                constraint.data.constrainedObject = bone;
                constraint.data.sourceObject = sourceBone; // Follow the previous bone
                constraint.data.dampPosition = dampingFactor;
                constraint.data.dampRotation = dampingFactor;
                constraint.data.maintainAim = true; // Maintain aim for natural tail movement
            }
            
            RefreshRigBuilder(rigBuilder);
            Selection.activeGameObject = tailRigGroup;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Damped Tail with {tailBones.Count - 1} damped constraints");
        }
        
        private static Dictionary<string, List<Transform>> DetectFingerChains(Transform handBone)
        {
            var fingerChains = new Dictionary<string, List<Transform>>
            {
                { "Thumb", new List<Transform>() },
                { "Index", new List<Transform>() },
                { "Middle", new List<Transform>() },
                { "Ring", new List<Transform>() },
                { "Pinky", new List<Transform>() }
            };
            
            var allChildren = handBone.GetComponentsInChildren<Transform>();
            
            foreach (var bone in allChildren)
            {
                if (bone == handBone) continue;
                
                string boneName = bone.name.ToLower();
                
                if (boneName.Contains("thumb"))
                {
                    fingerChains["Thumb"].Add(bone);
                }
                else if (boneName.Contains("index"))
                {
                    fingerChains["Index"].Add(bone);
                }
                else if (boneName.Contains("middle"))
                {
                    fingerChains["Middle"].Add(bone);
                }
                else if (boneName.Contains("ring"))
                {
                    fingerChains["Ring"].Add(bone);
                }
                else if (boneName.Contains("pinky") || boneName.Contains("little"))
                {
                    fingerChains["Pinky"].Add(bone);
                }
            }
            
            foreach (var finger in fingerChains)
            {
                finger.Value.Sort((a, b) => GetDepth(a).CompareTo(GetDepth(b)));
                
                if (finger.Value.Count > 1)
                {
                    var validChain = new List<Transform>();
                    for (int i = 0; i < finger.Value.Count; i++)
                    {
                        if (i == 0)
                        {
                            validChain.Add(finger.Value[i]);
                        }
                        else if (IsParentOf(finger.Value[i - 1], finger.Value[i]))
                        {
                            validChain.Add(finger.Value[i]);
                        }
                        else
                        {
                            break;
                        }
                    }
                    finger.Value.Clear();
                    finger.Value.AddRange(validChain);
                }
            }
            
            var nonEmptyChains = new Dictionary<string, List<Transform>>();
            foreach (var finger in fingerChains)
            {
                if (finger.Value.Count >= 2)
                {
                    nonEmptyChains[finger.Key] = finger.Value;
                }
            }
            
            return nonEmptyChains;
        }
        
        private static void CreateWingSetup(List<Transform> bones)
        {
            if (bones.Count < 2) return;
            
            var character = GetCharacterRoot(bones[0]);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(bones[0].name);
            bool isLeft = bones[0].name.ToLower().Contains("left") || bones[0].name.ToLower().StartsWith("l_");
            bool isRight = bones[0].name.ToLower().Contains("right") || bones[0].name.ToLower().StartsWith("r_");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} Wing" : "Wing";
            
            GameObject wingGroup = new GameObject($"{constraintName} Controls");
            wingGroup.transform.parent = rigRoot.transform;
            
            foreach (var bone in bones)
            {
                GameObject control = new GameObject($"{bone.name} Control");
                control.transform.parent = wingGroup.transform;
                control.transform.position = bone.position;
                control.transform.localRotation = Quaternion.identity;
                
                var constraint = wingGroup.AddComponent<OverrideTransform>();
                constraint.data.constrainedObject = bone;
                constraint.data.sourceObject = control.transform;
                constraint.data.rotationWeight = 1f;
                
                AddRigEffectors(rigRoot, control.transform);
            }
            
            RefreshRigBuilder(rigBuilder);
            Selection.activeGameObject = wingGroup;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created wing controls for {constraintName}");
        }
        
        private static void CreateDampedTransformSetup(Transform bone)
        {
            var character = GetCharacterRoot(bone);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(bone.name);
            bool isLeft = bone.name.ToLower().Contains("left") || bone.name.ToLower().StartsWith("l_");
            bool isRight = bone.name.ToLower().Contains("right") || bone.name.ToLower().StartsWith("r_");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            
            GameObject constraintObject = new GameObject($"{constraintName} Damped");
            constraintObject.transform.parent = rigRoot.transform;
            
            GameObject control = new GameObject($"{constraintName} Damped Control");
            control.transform.parent = constraintObject.transform;
            control.transform.position = bone.position;
            control.transform.localRotation = Quaternion.identity;
            
            var constraint = constraintObject.AddComponent<DampedTransform>();
            constraint.data.constrainedObject = bone;
            constraint.data.sourceObject = control.transform;
            constraint.data.dampPosition = 0.5f;
            constraint.data.dampRotation = 0.5f;
            
            RefreshRigBuilder(rigBuilder);
            AddRigEffectors(rigRoot, control.transform);
            
            Selection.activeGameObject = control;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Damped Transform for {constraintName}");
        }
        
        private static void CreateOverrideTransformSetup(Transform bone)
        {
            var character = GetCharacterRoot(bone);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(bone.name);
            bool isLeft = bone.name.ToLower().Contains("left") || bone.name.ToLower().StartsWith("l_");
            bool isRight = bone.name.ToLower().Contains("right") || bone.name.ToLower().StartsWith("r_");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            
            GameObject constraintObject = new GameObject($"{constraintName} Override");
            constraintObject.transform.parent = rigRoot.transform;
            
            GameObject control = new GameObject($"{constraintName} Override Control");
            control.transform.parent = constraintObject.transform;
            control.transform.position = bone.position;
            control.transform.localRotation = Quaternion.identity;
            
            var constraint = constraintObject.AddComponent<OverrideTransform>();
            constraint.data.constrainedObject = bone;
            constraint.data.sourceObject = control.transform;
            constraint.data.positionWeight = 1f;
            constraint.data.rotationWeight = 1f;
            
            RefreshRigBuilder(rigBuilder);
            AddRigEffectors(rigRoot, control.transform);
            
            Selection.activeGameObject = control;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Override Transform for {constraintName}");
        }
        
        private static void CreateMultiRotationSetup(Transform bone)
        {
            var character = GetCharacterRoot(bone);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(bone.name);
            bool isLeft = bone.name.ToLower().Contains("left") || bone.name.ToLower().StartsWith("l_");
            bool isRight = bone.name.ToLower().Contains("right") || bone.name.ToLower().StartsWith("r_");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            
            GameObject constraintObject = new GameObject($"{constraintName} Rotation");
            constraintObject.transform.parent = rigRoot.transform;
            
            GameObject control = new GameObject($"{constraintName} Rotation Control");
            control.transform.parent = constraintObject.transform;
            control.transform.position = bone.position;
            control.transform.localRotation = Quaternion.identity;
            
            var constraint = constraintObject.AddComponent<MultiRotationConstraint>();
            constraint.data.constrainedObject = bone;
            var sourceObjects = new WeightedTransformArray(1);
            sourceObjects.SetTransform(0, control.transform);
            sourceObjects.SetWeight(0, 1f);
            constraint.data.sourceObjects = sourceObjects;
            
            RefreshRigBuilder(rigBuilder);
            AddRigEffectors(rigRoot, control.transform);
            
            Selection.activeGameObject = control;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Multi-Rotation for {constraintName}");
        }
        
        private static void CreateMultiPositionSetup(Transform bone)
        {
            var character = GetCharacterRoot(bone);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(bone.name);
            bool isLeft = bone.name.ToLower().Contains("left") || bone.name.ToLower().StartsWith("l_");
            bool isRight = bone.name.ToLower().Contains("right") || bone.name.ToLower().StartsWith("r_");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            
            GameObject constraintObject = new GameObject($"{constraintName} Position");
            constraintObject.transform.parent = rigRoot.transform;
            
            GameObject control = new GameObject($"{constraintName} Position Control");
            control.transform.parent = constraintObject.transform;
            control.transform.position = bone.position;
            control.transform.localRotation = Quaternion.identity;
            
            var constraint = constraintObject.AddComponent<MultiPositionConstraint>();
            constraint.data.constrainedObject = bone;
            var sourceObjects = new WeightedTransformArray(1);
            sourceObjects.SetTransform(0, control.transform);
            sourceObjects.SetWeight(0, 1f);
            constraint.data.sourceObjects = sourceObjects;
            
            RefreshRigBuilder(rigBuilder);
            AddRigEffectors(rigRoot, control.transform);
            
            Selection.activeGameObject = control;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Multi-Position for {constraintName}");
        }
        
        private static void CreateJiggleBoneSetup(Transform bone)
        {
            var character = GetCharacterRoot(bone);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(bone.name);
            bool isLeft = bone.name.ToLower().Contains("left") || bone.name.ToLower().StartsWith("l_");
            bool isRight = bone.name.ToLower().Contains("right") || bone.name.ToLower().StartsWith("r_");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            
            GameObject constraintObject = new GameObject($"{constraintName} Jiggle");
            constraintObject.transform.parent = rigRoot.transform;
            constraintObject.transform.localPosition = Vector3.zero;
            constraintObject.transform.localRotation = Quaternion.identity;
            
            // Add RigTransform for proper evaluation
            constraintObject.AddComponent<RigTransform>();
            
            var constraint = constraintObject.AddComponent<JiggleBoneConstraint>();
            constraint.data.bone = bone;
            constraint.data.reference = bone.parent; // Use parent as reference for stable simulation
            constraint.data.stiffness = 40f;
            constraint.data.damping = 8f;
            constraint.data.mass = 1f;
            constraint.data.maxOffset = 0.5f; // Limit maximum offset for stability
            constraint.data.gravity = Physics.gravity; // Use Unity's gravity setting
            
            RefreshRigBuilder(rigBuilder);
            Selection.activeGameObject = constraintObject;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Jiggle Bone for {constraintName}");
        }
        
        private static void CreateJiggleChainSetup(Transform rootBone, Transform tipBone)
        {
            var character = GetCharacterRoot(rootBone);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(rootBone.name);
            bool isLeft = rootBone.name.ToLower().Contains("left") || rootBone.name.ToLower().StartsWith("l_");
            bool isRight = rootBone.name.ToLower().Contains("right") || rootBone.name.ToLower().StartsWith("r_");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            
            GameObject constraintObject = new GameObject($"{constraintName} Jiggle Chain");
            constraintObject.transform.parent = rigRoot.transform;
            constraintObject.transform.localPosition = Vector3.zero;
            constraintObject.transform.localRotation = Quaternion.identity;
            
            // Add RigTransform for proper evaluation
            constraintObject.AddComponent<RigTransform>();
            
            var constraint = constraintObject.AddComponent<JiggleChainConstraint>();
            constraint.data.rootBone = rootBone;
            constraint.data.tipBone = tipBone;
            constraint.data.mass = 1f;
            constraint.data.stiffness = 0.5f;
            constraint.data.damping = 0.1f;
            constraint.data.dynamicOffset = 4f;
            constraint.data.aimLocalVec = Vector3.right; // Default aim direction
            constraint.data.upLocalVec = Vector3.up; // Default up direction
            constraint.data.rollEnabled = false;
            constraint.data.gravity = Physics.gravity;
            constraint.data.externalForce = Vector3.zero;
            constraint.data.motionDecay = 0.1f;
            
            RefreshRigBuilder(rigBuilder);
            Selection.activeGameObject = constraintObject;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Jiggle Chain for {constraintName}: {rootBone.name} → {tipBone.name}");
        }
        
        private static void CreateTwoBoneIKSetup(Transform root, Transform mid, Transform tip)
        {
            var character = GetCharacterRoot(root);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(tip.name);
            bool isArm = tip.name.ToLower().Contains("hand") || tip.name.ToLower().Contains("wrist") || tip.name.ToLower().Contains("arm");
            bool isLeg = tip.name.ToLower().Contains("foot") || tip.name.ToLower().Contains("ankle") || tip.name.ToLower().Contains("leg");
            bool isLeft = tip.name.ToLower().Contains("left") || tip.name.ToLower().StartsWith("l_") || tip.name.ToLower().StartsWith("l ");
            bool isRight = tip.name.ToLower().Contains("right") || tip.name.ToLower().StartsWith("r_") || tip.name.ToLower().StartsWith("r ");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            
            // Create or get body part group with side prefix
            GameObject bodyPartGroup = null;
            if (isArm)
            {
                string groupName = sidePrefix != "" ? $"{sidePrefix} Hand Rig" : "Hand Rig";
                bodyPartGroup = rigRoot.transform.Find(groupName)?.gameObject;
                if (bodyPartGroup == null)
                {
                    bodyPartGroup = new GameObject(groupName);
                    bodyPartGroup.transform.parent = rigRoot.transform;
                    bodyPartGroup.transform.localPosition = Vector3.zero;
                    bodyPartGroup.transform.localRotation = Quaternion.identity;
                }
            }
            else if (isLeg)
            {
                string groupName = sidePrefix != "" ? $"{sidePrefix} Leg Rig" : "Leg Rig";
                bodyPartGroup = rigRoot.transform.Find(groupName)?.gameObject;
                if (bodyPartGroup == null)
                {
                    bodyPartGroup = new GameObject(groupName);
                    bodyPartGroup.transform.parent = rigRoot.transform;
                    bodyPartGroup.transform.localPosition = Vector3.zero;
                    bodyPartGroup.transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                bodyPartGroup = rigRoot;
            }
            
            // Create constraint container with proper naming
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            GameObject constraintContainer = new GameObject(constraintName);
            constraintContainer.transform.parent = bodyPartGroup.transform;
            constraintContainer.transform.localPosition = Vector3.zero;
            constraintContainer.transform.localRotation = Quaternion.identity;
            
            // Add RigTransform for proper rig evaluation
            constraintContainer.AddComponent<RigTransform>();
            
            // Create controls as children of constraint container
            GameObject ikTarget = new GameObject($"{constraintName} Target");
            ikTarget.transform.parent = constraintContainer.transform;
            ikTarget.transform.position = tip.position;
            ikTarget.transform.localRotation = Quaternion.identity;
            
            Vector3 hintPosition = CalculateOptimalHintPosition(root, mid, tip);
            GameObject ikHint = new GameObject($"{constraintName} Hint");
            ikHint.transform.parent = constraintContainer.transform;
            ikHint.transform.position = hintPosition;
            ikHint.transform.localRotation = Quaternion.identity;
            
            var ikConstraint = constraintContainer.AddComponent<TwoBoneIKConstraint>();
            ikConstraint.data.root = root;
            ikConstraint.data.mid = mid;
            ikConstraint.data.tip = tip;
            ikConstraint.data.target = ikTarget.transform;
            ikConstraint.data.hint = ikHint.transform;
            ikConstraint.data.targetPositionWeight = 1f;
            ikConstraint.data.targetRotationWeight = 1f;
            ikConstraint.data.hintWeight = 1f;
            ikConstraint.data.maintainTargetPositionOffset = false;
            ikConstraint.data.maintainTargetRotationOffset = true;
            
            RefreshRigBuilder(rigBuilder);
            AddRigEffectors(rigRoot, ikTarget.transform, ikHint.transform);
            
            Selection.activeGameObject = ikTarget;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created 2-Bone IK for {constraintName}: {root.name} → {mid.name} → {tip.name}");
        }
        
        private static void CreateMultiParentSetup(Transform bone)
        {
            var character = GetCharacterRoot(bone);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(bone.name);
            bool isHip = bone.name.ToLower().Contains("hip") || bone.name.ToLower().Contains("pelvis");
            bool isSpine = bone.name.ToLower().Contains("spine") || bone.name.ToLower().Contains("chest");
            bool isLeft = bone.name.ToLower().Contains("left") || bone.name.ToLower().StartsWith("l_") || bone.name.ToLower().StartsWith("l ");
            bool isRight = bone.name.ToLower().Contains("right") || bone.name.ToLower().StartsWith("r_") || bone.name.ToLower().StartsWith("r ");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            
            // Create proper group
            GameObject parentGroup = rigRoot;
            if (isHip)
            {
                // Hip goes directly under rig root
                parentGroup = rigRoot;
            }
            else if (isSpine)
            {
                GameObject spineRig = rigRoot.transform.Find("Spine Rig")?.gameObject;
                if (spineRig == null)
                {
                    spineRig = new GameObject("Spine Rig");
                    spineRig.transform.parent = rigRoot.transform;
                    spineRig.transform.localPosition = Vector3.zero;
                    spineRig.transform.localRotation = Quaternion.identity;
                }
                parentGroup = spineRig;
            }
            
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            GameObject constraintObject = new GameObject($"{constraintName} Rig");
            constraintObject.transform.parent = parentGroup.transform;
            constraintObject.transform.localPosition = Vector3.zero;
            constraintObject.transform.localRotation = Quaternion.identity;
            
            GameObject control = new GameObject(isHip ? "Hip target" : $"{constraintName} Control");
            control.transform.parent = constraintObject.transform;
            control.transform.position = bone.position;
            control.transform.localRotation = Quaternion.identity;
            
            var constraint = constraintObject.AddComponent<MultiParentConstraint>();
            constraint.data.constrainedObject = bone;
            var sourceObjects = new WeightedTransformArray(1);
            sourceObjects.SetTransform(0, control.transform);
            sourceObjects.SetWeight(0, 1f);
            constraint.data.sourceObjects = sourceObjects;
            constraint.data.maintainPositionOffset = false;
            constraint.data.maintainRotationOffset = false;
            
            RefreshRigBuilder(rigBuilder);
            AddRigEffectors(rigRoot, control.transform);
            
            Selection.activeGameObject = control;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Multi-Parent constraint for {constraintName}");
        }
        
        private static void CreateMultiAimSetup(Transform bone)
        {
            var character = GetCharacterRoot(bone);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(bone.name);
            bool isHead = bone.name.ToLower().Contains("head");
            bool isLeft = bone.name.ToLower().Contains("left") || bone.name.ToLower().StartsWith("l_") || bone.name.ToLower().StartsWith("l ");
            bool isRight = bone.name.ToLower().Contains("right") || bone.name.ToLower().StartsWith("r_") || bone.name.ToLower().StartsWith("r ");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            
            GameObject constraintObject = new GameObject(isHead ? "Head Aim Rig" : $"{constraintName} Aim Rig");
            constraintObject.transform.parent = rigRoot.transform;
            constraintObject.transform.localPosition = Vector3.zero;
            constraintObject.transform.localRotation = Quaternion.identity;
            
            GameObject aimTarget = new GameObject(isHead ? "Head LookAt Target" : $"{constraintName} Aim Target");
            aimTarget.transform.parent = constraintObject.transform;
            aimTarget.transform.position = bone.position + bone.forward * 1f;
            aimTarget.transform.localRotation = Quaternion.identity;
            
            var constraint = constraintObject.AddComponent<MultiAimConstraint>();
            constraint.data.constrainedObject = bone;
            var sourceObjects = new WeightedTransformArray(1);
            sourceObjects.SetTransform(0, aimTarget.transform);
            sourceObjects.SetWeight(0, 1f);
            constraint.data.sourceObjects = sourceObjects;
            constraint.data.aimAxis = MultiAimConstraintData.Axis.Z;
            constraint.data.upAxis = MultiAimConstraintData.Axis.Y;
            constraint.data.maintainOffset = false;
            
            RefreshRigBuilder(rigBuilder);
            AddRigEffectors(rigRoot, aimTarget.transform);
            
            Selection.activeGameObject = aimTarget;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Multi-Aim constraint for {constraintName}");
        }
        
        private static void CreateTwistChainSetup(Transform root, Transform tip)
        {
            var character = GetCharacterRoot(root);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(root.name);
            bool isSpine = root.name.ToLower().Contains("spine") || root.name.ToLower().Contains("chest");
            bool isLeft = root.name.ToLower().Contains("left") || root.name.ToLower().StartsWith("l_") || root.name.ToLower().StartsWith("l ");
            bool isRight = root.name.ToLower().Contains("right") || root.name.ToLower().StartsWith("r_") || root.name.ToLower().StartsWith("r ");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            
            GameObject parentGroup = rigRoot;
            if (isSpine)
            {
                GameObject spineRig = rigRoot.transform.Find("Spine Rig")?.gameObject;
                if (spineRig == null)
                {
                    spineRig = new GameObject("Spine Rig");
                    spineRig.transform.parent = rigRoot.transform;
                    spineRig.transform.localPosition = Vector3.zero;
                    spineRig.transform.localRotation = Quaternion.identity;
                }
                parentGroup = spineRig;
            }
            
            GameObject constraintObject = new GameObject(isSpine ? "Twist Spine Rig" : $"{constraintName} Twist Chain");
            constraintObject.transform.parent = parentGroup.transform;
            constraintObject.transform.localPosition = Vector3.zero;
            constraintObject.transform.localRotation = Quaternion.identity;
            
            GameObject rootControl = new GameObject(isSpine ? "Spine Root Target" : $"{constraintName} Root");
            rootControl.transform.parent = constraintObject.transform;
            rootControl.transform.position = root.position;
            rootControl.transform.localRotation = Quaternion.identity;
            
            GameObject tipControl = new GameObject(isSpine ? "Spine Tip Target" : $"{constraintName} Tip");
            tipControl.transform.parent = constraintObject.transform;
            tipControl.transform.position = tip.position;
            tipControl.transform.localRotation = Quaternion.identity;
            
            var constraint = constraintObject.AddComponent<TwistChainConstraint>();
            constraint.data.root = root;
            constraint.data.tip = tip;
            constraint.data.rootTarget = rootControl.transform;
            constraint.data.tipTarget = tipControl.transform;
            constraint.data.curve = AnimationCurve.Linear(0, 0, 1, 1);
            
            RefreshRigBuilder(rigBuilder);
            AddRigEffectors(rigRoot, rootControl.transform, tipControl.transform);
            
            Selection.activeGameObject = tipControl;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Twist Chain for {constraintName}: {root.name} → {tip.name}");
        }
        
        private static void CreateChainIKSetup(Transform root, Transform tip)
        {
            var character = GetCharacterRoot(root);
            var rigBuilder = EnsureRigBuilder(character);
            var (rigRoot, _) = EnsureRigStructure(character);
            
            string boneName = GetCleanBoneName(tip.name);
            bool isLeft = tip.name.ToLower().Contains("left") || tip.name.ToLower().StartsWith("l_") || tip.name.ToLower().StartsWith("l ");
            bool isRight = tip.name.ToLower().Contains("right") || tip.name.ToLower().StartsWith("r_") || tip.name.ToLower().StartsWith("r ");
            string sidePrefix = isLeft ? "Left" : (isRight ? "Right" : "");
            string constraintName = sidePrefix != "" ? $"{sidePrefix} {boneName}" : boneName;
            
            GameObject constraintObject = new GameObject($"{constraintName} Chain");
            constraintObject.transform.parent = rigRoot.transform;
            constraintObject.transform.localPosition = Vector3.zero;
            constraintObject.transform.localRotation = Quaternion.identity;
            
            GameObject target = new GameObject($"{constraintName} Chain Target");
            target.transform.parent = constraintObject.transform;
            target.transform.position = tip.position;
            target.transform.localRotation = Quaternion.identity;
            
            var constraint = constraintObject.AddComponent<ChainIKConstraint>();
            constraint.data.root = root;
            constraint.data.tip = tip;
            constraint.data.target = target.transform;
            constraint.data.maintainTargetPositionOffset = false;
            constraint.data.maintainTargetRotationOffset = true;
            constraint.data.chainRotationWeight = 1f;
            constraint.data.tipRotationWeight = 1f;
            
            RefreshRigBuilder(rigBuilder);
            AddRigEffectors(rigRoot, target.transform);
            
            Selection.activeGameObject = target;
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(rigRoot);
            
            Debug.Log($"Created Chain IK for {constraintName}: {root.name} → {tip.name}");
        }
        
        private static GameObject GetCharacterRoot(Transform bone)
        {
            Transform current = bone;
            while (current != null)
            {
                if (current.GetComponent<Animator>() != null)
                    return current.gameObject;
                current = current.parent;
            }
            
            current = bone.root;
            if (current.GetComponent<Animator>() == null)
            {
                Debug.LogWarning($"No Animator found in hierarchy. Using root: {current.name}");
            }
            return current.gameObject;
        }
        
        private static RigBuilder EnsureRigBuilder(GameObject character)
        {
            var rigBuilder = character.GetComponent<RigBuilder>();
            if (rigBuilder == null)
            {
                rigBuilder = character.AddComponent<RigBuilder>();
            }
            return rigBuilder;
        }
        
        private static (GameObject rigLayer, GameObject controlsParent) EnsureRigStructure(GameObject character)
        {
            GameObject rigRoot = character.transform.Find($"{character.name}_Rig")?.gameObject;
            if (rigRoot == null)
            {
                rigRoot = new GameObject($"{character.name}_Rig");
                rigRoot.transform.parent = character.transform;
                rigRoot.transform.localPosition = Vector3.zero;
                rigRoot.transform.localRotation = Quaternion.identity;
                rigRoot.transform.localScale = Vector3.one;
            }
            
            // Match Control Rig prefab structure - Rig component on root, not child
            var rig = rigRoot.GetComponent<Rig>();
            if (rig == null)
            {
                rig = rigRoot.AddComponent<Rig>();
                
                var rigBuilder = character.GetComponent<RigBuilder>();
                if (rigBuilder != null && !rigBuilder.layers.Any(l => l.rig == rig))
                {
                    rigBuilder.layers.Add(new RigLayer(rig, true));
                }
            }
            
            // Controls can be direct children or in a Controls group
            GameObject controlsParent = rigRoot.transform.Find("Controls")?.gameObject;
            if (controlsParent == null)
            {
                // For simplicity, use the rig root itself as controls parent
                // This matches your prefab structure better
                controlsParent = rigRoot;
            }
            
            return (rigRoot, controlsParent);
        }
        
        private static void RefreshRigBuilder(RigBuilder rigBuilder)
        {
            if (rigBuilder != null)
            {
                EditorUtility.SetDirty(rigBuilder);
            }
        }
        
        private static void AddRigEffectors(GameObject rigLayer, params Transform[] controls)
        {
            if (rigLayer == null) return;
            
            var rig = rigLayer.GetComponent<Rig>();
            if (rig == null) 
            {
                Debug.LogWarning("No Rig component found on " + rigLayer.name);
                return;
            }
            
            // The Animation Rigging package doesn't expose effectors directly in code
            // We need to use serialized properties to modify them
            var serializedObject = new UnityEditor.SerializedObject(rig);
            serializedObject.Update();
            
            var effectorsProperty = serializedObject.FindProperty("m_Effectors");
            
            if (effectorsProperty == null) return;
            
            foreach (var control in controls)
            {
                if (control == null) continue;
                
                // Check if this transform already has an effector
                bool alreadyExists = false;
                for (int i = 0; i < effectorsProperty.arraySize; i++)
                {
                    var existingElement = effectorsProperty.GetArrayElementAtIndex(i);
                    var existingTransform = existingElement.FindPropertyRelative("m_Transform");
                    if (existingTransform != null && existingTransform.objectReferenceValue == control)
                    {
                        alreadyExists = true;
                        break;
                    }
                }
                
                if (alreadyExists) continue;
                
                int index = effectorsProperty.arraySize;
                effectorsProperty.InsertArrayElementAtIndex(index);
                
                var effectorElement = effectorsProperty.GetArrayElementAtIndex(index);
                var transformProp = effectorElement.FindPropertyRelative("m_Transform");
                var visibleProp = effectorElement.FindPropertyRelative("m_Visible");
                var styleProp = effectorElement.FindPropertyRelative("m_Style");
                
                if (transformProp != null)
                {
                    transformProp.objectReferenceValue = control;
                }
                    
                if (visibleProp != null)
                {
                    visibleProp.boolValue = true;
                }
                    
                if (styleProp != null)
                {
                    var shapeProp = styleProp.FindPropertyRelative("shape");
                    var colorProp = styleProp.FindPropertyRelative("color");
                    var sizeProp = styleProp.FindPropertyRelative("size");
                    var positionProp = styleProp.FindPropertyRelative("position");
                    var rotationProp = styleProp.FindPropertyRelative("rotation");
                    
                    if (shapeProp != null)
                    {
                        var shape = GetEffectorShape(control.name);
                        if (shape != null)
                        {
                            shapeProp.objectReferenceValue = shape;
                        }
                    }
                    
                    if (colorProp != null)
                    {
                        colorProp.colorValue = GetEffectorColor(control.name);
                    }
                        
                    if (sizeProp != null)
                    {
                        sizeProp.floatValue = GetEffectorSize(control.name);
                    }
                        
                    if (positionProp != null)
                    {
                        positionProp.vector3Value = Vector3.zero;
                    }
                        
                    if (rotationProp != null)
                    {
                        rotationProp.vector3Value = Vector3.zero;
                    }
                }
            }
            
            serializedObject.ApplyModifiedProperties();
            
            // Force refresh the rig to avoid null reference issues
            EditorUtility.SetDirty(rig);
            
            // Delay call to ensure proper initialization
            EditorApplication.delayCall += () =>
            {
                if (rig != null)
                {
                    var rigBuilder = rig.transform.GetComponentInParent<RigBuilder>();
                    if (rigBuilder != null)
                    {
                        EditorUtility.SetDirty(rigBuilder);
                    }
                }
            };
        }
        
        private static UnityEngine.Object GetEffectorShape(string name)
        {
            name = name.ToLower();
            string shapePath = "Packages/com.unity.animation.rigging/Editor/Shapes/";
            
            // Match exact setup from Control Rig prefab
            if (name.Contains("thumb") || name.Contains("index") || name.Contains("middle") || 
                name.Contains("ring") || name.Contains("pinky"))
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(shapePath + "BallEffector.asset");
            else if (name.Contains("hint"))
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(shapePath + "BallEffector.asset");
            else if (name.Contains("target"))
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(shapePath + "BoxEffector.asset");
            else if (name.Contains("control") || name.Contains("hip") || name.Contains("spine"))
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(shapePath + "LocatorEffector.asset");
            else if (name.Contains("aim") || name.Contains("lookat"))
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(shapePath + "BallEffector.asset");
            else if (name.Contains("twist"))
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(shapePath + "LocatorEffector.asset");
            else
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(shapePath + "BallEffector.asset");
        }
        
        private static Color GetEffectorColor(string name)
        {
            name = name.ToLower();
            if (name.Contains("thumb") || name.Contains("index") || name.Contains("middle") || 
                name.Contains("ring") || name.Contains("pinky"))
                return new Color(0.3f, 1f, 0.3f, 0.5f);
            else if (name.Contains("target"))
                return new Color(1f, 0f, 0f, 0.5f);
            else if (name.Contains("hint"))
                return new Color(0.57f, 0f, 1f, 0.5f);
            else if (name.Contains("control"))
                return new Color(0f, 0.62f, 1f, 0.5f);
            else if (name.Contains("aim") || name.Contains("lookat"))
                return new Color(1f, 0.69f, 0f, 0.5f);
            else
                return new Color(1f, 0.84f, 0f, 0.5f);
        }
        
        private static float GetEffectorSize(string name)
        {
            name = name.ToLower();
            if (name.Contains("thumb") || name.Contains("index") || name.Contains("middle") || 
                name.Contains("ring") || name.Contains("pinky"))
                return 0.03f;
            else if (name.Contains("target"))
                return 0.1f;
            else if (name.Contains("hint"))
                return 0.07f;
            else if (name.Contains("control"))
                return 0.5f;
            else if (name.Contains("aim") || name.Contains("lookat"))
                return 0.2f;
            else
                return 0.05f;
        }
        
        private static string GetCleanBoneName(string boneName)
        {
            boneName = boneName.Replace("mixamorig:", "");
            boneName = boneName.Replace("Bone", "");
            boneName = boneName.Replace("_", "");
            boneName = boneName.Replace(".", "");
            boneName = boneName.Trim();
            
            if (string.IsNullOrEmpty(boneName))
                boneName = "Bone";
            
            return boneName;
        }
        
        private static Vector3 CalculateOptimalHintPosition(Transform root, Transform mid, Transform tip)
        {
            Vector3 rootToTip = tip.position - root.position;
            Vector3 rootToMid = mid.position - root.position;
            
            Vector3 projectedMid = root.position + Vector3.Project(rootToMid, rootToTip);
            Vector3 currentBendDirection = (mid.position - projectedMid).normalized;
            
            if (currentBendDirection.magnitude > 0.01f)
            {
                return mid.position + currentBendDirection * 0.5f;
            }
            
            bool isArm = tip.name.ToLower().Contains("hand") || tip.name.ToLower().Contains("wrist") || 
                        root.name.ToLower().Contains("shoulder") || root.name.ToLower().Contains("upperarm");
            bool isLeg = tip.name.ToLower().Contains("foot") || tip.name.ToLower().Contains("ankle") || 
                        root.name.ToLower().Contains("thigh") || root.name.ToLower().Contains("upleg") || 
                        root.name.ToLower().Contains("hip");
            
            if (isArm)
            {
                return mid.position + Vector3.back * 0.5f;
            }
            else if (isLeg)
            {
                return mid.position + Vector3.forward * 0.5f;
            }
            else
            {
                return mid.position + Vector3.forward * 0.5f;
            }
        }
        
        private static Transform FindChainRoot(Transform tip)
        {
            Transform current = tip;
            int depth = 0;
            
            while (current.parent != null && depth < 10)
            {
                current = current.parent;
                depth++;
                
                if (current.GetComponent<Animator>() != null)
                    break;
            }
            
            if (depth < 2)
            {
                return tip.parent ?? tip;
            }
            
            return current;
        }
        
        private static Transform FindChainTip(Transform root)
        {
            Transform current = root;
            
            // Follow the chain down to find the tip (deepest child)
            while (current.childCount > 0)
            {
                // Follow the first child (assumes single chain)
                // For branching structures, this will follow the first branch
                current = current.GetChild(0);
            }
            
            return current;
        }
        
        private static List<Transform> OrderBonesByHierarchy(Transform[] bones)
        {
            var ordered = new List<Transform>(bones);
            ordered.Sort((a, b) =>
            {
                // Check if a is parent of b
                if (IsParentOf(a, b)) return -1;
                if (IsParentOf(b, a)) return 1;
                
                // Otherwise sort by hierarchy depth
                int depthA = GetDepth(a);
                int depthB = GetDepth(b);
                return depthA.CompareTo(depthB);
            });
            return ordered;
        }
        
        private static bool IsParentOf(Transform parent, Transform child)
        {
            Transform current = child.parent;
            while (current != null)
            {
                if (current == parent) return true;
                current = current.parent;
            }
            return false;
        }
        
        private static int GetDepth(Transform t)
        {
            int depth = 0;
            Transform current = t.parent;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }
        
        private static Transform FindBoneByName(Transform root, string boneType, string side)
        {
            var allChildren = root.GetComponentsInChildren<Transform>();
            
            foreach (var bone in allChildren)
            {
                string boneName = bone.name.ToLower();
                if (boneName.Contains(boneType.ToLower()) && boneName.Contains(side.ToLower()))
                {
                    return bone;
                }
            }
            
            return null;
        }
    }
}