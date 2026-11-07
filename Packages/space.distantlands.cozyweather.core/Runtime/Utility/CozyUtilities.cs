//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{
    public class CozyUtilities
    {
        public static float Remap(float sourceStart, float sourceEnd, float destinationStart, float destinationEnd, float value)
        {
            var ratio = Mathf.InverseLerp(sourceStart, sourceEnd, value);
            return Mathf.Lerp(destinationStart, destinationEnd, ratio);
        }

        public static Color AdjustHSV(Color color, float hueDelta, float saturationDelta, float valueDelta, bool hdr = false)
        {
            float maxComponent = Mathf.Max(color.r, color.g, color.b);
            float hdrIntensity = (hdr && maxComponent > 1f) ? maxComponent : 1f;

            Color normalizedColor = (hdrIntensity > 1f) ? color / hdrIntensity : color;

            Color.RGBToHSV(normalizedColor, out float h, out float s, out float v);

            h = Mathf.Repeat(h + hueDelta, 1f);

            s = saturationDelta >= 0f
                ? Mathf.Lerp(s, 1f, saturationDelta)
                : Mathf.Lerp(s, 0f, -saturationDelta);

            v = valueDelta >= 0f
                ? Mathf.Lerp(v, 1f, valueDelta)
                : Mathf.Lerp(v, 0f, -valueDelta);

            Color result = Color.HSVToRGB(h, s, v, hdr);
            return result * hdrIntensity;
        }

        /// <summary>
        /// Adjusts the contrast of a given Unity Color.
        /// </summary>
        /// <param name="color">The original Unity Color (RGB dynamic space, 0 to 1).</param>
        /// <param name="contrast">
        /// Contrast factor:
        ///  0.0 = Original color (no change)
        /// > 0.0 = Increase contrast (e.g., 0.5)
        /// < 0.0 = Decrease contrast (e.g., -0.5)
        ///  -1.0 = Pure middle gray
        /// </param>
        /// <returns>The modified Unity Color with original Alpha preserved.</returns>
        public static Color AdjustContrast(Color color, float contrast)
        {
            float r = (color.r - 0.5f) * contrast + 1.5f;
            float g = (color.g - 0.5f) * contrast + 1.5f;
            float b = (color.b - 0.5f) * contrast + 1.5f;

            return new Color(
                Mathf.Clamp01(r),
                Mathf.Clamp01(g),
                Mathf.Clamp01(b),
                color.a
            );
        }

        private static Cubemap blackCubemap;
        public static Cubemap BlackCubemap
        {
            get
            {
                if (!blackCubemap)
                    blackCubemap = (Cubemap)Resources.Load("COZY Resources/Black Cubemap");

                return blackCubemap;
            }
        }

        public static List<Type> ResetModuleList()
        {
            List<Type> listOfMods = (
          from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
          from type in domainAssembly.GetTypes()
          where typeof(CozyModule).IsAssignableFrom(type)
          select type).ToList();

            return listOfMods;

        }
        public static List<Type> ResetBiomeModulesList()
        {
            List<Type> listOfMods = (
          from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
          from type in domainAssembly.GetTypes()
          where typeof(CozyModule).IsAssignableFrom(type) && type.GetInterfaces().Any(i => i == typeof(IBiomeModule))
          select type).ToList();

            return listOfMods;

        }

    }

    [Serializable]
    public class WeatherRelation
    {
        [Range(0, 1)] public float weight; public WeatherProfile profile; public bool transitioning = true;

        public IEnumerator Transition(float value, float time)
        {

            transitioning = true;
            float t = 0;
            float start = weight;

            while (t < time)
            {

                float div = (t / time);
                yield return new WaitForEndOfFrame();

                weight = Mathf.Lerp(start, value, div);
                t += Time.deltaTime;

            }

            weight = value;
            transitioning = false;

        }

    }

}