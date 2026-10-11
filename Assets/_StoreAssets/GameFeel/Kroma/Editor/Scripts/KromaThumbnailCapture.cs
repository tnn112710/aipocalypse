using UnityEngine;
using UnityEditor;
using System.IO;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Handles the interactive "Crop & Capture" overlay for Kroma Profile thumbnails.
    /// Draws a dimmed overlay on the Scene View and captures a specific 512x512 region.
    /// </summary>
    public class KromaThumbnailCapture
    {
        private bool _isCapturing = false;
        private KromaProfile _targetProfile;
        private Texture2D _headerTexture; // New Header Texture
        private bool _isLoadingHeader = false;
        
        // Configuration
        private const int CAPTURE_SIZE = 512;
        private readonly Color OVERLAY_COLOR = new Color(0, 0, 0, 0.6f);
        private readonly Color FRAME_COLOR = Color.white;
        
        // GUI Styles
        private GUIStyle _buttonStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _instructionStyle;

        /// <summary>
        /// Starts the interactive capture mode for the specified profile.
        /// </summary>
        public static void BeginCapture(KromaProfile profile)
        {
            if (profile == null) return;
            
            KromaThumbnailCapture captureInstance = new KromaThumbnailCapture();
            captureInstance.Start(profile);
        }

        private void Start(KromaProfile profile)
        {
            _targetProfile = profile;
            _isCapturing = true;
            
            // Subscribe to SceneView updates
            SceneView.duringSceneGui += OnSceneGUI;
            
            // Focus Scene View
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Focus();
                SceneView.lastActiveSceneView.Repaint();
            }
            else
            {
                // Try to find any scene view
                SceneView view = SceneView.sceneViews.Count > 0 ? (SceneView)SceneView.sceneViews[0] : null;
                if (view != null) 
                {
                    view.Focus();
                    view.Repaint();
                }
                else
                {
                    EditorUtility.DisplayDialog("Kroma", "No Scene View found. Please open a Scene View to capture.", "OK");
                    EndCapture();
                }
            }
        }

        private void EndCapture()
        {
            _isCapturing = false;
            _targetProfile = null;
            SceneView.duringSceneGui -= OnSceneGUI;
            
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_isCapturing || _targetProfile == null)
            {
                EndCapture();
                return;
            }
            
            // Disable selection interaction while capturing
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            
            DrawOverlay(sceneView);
            DrawControls(sceneView);
            
            // Force repaint to keep UI responsive
            sceneView.Repaint();
        }

        private void DrawOverlay(SceneView sceneView)
        {
            Camera cam = sceneView.camera;
            if (cam == null) return;

            Rect screenRect = new Rect(0, 0, sceneView.position.width, sceneView.position.height);
            
            // Calculate center frame
            float centerX = screenRect.width / 2f;
            float centerY = screenRect.height / 2f;
            float halfSize = CAPTURE_SIZE / 2f;
            
            Rect frameRect = new Rect(centerX - halfSize, centerY - halfSize, CAPTURE_SIZE, CAPTURE_SIZE);
            
            Handles.BeginGUI();
            
            // Draw dimmed areas (4 rectangles around the center hole)
            // Top
            EditorGUI.DrawRect(new Rect(0, 0, screenRect.width, frameRect.y), OVERLAY_COLOR);
            // Bottom
            EditorGUI.DrawRect(new Rect(0, frameRect.yMax, screenRect.width, screenRect.height - frameRect.yMax), OVERLAY_COLOR);
            // Left
            EditorGUI.DrawRect(new Rect(0, frameRect.y, frameRect.x, frameRect.height), OVERLAY_COLOR);
            // Right
            EditorGUI.DrawRect(new Rect(frameRect.xMax, frameRect.y, screenRect.width - frameRect.xMax, frameRect.height), OVERLAY_COLOR);
            
            // Draw Banner
            if (_headerTexture == null)
            {
                 // Try to find the banner texture safely
                 if (!_isLoadingHeader)
                 {
                     _isLoadingHeader = true;
                     EditorApplication.delayCall += () =>
                     {
                        string[] guids = AssetDatabase.FindAssets("Capture_Header t:Texture2D");
                        if (guids.Length > 0)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                            _headerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        }
                        _isLoadingHeader = false;
                        
                        // Force Repaint
                        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
                     };
                 }
            }

            if (_headerTexture != null)
            {
                // Draw centered banner above frame
                float bannerWidth = frameRect.width; 
                float aspect = (float)_headerTexture.width / _headerTexture.height;
                float bannerHeight = bannerWidth / aspect;
                
                Rect bannerRect = new Rect(
                    frameRect.x, 
                    frameRect.y - bannerHeight, 
                    bannerWidth, 
                    bannerHeight
                );
                
                // Draw White Background
                EditorGUI.DrawRect(bannerRect, Color.white);
                
                // Draw Texture
                GUI.DrawTexture(bannerRect, _headerTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                // Fallback to text if texture missing
                if (_titleStyle == null) // Check if style needs init/update
                {
                    _titleStyle = new GUIStyle(EditorStyles.boldLabel);
                    _titleStyle.alignment = TextAnchor.MiddleCenter;
                    _titleStyle.normal.textColor = Color.white;
                    _titleStyle.fontSize = 24;
                }
                
                Rect titleRect = new Rect(frameRect.x, frameRect.y - 40, frameRect.width, 30);
                GUI.Label(titleRect, "KROMA Thumbnail Capture", _titleStyle);
            }

            // Draw Frame Border
            DrawHollowRect(frameRect, FRAME_COLOR, 2f);
            
            Handles.EndGUI();
        }

        private void DrawControls(SceneView sceneView)
        {
            // Recalculate Frame Rect
            float centerX = sceneView.position.width / 2f;
            float centerY = sceneView.position.height / 2f;
            float halfSize = CAPTURE_SIZE / 2f; // 256
            Rect frameRect = new Rect(centerX - halfSize, centerY - halfSize, CAPTURE_SIZE, CAPTURE_SIZE);
            
            // Bottom Bar: 40px
            float barHeight = 40f;
            Rect bottomBarRect = new Rect(frameRect.x, frameRect.yMax, frameRect.width, barHeight);
            
            Handles.BeginGUI();
            // Draw Background (White)
            EditorGUI.DrawRect(bottomBarRect, Color.white);
            
            // --- Left Content ---
            float currentX = bottomBarRect.x + 10f; // Left Padding
            float barCenterY = bottomBarRect.y + (barHeight / 2f);
            
            // Icon (20x20)
            GUIContent iconContent = EditorGUIUtility.IconContent("MoveTool");
            if (iconContent != null && iconContent.image != null)
            {
                Rect iconRect = new Rect(currentX, barCenterY - 10f, 20f, 20f);
                GUI.Label(iconRect, iconContent.image);
                currentX += 20f + 5f; // Icon width + spacing
            }
            
            // Text
            GUIStyle textStyle = new GUIStyle(EditorStyles.label);
            textStyle.normal.textColor = new Color(0.2f, 0.2f, 0.2f);
            textStyle.hover.textColor = textStyle.normal.textColor;
            textStyle.fontSize = 12;
            textStyle.alignment = TextAnchor.MiddleLeft;
            
            // Text Rect
            Rect textRect = new Rect(currentX, bottomBarRect.y, 300f, barHeight); // Full height for easy vert align
            GUI.Label(textRect, "Adjust angle, then capture.", textStyle);
            
            // --- Right Content (Buttons) ---
            float rightX = bottomBarRect.xMax - 10f; // Right Padding
            
            // Colors
            Color originalBg = GUI.backgroundColor;
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.normal.textColor = Color.white;
            btnStyle.fontStyle = FontStyle.Bold;
            
            // Capture Button (80x30)
            Rect captureRect = new Rect(rightX - 80f, barCenterY - 15f, 80f, 30f);
            
            GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            if (GUI.Button(captureRect, "Capture", btnStyle))
            {
                // Defer to delayCall to avoid rendering mid-GUI event
                KromaProfile profileRef = _targetProfile;
                EditorApplication.delayCall += () => 
                {
                    if (profileRef != null && sceneView != null)
                    {
                        PerformCapture(sceneView, profileRef);
                    }
                };
                EndCapture();
            }
            
            // Cancel Button (80x30) - 5px spacing
            Rect cancelRect = new Rect(captureRect.x - 5f - 80f, barCenterY - 15f, 80f, 30f);
            
            GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f);
            if (GUI.Button(cancelRect, "Cancel", btnStyle))
            {
                EndCapture();
            }
            
            GUI.backgroundColor = originalBg;
            
            Handles.EndGUI();
        }

        private void PerformCapture(SceneView sceneView, KromaProfile targetProfile)
        {
            if (targetProfile == null) return;
            Camera sceneCam = sceneView.camera;
            if (sceneCam == null) return;
            
            // 1. Create a CLONE Camera to render safely without messing up SceneView state
            GameObject captureGO = new GameObject("Kroma_Capture_Cam");
            Camera captureCam = captureGO.AddComponent<Camera>();
            captureCam.CopyFrom(sceneCam);
            
            // Ensure we capture everything
            captureCam.enabled = false; // We will render manually
            captureCam.scene = sceneCam.scene; // Ensure it's in the right scene context
            
            // URP SPECIFIC: We MUST ensure the capture camera has URP data and Post-Processing enabled
            var captureCamData = captureGO.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (captureCamData == null) captureCamData = captureGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            
            // Try to read from source, or use sensible defaults
            var sceneCamData = sceneCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (sceneCamData != null)
            {
                 captureCamData.antialiasing = sceneCamData.antialiasing;
                 captureCamData.volumeLayerMask = sceneCamData.volumeLayerMask;
                 captureCamData.volumeTrigger = sceneCamData.volumeTrigger;
            }
            else
            {
                 captureCamData.volumeLayerMask = -1; // See everything
                 captureCamData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            
            // We use the current screen resolution of the Scene View for the texture
            int width = (int)sceneView.position.width;
            int height = (int)sceneView.position.height;
            
            int pixelWidth = (int)sceneCam.pixelRect.width;
            int pixelHeight = (int)sceneCam.pixelRect.height;

            captureCam.cameraType = CameraType.Game;
            
            captureCam.allowHDR = false;
            captureCam.allowMSAA = false;
            captureCam.depthTextureMode = DepthTextureMode.None;

            // FORCE ENABLE FEATURES
            captureCamData.renderPostProcessing = true;
            captureCamData.renderShadows = true;

            RenderTextureFormat format = RenderTextureFormat.Default;
            RenderTextureReadWrite rwMode = (QualitySettings.activeColorSpace == ColorSpace.Linear) ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Default;
            
            RenderTexture rt = new RenderTexture(pixelWidth, pixelHeight, 32, format, rwMode);
            rt.name = "Kroma_Capture_RT";
            
            try 
            {
                captureCam.targetTexture = rt;
                captureCam.Render();
                
                RenderTexture.active = rt;
                
                float pixelsPerPoint = (float)pixelWidth / width;
                int capturePixelSize = Mathf.RoundToInt(CAPTURE_SIZE * pixelsPerPoint);
                
                int texCenterX = pixelWidth / 2;
                int texCenterY = pixelHeight / 2;
                
                int readX = texCenterX - (capturePixelSize / 2);
                int readY = texCenterY - (capturePixelSize / 2);
                
                if (readX < 0) readX = 0;
                if (readY < 0) readY = 0;
                if (readX + capturePixelSize > pixelWidth) readX = pixelWidth - capturePixelSize;
                if (readY + capturePixelSize > pixelHeight) readY = pixelHeight - capturePixelSize;
                
                Texture2D tempTex = new Texture2D(capturePixelSize, capturePixelSize, TextureFormat.RGB24, false);
                tempTex.ReadPixels(new Rect(readX, readY, capturePixelSize, capturePixelSize), 0, 0);
                tempTex.Apply();
                
                Texture2D finalTex;
                if (capturePixelSize != CAPTURE_SIZE)
                {
                    RenderTexture resizeRT = RenderTexture.GetTemporary(CAPTURE_SIZE, CAPTURE_SIZE, 0);
                    Graphics.Blit(tempTex, resizeRT);
                    
                    RenderTexture.active = resizeRT;
                    finalTex = new Texture2D(CAPTURE_SIZE, CAPTURE_SIZE, TextureFormat.RGB24, false);
                    finalTex.ReadPixels(new Rect(0, 0, CAPTURE_SIZE, CAPTURE_SIZE), 0, 0);
                    finalTex.Apply();
                    
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(resizeRT);
                    Object.DestroyImmediate(tempTex);
                }
                else
                {
                    finalTex = tempTex;
                }
                
                SaveTexture(finalTex, targetProfile);
                
                if (finalTex != null) Object.DestroyImmediate(finalTex);
            }
            finally
            {
                RenderTexture.active = null;
                if (rt != null) rt.Release();
                if (captureGO != null) Object.DestroyImmediate(captureGO);
            }
        }

        private void SaveTexture(Texture2D tex, KromaProfile profile)
        {
            byte[] bytes = tex.EncodeToPNG();
            
            string profilePath = AssetDatabase.GetAssetPath(profile);
            string profileDir = Path.GetDirectoryName(profilePath).Replace("\\", "/");
            string parentDir = Path.GetDirectoryName(profileDir).Replace("\\", "/");
            
            string thumbnailDir;
            
            if (profileDir.EndsWith("/Data"))
            {
                thumbnailDir = Path.Combine(parentDir, "Thumbnails").Replace("\\", "/");
            }
            else
            {
                thumbnailDir = KromaAssetManager.GetThumbnailsFolder();
            }
            
            if (!Directory.Exists(thumbnailDir))
            {
                Directory.CreateDirectory(thumbnailDir);
            }
            
            string filename = $"{profile.name}_Thumb.png";
            string fullPath = Path.Combine(thumbnailDir, filename).Replace("\\", "/");
            
            File.WriteAllBytes(fullPath, bytes);
            AssetDatabase.Refresh();
            
            Texture2D importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (importedTex != null)
            {
                TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.crunchedCompression = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.SaveAndReimport();
                }

                profile.thumbnail = importedTex;
                EditorUtility.SetDirty(profile);
                
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Thumbnail Captured!"));
                }
            }
        }
        
        private void DrawHollowRect(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color); // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color); // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color); // Left
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color); // Right
        }
    }
}
