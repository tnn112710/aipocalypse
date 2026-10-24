using UnityEngine;

namespace SunfishCreations.Common
{
    /// <summary>
    /// Hides a serialized field in the Inspector when the referenced bool field equals hideWhenEquals.
    /// </summary>
    public class HideIfAttribute : PropertyAttribute
    {
        public readonly string ConditionFieldName;
        public readonly bool HideWhenEquals;

        public HideIfAttribute(string conditionFieldName, bool hideWhenEquals = true)
        {
            ConditionFieldName = conditionFieldName;
            HideWhenEquals = hideWhenEquals;
        }
    }
}
