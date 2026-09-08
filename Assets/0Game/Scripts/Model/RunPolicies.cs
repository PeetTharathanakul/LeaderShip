using System;
using System.Collections.Generic;

namespace LeaderShip.Model
{
    public struct CommandChoice
    {
        public int MemberIndex;
        public CommandType Type;

        public CommandChoice(int memberIndex, CommandType type)
        {
            MemberIndex = memberIndex;
            Type = type;
        }
    }

    /// <summary>
    /// นโยบายการเล่นอัตโนมัติ ใช้โดย Balance Simulator (ADR-0007)
    /// เป้าหมายไม่ใช่ทำ AI ที่เก่ง แต่คือ **ไล่หาสูตรง่าย ๆ ที่ชนะได้บ่อยเกินไป**
    /// ถ้ามีตัวไหนชนะเกินเกณฑ์ แปลว่าเกมยังมีสูตรตายตัวอยู่ ต้องแก้กติกาหรือตัวเลข
    /// </summary>
    public interface IRunPolicy
    {
        string Name { get; }
        void Reset(IRandomSource rng);
        bool AcceptEvent(TurnEngine engine);
        CommandChoice ChooseCommand(TurnEngine engine);
    }

    public static class PolicyUtil
    {
        public static List<CommandChoice> LegalChoices(TurnEngine engine)
        {
            var list = new List<CommandChoice>();
            var state = engine.State;

            for (int m = 0; m < state.Members.Count; m++)
            {
                foreach (CommandType t in AllCommands)
                {
                    if (!engine.PreviewCommand(m, t).Blocked)
                        list.Add(new CommandChoice(m, t));
                }
            }

            // เป็นไปไม่ได้ในทางปฏิบัติ เพราะ Rest ไม่เคยถูกบล็อก แต่กันไว้ให้ simulator ไม่ล้ม
            if (list.Count == 0) list.Add(new CommandChoice(0, CommandType.Rest));
            return list;
        }

        public static readonly CommandType[] AllCommands =
        {
            CommandType.Push, CommandType.Work, CommandType.Direct, CommandType.Rest
        };

        public static int LeastTiredMember(RunState state)
        {
            int best = 0;
            float bestFatigue = float.MaxValue;
            for (int i = 0; i < state.Members.Count; i++)
            {
                var m = state.Members[i];
                if (m.IsBurnedOut) continue;
                if (m.Fatigue < bestFatigue) { bestFatigue = m.Fatigue; best = i; }
            }
            return best;
        }

        public static int MostTiredMember(RunState state)
        {
            int best = 0;
            float worst = float.MinValue;
            for (int i = 0; i < state.Members.Count; i++)
            {
                if (state.Members[i].Fatigue > worst) { worst = state.Members[i].Fatigue; best = i; }
            }
            return best;
        }

        /// <summary>ความคืบหน้าที่คาดว่าจะได้จากคำสั่งนี้ในเทิร์นนี้ (ไม่มองอนาคต)</summary>
        public static float ExpectedProgress(CommandPreview p)
        {
            float landed = 1f - p.RefusalChance;
            return landed * ((p.SuccessChance * p.ProgressOnSuccess) + ((1f - p.SuccessChance) * p.ProgressOnFail));
        }
    }

    public sealed class AlwaysCommandPolicy : IRunPolicy
    {
        readonly CommandType _type;
        public string Name { get; }

        public AlwaysCommandPolicy(CommandType type, string name)
        {
            _type = type;
            Name = name;
        }

        public void Reset(IRandomSource rng) { }
        public bool AcceptEvent(TurnEngine engine) => engine.CurrentEvent.SuccessChance >= 0.5f;

        public CommandChoice ChooseCommand(TurnEngine engine)
        {
            int m = PolicyUtil.LeastTiredMember(engine.State);
            var choice = new CommandChoice(m, _type);
            return engine.PreviewCommand(m, _type).Blocked ? new CommandChoice(m, CommandType.Rest) : choice;
        }
    }

    /// <summary>วนคำสั่งอย่างเดียว ไม่สนว่าสั่งใคร — นี่คือสูตรที่กติกา "นับซ้ำต่อรายคน" ตั้งใจฆ่า</summary>
    public sealed class RoundRobinCommandPolicy : IRunPolicy
    {
        int _i;
        public string Name => "RoundRobinCommand";
        public void Reset(IRandomSource rng) => _i = 0;
        public bool AcceptEvent(TurnEngine engine) => engine.CurrentEvent.SuccessChance >= 0.5f;

        public CommandChoice ChooseCommand(TurnEngine engine)
        {
            var type = PolicyUtil.AllCommands[_i % 3]; // Push/Work/Direct วน 3 จังหวะ
            _i++;
            int m = PolicyUtil.LeastTiredMember(engine.State);
            return engine.PreviewCommand(m, type).Blocked
                ? new CommandChoice(m, CommandType.Rest)
                : new CommandChoice(m, type);
        }
    }

    /// <summary>วนทั้งคำสั่งและคน — สูตรที่เหลืออยู่หลังปิดรู A ถ้าตัวนี้ชนะเยอะแปลว่ายังปิดไม่สนิท</summary>
    public sealed class RoundRobinBothPolicy : IRunPolicy
    {
        int _i;
        public string Name => "RoundRobinBoth";
        public void Reset(IRandomSource rng) => _i = 0;
        public bool AcceptEvent(TurnEngine engine) => engine.CurrentEvent.SuccessChance >= 0.5f;

