using System.Collections.Generic;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.WeaponSystem;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace Aipocalypse.Tests
{
    // Bullets as data under one tick, followed through their pooled tracers.
    public class BulletSystemTests
    {
        // Away from whatever scene is open, so its colliders can't be hit.
        static readonly Vector3 Far = new(1000f, 1000f, 1000f);

        readonly List<Object> _created = new();
        IObjectResolver _resolver;
        EffectPool _effects;
        BulletSystem _bullets;
        BulletEffects _bulletEffects;
        PooledEffect _tracer;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.Register<DamageService>(Lifetime.Singleton);
            builder.Register<EffectPool>(Lifetime.Singleton);
            builder.Register<BulletSystem>(Lifetime.Singleton);
            _resolver = builder.Build();
            _effects = _resolver.Resolve<EffectPool>();
            _bullets = _resolver.Resolve<BulletSystem>();

            _tracer = Effect("Tracer", maxActive: 0);
            _bulletEffects = ScriptableObject.CreateInstance<BulletEffects>();
            _created.Add(_bulletEffects);
            var so = new SerializedObject(_bulletEffects);
            so.FindProperty("_tracer").objectReferenceValue = _tracer;
            so.FindProperty("_tracerConvergeTime").floatValue = 0f;
            so.FindProperty("_tracerLinger").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            _resolver.Dispose();
            GameObject root = GameObject.Find("[Effects]");
            if (root != null)
                Object.DestroyImmediate(root);
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        PooledEffect Effect(string name, int maxActive)
        {
            var go = new GameObject(name);
            _created.Add(go);
            var effect = go.AddComponent<PooledEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_lifetime").floatValue = 0f;
            so.FindProperty("_maxActive").intValue = maxActive;
            so.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        void SetMaxActive(PooledEffect effect, int maxActive)
        {
            var so = new SerializedObject(effect);
            so.FindProperty("_maxActive").intValue = maxActive;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject Box(Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = Far + position;
            box.transform.localScale = scale;
            _created.Add(box);
            Physics.SyncTransforms();
            return box;
        }

        void Fire(Vector3 origin, float range, Transform ignore = null) => _bullets.Fire(new BulletShot
        {
            Origin = Far + origin,
            VisualOrigin = Far + origin,
            Direction = Vector3.forward,
            Speed = 100f,
            Range = range,
            Layers = Physics.DefaultRaycastLayers,
            Ignore = ignore,
            Effects = _bulletEffects,
        });

        // The tracer each live bullet is moving, spawned in order under the pool root.
        static List<Transform> LiveTracers()
        {
            var live = new List<Transform>();
            GameObject root = GameObject.Find("[Effects]");
            if (root == null)
                return live;
            foreach (Transform child in root.transform)
                if (child.gameObject.activeSelf)
                    live.Add(child);
            return live;
        }

        [Test]
        public void Bullet_FliesUntilItsRange_ThenReturnsItsTracer()
        {
            Fire(Vector3.zero, range: 10f);

            _bullets.Advance(0.05f);
            Assert.AreEqual(1, LiveTracers().Count);
            Assert.AreEqual(5f, LiveTracers()[0].position.z - Far.z, 0.01f);

            _bullets.Advance(0.06f);
            Assert.AreEqual(0, LiveTracers().Count);
        }

        [Test]
        public void Tracer_StopsAtTheHitPoint_AndLingersForItsTrail()
        {
            var so = new SerializedObject(_bulletEffects);
            so.FindProperty("_tracerLinger").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Box(new Vector3(0f, 0f, 3f), new Vector3(4f, 4f, 0.2f));

            Fire(Vector3.zero, range: 100f);
            _bullets.Advance(0.05f);

            List<Transform> live = LiveTracers();
            Assert.AreEqual(1, live.Count, "kept after the hit so its trail can reach the wall");
            Assert.AreEqual(2.9f, live[0].position.z - Far.z, 0.01f, "at the hit point, not where the step would have ended");
        }

        [Test]
        public void Bullet_SkipsTheShooter_AndStopsAtTheWall()
        {
            GameObject shooter = Box(Vector3.zero, Vector3.one);
            Box(new Vector3(0f, 0f, 3f), new Vector3(4f, 4f, 0.2f));

            Assert.IsTrue(_bullets.TryHit(Far, Vector3.forward, 10f, Physics.DefaultRaycastLayers, shooter.transform, out RaycastHit hit));
            Assert.AreEqual(2.9f, hit.distance, 0.01f, "the shooter's own collider is skipped");

            Fire(Vector3.zero, range: 100f, ignore: shooter.transform);
            _bullets.Advance(0.02f);
            Assert.AreEqual(1, LiveTracers().Count, "2 m out, short of the wall");
            _bullets.Advance(0.02f);
            Assert.AreEqual(0, LiveTracers().Count, "stopped at the wall");
        }

        [Test]
        public void BulletHole_StaysOnTheTargetItHit_AtItsOwnSize()
        {
            PooledEffect hole = Effect("Hole", maxActive: 0);
            var decalGo = new GameObject("Decal");
            decalGo.transform.SetParent(hole.transform, false);
            var decal = decalGo.AddComponent<DecalProjector>();
            decal.scaleMode = DecalScaleMode.ScaleInvariant;
            decal.size = new Vector3(0.1f, 0.1f, 0.1f);
            var so = new SerializedObject(_bulletEffects);
            so.FindProperty("_bulletHole").objectReferenceValue = hole;
            so.FindProperty("_bulletHoleScale").vector2Value = new Vector2(2f, 2f);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject target = Box(new Vector3(0f, 0f, 3f), new Vector3(4f, 1f, 0.2f));
            Fire(Vector3.zero, range: 100f);
            _bullets.Advance(0.05f);

            DecalProjector spawned = target.GetComponentInChildren<DecalProjector>();
            Assert.IsNotNull(spawned, "the hole is parented to the collider it hit");
            Assert.AreEqual(new Vector3(0.2f, 0.2f, 0.1f), spawned.size, "only width and height scale");

            target.transform.position += Vector3.right * 5f;
            Assert.AreEqual(target.transform.position.x, spawned.transform.position.x, 0.01f, "it moves with the target");
        }

        [Test]
        public void CappedPool_RecyclesTheOldestEffect()
        {
            PooledEffect prefab = Effect("Flash", maxActive: 2);

            PooledEffect first = _effects.Spawn(prefab, Vector3.zero, Quaternion.identity);
            PooledEffect second = _effects.Spawn(prefab, Vector3.zero, Quaternion.identity);
            PooledEffect third = _effects.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Assert.AreSame(first, third, "the oldest was taken back and reused");
            Assert.IsTrue(second.gameObject.activeSelf);
        }

        [Test]
        public void RecycledTracer_FollowsOnlyTheBulletItWasHandedTo()
        {
            SetMaxActive(_tracer, 1);
            Fire(Vector3.zero, range: 100f);
            Fire(new Vector3(5f, 0f, 0f), range: 100f);

            _bullets.Advance(0.01f);
            List<Transform> live = LiveTracers();
            Assert.AreEqual(1, live.Count);
            Assert.AreEqual(5f, live[0].position.x - Far.x, 0.01f, "the second bullet owns the only tracer");

            _bullets.Advance(1.5f);
            Assert.AreEqual(0, LiveTracers().Count, "the first bullet ending doesn't take it back early; the second returns it at its range");
        }
    }
}
