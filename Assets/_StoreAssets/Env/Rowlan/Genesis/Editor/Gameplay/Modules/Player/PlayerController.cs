using Rowlan.UnityStandardAssets.Cameras;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

#if __MICROVERSE_SPLINES__
using UnityEngine.Splines;
#endif

namespace Rowlan.Genesis.Gameplay
{
    public class PlayerController
    {
        private GameplayModule editor;

        private PlayerSettings setup;

        public PlayerController(GameplayModule editor)
        {
            this.editor = editor;

            // camera
            Camera camera = GameObjectUtils.GetMainCamera();
            setup = ScriptableObject.CreateInstance<PlayerSettings>();
            setup.camera = camera;

            // player
            setup.player = GameObject.FindGameObjectWithTag("Player");

            // default to cinemachine 3 in case it's installed
            if (CineMachine3Utils.IsCineMachineInstalled())
            {
                setup.cameraSystem = PlayerSettings.CameraSystem.CineMachine3_OrbitalFollow;
            }
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Setup", GUIStyles.BoxTitleStyle);

                setup.camera = EditorGUILayout.ObjectField("Camera", setup.camera, typeof(Camera), true) as Camera;
                if (!setup.camera)
                {
                    EditorGUILayout.HelpBox("Camera required", MessageType.Error);
                }

                setup.player = EditorGUILayout.ObjectField("Player", setup.player, typeof(GameObject), true) as GameObject;
                if (!setup.player)
                {
                    EditorGUILayout.HelpBox("Player required", MessageType.Error);
                }

                setup.addCameraTarget = EditorGUILayout.Toggle(new GUIContent("Add Camera Target", "Dedicated target for freelook camera"), setup.addCameraTarget);
                if (setup.addCameraTarget)
                {
                    setup.cameraTargetY = EditorGUILayout.FloatField(new GUIContent("Camera Target Y", "Height position of the camera target"), setup.cameraTargetY);
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("System", GUIStyles.BoxTitleStyle);

                setup.cameraSystem = (PlayerSettings.CameraSystem) EditorGUILayout.EnumPopup(new GUIContent("Camera System", "The camera system to use"), setup.cameraSystem);

                switch (setup.cameraSystem)
                {
                    case PlayerSettings.CameraSystem.Genesis_FreeLook:
                        {

                            setup.genesisFreeLookSettings.cameraDistance = EditorGUILayout.FloatField(new GUIContent("Camera Distance", "Distance of camera to player, should be negative"), setup.genesisFreeLookSettings.cameraDistance);

                            bool valid = setup.camera && setup.player;

                            bool prevEnabled = GUI.enabled;
                            GUI.enabled = valid;
                            {
                                if (GUILayout.Button("Add Third Person Rig"))
                                {
                                    SetupThirdPersonRig();
                                }
                            }
                            GUI.enabled = prevEnabled;
                        }
                        break;

                    case PlayerSettings.CameraSystem.CineMachine3_OrbitalFollow:
                        if( CineMachine3Utils.IsCineMachineInstalled())
                        {

                            setup.cinemachine3OrbitalFollowSettings.radiusFactor = EditorGUILayout.FloatField(new GUIContent("Radius Factor", "Factor multiplied to Top/Center/Bottom radius"), setup.cinemachine3OrbitalFollowSettings.radiusFactor);

                            bool valid = setup.camera && setup.player;

                            bool prevEnabled = GUI.enabled;
                            GUI.enabled = valid;
                            {
                                if (GUILayout.Button("Add CineMachine 3"))
                                {
                                    SetupCineMachine3();
                                }
                            }
                            GUI.enabled = prevEnabled;
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("CineMachine 3 missing, please install it via Package Manager.", MessageType.Warning);
                        }

                        break;
                }

            }
            EditorGUILayout.EndVertical();

#if __MICROVERSE_SPLINES__

            // player positioning
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Positioning", GUIStyles.BoxTitleStyle);

                bool valid = setup.player != null;

                bool prevEnabled = GUI.enabled;
                GUI.enabled = valid;
                {
                    if (GUILayout.Button(new GUIContent("Position Player on Spline", "Find the closest spline point from current sceneview and position the player on it")))
                    {
                        PositionPlayerOnSpline();
                    }
                }
                GUI.enabled = prevEnabled;
            }
            EditorGUILayout.EndVertical();
#endif
        }

        private Transform AddOrGetCameraTarget()
        {
            Transform cameraTarget;

            if (setup.addCameraTarget)
            {
                // try to find existing
                string cameraTargetName = "Camera Target";
                cameraTarget = setup.player.transform.Find(cameraTargetName);

                // not found, add new one
                if (!cameraTarget)
                {
                    GameObject cameraTargetGO = new GameObject(cameraTargetName);

                    // reparent
                    cameraTargetGO.transform.parent = setup.player.transform;

                    // set local transform
                    cameraTargetGO.transform.localPosition = new Vector3(0f, setup.cameraTargetY, 0f);
                    cameraTargetGO.transform.localRotation = Quaternion.identity;
                    cameraTargetGO.transform.localScale = Vector3.one;

                    // set target for freelookcam
                    cameraTarget = cameraTargetGO.transform;

                    Undo.RegisterCreatedObjectUndo(cameraTargetGO, "Add camera target  " + cameraTargetGO.name);

                }
            }
            else
            {
                cameraTarget = setup.player.transform;
            }

            return cameraTarget;
        }

