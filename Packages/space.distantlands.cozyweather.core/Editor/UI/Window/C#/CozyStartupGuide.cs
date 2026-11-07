//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;

namespace DistantLands.Cozy.EditorScripts
{
    [InitializeOnLoad]
    public class CozyStartupGuide : EditorWindow
    {
        private static CozyStartupGuide wnd;
        private static readonly HttpClient client = new HttpClient();
        VisualElement root;

        static CozyStartupGuide()
        {
            EditorApplication.delayCall += OpenOnFirstInstall;
        }

        private static void OpenOnFirstInstall()
        {
            if (!EditorPrefs.GetBool("CZY_SetupWindowOpened", false) || EditorPrefs.GetInt("CZY_SetupWindowPreference", 0) == 0 || (EditorPrefs.GetInt("CZY_SetupWindowPreference", 0) == 1) && VersionChecking.VersionStatus == -1)
            {
                ShowWindow();

                EditorPrefs.SetBool("CZY_SetupWindowOpened", true);
            }
        }

        [MenuItem("Tools/COZY: Weather 4/Start Screen", false, 1000)]
        public static void Init()
        {
            ShowWindow();
        }

        private static void ShowWindow()
        {
            wnd = (CozyStartupGuide)GetWindow(typeof(CozyStartupGuide), true, "COZY Setup Guide");
            wnd.minSize = new Vector2(650, 500);
            wnd.maxSize = new Vector2(650, 500);
            wnd.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            root = rootVisualElement;

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Window/UXML/StartupGuide.uxml"
            );

            asset.CloneTree(root);

            var dropdown = root.Q<DropdownField>();

