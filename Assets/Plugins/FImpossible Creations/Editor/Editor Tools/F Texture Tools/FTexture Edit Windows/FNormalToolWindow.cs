using FIMSpace.FEditor;
using System;
using UnityEditor;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FNormalToolWindow : FTextureProcessWindow
    {

        enum ENormalMode
        {
            BasicPictureToNormal,
            OpenGLDirectXSwapConversion,
            InvertHeight,
            BlurNormalMap,
        }


        ENormalMode Mode = ENormalMode.BasicPictureToNormal;


        private float Blend = 1f;
        private float Boost = 0f;
        private float Range = 0f;
        private float RangeSens = 1.2f;
        private float Smoothing = 0f;
        private bool BlurAfterNormalizing = false;
        public AnimationCurve BrightnessIntensity = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        public static void Init()
        {
            FNormalToolWindow window = (FNormalToolWindow)GetWindow( typeof( FNormalToolWindow ) );
            window.titleContent = new GUIContent( "Normal Tool", FTextureToolsGUIUtilities.FindIcon( "SPR_NormalGen" ), "Normal Maps related operations" );
            window.previewScale = FEPreview.m_1x1;
            window.drawPreviewScale = true;

            window.previewSize = 82;
            window.position = new Rect( 340, 50, 520, 564 );
            window.Show();
            called = true;
        }


        protected override void OnGUICustom()
        {
            EditorGUI.BeginChangeCheck();

            Mode = (ENormalMode)EditorGUILayout.EnumPopup( "Mode:", Mode );
            GUILayout.Space( 8 );

            if( Mode == ENormalMode.BasicPictureToNormal )
            {
                GUI.backgroundColor = new Color( 0.6f, 1f, 0.7f );

                Blend = EditorGUILayout.Slider( new GUIContent( "Blend" ), Blend, -1f, 1f );
                Boost = EditorGUILayout.Slider( new GUIContent( "Boost" ), Boost, 0f, 20f );
                GUI.backgroundColor = Color.white;
                BrightnessIntensity = EditorGUILayout.CurveField(new GUIContent("Brightness Power", "Normal map power intensity control basing on the pixel brightness. X = brightness -> 0 is black 1 is white, Y = normal power from -1 to 1"), BrightnessIntensity, Color.cyan, new Rect(0f, -1f, 1f, 1f - (-1f)));
                GUILayout.Space( 8 );

                EditorGUILayout.BeginHorizontal();
                Range = EditorGUILayout.Slider( new GUIContent( "Range" ), Range, 0f, 1f );
                if( Range <= 0f ) GUI.enabled = false;
                EditorGUILayout.LabelField( "Boost:", GUILayout.Width( 58 ) );
                RangeSens = GUILayout.HorizontalSlider( RangeSens, 1f, 2f, GUILayout.MaxWidth( 120 ) );
                GUI.enabled = true;
                GUILayout.Space( 8 );
                EditorGUILayout.EndHorizontal();
                GUILayout.Space( 2 );
                Smoothing = EditorGUILayout.Slider( new GUIContent( "Smoothing" ), Smoothing, 0f, 1f );
                BlurAfterNormalizing = EditorGUILayout.Toggle( new GUIContent( "Post Blur:", "Blur after or before normalizing, giving different results" ), BlurAfterNormalizing );

            }
            else if( Mode == ENormalMode.OpenGLDirectXSwapConversion )
            {
                EditorGUILayout.HelpBox( "If you use this on OpenGL normal map, it will be converted to DirectX coordinates (left-handed)\nIf you use this on DirectX normal map, it will be converted to OpenGL coordinates (right-handed)", MessageType.Info );
            }
            else if( Mode == ENormalMode.InvertHeight )
            {
                EditorGUILayout.HelpBox( "Just inverting normal map height direction", MessageType.Info );
            }
            else if( Mode == ENormalMode.BlurNormalMap )
            {
                EditorGUILayout.HelpBox( "Blurring base image and then applying normalization", MessageType.Info );
                Blend = EditorGUILayout.Slider( new GUIContent( "Blend" ), Blend, 0f, 1f );
                Smoothing = EditorGUILayout.Slider( new GUIContent( "Smoothing" ), Smoothing, 0f, 1f );
            }

            if( EditorGUI.EndChangeCheck() )
                somethingChanged = true;
            else
                somethingChanged = false;
        }


        protected override void ProcessTexture( Texture2D source, Texture2D target, bool preview = true )
        {
            if( !preview ) EditorUtility.DisplayProgressBar( "Normalizing Texture...", "Preparing... ", 2f / 5f );

            if( Mode == ENormalMode.BasicPictureToNormal )
            {
                Color[] sourcePixels = source.GetPixels();
                Color32[] newPix32 = new Color32[sourcePixels.Length];
                Color[] newPixels;

                if( BlurAfterNormalizing == false )
                {
                    if( Smoothing > 0 )
                    {
                        newPix32 = FTextureEditorToolsMethods.ApplyGaussianBlur( source.GetPixels32(), source.width, source.height, Smoothing, true );
                        for( int i = 0; i < newPix32.Length; i++ ) sourcePixels[i] = (Color)newPix32[i];
                    }
                }

                int smoothingPixels = GetNormalRange( source.width, source.height );

                if( Range == 0 )
                {
                    newPixels = GenerateNormalMap( sourcePixels, source.width, source.height );
                }
                else
                {
                    Vector2 gradientScale = Vector2.one;
                    Texture2D reference = preview ? GetFirstTexture : null;
                    if( reference != null )
                    {
                        int referenceRange = GetNormalRange( reference.width, reference.height );
                        // Match the final kernel's response to a slope, including rounded preview radii.
                        float responseScale = GetRangeResponse( referenceRange, RangeSens ) / GetRangeResponse( smoothingPixels, RangeSens );
                        gradientScale.x = responseScale * source.width / reference.width;
                        gradientScale.y = responseScale * source.height / reference.height;
                    }

                    newPixels = GenerateNormalMap( sourcePixels, source.width, source.height, smoothingPixels, RangeSens, gradientScale );
                }

                // Finalizing changes
                if( !preview ) EditorUtility.DisplayProgressBar( "Normalizing Texture...", "Applying Normalizing to Texture... ", 3.85f / 5f );

                for( int i = 0; i < newPixels.Length; i++ ) newPix32[i] = (Color32)newPixels[i];

                if( BlurAfterNormalizing )
                {
                    if( Smoothing > 0 ) newPix32 = FTextureEditorToolsMethods.ApplyGaussianBlur( newPix32, source.width, source.height, Smoothing, true );
                }

                target.SetPixels32( newPix32 );
            }
            else if( Mode == ENormalMode.OpenGLDirectXSwapConversion )
            {
                Color[] sourcePixels = source.GetPixels();
                Color32[] newPix32 = ConvertDirectXToOpenGL( sourcePixels, source.width, source.height );
                target.SetPixels32( newPix32 );
            }
            else if( Mode == ENormalMode.InvertHeight )
            {
                Color[] sourcePixels = source.GetPixels();
                Color32[] newPix32 = InvertNormal( sourcePixels, source.width, source.height );
                target.SetPixels32( newPix32 );
            }
            else if( Mode == ENormalMode.BlurNormalMap )
            {
                Color32[] sourcePixels = source.GetPixels32();
                Color32[] newPix32 = Smoothing == 0 ? sourcePixels : FTextureEditorToolsMethods.ApplyGaussianBlur( sourcePixels, source.width, source.height, Smoothing, true );
                newPix32 = ReGenerateNormalMap( sourcePixels, newPix32, source.width, source.height, Blend );
                target.SetPixels32( newPix32 );
            }

            target.Apply( false, false );

            if( !preview ) EditorUtility.ClearProgressBar();
        }


        protected override void OnAfterProcessingImage( TextureImporter importer )
        {
            importer.textureType = TextureImporterType.NormalMap;
        }

        public Color[] GenerateNormalMap( Color[] imagePixels, int width, int height )
        {
            Color[] normalMapPixels = new Color[imagePixels.Length];
            float blend = Blend * ( 1f + Boost );
            Vector2Int dim = new Vector2Int(width, height);

            for ( int y = 0; y < height; y++ )
            {
                for( int x = 0; x < width; x++ )
                {
                    // Get surrounding pixel intensities
                    float left = GetIntensity( imagePixels, width, height, x - 1, y ) * blend;
                    float right = GetIntensity( imagePixels, width, height, x + 1, y ) * blend;
                    float top = GetIntensity( imagePixels, width, height, x, y - 1 ) * blend;
                    float bottom = GetIntensity( imagePixels, width, height, x, y + 1 ) * blend;

                    // Calculate direction gradients
                    float dx = right - left;
                    float dy = bottom - top;

                    Vector3 normal = NormalIntensityCalculate(imagePixels, dim, x, y, dx, dy);
                    //Vector3 normal = new Vector3( -dx, -dy, 1.0f ).normalized;

                    Color normalColor = new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f
                    );

                    normalMapPixels[y * width + x] = normalColor;
                }
            }

            return normalMapPixels;
        }

        public Color32[] ReGenerateNormalMap( Color32[] prePixels, Color32[] imagePixels, int width, int height, float blend = 1f )
        {
            Color32[] normalMapPixels = new Color32[imagePixels.Length];
            Vector2Int dim = new Vector2Int(width, height);

            for ( int y = 0; y < height; y++ )
            {
                for( int x = 0; x < width; x++ )
                {
                    int pix = GetPX( x, y, new Vector2( width, height ) );

                    float preX = NormalToIntensity32( prePixels[pix].r ); 
                    float preY = NormalToIntensity32( prePixels[pix].g );

                    float dx = Mathf.Lerp( preX, NormalToIntensity32( imagePixels[pix].r ), blend );
                    float dy = Mathf.Lerp( preY, NormalToIntensity32( imagePixels[pix].g ), blend );

                    Vector3 normal = NormalIntensityCalculate32(imagePixels, dim, x, y, dx, dy);
                    //Vector3 normal = new Vector3( dx, dy, 1.0f ).normalized;

                    normalMapPixels[y * width + x] = new Color32(
                        (byte)( ( normal.x * 0.5f + 0.5f ) * 255.0f ),
                        (byte)( ( normal.y * 0.5f + 0.5f ) * 255.0f ),
                        (byte)( ( normal.z * 0.5f + 0.5f ) * 255.0f ),
                        255
                    );
                }
            }

            return normalMapPixels;
        }

        private float ReGetIntensity( Color32[] pixels, int width, int height, int x, int y )
        {
            Color32 pixel = pixels[GetPXLoop( x, y, new Vector2( width, height ) )];
            float r = NormalToIntensity32( pixel.r );
            float g = NormalToIntensity32( pixel.g );
            return ( r + g ) / 2f;
        }

        Vector3 DefaultNormalCalculate32(Color32[] imagePixels, Vector2Int dim, int x, int y, float dx, float dy) => new Vector3(-dx, -dy, 1.0f).normalized;
        Vector3 DefaultNormalCalculate(Color[] imagePixels, Vector2Int dim, int x, int y, float dx, float dy) => new Vector3(-dx, -dy, 1.0f).normalized;

        Vector3 NormalIntensityCalculate32(Color32[] imagePixels, Vector2Int dim, int x, int y, float dx, float dy)
        {
            Vector3 normal = DefaultNormalCalculate32(imagePixels, dim, x, y, dx, dy);
            float brightness = GetIntensity(imagePixels, dim.x, dim.y, x, y);
            normal = LerpBumpPlusMinus(normal, BrightnessIntensity.Evaluate(brightness));
            return normal;
        }

        Vector3 NormalIntensityCalculate(Color[] imagePixels, Vector2Int dim, int x, int y, float dx, float dy)
        {
            Vector3 normal = DefaultNormalCalculate(imagePixels, dim, x, y, dx, dy);
            float brightness = GetIntensity(imagePixels, dim.x, dim.y, x, y);
            normal = LerpBumpPlusMinus(normal, BrightnessIntensity.Evaluate(brightness));
            return normal;
        }

        readonly Vector3 zeroBump = new Vector3(0f, 0f, 1f);
        Vector3 LerpBumpPlusMinus(Vector3 normal, float t)
        {
            if (t > 0f)
            {
                return Vector3.LerpUnclamped(zeroBump, normal, t);
            }
            else
            {
                t = -t;
                return Vector3.LerpUnclamped(zeroBump, new Vector3(-normal.x, -normal.y, normal.z), t);
            }
        }

        float NormalToIntensity32( byte c ) => ( ( c / 255.0f ) - 0.5f ) * 2f;

        public Color32[] GenerateNormalMap( Color32[] imagePixels, int width, int height, float blend, float boost )
        {
            Color32[] normalMapPixels = new Color32[imagePixels.Length];
            float adjustedBlend = blend * ( 1f + boost );
            Vector2Int dim = new Vector2Int(width, height);

            for ( int y = 0; y < height; y++ )
            {
                for( int x = 0; x < width; x++ )
                {
                    float left = GetIntensity( imagePixels, width, height, x - 1, y ) * adjustedBlend;
                    float right = GetIntensity( imagePixels, width, height, x + 1, y ) * adjustedBlend;
                    float top = GetIntensity( imagePixels, width, height, x, y - 1 ) * adjustedBlend;
                    float bottom = GetIntensity( imagePixels, width, height, x, y + 1 ) * adjustedBlend;

                    float dx = right - left;
                    float dy = bottom - top;

                    Vector3 normal = NormalIntensityCalculate32(imagePixels, dim, x, y, dx, dy);
                    //Vector3 normal = new Vector3( -dx, -dy, 1.0f ).normalized;

                    normalMapPixels[y * width + x] = new Color32(
                        (byte)( ( normal.x * 0.5f + 0.5f ) * 255.0f ),
                        (byte)( ( normal.y * 0.5f + 0.5f ) * 255.0f ),
                        (byte)( ( normal.z * 0.5f + 0.5f ) * 255.0f ),
                        255
                    );
                }
            }

            return normalMapPixels;
        }

        private float GetIntensity( Color32[] pixels, int width, int height, int x, int y )
        {
            Color32 pixel = pixels[GetPXLoop( x, y, new Vector2( width, height ) )];
            return ( pixel.r + pixel.g + pixel.b ) / 3.0f / 255.0f;
        }

        int GetNormalRange( int width, int height ) => Mathf.Max( 1, Mathf.RoundToInt( Mathf.Lerp( 1, ( width + height ) / 128f, Range ) ) );

        static float GetRangeWeight( int x, int y, float maxDistanceSquared )
        {
            return Mathf.Max( 0f, 1f - ( x * x + y * y ) / maxDistanceSquared );
        }

        static float GetRangeResponse( int range, float sensitivity )
        {
            float maxDistance = range * sensitivity + 0.1f;
            float response = 0f;
            for( int y = -range; y <= range; y++ )
                for( int x = -range; x <= range; x++ )
                    response += GetRangeWeight( x, y, maxDistance * maxDistance ) * x * x;
            return response;
        }

        public Color[] GenerateNormalMap( Color[] imagePixels, int width, int height, int range, float rangeSensitivity )
        {
            return GenerateNormalMap( imagePixels, width, height, range, rangeSensitivity, Vector2.one );
        }

        Color[] GenerateNormalMap( Color[] imagePixels, int width, int height, int range, float rangeSensitivity, Vector2 gradientScale )
        {
            Color[] normalMapPixels = new Color[imagePixels.Length];
            float blend = Blend * ( 1f + Boost );
            float maxDistance = range * rangeSensitivity + 0.1f;
            int diameter = range * 2 + 1;
            Vector2[] weights = new Vector2[diameter * diameter];
            for( int y = -range, index = 0; y <= range; y++ )
                for( int x = -range; x <= range; x++, index++ )
                {
                    float weight = GetRangeWeight( x, y, maxDistance * maxDistance );
                    weights[index] = new Vector2( x * weight, y * weight );
                }

            float[] intensities = new float[imagePixels.Length];
            for( int i = 0; i < intensities.Length; i++ )
                intensities[i] = ( imagePixels[i].r + imagePixels[i].g + imagePixels[i].b ) / 3f;

            for( int y = 0; y < height; y++ )
            {
                for( int x = 0; x < width; x++ )
                {
                    float dx = 0f, dy = 0f;
                    int pixelIndex = y * width + x;
                    float centerIntensity = intensities[pixelIndex];
                    int weightIndex = 0;

                    for( int offsetY = -range; offsetY <= range; offsetY++ )
                    {
                        int sampleY = ( ( y + offsetY ) % height + height ) % height;
                        int row = sampleY * width;
                        for( int offsetX = -range; offsetX <= range; offsetX++, weightIndex++ )
                        {
                            Vector2 weight = weights[weightIndex];
                            if( weight.x == 0f && weight.y == 0f ) continue;
                            int sampleX = x + offsetX;
                            if( sampleX < 0 || sampleX >= width ) sampleX = ( sampleX % width + width ) % width;
                            float difference = intensities[row + sampleX] - centerIntensity;
                            dx += difference * weight.x;
                            dy += difference * weight.y;
                        }
                    }

                    Vector3 normal = new Vector3( -dx * blend * gradientScale.x, -dy * blend * gradientScale.y, 1f ).normalized;
                    normal = LerpBumpPlusMinus( normal, BrightnessIntensity.Evaluate( centerIntensity ) );

                    Color normalColor = new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f
                    );

                    normalMapPixels[y * width + x] = normalColor;
                }
            }

            return normalMapPixels;
        }


        private float GetIntensity( Color[] pixels, int width, int height, int x, int y )
        {
            Color pixel = pixels[GetPXLoop( x, y, new Vector2( width, height ) )];
            return ( pixel.r + pixel.g + pixel.b ) / 3.0f; // Average intensity
        }


        public Color32[] ConvertDirectXToOpenGL( Color[] baseNormalMap, int width, int height )
        {
            Color[] coverterNormalMap = new Color[baseNormalMap.Length];

            for( int y = 0; y < height; y++ )
            {
                for( int x = 0; x < width; x++ )
                {
                    Color baseNormalColor = baseNormalMap[y * width + x];

                    Color convertedCol = new Color(
                        baseNormalColor.r,
                        1f - baseNormalColor.g,       // Invert
                        baseNormalColor.b,
                        baseNormalColor.a
                    );

                    coverterNormalMap[y * width + x] = convertedCol;
                }
            }

            Color32[] pix = new Color32[coverterNormalMap.Length];

            for( int i = 0; i < pix.Length; i++ ) pix[i] = (Color32)coverterNormalMap[i];

            return pix;
        }


        public Color32[] InvertNormal( Color[] baseNormalMap, int width, int height )
        {
            Color[] coverterNormalMap = new Color[baseNormalMap.Length];

            for( int y = 0; y < height; y++ )
            {
                for( int x = 0; x < width; x++ )
                {
                    Color baseNormalColor = baseNormalMap[y * width + x];

                    Color convertedCol = new Color(
                        1f - baseNormalColor.r,
                        baseNormalColor.g,
                        baseNormalColor.b,
                        baseNormalColor.a
                    );

                    coverterNormalMap[y * width + x] = convertedCol;
                }
            }

            Color32[] pix = new Color32[coverterNormalMap.Length];

            for( int i = 0; i < pix.Length; i++ ) pix[i] = (Color32)coverterNormalMap[i];

            return pix;
        }

    }

}
