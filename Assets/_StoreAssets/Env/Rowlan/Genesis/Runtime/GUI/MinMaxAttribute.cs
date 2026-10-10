using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
	[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = true)]
	public class MinMaxAttribute : PropertyAttribute
	{
		public float min;
		public float max;
		public bool useGuiContentFix = false;

		/// <summary>
		/// Use maxDigits = 0 for integer
		/// </summary>
		public int maxDigits;

        public MinMaxAttribute(float min, float max, bool useGuiContentFix, int maxDigits = -1)
		{
			this.min = min;
			this.max = max;
			this.useGuiContentFix = useGuiContentFix;
			this.maxDigits = maxDigits;

        }
	}
}