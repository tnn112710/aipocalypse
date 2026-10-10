using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis.Gameplay
{
    public class PlayerSettings : ScriptableObject
    {
        public enum CameraSystem
        {
            [InspectorName("Genesis (3rd Person Free Look)")]
            Genesis_FreeLook = 0,
            [InspectorName("CineMachine 3 (Orbital Follow)")]
            CineMachine3_OrbitalFollow,
        }

        public class GenesisFreeLookSettings
        {
            public float cameraDistance = -4f;
        }

        public class CineMachine3_OrbitalFollowSettings
        {
            [Tooltip("Factor multiplied to Top/Center/Bottom radius")]
            [Min(1f)]
            public float radiusFactor = 1f;
        }

        public Camera camera;
        public GameObject player;
        public bool addCameraTarget = true;
        public float cameraTargetY = 1;

        public CameraSystem cameraSystem = CameraSystem.Genesis_FreeLook;

        public GenesisFreeLookSettings genesisFreeLookSettings = new GenesisFreeLookSettings();
        public CineMachine3_OrbitalFollowSettings cinemachine3OrbitalFollowSettings = new CineMachine3_OrbitalFollowSettings();

    }
}