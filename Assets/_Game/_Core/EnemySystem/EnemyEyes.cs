using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Glowing eyes that keep an enemy readable at night: two dots under the head bone (EnemyEyeGlow shader), as bright as
    // NightHighlight's global, so they're off by day. Placed in the editor from the style (Place Eyes); dark while dead.
    public sealed class EnemyEyes : MonoBehaviour
    {
        const string EyeName = "EyeGlow";
        const float EyeScale = 0.1f;

        [SerializeField, Tooltip("Off: this prefab, variant or scene instance has no glowing eyes")] bool _glow = true;
        [SerializeField, Tooltip("Look and placement for this kind of enemy")] EyeGlowStyle _style;
        [SerializeField, Tooltip("The bone the eyes follow. Empty: the humanoid Head, else the first bone named like 'head'")]
        Transform _head;
        [SerializeField, ReadOnly] MeshRenderer[] _eyes = Array.Empty<MeshRenderer>();

        public bool Glow => _glow;
        public EyeGlowStyle Style => _style;
        public IReadOnlyList<MeshRenderer> Eyes => _eyes;

        void Awake() => SetLit(true);

        // Another look for this life (an elite's); null = the style's own.
        public void SetMaterial(Material material)
        {
            Material wanted = material != null ? material : _style != null ? _style.Material : null;
            if (wanted == null)
                return;
            foreach (MeshRenderer eye in _eyes)
                if (eye != null && eye.sharedMaterial != wanted)
                    eye.sharedMaterial = wanted;
        }

        // EnemyBody: off when killed, on when revived.
        public void SetLit(bool lit)
        {
            bool on = lit && _glow;
            foreach (MeshRenderer eye in _eyes)
                if (eye != null && eye.enabled != on)
                    eye.enabled = on;
        }

#if UNITY_EDITOR
        // Rebuilds the two eyes: on humanoid eye bones when the rig has them, else at the style's offset from the head.
        // Done in the prefab's rest pose; move the EyeGlow_L / _R children by hand afterwards if needed.
        [Button("Place Eyes"), PropertySpace]
        public string Place()
        {
            if (_style == null || _style.Material == null)
                return $"{name}: set a style with a material first";
            Animator animator = GetComponentInChildren<Animator>(true);
            Transform head = _head != null ? _head : HumanBone(animator, "Head") ?? NamedHead();
            if (head == null)
                return $"{name}: no head bone found, set Head";

            UnityEditor.Undo.RecordObject(this, "Place Eyes");
            RemoveEyes();
            Vector3 right = transform.right, up = transform.up, forward = transform.forward;
            Transform leftBone = _head == null ? HumanBone(animator, "LeftEye") : null;
            Transform rightBone = _head == null ? HumanBone(animator, "RightEye") : null;
            Vector3 left, rightEye;
            string from;
            if (leftBone != null && rightBone != null)
            {
                left = leftBone.position + forward * _style.EyeBoneForward;
                rightEye = rightBone.position + forward * _style.EyeBoneForward;
                from = "eye bones";
            }
            else
            {
                Vector3 center = head.position + right * _style.Offset.x + up * _style.Offset.y + forward * _style.Offset.z;
                left = center - right * (_style.Spacing * 0.5f);
                rightEye = center + right * (_style.Spacing * 0.5f);
                from = $"head offset ({head.name})";
            }
            _eyes = new[] { MakeEye(head, left, "L"), MakeEye(head, rightEye, "R") };
            UnityEditor.EditorUtility.SetDirty(this);
            return $"{name}: eyes placed from {from}";
        }

        [Button, PropertySpace]
        public void RemoveEyes()
        {
            UnityEditor.Undo.RecordObject(this, "Remove Eyes");
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.StartsWith(EyeName + "_") && !UnityEditor.PrefabUtility.IsPartOfPrefabInstance(t))
                    UnityEditor.Undo.DestroyObjectImmediate(t.gameObject);
            _eyes = Array.Empty<MeshRenderer>();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        MeshRenderer MakeEye(Transform head, Vector3 position, string side)
        {
            var go = new GameObject($"{EyeName}_{side}") { layer = gameObject.layer };
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Place Eyes");
            go.transform.SetParent(head, false);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(transform.forward, transform.up));
            go.transform.localScale = Vector3.one * (EyeScale / Mathf.Max(head.lossyScale.x, 0.0001f));
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var eye = go.AddComponent<MeshRenderer>();
            eye.sharedMaterial = _style.Material;
            eye.shadowCastingMode = ShadowCastingMode.Off;
            eye.receiveShadows = false;
            eye.lightProbeUsage = LightProbeUsage.Off;
            eye.reflectionProbeUsage = ReflectionProbeUsage.Off;
            eye.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return eye;
        }

        static Transform HumanBone(Animator animator, string humanName)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return null;
            foreach (HumanBone bone in animator.avatar.humanDescription.human)
                if (bone.humanName == humanName)
                    return FindChild(animator.transform, bone.boneName);
            return null;
        }

        // Depth-first, so a parent "Head" wins over its "HeadTop_End".
        Transform NamedHead()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (t != transform && n.Contains("head") && !n.StartsWith("eyeglow") && !n.Contains("end") && !n.Contains("top") && !n.Contains("nub"))
                    return t;
            }
            return null;
        }

        static Transform FindChild(Transform root, string childName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == childName)
                    return t;
            return null;
        }
#endif
    }
}