        private void SetupThirdPersonRig()
        {
            // third person
            GameObject go = new GameObject("Third Person Rig");

            GameObjectUtility.EnsureUniqueNameForSibling(go);

            // pivot
            GameObject pivot = new GameObject("Pivot");
            pivot.transform.parent = go.transform;

            // camera
            {
                Undo.RegisterFullObjectHierarchyUndo(setup.camera, "Change parent " + setup.camera);

                // reparent the camera
                setup.camera.transform.parent = pivot.transform;

                setup.camera.transform.position = new Vector3(0f, 0f, setup.genesisFreeLookSettings.cameraDistance);
                setup.camera.transform.rotation = Quaternion.identity;
                setup.camera.transform.localScale = Vector3.one;
            }

            // camera target
            {
                Transform cameraTarget = AddOrGetCameraTarget();

                // freelook camera script
                {
                    FreeLookCam freeLookCam = go.AddComponent<FreeLookCam>();

                    freeLookCam.SetAutoTargetPlayer(false);
                    freeLookCam.SetTarget(cameraTarget);
                    freeLookCam.SetMoveSpeed( 12f);
                }
            }

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            Selection.activeObject = go;
        }

        private void SetupCineMachine3()
        {
#if USING_CINEMACHINE3
            // third person
            GameObject go = new GameObject("CineMachine FreeLook");

            GameObjectUtility.EnsureUniqueNameForSibling(go);

            // camera
            {

                Undo.RegisterFullObjectHierarchyUndo(setup.camera, "Add CM brain to " + setup.camera);

                // CM brain
                Unity.Cinemachine.CinemachineBrain brain = setup.camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();

                if( !brain)
                {
                    brain = setup.camera.gameObject.AddComponent<Unity.Cinemachine.CinemachineBrain>();
                }

            }

            // camera target
            Transform cameraTarget = AddOrGetCameraTarget();

            // add CM camera
            Unity.Cinemachine.CinemachineCamera vcam = Undo.AddComponent<Unity.Cinemachine.CinemachineCamera>(go);

            // setup target
            vcam.Follow = cameraTarget;
            // vcam.Lens = CineMachine3Utils.MatchSceneViewCamera(vcam.transform);

            // add CM scripts (orbital, etc)
            Unity.Cinemachine.CinemachineOrbitalFollow orbitalFollow = Undo.AddComponent<Unity.Cinemachine.CinemachineOrbitalFollow>(vcam.gameObject);

            orbitalFollow.OrbitStyle = Unity.Cinemachine.CinemachineOrbitalFollow.OrbitStyles.ThreeRing;
            orbitalFollow.Orbits.Top.Radius *= setup.cinemachine3OrbitalFollowSettings.radiusFactor;
            orbitalFollow.Orbits.Center.Radius *= setup.cinemachine3OrbitalFollowSettings.radiusFactor;
            orbitalFollow.Orbits.Bottom.Radius *= setup.cinemachine3OrbitalFollowSettings.radiusFactor;

            Undo.AddComponent<Unity.Cinemachine.CinemachineRotationComposer>(vcam.gameObject);
            Undo.AddComponent<Unity.Cinemachine.CinemachineInputAxisController>(vcam.gameObject);
            Undo.AddComponent<Unity.Cinemachine.CinemachineFreeLookModifier>(vcam.gameObject).enabled = false;

#endif
        }



#if __MICROVERSE_SPLINES__

        private void PositionPlayerOnSpline()
        {
            if (setup.player == null)
                return;

            Transform sceneCameraTransform = SceneView.lastActiveSceneView.camera.transform;

            if (SplineUtils.GetNearestPoint(sceneCameraTransform, out float3 position, out float3 tangent, out float3 normal, out SplineContainer container))
            {
                // check dot of camera world position vs tangent
                // if it's negative then both face in opposite directions, ie the positioning of the gameobject along the spline should be inverted
                float dot = Vector3.Dot(sceneCameraTransform.TransformDirection(Vector3.forward), new Vector3(tangent.x, tangent.y, tangent.z));
                float tangentFactor = dot < 0 ? -1 : 1;

                setup.player.transform.position = position;
                setup.player.transform.rotation = Quaternion.LookRotation(tangent * tangentFactor);

                // frame the player
                float zoom = 4f;
                SceneView.lastActiveSceneView.LookAt(setup.player.transform.position, sceneCameraTransform.rotation, zoom);
                SceneView.lastActiveSceneView.Repaint();
            }

        }

#endif
    }
}