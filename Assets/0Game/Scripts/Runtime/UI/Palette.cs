using UnityEngine;

namespace LeaderShip.UI
{
    /// <summary>
    /// อาร์ต UI ที่ได้มาเป็นสีขาวล้วนทั้งชุด สีทั้งหมดจึงมาจากการ tint
    /// รวมไว้ที่เดียวเพื่อให้ทั้งเกมใช้ภาษาสีเดียวกัน และเปลี่ยนธีมทั้งเกมได้จากไฟล์เดียว
    ///
    /// ภาษาสีของเกมนี้:
    ///   เขียว = ความคืบหน้า/ดี · ส้ม = ความเหนื่อยล้า · ฟ้า = กำลังใจ · ม่วง = ความน่าเชื่อถือ
    ///   แดง = อันตราย/ล้มเหลว · เหลือง = คำเตือน
    /// </summary>
    public static class Palette
    {
        public static readonly Color Background = Hex("#12141A");
        public static readonly Color PanelFill = Hex("#1E2230");
        public static readonly Color PanelFillRaised = Hex("#272C3C");
        public static readonly Color PanelBorder = Hex("#4A5266");
        public static readonly Color PanelBorderActive = Hex("#8FA0C4");

        public static readonly Color TextPrimary = Hex("#E8ECF4");
        public static readonly Color TextMuted = Hex("#8C93A6");
        public static readonly Color TextOnAccent = Hex("#12141A");

        public static readonly Color Progress = Hex("#4CC38A");
        public static readonly Color Fatigue = Hex("#E5A13A");
        public static readonly Color Morale = Hex("#59A6F0");
        public static readonly Color Credibility = Hex("#B983F0");

        public static readonly Color Danger = Hex("#E5533D");
        public static readonly Color Warning = Hex("#E8B84B");
        public static readonly Color Good = Hex("#4CC38A");
        public static readonly Color BarTrack = Hex("#0D0F14");

        public static readonly Color ButtonDisabled = Hex("#2A2E3A");

        /// <summary>สีของแถบค่าที่ "ยิ่งสูงยิ่งแย่" — เปลี่ยนเป็นแดงเมื่อเข้าเขตอันตราย</summary>
        public static Color FatigueColor(float fatigue01)
        {
            if (fatigue01 >= 0.999f) return Danger;
            if (fatigue01 >= 0.70f) return Color.Lerp(Fatigue, Danger, (fatigue01 - 0.70f) / 0.30f);
            return Fatigue;
        }

        /// <summary>
        /// ความน่าเชื่อถือต้องเปลี่ยนเป็นสีเตือน **ก่อน** ถึงเขตที่ทีมเริ่มปฏิเสธคำสั่ง
        /// ผู้เล่นต้องเห็นหลุมก่อนตก ไม่ใช่ตอนอยู่ในหลุมแล้ว (ADR-0005)
        /// </summary>
        public static Color CredibilityColor(float credibility, float refusalThreshold)
        {
            float warnAt = refusalThreshold + 5f;
            if (credibility < refusalThreshold) return Danger;
            if (credibility < warnAt) return Warning;
            return Credibility;
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.magenta; // สีที่มองข้ามไม่ได้ ถ้ามีคนพิมพ์ hex ผิด
        }
    }
}
