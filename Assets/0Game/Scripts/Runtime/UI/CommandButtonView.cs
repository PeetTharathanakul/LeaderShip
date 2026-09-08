using System;
using LeaderShip.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeaderShip.UI
{
    /// <summary>
    /// ปุ่มคำสั่งหนึ่งปุ่ม พร้อมพรีวิวผลลัพธ์
    ///
    /// ปุ่มนี้แสดง "โอกาสสำเร็จ" ซึ่งต้องเป็นตัวเลข **ตัวเดียวกัน** กับที่ระบบใช้ตัดสินผลและคำนวณเครดิต
    /// จึงดึงจาก CommandPreview ก้อนเดียวเสมอ ห้ามคำนวณเองซ้ำ (ADR-0005)
    /// </summary>
    public sealed class CommandButtonView : MonoBehaviour
    {
        [SerializeField] CommandType type = CommandType.Work;
        [SerializeField] Button button;
        [SerializeField] Image frame;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text chanceText;
        [SerializeField] TMP_Text detailText;
        [SerializeField] TMP_Text warnText;

        public event Action<CommandType> Clicked;
        public CommandType Type => type;

        void Awake()
        {
            if (button == null) Debug.LogError($"[{nameof(CommandButtonView)}] '{name}' ไม่ได้ใส่ button", this);
            else button.onClick.AddListener(() => Clicked?.Invoke(type));
        }

        public void Refresh(TurnEngine engine, int memberIndex, bool interactable)
        {
            if (engine == null) return;

            var def = engine.Content.GetCommand(type);
            var preview = engine.PreviewCommand(memberIndex, type);

            if (titleText != null) titleText.text = def.DisplayName;

            bool usable = interactable && !preview.Blocked;
            if (button != null) button.interactable = usable;

            if (chanceText != null)
            {
                if (preview.Blocked)
                {
                    chanceText.text = "Unavailable";
                    chanceText.color = Palette.Danger;
                }
                else if (preview.AlwaysSucceeds)
                {
                    chanceText.text = "Always works";
                    chanceText.color = Palette.Good;
                }
                else
                {
                    chanceText.text = $"{GameText.Percent(preview.SuccessChance)} success";
                    chanceText.color = preview.SuccessChance >= 0.6f ? Palette.Good
                        : preview.SuccessChance >= 0.4f ? Palette.Warning
                        : Palette.Danger;
                }
            }

            if (detailText != null) detailText.text = BuildDetail(preview);

            if (warnText != null)
            {
                string warn = BuildWarning(preview, engine);
                warnText.text = warn;
                warnText.gameObject.SetActive(!string.IsNullOrEmpty(warn));
            }

            if (frame != null)
                frame.color = usable ? Palette.PanelBorder : Palette.ButtonDisabled;
        }

        static string BuildDetail(CommandPreview p)
        {
            string s = "";

            if (p.ProgressOnSuccess != 0f) s += $"Progress {GameText.Signed(p.ProgressOnSuccess)}%   ";
            if (p.ProgressOnFail < 0f) s += $"Rework on miss {GameText.Num(p.ProgressOnFail)}%   ";
            if (p.FatigueDelta != 0f) s += $"Fatigue {GameText.Signed(p.FatigueDelta, 0)}   ";
            if (p.MoraleOnSuccess != 0f) s += $"Morale {GameText.Signed(p.MoraleOnSuccess, 0)}   ";

            if (!p.AlwaysSucceeds && p.CredibilityGainOnSuccess >= 1f)
                s += $"Credibility on success {GameText.Signed(p.CredibilityGainOnSuccess, 0)}";

            return s.TrimEnd();
        }

        string BuildWarning(CommandPreview p, TurnEngine engine)
        {
            if (p.Blocked)
                return "Burned out — let them rest first";

            string s = "";

            if (p.RepeatPenalty > 0f)
                s += $"Same order {p.ConsecutiveIfChosen}x in a row · Credibility −{GameText.Num(p.RepeatPenalty, 0)}";

            if (p.PushTiredPenalty > 0f)
            {
                if (s.Length > 0) s += "   ";
                s += $"Pushing someone exhausted · Credibility −{GameText.Num(p.PushTiredPenalty, 0)}";
            }

            if (p.RefusalChance > 0f)
            {
                if (s.Length > 0) s += "   ";
                s += $"May be refused · {GameText.Percent(p.RefusalChance)}";
            }

            return s;
        }

#if UNITY_EDITOR
        public void EditorAssign(CommandType commandType, Button b, Image frameImage,
            TMP_Text title, TMP_Text chance, TMP_Text detail, TMP_Text warn)
        {
            type = commandType;
            button = b;
            frame = frameImage;
            titleText = title;
            chanceText = chance;
            detailText = detail;
            warnText = warn;
        }
#endif
    }
}
