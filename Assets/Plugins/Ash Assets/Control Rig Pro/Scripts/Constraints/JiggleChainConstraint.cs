using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace AshTools.ControlRigPro.Constraints
{
    [Serializable]
    public struct JiggleChainData : IAnimationJobData
    {
        [SyncSceneToStream] public Transform rootBone;
        [SyncSceneToStream] public Transform tipBone;

        [SyncSceneToStream] public float mass;
        [SyncSceneToStream, Range(0f, 1f)] public float stiffness;
        [SyncSceneToStream, Range(0.1f, 100f)] public float dynamicOffset;

        [SyncSceneToStream] public Vector3 aimLocalVec;
        [SyncSceneToStream] public Vector3 upLocalVec;

        [SyncSceneToStream] public bool rollEnabled;
        [SyncSceneToStream] public Vector3 gravity;
        [SyncSceneToStream] public Vector3 externalForce;

        [SyncSceneToStream, Range(0f, 1f)] public float damping;
        [SyncSceneToStream, Range(0f, 1f)] public float motionDecay;

        public bool IsValid()
        {
            if (rootBone == null || tipBone == null || rootBone == tipBone) return false;
            Transform t = tipBone;
            while (t != null && t != rootBone) t = t.parent;
            return t == rootBone;
        }

        public void SetDefaultValues()
        {
            rootBone = null;
            tipBone = null;
            mass = 1f;
            stiffness = 0.5f;
            dynamicOffset = 4f;
            aimLocalVec = new Vector3(1f, 0f, 0f);
            upLocalVec = new Vector3(0f, 1f, 0f);
            rollEnabled = false;
            gravity = Vector3.zero;
            externalForce = Vector3.zero;
            motionDecay = 0.1f;
            damping = 0.0f;
        }
    }

    public class JiggleChainBinder : AnimationJobBinder<JiggleChainJob, JiggleChainData>
    {
        public override JiggleChainJob Create(Animator animator, ref JiggleChainData data, Component component)
        {
            var chain = new List<Transform>();
            var t = data.tipBone;
            while (t != data.rootBone)
            {
                chain.Add(t);
                t = t.parent;
            }
            chain.Reverse();

            var job = new JiggleChainJob
            {
                bones = new NativeArray<ReadWriteTransformHandle>(chain.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                aimState = new NativeArray<JiggleChainJob.Particle>(chain.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                rollState = new NativeArray<JiggleChainJob.Particle>(chain.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                aimLocal = math.normalize(data.aimLocalVec),
                upLocal = math.normalize(data.upLocalVec)
            };

            float3 aimOffset = job.aimLocal * data.dynamicOffset;
            float3 upOffset = job.upLocal * data.dynamicOffset;

            for (int i = 0; i < chain.Count; ++i)
            {
                job.bones[i] = ReadWriteTransformHandle.Bind(animator, chain[i]);
                float4x4 tx = float4x4.TRS(chain[i].position, chain[i].rotation, new float3(1f));
                job.aimState[i] = new JiggleChainJob.Particle { position = math.transform(tx, aimOffset), velocity = float3.zero };
                job.rollState[i] = new JiggleChainJob.Particle { position = math.transform(tx, upOffset), velocity = float3.zero };
            }

            job.massProp = FloatProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.mass)));
            job.stiffProp = FloatProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.stiffness)));
            job.offsetProp = FloatProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.dynamicOffset)));
            job.rollProp = BoolProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.rollEnabled)));
            job.gravityProp = Vector3Property.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.gravity)));
            job.forceProp = Vector3Property.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.externalForce)));
            job.decayProp = FloatProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.motionDecay)));
            job.dampProp = FloatProperty.Bind(animator, component, ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(data.damping)));

            return job;
        }

        public override void Destroy(JiggleChainJob job)
        {
            job.bones.Dispose();
            job.aimState.Dispose();
            job.rollState.Dispose();
        }
    }

    [BurstCompile]
    public struct JiggleChainJob : IWeightedAnimationJob
    {
        const float kEps = 1e-6f;
        const float kStiffMul = 1f;
        const float kForceMul = 62f;
        const float kFixedDt = 1f / 60f;

        public struct SimParams
        {
            public float mass;
            public float decay;
            public float damping;
            public float stiffness;
            public float restDist;
            public float dt;
        }

        public struct Particle
        {
            public float3 position;
            public float3 velocity;

            public void Init(float3 p)
            {
                position = p;
                velocity = float3.zero;
            }

            public void Distance(ref float3 dyn, float3 pin, float stiff, float rest)
            {
                float3 d = pin - dyn;
                float m2 = math.lengthsq(d);
                if (m2 > kEps)
                {
                    float m = math.sqrt(m2);
                    dyn += d * (stiff * ((m - rest) / m));
                }
            }

            public void Step(float3 root, float3 target, float3 f, in SimParams s)
            {
                float3 a = (f - s.damping * velocity) / s.mass;
                velocity += a * s.dt;
                velocity *= s.decay;
                float3 prev = position;
                position += velocity * s.dt;
                Distance(ref position, root, s.stiffness, s.restDist);
                Distance(ref position, target, s.stiffness, 0f);
                velocity = (position - prev) / s.dt;
            }
        }

        public NativeArray<ReadWriteTransformHandle> bones;
        public float3 aimLocal;
        public float3 upLocal;
        public NativeArray<Particle> aimState;
        public NativeArray<Particle> rollState;

        public FloatProperty massProp;
        public FloatProperty stiffProp;
        public FloatProperty offsetProp;
        public BoolProperty rollProp;
        public Vector3Property gravityProp;
        public Vector3Property forceProp;
        public FloatProperty decayProp;
        public FloatProperty dampProp;

        public SimParams sim;
        public FloatProperty jobWeight { get; set; }

        static quaternion MaskProject(quaternion baseRot, quaternion delta, float3 mask)
        {
            quaternion inv = math.inverse(baseRot);
            quaternion tmp = math.mul(inv, math.mul(delta, baseRot));
            tmp.value.x *= mask.x;
            tmp.value.y *= mask.y;
            tmp.value.z *= mask.z;
            return math.mul(baseRot, math.mul(tmp, inv));
        }

        static quaternion FromTo(float3 a, float3 b)
        {
            float t = math.dot(math.normalize(a), math.normalize(b));
            if (t >= 1f) return quaternion.identity;
            if (t <= -1f)
            {
                float3 axis = math.cross(a, new float3(1f, 0f, 0f));
                if (math.lengthsq(axis) == 0f) axis = math.cross(a, math.up());
                return quaternion.AxisAngle(axis, (float)math.PI);
            }
            return quaternion.AxisAngle(math.normalize(math.cross(a, b)), math.acos(t));
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            float w = jobWeight.Get(stream);
            float off = offsetProp.Get(stream);
            bool doRoll = rollProp.Get(stream);

            sim.mass = massProp.Get(stream);
            sim.decay = AnimationRuntimeUtils.Square(1f - decayProp.Get(stream));
            sim.damping = 1f - dampProp.Get(stream);
            sim.stiffness = AnimationRuntimeUtils.Square(stiffProp.Get(stream) * kStiffMul);
            sim.restDist = off;
            sim.dt = kFixedDt;

            float3 g = gravityProp.Get(stream);
            float3 f = forceProp.Get(stream);
            float3 forces = ((g * sim.mass) + f) * kForceMul;

            for (int i = 0, n = bones.Length; i < n; ++i)
            {
                var h = bones[i];
                h.GetGlobalTR(stream, out Vector3 p, out Quaternion r);

                float4x4 tx = float4x4.TRS(p, r, new float3(1f));
                float3 aimT = math.transform(tx, (aimLocal * off));
                float3 rollT = math.transform(tx, (upLocal * off));

                var a = aimState[i];
                var rl = rollState[i];

                float rem = math.abs(stream.deltaTime);

                if (w > 0f && rem > 0f)
                {
                    float3 rootP = p;
                    quaternion rootR = r;

                    while (rem > 0f)
                    {
                        a.Step(rootP, aimT, forces, sim);
                        if (doRoll) rl.Step(rootP, rollT, float3.zero, sim);
                        rem -= kFixedDt;
                    }

                    float3 mask = math.abs(aimLocal);
                    quaternion aimDelta = MaskProject(rootR, FromTo(aimT - rootP, a.position - rootP), new float3(1f) - mask);
                    quaternion rollDelta = doRoll ? MaskProject(rootR, FromTo(rollT - rootP, rl.position - rootP), mask) : quaternion.identity;

                    bones[i].SetRotation(stream, math.slerp(bones[i].GetRotation(stream), math.mul(rollDelta, math.mul(aimDelta, rootR)), w));
                }
                else
                {
                    a.Init(aimT);
                    rl.Init(rollT);
                    AnimationRuntimeUtils.PassThrough(stream, h);
                }

                aimState[i] = a;
                rollState[i] = rl;
                bones[i] = h;
            }
        }
    }

    [DisallowMultipleComponent]
    public class JiggleChainConstraint : RigConstraint<JiggleChainJob, JiggleChainData, JiggleChainBinder> { }
}