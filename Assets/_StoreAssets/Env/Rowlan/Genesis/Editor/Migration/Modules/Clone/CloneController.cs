using JBooth.MicroVerseCore;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Migration
{
    public class CloneModuleController
    {
        private MigrationModule editor;

        private ContentCollection collection;
        private string outputPath = "";

        public CloneModuleController(MigrationModule editor)
        {
            this.editor = editor;
        }


        public void OnInspectorGUI()
        {

            EditorGUILayout.HelpBox("Clone a content collection including prefabs and thumbnails to a specified path", MessageType.None);

            collection = (ContentCollection) EditorGUILayout.ObjectField("Content Collection", collection, typeof(ContentCollection), false);
            outputPath = GUIUtils.AssetPathSelector(outputPath, "Output Path");

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Clone Collection"))
                {
                    CloneCollection();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CloneCollection()
        {
            if (!PerformConsistencyChecks())
                return;

            bool isContinue = EditorUtility.DisplayDialog("Clone Collection", "This will clone the selected collection and prefabs and thumbnails into the specified output folder!\n\nCreate a backup first!\n\nContinue?", "Yes", "No");

            if (!isContinue)
                return;

            // collection
            string source = AssetDatabase.GetAssetPath(collection);
            string target = Path.Combine(outputPath, Path.GetFileName(source));
            bool success = AssetDatabase.CopyAsset(source, target);

            ContentCollection clonedCollection = (ContentCollection) AssetDatabase.LoadAssetAtPath(target, typeof(ContentCollection));

            string sourceDirectory = Path.GetDirectoryName(source);

            // prefabs
            string sourcePath = Path.GetDirectoryName(source);
            
            for (int i = 0; i < collection.contents.Length; i++)
            {
                ContentData content = collection.contents[i];

                if (content.prefab == null)
                    continue;

                // prefab
                string prefabPath = AssetDatabase.GetAssetPath(content.prefab);

                string sourceDirectoryNormalized = PathUtils.Normalize(sourceDirectory);
                string prefabOutputPathNormalized = PathUtils.Normalize(prefabPath).Replace(sourceDirectoryNormalized, outputPath);

                Directory.CreateDirectory(Path.GetDirectoryName(prefabOutputPathNormalized));

                AssetDatabase.CopyAsset(prefabPath, prefabOutputPathNormalized);

                GameObject newPrefab = (GameObject)AssetDatabase.LoadAssetAtPath(prefabOutputPathNormalized, typeof(GameObject));

                content.prefab = newPrefab;

                // thumbnail
                string thumbnailPath = AssetDatabase.GetAssetPath(content.previewImage);

                if (thumbnailPath == null)
                    continue;

                sourceDirectoryNormalized = PathUtils.Normalize(sourceDirectory);
                string thumbnailOutputPathNormalized = PathUtils.Normalize(thumbnailPath).Replace(sourceDirectoryNormalized, outputPath);

                Directory.CreateDirectory(Path.GetDirectoryName(thumbnailOutputPathNormalized));

                AssetDatabase.CopyAsset(thumbnailPath, thumbnailOutputPathNormalized);

                Texture2D newThumbnail = (Texture2D)AssetDatabase.LoadAssetAtPath(thumbnailOutputPathNormalized, typeof(Texture2D));
                content.previewImage= newThumbnail;

                // collection
                clonedCollection.contents[i] = content;
            }

            EditorUtility.SetDirty(clonedCollection);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Cloned asset: {source} => {target}");

            // select clone collection
            Selection.activeObject = clonedCollection;
        }

        private bool PerformConsistencyChecks()
        {

            if (collection == null)
            {
                EditorUtility.DisplayDialog("Input Error", "Please provide a collection", "Ok");
                return false;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                EditorUtility.DisplayDialog("Input Error", "Please provide an output path", "Ok");
                return false;
            }

            string collectionPath = AssetDatabase.GetAssetPath(collection);
            string collectionPathNormalized = PathUtils.Normalize(collectionPath);
            string outputPathNormalized = PathUtils.Normalize(outputPath);

            bool isSubPath = collectionPathNormalized.StartsWith(outputPath);
            if (isSubPath)
            {
                EditorUtility.DisplayDialog("Input Error", $"Output path '{outputPathNormalized}' mustn't be a sub path of the collection path '{collectionPathNormalized}'", "Ok");
                return false;
            }

            return true;
        }
    }
}