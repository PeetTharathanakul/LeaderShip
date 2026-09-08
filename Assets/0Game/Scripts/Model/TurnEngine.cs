using System;
using System.Collections.Generic;

namespace LeaderShip.Model
{
    public struct LogLine
    {
        public string Text;
        public LogTone Tone;
        public int Turn;

        public LogLine(int turn, string text, LogTone tone)
        {
            Turn = turn;
            Text = text;
            Tone = tone;
        }
    }

    /// <summary>
    /// เครื่องยนต์ของรอบเล่น — ถือลำดับเฟสทั้งหมดไว้ที่เดียว
    ///
    ///   BeginTurn -> [AwaitingEventChoice] -> AwaitingCommand -> EndTurn -> BeginTurn -> ...
    ///
    /// ไม่พึ่ง Unity เลย (ADR-0007) จึงรันใน simulator ได้หลายหมื่นรอบต่อวินาที
    /// </summary>
    public sealed class TurnEngine
    {
        public readonly GameContent Content;
        public readonly RunState State;

        readonly IRandomSource _rng;
        readonly List<LogLine> _log = new List<LogLine>();
        readonly List<EventDefinition> _eventPool = new List<EventDefinition>();

        public TurnPhase Phase { get; private set; } = TurnPhase.NotStarted;
        public EventPreview CurrentEvent { get; private set; }
        public bool HasEvent { get; private set; }

        public IReadOnlyList<LogLine> Log => _log;

        /// <summary>ยิงทุกครั้งที่ state เปลี่ยนพอที่ UI ควรวาดใหม่ — View ห้าม poll ทุกเฟรม</summary>
        public event Action Changed;

        /// <summary>ยิงเมื่อรอบจบ ส่งผลลัพธ์ไปให้หน้าสรุป</summary>
        public event Action<RunResult> Finished;

        /// <summary>
        /// ยิงทันทีที่คำสั่งถูกตัดสิน **ก่อน** เทิร์นจะเดินต่อ
        /// มีไว้ให้ชั้นนำเสนอเล่นอนิเมชัน/ป๊อปอัปสรุปได้ — ไม่มีกติกาผูกกับมัน
        /// ใครไม่ฟังก็ไม่มีอะไรเปลี่ยน เกมยังเดินเหมือนเดิมทุกประการ
        /// </summary>
        public event Action<CommandResult> CommandResolved;

        /// <summary>ยิงทันทีที่เหตุการณ์ถูกตัดสิน ด้วยเหตุผลเดียวกับ <see cref="CommandResolved"/></summary>
        public event Action<EventResult> EventResolved;

