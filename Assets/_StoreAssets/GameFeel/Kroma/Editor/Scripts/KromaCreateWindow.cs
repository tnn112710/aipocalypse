using UnityEngine;
using UnityEditor;
using System.IO;

namespace FayLabs.Kroma.Editor
{
    public enum KromaProfilePreset
    {
        Default,
        SunnyMorning,
        StarryNight,
        RainyDay,
        GoldenHour
    }

    public class KromaCreateWindow : EditorWindow
    {
        private string _targetFolder;
        private string _newName;
        private bool _applyImmediately = true;
        private KromaProfilePreset _selectedPreset = KromaProfilePreset.Default;
        private Texture2D _defaultThumbnail;

        private static readonly string[] _presetLabels = new string[]
        {
            "Default",
            "Sunny Morning",
            "Starry Night",
            "Rainy Day",
            "Golden Hour"
        };

        private static readonly string[] _presetSourceNames = new string[]
        {
            null, // Default — no source, create blank
            "Kroma_Profile_01_AzureNoon",
            "Kroma_Profile_02_StarryNight",
            "Kroma_Profile_03_RainyDay",
            "Kroma_Profile_27_GoldenHour"
        };

        public static void ShowPrompt(string targetFolder)
        {
            KromaCreateWindow window = GetWindow<KromaCreateWindow>("Create Profile");
            window._targetFolder = targetFolder;
            window._newName = "NewProfile";
            window._selectedPreset = KromaProfilePreset.Default;
            
            // Size matched with Skybox Modal (350x320)
            window.minSize = new Vector2(350, 320); 
            window.maxSize = new Vector2(350, 320);
            window.CenterOnMainWin();
        }

        private void OnEnable()
        {
            string[] thumbGuids = AssetDatabase.FindAssets(KromaConstants.DEFAULT_THUMBNAIL_NAME);
            if (thumbGuids.Length > 0)
            {
                string thumbPath = AssetDatabase.GUIDToAssetPath(thumbGuids[0]);
                _defaultThumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbPath);
            }
        }

        private Texture2D GetPresetThumbnail()
        {
            if (_selectedPreset == KromaProfilePreset.Default)
            {
                return _defaultThumbnail;
            }

            int index = (int)_selectedPreset;
            if (index > 0 && index < _presetSourceNames.Length)
            {
                string assetName = _presetSourceNames[index];
                string[] guids = AssetDatabase.FindAssets($"t:KromaProfile {assetName}");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) == assetName)
                    {
                        KromaProfile profile = AssetDatabase.LoadAssetAtPath<KromaProfile>(path);
                        if (profile != null && profile.thumbnail != null)
                        {
                            return profile.thumbnail;
                        }
                    }
                }
            }

            return _defaultThumbnail;
        }

        private void OnGUI()
        {
            // --- 1. BANNER (350 x 150) ---
            float bannerHeight = 150f;
            Rect bannerRect = new Rect(0, 0, position.width, bannerHeight);

            // Draw clean dark background behind the entire banner area first
            EditorGUI.DrawRect(bannerRect, new Color(0.12f, 0.12f, 0.12f));

            // Draw current active preview or default fallback inside a clipping group
            Texture2D activeBanner = GetPresetThumbnail();
            if (activeBanner != null)
            {
                // Define the 350x150 crop frame
                GUI.BeginGroup(bannerRect);

                float textureAspect = (float)activeBanner.width / activeBanner.height;
                
                // Scale texture to fill the entire width proportionally
                float drawWidth = bannerRect.width;
                float drawHeight = drawWidth / textureAspect;
                
                // Center vertically within the 150px height frame
                float yOffset = (bannerHeight - drawHeight) * 0.5f;
                Rect drawRect = new Rect(0, yOffset, drawWidth, drawHeight);

                GUI.DrawTexture(drawRect, activeBanner, ScaleMode.StretchToFill);

                GUI.EndGroup();
            }

            // --- 2. CONTENT AREA ---
            float padding = 15f;
            Rect contentRect = new Rect(padding, bannerHeight + padding, position.width - (padding * 2), position.height - bannerHeight - padding);
            
            GUILayout.BeginArea(contentRect);
            
            GUILayout.Label("New Profile Name:", EditorStyles.label);
            GUILayout.Space(4);
            
            GUI.SetNextControlName("NameField");
            _newName = EditorGUILayout.TextField(_newName, GUILayout.Height(20));
            
            GUILayout.Space(8);

            // --- PRESET DROPDOWN ---
            _selectedPreset = (KromaProfilePreset)EditorGUILayout.Popup("Choose Preset:", (int)_selectedPreset, _presetLabels);

            GUILayout.Space(8);
            _applyImmediately = EditorGUILayout.ToggleLeft(" Switch to this new profile", _applyImmediately, EditorStyles.boldLabel);
            
            GUILayout.Space(15); 

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }
            GUILayout.Space(10);
            if (GUILayout.Button("Create", GUILayout.Height(30)))
            {
                if (!string.IsNullOrEmpty(_newName))
                {
                    KromaProfileCreator.CreateProfileAsset(_targetFolder, _newName, _applyImmediately, _selectedPreset);
                    Close();
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.EndArea();
            
            // Focus text field on open
            if (Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("NameField");
            }
        }

        private void CenterOnMainWin()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            Rect pos = position;
            float centerWidth = (main.width - pos.width) * 0.5f;
            float centerHeight = (main.height - pos.height) * 0.5f;
            pos.x = main.x + centerWidth;
            pos.y = main.y + centerHeight;
            position = pos;
        }
    }
}
