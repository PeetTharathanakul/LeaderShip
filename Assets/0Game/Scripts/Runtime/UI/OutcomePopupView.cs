using System;
using System.Collections.Generic;
using DG.Tweening;
using LeaderShip.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeaderShip.UI
{
    /// <summary>
    /// ป๊อปอัปสรุปหลังพาดหัว — "เกิดอะไรขึ้น ทำไม แล้วได้/เสียอะไรไปบ้าง"
    ///
    /// นี่คือที่ที่กติกาข้อ 6 ("ทุกผลลัพธ์ต้องบอกเหตุผล") ถูกทำให้เห็นเป็นรูปธรรม
    /// บรรทัดใน log เป็นบันทึกไว้ย้อนดู แต่ป๊อปอัปนี้คือการบอก **ตอนนั้น** ตอนที่ผู้เล่นยังสนใจอยู่
    ///
    /// การทาสีบวก/ลบไม่ได้ดูที่เครื่องหมาย แต่ดูที่ <see cref="Sentiment"/> ที่ชั้นโมเดลตัดสินมา
    /// เพราะ Fatigue กลับด้าน (+18 คือแย่ลง) ถ้าทาสีตามเครื่องหมายจะสื่อผิดทุกครั้งที่สั่งเร่งงาน
    /// </summary>
    public sealed class OutcomePopupView : MonoBehaviour
    {
        /// <summary>บรรทัด "ได้/เสีย" หนึ่งบรรทัด สร้างล่วงหน้าโดย PrototypeBuilder แล้วเปิด/ปิดเอา</summary>
        [Serializable]
        public sealed class DeltaRow
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public TMP_Text Label;
            public TMP_Text Value;
        }

        [SerializeField] GameObject root;
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup panelGroup;
        [SerializeField] CanvasGroup dimmerGroup;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text reasonText;
        [SerializeField] TMP_Text emptyText;
        [SerializeField] DeltaRow[] rows;
        [SerializeField] Button continueButton;

        public event Action ContinueRequested;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(RequestContinue);
            HideImmediate();
        }

        // ------------------------------------------------------------------ เปิด

        /// <summary>ผลของคำสั่งหนึ่งครั้ง</summary>
        public void ShowCommand(CommandResult result, RunState state)
        {
            var member = state.Members[result.MemberIndex];

            Show($"{member.Def.DisplayName}  ·  {GameText.CommandName(result.Preview.Type)}",
                GameText.OutcomeHeadline(result.Outcome),
                OutcomeFlashView.ColorFor(result.Outcome),
                GameText.OutcomeReason(result, state),
                GameText.CommandDeltas(result));
        }

        /// <summary>ผลของเหตุการณ์ — รวมกรณี "ไม่รับ" ซึ่งมีราคาของมันเสมอ (ADR-0005)</summary>
        public void ShowEvent(EventResult result)
        {
            string title = result.Preview.Def.Title;
            var deltas = GameText.EventDeltas(result.Applied);

            if (!result.Accepted)
            {
                Show(title, "DECLINED", Palette.Warning,
                    $"You turned it down. {result.Applied.Text}", deltas);
                return;
            }

            if (result.CredibilityGain > 0.5f)
                deltas.Add(new StatDelta("Credibility", GameText.Signed(result.CredibilityGain, 0), Sentiment.Good));

            Show(title,
                result.Succeeded ? "SUCCESS" : "FAILED",
                result.Succeeded ? Palette.Good : Palette.Danger,
                $"{result.Applied.Text} " +
                $"(you were shown {GameText.Percent(result.Preview.SuccessChance)})",
                deltas);
        }

        void Show(string title, string status, Color statusColor, string reason, IList<StatDelta> deltas)
        {
            if (root == null || panel == null) return;

            if (titleText != null) titleText.text = title;

            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = statusColor;
            }

            if (reasonText != null) reasonText.text = reason;

            int shown = FillRows(deltas);

            if (emptyText != null)
            {
                // ไม่มีค่าไหนขยับเลยก็ต้องพูดออกมา ไม่ใช่ปล่อยว่างให้ผู้เล่นเดาว่าจอค้าง
                emptyText.gameObject.SetActive(shown == 0);
                emptyText.text = "Nothing changed — the turn is spent all the same.";
            }

            root.SetActive(true);
            IsOpen = true;

            if (dimmerGroup != null)
            {
                dimmerGroup.alpha = 0f;
                dimmerGroup.DOFade(1f, UiTween.Fast).SetUpdate(true).SetLink(gameObject);
            }

            UiTween.PopIn(panel, panelGroup);
            AnimateRows(shown);
        }

        int FillRows(IList<StatDelta> deltas)
        {
            if (rows == null) return 0;

            int count = deltas == null ? 0 : deltas.Count;

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null || row.Root == null) continue;

                bool used = i < count;
                row.Root.gameObject.SetActive(used);
                if (!used) continue;

                var delta = deltas[i];
                if (row.Label != null)
                {
                    row.Label.text = delta.Label;
                    row.Label.color = Palette.TextMuted;
                }

                if (row.Value != null)
                {
                    row.Value.text = delta.Value;
                    row.Value.color = UiTween.ToneColor(delta.Tone);
                }
            }

            return Mathf.Min(count, rows.Length);
        }

        /// <summary>ไล่เข้าทีละบรรทัด เพื่อให้ตาไล่อ่านตามได้ ไม่ใช่โผล่พรวดพร้อมกันทั้งกอง</summary>
        void AnimateRows(int shown)
        {
            if (rows == null) return;

            for (int i = 0; i < shown && i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null || row.Root == null) continue;

                UiTween.SlideIn(row.Root, row.Group, new Vector2(-34f, 0f), 0.14f + i * 0.07f);
            }
        }

        // ------------------------------------------------------------------ ปิด

        public void RequestContinue()
        {
            if (!IsOpen) return;

            IsOpen = false;

            if (dimmerGroup != null)
                dimmerGroup.DOFade(0f, UiTween.Fast).SetUpdate(true).SetLink(gameObject);

            UiTween.PopOut(panel, panelGroup, () =>
            {
                if (root != null) root.SetActive(false);
                ContinueRequested?.Invoke();
            });
        }

        /// <summary>ปิดทันทีโดยไม่ยิง event — ใช้ตอนเริ่มรอบใหม่</summary>
        public void HideImmediate()
        {
            IsOpen = false;
            if (panel != null) UiTween.Kill(panel);
            if (root != null) root.SetActive(false);
        }

        void OnDisable()
        {
            if (panel != null) UiTween.Kill(panel);
        }

#if UNITY_EDITOR
        public void EditorAssign(GameObject rootObject, RectTransform panelRect, CanvasGroup panelCanvasGroup,
            CanvasGroup dimmer, TMP_Text title, TMP_Text status, TMP_Text reason, TMP_Text empty,
            DeltaRow[] deltaRows, Button continueBtn)
        {
            root = rootObject;
            panel = panelRect;
            panelGroup = panelCanvasGroup;
            dimmerGroup = dimmer;
            titleText = title;
            statusText = status;
            reasonText = reason;
            emptyText = empty;
            rows = deltaRows;
            continueButton = continueBtn;
        }
#endif
    }
}