        public TurnEngine(GameContent content, IRandomSource rng)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));

            Content.Validate();
            State = new RunState(Content);
        }

        public void Start()
        {
            RefillEventPool();
            AddLog("New project, team ready — get it to 100% before the deadline runs out.", LogTone.Neutral);
            BeginTurn();
        }

        // ------------------------------------------------------------------ เฟส

        void BeginTurn()
        {
            State.TurnNumber++;
            HasEvent = false;

            TickPendingDeadlineCut();
            if (State.IsOver) return;

            TryRollEvent();

            if (!HasEvent)
                Phase = TurnPhase.AwaitingCommand;

            RaiseChanged();
        }

        void TickPendingDeadlineCut()
        {
            if (!State.PendingCut.IsActive) return;

            State.PendingCut.TurnsRemaining--;
            if (State.PendingCut.TurnsRemaining > 0) return;

            int amount = State.PendingCut.Amount;
            string reason = State.PendingCut.Reason;
            State.PendingCut = default;

            State.TurnsRemaining -= amount;
            if (State.TurnsRemaining < 0) State.TurnsRemaining = 0;

            AddLog($"{reason} — the deadline lost {amount} turns, {State.TurnsRemaining} left.", LogTone.Bad);

            CheckLoss();
        }

        void TryRollEvent()
        {
            var b = State.Balance;

            if (State.TurnNumber < b.EventFirstEligibleTurn) return;
            if (State.TurnNumber - State.LastEventTurn <= b.EventMinGapTurns) return;
            if (_eventPool.Count == 0) RefillEventPool();
            if (_eventPool.Count == 0) return;

            if (_rng.NextDouble() >= b.EventChancePerTurn) return;

            int index = _rng.NextInt(0, _eventPool.Count);
            var def = _eventPool[index];
            _eventPool.RemoveAt(index);

            State.LastEventTurn = State.TurnNumber;

            var preview = EventResolver.Preview(State, def);

            if (preview.IsWarning)
            {
                // ใบเตือนไม่มีทางเลือก — หน้าที่เดียวของมันคือทำให้เดดไลน์ที่จะหด "อธิบายได้" (ADR-0003)
                State.PendingCut = new PendingDeadlineCut
                {
                    TurnsRemaining = def.WarningLeadTurns,
                    Amount = def.WarningDeadlineCut,
                    Reason = def.Title
                };

                AddLog($"[!] {def.Title} — {def.Body} (in {def.WarningLeadTurns} turns the deadline loses {def.WarningDeadlineCut})",
                    LogTone.Warning);
                return;
            }

            CurrentEvent = preview;
            HasEvent = true;
            Phase = TurnPhase.AwaitingEventChoice;
        }

        public void ChooseEvent(bool accept)
        {
            if (Phase != TurnPhase.AwaitingEventChoice)
                throw new InvalidOperationException($"ChooseEvent ถูกเรียกตอนเฟสเป็น {Phase}");

            var result = EventResolver.Resolve(State, CurrentEvent, accept, _rng);
            LogEvent(result);
            EventResolved?.Invoke(result);

            HasEvent = false;
            Phase = TurnPhase.AwaitingCommand;

            // Event หนึ่งใบทำให้แพ้/ชนะกลางเทิร์นได้ ต้องเช็คก่อนปล่อยให้สั่งต่อ
            if (CheckWin() || CheckLoss()) return;

            RaiseChanged();
        }

        public CommandPreview PreviewCommand(int memberIndex, CommandType type)
            => CommandResolver.Preview(State, Content, memberIndex, type);

        public void ExecuteCommand(int memberIndex, CommandType type)
        {
            if (Phase != TurnPhase.AwaitingCommand)
                throw new InvalidOperationException($"ExecuteCommand ถูกเรียกตอนเฟสเป็น {Phase}");

            var preview = PreviewCommand(memberIndex, type);
            if (preview.Blocked)
                throw new InvalidOperationException("คำสั่งนี้ถูกบล็อกอยู่ (Burnout) — UI ต้องกันไม่ให้กดตั้งแต่แรก");

            var result = CommandResolver.Resolve(State, Content, preview, _rng);
            AddLog(GameText.CommandLine(result, State), ToneFor(result.Outcome));
            CommandResolved?.Invoke(result);

            WarnIfCredibilityLow();
            WarnIfBurnedOut(memberIndex);

            EndTurn();
        }

        void EndTurn()
        {
            // ทีมผลิตงานเองทุกเทิร์น (ADR-0004) คิดหลังคำสั่ง เพื่อให้ผลของคำสั่งเห็นผลทันทีในเทิร์นเดียวกัน
            float output = State.TeamOutput();
            if (output > 0f)
            {
                State.AddProgress(output);
                AddLog($"The team moved the work on its own {GameText.Signed(output)}%", LogTone.Neutral);
            }
            else
            {
                AddLog("The team produced nothing this turn — everyone is burned out.", LogTone.Bad);
            }

            if (CheckWin()) return;

            State.TurnsRemaining--;
            if (CheckLoss()) return;

            BeginTurn();
        }

        // ------------------------------------------------------------------ จบรอบ

        bool CheckWin()
        {
            if (State.Progress < State.Balance.ProgressGoal) return false;

            State.Result = RunResult.Won;
            Phase = TurnPhase.Finished;
            AddLog($"Delivered! {State.TurnsRemaining} turns to spare.", LogTone.Good);
            RaiseChanged();
            Finished?.Invoke(RunResult.Won);
            return true;
        }

        bool CheckLoss()
        {
            if (State.TurnsRemaining > 0 || State.Progress >= State.Balance.ProgressGoal) return false;

            State.Result = RunResult.Lost;
            Phase = TurnPhase.Finished;
            AddLog($"Out of time — the work reached {GameText.Num(State.Progress)}% of 100%.", LogTone.Bad);
            RaiseChanged();
            Finished?.Invoke(RunResult.Lost);
            return true;
        }

        // ------------------------------------------------------------------ log

        void LogEvent(EventResult r)
        {
            string title = r.Preview.Def.Title;

            if (!r.Accepted)
            {
                AddLog($"Declined: {title} — {r.Applied.Text} {GameText.OutcomeSummary(r.Applied)}", LogTone.Warning);
                return;
            }

            if (r.Succeeded)
            {
                string gain = r.CredibilityGain > 0.5f
                    ? $" · Credibility {GameText.Signed(r.CredibilityGain, 0)}"
                    : "";
                AddLog($"Accepted: {title} — success ({GameText.Percent(r.Preview.SuccessChance)} chance) — " +
                       $"{r.Applied.Text} {GameText.OutcomeSummary(r.Applied)}{gain}", LogTone.Good);
            }
            else
            {
                AddLog($"Accepted: {title} — failed ({GameText.Percent(r.Preview.SuccessChance)} chance) — " +
                       $"{r.Applied.Text} {GameText.OutcomeSummary(r.Applied)}", LogTone.Bad);
            }
        }

        void WarnIfCredibilityLow()
        {
            if (State.Credibility >= State.Balance.RefusalThreshold) return;

            AddLog($"The team is starting to doubt your orders — the next one has a {GameText.Percent(State.RefusalChance())} chance of being refused.",
                LogTone.Warning);
        }

        void WarnIfBurnedOut(int memberIndex)
        {
            var m = State.Members[memberIndex];
            if (!m.IsBurnedOut) return;

            AddLog($"{m.Def.DisplayName} is burned out — no output and no orders accepted until rest brings Fatigue below {GameText.Num(State.Balance.BurnoutExit, 0)}.",
                LogTone.Bad);
        }

        static LogTone ToneFor(OutcomeKind kind)
        {
            switch (kind)
            {
                case OutcomeKind.Success: return LogTone.Good;
                case OutcomeKind.Fail: return LogTone.Bad;
                default: return LogTone.Warning;
            }
        }

        void AddLog(string text, LogTone tone) => _log.Add(new LogLine(State.TurnNumber, text, tone));

        void RaiseChanged() => Changed?.Invoke();

        void RefillEventPool()
        {
            _eventPool.Clear();
            if (Content.Events == null) return;
            _eventPool.AddRange(Content.Events);
        }
    }
}
