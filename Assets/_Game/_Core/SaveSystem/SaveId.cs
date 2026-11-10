using UnityEngine;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // A stable id for an object placed in a scene (a fence segment, a crate), so a save can find it again. Filled with a new
    // GUID when added in a scene, and again when the object is duplicated; empty on prefab assets and on objects spawned at
    // runtime (those aren't saved through it).
    [DisallowMultipleComponent]
    public sealed class SaveId : MonoBehaviour
    {
        [SerializeField, Tooltip("Generated; don't edit")] string _id;

        public string Id => _id;
        public bool HasId => !string.IsNullOrEmpty(_id);

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null)
            {
                if (HasId)
                    SetId(null);
                return;
            }
            if (!gameObject.scene.IsValid())
                return;
            if (!HasId || IsDuplicate())
                SetId(System.Guid.NewGuid().ToString("N"));
        }

        // Another object in the scene with the same id: one of them was duplicated (Ctrl+D copies the field). Whichever is
        // validated first takes a new id.
        bool IsDuplicate()
        {
            foreach (SaveId other in FindObjectsByType<SaveId>(FindObjectsInactive.Include))
                if (other != this && other._id == _id && other.gameObject.scene == gameObject.scene)
                    return true;
            return false;
        }

        void SetId(string id)
        {
            _id = id;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Editor scripts give objects placed before this component existed their ids.
        public void Regenerate() => SetId(System.Guid.NewGuid().ToString("N"));
#endif
    }
}
