using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis.Integrations
{
    /// <summary>
    /// Sample discrete points or interpolate along the curve
    /// Sampling is used in the Monobehaviour, so it's a dedicated class
    /// </summary>
    public enum Sampling
    {
        [Tooltip("Discrete spline points")]
        Discrete,

        [Tooltip("Interpolate along the total spline curve")]
        Interpolate,
    }
}