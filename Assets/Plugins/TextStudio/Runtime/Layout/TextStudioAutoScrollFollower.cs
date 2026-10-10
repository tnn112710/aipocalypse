using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio
{
    /// <summary>Keeps a ScrollRect positioned near the most recently revealed TextMeshPro glyph.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Text Studio/Layout/Auto Scroll Follower")]
    public sealed class TextStudioAutoScrollFollower : MonoBehaviour
    {
        [SerializeField] TextStudioText _textStudio;
        [SerializeField] TMP_Text _tmpText;
        [SerializeField] ScrollRect _scrollRect;
        [SerializeField, Range(0f, 1f)] float _padding = 0.12f;

        int _lastRevealedCount = -1;
        bool _needsMeshRefresh = true;
        bool _subscribedToTmpChanges;

        public TextStudioText Target
        {
            get => _textStudio;
            set
            {
                if (_textStudio == value)
                    return;

                _textStudio = value;
                _lastRevealedCount = -1;
            }
        }

        public TMP_Text TMPText
        {
            get => _tmpText;
            set
            {
                if (_tmpText == value)
                    return;

                UnsubscribeFromTmpChanges();
                _tmpText = value;
                SubscribeToTmpChanges();
                _needsMeshRefresh = true;
            }
        }

        public ScrollRect ScrollRect
        {
            get => _scrollRect;
            set => _scrollRect = value;
        }

        public float Padding
        {
            get => _padding;
            set => _padding = Mathf.Clamp01(value);
        }

        void Reset()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            SubscribeToTmpChanges();
            _lastRevealedCount = -1;
            _needsMeshRefresh = true;
        }

        void OnDisable()
        {
            UnsubscribeFromTmpChanges();
        }

        void OnValidate()
        {
            _padding = Mathf.Clamp01(_padding);
            _needsMeshRefresh = true;
        }

        void LateUpdate()
        {
            ResolveReferences();

            if (_textStudio == null)
                return;

            int revealed = _textStudio.RevealedGlyphCount;
            if (!_needsMeshRefresh && revealed == _lastRevealedCount)
                return;

            _lastRevealedCount = revealed;
            FollowNow();
        }

        /// <summary>Assigns playback, rendered text, and scroll targets for subsequent follow updates.</summary>
        public void SetTargets(TextStudioText textStudio, TMP_Text tmpText, ScrollRect scrollRect)
        {
            Target = textStudio;
            TMPText = tmpText;
            ScrollRect = scrollRect;
            _needsMeshRefresh = true;
        }

        /// <summary>Scrolls immediately to keep the latest revealed glyph inside the padded viewport.</summary>
        /// <returns><see langword="false"/> when targets, layout, or a scrollable range are unavailable.</returns>
        public bool FollowNow()
        {
            ResolveReferences();

            if (_textStudio == null || _tmpText == null || _scrollRect == null)
                return false;

            if (_needsMeshRefresh)
                RefreshMesh();

            int glyphIndex = _textStudio.RevealedGlyphCount - 1;
            if (!TryGetGlyphContentLocalPosition(glyphIndex, out Vector3 localPosition))
                return false;

            RectTransform content = _scrollRect.content;
            RectTransform viewport = _scrollRect.viewport != null ? _scrollRect.viewport : _scrollRect.GetComponent<RectTransform>();
            if (content == null || viewport == null)
                return false;

            float contentHeight = Mathf.Max(1f, content.rect.height);
            float viewportHeight = Mathf.Max(1f, viewport.rect.height);
            float scrollableHeight = contentHeight - viewportHeight;
            if (scrollableHeight <= 0f)
                return false;

            float offsetFromTop = content.rect.yMax - localPosition.y;
            float desiredScroll = Mathf.Clamp(offsetFromTop - viewportHeight * _padding, 0f, scrollableHeight);
            _scrollRect.verticalNormalizedPosition = 1f - desiredScroll / scrollableHeight;
            return true;
        }

        /// <summary>Forces TextMeshPro layout before the next reveal-position lookup.</summary>
        public void RefreshMesh()
        {
            _needsMeshRefresh = false;
            TryForceMeshUpdate(_tmpText);
        }

        bool TryGetGlyphContentLocalPosition(int glyphIndex, out Vector3 localPosition)
        {
            localPosition = default;

            if (glyphIndex < 0 || _tmpText == null || _scrollRect == null || _scrollRect.content == null)
                return false;

            TMP_TextInfo textInfo = _tmpText.textInfo;
            TextStudioPlanView plan = _textStudio != null ? _textStudio.Plan : default;
            int characterInfoIndex = plan.IsCreated
                ? TextStudioTMPCharacterLookup.FindCharacterInfoIndex(plan, textInfo, glyphIndex)
                : glyphIndex;
            if (textInfo == null || characterInfoIndex < 0 || characterInfoIndex >= textInfo.characterCount)
                return false;

            ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[characterInfoIndex];
            Vector3 glyphLocal = new Vector3((character.bottomLeft.x + character.topRight.x) * 0.5f, character.baseLine, 0f);
            Vector3 glyphWorld = _tmpText.transform.TransformPoint(glyphLocal);
            localPosition = _scrollRect.content.InverseTransformPoint(glyphWorld);
            return true;
        }

        void ResolveReferences()
        {
            if (_textStudio == null)
                _textStudio = GetComponent<TextStudioText>();

            if (_tmpText == null)
            {
                _tmpText = GetComponent<TMP_Text>();
                SubscribeToTmpChanges();
            }

            if (_scrollRect == null)
                _scrollRect = GetComponentInParent<ScrollRect>();
        }

        void SubscribeToTmpChanges()
        {
            if (_subscribedToTmpChanges || _tmpText == null)
                return;

            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTmpTextChanged);
            _subscribedToTmpChanges = true;
        }

        void UnsubscribeFromTmpChanges()
        {
            if (!_subscribedToTmpChanges)
                return;

            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTmpTextChanged);
            _subscribedToTmpChanges = false;
        }

        void HandleTmpTextChanged(UnityEngine.Object changedObject)
        {
            if (_tmpText != null && ReferenceEquals(changedObject, _tmpText))
                _needsMeshRefresh = true;
        }

        static bool TryForceMeshUpdate(TMP_Text tmpText)
        {
            if (tmpText == null)
                return false;

            try
            {
                tmpText.ForceMeshUpdate();
                return tmpText.textInfo != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }
    }
}
