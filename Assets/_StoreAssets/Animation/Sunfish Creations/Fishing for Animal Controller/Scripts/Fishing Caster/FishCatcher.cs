using MalbersAnimations.Controller;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using MalbersAnimations.Scriptables;
using SunfishCreations.Animations;
using SunfishCreations.Events;
using System;

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/fish-catcher")]
    public class FishCatcher : MonoBehaviour, IFishSpawner
    {
        [SerializeField] private IntReference _fishCount;
        public FEvent FishingEndPresentationEvent;

        public UnityEvent OnFishPresented;

        public FishHolder fishHolder;

        public Transform LeftHand { get; private set; }

        private MAnimal _animal;

        private Dictionary<FishData, GameObject> _fishPool = new Dictionary<FishData, GameObject>();
        private GameObject _currentActiveFish;
        private FishJoint _fishJoint;

        private Transform _holderTransform;

        public bool FishBeingPresented { get; set; }


        protected virtual void Awake()
        {
            _animal = GetComponentInParent<MAnimal>();
            LeftHand = _animal.Anim.GetBoneTransform(HumanBodyBones.LeftHand);

            _holderTransform = fishHolder.transform;
        }

        public virtual void HandleFishCatched(FishData fishData)
        {
            if (FishingGlobalSettings.SpawnFishOnWater)
            {
                return;
            }

            if (_fishPool.TryGetValue(fishData, out GameObject pooledFish))
            {
                _currentActiveFish = pooledFish;
                _currentActiveFish.SetActive(false);
            }
            else
            {
                GameObject newFish = Instantiate(fishData.FishPrefab, _holderTransform, false);
                _fishPool[fishData] = newFish;
                _currentActiveFish = newFish;
                _currentActiveFish.SetActive(false);
            }
        }

        //Called by Event
        public virtual void FishPresentation()
        {
            FishBeingPresented = true;
            OnFishPresented?.Invoke();
        }

        //Called by Animator on Fishing State End
        public virtual void HandleFishingEnd()
        {
            if (FishBeingPresented)
            {
                FishingEndPresentationEvent?.Invoke();
                DespawnFish();
            }
        }

        public virtual void DespawnFish()
        {
            if (FishingGlobalSettings.SpawnFishOnWater)
            {
                _fishJoint.transform.SetParent(_bobber.transform);
                _fishJoint.DespawnFish();
            }
            if (_currentActiveFish != null)
            {
                _currentActiveFish.SetActive(false);
            }
            FishBeingPresented = false;
            _fishCount.Value += 1;
        }

        public virtual void SpawnFish()
        {
            if (_currentActiveFish != null)
            {
                _currentActiveFish.SetActive(true);
            }
        }

        public virtual void ClearFishPool()
        {
            foreach (var fish in _fishPool.Values)
            {
                if (fish != null)
                {
                    DestroyImmediate(fish);
                }
            }
            _fishPool.Clear();
            _currentActiveFish = null;
        }

        protected virtual void OnDestroy()
        {
            ClearFishPool();
        }

        //Called by Event Listener
        public virtual void ReactToFishEndPresentationEvent()
        {
            if (_fishJoint != null)
            {
                _fishJoint.DisableFishCollider();

                if (_fishJoint.ActiveFishData != null && _fishJoint.ActiveFishData.lockFishBodyToHolderDuringStore)
                {
                    _fishJoint.LockFishBodyToHolder();
                }
            }
        }

        private FishingBobber _bobber;

        public virtual void SetBobberReference(FishingBobber bobber)
        {
            _bobber = bobber;
            _fishJoint = bobber.GetJoint();
        }

        /// <summary>
        /// Reparents the caught fish's joint onto the hand-tracking Fish Holder.
        /// Called by BobberController when the bobber retreat animation finishes.
        /// Guarded by HasActiveFish because the bobber also retreats empty after a catch.
        /// </summary>
        public virtual void AttachFishToHand()
        {
            if (!FishingGlobalSettings.SpawnFishOnWater || _fishJoint == null || !_fishJoint.HasActiveFish)
            {
                return;
            }

            _fishJoint.TransferParent(fishHolder.transform);
        }
    }
}