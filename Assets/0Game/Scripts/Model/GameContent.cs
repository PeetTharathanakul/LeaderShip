using System;

namespace LeaderShip.Model
{
    /// <summary>
    /// ค่าบาลานซ์ทั้งหมดของเกม — ทุกตัวเป็น "Starting value" ตาม Numbers Policy
    /// ค่าจริงมาจาก BalanceConfigSO (ADR-0001: ห้าม hardcode) ค่า default ที่นี่คือชุดใน docs/balance.md
    /// </summary>
    [Serializable]
    public sealed class BalanceData
    {
        // --- เริ่มต้นรอบเล่น ---
        public float ProgressGoal = 100f;
        public int StartTurns = 18;
        public float StartCredibility = 50f;
        public float StartFatigue = 10f;
        public float StartMorale = 60f;

        // --- Output พื้นฐาน: Output = baseOutput * moraleFactor * fatigueFactor ---
        public float MoraleFactorBase = 0.6f;
        public float MoraleFactorDivisor = 125f;
        public float FatigueFactorDivisor = 125f;

        // --- โอกาสสำเร็จ ---
        public float MoraleChanceDivisor = 250f;   // (Morale - 50) / 250  -> +-0.20
        public float FatigueChanceDivisor = 250f;  // -Fatigue / 250       ->  0 .. -0.40
        public float CredibilityChanceDivisor = 500f; // (Cred - 50) / 500 -> +-0.10
        public float MinSuccessChance = 0.05f;
        public float MaxSuccessChance = 0.95f;

        // --- Credibility ---
        public float CredibilityGainK = 20f;       // gain = (1 - p) * K
        public float RepeatPenaltySecond = 6f;
        public float RepeatPenaltyThirdPlus = 14f;
        public float PushTiredThreshold = 70f;
        public float PushTiredPenalty = 10f;

        // --- ปฏิเสธคำสั่ง ---
        public float RefusalThreshold = 25f;
        public float RefusalChancePerPoint = 0.02f;

        // --- Burnout ---
        public float BurnoutEnter = 100f;
        public float BurnoutExit = 70f;

        // --- Event ---
        public float EventChancePerTurn = 0.35f;
        public int EventFirstEligibleTurn = 2;  // เทิร์น 1 สงวนไว้ให้ผู้เล่นเรียนลูปพื้นฐาน
        public int EventMinGapTurns = 1;        // ห้ามเกิดติดกันสองเทิร์น

        public BalanceData Clone() => (BalanceData)MemberwiseClone();
    }

    /// <summary>
    /// Trait = โปรไฟล์ตัวคูณต่อคำสั่งแต่ละแบบ แกนเดียว ไม่ใช่ถุงคุณสมบัติ (ADR-0004)
    /// ถ้าจะเพิ่มความต่างของลูกทีม ให้เพิ่มตัวคูณในนี้ ห้ามเพิ่มความสามารถพิเศษ
    /// </summary>
    [Serializable]
    public sealed class MemberDefinition
    {
        public string Id = "member";
        public string DisplayName = "Team Member";
        public string Nickname = "";        // ประโยคที่ผู้เล่นควรสรุปได้เอง เช่น "อย่าพูดเยอะ บอกมาว่าให้ทำอะไร"

        public float BaseOutput = 1.0f;

        public float PushProgressMult = 1f;
        public float PushMoraleLossMult = 1f;
        public float WorkProgressMult = 1f;
        public float DirectMoraleMult = 1f;
        public float FatigueGainMult = 1f;
        public float RepeatPenaltyMult = 1f;
    }

    /// <summary>ผลของคำสั่งหนึ่งชนิด ก่อนคูณ Trait</summary>
    [Serializable]
    public sealed class CommandDefinition
    {
        public CommandType Type = CommandType.Work;
        public string DisplayName = "Command";
        public string Description = "";

        /// <summary>Rest ไม่ทอยลูกเต๋า (ADR-0006) — เป็นทางออกที่การันตีของผู้เล่น</summary>
        public bool AlwaysSucceeds = false;

        public float BaseSuccessChance = 0.7f;

        // ผลเมื่อสำเร็จ
        public float ProgressOnSuccess = 0f;
        public float FatigueDelta = 0f;
        public float MoraleDelta = 0f;

        // ผลเมื่อล้มเหลว — Fatigue/Morale ยังโดนเต็มจำนวนเสมอ "เหนื่อยฟรี เสียใจฟรี"
        public float ProgressOnFail = 0f;    // ติดลบ = Rework
        public float MoraleDeltaOnFail = 0f; // ใช้แทน MoraleDelta เมื่อล้มเหลว
    }

    [Serializable]
    public sealed class EventOutcome
    {
        public float ProgressDelta = 0f;
        public float CredibilityDelta = 0f;
        public int DeadlineDelta = 0;        // ติดลบ = เดดไลน์หด
        public float TeamFatigueDelta = 0f;
        public float TeamMoraleDelta = 0f;
        public string Text = "";
    }

    /// <summary>
    /// Event ทุกใบต้องเขียนผลครบ 3 ทาง: รับ-สำเร็จ / รับ-ล้มเหลว / ไม่รับ (ADR-0005)
    /// "ไม่รับ" ห้ามว่าง — ถ้าปฏิเสธแล้วฟรี ระบบ Event ทั้งระบบจะกลายเป็นปุ่ม "ไม่" ที่กดรัว ๆ
    /// </summary>
    [Serializable]
    public sealed class EventDefinition
    {
        public string Id = "event";
        public string Title = "";
        public string Body = "";

        public float BaseSuccessChance = 0.55f;

        public EventOutcome OnAcceptSuccess = new EventOutcome();
        public EventOutcome OnAcceptFail = new EventOutcome();
        public EventOutcome OnDecline = new EventOutcome();

        /// <summary>
        /// ถ้า &gt; 0 ใบนี้เป็น "ใบเตือน": ไม่มีทางเลือก แต่ประกาศล่วงหน้าว่าอีกกี่เทิร์นเดดไลน์จะหด
        /// นี่คือทางเดียวที่เดดไลน์หดโดยผู้เล่นไม่ได้เลือกเอง และมันต้องเตือนก่อนเสมอ (ADR-0003)
        /// </summary>
        public int WarningLeadTurns = 0;
        public int WarningDeadlineCut = 0;
    }

    /// <summary>คอนเทนต์ทั้งหมดของเกมในรูปแบบที่ชั้นโมเดลใช้ได้ ไม่พึ่ง Unity</summary>
    public sealed class GameContent
    {
        public BalanceData Balance;
        public MemberDefinition[] Members;
        public CommandDefinition[] Commands; // เรียงตาม (int)CommandType เสมอ
        public EventDefinition[] Events;

        public CommandDefinition GetCommand(CommandType type)
        {
            for (int i = 0; i < Commands.Length; i++)
                if (Commands[i].Type == type) return Commands[i];

            throw new InvalidOperationException($"ไม่พบนิยามของคำสั่ง {type} ใน GameContent");
        }

        public void Validate()
        {
            if (Balance == null) throw new InvalidOperationException("GameContent.Balance เป็น null");
            if (Members == null || Members.Length == 0) throw new InvalidOperationException("GameContent ไม่มีลูกทีมเลย");
            if (Commands == null || Commands.Length == 0) throw new InvalidOperationException("GameContent ไม่มีคำสั่งเลย");

            foreach (CommandType t in (CommandType[])Enum.GetValues(typeof(CommandType)))
                GetCommand(t); // โยน exception ถ้าขาด

            if (Events == null) Events = Array.Empty<EventDefinition>();
        }
    }
}
