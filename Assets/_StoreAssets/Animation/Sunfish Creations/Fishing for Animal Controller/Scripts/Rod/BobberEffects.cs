using UnityEngine;

namespace SunfishCreations.Effects
{
    public class BobberEffects : MonoBehaviour
    {
        public ParticleSystem leftSideV;
        public ParticleSystem rightSideV;

        // Cache rotations to avoid GC
        private Quaternion _leftRotation;
        private Quaternion _rightRotation;
        private bool _vRotationSet = false;

        // Call this ONCE when fleeing starts, passing the flee direction
        public void SetVDirection(Vector3 fleeDirection)
        {
            Vector3 backward = -fleeDirection;
            backward.y = 0f;
            if (backward == Vector3.zero) return;

            float angle = Mathf.Atan2(backward.x, backward.z) * Mathf.Rad2Deg;
            Quaternion baseRot = Quaternion.Euler(0, angle, 0);

            _leftRotation = baseRot * Quaternion.Euler(0, -15, 0);
            _rightRotation = baseRot * Quaternion.Euler(0, +15, 0);

            _vRotationSet = true;
        }

        public virtual void StartVEffect()
        {
            // Apply rotation only if it was set
            if (_vRotationSet)
            {
                leftSideV.transform.rotation = _leftRotation;
                rightSideV.transform.rotation = _rightRotation;
            }

            leftSideV.Play();
            rightSideV.Play();
        }

        public virtual void StopVEffect(bool clear = false)
        {
            var clearBehavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            leftSideV.Stop(true, clearBehavior);
            rightSideV.Stop(true, clearBehavior);
        }

        //Called by external Event
        public void FishGotAway()
        {
            StopVEffect(true);
        }
    }
}