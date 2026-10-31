using System;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace AshTools.ControlRigPro.Constraints
{
    [Serializable]
    public struct JiggleBoneData : IAnimationJobData
    {
        [SyncSceneToStream] public Transform bone;
        [SyncSceneToStream] public Transform reference;
        [SyncSceneToStream, Range(0f, 200f)] public float stiffness;
        [SyncSceneToStream, Range(0f, 50f)] public float damping;
        [SyncSceneToStream] public float mass;
        [SyncSceneToStream] public float maxOffset;
        [SyncSceneToStream] public Vector3 gravity;

        [NonSerialized] public Vector3 restLocal;

        public bool IsValid() => bone != null;
        public void SetDefaultValues()
        {
            bone = null;
            reference = null;
            stiffness = 40f;
            damping = 8f;
            mass = 1f;
            maxOffset = 0f;
            gravity = Vector3.zero;
            restLocal = Vector3.zero;
        }
    }

    public class JiggleBoneBinder : AnimationJobBinder<JiggleBoneJob, JiggleBoneData>
    {
        public override JiggleBoneJob Create(Animator animator, ref JiggleBoneData data, Component component)
        {
            var refTx = data.reference != null ? data.reference : (data.bone != null ? data.bone.parent : null);
            var rest = Vector3.zero;
            if (data.bone != null)
            {
                if (refTx != null) rest = Quaternion.Inverse(refTx.rotation) * (data.bone.position - refTx.position);
                else rest = data.bone.position;
            }

            return new JiggleBoneJob
            {
                handle = ReadWriteTransformHandle.Bind(animator, data.bone),
                refHandle = ReadOnlyTransformHandle.Bind(animator, refTx),
                jobWeight = FloatProperty.Bind(animator, component, "m_Weight"),
                k = FloatProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.stiffness))),
                d = FloatProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.damping))),
                m = FloatProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.mass))),
                clamp = FloatProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.maxOffset))),
                g = Vector3Property.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.gravity))),
                simPos = Vector3Property.BindCustom(animator, "JiggleBone.simPos"),
                simVel = Vector3Property.BindCustom(animator, "JiggleBone.simVel"),
                init = FloatProperty.BindCustom(animator, "JiggleBone.init"),
                restLocal = rest
            };
        }

        public override void Destroy(JiggleBoneJob job) { }
    }

    [BurstCompile]
    public struct JiggleBoneJob : IWeightedAnimationJob
    {
        public ReadWriteTransformHandle handle;
        public ReadOnlyTransformHandle refHandle;

        public FloatProperty jobWeight { get; set; }
        public FloatProperty k;
        public FloatProperty d;
        public FloatProperty m;
        public FloatProperty clamp;
        public Vector3Property g;

        public Vector3Property simPos;
        public Vector3Property simVel;
        public FloatProperty init;
        public Vector3 restLocal;

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            float w = jobWeight.Get(stream);
            if (w <= 0f)
            {
                init.Set(stream, 0f);
                return;
            }

            refHandle.GetGlobalTR(stream, out Vector3 refPos, out Quaternion refRot);
            Vector3 target = refPos + refRot * restLocal;

            if (init.Get(stream) < 0.5f)
            {
                handle.GetGlobalTR(stream, out Vector3 cPos, out _);
                simPos.Set(stream, cPos);
                simVel.Set(stream, Vector3.zero);
                init.Set(stream, 1f);
            }

            float dt = stream.deltaTime;
            float stiffness = Mathf.Max(0f, k.Get(stream));
            float damping = Mathf.Max(0f, d.Get(stream));
            float mass = Mathf.Max(1e-6f, m.Get(stream));
            float maxR = Mathf.Max(0f, clamp.Get(stream));
            Vector3 grav = g.Get(stream);

            Vector3 x = simPos.Get(stream);
            Vector3 v = simVel.Get(stream);

            Vector3 disp = x - target;
            Vector3 accel = (-stiffness * disp) + (-damping * v) + grav;
            v += accel * dt;
            x += v * dt;

            if (maxR > 0f)
            {
                Vector3 fromT = x - target;
                float mag = fromT.magnitude;
                if (mag > maxR)
                {
                    Vector3 n = fromT / Mathf.Max(1e-6f, mag);
                    x = target + n * maxR;
                    v -= Vector3.Dot(v, n) * n;
                }
            }

            simPos.Set(stream, x);
            simVel.Set(stream, v);

            handle.GetGlobalTR(stream, out Vector3 basePos, out Quaternion baseRot);
            Vector3 outPos = Vector3.Lerp(basePos, x, w);
            handle.SetGlobalTR(stream, outPos, baseRot);
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Animation Rigging/Control Rig Pro/Jiggle Bone")]
    public class JiggleBoneConstraint : RigConstraint<JiggleBoneJob, JiggleBoneData, JiggleBoneBinder> { }
}