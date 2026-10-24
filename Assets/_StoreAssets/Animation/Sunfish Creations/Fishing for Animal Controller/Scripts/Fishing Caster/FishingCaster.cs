
using MalbersAnimations;
using MalbersAnimations.Scriptables;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace SunfishCreations
{
    /// <summary>
    /// Handles casting a fishing line and detecting valid water spots.
    /// </summary>
    [RequireComponent(typeof(BobberController))]
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/fishing-caster")]
    public class FishingCaster : MonoBehaviour
    {
        [Tooltip("Enables or disables the fishing caster behavior.")]
        public BoolReference active = true;     

        [Header("Player Radius Casting")]
        [Tooltip("How far in front of the player to start searching for water.")]
        public FloatReference castDistance = 10f;

        [Tooltip("Search radius around the target point for water colliders.")]
        public FloatReference searchRadius = 3f;

        [Tooltip("Vertical offset applied to the search center point.")]
        public FloatReference heightOffset = -1.6f;

        [Tooltip("A offset distance for the raycast that is cast down to water detection")]
        public FloatReference castStartHeight = 2.2f;

        [Header("Cast Bias")]
        [Tooltip("How strongly to pull toward the LEFT when biased left")]
        public FloatReference leftBiasStrength = 0.6f;

        [Tooltip("How strongly to pull toward the RIGHT when biased right")]
        public FloatReference rightBiasStrength = 0.3f;

        [Tooltip("If true, prefers water spots closer to the target point.")]
        public BoolReference preferCloserSpots = true;

        [Header("Water Detection")]
        [Tooltip("Layer mask for water colliders. Should include your water layer.")]
        public LayerMask waterLayer;

        [Tooltip("Layers that block line of sight to water (e.g., terrain, walls).")]
        public LayerMask blockingLayers;

        [Tooltip("Enable to show debug gizmos (sphere, rays) in Scene view.")]
        public bool debug = false;

        [SerializeField, Tooltip("Max iterations for bisection to find retrieval point")]
        private int _maxBisectionIterations = 8;

        [SerializeField, Tooltip("Visual size of retrieval gizmo")]
        private float _retrievalGizmoSize = 0.15f;      

        [Header("Shore Safety")]
        [Tooltip("Minimum distance (XZ) the bobber must be from shore (retrieval point). Prevents 'cheese' casts.")]
        public FloatReference _minShoreDistance = 3f;

        private BobberController _bobberController; 
        private Vector3 _retrievalPoint;
        private bool _hasRetrievalPoint;
        private bool _lastCastWasLeft = false;


        protected virtual void Awake()
        {
            _bobberController = GetComponent<BobberController>();
        }

        protected virtual void Reset()
        {
            if (waterLayer == 0)
                waterLayer = LayerMask.GetMask("Water");
        }

        /// <summary>
        /// Attempts to find a valid water surface to cast to.
        /// </summary>
        public virtual bool TryCastToWater(out Vector3 waterHitPoint, out GameObject hitWaterObject)
        {
            if (!active.Value)
            {
                Debug.LogWarning("Fishing Caster not Active");
                waterHitPoint = Vector3.zero;
                hitWaterObject = null;
                return false;
            }

            waterHitPoint = Vector3.zero;
            hitWaterObject = null;

            Vector3 sphereCenter = transform.position + transform.forward * castDistance.Value + Vector3.up * heightOffset.Value;

            // Generate first random offset with bias
            Vector3 randomOffset = Random.insideUnitSphere * searchRadius.Value;
            Vector3 biasDirection;
            float biasStrength;

            if (_lastCastWasLeft)
            {
                biasDirection = transform.right;
                biasStrength = rightBiasStrength.Value;
            }
            else
            {
                biasDirection = -transform.right;
                biasStrength = leftBiasStrength.Value;
            }

            _lastCastWasLeft = !_lastCastWasLeft;

            randomOffset += biasDirection * biasStrength * searchRadius.Value;
            randomOffset.y = 0f;

            if (randomOffset.magnitude > searchRadius.Value)
            {
                randomOffset = randomOffset.normalized * searchRadius.Value;
            }

            // Try primary point
            if (TryFindValidWaterAt(sphereCenter + randomOffset, out waterHitPoint, out hitWaterObject))
            {
                if (EnforceMinShoreDistance(ref waterHitPoint))
                {
                    return true;
                }
            }

            // Fallback: try opposite direction
            Vector3 fallbackOffset = -randomOffset;
            if (TryFindValidWaterAt(sphereCenter + fallbackOffset, out waterHitPoint, out hitWaterObject))
            {
                if (debug)
                {
                    FDebug("Primary cast failed. Used fallback point in opposite direction.", false, "yellow");
                }

                // Enforce min shore distance on fallback too
                if (EnforceMinShoreDistance(ref waterHitPoint))                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to find a valid water surface at a specific target point.
        /// </summary>
        private bool TryFindValidWaterAt(Vector3 targetPoint, out Vector3 waterHitPoint, out GameObject hitWaterObject)        {
            waterHitPoint = Vector3.zero;
            hitWaterObject = null;

            // QueryTriggerInteraction.Collide makes this independent of the project-wide setting.
            Collider[] waterColliders = Physics.OverlapSphere(targetPoint, searchRadius.Value, waterLayer, QueryTriggerInteraction.Collide);
            if (waterColliders.Length == 0)
            {
                if (debug)
                {
                    FDebug($"No water colliders near point {targetPoint}", true, "red");
                }
                return false;
            }

            Collider bestWater = null;
            float bestScore = float.MaxValue;
            Vector3 bestHitPoint = Vector3.zero;

            Vector3 startPoint = transform.position + Vector3.up * castStartHeight.Value;

            foreach (var waterCollider in waterColliders)
            {
                Vector3 closestPoint = waterCollider.ClosestPoint(targetPoint);
                Vector3 directionToWater = (closestPoint - startPoint).normalized;
                float distanceToWater = Vector3.Distance(startPoint, closestPoint);
                float rayDistance = distanceToWater + 0.1f;

                int combinedLayers = waterLayer | blockingLayers;
                bool hitSomething = Physics.Raycast(startPoint, directionToWater, out RaycastHit hit, rayDistance, combinedLayers, QueryTriggerInteraction.Collide);

                bool isBlocked = false;
                if (hitSomething)
                {
                    int hitLayer = hit.collider.gameObject.layer;
                    bool hitWater = ((1 << hitLayer) & waterLayer) != 0;
                    if (!hitWater)
                    {
                        isBlocked = true;
                    }
                }

                if (isBlocked)
                {
                    if (debug)
                    {
                        Debug.DrawRay(startPoint, directionToWater * hit.distance, Color.red, 2f);
                    }
                    continue;
                }

                // Downward raycast check
                Vector3 downwardStartPoint = new Vector3(targetPoint.x, transform.position.y + castStartHeight.Value + 10f, targetPoint.z);
                float downwardDistance = (transform.position.y + castStartHeight.Value) - targetPoint.y + 10f;
                bool downwardBlocked = Physics.Raycast(downwardStartPoint, Vector3.down, out RaycastHit downHit, downwardDistance, blockingLayers);

                if (downwardBlocked)
                {
                    if (debug)
                    {
                        Debug.DrawRay(downwardStartPoint, Vector3.down * downHit.distance, Color.yellow, 2f);
                    }
                    continue;
                }

                if (debug)
                {
                    Debug.DrawRay(startPoint, directionToWater * rayDistance, Color.green, 2f);
                    Debug.DrawRay(downwardStartPoint, Vector3.down * downwardDistance, Color.cyan, 2f);
                }

                float distance = Vector3.Distance(targetPoint, closestPoint);
                float score = preferCloserSpots.Value ? distance : Random.value;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestWater = waterCollider;
                    bestHitPoint = closestPoint;
                }
            }

            if (bestWater != null)
            {
                waterHitPoint = bestHitPoint;
                hitWaterObject = bestWater.gameObject;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adjusts waterHitPoint to ensure it's at least _minShoreDistance from shore.
        /// </summary>
        private bool EnforceMinShoreDistance(ref Vector3 waterHitPoint)
        {
            Vector3 playerPos = transform.position;
            _retrievalPoint = FindRetrievalPoint(playerPos, waterHitPoint);
            _hasRetrievalPoint = true;

            Vector2 bobber2D = new Vector2(waterHitPoint.x, waterHitPoint.z);
            Vector2 retrieval2D = new Vector2(_retrievalPoint.x, _retrievalPoint.z);
            float shoreDistance = Vector2.Distance(bobber2D, retrieval2D);

            if (shoreDistance >= _minShoreDistance.Value)
                return true;

            Vector2 pushDir = (bobber2D - retrieval2D).normalized;
            if (pushDir == Vector2.zero)
                pushDir = new Vector2(transform.forward.x, transform.forward.z).normalized;

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                float targetDist = _minShoreDistance.Value + (attempt - 1) * 0.5f;
                Vector2 candidate2D = retrieval2D + pushDir * targetDist;
                Vector3 rayOrigin = new Vector3(candidate2D.x, playerPos.y + 5f, candidate2D.y);

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 15f, waterLayer, QueryTriggerInteraction.Collide))
                {
                    waterHitPoint = new Vector3(candidate2D.x, hit.point.y, candidate2D.y);
                    _retrievalPoint = new Vector3(retrieval2D.x, hit.point.y, retrieval2D.y); // keep same Y
                    if (debug) Debug.DrawLine(_retrievalPoint, waterHitPoint, Color.magenta, 3f);
                    return true;
                }
            }

            // Fallback: keep original (better than failing)
            if (debug)            {
                FDebug($"Could not enforce min shore distance ({_minShoreDistance.Value}m). Using original point.", false, "orange");
            }
            return true;
        }

        protected virtual void OnDrawGizmos()
        {
            if (!debug) return;

            Vector3 targetPoint = transform.position + transform.forward * castDistance + Vector3.up * heightOffset;
            Vector3 startPoint = transform.position + Vector3.up * castStartHeight;

            // Draw the search sphere at target point
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(targetPoint, searchRadius);

            // Draw line from cast start point to target point
            Gizmos.color = Color.green;
            Gizmos.DrawLine(startPoint, targetPoint);

            // Draw target point sphere
            Gizmos.DrawSphere(targetPoint, 0.1f);

            // Draw cast start point to visualize where the raycast starts from
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(startPoint, 0.1f);

            // --- Retrieval Point Gizmo ---
            if (_hasRetrievalPoint)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(_retrievalPoint, _retrievalGizmoSize);

                // Optional: label
                Handles.Label(_retrievalPoint + Vector3.up * 0.3f, "Retrieval Point", EditorStyles.boldLabel);
            }
        }
        public virtual void InformWaterHit(Vector3 point)
        {
            _bobberController.InformWaterHit(point, waterLayer, _retrievalPoint);
        }

        /// <summary>
        /// Finds the first point in water along the line from player to waterHit using bisection.
        /// </summary>
        private Vector3 FindRetrievalPoint(Vector3 playerPos, Vector3 waterHitPos)
        {
            // Work in XZ plane — Y will be determined by raycast
            Vector2 player2D = new Vector2(playerPos.x, playerPos.z);
            Vector2 water2D = new Vector2(waterHitPos.x, waterHitPos.z);

            Vector2 left = player2D;   // Potentially over land
            Vector2 right = water2D;   // Known to be in water

            // Ensure we have a valid segment
            if (Vector2.Distance(left, right) < 0.01f)
                return new Vector3(waterHitPos.x, waterHitPos.y, waterHitPos.z);

            // Bisection loop
            for (int i = 0; i < _maxBisectionIterations; i++)
            {
                Vector2 mid2D = Vector2.Lerp(left, right, 0.5f);
                Vector3 mid3D = new Vector3(mid2D.x, playerPos.y + 5f, mid2D.y); // High start for downward ray

                // Raycast straight down to check what's below
                if (Physics.Raycast(mid3D, Vector3.down, out RaycastHit hit, 20f, waterLayer | blockingLayers, QueryTriggerInteraction.Collide))
                {
                    bool isWater = ((1 << hit.collider.gameObject.layer) & waterLayer) != 0;

                    if (isWater)
                    {
                        right = mid2D;
                    }
                    else
                    {
                        left = mid2D;
                    }
                }
                else
                {
                    left = mid2D;
                }
            }

            // Final point: closest to player that is still over water
            Vector2 final2D = right;
            Vector3 final3D = new Vector3(final2D.x, waterHitPos.y, final2D.y);

            //Vector3 surfacePoint = waterHitPos;
            Vector3 rayStart = waterHitPos + Vector3.up * 5f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit surfaceHit, 10f, waterLayer, QueryTriggerInteraction.Collide))
            {
                final3D = new Vector3(final2D.x, surfaceHit.point.y, final2D.y); //Updates to match the water Y pos
            }

            return final3D;
        }

        public virtual void FDebug(string value, bool uiLogger = false, string color = "white")
        {
#if UNITY_EDITOR
            if (uiLogger)
            {
                UILogger.Log(value, color);
            }
            MDebug.Log($"<B>[{name}]</B> → <B>[{this.GetType().Name}]</B> → <color={color}>{value}</color>", this);
#endif
        }
    }

    #region Inspector
