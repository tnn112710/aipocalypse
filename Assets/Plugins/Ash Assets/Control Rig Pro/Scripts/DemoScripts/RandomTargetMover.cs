using UnityEngine;
using System.Collections.Generic;

namespace ControlRigPro
{
    public class RandomTargetMover : MonoBehaviour
    {
        [Header("Target Transforms")]
        public List<Transform> targetTransforms = new List<Transform>();
        
        [Header("Movement Settings")]
        public float movementSpeed = 1f;
        public float movementRange = 2f;
        public bool usePerlinNoise = true;
        
        [Header("Noise Settings")]
        public float noiseScale = 1f;
        public Vector3 noiseOffset;
        
        [Header("Movement Options")]
        public bool synchronizeMovement = false;
        public float phaseShift = 0.5f;
        public bool constrainToPlane = false;
        public Vector3 planeNormal = Vector3.up;
        
        private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
        private float timeOffset;
        
        private void Start()
        {
            foreach (Transform target in targetTransforms)
            {
                if (target != null)
                {
                    originalPositions[target] = target.position;
                }
            }
            
            timeOffset = Random.Range(0f, 100f);
            noiseOffset = new Vector3(
                Random.Range(0f, 100f),
                Random.Range(0f, 100f),
                Random.Range(0f, 100f)
            );
        }
        
        private void Update()
        {
            float currentTime = Time.time * movementSpeed;
            
            for (int i = 0; i < targetTransforms.Count; i++)
            {
                Transform target = targetTransforms[i];
                if (target == null || !originalPositions.ContainsKey(target))
                    continue;
                
                Vector3 originalPos = originalPositions[target];
                Vector3 offset;
                
                float timeWithPhase = currentTime;
                if (!synchronizeMovement)
                {
                    timeWithPhase += i * phaseShift;
                }
                
                if (usePerlinNoise)
                {
                    offset = GetPerlinNoiseOffset(timeWithPhase, target.GetEntityId().GetHashCode());
                }
                else
                {
                    offset = GetRandomOffset(timeWithPhase, target.GetEntityId().GetHashCode());
                }
                
                if (constrainToPlane)
                {
                    offset = Vector3.ProjectOnPlane(offset, planeNormal.normalized);
                }
                
                target.position = originalPos + offset * movementRange;
            }
        }
        
        private Vector3 GetPerlinNoiseOffset(float time, int seed)
        {
            float seedOffset = seed * 0.001f;
            
            float x = Mathf.PerlinNoise(
                time * noiseScale + noiseOffset.x + seedOffset, 
                seedOffset
            ) * 2f - 1f;
            
            float y = Mathf.PerlinNoise(
                time * noiseScale + noiseOffset.y + seedOffset, 
                seedOffset + 100f
            ) * 2f - 1f;
            
            float z = Mathf.PerlinNoise(
                time * noiseScale + noiseOffset.z + seedOffset, 
                seedOffset + 200f
            ) * 2f - 1f;
            
            return new Vector3(x, y, z);
        }
        
        private Vector3 GetRandomOffset(float time, int seed)
        {
            Random.InitState(seed + Mathf.FloorToInt(time));
            return Random.insideUnitSphere;
        }
        
        public void AddTarget(Transform newTarget)
        {
            if (!targetTransforms.Contains(newTarget))
            {
                targetTransforms.Add(newTarget);
                originalPositions[newTarget] = newTarget.position;
            }
        }
        
        public void RemoveTarget(Transform targetToRemove)
        {
            targetTransforms.Remove(targetToRemove);
            originalPositions.Remove(targetToRemove);
        }
        
        public void ResetToOriginalPositions()
        {
            foreach (var kvp in originalPositions)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.position = kvp.Value;
                }
            }
        }
    }
}