        public CommandChoice ChooseCommand(TurnEngine engine)
        {
            int memberCount = engine.State.Members.Count;
            var type = PolicyUtil.AllCommands[_i % 3];
            int m = _i % memberCount;
            _i++;
            return engine.PreviewCommand(m, type).Blocked
                ? new CommandChoice(m, CommandType.Rest)
                : new CommandChoice(m, type);
        }
    }

    /// <summary>เลือกช่องที่ได้ความคืบหน้าคาดหวังสูงสุดในเทิร์นนี้ — ผู้เล่นที่คิดเลขเป็นแต่ไม่มองยาว</summary>
    public sealed class GreedyEvPolicy : IRunPolicy
    {
        public string Name => "GreedyEV";
        public void Reset(IRandomSource rng) { }
        public bool AcceptEvent(TurnEngine engine) => engine.CurrentEvent.SuccessChance >= 0.5f;

        public CommandChoice ChooseCommand(TurnEngine engine)
        {
            var choices = PolicyUtil.LegalChoices(engine);
            CommandChoice best = choices[0];
            float bestScore = float.MinValue;

            foreach (var c in choices)
            {
                var p = engine.PreviewCommand(c.MemberIndex, c.Type);
                float score = PolicyUtil.ExpectedProgress(p);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }
    }

    /// <summary>ทำงานปกติ แล้วพักคนที่เหนื่อยเกินเกณฑ์ — สิ่งที่ผู้เล่นทั่วไปน่าจะทำโดยสัญชาตญาณ</summary>
    public sealed class RestWhenTiredPolicy : IRunPolicy
    {
        readonly float _threshold;
        public string Name => "RestWhenTired";

        public RestWhenTiredPolicy(float threshold = 60f) { _threshold = threshold; }

        public void Reset(IRandomSource rng) { }
        public bool AcceptEvent(TurnEngine engine) => engine.CurrentEvent.SuccessChance >= 0.5f;

        public CommandChoice ChooseCommand(TurnEngine engine)
        {
            int tired = PolicyUtil.MostTiredMember(engine.State);
            if (engine.State.Members[tired].Fatigue >= _threshold)
                return new CommandChoice(tired, CommandType.Rest);

            int m = PolicyUtil.LeastTiredMember(engine.State);
            return engine.PreviewCommand(m, CommandType.Work).Blocked
                ? new CommandChoice(m, CommandType.Rest)
                : new CommandChoice(m, CommandType.Work);
        }
    }

    /// <summary>
    /// ทำงานปกติ แต่ **ปฏิเสธทุก Event** — ตัวนี้มีไว้ทดสอบข้อเดียว: กติกา "ปฏิเสธก็มีราคา" ใช้ได้จริงไหม
    /// ถ้าตัวนี้ชนะสูงกว่า RestWhenTired แปลว่าราคาของการปฏิเสธยังถูกเกินไป
    /// </summary>
    public sealed class DeclineAllEventsPolicy : IRunPolicy
    {
        readonly RestWhenTiredPolicy _inner = new RestWhenTiredPolicy();
        public string Name => "DeclineAllEvents";
        public void Reset(IRandomSource rng) => _inner.Reset(rng);
        public bool AcceptEvent(TurnEngine engine) => false;
        public CommandChoice ChooseCommand(TurnEngine engine) => _inner.ChooseCommand(engine);
    }

    /// <summary>เส้นฐาน: สุ่มมั่ว ถ้าตัวนี้ชนะบ่อย แปลว่าการตัดสินใจของผู้เล่นไม่มีน้ำหนัก</summary>
    public sealed class RandomPolicy : IRunPolicy
    {
        IRandomSource _rng;
        public string Name => "RandomLegal";

        // rng มาจาก Reset ของแต่ละรอบ เพื่อให้ seed เดียวกันให้ผลเดิมเสมอ

        public void Reset(IRandomSource rng) { _rng = rng; }
        public bool AcceptEvent(TurnEngine engine) => _rng.NextDouble() < 0.5;

        public CommandChoice ChooseCommand(TurnEngine engine)
        {
            var choices = PolicyUtil.LegalChoices(engine);
            return choices[_rng.NextInt(0, choices.Count)];
        }
    }

    public static class PolicyRunner
    {
        /// <summary>เล่นหนึ่งรอบจนจบด้วยนโยบายที่ให้มา แล้วคืนผล</summary>
        public static RunResult PlayOne(GameContent content, IRunPolicy policy, int seed, int maxSteps = 500)
        {
            var rng = new SeededRandom(seed);
            var engine = new TurnEngine(content, rng);

            policy.Reset(rng);
            engine.Start();

            int steps = 0;
            while (engine.Phase != TurnPhase.Finished)
            {
                if (++steps > maxSteps)
                    throw new InvalidOperationException("รอบเล่นไม่ยอมจบ — น่าจะมีลูปในลำดับเฟส");

                if (engine.Phase == TurnPhase.AwaitingEventChoice)
                {
                    engine.ChooseEvent(policy.AcceptEvent(engine));
                    continue;
                }

                var choice = policy.ChooseCommand(engine);
                engine.ExecuteCommand(choice.MemberIndex, choice.Type);
            }

            return engine.State.Result;
        }
    }
}
