using System;

namespace LeaderShip.Model
{
    /// <summary>เหตุผลหลักที่ทำให้โอกาสสำเร็จต่ำ ใช้ตอบคำถาม "ทำไมมันพัง" ให้ผู้เล่นเสมอ</summary>
    public enum ChanceDriver
    {
        None,
        Fatigue,
        Morale,
        Credibility,
        CommandRisk
    }

    /// <summary>
    /// ผลลัพธ์ที่คำนวณไว้ล่วงหน้าของคำสั่งหนึ่งช่อง (คำสั่ง x คน)
    /// UI ใช้ก้อนนี้แสดงพรีวิว และ Resolver ใช้ก้อนเดียวกันนี้ตัดสินผล
    /// **ห้ามคำนวณโอกาสสำเร็จซ้ำที่อื่น** ค่าที่โชว์ต้องเป็นค่าที่ใช้เป๊ะ ๆ ไม่งั้นผู้เล่นจะรู้สึกโดนโกง (ADR-0005)
    /// </summary>
    public struct CommandPreview
    {
        public CommandType Type;
        public int MemberIndex;

        public bool Blocked;              // Burnout: เลือกไม่ได้เลย ไม่ใช่เลือกแล้วพัง
        public bool AlwaysSucceeds;       // Rest
        public float SuccessChance;
        public float RefusalChance;

        public int ConsecutiveIfChosen;   // 1 = ไม่ซ้ำ
        public float RepeatPenalty;       // ค่าบวก = จะโดนหักเท่านี้
        public float PushTiredPenalty;
        public float CredibilityGainOnSuccess;

        public float ProgressOnSuccess;
        public float ProgressOnFail;
        public float FatigueDelta;
        public float MoraleOnSuccess;
        public float MoraleOnFail;

        public ChanceDriver Driver;

        public float TotalCredibilityCost => RepeatPenalty + PushTiredPenalty;
    }

    public struct CommandResult
    {
        public OutcomeKind Outcome;
        public CommandPreview Preview;
        public int MemberIndex;

        public float ProgressDelta;
        public float FatigueDelta;
        public float MoraleDelta;
        public float CredibilityDelta;

        public double Roll;
    }

    public static class CommandResolver
    {
        /// <summary>
        /// คำนวณทุกอย่างที่จะเกิดขึ้นถ้าเลือกช่องนี้ โดยไม่แตะ state
        /// เรียกได้บ่อยเท่าที่ต้องการ (UI เรียกทุกเฟรมได้)
        /// </summary>
        public static CommandPreview Preview(RunState state, GameContent content, int memberIndex, CommandType type)
        {
            var b = state.Balance;
            var member = state.Members[memberIndex];
            var def = content.GetCommand(type);

            var p = new CommandPreview
            {
                Type = type,
                MemberIndex = memberIndex,
                AlwaysSucceeds = def.AlwaysSucceeds,
                Blocked = member.IsBurnedOut && type != CommandType.Rest,
                RefusalChance = state.RefusalChance()
            };

            // --- โอกาสสำเร็จ ---
            float moraleTerm = (member.Morale - 50f) / b.MoraleChanceDivisor;
            float fatigueTerm = -(member.Fatigue / b.FatigueChanceDivisor);
            float credTerm = (state.Credibility - 50f) / b.CredibilityChanceDivisor;

            if (def.AlwaysSucceeds)
            {
                p.SuccessChance = 1f;
                p.Driver = ChanceDriver.None;
            }
            else
            {
                float raw = def.BaseSuccessChance + moraleTerm + fatigueTerm + credTerm;
                p.SuccessChance = RunState.Clamp(raw, b.MinSuccessChance, b.MaxSuccessChance);
                p.Driver = FindDriver(def.BaseSuccessChance, moraleTerm, fatigueTerm, credTerm);
            }

            // --- โทษเครดิต: เกิดขึ้นเพราะ "สั่ง" ไม่ใช่เพราะ "สำเร็จ/ล้มเหลว" ---
            p.ConsecutiveIfChosen = (member.LastCommand.HasValue && member.LastCommand.Value == type)
                ? member.ConsecutiveCount + 1
                : 1;

            p.RepeatPenalty = RepeatPenaltyFor(p.ConsecutiveIfChosen, b) * member.Def.RepeatPenaltyMult;

            if (type == CommandType.Push && member.Fatigue >= b.PushTiredThreshold)
                p.PushTiredPenalty = b.PushTiredPenalty;

            // --- เครดิตที่ได้คืน: แปรผกผันกับโอกาสสำเร็จ (ADR-0005) ---
            // Rest มีโอกาส 100% จึงได้ 0 โดยอัตโนมัติ ไม่ต้องเขียนกรณีพิเศษ
            p.CredibilityGainOnSuccess = (1f - p.SuccessChance) * b.CredibilityGainK;

            // --- ผลต่อค่าต่าง ๆ (คูณ Trait แล้ว) ---
            float fatigue = def.FatigueDelta;
            if (fatigue > 0f) fatigue *= member.Def.FatigueGainMult; // ตัวคูณนี้เร่งความเหนื่อย ไม่ใช่หน่วงการพัก
            p.FatigueDelta = fatigue;

            p.ProgressOnSuccess = def.ProgressOnSuccess * ProgressMultFor(type, member.Def);
            p.ProgressOnFail = def.ProgressOnFail; // Rework เป็นค่าคงที่ ไม่สเกลตามความเก่งของคน

            p.MoraleOnSuccess = ApplyMoraleTrait(type, def.MoraleDelta, member.Def);
            p.MoraleOnFail = ApplyMoraleTrait(type, def.MoraleDeltaOnFail, member.Def);

            return p;
        }

