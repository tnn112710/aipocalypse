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
using sc.screeneffects.runtime;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Assertions;

namespace sc.screeneffects.editor
{
    [NoAutoStaticsCleanup]
    public static class UI
    {
        private const string WarningIconData = "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";
        public static Texture WarningIcon;
        private const string ErrorIconData = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAD9klEQVR4Ae2bz2vUQBTHJ91S7FZ6EH/VY/GuRy8evHoVWnELgl68eBG8eNhfUPoXeOxB6B5WsGAPpZT+ISK46j/Q7MZiy2q75u04a35MkjeTl7fbrQMhmTcv8973k8nM0E2dQbslrIrj6G9rb+ntVNbVNbueBgPtfTNa6wUy/gdA/LCrfn8w1oo8IAZZoRwBVdHeapBlltSRjEEGgQoAj3gFhRACBQBe8cQQ8gIYj3hCCHkAjFc8EQRbADWWCU+JzDrnmBNsANTE+1Y9Kyf2dksIs4aJ2ou//6AlHq/hlq+PH16L/d0XhrmJ4aiUW+Um9l6TEWAvHrIplz2xs93xr9KPg72OWFjoYgXE/AxHAhZAPvHBLHe2g7Xw9cFeuG5bM4CAAUAnXgnSQaASr2IgIWQBoBevEgxCoBavYiAgpAEoTrxKECAUJV7FyICQBKB48SpBjrOEUNOF0gGYLvFKtdy7xCBEAUyn+BQIs/6uTjXDuR6sTOW1HAmjv1uY7gTtmZydRUeb7OvkRNfnZZ2xCBsfgB/eVQMBSwa+uVz5AHS7NwwyNfE16Dbuygeg55qIMvGNqzKw8AFw3SWxGZpwZZq6H1iePWF7BfQTkwFBtOvJMUxsZYT/vJC+CNf8LnwjAHJ1D+HJfslI+1ZGO2kzLwCvdx0BAHzYCi8AOQKyxLG9/5AIL4AuaiVgWwH4AfRQe4EpBuC6N7PGv9+O8UF0g3PhfQW8LmaCw/jg1CG8eAEcHmKWOIwPQhrOhReA17sWSqu6LquNN0Ez6whwBo8eBoPDhw3Flbm5Y387LHeDnheOs7go688rP0W/Px9uJK+NPnDi2wqDBinsr1KtqEUG8aHAvK8AhIaVoFSKPH7fDiPi9JR1BYB0+AF8/3pHLN/+DcFj5VvnbsxWsIF3DihYjEH3Y5oDDDLkcuWdBLlUGcS58ABmxUolyKs+kV9/BDPMe71SqQe7iK4CjSGQIj+Bede+Ekwgdr1e2xWfP92L2SkMUvzoRxHoUvcKAIQ6RTxtH09XXd+uPzaarpgp6ZdIbWcGRo14uFsHAOxFQ4AY4bLRDNcpa6trNeE4Df8Q0SMJAITng1C0eCES6aYBKBbC200hjo7k8fIV5fP+1xc8+RTx4JgFAHzoRwKIjxZqCAjxkAIGAPjRQdCJhwhQqCAgxUPI6DIItqQCEATBPmE5KcDQ/qt/KbU9q9FAPHTlWPzT1OR+RWIoHgBgXwHwVYXudVA9UpwtxEPYmei6iKw3hAxIkXr+PizFSwD24ZsTASGH+LwA4P7xQsgpngLA+CAQiKcCwA+BSDwlAD4IhOIhaZONEPhnlabvAMe5KTb7gHMjDpPohQfwB63ZMyXk5VWPAAAAAElFTkSuQmCC";
        public static Texture ErrorIcon;
        private const string InfoIconData = "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";
        public static Texture InfoIcon;
        
        public static readonly Color RedColor = new Color(1f, 0.31f, 0.34f);
        public static readonly Color OrangeColor= new Color(1f, 0.68f, 0f);
        public static readonly Color GreenColor = new Color(0.33f, 1f, 0f);
        public static readonly Color LightBlueColor = new Color(0.4f, 0.6f, 1f); 
        public static readonly Color LighterBlueColor = new Color(0.6f, 0.8f, 1f); 
        
