#if UNITY_EDITOR
using MalbersAnimations;
using MalbersAnimations.Controller;
using MalbersAnimations.Items;
using UnityEditor;
using UnityEngine;

namespace SunfishCreations
{
    /// <summary>
    /// Fishing mode determines which Human Fishing state is applied and whether hand IK is required.
    /// </summary>
    public enum FishingMode
    {
        Simple = 0,
        Reeling = 1,
        TugOfWar = 2,
    }

    /// <summary>
    /// Shared installer logic for creating fisher player characters.
    /// </summary>
    public static class FisherInstaller
    {
        public const string HumanPlayerPrefabPath = "Assets/Sunfish Creations/Fishing for Animal Controller/Prefabs/Characters/Player Fisher.prefab";
        public const string HumanFishingStatePath = "Assets/Sunfish Creations/Fishing for Animal Controller/States/Human Fishing.asset";
        public const string NoReelingDemoStatePath = "Assets/Sunfish Creations/Fishing for Animal Controller/States/Human Fishing - No Reeling Demo.asset";
        public const string TugOfWarDemoStatePath = "Assets/Sunfish Creations/Fishing for Animal Controller/States/Human Fishing - Tug of War Demo.asset";
        public const string FishingRodSuperItemPrefabPath = "Assets/Sunfish Creations/Fishing for Animal Controller/Prefabs/Fishing Rod/Fishing Rod (SI).prefab";
        public const string HumanFishingStateName = "Human Fishing";
        public const string FishingStateNamePrefix = "Human Fishing";

        public static string GetStatePath(FishingMode mode)
        {
            switch (mode)
            {
                case FishingMode.Reeling: return HumanFishingStatePath;
                case FishingMode.TugOfWar: return TugOfWarDemoStatePath;
                default: return NoReelingDemoStatePath;
            }
        }

        public static bool RequiresIK(FishingMode mode)
        {
            return mode == FishingMode.Reeling || mode == FishingMode.TugOfWar;
        }


        public static string GetStateName(FishingMode mode)
        {
            switch (mode)
            {
                case FishingMode.Reeling: return HumanFishingStateName;
                case FishingMode.TugOfWar: return "Human Fishing - Tug of War Demo";
                default: return "Human Fishing - No Reeling Demo";
            }
        }

        /// <summary>
        /// Checks whether a GameObject is a Super Item fisher player supported by the wizard.
        /// </summary>
        public static bool IsFisherPlayer(GameObject obj)
        {
            if (obj == null)
                return false;

            if (obj.GetComponent<MAnimal>() == null || obj.GetComponent<SuperItemManager>() == null)
                return false;

            var capabilities = obj.GetComponentsInChildren<FishingRigCapabilities>(true);
            return capabilities != null && capabilities.Length > 0;
        }

        /// <summary>
        /// Checks whether a target is a legacy fisher player or its direct child.
        /// Legacy Weapon Manager players are not updated or rebuilt by this wizard.
        /// </summary>
        public static bool IsLegacyFisherPlayerTarget(GameObject target)
        {
            return IsLegacyFisherPlayer(target) ||
                   (target != null && target.transform.parent != null && IsLegacyFisherPlayer(target.transform.parent.gameObject));
        }

        private static bool IsLegacyFisherPlayer(GameObject obj)
        {
            return obj != null &&
                   obj.GetComponent<MAnimal>() != null &&
                   obj.GetComponent<SuperItemManager>() == null &&
                   obj.GetComponentsInChildren<FishingRigCapabilities>(true).Length > 0;
        }

