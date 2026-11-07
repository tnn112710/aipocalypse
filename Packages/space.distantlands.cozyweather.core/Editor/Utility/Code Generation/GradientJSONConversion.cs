//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    public static class GradientJSONConverter
    {
        [Serializable]
        private class GradientWrapperRoot
        {
            public GradientData gradient;
        }

        [Serializable]
        private class GradientData
        {
            public int m_NumColorKeys;
            public int m_NumAlphaKeys;

            public SerializableColor key0, key1, key2, key3, key4, key5, key6, key7;
            public int ctime0, ctime1, ctime2, ctime3, ctime4, ctime5, ctime6, ctime7;
            public int atime0, atime1, atime2, atime3, atime4, atime5, atime6, atime7;
        }

        [Serializable]
        private struct SerializableColor
        {
            public float r, g, b, a;
            public Color ToColor() => new Color(r, g, b, a);
        }

        public static Gradient Parse(string json)
        {
            if (json.StartsWith("UnityEditor.GradientWrapperJSON:"))
                json = json.Replace("UnityEditor.GradientWrapperJSON:", "");

            var wrapper = JsonUtility.FromJson<GradientWrapperRoot>(json);
            var gradData = wrapper.gradient;

            var colorKeys = ExtractColorKeys(gradData);
            var alphaKeys = ExtractAlphaKeys(gradData);

            Gradient gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        private static GradientColorKey[] ExtractColorKeys(GradientData gradData)
        {
            var fields = typeof(GradientData).GetFields(BindingFlags.Public | BindingFlags.Instance);

            var colorFields = fields
                .Where(f => f.Name.StartsWith("key") && f.FieldType == typeof(SerializableColor))
                .Take(gradData.m_NumColorKeys)
                .ToList();

            var timeFields = fields
                .Where(f => f.Name.StartsWith("ctime"))
                .Take(gradData.m_NumColorKeys)
                .ToList();

            GradientColorKey[] keys = new GradientColorKey[gradData.m_NumColorKeys];

            for (int i = 0; i < keys.Length; i++)
            {
                var color = (SerializableColor)colorFields[i].GetValue(gradData);
                float time = (int)timeFields[i].GetValue(gradData) / 65535f;
                keys[i] = new GradientColorKey(color.ToColor(), time);
            }

            return keys;
        }

        private static GradientAlphaKey[] ExtractAlphaKeys(GradientData gradData)
        {
            var fields = typeof(GradientData).GetFields(BindingFlags.Public | BindingFlags.Instance);

            var colorFields = fields
                .Where(f => f.Name.StartsWith("key") && f.FieldType == typeof(SerializableColor))
                .Take(gradData.m_NumAlphaKeys)
                .ToList();

            var timeFields = fields
                .Where(f => f.Name.StartsWith("atime"))
                .Take(gradData.m_NumAlphaKeys)
                .ToList();

            GradientAlphaKey[] keys = new GradientAlphaKey[gradData.m_NumAlphaKeys];

            for (int i = 0; i < keys.Length; i++)
            {
                var color = (SerializableColor)colorFields[i].GetValue(gradData);
                float time = (int)timeFields[i].GetValue(gradData) / 65535f;
                keys[i] = new GradientAlphaKey(color.a, time);
            }

            return keys;
        }
    }
}