        public static Texture CreateIcon(string data)
        {
            byte[] bytes = System.Convert.FromBase64String(data);

            Texture2D icon = new Texture2D(32, 32, TextureFormat.RGBA32, false, false);
            icon.LoadImage(bytes, true);
            
            return icon;
        }

        public static void DrawHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect();
            rect.x -= 20f;
            
            //Draw title
            GUIContent textContent = new GUIContent($"{AssetInfo.ASSET_NAME}");
            Vector2 titleSize = EditorStyles.boldLabel.CalcSize(textContent);
        
            // Calculate centered position
            float totalWidth = titleSize.x;
            Rect textRect = new Rect(rect.x + (rect.width - totalWidth) * 0.5f, rect.y, titleSize.x, titleSize.y);
            GUI.Label(textRect, textContent, EditorStyles.boldLabel);
        
            //Version
            GUIContent version = new GUIContent(AssetInfo.INSTALLED_VERSION);
            float versionWidth = EditorStyles.miniLabel.CalcSize(version).x;

            Rect versionRect = new Rect(textRect.x + titleSize.x + 1f, rect.y+1f, versionWidth, titleSize.y);
            GUI.Label(versionRect, version, EditorStyles.miniLabel);

            //Help button aligned to the right
            GUIContent helpContent = new GUIContent(EditorGUIUtility.IconContent("_Help").image, "Open asset window");
            Vector2 helpButtonSize = new Vector2(37f, 22f);
            Rect helpRect = new Rect(rect.xMax - (helpButtonSize.x * 0.5f) - 4f, rect.y + (titleSize.y - helpButtonSize.y) * 0.5f, helpButtonSize.x, helpButtonSize.y);
            
            if (GUI.Button(helpRect, helpContent))
            {
                HelpWindow.ShowWindow();
            }
            
            if (AssetInfo.VersionChecking.UPDATE_AVAILABLE)
            {
                GUIContent update = new GUIContent($" Update to {AssetInfo.VersionChecking.LATEST_VERSION}", EditorGUIUtility.IconContent("d_Package Manager").image);

                GUIStyle linkStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                linkStyle.normal.textColor = Color.Lerp(LightBlueColor, LightBlueColor * 1.4f,
                    Mathf.Sin((float)EditorApplication.timeSinceStartup * 5f) * 0.5f + 0.5f);
                linkStyle.hover.textColor = LighterBlueColor;

                float updateWidth = linkStyle.CalcSize(update).x;

                Rect updateRect = new Rect(rect.x + (rect.width - updateWidth) * 0.5f, rect.y + titleSize.y + 1f, updateWidth, titleSize.y);

                //Make it clickable
                EditorGUIUtility.AddCursorRect(updateRect, MouseCursor.Link);

                if (GUI.Button(updateRect, update, linkStyle))
                {
                    //Because update checking only occurs when the editor starts, assume the user will update, so mark it accordingly.
                    AssetInfo.VersionChecking.UPDATE_AVAILABLE = false;
                
                    AssetInfo.OpenInPackageManager();
                }
                
            }
            
            EditorGUILayout.Space();
        }
        
        public static void DrawComponentHeader<T>(bool isSetup)
        {
            DrawHeader();

            if (isSetup)
            {
                //Settings/Gear button right to the help button
                GUIContent settingsContent = new GUIContent(EditorGUIUtility.IconContent("d_SettingsIcon").image, "Edit render feature");
                Vector2 settingsButtonSize = new Vector2(37f, 22f);
                
                Rect rect = EditorGUILayout.GetControlRect();
                rect.y -= 43f;
                rect.x = rect.width - settingsButtonSize.x - 27f;
                
                Rect settingsRect = new Rect(rect.x, rect.y + (settingsButtonSize.y) * 0.5f, settingsButtonSize.x, settingsButtonSize.y);

                if (GUI.Button(settingsRect, settingsContent))
                {
                    ScriptableRendererFeature rendererFeature = AutoSetup.GetCurrentRenderFeature<T>();

                    if (rendererFeature)
                    {
                        RenderFeatureInspector.OpenPopup(rendererFeature);
                    }
                }
            }
        }
        
        public static void DrawFooter()
        {
            EditorGUILayout.Space();
            GUILayout.Label("- Staggart Creations -", Styles.Footer);
            EditorGUILayout.Space();
        }
        
        public static void DrawActionBox(string text, string label, MessageType messageType, Action action)
        {
            DrawActionBox(text, new GUIContent(label), messageType, action);
        }

