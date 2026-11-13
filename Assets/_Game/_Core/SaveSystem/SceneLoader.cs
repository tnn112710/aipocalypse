using System;
using Cysharp.Threading.Tasks;
using MB13.Aipocalypse.Core.LocalizationSystem;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Root: the only place that loads scenes. Covers the switch with a black loading screen, kept up until the new scene has
    // restored its save (its first frames), so the player never sees the fresh scene before the loaded one.
    public sealed class SceneLoader : IDisposable
    {
        // Frames after the load: the new scene's entry points start and WorldSaver restores on the first.
        const int SettleFrames = 2;

        readonly SaveSettings _settings;
        GameObject _screen;
        TMP_Text _screenText;

        public SceneLoader(SaveSettings settings) => _settings = settings;

        public bool IsLoading { get; private set; }

        // False when the scene isn't in the Build Settings (or a load is already running). `failed` runs when the load
        // throws part-way (the old scene then keeps running).
        public bool Load(string scene, Action failed = null)
        {
            if (IsLoading)
                return false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"Scene '{scene}' is not in the Build Settings; can't load it.");
                return false;
            }
            LoadAsync(scene, failed).Forget();
            return true;
        }

        public void Dispose()
        {
            if (_screen != null)
                Object.Destroy(_screen);
        }

        async UniTaskVoid LoadAsync(string scene, Action failed)
        {
            IsLoading = true;
            ShowScreen(true);
            try
            {
                // One frame so the black screen is drawn before the load stalls the main thread.
                await UniTask.Yield();
                await SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                await UniTask.DelayFrame(SettleFrames);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                failed?.Invoke();
            }
            finally
            {
                IsLoading = false;
                ShowScreen(false);
            }
        }

        void ShowScreen(bool show)
        {
            if (_screen == null)
            {
                if (!show)
                    return;
                _screen = BuildScreen();
            }
            if (show)
                _screenText.text = _settings.LoadingText.Text();
            _screen.SetActive(show);
        }

        // Built in code: a black overlay above everything, with one line of text.
        GameObject BuildScreen()
        {
            var root = new GameObject("LoadingScreen", typeof(Canvas), typeof(CanvasScaler));
            Object.DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            Stretch((RectTransform)background.transform);
            background.GetComponent<Image>().color = Color.black;

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-60f, 50f);
            rect.sizeDelta = new Vector2(600f, 60f);
            var text = label.GetComponent<TextMeshProUGUI>();
            if (_settings.LoadingFont != null)
                text.font = _settings.LoadingFont;
            text.fontSize = 36f;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.color = new Color(1f, 1f, 1f, 0.85f);
            text.raycastTarget = false;
            _screenText = text;
            return root;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
