using System;
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
    /// </summary>
    public sealed class EventDialogView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] TMP_Text chanceText;
        [SerializeField] TMP_Text acceptDetailText;
        [SerializeField] TMP_Text declineDetailText;
        [SerializeField] Button acceptButton;
        [SerializeField] Button declineButton;

        public event Action<bool> Chosen;

        void Awake()
        {
            if (acceptButton != null) acceptButton.onClick.AddListener(() => Chosen?.Invoke(true));
            if (declineButton != null) declineButton.onClick.AddListener(() => Chosen?.Invoke(false));
            Hide();
        }

        public void Show(EventPreview preview)
        {
            var def = preview.Def;

            if (titleText != null) titleText.text = def.Title;
            if (bodyText != null) bodyText.text = def.Body;

            if (chanceText != null)
            {
                chanceText.text = $"{GameText.Percent(preview.SuccessChance)} chance of success";
                chanceText.color = preview.SuccessChance >= 0.6f ? Palette.Good
                    : preview.SuccessChance >= 0.4f ? Palette.Warning
                    : Palette.Danger;
            }

            if (acceptDetailText != null)
            {
                acceptDetailText.text =
                    $"If it works: {def.OnAcceptSuccess.Text} {GameText.OutcomeSummary(def.OnAcceptSuccess)}\n" +
                    $"If it fails: {def.OnAcceptFail.Text} {GameText.OutcomeSummary(def.OnAcceptFail)}";
            }

            if (declineDetailText != null)
            {
                string summary = GameText.OutcomeSummary(def.OnDecline);
                declineDetailText.text = string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(def.OnDecline.Text)
                    ? "<color=#E5533D>This event has no cost for declining — violates ADR-0005</color>"
                    : $"{def.OnDecline.Text} {summary}";
            }

            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

#if UNITY_EDITOR
        public void EditorAssign(GameObject rootObject, TMP_Text title, TMP_Text body, TMP_Text chance,
            TMP_Text acceptDetail, TMP_Text declineDetail, Button accept, Button decline)
        {
            root = rootObject;
            titleText = title;
            bodyText = body;
            chanceText = chance;
            acceptDetailText = acceptDetail;
            declineDetailText = declineDetail;
            acceptButton = accept;
            declineButton = decline;
        }
#endif
    }
}
