using JBooth.MicroVerseCore;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Converter
{
	public class TerrainController
	{
		private static GUIContent terrainContent = EditorGUIUtility.TrTextContent("Terrain", "The terrain to get the detail prototypes from");
		private static GUIContent parentContent = EditorGUIUtility.TrTextContent("Parent", "The parent gameobject in which the microverse stamps will be created");

		private ConverterModule editor;

		private PlayerSettings setup;

		private Terrain terrain;
		private GameObject parent;

		public TerrainController(ConverterModule editor)
		{
			this.editor = editor;
		}


		public void OnInspectorGUI()
		{
#if __MICROVERSE_VEGETATION__
			EditorGUILayout.HelpBox("Convert terrain tree and detail prototypes to MicroVerse stamps", MessageType.None);

			terrain = EditorGUILayout.ObjectField(terrainContent, terrain, typeof(Terrain), true) as Terrain;
			parent = EditorGUILayout.ObjectField(parentContent, parent, typeof(GameObject), true) as GameObject;

			EditorGUILayout.BeginHorizontal();
			{
				if (GUILayout.Button("Convert Trees"))
				{
					ConvertToMicroVerseTrees();
				}

				if (GUILayout.Button("Convert Details"))
				{
					ConvertToMicroVerseDetails();
				}
			}
			EditorGUILayout.EndHorizontal();
#endif
		}
#if __MICROVERSE_VEGETATION__
		private void ConvertToMicroVerseTrees()
		{
			if (!PerformConsistencyChecks())
				return;

			GameObject stampGO = new GameObject("Tree Stamp");

			// create stamp
			TreeStamp stamp = stampGO.AddComponent<TreeStamp>();

			TreePrototype[] prototypes = terrain.terrainData.treePrototypes;

			foreach (TreePrototype prototype in prototypes)
			{
				TreePrototypeSerializable mvPrototype = new TreePrototypeSerializable(prototype);

				stamp.prototypes.Add(mvPrototype);
			}

			// reparent
			GameObjectUtility.SetParentAndAlign(stampGO, parent);

			// modify gameobject name for uniqueness
			GameObjectUtility.EnsureUniqueNameForSibling(stampGO);

			EditorUtility.DisplayDialog("Message", "Trees converted", "Ok");

		}

		private void ConvertToMicroVerseDetails()
		{
			if (!PerformConsistencyChecks())
				return;

			DetailPrototype[] prototypes = terrain.terrainData.detailPrototypes;

			foreach (DetailPrototype prototype in prototypes)
			{
				GameObject stampGO = new GameObject("Detail Stamp");

				// create stamp
				DetailStamp stamp = stampGO.AddComponent<DetailStamp>();

				stamp.prototype = new DetailPrototypeSerializable(prototype);

				// reparent
				GameObjectUtility.SetParentAndAlign(stampGO, parent);

				// modify gameobject name for uniqueness
				GameObjectUtility.EnsureUniqueNameForSibling(stampGO);
			}

			EditorUtility.DisplayDialog("Message", "Details converted", "Ok");

		}

		private bool PerformConsistencyChecks()
		{

			if (terrain == null)
			{
				EditorUtility.DisplayDialog("Input Error", "Please provide a terrain", "Ok");
				return false;
			}

			if (parent == null)
			{
				EditorUtility.DisplayDialog("Input Error", "Please provide a gameobject to use as parent", "Ok");
				return false;
			}

			return true;
		}
#endif

	}
}