#if UNITY_EDITOR
    [CanEditMultipleObjects, CustomEditor(typeof(FishingCaster))]
    public class FishingRodSettingsEditor : Editor
    {
        SerializedProperty castDistance, searchRadius, heightOffset, preferCloserSpots, waterLayer, blockingLayers,
            debug, active, castStartHeight, leftBiasStrength, rightBiasStrength, _minShoreDistance, retrievalGizmoSize,
        _maxBisectionIterations;

        protected virtual void OnEnable()
        {
            castDistance = serializedObject.FindProperty("castDistance");
            searchRadius = serializedObject.FindProperty("searchRadius");
            heightOffset = serializedObject.FindProperty("heightOffset");
            preferCloserSpots = serializedObject.FindProperty("preferCloserSpots");
            waterLayer = serializedObject.FindProperty("waterLayer");
            blockingLayers = serializedObject.FindProperty("blockingLayers");
            debug = serializedObject.FindProperty("debug");
            active = serializedObject.FindProperty("active");
            castStartHeight = serializedObject.FindProperty("castStartHeight");
            leftBiasStrength = serializedObject.FindProperty("leftBiasStrength");
            rightBiasStrength = serializedObject.FindProperty("rightBiasStrength");
            retrievalGizmoSize = serializedObject.FindProperty("_retrievalGizmoSize");
            _minShoreDistance = serializedObject.FindProperty("_minShoreDistance");
            _maxBisectionIterations = serializedObject.FindProperty("_maxBisectionIterations");

            
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(active);
                MalbersEditor.DrawDebugIcon(debug);
            }

            EditorGUILayout.Space();


            var settings = (FishingCaster)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Player Radius Casting", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Finds water within a sphere in front of the player.", MessageType.Info);

            EditorGUILayout.PropertyField(castDistance);
            EditorGUILayout.PropertyField(searchRadius);
            EditorGUILayout.PropertyField(heightOffset);
            EditorGUILayout.PropertyField(castStartHeight);
            EditorGUILayout.PropertyField(leftBiasStrength);
            EditorGUILayout.PropertyField(rightBiasStrength);

            EditorGUILayout.PropertyField(preferCloserSpots);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layer Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(waterLayer);
            EditorGUILayout.PropertyField(blockingLayers);


            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Retrieval Point", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_minShoreDistance);
            EditorGUILayout.HelpBox("Used for shore-entry detection. Visible in Scene when Debug is enabled.", MessageType.Info);

            EditorGUILayout.PropertyField(_maxBisectionIterations);
            EditorGUILayout.PropertyField(retrievalGizmoSize);

            if (settings.waterLayer == 0)
            {
                EditorGUILayout.HelpBox("Water LayerMask is not set. Click to set it to the layer 'Water'.", MessageType.Warning);
                if (GUILayout.Button("Set Water Layer to 'Water'"))
                {
                    foreach (var t in targets)
                    {
                        var inst = (FishingCaster)t;
                        Undo.RecordObject(inst, "Set Water Layer Mask");
                        inst.waterLayer = LayerMask.GetMask("Water");
                        EditorUtility.SetDirty(inst);
                    }
                }
            }

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
    #endregion
}