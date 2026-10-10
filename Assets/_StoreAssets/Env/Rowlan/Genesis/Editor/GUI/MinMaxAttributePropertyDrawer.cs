using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis
{
	/// <summary>
	/// Property drawer for Min/Max values which shows [min-float] <--- min-max-slider ---> [max-float]
	/// 
	/// Usage example:
	/// 
	///         [MinMax(0, 360)]
	///         public Vector2 randomX;
	///         
	/// </summary>
	[CustomPropertyDrawer(typeof(MinMaxAttribute))]
	public class MinMaxAttributePropertyDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			// get the attribute
			MinMaxAttribute range = attribute as MinMaxAttribute;

			if (property.propertyType == SerializedPropertyType.Vector2)
			{
				/// 
				/// minmax slider
				/// 

				float minValue = property.vector2Value.x;
				float maxValue = property.vector2Value.y;
				float minLimit = range.min;
				float maxLimit = range.max;

				EditorGUI.BeginChangeCheck();
				{
					/// 
					/// label
					/// 

					Rect labelRect = position;
					labelRect.width = EditorGUIUtility.labelWidth;

					// example for directly aligned
					/*
					Vector2 labelSize = EditorStyles.label.CalcSize(label);
					labelRect.width = labelSize.x;
					*/
					EditorGUI.PrefixLabel(labelRect, label);

					/// 
					/// value
					/// 

					// width of the float fields
					float limitFieldWidth = 55f;

					Rect fieldRect = position;
					fieldRect.x += labelRect.width;
					fieldRect.width = position.width - labelRect.width;

					Rect minLimitFieldRect = fieldRect;
					minLimitFieldRect.width = limitFieldWidth;

					Rect maxLimitFieldRect = fieldRect;
					maxLimitFieldRect.x = fieldRect.max.x - limitFieldWidth;
					maxLimitFieldRect.width = limitFieldWidth;

					fieldRect.x += minLimitFieldRect.width;
					fieldRect.width -= minLimitFieldRect.width;
					fieldRect.width -= maxLimitFieldRect.width;

					// fix for GUIContent.None usage: for some reason there's a space at every component
					// the observation is that the space differs depending on GUIContent.None, but could be something else entirely
					// should fix this at some point
					// the fix seems to be depending on whether we're in a property drawer or now; and in that way most probably because of the provided width calculation
					bool guiContentNoneFix = range.useGuiContentFix; 
					if (guiContentNoneFix)
					{
						float offsetX = 14;
						minLimitFieldRect.x -= offsetX;
						fieldRect.x -= offsetX * 2;
						fieldRect.width += offsetX * 3;
					}

					// add margin
					float margin = 5f;
					fieldRect.x += margin;
					fieldRect.width -= margin * 2f;

					// limit digits
					if(range.maxDigits >= 0)
					{
						minValue = System.MathF.Round(minValue, range.maxDigits);
                        maxValue = System.MathF.Round(maxValue, range.maxDigits);
                    }

                    minValue = EditorGUI.FloatField(minLimitFieldRect, GUIContent.none, minValue);
					minValue = Mathf.Clamp(minValue, range.min, range.max);

					EditorGUI.MinMaxSlider(fieldRect, GUIContent.none, ref minValue, ref maxValue, minLimit, maxLimit);

					maxValue = EditorGUI.FloatField(maxLimitFieldRect, GUIContent.none, maxValue);
					maxValue = Mathf.Clamp(maxValue, range.min, range.max);
					maxValue = maxValue < minValue ? minValue : maxValue;

				}
				if (EditorGUI.EndChangeCheck())
				{
					property.vector2Value = new Vector2(minValue, maxValue);
				}
			}

		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			// MinMaxAttribute minMax = attribute as MinMaxAttribute;

			float height = EditorGUIUtility.singleLineHeight;

			return height;
		}
	}
}