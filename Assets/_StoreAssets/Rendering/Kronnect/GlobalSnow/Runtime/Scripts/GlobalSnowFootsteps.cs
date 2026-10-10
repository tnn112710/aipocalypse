using UnityEngine;

namespace GlobalSnowEffect {

    [AddComponentMenu("Effects/Global Snow Footsteps")]
    [DisallowMultipleComponent]
    public class GlobalSnowFootsteps : MonoBehaviour {

        // set only when this component was added automatically by a GlobalSnow in Third Person mode; manual components are never touched
        [SerializeField, HideInInspector] bool autoManaged;
        [SerializeField, HideInInspector] GlobalSnow autoManagedOwner;

        public void SetAutoManaged(GlobalSnow owner) {
            autoManaged = true;
            autoManagedOwner = owner;
        }

        public bool isAutoManaged { get { return autoManaged; } }

        [Tooltip("Feet transforms (even indices = left feet, odd indices = right feet). If empty, humanoid feet are auto-detected from the Animator.")]
        public Transform[] feet;

        [Tooltip("Foot height over the character base below which the foot counts as planted.")]
        public float plantHeight = 0.12f;

        [Tooltip("Maximum world speed in m/s of the foot bone to count as planted.")]
        public float maxPlantSpeed = 0.5f;

        [Tooltip("Offset in meters from the foot bone toward the toes; centers the footprint on the actual contact area.")]
        public float toeOffset = 0.07f;

        [Tooltip("Horizontal distance in meters the foot must move away from its last footprint to leave a new one (covers small shuffling steps that never lift the foot).")]
        public float replantDistance = 0.3f;

        [Tooltip("Log plant events and per-foot height/speed to the console (debug).")]
        public bool debugLog;

        Transform[] activeFeet;
        bool[] planted;
        Vector3[] plantedPos;
        // windowed samples: per-frame deltas are unusable with Animate Physics animators
        Vector3[] histPos, histPos2;
        float[] histTime, histTime2;
        Vector3 rootHistPos, rootHistPos2;
        float rootHistTime, rootHistTime2;
        bool hasHist;
        float nextDebugTime;

        void OnEnable() {
            activeFeet = null;
            hasHist = false;
        }

