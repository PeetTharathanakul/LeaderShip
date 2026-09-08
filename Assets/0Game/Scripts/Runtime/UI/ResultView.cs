using System;
using LeaderShip.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeaderShip.UI
{
    /// <summary>หน้าจบรอบ — สรุปผลและสะท้อนสไตล์การนำ ไม่ใช่การให้คะแนนคน (ดู RunSummary)</summary>
    public sealed class ResultView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text headlineText;
        [SerializeField] TMP_Text styleText;
        [SerializeField] TMP_Text breakdownText;
        [SerializeField] Button restartButton;

        public event Action RestartRequested;

        void Awake()
        {
            if (restartButton != null) restartButton.onClick.AddListener(() => RestartRequested?.Invoke());
            Hide();
        }

        public void Show(RunState state)
        {
            bool won = state.Result == RunResult.Won;

            if (titleText != null)
            {
                titleText.text = won ? "Delivered On Time" : "Deadline Missed";
                titleText.color = won ? Palette.Good : Palette.Danger;
            }

            if (headlineText != null) headlineText.text = RunSummary.Headline(state);
            if (styleText != null) styleText.text = RunSummary.StyleLabel(state);
            if (breakdownText != null) breakdownText.text = RunSummary.CommandBreakdown(state);

            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

#if UNITY_EDITOR
        public void EditorAssign(GameObject rootObject, TMP_Text title, TMP_Text headline,
            TMP_Text style, TMP_Text breakdown, Button restart)
        {
            root = rootObject;
            titleText = title;
            headlineText = headline;
            styleText = style;
            breakdownText = breakdown;
            restartButton = restart;
        }
#endif
    }
}
