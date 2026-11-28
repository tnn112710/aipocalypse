using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // Local patches to vendor packs (ASSETS.md): a pack update overwrites them silently, these fail when it does.
    public sealed class VendorPatchTests
    {
        // PolyartStudio's NPCStateMachine (Assembly-CSharp, found by name) builds its states in OnValidate; prefabs without an
        // eat clip threw when a build loads every prefab, until NPCOneShotState got a null guard.
        [Test]
        public void PolyartAnimals_WithoutAnEatClip_BuildTheirStates()
        {
            Type machine = Type.GetType("Polyart.NPCStateMachine, Assembly-CSharp");
            Assert.IsNotNull(machine, "Polyart.NPCStateMachine");
            MethodInfo initialize = machine.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(initialize, "NPCStateMachine.Initialize");
            FieldInfo eat = machine.GetField("eatAnim");

            int checkedPrefabs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_StoreAssets/Env/PolyartStudio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Component found = asset.GetComponentInChildren(machine, true);
                if (found == null || eat.GetValue(found) as AnimationClip != null)
                    continue;
                // An isolated copy: Initialize adds components, which must never reach the vendor prefab.
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Assert.DoesNotThrow(() => initialize.Invoke(root.GetComponentInChildren(machine, true), null), path);
                    checkedPrefabs++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Assert.That(checkedPrefabs, Is.GreaterThan(0), "a PolyartStudio prefab without an eat clip");
        }
    }
}
