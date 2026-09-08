namespace LeaderShip.Model
{
    /// <summary>
    /// สรุปตอนจบรอบ
    ///
    /// **นี่ไม่ใช่ระบบวัดผลการเรียนรู้** โปรเจคนี้เป็นงาน portfolio ไม่มี assessment layer (ADR-0002 / docs/README.md)
    /// มันมีหน้าที่เดียวคือสะท้อนให้ผู้เล่นเห็นว่าตัวเองนำแบบไหน ซึ่งเป็นการปิดรอบที่ทำให้อยากเล่นซ้ำ
    /// ห้ามให้มันโตขึ้นเป็นระบบให้คะแนนคน
    /// </summary>
    public static class RunSummary
    {
        public static CommandType MostUsedCommand(RunState state)
        {
            int bestIndex = 0;
            for (int i = 1; i < state.CommandUseCount.Length; i++)
                if (state.CommandUseCount[i] > state.CommandUseCount[bestIndex]) bestIndex = i;

            return (CommandType)bestIndex;
        }

        public static string StyleLabel(RunState state)
        {
            int total = 0;
            foreach (int c in state.CommandUseCount) total += c;
            if (total == 0) return "You never gave an order";

            switch (MostUsedCommand(state))
            {
                case CommandType.Push: return "You led by pushing";
                case CommandType.Work: return "You led by assigning";
                case CommandType.Direct: return "You led by giving direction";
                default: return "You led by looking after people";
            }
        }

        public static string CommandBreakdown(RunState state)
        {
            return $"Push {state.CommandUseCount[(int)CommandType.Push]} · " +
                   $"Work {state.CommandUseCount[(int)CommandType.Work]} · " +
                   $"Direct {state.CommandUseCount[(int)CommandType.Direct]} · " +
                   $"Rest {state.CommandUseCount[(int)CommandType.Rest]}";
        }

        public static string Headline(RunState state)
        {
            return state.Result == RunResult.Won
                ? $"Delivered on time — {state.TurnsRemaining} turns to spare"
                : $"Missed the deadline — the work reached {GameText.Num(state.Progress)}%";
        }
    }
}
