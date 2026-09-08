using System;
using LeaderShip.Core;
using LeaderShip.Data;
using LeaderShip.Model;
using UnityEngine;

namespace LeaderShip.Managers
{
    /// <summary>
    /// เจ้าของ "รอบเล่น" ทั้งหมด — เป็น manager จึงเป็น singleton ได้ตาม .agents/AGENTS.md
    /// หน้าที่เดียวของมันคือต่อ ScriptableObject เข้ากับ TurnEngine แล้วกระจาย event ให้ View
    /// **ตรรกะกติกาห้ามอยู่ที่นี่** ทุกกฎอยู่ในชั้นโมเดลที่ไม่พึ่ง Unity (ADR-0007)
    /// </summary>
    public sealed class RunManager : SceneSingleton<RunManager>
    {
        [Header("Content")]
        [SerializeField] GameContentSO content;

        [Header("Randomness")]
        [Tooltip("เปิดเพื่อล็อก seed ตอนไล่บั๊ก — รอบเดิมจะเล่นซ้ำได้เป๊ะ ๆ")]
        [SerializeField] bool useFixedSeed = false;
        [SerializeField] int fixedSeed = 20260908;

        public TurnEngine Engine { get; private set; }
        public GameContent Content { get; private set; }
        public int CurrentSeed { get; private set; }

        /// <summary>ยิงเมื่อ state เปลี่ยนพอที่ UI ควรวาดใหม่</summary>
        public event Action Changed;

        public event Action<RunResult> RunFinished;

        /// <summary>ส่งต่อผลของคำสั่งให้ชั้นนำเสนอ (แฟลชกลางจอ + ป๊อปอัปสรุป) — ไม่มีกติกาผูกกับมัน</summary>
        public event Action<CommandResult> CommandResolved;

        public event Action<EventResult> EventResolved;

        protected override void Awake()
        {
            base.Awake();

            if (content == null)
            {
                Debug.LogError($"[{nameof(RunManager)}] ไม่ได้ใส่ GameContent — เกมเริ่มไม่ได้ " +
                               "ลากไฟล์ Assets/0Game/Data/GameContent.asset มาใส่ในช่อง Content", this);
            }
        }

        void Start()
        {
            if (content == null) return;
            StartNewRun();
        }

        public void StartNewRun()
        {
            if (content == null) return;

            CurrentSeed = useFixedSeed ? fixedSeed : UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            Content = content.Build();
            Engine = new TurnEngine(Content, new SeededRandom(CurrentSeed));

            Engine.Changed += OnEngineChanged;
            Engine.Finished += OnEngineFinished;
            Engine.CommandResolved += OnCommandResolved;
            Engine.EventResolved += OnEventResolved;

            Engine.Start();
            Changed?.Invoke();
        }

        void OnEngineChanged() => Changed?.Invoke();

        void OnEngineFinished(RunResult result) => RunFinished?.Invoke(result);

        void OnCommandResolved(CommandResult result) => CommandResolved?.Invoke(result);

        void OnEventResolved(EventResult result) => EventResolved?.Invoke(result);

        void OnDisable()
        {
            if (Engine == null) return;
            Engine.Changed -= OnEngineChanged;
            Engine.Finished -= OnEngineFinished;
            Engine.CommandResolved -= OnCommandResolved;
            Engine.EventResolved -= OnEventResolved;
        }

#if UNITY_EDITOR
        public void EditorSetContent(GameContentSO value) => content = value;
#endif
    }
}
