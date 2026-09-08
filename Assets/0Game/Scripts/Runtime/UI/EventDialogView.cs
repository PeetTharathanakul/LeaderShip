using System;
using DG.Tweening;
using LeaderShip.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeaderShip.UI
{
    /// <summary>
    /// กล่องเหตุการณ์
    ///
    /// กติกาเดียวที่ห้ามผิด: **ต้องโชว์ราคาของทั้งสองทางก่อนเลือกเสมอ** (ADR-0005)
    /// ถ้าวันไหนมีคนทำให้ฝั่ง "ไม่รับ" ว่างเปล่า เกมจะกลายเป็นปุ่ม "ไม่" ที่กดรัว ๆ แล้วชนะ
    ///
    /// เลย์เอาต์เป็น **การ์ดสองใบ ปุ่มอยู่ในใบของตัวเอง** จงใจให้เป็นแบบนี้:
    /// ของเดิมเป็นข้อความลอย ๆ เรียงลงมาแล้วมีปุ่มคั่น ผู้เล่นอ่านไม่ออกว่าข้อความไหนเป็นของปุ่มไหน
    /// และไม่รู้ว่าที่เขียนอยู่คือ "สิ่งที่จะเกิด" หรือ "สิ่งที่เกิดไปแล้ว"
    /// หัวการ์ดจึงต้องขึ้นต้นด้วย "IF YOU ..." เสมอ และฝั่งปฏิเสธต้องบอกว่าเกิดขึ้นแน่นอน ไม่ใช่ความเสี่ยง
    /// </summary>
    public sealed class EventDialogView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup panelGroup;
        [SerializeField] CanvasGroup dimmerGroup;

        [Header("Header")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text bodyText;

        [Header("Accept card")]
        [SerializeField] RectTransform acceptCard;
        [SerializeField] CanvasGroup acceptCardGroup;
        [SerializeField] TMP_Text acceptHeaderText;
        [SerializeField] TMP_Text acceptChanceText;
        [SerializeField] TMP_Text acceptSuccessText;
        [SerializeField] TMP_Text acceptFailText;
        [SerializeField] Button acceptButton;

        [Header("Decline card")]
        [SerializeField] RectTransform declineCard;
        [SerializeField] CanvasGroup declineCardGroup;
        [SerializeField] TMP_Text declineHeaderText;
        [SerializeField] TMP_Text declineText;
        [SerializeField] Button declineButton;

        public event Action<bool> Chosen;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (acceptButton != null) acceptButton.onClick.AddListener(() => Choose(true));
            if (declineButton != null) declineButton.onClick.AddListener(() => Choose(false));
            Hide();
        }

        void Choose(bool accept)
        {
            if (!IsOpen) return;
            IsOpen = false;
            Chosen?.Invoke(accept);
        }

        public void Show(EventPreview preview)
        {
            if (root == null) return;

            // กันการเรียกซ้ำจาก Refresh ทุกรอบ ไม่งั้นอนิเมชันจะรีเซ็ตตัวเองไม่จบสักที
            if (IsOpen) return;

            var def = preview.Def;

            if (titleText != null) titleText.text = def.Title;
            if (bodyText != null) bodyText.text = def.Body;

            FillAcceptCard(preview);
            FillDeclineCard(def);

            root.SetActive(true);
            IsOpen = true;

            if (dimmerGroup != null)
            {
                dimmerGroup.alpha = 0f;
                dimmerGroup.DOFade(1f, UiTween.Fast).SetUpdate(true).SetLink(gameObject);
            }

            UiTween.PopIn(panel, panelGroup);
            UiTween.SlideIn(acceptCard, acceptCardGroup, new Vector2(-40f, 0f), 0.10f);
            UiTween.SlideIn(declineCard, declineCardGroup, new Vector2(-40f, 0f), 0.18f);
        }

        void FillAcceptCard(EventPreview preview)
        {
            var def = preview.Def;

            if (acceptHeaderText != null)
            {
                acceptHeaderText.text = "IF YOU ACCEPT";
                acceptHeaderText.color = Palette.Good;
            }

            if (acceptChanceText != null)
            {
                acceptChanceText.text = $"{GameText.Percent(preview.SuccessChance)} it works";
                acceptChanceText.color = preview.SuccessChance >= 0.6f ? Palette.Good
                    : preview.SuccessChance >= 0.4f ? Palette.Warning
                    : Palette.Danger;
            }

            if (acceptSuccessText != null)
                acceptSuccessText.text =
                    $"<color=#{Hex(Palette.Good)}>IT WORKS</color>   {def.OnAcceptSuccess.Text}  " +
                    $"<color=#{Hex(Palette.TextMuted)}>{GameText.OutcomeSummary(def.OnAcceptSuccess)}</color>";

            if (acceptFailText != null)
                acceptFailText.text =
                    $"<color=#{Hex(Palette.Danger)}>IT BACKFIRES</color>   {def.OnAcceptFail.Text}  " +
                    $"<color=#{Hex(Palette.TextMuted)}>{GameText.OutcomeSummary(def.OnAcceptFail)}</color>";
        }

        void FillDeclineCard(EventDefinition def)
        {
            string summary = GameText.OutcomeSummary(def.OnDecline);
            bool free = string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(def.OnDecline.Text);

            if (declineHeaderText != null)
            {
                // "guaranteed" คือคำสำคัญ ฝั่งนี้ไม่ใช่การพนัน มันเกิดขึ้นแน่นอน
                declineHeaderText.text = free
                    ? "IF YOU DECLINE"
                    : "IF YOU DECLINE — this happens for certain";
                declineHeaderText.color = free ? Palette.Danger : Palette.Warning;
            }

            if (declineText != null)
            {
                declineText.text = free
                    ? $"<color=#{Hex(Palette.Danger)}>This event has no cost for declining — violates ADR-0005</color>"
                    : $"{def.OnDecline.Text}  <color=#{Hex(Palette.TextMuted)}>{summary}</color>";
            }
        }

        public void Hide()
        {
            IsOpen = false;
            if (panel != null) UiTween.Kill(panel);
            if (root != null) root.SetActive(false);
        }

        void OnDisable()
        {
            if (panel != null) UiTween.Kill(panel);
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

#if UNITY_EDITOR
        public void EditorAssign(GameObject rootObject, RectTransform panelRect, CanvasGroup panelCanvasGroup,
            CanvasGroup dimmer, TMP_Text title, TMP_Text body,
            RectTransform acceptCardRect, CanvasGroup acceptGroup, TMP_Text acceptHeader, TMP_Text acceptChance,
            TMP_Text acceptSuccess, TMP_Text acceptFail, Button accept,
            RectTransform declineCardRect, CanvasGroup declineGroup, TMP_Text declineHeader, TMP_Text decline,
            Button declineBtn)
        {
            root = rootObject;
            panel = panelRect;
            panelGroup = panelCanvasGroup;
            dimmerGroup = dimmer;
            titleText = title;
            bodyText = body;

            acceptCard = acceptCardRect;
            acceptCardGroup = acceptGroup;
            acceptHeaderText = acceptHeader;
            acceptChanceText = acceptChance;
            acceptSuccessText = acceptSuccess;
            acceptFailText = acceptFail;
            acceptButton = accept;

            declineCard = declineCardRect;
            declineCardGroup = declineGroup;
            declineHeaderText = declineHeader;
            declineText = decline;
            declineButton = declineBtn;
        }
#endif
    }
}