        /// <summary>ลงมือจริง — แก้ state ตาม preview ที่ผู้เล่นเห็นก่อนกด</summary>
        public static CommandResult Resolve(RunState state, GameContent content, CommandPreview preview, IRandomSource rng)
        {
            if (preview.Blocked)
                throw new InvalidOperationException("พยายามสั่งคำสั่งที่ถูกบล็อกอยู่ (Burnout) — UI ต้องกันไว้ก่อน");

            var b = state.Balance;
            var member = state.Members[preview.MemberIndex];

            var result = new CommandResult
            {
                Preview = preview,
                MemberIndex = preview.MemberIndex
            };

            // 1) ทีมยอมทำตามไหม — เช็คก่อนทุกอย่าง
            if (preview.RefusalChance > 0f)
            {
                double refusalRoll = rng.NextDouble();
                if (refusalRoll < preview.RefusalChance)
                {
                    // ถูกปฏิเสธ = คำสั่งไม่เคยลงจริง จึงไม่นับซ้ำ ไม่กินเครดิตเพิ่ม ไม่กิน Fatigue
                    // ราคาของมันคือ "เทิร์นนี้หายไป" ซึ่งแพงพออยู่แล้วเพราะเดดไลน์ยังเดิน
                    result.Outcome = OutcomeKind.Refused;
                    result.Roll = refusalRoll;
                    return result;
                }
            }

            // 2) โทษจากการสั่ง — เกิดไม่ว่าผลจะออกมาทางไหน
            float credDelta = -(preview.RepeatPenalty + preview.PushTiredPenalty);

            // 3) ทอย
            bool success;
            if (preview.AlwaysSucceeds)
            {
                success = true;
                result.Roll = 0d;
            }
            else
            {
                double roll = rng.NextDouble();
                result.Roll = roll;
                success = roll < preview.SuccessChance;
            }

            if (success)
            {
                result.Outcome = OutcomeKind.Success;
                result.ProgressDelta = preview.ProgressOnSuccess;
                result.MoraleDelta = preview.MoraleOnSuccess;
                credDelta += preview.CredibilityGainOnSuccess;
            }
            else
            {
                result.Outcome = OutcomeKind.Fail;
                result.ProgressDelta = preview.ProgressOnFail;
                result.MoraleDelta = preview.MoraleOnFail;
            }

            // Fatigue โดนเต็มจำนวนทั้งสำเร็จและล้มเหลว — "เหนื่อยฟรี เสียใจฟรี งานถอยหลัง"
            result.FatigueDelta = preview.FatigueDelta;
            result.CredibilityDelta = credDelta;

            // 4) เขียนลง state
            member.Fatigue = RunState.Clamp(member.Fatigue + result.FatigueDelta, 0f, 100f);
            member.Morale = RunState.Clamp(member.Morale + result.MoraleDelta, 0f, 100f);
            member.RefreshBurnout(b);

            state.AddProgress(result.ProgressDelta);
            state.AddCredibility(result.CredibilityDelta);

            member.LastCommand = preview.Type;
            member.ConsecutiveCount = preview.ConsecutiveIfChosen;
            state.CommandUseCount[(int)preview.Type]++;

            return result;
        }

        static float RepeatPenaltyFor(int consecutive, BalanceData b)
        {
            if (consecutive <= 1) return 0f;
            return consecutive == 2 ? b.RepeatPenaltySecond : b.RepeatPenaltyThirdPlus;
        }

        static float ProgressMultFor(CommandType type, MemberDefinition def)
        {
            switch (type)
            {
                case CommandType.Push: return def.PushProgressMult;
                case CommandType.Work: return def.WorkProgressMult;
                default: return 1f;
            }
        }

        static float ApplyMoraleTrait(CommandType type, float morale, MemberDefinition def)
        {
            switch (type)
            {
                // ใบเตยเสียขวัญหนักกว่าใครเมื่อโดนกดดัน — ตัวคูณนี้จึงจับเฉพาะฝั่งลบ
                case CommandType.Push: return morale < 0f ? morale * def.PushMoraleLossMult : morale;
                case CommandType.Direct: return morale > 0f ? morale * def.DirectMoraleMult : morale;
                default: return morale;
            }
        }

        /// <summary>ตัวไหนดึงโอกาสสำเร็จลงมากที่สุด — คำตอบของคำถาม "ทำไมมันพัง"</summary>
        static ChanceDriver FindDriver(float baseChance, float moraleTerm, float fatigueTerm, float credTerm)
        {
            ChanceDriver worst = ChanceDriver.None;
            float worstValue = 0f;

            if (fatigueTerm < worstValue) { worstValue = fatigueTerm; worst = ChanceDriver.Fatigue; }
            if (moraleTerm < worstValue) { worstValue = moraleTerm; worst = ChanceDriver.Morale; }
            if (credTerm < worstValue) { worstValue = credTerm; worst = ChanceDriver.Credibility; }

            // ไม่มีอะไรติดลบเลย แต่คำสั่งเองเสี่ยงสูง (Push base 0.45)
            if (worst == ChanceDriver.None && baseChance < 0.6f) return ChanceDriver.CommandRisk;

            return worst;
        }
    }
}
