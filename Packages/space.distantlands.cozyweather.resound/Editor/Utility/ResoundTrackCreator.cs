using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    internal static class ResoundTrackCreator
    {
        const string MenuPath = "Assets/Create/Distant Lands/Cozy/Resound Tracks From Selected Audio Clips";

        [MenuItem(MenuPath, false, 2000)]
        static void CreateTracksFromSelectedAudioClips()
        {
            AudioClip[] clips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);
            List<UnityEngine.Object> createdTracks = new List<UnityEngine.Object>(clips.Length);

            foreach (AudioClip clip in clips)
            {
                ResoundTrack track = ScriptableObject.CreateInstance<ResoundTrack>();
                track.name = clip.name;

                SerializedObject serializedTrack = new SerializedObject(track);
                SerializedProperty clipProperty = serializedTrack.FindProperty("clip");
                if (clipProperty == null)
                {
                    UnityEngine.Object.DestroyImmediate(track);
                    throw new InvalidOperationException("ResoundTrack does not contain a serialized 'clip' field.");
                }

                clipProperty.objectReferenceValue = clip;
                serializedTrack.ApplyModifiedProperties();

                string destinationFolder = GetDestinationFolder(clip);
                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{destinationFolder}/{clip.name}.asset");
                AssetDatabase.CreateAsset(track, assetPath);
                createdTracks.Add(track);
            }

            AssetDatabase.SaveAssets();
            Selection.objects = createdTracks.ToArray();
        }

        [MenuItem(MenuPath, true)]
        static bool CanCreateTracksFromSelectedAudioClips()
        {
            return Selection.GetFiltered<AudioClip>(SelectionMode.Assets).Length > 0;
        }

        static string GetDestinationFolder(AudioClip clip)
        {
            string clipPath = AssetDatabase.GetAssetPath(clip);
            string clipFolder = Path.GetDirectoryName(clipPath)?.Replace('\\', '/');
            if (clipFolder == "Assets" || (clipFolder != null && clipFolder.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                return clipFolder;
            }

            return "Assets";
        }
    }
}
