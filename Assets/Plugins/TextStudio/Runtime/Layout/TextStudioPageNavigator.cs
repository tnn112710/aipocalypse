using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace TextStudio
{
    /// <summary>Controls TextMeshPro page overflow manually or follows the page containing the latest revealed glyph.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Text Studio/Layout/Page Navigator")]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    public sealed partial class TextStudioPageNavigator : MonoBehaviour
    {
        [SerializeField] TextStudioText _textStudio;
        [SerializeField] TMP_Text _tmpText;
        [SerializeField] bool _followReveal;
        [SerializeField] UnityEvent<int, int> _onPageChanged = new UnityEvent<int, int>();

        int _currentPage = 1;
        int _pageCount = 1;
        int _lastRevealedCount = -1;
        int _observedPlaySession = -1;
        bool _needsRefresh = true;
        bool _subscribedToTmpChanges;
        bool _isRefreshing;
        bool _manualPageOverride;
        static int s_playSession = 0;

        public TextStudioText Target
        {
            get => _textStudio;
            set
            {
                if (_textStudio == value)
                    return;

                _textStudio = value;
                _lastRevealedCount = -1;
                _manualPageOverride = false;
                _needsRefresh = true;
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
                _manualPageOverride = false;
                _needsRefresh = true;
            }
        }

        public bool FollowReveal
        {
            get => _followReveal;
            set
            {
                if (_followReveal == value)
                {
                    if (value)
                        _manualPageOverride = false;
                    return;
                }

                _followReveal = value;
                _manualPageOverride = false;
                if (_followReveal)
                    _lastRevealedCount = _textStudio != null
                        ? _textStudio.RevealedGlyphCount
                        : -1;
            }
        }

        public int CurrentPage => _currentPage;
        public int PageCount => _pageCount;
        public UnityEvent<int, int> OnPageChanged => _onPageChanged;

        // Subsystem registration runs for every Play session, including fast-enter sessions without domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BeginPlaySession()
        {
            s_playSession++;
        }

        void Reset()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            SubscribeToTmpChanges();
            _manualPageOverride = false;
            _lastRevealedCount = -1;
            _observedPlaySession = s_playSession;
            if (Application.isPlaying)
                ResetForPlayback();
            else
                Refresh();
        }

        void Start()
        {
            if (!Application.isPlaying)
                return;

            _observedPlaySession = s_playSession;
            ResetForPlayback();
        }

        void OnDisable()
        {
            UnsubscribeFromTmpChanges();
        }

        void OnValidate()
        {
            _pageCount = Mathf.Max(1, _pageCount);
            _currentPage = Mathf.Clamp(_currentPage, 1, _pageCount);
            _manualPageOverride = false;
            _needsRefresh = true;
        }

        void LateUpdate()
        {
            ResolveReferences();

            if (Application.isPlaying && _observedPlaySession != s_playSession)
            {
                _observedPlaySession = s_playSession;
                ResetForPlayback();
                return;
            }

            if (_needsRefresh)
                Refresh();

            if (!_followReveal || _textStudio == null)
                return;

            int revealed = _textStudio.RevealedGlyphCount;
            if (revealed == _lastRevealedCount)
                return;

            bool revealRestarted = _manualPageOverride &&
                                   _lastRevealedCount >= 0 &&
                                   revealed < _lastRevealedCount;
            _lastRevealedCount = revealed;
            if (_manualPageOverride && !revealRestarted)
                return;

            _manualPageOverride = false;
            FollowRevealNow();
        }

        /// <summary>Assigns the playback host and paginated TextMeshPro output, then refreshes page state.</summary>
        public void SetTargets(TextStudioText textStudio, TMP_Text tmpText)
        {
            Target = textStudio;
            TMPText = tmpText;
            Refresh();
        }

        /// <summary>Forces current text layout and updates the valid 1-based page range.</summary>
        public void Refresh()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;
            try
            {
                ResolveReferences();

                if (!TryForceMeshUpdate(_tmpText))
                {
                    _needsRefresh = false;
                    SetPageState(1, 1);
                    return;
                }

                int pageCount = Mathf.Max(1, _tmpText.textInfo.pageCount);
                _needsRefresh = false;
                SetPageState(Mathf.Clamp(_currentPage, 1, pageCount), pageCount);
                // Rebaseline after the layout pass so page display refreshes do not look like reveal progress.
                if (_followReveal && _textStudio != null)
                    _lastRevealedCount = _textStudio.RevealedGlyphCount;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        /// <summary>Moves to the next page, clamped to the current page count.</summary>
        public void NextPage()
        {
            GoToPage(_currentPage + 1);
        }

        /// <summary>Moves to the previous page, clamped to page one.</summary>
        public void PreviousPage()
        {
            GoToPage(_currentPage - 1);
        }

        /// <summary>Displays the first page.</summary>
        public void FirstPage()
        {
            GoToPage(1);
        }

        /// <summary>Displays the last currently known page.</summary>
        public void LastPage()
        {
            GoToPage(_pageCount);
        }

        /// <summary>Displays a 1-based page number clamped to the current layout.</summary>
        public void GoToPage(int page)
        {
            GoToPage(page, true);
        }

        void GoToPage(int page, bool manualSelection)
        {
            int pageCount = Mathf.Max(1, _pageCount);
            if (manualSelection && _followReveal)
                _manualPageOverride = true;

            SetPageState(Mathf.Clamp(page, 1, pageCount), pageCount);
            if (_followReveal && _textStudio != null)
                _lastRevealedCount = _textStudio.RevealedGlyphCount;
        }

        /// <summary>Displays the page containing the most recently revealed semantic glyph.</summary>
        /// <returns><see langword="false"/> when required targets or glyph mapping are unavailable.</returns>
        public bool FollowRevealNow()
        {
            _manualPageOverride = false;
            ResolveReferences();

            if (_textStudio == null || _tmpText == null)
                return false;

            if (_needsRefresh)
                Refresh();

            TMP_TextInfo textInfo = _tmpText.textInfo;
            if (textInfo == null || textInfo.characterCount <= 0 || _textStudio.TotalGlyphCount <= 0)
            {
                GoToPage(1, false);
                return true;
            }

            int maxGlyphIndex = _textStudio.TotalGlyphCount - 1;
            int glyphIndex = Mathf.Clamp(_textStudio.RevealedGlyphCount - 1, 0, Mathf.Max(0, maxGlyphIndex));
            TextStudioPlanView plan = _textStudio.Plan;
            int characterInfoIndex = plan.IsCreated
                ? TextStudioTMPCharacterLookup.FindCharacterInfoIndex(plan, textInfo, glyphIndex)
                : glyphIndex;
            if (characterInfoIndex < 0 || characterInfoIndex >= textInfo.characterCount)
                return false;

            ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[characterInfoIndex];
            GoToPage(character.pageNumber + 1, false);
            return true;
        }

        void SetPageState(int page, int pageCount)
        {
            int safePageCount = Mathf.Max(1, pageCount);
            int safePage = Mathf.Clamp(page, 1, safePageCount);

            if (_tmpText != null)
                _tmpText.pageToDisplay = safePage;

            bool changed = _currentPage != safePage || _pageCount != safePageCount;
            _currentPage = safePage;
            _pageCount = safePageCount;

            if (changed)
            {
                _onPageChanged.Invoke(_currentPage, _pageCount);
                _textStudio?.ScheduleLayoutRebuild();
            }
        }

        void ResetPageForPlayback()
        {
            ResolveReferences();
            _currentPage = 1;
            _manualPageOverride = false;
            _lastRevealedCount = _textStudio != null
                ? _textStudio.RevealedGlyphCount
                : -1;
            _needsRefresh = true;

            if (_tmpText != null)
                _tmpText.pageToDisplay = 1;
        }

        internal void ResetForPlayback()
        {
            _observedPlaySession = s_playSession;
            ResetPageForPlayback();
            Refresh();
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
            {
                if (!_isRefreshing)
                    _lastRevealedCount = -1;
                _needsRefresh = true;
            }
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