            dropdown.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetInt("CZY_SetupWindowPreference", dropdown.index);
            });


            var tabContainer = root.Q<VisualElement>("tabContainer");
            var contentContainer = root.Q<VisualElement>("contentContainer");

            var welcomeTab = tabContainer.Q<Button>("welcome");
            var gettingStartedTab = tabContainer.Q<Button>("gettingStarted");
            var supportTab = tabContainer.Q<Button>("support");
            var samples = tabContainer.Q<Button>("samples");
            var documentation = root.Query<Button>("documentation");
            var tutorials = root.Query<Button>("tutorials");
            var review = tabContainer.Q<Button>("review");

            var welcomeContent = contentContainer.Q<VisualElement>("welcome");
            var gettingStartedContent = contentContainer.Q<VisualElement>("gettingStarted");
            var supportContent = contentContainer.Q<VisualElement>("support");

            var projectSetup = gettingStartedContent.Q<Button>("projectSetup");
            var sceneSetup = gettingStartedContent.Q<Button>("sceneSetup");
            var preferences = gettingStartedContent.Q<Button>("preferences");

            var discord = supportContent.Q<Button>("discord");
            var email = supportContent.Q<Button>("email");
            var faqs = supportContent.Q<Button>("faqs");

            welcomeTab.clicked += () =>
            {
                welcomeTab.AddToClassList("active");
                gettingStartedTab.RemoveFromClassList("active");
                supportTab.RemoveFromClassList("active");

                welcomeContent.style.display = DisplayStyle.Flex;
                gettingStartedContent.style.display = DisplayStyle.None;
                supportContent.style.display = DisplayStyle.None;
            };
            gettingStartedTab.clicked += () =>
            {
                welcomeTab.RemoveFromClassList("active");
                gettingStartedTab.AddToClassList("active");
                supportTab.RemoveFromClassList("active");

                welcomeContent.style.display = DisplayStyle.None;
                gettingStartedContent.style.display = DisplayStyle.Flex;
                supportContent.style.display = DisplayStyle.None;
            };
            supportTab.clicked += () =>
            {
                welcomeTab.RemoveFromClassList("active");
                gettingStartedTab.RemoveFromClassList("active");
                supportTab.AddToClassList("active");

                welcomeContent.style.display = DisplayStyle.None;
                gettingStartedContent.style.display = DisplayStyle.None;
                supportContent.style.display = DisplayStyle.Flex;
            };
            samples.clicked += () =>
            {
                string importedPath = "Assets/Samples/COZY: Stylized Weather";

                if (Directory.Exists(importedPath))
                {
                    UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(importedPath);
                    if (folder != null)
                    {
                        Selection.activeObject = folder;
                        EditorGUIUtility.PingObject(folder);
                        return;
                    }
                }

                UnityEditor.PackageManager.UI.Window.Open("space.distantlands.cozyweather.core");
            };
            documentation.ForEach(x => x.clicked += () =>
            {
                Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4");
            });
            tutorials.ForEach(x => x.clicked += () =>
            {
                Application.OpenURL("https://www.youtube.com/playlist?list=PLLEIdf9CiVNU");
            });
            review.clicked += () =>
            {
                Application.OpenURL("https://assetstore.unity.com/packages/vfx/cozy-weather-4-389284#reviews");
            };

            dropdown.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetInt("CZY_SetupWindowPreference", dropdown.index);
            });

            projectSetup.clicked += () =>
            {
                CozyProjectSetup.ShowSetupWizard();
            };

            sceneSetup.enabledSelf = !CozyWeather.Instance;

            sceneSetup.clicked += () =>
            {
                if (!CozyWeather.Instance)
                    CozySceneSetupHelper.SetupSceneBasicModules();
            };

            preferences.clicked += () =>
            {
                SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Runtime");
            };

            faqs.clicked += () =>
            {
                Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4/appendix/faqs");
            };

            discord.clicked += () =>
            {
                Application.OpenURL("https://discord.gg/HWGU4hj");
            };

            email.clicked += () =>
            {
                Application.OpenURL("mailto:contact@distantlands.space");
            };

            _ = FetchAndDisplayReleaseNotes();
        }

        private async Task FetchAndDisplayReleaseNotes()
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Grab-Release-Notes");

            string rawUrl = "https://raw.githubusercontent.com/DistantLandsProductions/cozy-documentation/main/release-notes.md";

            try
            {
                string markdownContent = await client.GetStringAsync(rawUrl);

                VisualElement releaseNotesContainer = root.Q<VisualElement>("releaseNotes");
                releaseNotesContainer.Clear();

                ParseMarkdownToUI(markdownContent, releaseNotesContainer);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Error fetching file: {e.Message}");
            }
        }

        private void ParseMarkdownToUI(string markdown, VisualElement targetContainer)
        {
            using (StringReader reader = new StringReader(markdown))
            {
                string line;
                bool inFrontmatter = false;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line == "---")
                    {
                        inFrontmatter = !inFrontmatter;
                        continue;
                    }
                    if (inFrontmatter) continue;

                    if (line.StartsWith("# "))
                    {
                        Label h1 = new Label(line.Substring(2));
                        h1.AddToClassList("h1");
                        targetContainer.Add(h1);
                    }
                    else if (line.StartsWith("## "))
                    {
                        string versionTitle = line.Substring(3);
                        if (line.Substring(3).Contains(VersionChecking.InstalledVersion))
                            versionTitle += " (Installed)";
                        Label h2 = new Label(versionTitle);
                        h2.AddToClassList("h2");
                        targetContainer.Add(h2);
                    }
                    else if (line.StartsWith("### "))
                    {
                        Label h3 = new Label(line.Substring(4));
                        h3.AddToClassList("h3");
                        targetContainer.Add(h3);
                    }

                    else if (line.StartsWith("- ") || line.StartsWith("* "))
                    {
                        Label bullet = new Label($"•  {line.Substring(2)}");
                        // bullet.AddToClassList("bullet");
                        targetContainer.Add(bullet);
                    }

                    else
                    {
                        Label body = new Label(line);
                        targetContainer.Add(body);
                    }
                }
            }
        }
    }
}