using FIMSpace.FEditor;
using UnityEditor;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    public class FTexEqualizeWindow : FTextureProcessWindow
    {
        private float Equalize = 0.5f;
        private float EqualizeWhites = 1f;
        private float EqualizeBlacks = 1f;
        private float EqualizeTexture = 0f;

        bool MaskingMode = false;
        Texture2D TexturizeWith;
        Color MaskingColor = Color.white;
        bool TexturizeReadable = false;
        float TexturizeTiling = 1f;
        bool TexturizeBlending = false;
        bool RandomSplat = true;

        AnimationCurve WhitesIntensity = AnimationCurve.EaseInOut( 0f, 1f, 1f, 1f );
        AnimationCurve ShadowsIntensity = AnimationCurve.EaseInOut( 0f, 1f, 1f, 1f );

        Texture2D texturizeMemo = null;
        Texture2D texturizeMemoSource = null;
        EMaskExport maskExport = EMaskExport.None;

        public static void Init()
        {
            FTexEqualizeWindow window = (FTexEqualizeWindow)GetWindow( typeof( FTexEqualizeWindow ) );
            window.titleContent = new GUIContent( "Texure Equalize", FTextureToolsGUIUtilities.FindIcon( "SPR_EqualizerGen" ), "Tweak too bright or too dark parts of your texture" );
            window.previewScale = FEPreview.m_1x1;
            window.drawPreviewScale = true;

            window.position = new Rect( 340, 50, 585, 630 );
            window.Show();
            called = true;
        }


        protected override void OnGUICustom()
        {
            EditorGUI.BeginChangeCheck();

            GUI.backgroundColor = new Color( 0.6f, 1f, 0.7f );
            Equalize = EditorGUILayout.Slider( new GUIContent( "Equalize Amount" ), Equalize, 0.0f, 1f );
            GUI.backgroundColor = Color.white;
            GUILayout.Space( 8 );

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 0;
            EqualizeWhites = EditorGUILayout.Slider( new GUIContent( "Whites" ), EqualizeWhites, 0f, 2f, GUILayout.MaxWidth( position.width - 60 ) );
            EditorGUIUtility.labelWidth = 5;
            WhitesIntensity = EditorGUILayout.CurveField( new GUIContent( " ", "Whites mapping power intensity control basing on the pixel brightness. X = brightness value -> 0 is black 1 is white, Y = highlights intensity blend from 0 to 1" ), WhitesIntensity, Color.cyan, new Rect( 0f, 0, 1f, 1f ), GUILayout.Width( 44 ) );
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 0;
            EqualizeBlacks = EditorGUILayout.Slider( new GUIContent( "Shadows", "0 disables shadows. Up to 1 attenuates shadow equalization; above 1 gradually extends the falloff into nearby midtones without overshooting the average brightness." ), EqualizeBlacks, 0f, 2f, GUILayout.MaxWidth( position.width - 60 ) );
            EditorGUIUtility.labelWidth = 5;
            ShadowsIntensity = EditorGUILayout.CurveField( new GUIContent( " ", "Shadow attenuation by pixel brightness. X = brightness (0 is black, 1 is white), Y = shadow influence from 0 to 1, including midtones reached with Shadows above 1." ), ShadowsIntensity, Color.gray, new Rect( 0f, 0, 1f, 1f ), GUILayout.Width( 44 ) );
            EditorGUIUtility.labelWidth = 0;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space( 8 );

            EditorGUILayout.BeginHorizontal();

            MaskingMode = EditorGUILayout.Toggle( new GUIContent( "Masking Mode:", "Using single color to replace target texture areas, which can be useful when generating masks for shaders." ), MaskingMode );

            if( MaskingMode )
            {
                MaskingColor = EditorGUILayout.ColorField( "Masking Color", MaskingColor );

                if( GUILayout.Button( new GUIContent( " Export", FTextureToolsGUIUtilities.FindIcon( "SPR_GrayscaleS" ), "Export mask as grayscale .png file" ), GUILayout.MaxWidth( 70 ) ) )
                {
                    Texture2D newFile = GenerateExtraFile();
                    if( newFile != null )
                    {
                        maskExport = EMaskExport.Grayscale;
                        ApplyTexProcessToFile( newFile );
                    }
                }

                if( GUILayout.Button( new GUIContent( " Export", FTextureToolsGUIUtilities.FindIcon( "SPR_rgbscale" ), "Export mask as color channel .png file" ), GUILayout.MaxWidth( 70 ) ) )
                {
                    Texture2D newFile = GenerateExtraFile();
                    if( newFile != null )
                    {
                        maskExport = EMaskExport.Color;
                        ApplyTexProcessToFile( newFile );
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = new Color( 0.6f, 1f, 0.6f, 1f );
                EqualizeTexture = EditorGUILayout.Slider( new GUIContent( "Texturize Blend:" ), EqualizeTexture, 0f, 1f );
                GUI.backgroundColor = Color.white;

                TexturizeReadable = false;

                if( EqualizeTexture > 0f )
                {
                    RandomSplat = EditorGUILayout.Toggle( new GUIContent( "Random Splat", "Use the existing random offset and tiled texturizing. Disable to fit the selected image over the source and fade it into equalized pixels." ), RandomSplat );

                    string helpText = "";
                    if( RandomSplat ) helpText = "Texture should be tileable in order to paint mask without seams";
                    else helpText = "The overlay image is fitted over the whole source. Texturize Blend controls its fade into equalized pixels; image transparency is respected.";

                    EditorGUILayout.HelpBox( helpText, MessageType.None );

                    TexturizeWith = (Texture2D)EditorGUILayout.ObjectField( new GUIContent( RandomSplat ? "Texturize (Optional)" : "Overlay Image" ), TexturizeWith, typeof( Texture2D ), false, GUILayout.Height( 40 ) );
                    
                    if( RandomSplat )
                    {
                        TexturizeBlending = EditorGUILayout.Toggle( new GUIContent( "Blending", "Multiply texture colors in highlights and add them in shadows. The midtone fade only reduces blending near the texture's average brightness, leaving central midtones untexturized. Equalization still applies before texturizing. Strength is controlled by Texturize Blend." ), TexturizeBlending );

                        seed = EditorGUILayout.IntSlider( new GUIContent( "  Seed", FTextureToolsGUIUtilities.FindIcon( "FRandomIcon" ) ), seed, -50, 50 );
                    }

                    if( TexturizeWith != null )
                    {
                        string path = AssetDatabase.GetAssetPath( TexturizeWith );
                        TextureImporter tImp = (TextureImporter)AssetImporter.GetAtPath( path );

                        if( tImp is null == false )
                        {
                            TexturizeReadable = tImp.isReadable;

                            GUILayout.Space( 4 );
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space( 32 );

                            if( tImp.isReadable == false ) GUI.backgroundColor = Color.green;
                            if( GUILayout.Button( "Switch readonly for '" + TexturizeWith.name + "' to " + ( !tImp.isReadable ).ToString() ) )
                            {
                                tImp.isReadable = !tImp.isReadable;
                                AssetDatabase.ImportAsset( path, ImportAssetOptions.ForceUpdate );
                                TexturizeReadable = tImp.isReadable;
                            }

                            GUI.backgroundColor = Color.white;
                            GUILayout.Space( 32 );
                            EditorGUILayout.EndHorizontal();

                            if( RandomSplat )
                            {
                                GUILayout.Space( 5 );

                                EditorGUILayout.BeginHorizontal();
                                float highRange = TexturizeTiling > 4f ? 16f : 4.001f;
                                TexturizeTiling = EditorGUILayout.Slider( "Texturize Tiling:", TexturizeTiling, 0.05f, highRange );
                                EditorGUILayout.EndHorizontal();

                                Texture2D srcTex = GetFirstTexture;
                                if( srcTex ) if( TexturizeWith.width != srcTex.width )
                                    {
                                        EditorGUILayout.HelpBox( "Texture sizes differs, preview can look different than final file result!", MessageType.None );
                                    }
                            }
                        }

                        if( TexturizeReadable == false )
                            EditorGUILayout.HelpBox( "Texture must have 'Read/Write' enabled", MessageType.Warning );
                    }
                    else
                    {
                        TexturizeReadable = false;
                        if( !RandomSplat ) EditorGUILayout.HelpBox( "Assign an overlay image to texturize the equalized areas.", MessageType.Info );
                    }
                }


            }

            maskExport = EMaskExport.None;

            EditorGUI.indentLevel--;

            if( EditorGUI.EndChangeCheck() )
                somethingChanged = true;
            else
                somethingChanged = false;
        }


        private static float CalculateShadowBlend( float brightness, float averageBrightness, float highestVDiff, float shadows )
        {
            if( highestVDiff <= 0f || shadows <= 0f ) return 0f;

            float midtoneReach = Mathf.Clamp01( shadows - 1f ) * highestVDiff * 0.5f;
            float attenuation = Mathf.Clamp01( ( averageBrightness - brightness + midtoneReach ) / ( highestVDiff + midtoneReach ) );
            return attenuation * Mathf.Clamp01( shadows );
        }


        private static Color BlendTextureByTone( Color equalizedColor, Color textureColor, float brightnessDifference, float shadowRange, float highlightRange, float strength )
        {
            float brightnessRange = brightnessDifference < 0f ? shadowRange : highlightRange;
            float normalizedBrightnessDifference = brightnessRange > 0f ? brightnessDifference / brightnessRange : 0f;
            const float midtoneProtection = 0.2f;
            const float fullBlendDistance = 0.5f;
            
            float fade = Mathf.SmoothStep( 0f, 1f, Mathf.InverseLerp( midtoneProtection, fullBlendDistance, Mathf.Abs( normalizedBrightnessDifference ) ) );
            float blend = Mathf.Clamp01( strength ) * fade;

            Color result;

            if( normalizedBrightnessDifference < 0f )
            {
                result = equalizedColor + textureColor * blend;
            }
            else
            {
                result = Color.Lerp( equalizedColor, equalizedColor * textureColor, blend );
            }

            result.a = equalizedColor.a;

            return result;
        }


        private static Color SampleOverlay( Color32[] pixels, int width, int height, float u, float v )
        {
            // Sample pixel centers with clamped edges, independent of texture wrap mode
            float sampleX = Mathf.Clamp( u * width - 0.5f, 0f, width - 1f );
            float sampleY = Mathf.Clamp( v * height - 0.5f, 0f, height - 1f );

            int x0 = Mathf.FloorToInt( sampleX );
            int y0 = Mathf.FloorToInt( sampleY );
            int x1 = Mathf.Min( x0 + 1, width - 1 );
            int y1 = Mathf.Min( y0 + 1, height - 1 );

            Color bottom = Color.Lerp( pixels[y0 * width + x0], pixels[y0 * width + x1], sampleX - x0 );
            Color top = Color.Lerp( pixels[y1 * width + x0], pixels[y1 * width + x1], sampleX - x0 );

            return Color.Lerp( bottom, top, sampleY - y0 );
        }


        private static Color BlendTextureOverlay( Color equalizedColor, Color overlayColor, float equalizationBlend, float textureBlend )
        {
            float strength = Mathf.Clamp01( equalizationBlend * 2f ) * Mathf.Clamp01( textureBlend ) * overlayColor.a;
            Color result = Color.Lerp( equalizedColor, overlayColor, strength );
            result.a = equalizedColor.a;
            return result;
        }


        protected override void ProcessTexture( Texture2D source, Texture2D target, bool preview = true )
        {
            if( !preview ) EditorUtility.DisplayProgressBar( "Equalizing Texture...", "Preparing... ", 2f / 5f );

            #region Preparing variables to use down below

            Color32[] sourcePixels = source.GetPixels32();
            Color32[] newPixels = source.GetPixels32();

            Color32[] texturizePixels = sourcePixels;
            Texture2D texturizeSource = source;
            bool hasTexturizeTexture = TexturizeWith != null && TexturizeReadable;

            if( MaskingMode )
            {
                texturizePixels = texturizeSource.GetPixels32();
                for( int i = 0; i < texturizePixels.Length; i++ ) texturizePixels[i] = (Color32)MaskingColor;
            }
            else
            {
                if( hasTexturizeTexture )
                {
                    if( RandomSplat )
                    {
                        int targetWidth = Mathf.RoundToInt( source.width / TexturizeTiling );
                        int targetHeight = Mathf.RoundToInt( source.height / TexturizeTiling );
                        if( targetWidth < 1 ) targetWidth = 1;
                        if( targetHeight < 1 ) targetHeight = 1;


                        if( texturizeMemo == null || texturizeMemoSource != TexturizeWith || texturizeMemo.width != targetWidth || texturizeMemo.height != targetHeight )
                        {
                            if( texturizeMemo != null ) DestroyImmediate( texturizeMemo );
                            texturizeMemo = FTextureEditorToolsMethods.GenerateScaledTexture2DReference( TexturizeWith, new Vector2( targetWidth, targetHeight ), preview ? 2 : 4 );
                            texturizeMemoSource = TexturizeWith;
                        }

                        texturizeSource = texturizeMemo;
                    }
                    else
                        texturizeSource = TexturizeWith;

                    texturizePixels = texturizeSource.GetPixels32();
                }
            }


            if( source.width != target.width || source.height != target.height )
            {
                Debug.LogError( "[SEAMLESS GENERATOR] Source texture is different scale or target texture! Can't create seamless texture!" );
                return;
            }

            Vector2 dimensions = GetDimensions( source );

            #endregion


            #region Equalizing Texture

            if( !preview ) EditorUtility.DisplayProgressBar( "Equalizing...", "Equalizing... ", 3f / 5f );

            Vector3 rgbAverages = new Vector3( 0.0f, 0.0f, 0.0f );
            Vector3 hsvAverages = new Vector3( 0.0f, 0.0f, 0.0f );
            Vector2 dim = new Vector2( source.width, source.height );

            for( int x = 0; x < source.width; x++ )
            {
                for( int y = 0; y < source.height; y++ )
                {
                    int px = GetPX( x, y, dim );
                    Color c = sourcePixels[px];
                    rgbAverages.x += c.r;
                    rgbAverages.y += c.g;
                    rgbAverages.z += c.b;

                    float h, s, v;
                    Color.RGBToHSV( sourcePixels[px], out h, out s, out v );
                    hsvAverages.x += h;
                    hsvAverages.y += s;
                    hsvAverages.z += v;
                }
            }

            float norm = source.width * source.height;
            Vector3 rgbAveragesN = rgbAverages / norm;
            Vector3 hsvAveragesN = hsvAverages / norm;

            float highestVDiff = 0f;
            float highestShadowDiff = 0f;
            float highestWhiteDiff = 0f;

            for( int x = 0; x < source.width; x++ )
            {
                for( int y = 0; y < source.height; y++ )
                {
                    int px = GetPX( x, y, dim );

                    float h, s, v;
                    Color.RGBToHSV( sourcePixels[px], out h, out s, out v );
                    hsvAverages.x += h;
                    hsvAverages.y += s;
                    hsvAverages.z += v;

                    float vDiff = v - hsvAveragesN.z;
                    highestShadowDiff = Mathf.Max( highestShadowDiff, -vDiff );
                    highestWhiteDiff = Mathf.Max( highestWhiteDiff, vDiff );
                    float diff = Mathf.Abs( vDiff );
                    if( diff > highestVDiff ) highestVDiff = diff;
                }
            }

            Color avgHsvCol = Color.HSVToRGB( hsvAveragesN.x, hsvAveragesN.y, hsvAveragesN.z );
            int randX = 0;
            int randY = 0;

            if( RandomSplat || MaskingMode )
            {
                randX = RandomRange( texturizeSource.width / 2, texturizeSource.width / 2 + ( RandomRange( 0f, 1f ) < 0.5f ? texturizeSource.width / 3 : -source.width / 3 ) );
                randY = RandomRange( texturizeSource.height / 2, texturizeSource.height / 2 + ( RandomRange( 0f, 1f ) < 0.5f ? texturizeSource.height / 3 : -source.height / 3 ) );
            }

            for( int x = 0; x < source.width; x++ )
            {
                for( int y = 0; y < source.height; y++ )
                {
                    int px = GetPX( x, y, dim );
                    Color srcPixel = sourcePixels[px];

                    float h, s, v;
                    Color.RGBToHSV( srcPixel, out h, out s, out v );

                    float vDiff = v - hsvAveragesN.z;

                    float shadowBlend = CalculateShadowBlend( v, hsvAveragesN.z, highestVDiff, EqualizeBlacks ) * Mathf.Clamp01( ShadowsIntensity.Evaluate( v ) );
                    float lerpV = shadowBlend;
                    float whitesBlend = 0f;

                    if( vDiff > 0f && highestVDiff > 0f )
                    {
                        whitesBlend = Mathf.Abs( vDiff * WhitesIntensity.Evaluate( vDiff ) * EqualizeWhites ) / highestVDiff;
                        lerpV = Mathf.Max( lerpV, whitesBlend );
                    }

                    float tgtV = Mathf.LerpUnclamped( v, hsvAveragesN.z, lerpV );
                    Color tgtColor = Color.HSVToRGB( h, s, tgtV );

                    if( MaskingMode )
                    {
                        if( maskExport == EMaskExport.None )
                        {
                            Color offsetRefColor = texturizePixels[GetPXLoopSkipEdges( randX + x, randY + y, new Vector2( texturizeSource.width, texturizeSource.height ) )];
                            tgtColor = Color.Lerp( tgtColor, offsetRefColor, lerpV * 2f );
                        }
                        else if( maskExport == EMaskExport.Color )
                        {
                            tgtColor = Color.Lerp( Color.black, MaskingColor, lerpV * 2f );
                        }
                        else if( maskExport == EMaskExport.Grayscale )
                        {
                            tgtColor = Color.Lerp( Color.black, Color.white, lerpV * 2f );
                        }
                    }
                    else if( EqualizeTexture > 0f && !RandomSplat && hasTexturizeTexture )
                    {
                        Color overlayColor = SampleOverlay( texturizePixels, texturizeSource.width, texturizeSource.height, ( x + 0.5f ) / source.width, ( y + 0.5f ) / source.height );
                        tgtColor = BlendTextureOverlay( tgtColor, overlayColor, lerpV, EqualizeTexture );
                    }
                    else if( EqualizeTexture > 0f && RandomSplat )
                    {
                        Color offsetRefColor = texturizePixels[GetPXLoopSkipEdges( randX + x, randY + y, new Vector2( texturizeSource.width, texturizeSource.height ) )];

                        if( TexturizeBlending )
                        {
                            float textureStrength = ( vDiff < 0f ? shadowBlend : whitesBlend ) * 2f * EqualizeTexture;
                            tgtColor = BlendTextureByTone( tgtColor, offsetRefColor, vDiff, highestShadowDiff, highestWhiteDiff, textureStrength );
                        }
                        else
                            tgtColor = Color.Lerp( tgtColor, offsetRefColor, lerpV * 2f * EqualizeTexture );
                    }

                    tgtColor.a = srcPixel.a;
                    newPixels[px] = Color32.LerpUnclamped( sourcePixels[px], tgtColor, Equalize );
                }
            }

            maskExport = EMaskExport.None;

            #endregion


            // Finalizing changes
            if( !preview ) EditorUtility.DisplayProgressBar( "Equalizing Texture...", "Applying Equalization to Texture... ", 3.85f / 5f );

            target.SetPixels32( newPixels );
            target.Apply( false, false );

            if( !preview )
                EditorUtility.ClearProgressBar();

        }


    }
}
