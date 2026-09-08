using System;
using DG.Tweening;
using LeaderShip.Model;
using TMPro;
using UnityEngine;

namespace LeaderShip.UI
{
    /// <summary>
    /// พาดหัวผลลัพธ์กลางจอ — SUCCESS / FAILED / REFUSED / DECLINED
    ///
    /// มีหน้าที่เดียว: ตอบคำถามแรกของผู้เล่นหลังกดปุ่ม ("ผ่านไหม") ให้จบภายในครึ่งวินาที
    /// รายละเอียดว่าได้อะไรเสียอะไรเป็นหน้าที่ของ <see cref="OutcomePopupView"/> ที่ตามมาทีหลัง
    /// แยกกันเพราะสองคำถามนี้คนละจังหวะ ถ้ายัดรวมกันผู้เล่นจะอ่านไม่ทันทั้งคู่
    ///
    /// ตัวคอมโพเนนต์ต้องอยู่บนอ็อบเจกต์ที่แอ็กทีฟตลอด ส่วนที่ปิด/เปิดคือ <see cref="root"/> ที่เป็นลูกของมัน
    /// </summary>
    public sealed class OutcomeFlashView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text headlineText;
        [SerializeField] TMP_Text captionText;

        [Header("Timing")]
        [SerializeField] float holdSeconds = 0.40f;

        Sequence _sequence;

        void Awake() => Hide();

        /// <summary>ทางลัดสำหรับผลของคำสั่ง — สีและท่าทางมาจาก <see cref="OutcomeKind"/> เอง</summary>
        public void Play(OutcomeKind outcome, string caption, Action onComplete)
            => Play(GameText.OutcomeHeadline(outcome), ColorFor(outcome),
                outcome == OutcomeKind.Success, caption, onComplete);

        /// <summary>
        /// เล่นพาดหัวหนึ่งครั้ง แล้วเรียก <paramref name="onComplete"/> เมื่อจบ
        /// <paramref name="positive"/> เปลี่ยนท่าทาง: ดี = กระตุก · แย่ = เขย่า
        /// ผู้เล่นแยกออกตั้งแต่ก่อนอ่านตัวอักษรจบ
        /// </summary>
        public void Play(string headline, Color color, bool positive, string caption, Action onComplete)
        {
            if (root == null || panel == null)
            {
                onComplete?.Invoke();
                return;
            }

            _sequence?.Kill();

            if (headlineText != null)
            {
                headlineText.text = headline;
                headlineText.color = color;
            }

            if (captionText != null)
            {
                captionText.text = caption ?? "";
                captionText.color = Palette.TextPrimary;
            }

            root.SetActive(true);

            Vector2 restPosition = panel.anchoredPosition;
            panel.localScale = Vector3.one * 0.55f;
            if (group != null) group.alpha = 0f;

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _sequence.Append(panel.DOScale(1f, 0.24f).SetEase(Ease.OutBack, 2.2f));
            if (group != null) _sequence.Join(group.DOFade(1f, 0.14f));

            if (positive) _sequence.Append(UiTween.Punch(panel, 0.1f, 0.26f));
            else _sequence.Append(UiTween.Shake(panel, 26f, 0.34f));

            _sequence.AppendInterval(holdSeconds);
            _sequence.Append(panel.DOAnchorPos(restPosition + new Vector2(0f, 52f), 0.2f).SetEase(Ease.InQuad));
            if (group != null) _sequence.Join(group.DOFade(0f, 0.2f));

            _sequence.OnComplete(() =>
            {
                panel.anchoredPosition = restPosition;
                Hide();
                onComplete?.Invoke();
            });
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>ตัดอนิเมชันทิ้งทันที ใช้ตอนเริ่มรอบใหม่ระหว่างที่ยังเล่นค้างอยู่</summary>
        public void Cancel()
        {
            _sequence?.Kill();
            _sequence = null;
            Hide();
        }

        void OnDisable() => Cancel();

        public static Color ColorFor(OutcomeKind outcome)
        {
            switch (outcome)
            {
                case OutcomeKind.Success: return Palette.Good;
                case OutcomeKind.Refused: return Palette.Warning;
                default: return Palette.Danger;
            }
        }

#if UNITY_EDITOR
        public void EditorAssign(GameObject rootObject, RectTransform panelRect, CanvasGroup canvasGroup,
            TMP_Text headline, TMP_Text caption)
        {
            root = rootObject;
            panel = panelRect;
            group = canvasGroup;
            headlineText = headline;
            captionText = caption;
        }
#endif
    }
}
