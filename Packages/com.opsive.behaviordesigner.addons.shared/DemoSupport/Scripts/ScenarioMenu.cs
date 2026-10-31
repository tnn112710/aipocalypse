/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Demo
{
    using System.Collections.Generic;
    using Opsive.BehaviorDesigner.Runtime;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>Displays the shared add-on sidebar and controls the demo session.</summary>
    public class ScenarioMenu : MonoBehaviour
    {
        [Tooltip("The scenario controller.")] [SerializeField] private ScenarioSelector m_Selector;
        [Tooltip("The scenario selection buttons.")] [SerializeField] private Button[] m_Buttons;
        [Tooltip("The scrolling scenario list.")] [SerializeField] private ScrollRect m_List;
        [Tooltip("The pause button label.")] [SerializeField] private Text m_PauseLabel;
        [Tooltip("The arena status label.")] [SerializeField] private Text m_Status;
        [Tooltip("The optional keyboard instructions.")] [SerializeField] private Text m_KeyboardHint;
        private readonly List<BehaviorTree> m_PausedTrees = new List<BehaviorTree>();
        private int m_DisplayedIndex = -1;
        private bool m_Paused;
        private float m_PreviousTimeScale;
        private static System.Func<bool, int> s_ReadNavigation;

        /// <summary>Registers the Input System keyboard reader.</summary><param name="reader">Reads a direction, allowing WASD only when requested.</param>
        public static void RegisterNavigationInput(System.Func<bool, int> reader) { s_ReadNavigation = reader; }

        /// <summary>Gets whether the demo is paused.</summary>
        public bool IsPaused => m_Paused;

        /// <summary>Updates selection feedback and immediate keyboard navigation.</summary>
        private void Update()
        {
            if (s_ReadNavigation != null) {
                var direction = s_ReadNavigation(!m_Selector.PlayerIsActive);
                Navigate(direction < 0, direction > 0);
            }
            if (m_DisplayedIndex != m_Selector.ActiveIndex) RefreshSelection();
            var tree = m_Selector.AgentBehaviorTrees[0][m_Selector.ActiveIndex];
            m_Status.text = tree.Name + " / " + (m_Paused ? "Paused" : tree.Status.ToString());
        }

        /// <summary>Changes scenarios on a single directional press.</summary>
        /// <param name="previous">Whether a previous key was pressed.</param>
        /// <param name="next">Whether a next key was pressed.</param>
        public void Navigate(bool previous, bool next)
        {
            if (previous == next) return;
            Resume();
            m_Selector.ChangeScenario(next);
            RefreshSelection();
        }

        /// <summary>Selects the specified scenario immediately.</summary><param name="index">The scenario index.</param>
        public void SelectScenario(int index)
        {
            Resume();
            m_Selector.SelectScenario(index);
            RefreshSelection();
        }

        /// <summary>Restarts the selected scenario.</summary>
        public void Restart() { Resume(); m_Selector.ResetScenario(); RefreshSelection(); }

        /// <summary>Pauses behavior trees and scaled demo motion, or resumes the session.</summary>
        public void TogglePause()
        {
            if (m_Paused) { Resume(); return; }
            m_PreviousTimeScale = Time.timeScale;
            m_Paused = true;
            foreach (var tree in FindObjectsOfType<BehaviorTree>()) {
                if (tree.gameObject.scene != gameObject.scene || !tree.IsActive() || tree.IsPaused()) continue;
                if (tree.StopBehavior(true)) m_PausedTrees.Add(tree);
            }
            Time.timeScale = 0;
            m_PauseLabel.text = "Resume";
        }

        /// <summary>Restores only the behavior trees and clock paused by this menu.</summary>
        private void Resume()
        {
            if (!m_Paused) return;
            Time.timeScale = m_PreviousTimeScale;
            foreach (var tree in m_PausedTrees) {
                if (tree != null && tree.isActiveAndEnabled && tree.IsPaused()) tree.StartBehavior();
            }
            m_PausedTrees.Clear();
            m_Paused = false;
            m_PauseLabel.text = "Pause";
        }

        /// <summary>Restores the session clock when leaving the demo.</summary>
        private void OnDisable() { Resume(); }

        /// <summary>Highlights the active row and scrolls it into view.</summary>
        private void RefreshSelection()
        {
            m_DisplayedIndex = m_Selector.ActiveIndex;
            for (int i = 0; i < m_Buttons.Length; ++i) {
                m_Buttons[i].image.color = i == m_DisplayedIndex ? new Color(0.28627452f, 0.50980395f, 0.85882354f) : new Color(0.13f, 0.18f, 0.24f);
            }
            Canvas.ForceUpdateCanvases();
            var row = (RectTransform)m_Buttons[m_DisplayedIndex].transform;
            var top = -row.anchoredPosition.y;
            var bottom = top + row.rect.height;
            var offset = m_List.content.anchoredPosition.y;
            var viewportHeight = m_List.viewport.rect.height;
            if (top < offset) offset = top;
            else if (bottom > offset + viewportHeight) offset = bottom - viewportHeight;
            m_List.StopMovement();
            m_List.content.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, Mathf.Max(0, m_List.content.rect.height - viewportHeight)));
            if (m_KeyboardHint != null) { m_KeyboardHint.text = m_Selector.PlayerIsActive ? "WASD: move player" : string.Empty; }
        }
    }
}