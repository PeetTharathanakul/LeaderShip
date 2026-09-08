using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using LeaderShip.Data;
using LeaderShip.Model;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LeaderShip.EditorTools
{
    /// <summary>
    /// Balance Simulator (ADR-0007)
    ///
    /// เหตุผลที่มันมีอยู่: ADR-0003 ประกาศว่า "เกมต้องไม่มีสูตรตายตัว"
    /// แต่ความรู้สึกว่าไม่มีสูตร ไม่ใช่หลักฐาน — สูตรตายตัวเกิดจากตัวเลข ไม่ใช่จากกฎ และมันซ่อนตัวเก่ง
    /// หน้าต่างนี้เล่นเกมหลายหมื่นรอบด้วยกลยุทธ์โง่ ๆ แล้วรายงานว่ามีตัวไหนชนะบ่อยเกินไปไหม
    ///
    /// เกณฑ์ผ่าน:
    ///   - กลยุทธ์ตายตัวทุกตัว ชนะไม่เกิน 40%
    ///   - สุ่มมั่ว ชนะไม่เกิน 20%   (ต่ำกว่านี้ = การตัดสินใจมีน้ำหนัก)
    ///   - Skilled อยู่ในช่วง 50-70% (ต่ำกว่า = ยากเกินจนคนเล่นเป็นก็แพ้ · สูงกว่า = ง่ายเกิน)
    /// </summary>
    public sealed class BalanceSimulatorWindow : EditorWindow
    {
        const float DegenerateMaxWinRate = 40f;
        const float RandomMaxWinRate = 20f;
        const float SkilledMinWinRate = 50f;
        const float SkilledMaxWinRate = 70f;

        GameContentSO _contentAsset;
        int _runs = 3000;
        Vector2 _scroll;
        string _report = "";

        [MenuItem("LeaderShip/Balance Simulator", priority = 20)]
        public static void Open()
        {
            var window = GetWindow<BalanceSimulatorWindow>("Balance Simulator");
            window.minSize = new Vector2(560, 420);
        }

        void OnEnable()
        {
            if (_contentAsset == null)
                _contentAsset = AssetDatabase.LoadAssetAtPath<GameContentSO>("Assets/0Game/Data/GameContent.asset");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("พิสูจน์ว่าเกมไม่มีสูตรตายตัว", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "รันเกมหลายพันรอบด้วยกลยุทธ์อัตโนมัติ แล้วดูว่ามีตัวไหนชนะบ่อยเกินเกณฑ์ไหม\n" +
                $"กลยุทธ์ตายตัว ≤ {DegenerateMaxWinRate:0}%  ·  สุ่มมั่ว ≤ {RandomMaxWinRate:0}%  ·  " +
                $"Skilled {SkilledMinWinRate:0}-{SkilledMaxWinRate:0}%",
                MessageType.Info);

            _contentAsset = (GameContentSO)EditorGUILayout.ObjectField(
                new GUIContent("Game Content", "เว้นว่างไว้จะใช้ค่าตั้งต้นในโค้ดแทน"),
                _contentAsset, typeof(GameContentSO), false);

            _runs = EditorGUILayout.IntSlider("จำนวนรอบต่อกลยุทธ์", _runs, 200, 30000);

            EditorGUILayout.Space();

            if (GUILayout.Button("รัน", GUILayout.Height(32)))
                Run();

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        void Run()
        {
            GameContent content;

            if (_contentAsset != null && _contentAsset.IsComplete)
            {
                content = _contentAsset.Build();
            }
            else
            {
                content = DefaultContent.Create();
                Debug.LogWarning("[BalanceSimulator] ไม่ได้ใส่ GameContent ที่สมบูรณ์ — ใช้ค่าตั้งต้นในโค้ดแทน " +
                                 "ผลที่ได้จึงอาจไม่ตรงกับที่เกมจริงใช้");
            }

            var policies = BuildPolicies();
            var sb = new StringBuilder();
            var stopwatch = Stopwatch.StartNew();

            sb.AppendLine($"รอบต่อกลยุทธ์: {_runs}");
            sb.AppendLine($"เดดไลน์เริ่มต้น: {content.Balance.StartTurns} เทิร์น · เป้าหมาย {content.Balance.ProgressGoal:0}%");
            sb.AppendLine();
            sb.AppendLine("กลยุทธ์                 ชนะ%   คืบหน้าเฉลี่ย   ผล");
            sb.AppendLine("--------------------------------------------------------");

            bool allPassed = true;

            for (int i = 0; i < policies.Count; i++)
            {
                var entry = policies[i];

                EditorUtility.DisplayProgressBar("Balance Simulator", entry.Policy.Name, (float)i / policies.Count);

                var result = Measure(content, entry.Policy, _runs);
                string verdict = Judge(entry, result.WinRate, ref allPassed);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-22} {1,6:0.0}   {2,12:0.0}   {3}",
                    entry.Policy.Name, result.WinRate, result.AverageProgress, verdict));
            }

            EditorUtility.ClearProgressBar();
            stopwatch.Stop();

            sb.AppendLine();
            sb.AppendLine(allPassed
                ? "ผ่านทุกเกณฑ์ — ยังไม่พบกลยุทธ์ตายตัวที่ชนะได้ง่ายเกินไป"
                : "ไม่ผ่าน — มีกลยุทธ์ที่ชนะผิดปกติ ให้แก้ *กติกา* ก่อน แล้วค่อยแก้ตัวเลข (docs/balance.md §9)");
            sb.AppendLine($"ใช้เวลา {stopwatch.ElapsedMilliseconds} ms");

            _report = sb.ToString();
            Repaint();
        }

        static string Judge(PolicyEntry entry, float winRate, ref bool allPassed)
        {
            bool pass;
            string label;

            switch (entry.Kind)
            {
                case PolicyKind.Skilled:
                    pass = winRate >= SkilledMinWinRate && winRate <= SkilledMaxWinRate;
                    label = pass ? "ok" : (winRate < SkilledMinWinRate ? "ยากเกินไป" : "ง่ายเกินไป");
                    break;
                case PolicyKind.Random:
                    pass = winRate <= RandomMaxWinRate;
                    label = pass ? "ok" : "สุ่มมั่วก็ชนะได้ — การตัดสินใจไม่มีน้ำหนัก";
                    break;
                default:
                    pass = winRate <= DegenerateMaxWinRate;
                    label = pass ? "ok" : "พบสูตรตายตัว";
                    break;
            }

            if (!pass) allPassed = false;
            return label;
        }

        static MeasureResult Measure(GameContent content, IRunPolicy policy, int runs)
        {
            int wins = 0;
            double progressSum = 0;

            for (int i = 0; i < runs; i++)
            {
                var rng = new SeededRandom(i);
                var engine = new TurnEngine(content, rng);

                policy.Reset(rng);
                engine.Start();

                int steps = 0;
                while (engine.Phase != TurnPhase.Finished)
                {
                    if (++steps > 500)
                    {
                        Debug.LogError($"[BalanceSimulator] รอบเล่นไม่จบด้วยกลยุทธ์ {policy.Name} — น่าจะมีลูปในลำดับเฟส");
                        break;
                    }

                    if (engine.Phase == TurnPhase.AwaitingEventChoice)
                    {
                        engine.ChooseEvent(policy.AcceptEvent(engine));
                        continue;
                    }

                    var choice = policy.ChooseCommand(engine);
                    engine.ExecuteCommand(choice.MemberIndex, choice.Type);
                }

                if (engine.State.Result == RunResult.Won) wins++;
                progressSum += engine.State.Progress;
            }

            return new MeasureResult
            {
                WinRate = 100f * wins / runs,
                AverageProgress = (float)(progressSum / runs)
            };
        }

        static List<PolicyEntry> BuildPolicies() => new List<PolicyEntry>
        {
            new PolicyEntry(new SkilledPolicy(), PolicyKind.Skilled),
            new PolicyEntry(new AlwaysCommandPolicy(CommandType.Push, "AlwaysPush"), PolicyKind.Degenerate),
            new PolicyEntry(new AlwaysCommandPolicy(CommandType.Work, "AlwaysWork"), PolicyKind.Degenerate),
            new PolicyEntry(new AlwaysCommandPolicy(CommandType.Direct, "AlwaysDirect"), PolicyKind.Degenerate),
            new PolicyEntry(new RoundRobinCommandPolicy(), PolicyKind.Degenerate),
            new PolicyEntry(new RoundRobinBothPolicy(), PolicyKind.Degenerate),
            new PolicyEntry(new GreedyEvPolicy(), PolicyKind.Degenerate),
            new PolicyEntry(new RestWhenTiredPolicy(), PolicyKind.Degenerate),
            new PolicyEntry(new DeclineAllEventsPolicy(), PolicyKind.Degenerate),
            new PolicyEntry(new RandomPolicy(), PolicyKind.Random)
        };

        enum PolicyKind { Degenerate, Random, Skilled }

        readonly struct PolicyEntry
        {
            public readonly IRunPolicy Policy;
            public readonly PolicyKind Kind;

            public PolicyEntry(IRunPolicy policy, PolicyKind kind)
            {
                Policy = policy;
                Kind = kind;
            }
        }

        struct MeasureResult
        {
            public float WinRate;
            public float AverageProgress;
        }
    }
}