        public static void DrawActionBox(string text, GUIContent buttonContent, MessageType messageType, Action action)
        {
            Assert.IsNotNull(action);

            EditorGUILayout.HelpBox(text, messageType);

            GUILayout.Space(-32);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(buttonContent, GUILayout.Width(EditorStyles.miniButton.CalcSize(new GUIContent(buttonContent)).x + 5f)))
                    action();

                GUILayout.Space(8);
            }
            GUILayout.Space(11);
        }

        private static float Sin(float offset = 0f)
        {
            return Mathf.Sin(offset + (float)EditorApplication.timeSinceStartup * Mathf.PI * 2f) * 0.5f + 0.5f;
        }
        
        public static void DrawNotification(string text, MessageType messageType = MessageType.None)
        {
            DrawHelpbox(text, messageType);
        }
        
        public static void DrawNotification(bool condition, string text, string buttonLabel, Action action, MessageType messageType = MessageType.None)
        {
            DrawHelpbox(text, messageType, condition, buttonLabel, action);
        }
        
        public static void DrawNotification(bool condition, string text, MessageType messageType = MessageType.None)
        {
            DrawHelpbox(text, messageType, condition, null, null);
        }
        
        private static void DrawHelpbox(string text, MessageType messageType = MessageType.None, bool condition = true, string buttonLabel = "", Action action = null)
        {
            if (!condition) return;
            
            Rect r = EditorGUILayout.GetControlRect();
            r.width -= 10f;

            Color sideColor = Color.gray;
            Texture icon = null;
            switch (messageType)
            {
                case (MessageType.None):
                    {
                        sideColor = Color.gray;
                    }
                    break;
                case (MessageType.Warning): //Warning
                    {
                        sideColor = Color.Lerp(OrangeColor, OrangeColor * 1.20f, Sin(r.y));
                        icon = WarningIcon;
                    }
                    break;
                case (MessageType.Error): //Error
                    {
                        sideColor = Color.Lerp(RedColor, RedColor * 1.33f, Sin(r.y));
                        icon = ErrorIcon;
                    }
                    break;
                case (MessageType.Info): //Info
                    {
                        sideColor = Color.Lerp(new Color(1f, 1f, 1f), new Color(0.9f, 0.9f, 0.9f), Sin(r.y));
                        icon = InfoIcon;
                    }
                    break;
            }
            
            float width = r.width;
            //float rightPadding = 
            float height = EditorStyles.helpBox.CalcHeight(new GUIContent(text), EditorGUIUtility.currentViewWidth) + (EditorStyles.label.lineHeight * 2f);
            r.height = height;

            Rect btnRect = r;
            GUIContent btnContent = null;
            //Showing a button instead
            if (action != null)
            {
                icon = null;

                btnContent = new GUIContent(" " + buttonLabel, EditorGUIUtility.IconContent("SceneLoadIn").image, "Execute suggested action");
                float size = EditorStyles.toolbarButton.CalcSize(btnContent).x + 5f;
                btnRect.width = size;
                btnRect.x = width - size;
                btnRect.height = EditorStyles.miniButtonMid.fixedHeight+5f;
                //Vertical center
                btnRect.y += (height * 0.5f) - (btnRect.height * 0.5f);
            }

            Rect iconRect = r;
            if (icon != null) 
            {
                float size = Mathf.Min(height * 0.75f, 50f);
                iconRect = r;
                iconRect.x = r.width - size;
                iconRect.width = size;
                iconRect.height = iconRect.width;
                //Vertical center
                iconRect.y += (height * 0.5f) - (iconRect.height * 0.5f);
                
                //Recalculate text height
                height = EditorStyles.helpBox.CalcHeight(new GUIContent(text),
                    EditorGUIUtility.currentViewWidth - size) + (EditorStyles.label.lineHeight * 2f);
                r.height = height;
            }

            float backgroundTint = EditorGUIUtility.isProSkin ? 0.4f : 1f;
            EditorGUI.DrawRect(r, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

            Rect colorRect = r;
            colorRect.width = 7f;

            EditorGUI.DrawRect(colorRect, sideColor);

            Rect textRect = r;
            textRect.x += colorRect.width + 10f;

            //Shrink text area on right side to make room
            if (icon != null) textRect.width -= iconRect.width * 2f;
            if(action != null) textRect.width -= btnRect.width + 50f;

            GUI.Label(textRect, new GUIContent(text), Styles.NotificationArea);

            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            if (action != null)
            {
                if (GUI.Button(btnRect, btnContent))
                {
                    action?.Invoke();
                }
            }

            GUILayout.Space(height - EditorStyles.label.lineHeight); //17=default line height
        }
        
        public static bool DrawSetupItem(Installer.SetupItem item)
        {
            Texture icon = Styles.CheckMark;

            switch (item.state)
            {
                case MessageType.Error: icon = Styles.ErrorIcon; break;
                case MessageType.Warning: icon = Styles.WarningIcon; break;
                case MessageType.None: icon = Styles.CheckMark; break;
                case MessageType.Info: icon = Styles.InfoIcon; break;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.label))
            {
                Color defaultColor = GUI.contentColor;

                switch (item.state)
                {
                    case MessageType.Error: GUI.contentColor = UI.RedColor;
                        break;                    
                    case MessageType.Warning: GUI.contentColor = UI.OrangeColor;
                        break;
                    case MessageType.None: GUI.contentColor = UI.GreenColor;
                        break;
                }
                
                EditorGUILayout.LabelField(new GUIContent("  " + item.name, icon), EditorStyles.boldLabel);
                
                GUI.contentColor = defaultColor;
                
                if (item.action != null)
                {
                    if (GUILayout.Button(item.actionName))
                    {
                        item.ExecuteAction();
                        return true;
                    }
                }
            }

            GUILayout.Space(-3f);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(25f);
                EditorGUILayout.LabelField(item.description, Styles.WordWrapLabel);
            }
            
            GUILayout.Space(-7f);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.Height(12f));
            
            GUILayout.Space(-3f);
            
            //No action clicked
            return false;
        }
        
        public static void DrawH2(string text)
        {
            Rect backgroundRect = EditorGUILayout.GetControlRect();
            backgroundRect.height = 25f;
            
            var labelRect = backgroundRect;

            // Background rect should be full-width
            backgroundRect.xMin = 0f;

            // Background
            float backgroundTint = (EditorGUIUtility.isProSkin ? 0.1f : 1f);
            EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

            // Title
            EditorGUI.LabelField(labelRect, new GUIContent(text), Styles.H2);
            
            EditorGUILayout.Space(backgroundRect.height * 0.5f);
        }

        public static void DrawRenderingLayerSetupError()
        {
            EditorGUILayout.HelpBox("Rendering Layers are disabled in one or more Universal Render Pipeline assets. Functionality will not work when they are actively rendering.", MessageType.Warning);
            if (GUILayout.Button("Open Installer"))
            {
                HelpWindow.ShowWindow(true);
            }
        }
        
        [NoAutoStaticsCleanup]
        public static class Styles
        {
            public static GUIStyle _NotificationArea;

            public static GUIStyle NotificationArea
            {
                get
                {
                    if (_NotificationArea == null)
                    {
                        _NotificationArea = new GUIStyle(EditorStyles.label)
                        {
                            //margin = new RectOffset(15, 0, 15, 0),
                            //padding = new RectOffset(5, 5, 5, 5),
                            richText = true,
                            wordWrap = true,
                            clipping = TextClipping.Overflow,
                        };
                    }

                    return _NotificationArea;
                }
            }

            private static Texture _CheckMark;

            public static Texture CheckMark
            {
                get
                {
                    if (_CheckMark == null)
                    {
                        _CheckMark = EditorGUIUtility.IconContent("TestPassed").image;

                    }

                    return _CheckMark;
                }
            }

            private static Texture _InfoIcon;

            public static Texture InfoIcon
            {
                get
                {
                    if (_InfoIcon == null)
                    {
                        _InfoIcon = EditorGUIUtility.IconContent("console.infoicon.sml").image;
                    }

                    return _InfoIcon;
                }
            }

            private static Texture _ErrorIcon;

            public static Texture ErrorIcon
            {
                get
                {
                    if (_ErrorIcon == null)
                    {
                        _ErrorIcon = EditorGUIUtility.IconContent("console.erroricon.sml").image;
                    }

                    return _ErrorIcon;
                }
            }

            private static Texture _WarningIcon;

            public static Texture WarningIcon
            {
                get
                {
                    if (_WarningIcon == null)
                    {
                        _WarningIcon = EditorGUIUtility.IconContent("console.warnicon.sml").image;
                    }

                    return _WarningIcon;
                }
            }
            
            private static Texture _RenderFeatureIcon;
            public static Texture RenderFeatureIcon
            {
                get
                {
                    if (_RenderFeatureIcon == null)
                    {
                        _RenderFeatureIcon = EditorGUIUtility.IconContent("d_ProceduralMaterial Icon").image;
                    }

                    return _RenderFeatureIcon;
                }
            }
            
            private static Texture _VolumeRendererIcon;
            public static Texture VolumeRendererIcon
            {
                get
                {
                    if (_VolumeRendererIcon == null)
                    {
                        string path = AssetDatabase.GUIDToAssetPath("aa30e3b0fe051f948a6a30571211d49f");
                        _VolumeRendererIcon = AssetDatabase.LoadAssetAtPath<Texture>(path);
                    }

                    return _VolumeRendererIcon;
                }
            }
            
            private static GUIStyle _UpdateText;

            public static GUIStyle UpdateText
            {
                get
                {
                    if (_UpdateText == null)
                    {
                        _UpdateText = new GUIStyle("Button")
                        {
                            //fontSize = 10,
                            alignment = TextAnchor.MiddleLeft,
                            stretchWidth = false,
                        };
                    }

                    return _UpdateText;
                }
            }

            private static GUIStyle _Footer;

            public static GUIStyle Footer
            {
                get
                {
                    if (_Footer == null)
                    {
                        _Footer = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                        {
                            richText = true,
                            alignment = TextAnchor.MiddleCenter,
                            wordWrap = true,
                            fontSize = 12
                        };
                    }

                    return _Footer;
                }
            }

            private static GUIStyle _Button;

            public static GUIStyle Button
            {
                get
                {
                    if (_Button == null)
                    {
                        _Button = new GUIStyle(GUI.skin.button)
                        {
                            alignment = TextAnchor.MiddleLeft,
                            stretchWidth = true,
                            richText = true,
                            wordWrap = true,
                            padding = new RectOffset()
                            {
                                left = 7,
                                right = 0,
                                top = 5,
                                bottom = 5
                            },
                            imagePosition = ImagePosition.ImageLeft
                        };
                    }

                    return _Button;
                }
            }

            private static GUIContent _AssetStoreBtnContent;

            public static GUIContent AssetStoreBtnContent
            {
                get
                {
                    if (_AssetStoreBtnContent == null)
                    {
                        _AssetStoreBtnContent = new GUIContent("  View on Asset Store ",
                            EditorGUIUtility.IconContent("Asset Store").image,
                            "Open web page.\n\nURL may contain an affiliate ID, this commission helps to fund the purchase of new assets in order to investigate/develop integrations for them.");
                    }

                    return _AssetStoreBtnContent;
                }
            }

            private static GUIStyle _H1;

            public static GUIStyle H1
            {
                get
                {
                    if (_H1 == null)
                    {
                        _H1 = new GUIStyle(GUI.skin.label)
                        {
                            richText = true,
                            alignment = TextAnchor.MiddleCenter,
                            wordWrap = true,
                            fontSize = 18,
                            fontStyle = FontStyle.Normal
                        };
                    }

                    return _H1;
                }
            }

            private static GUIStyle _H2;

            public static GUIStyle H2
            {
                get
                {
                    if (_H2 == null)
                    {
                        _H2 = new GUIStyle(GUI.skin.label)
                        {
                            richText = true,
                            alignment = TextAnchor.MiddleLeft,
                            wordWrap = true,
                            fontSize = 14,
                            fontStyle = FontStyle.Bold
                        };
                    }

                    return _H2;
                }
            }

            private static GUIStyle _Section;

            public static GUIStyle Section
            {
                get
                {
                    if (_Section == null)
                    {
                        _Section = new GUIStyle(EditorStyles.helpBox)
                        {
                            margin = new RectOffset(0, 0, -5, 5),
                            padding = new RectOffset(10, 10, 5, 5),
                            clipping = TextClipping.Clip,
                        };
                    }

                    return _Section;
                }
            }

            private static GUIStyle _WordWrapLabel;

            public static GUIStyle WordWrapLabel
            {
                get
                {
                    if (_WordWrapLabel == null)
                    {
                        _WordWrapLabel = new GUIStyle(EditorStyles.label);
                        _WordWrapLabel.wordWrap = true;
                        _WordWrapLabel.richText = true;
                    }

                    return _WordWrapLabel;
                }
            }

            private static GUIStyle _BoldLabel;

            public static GUIStyle BoldLabel
            {
                get
                {
                    if (_BoldLabel == null)
                    {
                        _BoldLabel = new GUIStyle(EditorStyles.largeLabel);
                        _BoldLabel.fontStyle = FontStyle.Bold;
                    }

                    return _BoldLabel;
                }
            }

            private static GUIStyle _Tab;

            public static GUIStyle Tab
            {
                get
                {
                    if (_Tab == null)
                    {
                        _Tab = new GUIStyle(EditorStyles.miniButtonMid)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            stretchWidth = true,
                            richText = true,
                            wordWrap = true,
                            fontSize = 16,
                            fixedHeight = 27.5f,
                            fontStyle = FontStyle.Bold,
                            padding = new RectOffset()
                            {
                                left = 14,
                                right = 14,
                                top = 3,
                                bottom = 3
                            }
                        };
                    }

                    return _Tab;
                }
            }

            private static GUIStyle s_CenterBoldLabel;

            public static GUIStyle CenterBoldLabel
            {
                get
                {
                    if (s_CenterBoldLabel == null)
                    {
                        s_CenterBoldLabel = new GUIStyle(EditorStyles.largeLabel);
                        s_CenterBoldLabel.alignment = TextAnchor.UpperCenter;
                        s_CenterBoldLabel.padding = new RectOffset();
                        s_CenterBoldLabel.fontStyle = FontStyle.Bold;
                    }

                    return s_CenterBoldLabel;
                }
            }

            private static GUIStyle s_AddOnTitle;

            private static GUIStyle AddOnTitle
            {
                get
                {
                    if (s_AddOnTitle == null)
                    {
                        s_AddOnTitle = new GUIStyle(CenterBoldLabel);
                        s_AddOnTitle.fontSize = 14;
                        s_AddOnTitle.alignment = TextAnchor.MiddleLeft;
                    }

                    return s_AddOnTitle;
                }
            }
        }

        public static GUIContent TargetPlatform()
        {
            GUIContent c = new GUIContent();

            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.Android:
                    {
                        c.text = "Android";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.Android.Small").image;
                    }
                    break;
                case BuildTarget.iOS:
                    {
                        c.text = "iOS";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.iPhone.Small").image;
                    }
                    break;
                case BuildTarget.tvOS:
                {
                    c.text = "tvOS";
                    c.image = EditorGUIUtility.IconContent("BuildSettings.iPhone.Small").image;
                }
                    break;
                case BuildTarget.PS4:
                    {
                        c.text = "Playstation 4";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.PS4.Small").image;
                    }
                    break;
                case BuildTarget.PS5:
                    {
                        c.text = "Playstation 5";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.PS5.Small").image;
                    }
                    break;
                case BuildTarget.StandaloneOSX:
                    {
                        c.text = "MacOS";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.Standalone.Small").image;
                    }
                    break;
                case BuildTarget.StandaloneWindows:
                    {
                        c.text = "Windows";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.Metro.Small").image;
                    }
                    break;
                case BuildTarget.StandaloneWindows64:
                    {
                        c.text = "Windows";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.Metro.Small").image;
                    }
                    break;
                case BuildTarget.Switch:
                    {
                        c.text = "Nintendo Switch 1";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.Switch.Small").image;
                    }
                    break;
                #if UNITY_6000_2_OR_NEWER
                case BuildTarget.Switch2:
                {
                    c.text = "Nintendo Switch 2";
                    c.image = EditorGUIUtility.IconContent("BuildSettings.Switch.Small").image;
                }
                    break;
                #endif
                case BuildTarget.XboxOne:
                    {
                        c.text = "Xbox One";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.XboxOne.Small").image;
                    }
                    break;
                case BuildTarget.WebGL:
                    {
                        c.text = "Web";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.Web.Small").image;
                    }
                    break;
                case BuildTarget.WSAPlayer:
                    {
                        c.text = "Universal Windows Platform";
                        c.image = EditorGUIUtility.IconContent("BuildSettings.WP8.Small").image;
                    }
                    break;
                default:
                    {
                        c.text = "Unknown";
                        c.image = EditorGUIUtility.IconContent("console.erroricon.sml").image;
                    }
                    break;
            }

            return c;
        }
    }
}