using FIMSpace.FTextureTools;
using FIMSpace.FTex;
using UnityEditor;
using UnityEngine;

namespace FIMSpace.FEditor
{
    public class FChannelInserter : EditorWindow
    {
        public Texture2D From;
        public enum EChannelSelect { R, G, B, A, RGB }
        public EChannelSelect ChannelFrom = EChannelSelect.R;
        public EChannelSelect ApplyTo = EChannelSelect.A;

        public static void Init()
        {
            FChannelInserter window = (FChannelInserter)GetWindow( typeof( FChannelInserter ) );

            window.minSize = new Vector2( 270f, 225f );

            window.titleContent = new GUIContent( "Channel Insert", FTextureToolsGUIUtilities.FindIcon( "SPR_Channels" ) );

            window.position = new Rect( 200, 100, 270, 225 );

            window.Show();
        }


        void OnGUI()
        {
            Texture2D texture = null;

            if( Selection.objects.Length > 0 )
            {
                for( int i = 0; i < Selection.objects.Length; i++ )
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>( AssetDatabase.GetAssetPath( Selection.objects[i] ) );
                    if( texture != null ) break;
                }
            }

            if( texture == null )
            {
                EditorGUILayout.HelpBox( "You must select at least one texture file!", MessageType.Warning );
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField( "Texture to edit: " + texture.name );

            if( texture )
            {
                GUILayout.FlexibleSpace();
                var texRect = GUILayoutUtility.GetRect( 40, 40 );
                GUI.DrawTexture( texRect, texture, ScaleMode.ScaleToFit );
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space( 6 );

            EditorGUIUtility.labelWidth = 190;
            From = (Texture2D)EditorGUILayout.ObjectField( From == null ? "Get Channel From:" : "Get Channel From: " + From.name, From, typeof( Texture2D ), false );
            EditorGUIUtility.labelWidth = 0;

            GUILayout.Space( 6 );
            ChannelFrom = (EChannelSelect)EditorGUILayout.EnumPopup( "Get channel:", ChannelFrom );
            ApplyTo = (EChannelSelect)EditorGUILayout.EnumPopup( "Apply it to:", ApplyTo );


            if( From != null && texture != null )
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUIUtility.labelWidth = 50;
                EditorGUILayout.LabelField( "Will Paste this: " );
                var rect = GUILayoutUtility.GetRect( 40, 40 );
                FTextureToolsGUIUtilities.DrawPreviewTexture( rect, From, ScaleMode.ScaleToFit, EChannelToWriteChannel( ChannelFrom ) );
                EditorGUILayout.LabelField( "Replacing this: " );
                rect = GUILayoutUtility.GetRect( 40, 40 );
                FTextureToolsGUIUtilities.DrawPreviewTexture( rect, texture, ScaleMode.ScaleToFit, EChannelToWriteChannel( ApplyTo ) );
                EditorGUILayout.EndHorizontal();
                EditorGUIUtility.labelWidth = 0;
            }



            FTextureToolsGUIUtilities.DrawUILine( Color.white * 0.35f, 2, 5 );

            if( From != null )
            {
                if( GetSourceDimensions( From ) != GetSourceDimensions( texture ) )
                {
                    EditorGUILayout.HelpBox( "Source file dimensions differ. The source channel will be scaled in memory to fit the target's full resolution.", MessageType.Info );
                }

                if( GUILayout.Button( ( ApplyTo == EChannelSelect.A ? "(Requires A channel) " : "" ) + "Insert '" + ChannelFrom + "' to '" + ApplyTo + "' channel of " + texture.name ) )
                {
                    ProcessChanneling( From, texture );
                }

                if( GUILayout.Button( "Duplicate (png) and Insert Channel (" + texture.name + ")" ) )
                {
                    Texture2D duplicated = FTextureEditorToolsMethods.DuplicateAsPNG( texture, "-PNG", true, ApplyTo == EChannelSelect.A );
                    if( duplicated != null ) ProcessChanneling( From, duplicated );
                }
            }
            else
            {
                EditorGUILayout.HelpBox( "You must select 'From' texture", MessageType.Info );
            }

        }

        UnityEngine.Rendering.ColorWriteMask EChannelToWriteChannel( EChannelSelect channel )
        {
            switch( channel )
            {
                case EChannelSelect.R: return UnityEngine.Rendering.ColorWriteMask.Red;
                case EChannelSelect.G: return UnityEngine.Rendering.ColorWriteMask.Green;
                case EChannelSelect.B: return UnityEngine.Rendering.ColorWriteMask.Blue;
                case EChannelSelect.A: return UnityEngine.Rendering.ColorWriteMask.Alpha;
            }

            return UnityEngine.Rendering.ColorWriteMask.All;
        }

