namespace LeaderShip.Model
{
    public struct EventPreview
    {
        public EventDefinition Def;
        public float SuccessChance;
        public float CredibilityGainOnSuccess;

        /// <summary>ใบเตือน: ไม่มีทางเลือก แค่ประกาศว่าอีกกี่เทิร์นเดดไลน์จะหด</summary>
        public bool IsWarning;
    }

    public struct EventResult
    {
        public EventPreview Preview;
        public bool Accepted;
        public bool Succeeded;
        public EventOutcome Applied;
        public float CredibilityGain;
        public double Roll;
    }

    public static class EventResolver
    {
        /// <summary>
        /// โอกาสสำเร็จของ Event ใช้สูตรรูปร่างเดียวกับคำสั่ง แต่คิดจากค่าเฉลี่ยทั้งทีม
        /// จงใจให้เหมือนกัน: ผู้เล่นเรียนสูตรเดียวแล้วใช้ได้ทั้งเกม (Clarity)
        /// และทำให้การลงทุนกับทีมมีผลกับ Event ด้วย ไม่ใช่แค่กับคำสั่ง
        /// </summary>
        public static EventPreview Preview(RunState state, EventDefinition def)
        {
            var b = state.Balance;

            var p = new EventPreview
            {
                Def = def,
                IsWarning = def.WarningLeadTurns > 0
            };

            if (p.IsWarning)
            {
                p.SuccessChance = 1f;
                return p;
            }

            float moraleTerm = (state.TeamAverageMorale - 50f) / b.MoraleChanceDivisor;
            float fatigueTerm = -(state.TeamAverageFatigue / b.FatigueChanceDivisor);
            float credTerm = (state.Credibility - 50f) / b.CredibilityChanceDivisor;

            float raw = def.BaseSuccessChance + moraleTerm + fatigueTerm + credTerm;
            p.SuccessChance = RunState.Clamp(raw, b.MinSuccessChance, b.MaxSuccessChance);
            p.CredibilityGainOnSuccess = (1f - p.SuccessChance) * b.CredibilityGainK;

            return p;
        }

        public static EventResult Resolve(RunState state, EventPreview preview, bool accept, IRandomSource rng)
        {
            var result = new EventResult { Preview = preview, Accepted = accept };

            if (!accept)
            {
                // "ไม่รับ" ต้องมีราคาเสมอ ไม่งั้นระบบ Event กลายเป็นปุ่ม "ไม่" ที่กดรัว ๆ (ADR-0005)
                result.Applied = preview.Def.OnDecline;
                Apply(state, result.Applied);
                return result;
            }

            double roll = rng.NextDouble();
            result.Roll = roll;
            result.Succeeded = roll < preview.SuccessChance;

            result.Applied = result.Succeeded ? preview.Def.OnAcceptSuccess : preview.Def.OnAcceptFail;
            Apply(state, result.Applied);

            if (result.Succeeded)
            {
                // กติกาเดียวกับคำสั่ง: สำเร็จเรื่องที่ดูยาก = ได้ความนับถือ
                result.CredibilityGain = preview.CredibilityGainOnSuccess;
                state.AddCredibility(result.CredibilityGain);
            }

            return result;
        }

        static void Apply(RunState state, EventOutcome o)
        {
            if (o == null) return;

            state.AddProgress(o.ProgressDelta);
            state.AddCredibility(o.CredibilityDelta);

            if (o.DeadlineDelta != 0)
            {
                state.TurnsRemaining += o.DeadlineDelta;
                if (state.TurnsRemaining < 0) state.TurnsRemaining = 0;
            }

            if (o.TeamFatigueDelta != 0f || o.TeamMoraleDelta != 0f)
            {
                for (int i = 0; i < state.Members.Count; i++)
                {
                    var m = state.Members[i];
                    m.Fatigue = RunState.Clamp(m.Fatigue + o.TeamFatigueDelta, 0f, 100f);
                    m.Morale = RunState.Clamp(m.Morale + o.TeamMoraleDelta, 0f, 100f);
                    m.RefreshBurnout(state.Balance);
                }
            }
        }
    }
}
