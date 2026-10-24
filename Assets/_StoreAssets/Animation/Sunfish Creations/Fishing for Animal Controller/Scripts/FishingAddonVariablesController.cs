using MalbersAnimations.Scriptables;
using UnityEngine;
using System.Collections.Generic;

namespace SunfishCreations
{

    [CreateAssetMenu(fileName = "Fishing Variables", menuName = "Malbers Animations/Fishing/Fishing Variables", order = 4000)]
    public class SunfishCreationsVariablesController : ScriptableObject
    {
        [Header("Fishing Reeler")]
        [Tooltip("How fast will the bobber moves in the player direction")]
        public FloatReference reelerSpeed = 1f;
        [Tooltip("After the bobber travelled this distance, it will be pulled out of the water")]
        public FloatReference reelerRequiredDistance = 5f;
        [Tooltip("Use this if you want a animator to have a different speed then the real reeling")]
        public FloatReference reelerAnimatorSpeedModifier = 1f;

        [Header("Fishing Caster")]
        [Tooltip("How far in front of the player to start searching for water.")]
        public FloatReference castDistance = 10f;
        [Tooltip("Search radius around the target point for water colliders.")]
        public FloatReference searchRadius = 3f;
        [Tooltip("Vertical offset applied to the search center point.")]
        public FloatReference heightOffset = -1.6f;
        [Tooltip("A offset distance for the raycast that is cast down to water detection")]
        public FloatReference castStartHeight = 2.2f;
        [Tooltip("How strongly to pull toward the LEFT when biased left")]
        public FloatReference leftBiasStrength = 0.6f;
        [Tooltip("How strongly to pull toward the RIGHT when biased right")]
        public FloatReference rightBiasStrength = 0.3f;

        [Tooltip("If true, prefers water spots closer to the target point.")]
        public BoolReference preferCloserSpots = true;

        [Header("Placeholder Section")]
        [Tooltip("Tooltip")]
        public FloatReference placeHolderVariable = 1f;
    }
}