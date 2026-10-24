using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SunfishCreations.Common
{
public class LayerSetter : MonoBehaviour
{
    public string _targetLayer = "Default";
    public bool _includeChildren = true;
    public bool applyOnEnable = true;

    private void OnEnable()
    {
        if (applyOnEnable)
        {
            ApplyLayer();
        }
    }

    private void Reset()
    {
        _targetLayer = "Default";
        _includeChildren = false;
    }

    public void ApplyLayer()
    {
        int layerIndex = LayerMask.NameToLayer(_targetLayer);
        if (layerIndex == -1)
        {
            Debug.LogError($"Layer '{_targetLayer}' does not exist.");
            return;
        }

        SetLayerRecursively(gameObject, layerIndex);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        if (_includeChildren)
        {
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LayerSetter))]
public class LayerSetterEditor : UnityEditor.Editor
{
    private string[] _layerNames;

    private void OnEnable()
    {
        // Cache layer names (0 to 31)
        _layerNames = new string[32];
        for (int i = 0; i < 32; i++)
        {
            string name = LayerMask.LayerToName(i);
            _layerNames[i] = string.IsNullOrEmpty(name) ? $"Layer {i}" : name;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var layerProp = serializedObject.FindProperty("_targetLayer");
        var includeChildrenProp = serializedObject.FindProperty("_includeChildren");

        EditorGUILayout.LabelField("Layer Settings", EditorStyles.boldLabel);

        // Dropdown for layer selection
        int currentLayerIndex = LayerMask.NameToLayer(layerProp.stringValue);
        if (currentLayerIndex < 0) currentLayerIndex = 0;

        int newLayerIndex = EditorGUILayout.Popup("Target Layer", currentLayerIndex, _layerNames);
        if (newLayerIndex != currentLayerIndex)
        {
            layerProp.stringValue = _layerNames[newLayerIndex];
        }

        EditorGUILayout.PropertyField(includeChildrenProp);

        serializedObject.ApplyModifiedProperties();

        // Apply button
        if (GUILayout.Button("Apply Layer"))
        {
            (target as LayerSetter)?.ApplyLayer();
        }
    }
}
#endif
}