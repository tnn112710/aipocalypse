// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace sc.screeneffects.runtime
{
    [ExecuteAlways]
    [AddComponentMenu("SC Screen Effects/Post Effect Debugger")]
    public class PostEffectDebugger : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)]
        private float debugSlider = 0f;

        private const string PANEL_NAME = "Custom Post Effects";

        private float DebugSlider
        {
            get => debugSlider;
            set
            {
                debugSlider = value;
                PostEffectDebug.DebugSlider = value;
            }
        }

        void OnEnable()
        {
            PostEffectDebug.DebugSlider = debugSlider;

            var widgetList = new List<DebugUI.Widget>();

            widgetList.AddRange(new DebugUI.Widget[]
            {
                new DebugUI.FloatField()
                {
                    displayName = "Debug Post Processing effects",
                    tooltip = "Split-view custom post-processing effects",
                    getter = () => DebugSlider,
                    min = () => 0f,
                    max = () => 1f,
                    setter = value => DebugSlider = value,
                },
            });

            var panel = DebugManager.instance.GetPanel(PANEL_NAME, createIfNull: true);

            panel.children.Add(widgetList.ToArray());
        }

        void OnValidate()
        {
            DebugSlider = debugSlider;
        }

        private void Update()
        {
            PostEffectDebug.DebugSlider = debugSlider;
        }

        void OnDisable()
        {
            DebugManager.instance.RemovePanel(PANEL_NAME);
            PostEffectDebug.DebugSlider = 0;
        }
    }
}