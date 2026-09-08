using LeaderShip.Managers;
using LeaderShip.Model;
using TMPro;
using UnityEngine;

namespace LeaderShip.UI
{
    /// <summary>
    /// ตัวควบคุมหน้าจอเล่นเกม — ต่อ RunManager เข้ากับ View ทุกตัว
    ///
    /// ที่นี่ถือ state ของ **การนำเสนอ** อย่างเดียว (ตอนนี้เลือกใครอยู่)
    /// state ของกติกาอยู่ในชั้นโมเดลทั้งหมด ห้ามย้ายมาที่นี่
    ///
    /// View ทุกตัวถูกฉีดผ่าน SerializeField ไม่ใช้ FindObjectOfType (.agents/AGENTS.md — banned)
    /// </summary>
    public sealed class GameplayScreen : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] RunManager runManager;

        [Header("Views")]
        [SerializeField] HudView hud;
        [SerializeField] MemberCardView[] memberCards;
        [SerializeField] CommandButtonView[] commandButtons;
        [SerializeField] EventDialogView eventDialog;
        [SerializeField] LogView logView;
        [SerializeField] ResultView resultView;
        [SerializeField] TMP_Text selectionHintText;

        int _selected;
        bool _subscribed;

        void Awake()
        {
            // RunManager เป็น manager จึงเข้าถึงผ่าน Instance ได้ตามกฎ แต่ยังชอบการฉีดมากกว่า
            if (runManager == null) runManager = RunManager.Instance;

            if (runManager == null)
                Debug.LogError($"[{nameof(GameplayScreen)}] หา RunManager ไม่เจอ — ลากมาใส่ในช่อง Run Manager", this);

            if (memberCards == null || memberCards.Length == 0)
                Debug.LogError($"[{nameof(GameplayScreen)}] ไม่ได้ใส่การ์ดลูกทีมเลย", this);

            if (commandButtons == null || commandButtons.Length == 0)
                Debug.LogError($"[{nameof(GameplayScreen)}] ไม่ได้ใส่ปุ่มคำสั่งเลย", this);

            // ต้องผูก index ตั้งแต่ Awake ไม่ใช่ Start:
            // ลำดับ Start() ระหว่าง MonoBehaviour ไม่ถูกกำหนด ถ้า RunManager.Start() มาก่อน
            // มันจะยิง Changed ตั้งแต่การ์ดยังไม่มี index แล้วหน้าจอจะว่างจนกว่าจะมี event ถัดไป
            BindCards();
        }

        void OnEnable() => Subscribe();
        void OnDisable() => Unsubscribe();

        void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            if (runManager != null)
            {
                runManager.Changed += Refresh;
                runManager.RunFinished += OnRunFinished;
            }

            if (memberCards != null)
                foreach (var card in memberCards)
                    if (card != null) card.Selected += OnMemberSelected;

            if (commandButtons != null)
                foreach (var button in commandButtons)
                    if (button != null) button.Clicked += OnCommandClicked;

            if (eventDialog != null) eventDialog.Chosen += OnEventChosen;
            if (resultView != null) resultView.RestartRequested += OnRestart;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            if (runManager != null)
            {
                runManager.Changed -= Refresh;
                runManager.RunFinished -= OnRunFinished;
            }

            if (memberCards != null)
                foreach (var card in memberCards)
                    if (card != null) card.Selected -= OnMemberSelected;

            if (commandButtons != null)
                foreach (var button in commandButtons)
                    if (button != null) button.Clicked -= OnCommandClicked;

            if (eventDialog != null) eventDialog.Chosen -= OnEventChosen;
            if (resultView != null) resultView.RestartRequested -= OnRestart;
        }

        void Start() => Refresh();

        void BindCards()
        {
            if (memberCards == null) return;
            for (int i = 0; i < memberCards.Length; i++)
                if (memberCards[i] != null) memberCards[i].Bind(i);
        }

        // ------------------------------------------------------------------ input

        void OnMemberSelected(int index)
        {
            _selected = index;
            Refresh();
        }

        void OnCommandClicked(CommandType type)
        {
            var engine = runManager != null ? runManager.Engine : null;
            if (engine == null || engine.Phase != TurnPhase.AwaitingCommand) return;
            if (engine.PreviewCommand(_selected, type).Blocked) return;

            engine.ExecuteCommand(_selected, type);
            Refresh();
        }

        void OnEventChosen(bool accept)
        {
            var engine = runManager != null ? runManager.Engine : null;
            if (engine == null || engine.Phase != TurnPhase.AwaitingEventChoice) return;

            engine.ChooseEvent(accept);
            Refresh();
        }

        void OnRunFinished(RunResult result)
        {
            var engine = runManager != null ? runManager.Engine : null;
            if (resultView != null && engine != null) resultView.Show(engine.State);
        }

        void OnRestart()
        {
            if (resultView != null) resultView.Hide();
            if (eventDialog != null) eventDialog.Hide();
            _selected = 0;
            if (runManager != null) runManager.StartNewRun();
            Refresh();
        }

        /// <summary>คีย์ลัดสำหรับไล่เทสต์เร็ว ๆ: 1-3 เลือกคน · Q W E R สั่ง · Y/N ตอบเหตุการณ์</summary>
        void Update()
        {
            var engine = runManager != null ? runManager.Engine : null;
            if (engine == null) return;

            if (engine.Phase == TurnPhase.AwaitingEventChoice)
            {
                if (Input.GetKeyDown(KeyCode.Y)) OnEventChosen(true);
                else if (Input.GetKeyDown(KeyCode.N)) OnEventChosen(false);
                return;
            }

            if (engine.Phase != TurnPhase.AwaitingCommand) return;

            int memberCount = engine.State.Members.Count;
            if (memberCount > 0 && Input.GetKeyDown(KeyCode.Alpha1)) OnMemberSelected(0);
            if (memberCount > 1 && Input.GetKeyDown(KeyCode.Alpha2)) OnMemberSelected(1);
            if (memberCount > 2 && Input.GetKeyDown(KeyCode.Alpha3)) OnMemberSelected(2);

            if (Input.GetKeyDown(KeyCode.Q)) OnCommandClicked(CommandType.Push);
            else if (Input.GetKeyDown(KeyCode.W)) OnCommandClicked(CommandType.Work);
            else if (Input.GetKeyDown(KeyCode.E)) OnCommandClicked(CommandType.Direct);
            else if (Input.GetKeyDown(KeyCode.R)) OnCommandClicked(CommandType.Rest);
        }

        // ------------------------------------------------------------------ วาดใหม่

        void Refresh()
        {
            var engine = runManager != null ? runManager.Engine : null;
            if (engine == null) return;

            ClampSelection(engine);

            bool awaitingCommand = engine.Phase == TurnPhase.AwaitingCommand;

            if (hud != null) hud.Refresh(engine);

            if (memberCards != null)
                for (int i = 0; i < memberCards.Length; i++)
                    if (memberCards[i] != null) memberCards[i].Refresh(engine, i == _selected);

            if (commandButtons != null)
                foreach (var button in commandButtons)
                    if (button != null) button.Refresh(engine, _selected, awaitingCommand);

            if (selectionHintText != null && _selected < engine.State.Members.Count)
                selectionHintText.text = $"Commanding: {engine.State.Members[_selected].Def.DisplayName}";

            if (eventDialog != null)
            {
                if (engine.Phase == TurnPhase.AwaitingEventChoice) eventDialog.Show(engine.CurrentEvent);
                else eventDialog.Hide();
            }

            if (logView != null) logView.Refresh(engine);
        }

        void ClampSelection(TurnEngine engine)
        {
            int count = engine.State.Members.Count;
            if (count == 0) { _selected = 0; return; }
            if (_selected < 0 || _selected >= count) _selected = 0;
        }

#if UNITY_EDITOR
        public void EditorAssign(RunManager manager, HudView hudView, MemberCardView[] cards,
            CommandButtonView[] buttons, EventDialogView dialog, LogView log, ResultView result, TMP_Text hint)
        {
            runManager = manager;
            hud = hudView;
            memberCards = cards;
            commandButtons = buttons;
            eventDialog = dialog;
            logView = log;
            resultView = result;
            selectionHintText = hint;
        }
#endif
    }
}