        /// <summary>
        /// If the target is already part of a fisher player, returns that root.
        /// </summary>
        public static bool TryGetExistingFisherPlayer(GameObject target, out GameObject existingPlayer)
        {
            existingPlayer = null;
            if (target == null)
                return false;

            // Case 1: the target itself is the fisher player.
            if (IsFisherPlayer(target))
            {
                existingPlayer = target;
                return true;
            }

            // Case 2: the target is the original humanoid, parented under the fisher player.
            if (target.transform.parent != null && IsFisherPlayer(target.transform.parent.gameObject))
            {
                existingPlayer = target.transform.parent.gameObject;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Instantiates Player Fisher.prefab, transfers the avatar and root bone from
        /// the source, and returns the created scene object.
        /// </summary>
        public static GameObject CreateFisherPlayer(GameObject source)
        {
            if (source == null)
            {
                Debug.LogWarning("[FisherInstaller] Source GameObject is null. Cannot create fisher player.");
                return null;
            }

            var humanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanPlayerPrefabPath);
            if (humanPrefab == null)
            {
                Debug.LogWarning($"[FisherInstaller] Could not load prefab at '{HumanPlayerPrefabPath}'.", source);
                return null;
            }

            if (humanPrefab.GetComponent<SuperItemManager>() == null)
            {
                Debug.LogWarning($"[FisherInstaller] The base prefab at '{HumanPlayerPrefabPath}' must contain a SuperItemManager. Legacy Weapon Manager player prefabs are not supported.", source);
                return null;
            }

            // Capture the source's world position and rotation before resetting.
            var sourcePosition = source.transform.position;
            var sourceRotation = source.transform.rotation;

            source.transform.ResetLocal();

            if (!source.TryGetComponent<Animator>(out var animator))
                animator = source.AddComponent<Animator>();

            var currentAvatar = animator.avatar;
            var avatarRoot = animator.avatarRoot;

            var sceneObj = (GameObject)PrefabUtility.InstantiatePrefab(humanPrefab);
            sceneObj.GetComponent<Animator>().avatar = currentAvatar;

            source.transform.parent = sceneObj.transform;

            // Transfer the source's original world transform to the created player.
            sceneObj.transform.SetPositionAndRotation(sourcePosition, sourceRotation);

            var animal = sceneObj.GetComponent<MAnimal>();
            animal.RootBone = avatarRoot;
            sceneObj.name = source.name;

            Selection.activeGameObject = sceneObj;
            GameObject.DestroyImmediate(animator);

            return sceneObj;
        }

        /// <summary>
        /// Applies a fishing configuration and optionally assigns a Super Item rod to its matching equip point.
        /// </summary>
        public static void ApplyFishingConfiguration(GameObject root, FishingMode mode, bool supportIK, GameObject rodPrefab, bool startWithRod)
        {
            if (root == null)
                return;

            SwapFishingState(root, GetStatePath(mode));
            SetFishingRigCapabilities(root, supportIK || RequiresIK(mode));
            SetStartingRod(root, rodPrefab, startWithRod);
        }

        /// <summary>
        /// Replaces any Human Fishing state variant in all MAnimal components under the root.
        /// Handles both first-time builds and re-configurations.
        /// </summary>
        private static void SwapFishingState(GameObject root, string statePath)
        {
            var targetState = AssetDatabase.LoadAssetAtPath<State>(statePath);
            if (targetState == null)
            {
                Debug.LogWarning($"[FisherInstaller] Could not load state asset at '{statePath}'. State swap skipped.", root);
                return;
            }

            var animals = root.GetComponentsInChildren<MAnimal>(true);
            if (animals == null || animals.Length == 0)
            {
                Debug.LogWarning("[FisherInstaller] No MAnimal components found. State swap skipped.", root);
                return;
            }

            foreach (var animal in animals)
            {
                for (int i = 0; i < animal.states.Count; i++)
                {
                    if (animal.states[i] != null && animal.states[i].name.StartsWith(FishingStateNamePrefix))
                    {
                        animal.states[i] = targetState;
                        EditorUtility.SetDirty(animal);
                    }
                }
            }
        }

        private static void SetFishingRigCapabilities(GameObject root, bool supportsIK)
        {
            var capabilities = root.GetComponentsInChildren<FishingRigCapabilities>(true);
            if (capabilities == null || capabilities.Length == 0)
            {
                Debug.LogWarning("[FisherInstaller] No FishingRigCapabilities components found. Capability override skipped.", root);
                return;
            }

            foreach (var capabilitiesComponent in capabilities)
            {
                var serializedObject = new SerializedObject(capabilitiesComponent);
                serializedObject.FindProperty("supportsIK").boolValue = supportsIK;
                serializedObject.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Assigns or clears the selected Super Item rod on the matching starting equip point.
        /// </summary>
        private static void SetStartingRod(GameObject root, GameObject rodPrefab, bool startWithRod)
        {
            var manager = root.GetComponent<SuperItemManager>();
            if (manager == null)
            {
                Debug.LogWarning("[FisherInstaller] No SuperItemManager component found on root. Starting rod was not configured.", root);
                return;
            }

            var prefabToInspect = rodPrefab;
            if (prefabToInspect == null || prefabToInspect.GetComponent<SuperItem>() == null)
            {
                if (startWithRod)
                {
                    Debug.LogWarning("[FisherInstaller] The selected rod prefab must have a SuperItem component. Legacy Weapon Manager weapon prefabs are not supported.", root);
                    return;
                }

                prefabToInspect = AssetDatabase.LoadAssetAtPath<GameObject>(FishingRodSuperItemPrefabPath);
            }

            if (prefabToInspect == null)
            {
                Debug.LogWarning($"[FisherInstaller] Could not load the default Super Item rod prefab at '{FishingRodSuperItemPrefabPath}'. Starting rod was not configured.", root);
                return;
            }

            var rod = prefabToInspect.GetComponent<SuperItem>();
            if (rod == null || rod.EquipPoint == null)
            {
                Debug.LogWarning($"[FisherInstaller] Super Item rod prefab '{prefabToInspect.name}' has no valid SuperItem equip point.", prefabToInspect);
                return;
            }

            var equipPoint = manager.EquipPoints.Find(point => point != null && point.ID == rod.EquipPoint);
            if (equipPoint == null)
            {
                Debug.LogWarning($"[FisherInstaller] No Super Item equip point matching '{rod.EquipPoint.name}' was found on the player. Starting rod was not configured.", root);
                return;
            }

            equipPoint.Item = startWithRod ? rod : null;
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorUtility.SetDirty(manager);
        }
    }
}
#endif
