using UnityEditor;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FChannelTweakWindow : FTextureProcessWindow
    {
        public enum EChannelEdit { RGB, R, G, B, A }

        [SerializeField] private EChannelEdit ChannelEdit = EChannelEdit.RGB;
        [SerializeField] private float ValueMultiplier = 1f;
        [SerializeField] private bool Invert;
        public AnimationCurve ValueCurve = AnimationCurve.Linear( 0f, 0f, 1f, 1f );

        private Texture2D sourceChannelPreview;
        private Texture2D resultChannelPreview;

        public static void Init()
        {
            FChannelTweakWindow window = GetWindow<FChannelTweakWindow>();
            window.titleContent = new GUIContent( "Channel Tweak", FTextureToolsGUIUtilities.FindIcon( "SPR_ColorReplace" ), "Tweak selected channels of target texture" );
            window.previewScale = FEPreview.m_1x1;
            window.drawPreviewScale = true;
            window.previewSize = 128;
            window.position = new Rect( 140, 50, 454, 662 );
            window.somethingChanged = true;
            window.Show();
            called = true;
        }

        protected override void OnGUICustom()
        {
            GUILayout.Space( 4 );
            EditorGUI.BeginChangeCheck();
            ChannelEdit = (EChannelEdit)EditorGUILayout.EnumPopup( "Channel Edit", ChannelEdit );
            ValueCurve = EditorGUILayout.CurveField( "Curve", ValueCurve, Color.white, new Rect( 0, 0, 1, 1 ) );
            ValueMultiplier = EditorGUILayout.FloatField( "Value Multiplier", ValueMultiplier );
            Invert = EditorGUILayout.Toggle( "Invert", Invert );
            if( EditorGUI.EndChangeCheck() ) Repaint();
            GUILayout.Space( 8 );
        }

        private byte TweakValue( byte value )
        {
            float result = Mathf.Clamp01( ValueCurve.Evaluate( value / 255f ) * ValueMultiplier );
            if( Invert ) result = 1f - result;
            return (byte)Mathf.RoundToInt( result * 255f );
        }

        protected override void ProcessTexture( Texture2D source, Texture2D target, bool preview = true )
        {
            if( source.width != target.width || source.height != target.height )
            {
                Debug.LogError( "[CHANNEL TWEAK] Source and target texture dimensions must match." );
                return;
            }

            try
            {
                if( !preview ) EditorUtility.DisplayProgressBar( "Tweaking Texture Channels...", "Processing...", 0.5f );

                Color32[] sourcePixels = source.GetPixels32();
                Color32[] resultPixels = new Color32[sourcePixels.Length];
                for( int i = 0; i < sourcePixels.Length; i++ )
                {
                    Color32 pixel = sourcePixels[i];
                    switch( ChannelEdit )
                    {
                        case EChannelEdit.RGB:
                            pixel.r = TweakValue( pixel.r );
                            pixel.g = TweakValue( pixel.g );
                            pixel.b = TweakValue( pixel.b );
                            break;
                        case EChannelEdit.R: pixel.r = TweakValue( pixel.r ); break;
                        case EChannelEdit.G: pixel.g = TweakValue( pixel.g ); break;
                        case EChannelEdit.B: pixel.b = TweakValue( pixel.b ); break;
                        case EChannelEdit.A: pixel.a = TweakValue( pixel.a ); break;
                    }

                    resultPixels[i] = pixel;
                }

                target.SetPixels32( resultPixels );
                target.Apply( false, false );

                // Display copies isolate channel visualization from the pixels written to the asset.
                if( preview )
                {
                    UpdateChannelPreview( ref sourceChannelPreview, sourcePixels, source.width, source.height );
                    UpdateChannelPreview( ref resultChannelPreview, resultPixels, target.width, target.height );
                    previewTitle = "Preview (" + ChannelEdit + ")";
                }
            }
            finally
            {
                if( !preview ) EditorUtility.ClearProgressBar();
            }
        }

        private void UpdateChannelPreview( ref Texture2D texture, Color32[] pixels, int width, int height )
        {
            if( texture == null || texture.width != width || texture.height != height )
            {
                if( texture != null ) DestroyImmediate( texture );
                texture = new Texture2D( width, height, TextureFormat.ARGB32, false, false );
                texture.hideFlags = HideFlags.HideAndDontSave;
            }

            Color32[] displayPixels = new Color32[pixels.Length];
            for( int i = 0; i < pixels.Length; i++ )
            {
                Color32 pixel = pixels[i];
                if( ChannelEdit != EChannelEdit.RGB )
                {
                    byte value = pixel.r;
                    switch( ChannelEdit )
                    {
                        case EChannelEdit.G: value = pixel.g; break;
                        case EChannelEdit.B: value = pixel.b; break;
                        case EChannelEdit.A: value = pixel.a; break;
                    }
                    pixel = new Color32( value, value, value, 255 );
                }

                pixel.a = 255;
                displayPixels[i] = pixel;
            }

            texture.SetPixels32( displayPixels );
            texture.Apply( false, false );
        }

        protected override void DrawPreviewTexture( Rect position, Texture2D texture, bool isSource )
        {
            Texture2D display = isSource ? sourceChannelPreview : resultChannelPreview;
            base.DrawPreviewTexture( position, display != null ? display : texture, isSource );
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if( sourceChannelPreview != null ) DestroyImmediate( sourceChannelPreview );
            if( resultChannelPreview != null ) DestroyImmediate( resultChannelPreview );
            sourceChannelPreview = null;
            resultChannelPreview = null;
            somethingChanged = true;
        }
    }
}