        static Vector2Int GetSourceDimensions( Texture2D texture )
        {
            var importer = AssetImporter.GetAtPath( AssetDatabase.GetAssetPath( texture ) ) as TextureImporter;
            if( importer == null ) return new Vector2Int( texture.width, texture.height );

            int width, height;
            importer.GetSourceTextureWidthAndHeight( out width, out height );
            return new Vector2Int( width, height );
        }

        public void ProcessChanneling( Texture2D source, Texture2D target )
        {
            if( source == null || target == null ) return;

            TextureImporter srcImporter = FTextureEditorToolsMethods.GetTextureAsset( source );
            TextureImporter tgtImporter = FTextureEditorToolsMethods.GetTextureAsset( target );
            if( srcImporter == null || tgtImporter == null ) return;

            bool sameTexture = source == target;
            var srcInfo = FTextureEditorToolsMethods.GetTextureInfo( srcImporter, source );
            var tgtInfo = sameTexture ? srcInfo : FTextureEditorToolsMethods.GetTextureInfo( tgtImporter, target );

            try
            {
                EditorUtility.DisplayProgressBar( "Channeling textures...", "Reading full-resolution textures", 0.2f );

                FTextureEditorToolsMethods.StartEditingTextureAsset( srcImporter, source, srcInfo );
                if( !sameTexture ) FTextureEditorToolsMethods.StartEditingTextureAsset( tgtImporter, target, tgtInfo );

                // Reimporting removes the import size limit, so use the reloaded textures' dimensions.
                source = AssetDatabase.LoadAssetAtPath<Texture2D>( srcImporter.assetPath );
                target = sameTexture ? source : AssetDatabase.LoadAssetAtPath<Texture2D>( tgtImporter.assetPath );

                Color32[] srcPixels = source.GetPixels32();
                Color32[] newPixels = target.GetPixels32();

                if( source.width != target.width || source.height != target.height )
                {
                    EditorUtility.DisplayProgressBar( "Channeling textures...", "Scaling source channel to fit " + target.name, 0.5f );
                    // Treat channels independently, including RGB data in fully transparent pixels.
                    srcPixels = FTex_ScaleLanczos.ScaleTexture( srcPixels, source.width, source.height, target.width, target.height, 4, false );
                }

                for( int i = 0; i < newPixels.Length; i++ )
                    newPixels[i] = SwapChannel( srcPixels[i], newPixels[i], ChannelFrom, ApplyTo );

                // Only write the destination. Keep both textures at full size until writing is done.
                FTextureEditorToolsMethods.EndEditingTextureAsset( newPixels, tgtInfo, tgtImporter, target, false );
            }
            catch( System.Exception exc )
            {
                UnityEngine.Debug.LogError( "[Fimpo Image Tools] Something went wrong when channeling textures! " + exc );
            }
            finally
            {
                try
                {
                    srcInfo.RestoreOn( srcImporter, source, false );
                    srcImporter.SaveAndReimport();
                }
                finally
                {
                    try
                    {
                        if( !sameTexture )
                        {
                            tgtInfo.RestoreOn( tgtImporter, target, false );
                            tgtImporter.SaveAndReimport();
                        }
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
        }

        public Color32 SwapChannel( Color32 source, Color32 target, EChannelSelect from, EChannelSelect to )
        {
            Color32 newC = target;
            byte tgt = target.r;

            switch( from )
            {
                case EChannelSelect.R: tgt = source.r; break;
                case EChannelSelect.G: tgt = source.g; break;
                case EChannelSelect.B: tgt = source.b; break;
                case EChannelSelect.A: tgt = source.a; break;
                case EChannelSelect.RGB: tgt = (byte)( Mathf.Min( ( source.r + source.g + source.b ) / 3, byte.MaxValue ) ); break;
            }

            switch( to )
            {
                case EChannelSelect.R: newC.r = tgt; break;
                case EChannelSelect.G: newC.g = tgt; break;
                case EChannelSelect.B: newC.b = tgt; break;
                case EChannelSelect.A: newC.a = tgt; break;
                case EChannelSelect.RGB: newC.r = tgt; newC.g = tgt; newC.b = tgt; break;
            }

            return newC;
        }

    }
}
