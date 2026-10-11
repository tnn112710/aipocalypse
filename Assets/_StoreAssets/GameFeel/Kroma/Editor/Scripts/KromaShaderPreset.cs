using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FayLabs.Kroma.Editor
{
    [System.Serializable]
    public class ShaderPropertyData
    {
        public string name;
        public enum Type { Color, Vector, Float, Texture }
        public Type type;
        
        // UI Metadata
        public bool isRange;
        public float rangeMin;
        public float rangeMax;
        public bool isHDR;
        
        public Color colorValue;
        public Vector4 vectorValue;
        public float floatValue;
        public Texture textureValue;
    }

    public class KromaShaderPreset : ScriptableObject
    {
        public string sectionName; // "Skybox Colors", "Clouds", "Sun / Moon", etc.
        public List<ShaderPropertyData> properties = new List<ShaderPropertyData>();

        public void ApplyToMaterial(Material material)
        {
            Undo.RecordObject(material, "Apply Kroma Preset");
            
            foreach (var prop in properties)
            {
                if (!material.HasProperty(prop.name)) continue;

                switch (prop.type)
                {
                    case ShaderPropertyData.Type.Color:
                        material.SetColor(prop.name, prop.colorValue);
                        break;
                    case ShaderPropertyData.Type.Vector:
                        material.SetVector(prop.name, prop.vectorValue);
                        break;
                    case ShaderPropertyData.Type.Float:
                        material.SetFloat(prop.name, prop.floatValue);
                        break;
                    case ShaderPropertyData.Type.Texture:
                        material.SetTexture(prop.name, prop.textureValue);
                        break;
                }
            }
        }

        public void SaveFromMaterial(Material material, string[] propertyNames)
        {
            properties.Clear();
            foreach (var name in propertyNames)
            {
                if (!material.HasProperty(name)) continue;

                var propData = new ShaderPropertyData { name = name };

                // Determine type (heuristic since ShaderUtil is limited at runtime, or check common prefixes)
                // Better approach: Check Material property type if possible, or try-catch.
                // However, Material doesn't easily expose type by name.
                // We will rely on manual definition or checks in the ShaderGUI when saving.
                // For simplicity here, we assume the caller knows the type or we check the current value.
                
                // Note: Material.Get* does not throw if type mismatch, usually returns default. 
                // BUT we need to know what to SAVE.
                // We'll rely on ShaderUtil in an Editor script context, or pass type info.
                // For now, let's implement a robust check using ShaderUtil.
                
                int index = material.shader.FindPropertyIndex(name);
                if (index == -1) continue;
                
                UnityEngine.Rendering.ShaderPropertyType type = material.shader.GetPropertyType(index);

                switch (type)
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        propData.type = ShaderPropertyData.Type.Color;
                        propData.colorValue = material.GetColor(name);
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        propData.type = ShaderPropertyData.Type.Vector;
                        propData.vectorValue = material.GetVector(name);
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        propData.type = ShaderPropertyData.Type.Float;
                        propData.floatValue = material.GetFloat(name);
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        propData.type = ShaderPropertyData.Type.Texture;
                        propData.textureValue = material.GetTexture(name);
                        break;
                }
                
                properties.Add(propData);
            }
        }
    }
}
