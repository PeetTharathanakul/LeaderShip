namespace LeaderShip.Model
{
    /// <summary>
    /// ตัวแทนของ "ผู้เล่นที่เข้าใจเกม" — ไม่ใช่ AI ที่เก่งที่สุด แต่คือคนที่จับหลักได้ครบ 4 ข้อ:
    ///   1. ไม่ปล่อยให้ใคร Burnout
    ///   2. เติมกำลังใจให้คนที่ตอบสนองต่อการกำหนดทิศทางดี ก่อนที่กำลังใจจะตก
    ///   3. เร่งงานกับคนที่เร่งแล้วได้ผล และเฉพาะตอนที่เขายังไหว
    ///   4. ไม่สั่งซ้ำใส่คนเดิม
    ///
    /// ใช้เป็น "เพดานอ้างอิง" ของ Balance Simulator (ADR-0007):
    /// ถ้าตัวนี้ชนะน้อยเกินไป เกมยากเกินจนคนเล่นเก่งก็ยังแพ้ — ถ้าชนะเยอะเกินไป เกมง่ายเกิน
    /// </summary>
    public sealed class SkilledPolicy : IRunPolicy
    {
        public string Name => "Skilled";

        public void Reset(IRandomSource rng) { }

        public bool AcceptEvent(TurnEngine engine)
        {
            // รับเมื่อโอกาสพอไปวัดไปวา — เพราะการปฏิเสธก็มีราคาเสมอ ไม่มีทางเลือกฟรี
            return engine.CurrentEvent.SuccessChance >= 0.45f;
        }

        public CommandChoice ChooseCommand(TurnEngine engine)
        {
            var state = engine.State;
            var b = state.Balance;

            // 1) คนหมดสภาพต้องได้พักก่อนอย่างอื่นทั้งหมด — เขาไม่ผลิตงานเลยตอนนี้
            for (int i = 0; i < state.Members.Count; i++)
                if (state.Members[i].IsBurnedOut) return new CommandChoice(i, CommandType.Rest);

            // 2) ใกล้พังและยังมีเวลาเหลือพอให้คุ้มที่จะพัก
            if (state.TurnsRemaining > 3)
            {
                int worst = -1;
                float worstFatigue = b.PushTiredThreshold;
                for (int i = 0; i < state.Members.Count; i++)
                {
                    var m = state.Members[i];
                    if (m.Fatigue > worstFatigue && m.LastCommand != CommandType.Rest)
                    {
                        worstFatigue = m.Fatigue;
                        worst = i;
                    }
                }
                if (worst >= 0) return new CommandChoice(worst, CommandType.Rest);
            }

            // 3) เลือกช่องที่ดีที่สุดจากคะแนนรวม ไม่ใช่แค่ความคืบหน้าเฉพาะหน้า
            var choices = PolicyUtil.LegalChoices(engine);
            CommandChoice best = choices[0];
            float bestScore = float.MinValue;

            foreach (var c in choices)
            {
                var p = engine.PreviewCommand(c.MemberIndex, c.Type);
                float score = ScoreChoice(engine, p);
                if (score > bestScore) { bestScore = score; best = c; }
            }

            return best;
        }

        /// <summary>
        /// ตีมูลค่าของทุกอย่างให้เป็นหน่วยเดียวกัน คือ "ความคืบหน้าที่คาดว่าจะได้"
        /// น้ำหนักพวกนี้เป็นค่าประมาณของผู้เล่นที่เล่นเป็น ไม่ใช่ค่าที่พิสูจน์แล้วว่าเหมาะสมที่สุด
        /// </summary>
        static float ScoreChoice(TurnEngine engine, CommandPreview p)
        {
            var state = engine.State;
            int horizon = state.TurnsRemaining;

            float score = PolicyUtil.ExpectedProgress(p);

            float expectedMorale = (p.SuccessChance * p.MoraleOnSuccess) + ((1f - p.SuccessChance) * p.MoraleOnFail);

            // กำลังใจและความเหนื่อยล้ามีค่าก็ต่อเมื่อยังมีเทิร์นเหลือให้ใช้ประโยชน์
            float horizonWeight = horizon >= 6 ? 1f : horizon / 6f;

            score += expectedMorale * 0.16f * horizonWeight;
            score += -p.FatigueDelta * 0.12f * horizonWeight;

            // เครดิตมีค่ามากเป็นพิเศษตอนใกล้เขตที่ทีมเริ่มปฏิเสธคำสั่ง
            float credUrgency = state.Credibility < 40f ? 0.25f : 0.08f;
            float expectedCred = (p.SuccessChance * p.CredibilityGainOnSuccess) - p.TotalCredibilityCost;
            score += expectedCred * credUrgency;

            return score;
        }
    }
}
