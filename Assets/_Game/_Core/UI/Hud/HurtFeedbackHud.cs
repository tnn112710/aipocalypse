using System;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MessagePipe;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // Hits the active player takes (DamageDealt to the player): the screen edge toward the attacker flashes and fades, top for
    // in front, bottom for behind. Hits with no direction (a fall, the player's own position) light every edge.
    public sealed class HurtFeedbackHud : PlayerHudView
    {
        const int EdgeCount = 4;
        const int GradientLength = 64;
        // Metres: a source closer than this on the ground plane gives no direction.
        const float MinSourceDistance = 0.3f;
        const float MinHeading = 1e-6f;

        [SerializeField, Tooltip("Hits from in front")] RawImage _top;
        [SerializeField, Tooltip("Hits from behind")] RawImage _bottom;
        [SerializeField] RawImage _left;
        [SerializeField] RawImage _right;
        [SerializeField, InlineEditor, Tooltip("Color, strength, thickness and fade of the flash")] HurtFeedbackStyle _style;

        readonly float[] _peak = new float[EdgeCount];
        readonly float[] _elapsed = new float[EdgeCount];
        RawImage[] _edges;
        Texture2D _vertical;
        Texture2D _horizontal;
        TimeService _time;
        IDisposable _subscription;
        float _laidOutThickness = -1f;

        float HudHeight => ((RectTransform)transform).rect.height;

        [Inject]
        void Construct(TimeService time, ISubscriber<DamageDealt> dealt)
        {
            _time = time;
            _subscription = dealt.Subscribe(OnDamageDealt);
        }

        void Awake()
        {
            if (_style == null)
            {
                Debug.LogWarning($"{name}: no HurtFeedbackStyle assigned; using defaults.", this);
                _style = ScriptableObject.CreateInstance<HurtFeedbackStyle>();
            }
            _edges = new[] { _top, _bottom, _left, _right };
            _vertical = Gradient(1, GradientLength);
            _horizontal = Gradient(GradientLength, 1);
            _top.texture = _vertical;
            _bottom.texture = _vertical;
            _left.texture = _horizontal;
            _right.texture = _horizontal;
            ApplyStyle();
            HideAll();
        }

        protected override void Unbind(IPlayer player) => HideAll();

        protected override void OnDestroy()
        {
            _subscription?.Dispose();
            base.OnDestroy();
            Destroy(_vertical);
            Destroy(_horizontal);
        }

        // Real time, so a hit stop doesn't hold the flash; frozen while paused. Turns itself off once every edge has faded.
        void Update()
        {
            if (_time != null && _time.IsPaused)
                return;

            float deltaTime = Time.unscaledDeltaTime;
            bool showing = false;
            for (int i = 0; i < EdgeCount; i++)
            {
                if (_peak[i] <= 0f)
                    continue;
                _elapsed[i] += deltaTime;
                float alpha = Alpha(i);
                if (alpha <= 0f)
                {
                    Hide(i);
                    continue;
                }
                _edges[i].canvasRenderer.SetAlpha(alpha);
                showing = true;
            }
            if (!showing)
                enabled = false;
        }

        public void ApplyStyle() => ApplyStyle(HudHeight);

        // Anchors each edge to its screen side, tints it and sizes it for a HUD this tall. AgentScripts/Hud/HurtFeedbackSetup
        // runs it on the prefab, whose HUD has no size, with the CanvasScaler's reference height.
        public void ApplyStyle(float height)
        {
            if (_style == null)
                return;
            Place(_top, new Vector2(0f, 1f), Vector2.one, new Rect(0f, 1f, 1f, -1f));
            Place(_bottom, Vector2.zero, new Vector2(1f, 0f), new Rect(0f, 0f, 1f, 1f));
            Place(_left, Vector2.zero, new Vector2(0f, 1f), new Rect(0f, 0f, 1f, 1f));
            Place(_right, new Vector2(1f, 0f), Vector2.one, new Rect(1f, 0f, -1f, 1f));
            _laidOutThickness = -1f;
            Resize(height);
        }

        // Share of a hit's flash per edge (x top / front, y bottom / back, z left, w right), from the direction to its source
        // against the view's heading on the ground plane. A source too close to tell, or no heading, lights every edge.
        public static Vector4 EdgeWeights(Vector3 toSource, Vector3 viewForward)
        {
            var toward = new Vector2(toSource.x, toSource.z);
            var ahead = new Vector2(viewForward.x, viewForward.z);
            if (toward.sqrMagnitude < MinSourceDistance * MinSourceDistance || ahead.sqrMagnitude < MinHeading)
                return Vector4.one;

            toward.Normalize();
            ahead.Normalize();
            float front = Vector2.Dot(toward, ahead);
            float right = toward.x * ahead.y - toward.y * ahead.x;
            return new Vector4(Mathf.Max(0f, front), Mathf.Max(0f, -front), Mathf.Max(0f, -right), Mathf.Max(0f, right));
        }

        void OnDamageDealt(DamageDealt hit)
        {
            IPlayer player = Player;
            if (player == null || !ReferenceEquals(hit.Target, player))
                return;

            Transform view = player.View != null ? player.View : player.Transform;
            GameObject source = hit.Info.Source;
            Vector4 weights = Vector4.one;
            // The player as its own source (a fall) has no direction; a hit from the world comes from its point.
            if (source != player.Transform.gameObject)
            {
                Vector3 from = source != null ? source.transform.position : hit.Info.Point;
                weights = EdgeWeights(from - view.position, view.forward);
            }

            float maxHealth = player.Health.Max;
            float share = maxHealth > 0f ? hit.Result.Applied / maxHealth : 1f;
            float strength = Mathf.Max(_style.MinStrength, Mathf.Clamp01(share / _style.FullStrengthShare));
            Flash(weights * (strength * _style.MaxAlpha));
        }

        // An edge keeps the brighter of its current and the new flash, so a light hit never cuts a strong one short and
        // several hits in one frame (pellets) count once. Size and color are re-read per hit, so style edits show in Play mode.
        void Flash(Vector4 alphas)
        {
            Resize(HudHeight);
            Color color = _style.FlashColor;
            for (int i = 0; i < EdgeCount; i++)
            {
                float alpha = alphas[i];
                if (alpha <= 0f || alpha <= Alpha(i))
                    continue;
                _peak[i] = alpha;
                _elapsed[i] = 0f;
                RawImage edge = _edges[i];
                edge.color = color;
                edge.enabled = true;
                edge.canvasRenderer.SetAlpha(alpha);
                enabled = true;
            }
        }

        // Fades with an ease-in: stays bright, then drops.
        float Alpha(int edge)
        {
            float progress = _elapsed[edge] / _style.FadeOut;
            return progress >= 1f ? 0f : _peak[edge] * (1f - progress * progress);
        }

        void Hide(int edge)
        {
            _peak[edge] = 0f;
            _elapsed[edge] = 0f;
            if (_edges[edge] != null)
                _edges[edge].enabled = false;
        }

        void HideAll()
        {
            if (_edges == null)
                return;
            for (int i = 0; i < EdgeCount; i++)
                Hide(i);
        }

        // Pivot on the screen side; uv flips the gradient so its opaque end sits on the screen edge.
        void Place(RawImage edge, Vector2 anchorMin, Vector2 anchorMax, Rect uv)
        {
            RectTransform rect = edge.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = (anchorMin + anchorMax) * 0.5f;
            rect.anchoredPosition = Vector2.zero;
            edge.uvRect = uv;
            edge.color = _style.FlashColor;
        }

        // Every edge is as thick as a share of the HUD's height; redone when that changes (resolution or style). A HUD with no
        // height yet (not laid out, or the prefab) keeps its current size.
        void Resize(float height)
        {
            if (height <= 0f)
                return;
            float thickness = _style.Thickness * height;
            if (Mathf.Approximately(thickness, _laidOutThickness))
                return;
            _laidOutThickness = thickness;
            _top.rectTransform.sizeDelta = new Vector2(0f, thickness);
            _bottom.rectTransform.sizeDelta = new Vector2(0f, thickness);
            _left.rectTransform.sizeDelta = new Vector2(thickness, 0f);
            _right.rectTransform.sizeDelta = new Vector2(thickness, 0f);
        }

        // White, opaque at texel 0 and clear at the far end (squared falloff). Made once, never saved.
        static Texture2D Gradient(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "HurtFeedbackGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                float fade = 1f - i / (pixels.Length - 1f);
                pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(fade * fade * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