        void LateUpdate() {
            GlobalSnow snow;
            if (autoManaged) {
                // self-clean when the owning GlobalSnow no longer targets this character in Third Person mode
                if (autoManagedOwner == null || autoManagedOwner.groundCheck != GroundCheck.ThirdPerson || autoManagedOwner.footprintsCharacter == null || autoManagedOwner.footprintsCharacter.gameObject != gameObject) {
                    if (Application.isPlaying) Destroy(this);
                    return;
                }
                snow = autoManagedOwner;
            } else {
                snow = GlobalSnow.instance;
            }
            // a disabled Global Snow must not receive stamps, but the component is kept alive
            if (snow == null || !snow.isActiveAndEnabled || !snow.footprints) return;
            if (activeFeet == null && !TryGetFeet()) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // re-arm only well above any trigger height, or toe-off re-fires the same plant
            float relativeCap = plantHeight + 0.08f;
            float releaseHeight = relativeCap + 0.06f;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) return;
            fwd.Normalize();
            int count = activeFeet.Length;
            float now = Time.time;
            if (!hasHist) {
                rootHistPos = rootHistPos2 = transform.position;
                rootHistTime = rootHistTime2 = now;
            }
            if (now - rootHistTime2 >= 0.05f) {
                rootHistPos = rootHistPos2;
                rootHistTime = rootHistTime2;
                rootHistPos2 = transform.position;
                rootHistTime2 = now;
            }
            float rootSpeed = (transform.position - rootHistPos).magnitude / Mathf.Max(now - rootHistTime, 0.02f);
            // support foot speed grows with gait speed: the plant gate must scale with it
            float effectiveMaxSpeed = Mathf.Max(maxPlantSpeed, rootSpeed * 0.4f);
            for (int k = 0; k < count; k++) {
                Transform foot = activeFeet[k];
                if (foot == null) continue;
                Vector3 footPos = foot.position;
                if (!hasHist) {
                    histPos[k] = histPos2[k] = footPos;
                    histTime[k] = histTime2[k] = now;
                }
                // speed over a ~0.05-0.1s window
                if (now - histTime2[k] >= 0.05f) {
                    histPos[k] = histPos2[k];
                    histTime[k] = histTime2[k];
                    histPos2[k] = footPos;
                    histTime2[k] = now;
                }
                float footSpeed = (footPos - histPos[k]).magnitude / Mathf.Max(now - histTime[k], 0.02f);
                float h = footPos.y - transform.position.y;
                int mate = k ^ 1;
                Transform other = mate < count ? activeFeet[mate] : null;
                float otherH = other != null ? other.position.y - transform.position.y : float.MaxValue;
                // plant = stationary in world space AND resting low: below the absolute threshold,
                // or clearly the support foot of the pair (bone heights vary per rig)
                bool low = h < plantHeight || (h < relativeCap && h <= otherH - 0.02f);
                if (!planted[k] && low && footSpeed <= effectiveMaxSpeed) {
                    planted[k] = true;
                    plantedPos[k] = footPos;
                    Vector3 pos = footPos + fwd * toeOffset;
                    if (debugLog) DebugOut($"PLANT {((k & 1) == 0 ? "L" : "R")} at {pos} h={h:F3} v={footSpeed:F2}");
                    snow.FootPlantAt(pos, fwd, (k & 1) == 0 ? FootSide.Left : FootSide.Right);
                } else if (planted[k]) {
                    Vector3 slide = footPos - plantedPos[k];
                    slide.y = 0f;
                    if (h > releaseHeight || slide.sqrMagnitude > replantDistance * replantDistance) {
                        planted[k] = false;
                    }
                }
                if (debugLog && Time.time >= nextDebugTime) {
                    DebugOut($"{((k & 1) == 0 ? "L" : "R")} h={h:F3} v={footSpeed:F2} planted={planted[k]} (rootV={rootSpeed:F2}, maxV={effectiveMaxSpeed:F2})");
                    if (k == count - 1) nextDebugTime = Time.time + 0.5f;
                }
            }
            hasHist = true;
        }

        bool warnedNoFeet;

        bool TryGetFeet() {
            if (feet != null && feet.Length > 0) {
                activeFeet = feet;
            } else {
                Animator anim = GetComponentInChildren<Animator>();
                if (anim == null || !anim.isHuman) {
                    WarnNoFeet("no humanoid Animator found on this object or its children");
                    return false;
                }
                Transform l = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform r = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                if (l == null || r == null) {
                    WarnNoFeet("the humanoid Animator has no foot bones");
                    return false;
                }
                activeFeet = new Transform[] { l, r };
            }
            planted = new bool[activeFeet.Length];
            plantedPos = new Vector3[activeFeet.Length];
            histPos = new Vector3[activeFeet.Length];
            histPos2 = new Vector3[activeFeet.Length];
            histTime = new float[activeFeet.Length];
            histTime2 = new float[activeFeet.Length];
            hasHist = false;
            return true;
        }

        void DebugOut(string msg) {
            Debug.Log("[Footsteps] " + msg);
#if UNITY_EDITOR
            try {
                System.IO.File.AppendAllText(Application.dataPath + "/../Logs/footsteps_debug.log", System.DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + "\n");
            } catch { }
#endif
        }

        void WarnNoFeet(string reason) {
            if (warnedNoFeet) return;
            warnedNoFeet = true;
            Debug.LogWarning("Global Snow Footsteps on '" + name + "': " + reason + ". Add this component to the character root or assign the feet transforms manually.", this);
        }
    }
}
