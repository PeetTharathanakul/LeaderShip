using System;
using System.Text;
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
            else button.onClick.AddListener(OnClick);
        }

        void OnClick()
        {
            // กระตุกปุ่มที่เพิ่งกด เพื่อให้ตาเชื่อมโยงได้ว่าผลที่กำลังจะขึ้นกลางจอมาจากปุ่มไหน
            UiTween.Punch(transform, 0.12f, 0.26f);
            Clicked?.Invoke(type);
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

        /// <summary>
        /// ตารางสองคอลัมน์ ชื่อค่าอยู่ซ้าย ตัวเลขอยู่ขวา บรรทัดละค่า
        ///
        /// ของเดิมเป็นข้อความยาวต่อกันคั่นด้วยช่องว่าง ซึ่งอ่านเทียบระหว่างปุ่มไม่ได้เลย
        /// ทั้งที่การเทียบสี่ปุ่มคือสิ่งเดียวที่ผู้เล่นต้องทำในทุกเทิร์น
        /// สีบอกว่าค่านั้นดีหรือแย่ **สำหรับผู้เล่น** ไม่ใช่ตามเครื่องหมาย — Fatigue +18 คือแย่
        /// </summary>
        static string BuildDetail(CommandPreview p)
        {
            var sb = new StringBuilder();

            if (p.ProgressOnSuccess != 0f)
                Row(sb, "Progress", GameText.Signed(p.ProgressOnSuccess) + "%", Palette.Good);

            if (p.ProgressOnFail < 0f)
                Row(sb, "On miss", GameText.Num(p.ProgressOnFail) + "%", Palette.Danger);

            if (p.FatigueDelta != 0f)
                Row(sb, "Fatigue", GameText.Signed(p.FatigueDelta, 0),
                    p.FatigueDelta > 0f ? Palette.Danger : Palette.Good);

            if (p.MoraleOnSuccess != 0f)
                Row(sb, "Morale", GameText.Signed(p.MoraleOnSuccess, 0),
                    p.MoraleOnSuccess > 0f ? Palette.Good : Palette.Danger);

            if (!p.AlwaysSucceeds && p.CredibilityGainOnSuccess >= 1f)
                Row(sb, "Credibility", GameText.Signed(p.CredibilityGainOnSuccess, 0), Palette.Credibility);

            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>ค่าตัวเลขชิดขวาด้วยแท็ก &lt;pos&gt; ของ TMP — ไม่ต้องใช้ layout group ให้เปลืองเฟรม</summary>
        static void Row(StringBuilder sb, string label, string value, Color valueColor)
        {
            sb.Append(label)
              .Append("<pos=62%><color=#")
              .Append(ColorUtility.ToHtmlStringRGB(valueColor))
              .Append('>')
              .Append(value)
              .Append("</color>\n");
        }

        /// <summary>
        /// ราคาที่ต้องจ่ายเพราะ "สั่งแบบนี้ตอนนี้" ไม่ใช่เพราะสำเร็จหรือล้มเหลว
        /// ต้องขึ้นบรรทัดแยกทีละข้อ ผู้เล่นถึงจะเห็นว่ามีกี่ข้อพร้อมกัน
        /// </summary>
        string BuildWarning(CommandPreview p, TurnEngine engine)
        {
            if (p.Blocked)
                return "Burned out — let them rest first";

            var sb = new StringBuilder();

            if (p.RepeatPenalty > 0f)
                sb.Append($"• Same order {p.ConsecutiveIfChosen}x in a row · Credibility −{GameText.Num(p.RepeatPenalty, 0)}\n");

            if (p.PushTiredPenalty > 0f)
                sb.Append($"• Pushing someone exhausted · Credibility −{GameText.Num(p.PushTiredPenalty, 0)}\n");

            if (p.RefusalChance > 0f)
                sb.Append($"• {GameText.Percent(p.RefusalChance)} chance they refuse outright\n");

            return sb.ToString().TrimEnd('\n');
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
