using System.Collections.Generic;
using System.Globalization;

namespace LeaderShip.Model
{
    /// <summary>ค่าที่เปลี่ยนไปหนึ่งบรรทัด "ดีกับผู้เล่นไหม" ตัดสินที่นี่ที่เดียว ไม่ใช่ให้ View เดาเอง</summary>
    public enum Sentiment { Neutral, Good, Bad }

    /// <summary>
    /// หนึ่งบรรทัดของ "ได้อะไร เสียอะไร" ที่หน้าป๊อปอัปเอาไปวาด
    /// แยก Tone ออกจากสี เพราะชั้นโมเดลห้ามรู้จัก UnityEngine.Color (ADR-0007)
    /// </summary>
    public struct StatDelta
    {
        public string Label;
        public string Value;
        public Sentiment Tone;

        public StatDelta(string label, string value, Sentiment tone)
        {
            Label = label;
            Value = value;
            Tone = tone;
        }
    }

    /// <summary>
    /// ข้อความที่ผู้เล่นเห็นของชั้นโมเดลรวมอยู่ที่นี่ที่เดียว (ภาษาอังกฤษทั้งหมด)
    /// เหตุผล: กติกา "ทุกผลลัพธ์ต้องบอกเหตุผล" เป็นข้อบังคับเชิงออกแบบ (Clarity) ไม่ใช่ของตกแต่ง UI
    /// ถ้าปล่อยให้ View ประกอบประโยคเอง จะมีวันที่ลืมบอกเหตุผลแล้วไม่มีใครรู้
    /// </summary>
    public static class GameText
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Num(float v, int decimals = 1) =>
            v.ToString("0." + new string('0', decimals), Inv);

        public static string Signed(float v, int decimals = 1) =>
            (v >= 0f ? "+" : "") + Num(v, decimals);

        public static string Percent(float chance01) =>
            (chance01 * 100f).ToString("0", Inv) + "%";

        public static string CommandName(CommandType t)
        {
            switch (t)
            {
                case CommandType.Push: return "Push Hard";
                case CommandType.Work: return "Assign Work";
                case CommandType.Direct: return "Give Direction";
                case CommandType.Rest: return "Let Rest";
                default: return t.ToString();
            }
        }

        /// <summary>รูปอดีต ใช้เล่าว่า "เกิดอะไรขึ้น" ในบรรทัด log</summary>
        public static string CommandVerbPast(CommandType t)
        {
            switch (t)
            {
                case CommandType.Push: return "pushed hard";
                case CommandType.Work: return "worked";
                case CommandType.Direct: return "took direction";
                case CommandType.Rest: return "rested";
                default: return t.ToString();
            }
        }

        /// <summary>รูปฐาน ใช้ต่อหลัง "failed to ..."</summary>
        public static string CommandVerb(CommandType t)
        {
            switch (t)
            {
                case CommandType.Push: return "push hard";
                case CommandType.Work: return "work";
                case CommandType.Direct: return "take direction";
                case CommandType.Rest: return "rest";
                default: return t.ToString();
            }
        }

        public static string DriverReason(ChanceDriver driver, MemberState member, RunState state)
        {
            switch (driver)
            {
                case ChanceDriver.Fatigue:
                    return $"too worn out (Fatigue {Num(member.Fatigue, 0)})";
                case ChanceDriver.Morale:
                    return $"morale is low (Morale {Num(member.Morale, 0)})";
                case ChanceDriver.Credibility:
                    return $"the team doubts your orders (Credibility {Num(state.Credibility, 0)})";
                case ChanceDriver.CommandRisk:
                    return "this order is risky by nature";
                default:
                    return "bad timing";
            }
        }

        /// <summary>บรรทัด log ของการสั่งหนึ่งครั้ง — ต้องตอบได้เสมอว่า "เกิดอะไรขึ้น และทำไม"</summary>
        public static string CommandLine(CommandResult r, RunState state)
        {
            var member = state.Members[r.MemberIndex];
            string who = member.Def.DisplayName;
            var p = r.Preview;

            switch (r.Outcome)
            {
                case OutcomeKind.Refused:
                    return $"{who} refused the order — Credibility is now {Num(state.Credibility, 0)} " +
                           $"({Percent(p.RefusalChance)} refusal chance). The turn is wasted.";

                case OutcomeKind.Success:
                {
                    string head = p.AlwaysSucceeds
                        ? $"{who} {CommandVerbPast(p.Type)}"
                        : $"{who} {CommandVerbPast(p.Type)} — success ({Percent(p.SuccessChance)} chance)";

                    string body = "";
                    if (r.ProgressDelta != 0f) body += $" · Progress {Signed(r.ProgressDelta)}%";
                    if (r.MoraleDelta != 0f) body += $" · Morale {Signed(r.MoraleDelta, 0)}";
                    if (r.FatigueDelta != 0f) body += $" · Fatigue {Signed(r.FatigueDelta, 0)}";
                    if (r.CredibilityDelta != 0f) body += $" · Credibility {Signed(r.CredibilityDelta, 0)}";
                    return head + body;
                }

                default:
                {
                    string reason = DriverReason(p.Driver, member, state);
                    string head = $"{who} failed to {CommandVerb(p.Type)} — {reason} ({Percent(p.SuccessChance)} chance)";

                    string body = "";
                    if (r.ProgressDelta < 0f) body += $" · Rework {Num(r.ProgressDelta)}%";
                    if (r.MoraleDelta != 0f) body += $" · Morale {Signed(r.MoraleDelta, 0)}";
                    if (r.FatigueDelta != 0f) body += $" · Fatigue {Signed(r.FatigueDelta, 0)}";
                    if (r.CredibilityDelta != 0f) body += $" · Credibility {Signed(r.CredibilityDelta, 0)}";
                    return head + body;
                }
            }
        }

