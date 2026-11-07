//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    public enum VarType
    {
        Float,
        Bool,
        Vector3,
        Color,
        Texture2D,
        Cubemap,
        Texture3D,
        Shadow,
        RenderTexture,
        Enum,
        Object
    }
    
    public class AtmospherePropertyAttribute : PropertyAttribute
    {
        public VarType varType;
        public string fieldTitle;
        public float minFloat;
        public float maxFloat;
        public Vector3 minVector;
        public Vector3 maxVector;
        public string[] enumNames;
        public AtmospherePropertyAttribute(VarType varType, string title)
        {
            this.varType = varType;
            this.fieldTitle = title;
        }
    }
}