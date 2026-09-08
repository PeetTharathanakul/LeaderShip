namespace LeaderShip.Model
{
    /// <summary>คำสั่งที่ผู้เล่นเลือกได้ 1 อย่างต่อเทิร์น เล็งใส่ลูกทีม 1 คนเสมอ (ADR-0004)</summary>
    public enum CommandType
    {
        Push = 0,   // สั่งเร่งงาน
        Work = 1,   // สั่งทำงาน
        Direct = 2, // กำหนดทิศทาง
        Rest = 3    // ให้พัก
    }

    /// <summary>
    /// ผลของการสั่งหนึ่งครั้ง
    /// Fail = สั่งแล้วทำไม่สำเร็จ (ยังกิน Fatigue) · Refused = ทีมไม่ทำตามตั้งแต่แรก (ไม่เกิดอะไรเลย)
    /// สองอย่างนี้ห้ามปนกัน — ดู glossary.md
    /// </summary>
    public enum OutcomeKind
    {
        Success,
        Fail,
        Refused
    }

    public enum TurnPhase
    {
        NotStarted,
        AwaitingEventChoice,
        AwaitingCommand,
        Finished
    }

    public enum RunResult
    {
        InProgress,
        Won,
        Lost
    }

    /// <summary>ใช้ให้ UI เลือกสีของบรรทัด log ไม่มีผลต่อกติกา</summary>
    public enum LogTone
    {
        Neutral,
        Good,
        Bad,
        Warning
    }
}
