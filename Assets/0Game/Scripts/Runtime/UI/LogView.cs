using System.Text;
using LeaderShip.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeaderShip.UI
{
    /// <summary>
    /// บันทึกเหตุการณ์
    ///
    /// นี่คือที่ที่กติกา "ผู้เล่นต้องรู้เสมอว่าเกิดอะไรขึ้นและทำไม" ถูกทำให้เป็นจริง
    /// ถ้าตัดหน้านี้ออก เกมจะละเมิดข้อ Clarity ทันที ไม่ใช่ของตกแต่ง
    /// </summary>
    public sealed class LogView : MonoBehaviour
    {
        [SerializeField] TMP_Text text;
        [SerializeField] ScrollRect scrollRect;

        [Tooltip("เก็บกี่บรรทัดล่าสุด — มากกว่านี้ TMP จะเริ่มกินเฟรมเวลาสร้าง mesh ใหม่")]
        [SerializeField] int maxLines = 60;

        readonly StringBuilder _sb = new StringBuilder(4096);
        int _renderedCount = -1;
        TurnEngine _renderedEngine;

        public void Refresh(TurnEngine engine)
        {
            if (engine == null || text == null) return;

            var log = engine.Log;

            // ต้องเทียบตัว engine ด้วย ไม่ใช่แค่จำนวนบรรทัด — เริ่มรอบใหม่จะได้ engine คนละตัว
            // ที่บังเอิญมีจำนวนบรรทัดเท่ากันได้ แล้วบันทึกของรอบเก่าจะค้างอยู่บนจอ
            if (ReferenceEquals(engine, _renderedEngine) && log.Count == _renderedCount) return;

            _renderedEngine = engine;
            _renderedCount = log.Count;

            int start = Mathf.Max(0, log.Count - maxLines);

            _sb.Clear();
            int lastTurn = -1;

            for (int i = start; i < log.Count; i++)
            {
                var line = log[i];

                if (line.Turn != lastTurn)
                {
                    lastTurn = line.Turn;
                    if (_sb.Length > 0) _sb.Append('\n');
                    _sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(Palette.TextMuted)}>— Turn {line.Turn} —</color>\n");
                }

                _sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(ToneColor(line.Tone))}>{line.Text}</color>\n");
            }

            text.text = _sb.ToString();

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f; // เลื่อนไปบรรทัดล่าสุดเสมอ
            }
        }

        static Color ToneColor(LogTone tone)
        {
            switch (tone)
            {
                case LogTone.Good: return Palette.Good;
                case LogTone.Bad: return Palette.Danger;
                case LogTone.Warning: return Palette.Warning;
                default: return Palette.TextPrimary;
            }
        }

#if UNITY_EDITOR
        public void EditorAssign(TMP_Text logText, ScrollRect scroll)
        {
            text = logText;
            scrollRect = scroll;
        }
#endif
    }
}
