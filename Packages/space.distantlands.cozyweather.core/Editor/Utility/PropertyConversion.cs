//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;

namespace DistantLands.Cozy.EditorScripts
{
    /// <summary>
    /// Contains conversions from property definitions to custom COZY structs and back
    /// </summary>
    static class PropertyConversion
    {
        public static MeridiemTime GetMeridiemTime(SerializedProperty property)
        {
            return new MeridiemTime(property.FindPropertyRelative("hours").intValue,
                                    property.FindPropertyRelative("minutes").intValue,
                                    property.FindPropertyRelative("seconds").intValue,
                                    property.FindPropertyRelative("milliseconds").intValue,
                                    (Meridiem)property.FindPropertyRelative("meridiem").enumValueIndex);
        }
        public static void SetMeridiemTime(SerializedProperty property, MeridiemTime time)
        {
            property.FindPropertyRelative("hours").intValue = time.hours;
            property.FindPropertyRelative("minutes").intValue = time.minutes;
            property.FindPropertyRelative("seconds").intValue = time.seconds;
            property.FindPropertyRelative("milliseconds").intValue = time.milliseconds;
            property.FindPropertyRelative("meridiem").enumValueIndex = (int)time.meridiem;
        }
    }
}