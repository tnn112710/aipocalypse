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
using UnityEngine;
using UnityEngine.Rendering;

namespace sc.screeneffects.runtime
{
    public partial class PostProcessingComponent
    {
        /// <summary>
        /// Main effect parameter. Can be used to globally control the effect intensity.
        /// </summary>
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f, true);

        public enum TextureProjection
        {
            ScreenSpace,
            Spherical
        }
                
        [Serializable]
        public sealed class TextureProjectionParameter : VolumeParameter<TextureProjection> { }
        
        public enum BlendMode
        {
            Additive,
            Screen
        }
        
        [Serializable]
        public sealed class BlendModeParameter : VolumeParameter<BlendMode> { }
        
        public enum BlendModeFull
        {
            Linear,
            Additive,
            Multiply,
            Screen
        }
        
        [Serializable]
        public sealed class BlendModeFullParameter : VolumeParameter<BlendModeFull> { }

        public enum Resolution
        {
            Full = 1,
            Half = 2,
            Third = 3,
        }
        
        [Serializable]
        public sealed class ResolutionParameter : VolumeParameter<Resolution> { }
        
        public enum Channel
        {
            Red,
            Green,
            Blue,
            Alpha,
        }
        
        [Serializable]
        public sealed class ChannelParameter : VolumeParameter<Channel> { }

        [Serializable]
        public class TextureAnimation
        {
            public Vector2 direction = Vector2.up;
            public float speed = 1.0f;
            public float tiling = 1f;

            public TextureAnimation() {}
            
            public TextureAnimation(Vector2 direction, float speed, float tiling)
            {
                this.direction = direction;
                this.speed = speed;
                this.tiling = tiling;
            }
        }
        
        [Serializable]
        public class AnimationParameter : VolumeParameter<TextureAnimation>
        {
            private TextureAnimation animation = new TextureAnimation();

            /// <summary>
            /// The value that this parameter stores.
            /// </summary>
            /// <remarks>
            /// You can override this property to define custom behaviors when the value is changed.
            /// </remarks>
            public override TextureAnimation value
            {
                get => m_Value;
                set
                {
                    m_Value = value;
                }
            }
            
            public Vector2 direction => m_Value.direction;
            public float speed => m_Value.speed;
            public float tiling => m_Value.tiling;
            
            public AnimationParameter(TextureAnimation value, bool overrideState = false)
                : base(value, overrideState)
            {
                this.animation = value;
            }
            
            public override void Interp(TextureAnimation from, TextureAnimation to, float t)
            {
                m_Value.direction = Vector2.Lerp(from.direction, to.direction, t);
                m_Value.speed = Mathf.Lerp(from.speed, to.speed, t);
                m_Value.tiling = Mathf.Lerp(from.tiling, to.tiling, t);
            }
        }
    }
}