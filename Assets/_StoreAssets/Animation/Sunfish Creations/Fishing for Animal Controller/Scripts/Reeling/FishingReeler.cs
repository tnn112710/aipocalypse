using MalbersAnimations.Controller;
using MalbersAnimations.Scriptables;
using SunfishCreations.Controller;
using Unity.Collections;
using UnityEngine;

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/fishing-reeler")]
    public class FishingReeler : MonoBehaviour
    {
        [SerializeField, ReadOnly] private RodReeler rodReeler;

        private BobberController bobberController;
        private bool isReeling;
        public MAnimal animal;

        public FloatReference speed = 1f;
        public FloatReference requiredDistance = 5f;

        protected virtual void Awake()
        {
            animal = GetComponentInParent<MAnimal>();

            bobberController = GetComponent<BobberController>();

            bobberController.OnReelerComplete.AddListener(HandleReelerComplete);
        }

        protected virtual void HandleReelerComplete()
        {
            StopReeling();
            animal.State_Get<Fishing>().ReelingInComplete();
        }

        protected virtual void GetReference()
        {
            if (animal == null)
                animal = GetComponentInParent<MAnimal>();

            if (rodReeler == null)
                rodReeler = animal != null ? animal.GetComponentInChildren<RodReeler>(true) : null;
        }

        /// <summary>
        /// The Fishing State disables the Holster inputs for its whole duration, so the rod cannot be
        /// stored while reeling, hooked, or while a caught fish is being presented.
        /// </summary>

        //Called by Action in the animal
        public virtual void ReelActionStart()
        {
            GetReference();
            isReeling = true;
            if (rodReeler != null)
                rodReeler.StartReeling(speed);
            if (bobberController != null)
                bobberController.StartReeling(speed, requiredDistance);
        }

        //Called by Action in the animal
        public virtual void ReelActionEnd()
        {
            StopReeling();
        }

        private void StopReeling()
        {
            if (isReeling)
            {
                isReeling = false;
                if (rodReeler != null)
                    rodReeler.StopReeling();
                if (bobberController != null)
                    bobberController.StopReeling();
            }
        }

        protected virtual void OnDisable()
        {
            StopReeling();
        }
    } 
}