        // ------------------------------------------------------------------ ได้อะไร เสียอะไร

        /// <summary>
        /// พาดหัวสั้น ๆ กลางจอ — ต้องอ่านจบในครึ่งวินาที
        /// "ถูกปฏิเสธ" แยกจาก "ล้มเหลว" เพราะคนละสาเหตุกันคนละเรื่อง (ADR-0005)
        /// </summary>
        public static string OutcomeHeadline(OutcomeKind kind)
        {
            switch (kind)
            {
                case OutcomeKind.Success: return "SUCCESS";
                case OutcomeKind.Refused: return "REFUSED";
                default: return "FAILED";
            }
        }

        /// <summary>บรรทัดเดียวที่ตอบว่า "ทำไมถึงออกมาแบบนี้" — ต้องมีเสมอ ไม่ใช่เฉพาะตอนพัง</summary>
        public static string OutcomeReason(CommandResult r, RunState state)
        {
            var member = state.Members[r.MemberIndex];
            var p = r.Preview;

            switch (r.Outcome)
            {
                case OutcomeKind.Refused:
                    return $"{member.Def.DisplayName} would not take the order — " +
                           $"credibility is at {Num(state.Credibility, 0)} " +
                           $"({Percent(p.RefusalChance)} refusal chance). The turn is gone.";

                case OutcomeKind.Success:
                    return p.AlwaysSucceeds
                        ? $"{CommandName(p.Type)} always works — that is what you pay the turn for."
                        : $"The roll landed inside the {Percent(p.SuccessChance)} you were shown.";

                default:
                    return $"The roll missed the {Percent(p.SuccessChance)} you were shown — " +
                           $"{DriverReason(p.Driver, member, state)}.";
            }
        }

        /// <summary>
        /// แตกผลของคำสั่งเป็นบรรทัด "ได้/เสีย"
        /// Fatigue กลับด้าน: ตัวเลขบวกคือ **แย่ลง** — ถ้าทาสีตามเครื่องหมายเฉย ๆ ผู้เล่นจะอ่านผิดทุกครั้ง
        /// </summary>
        public static List<StatDelta> CommandDeltas(CommandResult r)
        {
            var list = new List<StatDelta>(4);

            if (r.ProgressDelta != 0f)
                list.Add(new StatDelta("Progress", Signed(r.ProgressDelta) + "%", ToneUp(r.ProgressDelta)));

            if (r.MoraleDelta != 0f)
                list.Add(new StatDelta("Morale", Signed(r.MoraleDelta, 0), ToneUp(r.MoraleDelta)));

            if (r.FatigueDelta != 0f)
                list.Add(new StatDelta("Fatigue", Signed(r.FatigueDelta, 0), ToneDown(r.FatigueDelta)));

            if (r.CredibilityDelta != 0f)
                list.Add(new StatDelta("Credibility", Signed(r.CredibilityDelta, 0), ToneUp(r.CredibilityDelta)));

            return list;
        }

        public static List<StatDelta> EventDeltas(EventOutcome o)
        {
            var list = new List<StatDelta>(5);

            if (o.ProgressDelta != 0f)
                list.Add(new StatDelta("Progress", Signed(o.ProgressDelta) + "%", ToneUp(o.ProgressDelta)));

            if (o.CredibilityDelta != 0f)
                list.Add(new StatDelta("Credibility", Signed(o.CredibilityDelta, 0), ToneUp(o.CredibilityDelta)));

            if (o.DeadlineDelta != 0)
                list.Add(new StatDelta("Deadline",
                    (o.DeadlineDelta > 0 ? "+" : "") + o.DeadlineDelta + " turns", ToneUp(o.DeadlineDelta)));

            if (o.TeamMoraleDelta != 0f)
                list.Add(new StatDelta("Team Morale", Signed(o.TeamMoraleDelta, 0), ToneUp(o.TeamMoraleDelta)));

            if (o.TeamFatigueDelta != 0f)
                list.Add(new StatDelta("Team Fatigue", Signed(o.TeamFatigueDelta, 0), ToneDown(o.TeamFatigueDelta)));

            return list;
        }

        /// <summary>ค่าที่ "ยิ่งมากยิ่งดี"</summary>
        static Sentiment ToneUp(float v) => v > 0f ? Sentiment.Good : v < 0f ? Sentiment.Bad : Sentiment.Neutral;

        /// <summary>ค่าที่ "ยิ่งมากยิ่งแย่" เช่น Fatigue</summary>
        static Sentiment ToneDown(float v) => v > 0f ? Sentiment.Bad : v < 0f ? Sentiment.Good : Sentiment.Neutral;

        public static string OutcomeSummary(EventOutcome o)
        {
            string s = "";
            if (o.ProgressDelta != 0f) s += $" · Progress {Signed(o.ProgressDelta)}%";
            if (o.CredibilityDelta != 0f) s += $" · Credibility {Signed(o.CredibilityDelta, 0)}";
            if (o.DeadlineDelta != 0) s += $" · Deadline {(o.DeadlineDelta > 0 ? "+" : "")}{o.DeadlineDelta} turns";
            if (o.TeamMoraleDelta != 0f) s += $" · Team Morale {Signed(o.TeamMoraleDelta, 0)}";
            if (o.TeamFatigueDelta != 0f) s += $" · Team Fatigue {Signed(o.TeamFatigueDelta, 0)}";
            return s.TrimStart(' ', '·').TrimStart();
        }
    }
}
