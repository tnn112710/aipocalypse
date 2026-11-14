using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreWeaponModelParts : MonoBehaviour
    {
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform body;
        [SerializeField] private Transform magazine;
        [SerializeField] private Transform action;
        [SerializeField] private Transform slide;
        [SerializeField] private Transform defaultStock;
        [SerializeField] private Transform defaultGrip;
        [SerializeField] private Transform defaultScope;
        [SerializeField] private Transform defaultBarrel;
        [SerializeField] private Transform attachmentGeometry;

        public Transform ModelRoot => modelRoot;
        public Transform Body => body;
        public Transform Magazine => magazine;
        public Transform Action => action;
        public Transform Slide => slide;
        public Transform DefaultStock => defaultStock;
        public Transform DefaultGrip => defaultGrip;
        public Transform DefaultScope => defaultScope;
        public Transform DefaultBarrel => defaultBarrel;
        public Transform AttachmentGeometry => attachmentGeometry;
        public int RecognizedGroupCount => EnumerateGroups().Count(value => value != null);
        public bool HasMovableAction => action != null || slide != null;

        private void Awake()
        {
            if (modelRoot == null)
                modelRoot = transform.Find("Weapon") ?? transform;
            BindKnownGroups(false);
        }

        public void Configure(Transform sourceRoot, bool preserveIndividualPieces = true)
        {
            modelRoot = sourceRoot == null ? transform : sourceRoot;
            BindKnownGroups(preserveIndividualPieces);
        }

        public void Rebind(bool preserveIndividualPieces = true)
        {
            BindKnownGroups(preserveIndividualPieces);
        }

        private void BindKnownGroups(bool preserveIndividualPieces)
        {
            if (modelRoot == null)
                return;

            body = ResolveGroup("gunModel", preserveIndividualPieces);
            magazine = ResolveGroup("ammoModel", preserveIndividualPieces);
            action = ResolveGroup("pumpModel", preserveIndividualPieces);
            slide = ResolveGroup("slideModel", preserveIndividualPieces);
            defaultStock = ResolveGroup("defaultStockModel", preserveIndividualPieces);
            defaultGrip = ResolveGroup("defaultGripModel", preserveIndividualPieces);
            defaultScope = ResolveGroup("defaultScopeModel", preserveIndividualPieces);
            defaultBarrel = ResolveGroup("defaultBarrelModel", preserveIndividualPieces);
            attachmentGeometry = ResolveGroup("attachmentModel", preserveIndividualPieces);
        }

        private Transform ResolveGroup(string sourceName, bool createContainer)
        {
            var transforms = modelRoot.GetComponentsInChildren<Transform>(true);
            var exact = transforms.FirstOrDefault(value =>
                value != modelRoot
                && string.Equals(value.name, sourceName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            var prefix = sourceName + "_";
            var pieces = transforms
                .Where(value => value != modelRoot
                    && value.parent != null
                    && value.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (pieces.Length == 0)
                return null;
            if (pieces.Length == 1 || !createContainer)
                return pieces[0];

            var container = new GameObject(sourceName).transform;
            container.SetParent(modelRoot, false);
            for (var index = 0; index < pieces.Length; index++)
                pieces[index].SetParent(container, true);
            return container;
        }

        private IEnumerable<Transform> EnumerateGroups()
        {
            yield return body;
            yield return magazine;
            yield return action;
            yield return slide;
            yield return defaultStock;
            yield return defaultGrip;
            yield return defaultScope;
            yield return defaultBarrel;
            yield return attachmentGeometry;
        }
    }
}
