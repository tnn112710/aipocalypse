#if UNITY_EDITOR
using MalbersAnimations.Items;
using UnityEditor;
using UnityEngine;

namespace SunfishCreations
{
    /// <summary>
    /// Wizard window for creating fisher player characters from humanoid GameObjects.
    /// </summary>
    public class CreatePlayerWizard : EditorWindow
    {
        private const string BuildButtonLabel = "Build Fisher Player";
        private const string UpdateButtonLabel = "Update Fisher Player";

        [Tooltip("The humanoid character to convert into a fisher player.")]
        [SerializeField] private GameObject targetObject;

        [Tooltip("Which fishing state to apply to the character.")]
        [SerializeField] private FishingMode fishingMode = FishingMode.Simple;

        [Tooltip("Whether hand IK (reeling/holding poses) is allowed on this character.")]
        [SerializeField] private bool supportIK = false;

        [Tooltip("Whether the character starts with the selected Super Item fishing rod equipped.")]
        [SerializeField] private bool startWithRod = false;

        [Tooltip("Super Item rod prefab to equip on start. Auto-filled with the Fishing Rod (SI) prefab.")]
        [SerializeField] private GameObject rodPrefab;

        [MenuItem("GameObject/Sunfish Creations/Create Player (Wizard)", false, -100)]
        static void OpenWizard(MenuCommand menuCommand)
        {
            var window = GetWindow<CreatePlayerWizard>("Create Player");
            window.minSize = new Vector2(300, 230);

            var contextObject = menuCommand.context as GameObject;
            if (contextObject != null)
                window.targetObject = contextObject;

            window.Show();
        }

        private void OnEnable()
        {
            // Replace any previously serialized legacy weapon prefab with the Super Item rod.
            if (rodPrefab == null || rodPrefab.GetComponent<SuperItem>() == null)
                rodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FisherInstaller.FishingRodSuperItemPrefabPath);
        }

        private void OnGUI()
        {
            GUILayout.Label("Create Fisher Player", EditorStyles.boldLabel);
            GUILayout.Space(4);

            // Detect whether the target is already part of a supported or legacy fisher player.
            bool isUpdate = FisherInstaller.TryGetExistingFisherPlayer(targetObject, out var existingPlayer);
            bool isLegacyPlayerTarget = FisherInstaller.IsLegacyFisherPlayerTarget(targetObject);

            if (isUpdate)
                EditorGUILayout.HelpBox("Target is already a Super Item fisher player. Clicking Build will update its configuration.", MessageType.Info);
            else if (isLegacyPlayerTarget)
                EditorGUILayout.HelpBox("Legacy Weapon Manager fisher players are not supported. Select an unconverted humanoid to build a Super Item fisher player.", MessageType.Error);
            else
                EditorGUILayout.HelpBox("Drag a humanoid GameObject here, then click Build.", MessageType.Info);

            GUILayout.Space(4);

            targetObject = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Target Object", "The humanoid character to convert into a fisher player."),
                targetObject,
                typeof(GameObject),
                true
            );

            GUILayout.Space(6);

            fishingMode = (FishingMode)EditorGUILayout.EnumPopup(
                new GUIContent("Fishing Mode", "Determines which Human Fishing state is applied to the character."),
                fishingMode
            );

            GUILayout.Space(4);

            // Reeling and Tug of War require IK — force it on and lock the toggle.
            bool ikRequired = FisherInstaller.RequiresIK(fishingMode);            if (ikRequired)
                supportIK = true;

            EditorGUI.BeginDisabledGroup(ikRequired);
            {
                supportIK = EditorGUILayout.Toggle(
                    new GUIContent("Support IK", "Whether hand IK (reeling/holding poses) is allowed on this character. Required by Reeling and Tug of War modes."),
                    supportIK
                );
            }
            EditorGUI.EndDisabledGroup();

            if (ikRequired)
                EditorGUILayout.LabelField("", "IK is required for this mode.", EditorStyles.miniLabel);

            GUILayout.Space(4);

            startWithRod = EditorGUILayout.Toggle(
                new GUIContent("Start with Rod", "Whether the character starts with the selected rod assigned to its matching Super Item equip point."),
                startWithRod
            );

            // Show the rod prefab field only when "Start with Rod" is enabled.
            bool isSuperItemRodPrefab = rodPrefab != null && rodPrefab.GetComponent<SuperItem>() != null;
            if (startWithRod)
            {
                EditorGUI.indentLevel++;
                rodPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Rod", "Super Item rod prefab to assign to the matching equip point."),
                    rodPrefab,
                    typeof(GameObject),
                    false
                );
                EditorGUI.indentLevel--;

                isSuperItemRodPrefab = rodPrefab != null && rodPrefab.GetComponent<SuperItem>() != null;
                if (!isSuperItemRodPrefab)
                    EditorGUILayout.HelpBox("Choose a rod prefab with a SuperItem component. Legacy Weapon Manager weapon prefabs are not supported.", MessageType.Error);
            }

            GUILayout.Space(8);

            EditorGUI.BeginDisabledGroup(targetObject == null || isLegacyPlayerTarget || (startWithRod && !isSuperItemRodPrefab));
            {
                string buttonLabel = isUpdate ? UpdateButtonLabel : BuildButtonLabel;
                if (GUILayout.Button(buttonLabel, GUILayout.Height(28)))
                {
                    BuildPlayer(isUpdate, existingPlayer);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Creates or updates a fisher player with the selected configuration.
        /// </summary>
        private void BuildPlayer(bool isUpdate, GameObject existingPlayer)
        {
            if (targetObject == null)
                return;

            if (FisherInstaller.IsLegacyFisherPlayerTarget(targetObject))
            {
                Debug.LogWarning("Legacy Weapon Manager fisher players are not supported by the Create Player Wizard. Select an unconverted humanoid instead.", targetObject);
                return;
            }

            if (startWithRod && (rodPrefab == null || rodPrefab.GetComponent<SuperItem>() == null))
            {
                Debug.LogWarning("The selected rod prefab must have a SuperItem component. Legacy Weapon Manager weapon prefabs are not supported.", rodPrefab);
                return;
            }

            if (isUpdate && existingPlayer != null)
            {
                FisherInstaller.ApplyFishingConfiguration(existingPlayer, fishingMode, supportIK, rodPrefab, startWithRod);
                Selection.activeGameObject = existingPlayer;
                Debug.Log("Fisher Player updated! Configuration has been applied to the existing Super Item player.", existingPlayer);
                return;
            }

            var sceneObj = FisherInstaller.CreateFisherPlayer(targetObject);

            if (sceneObj != null)
            {
                FisherInstaller.ApplyFishingConfiguration(sceneObj, fishingMode, supportIK, rodPrefab, startWithRod);
                Debug.Log("Fisher Player created! Save the new Player as a Prefab Variant in your project.", sceneObj);
            }
        }
    }
}
#endif
