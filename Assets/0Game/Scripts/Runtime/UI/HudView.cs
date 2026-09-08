using LeaderShip.Model;
using TMPro;
using UnityEngine;

namespace LeaderShip.UI
{
    /// <summary>แถบบน: ความคืบหน้า เดดไลน์ ความน่าเชื่อถือ และคำเตือนเดดไลน์ที่ประกาศไว้ล่วงหน้า</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] BarView progressBar;
        [SerializeField] BarView credibilityBar;
        [SerializeField] TMP_Text turnText;
        [SerializeField] TMP_Text deadlineText;
        [SerializeField] TMP_Text warningText;

        public void Refresh(TurnEngine engine)
        {
            if (engine == null) return;

            var state = engine.State;
            var b = state.Balance;

            if (progressBar != null)
                progressBar.Set(state.Progress / b.ProgressGoal,
                    $"Progress {GameText.Num(state.Progress)} / {GameText.Num(b.ProgressGoal, 0)}%",
                    Palette.Progress);

            if (credibilityBar != null)
                credibilityBar.Set(state.Credibility / 100f,
                    $"Credibility {GameText.Num(state.Credibility, 0)}",
                    Palette.CredibilityColor(state.Credibility, b.RefusalThreshold));

            if (turnText != null) turnText.text = $"Turn {state.TurnNumber}";

            if (deadlineText != null)
            {
                deadlineText.text = $"{state.TurnsRemaining} turns left";
                deadlineText.color = state.TurnsRemaining <= 3 ? Palette.Danger
                    : state.TurnsRemaining <= 6 ? Palette.Warning
                    : Palette.TextPrimary;
            }

            if (warningText != null)
            {
                string warn = BuildWarning(state);
                warningText.text = warn;
                warningText.gameObject.SetActive(!string.IsNullOrEmpty(warn));
            }
        }

        static string BuildWarning(RunState state)
        {
            // เดดไลน์ที่กำลังจะหดต้องเห็นตลอดเวลา ไม่ใช่โผล่มาบรรทัดเดียวใน log แล้วหายไป
            if (state.PendingCut.IsActive)
                return $"⚠ {state.PendingCut.Reason} — in {state.PendingCut.TurnsRemaining} turns the deadline loses {state.PendingCut.Amount}";

            if (state.Credibility < state.Balance.RefusalThreshold)
                return $"⚠ The team is doubting your orders — {GameText.Percent(state.RefusalChance())} chance of refusal";

            return "";
        }

#if UNITY_EDITOR
        public void EditorAssign(BarView progress, BarView credibility, TMP_Text turn, TMP_Text deadline, TMP_Text warning)
        {
            progressBar = progress;
            credibilityBar = credibility;
            turnText = turn;
            deadlineText = deadline;
            warningText = warning;
        }
#endif
    }
}
