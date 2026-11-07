//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Used by the FX system to define a blendable value that may or may not exist
    /// </summary>
    [System.Serializable]
    public class Overridable<T>
    {
        [SerializeField]
        protected T value;

        public virtual T Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
            }
        }

        [SerializeField]
        private bool overrideState;
        public bool HasOverride
        {
            get { return overrideState; }
            set { overrideState = value; }
        }
        public Overridable(T value, bool overrideState = false)
        {
            this.overrideState = overrideState;
            this.value = value;
        }

        public T GetValue(T fallback = default)
        {
            return overrideState ? Value : fallback;
        }

        public static implicit operator T(Overridable<T> data)
        {
            return data.GetValue();
        }
        public static implicit operator Overridable<T>(T value)
        {
            return new Overridable<T>(value, true);
        }
    }
}