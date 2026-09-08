using System;
using System.Collections.Generic;

namespace LeaderShip.Model
{
    /// <summary>สถานะของลูกทีมหนึ่งคน</summary>
    public sealed class MemberState
    {
        public readonly MemberDefinition Def;

        public float Fatigue;
        public float Morale;

        /// <summary>คำสั่งล่าสุดที่สั่งใส่คนนี้ ใช้นับ "สั่งซ้ำ" ต่อรายคน (ADR-0005)</summary>
        public CommandType? LastCommand;

        /// <summary>สั่งคำสั่งเดิมใส่คนนี้ติดกันมาแล้วกี่ครั้ง (1 = ครั้งแรก)</summary>
        public int ConsecutiveCount;

        public bool IsBurnedOut;

        public MemberState(MemberDefinition def, BalanceData balance)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            Fatigue = balance.StartFatigue;
            Morale = balance.StartMorale;
            LastCommand = null;
            ConsecutiveCount = 0;
            IsBurnedOut = false;
        }

        /// <summary>ผลงานที่คนนี้ผลิตเองต่อเทิร์นโดยไม่ต้องสั่ง (ADR-0004)</summary>
        public float Output(BalanceData b)
        {
            if (IsBurnedOut) return 0f;

            float moraleFactor = b.MoraleFactorBase + (Morale / b.MoraleFactorDivisor);
            float fatigueFactor = 1f - (Fatigue / b.FatigueFactorDivisor);

            if (moraleFactor < 0f) moraleFactor = 0f;
            if (fatigueFactor < 0f) fatigueFactor = 0f;

            return Def.BaseOutput * moraleFactor * fatigueFactor;
        }

        /// <summary>
        /// อัปเดตสถานะ Burnout ตามค่า Fatigue ปัจจุบัน
        /// เข้าเมื่อแตะ 100 ออกเมื่อต่ำกว่า 70 — ช่องว่างนี้ตั้งใจ กัน flicker ไปมารอบ ๆ เส้นเดียว
        /// </summary>
        public void RefreshBurnout(BalanceData b)
        {
            if (!IsBurnedOut && Fatigue >= b.BurnoutEnter) IsBurnedOut = true;
            else if (IsBurnedOut && Fatigue < b.BurnoutExit) IsBurnedOut = false;
        }
    }

    /// <summary>เดดไลน์ที่ประกาศล่วงหน้าแล้วว่าจะหด — ยังไม่เกิด แต่ผู้เล่นเห็นแล้ว</summary>
    public struct PendingDeadlineCut
    {
        public int TurnsRemaining;
        public int Amount;
        public string Reason;

        public bool IsActive => Amount > 0;
    }

    public sealed class RunState
    {
        public readonly BalanceData Balance;
        public readonly List<MemberState> Members = new List<MemberState>();

        public float Progress;
        public float Credibility;
        public int TurnsRemaining;
        public int TurnNumber;

        public RunResult Result = RunResult.InProgress;
        public PendingDeadlineCut PendingCut;

        /// <summary>เทิร์นล่าสุดที่มี Event เกิด ใช้บังคับระยะห่างขั้นต่ำ</summary>
        public int LastEventTurn = -99;

        /// <summary>นับจำนวนครั้งที่ใช้แต่ละคำสั่ง ใช้สรุปสไตล์การนำตอนจบรอบ</summary>
        public readonly int[] CommandUseCount = new int[4];

        public RunState(GameContent content)
        {
            content.Validate();
            Balance = content.Balance;

            foreach (var def in content.Members)
                Members.Add(new MemberState(def, Balance));

            Progress = 0f;
            Credibility = Balance.StartCredibility;
            TurnsRemaining = Balance.StartTurns;
            TurnNumber = 0;
        }

        public bool IsOver => Result != RunResult.InProgress;

        public float TeamAverageMorale
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < Members.Count; i++) sum += Members[i].Morale;
                return Members.Count == 0 ? 0f : sum / Members.Count;
            }
        }

        public float TeamAverageFatigue
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < Members.Count; i++) sum += Members[i].Fatigue;
                return Members.Count == 0 ? 0f : sum / Members.Count;
            }
        }

        /// <summary>ผลงานรวมที่ทีมผลิตเองในเทิร์นนี้</summary>
        public float TeamOutput()
        {
            float sum = 0f;
            for (int i = 0; i < Members.Count; i++) sum += Members[i].Output(Balance);
            return sum;
        }

        /// <summary>โอกาสที่คำสั่งจะถูกปฏิเสธเพราะเครดิตต่ำ (ADR-0005)</summary>
        public float RefusalChance()
        {
            if (Credibility >= Balance.RefusalThreshold) return 0f;
            float chance = (Balance.RefusalThreshold - Credibility) * Balance.RefusalChancePerPoint;
            return Clamp01(chance);
        }

        public void AddCredibility(float delta)
        {
            Credibility = Clamp(Credibility + delta, 0f, 100f);
        }

        public void AddProgress(float delta)
        {
            Progress = Clamp(Progress + delta, 0f, Balance.ProgressGoal);
        }

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
    }
}
