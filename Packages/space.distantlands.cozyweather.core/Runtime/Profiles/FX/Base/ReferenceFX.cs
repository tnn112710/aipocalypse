//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    public abstract class ReferenceFX<T> : CozyFXProfile where T : Component
    {
        public T instance;

        /// <summary>
        /// Override to instantiate a prefab rather than an empty game object.
        /// </summary>
        internal virtual T Prefab => null;

        [SerializeField]
        private TransitionTimeModifier transitionTimeModifier = TransitionTimeModifier.Linear;

        public override bool IsValid => instance != null;
        public override TransitionTimeModifier TransitionTimeModifier => transitionTimeModifier;

        public override bool InitializeEffect()
        {
            if (!Application.isPlaying)
                return false;

            CozyWeather weatherManager = CozyWeather.Instance;

            if (weatherManager == null) return false;

            if (instance == null)
            {
                instance = weatherManager.VFXProvider.GetFXRuntimeRef<T>(name);
                if (instance)
                    return true;

                if (Prefab != null)
                {
                    instance = Instantiate(Prefab);
                }
                else
                {
                    instance = new GameObject().AddComponent<T>();
                }

                instance.gameObject.name = name;
                if (weatherManager.VFXProvider.GetFXParent<T>(out Transform parent))
                {
                    instance.transform.parent = parent;
                }
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                GetDefaultInstance();
            }

            return true;
        }

        /// <summary>
        /// Set the default values for a component. Set values on <i>instance</i>.
        /// <example>
        ///This shows how to set the default values for an audio source.
        ///<code>
        /// instance.volume = 0;
        /// instance.clip = myClip;
        /// etc...
        ///</code>
        ///</example>
        /// </summary>
        public abstract void GetDefaultInstance();

        public override void PlayEffect(float weight)
        {
            if (instance == null && !InitializeEffect())
                return;

            if (weight == 0)
            {
                StopReference();
                return;
            }

            UpdateReference(Easing.Evaluate(weight, TransitionTimeModifier));

        }

        public abstract void UpdateReference(float weight);


        public override void StopEffect()
        {
            if (instance == null && !InitializeEffect())
                return;

            StopReference();
        }

        public abstract void StopReference();

        public override void DeinitializeEffect()
        {
            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
        }
    }
}