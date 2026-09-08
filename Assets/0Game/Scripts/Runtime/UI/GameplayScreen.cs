using LeaderShip.Managers;
using LeaderShip.Model;
using TMPro;
using UnityEngine;

namespace LeaderShip.UI
{
    /// <summary>
    /// ตัวควบคุมหน้าจอเล่นเกม — ต่อ RunManager เข้ากับ View ทุกตัว
    ///
    /// ที่นี่ถือ state ของ **การนำเสนอ** อย่างเดียว (ตอนนี้เลือกใครอยู่ · กำลังโชว์ผลอยู่ไหม)
    /// state ของกติกาอยู่ในชั้นโมเดลทั้งหมด ห้ามย้ายมาที่นี่
    ///
    /// View ทุกตัวถูกฉีดผ่าน SerializeField ไม่ใช้ FindObjectOfType (.agents/AGENTS.md — banned)
    ///
    /// ## จังหวะการนำเสนอ
    /// กดคำสั่ง → เอนจินตัดสินและ **เดินเทิร์นต่อทันที** → เราหยุด "การเล่าเรื่อง" ไว้ก่อน:
    ///   1. แฟลชกลางจอ SUCCESS / FAILED — ตอบคำถามแรกให้จบใน ~0.8 วินาที
    ///   2. ป๊อปอัปสรุป — ได้อะไร เสียอะไร เพราะอะไร
    ///   3. ผู้เล่นกด Continue → ค่อยวาดหน้าจอใหม่ · ค่อยเปิดกล่องเหตุการณ์ · ค่อยขึ้นหน้าจบ
    /// ระหว่าง 1–2 ปิดอินพุตทั้งหมด ไม่งั้นผู้เล่นจะสั่งเทิร์นถัดไปโดยไม่ทันเห็นว่าเทิร์นที่แล้วเกิดอะไร
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

        [Header("Outcome presentation")]
        [SerializeField] OutcomeFlashView outcomeFlash;
        [SerializeField] OutcomePopupView outcomePopup;

        int _selected;
        bool _subscribed;

        /// <summary>true ตั้งแต่กดสั่งจนกว่าผู้เล่นจะปิดป๊อปอัป — ระหว่างนี้ห้ามรับอินพุตและห้ามเปิดกล่องอื่น</summary>
        bool _presenting;

        bool _hasPendingOutcome;
        OutcomePopupView.Payload _pendingOutcome;
        string _pendingFlashCaption;
        Color _pendingFlashColor;
        string _pendingFlashHeadline;
        bool _pendingFlashPositive;

        /// <summary>รอบจบระหว่างที่ยังโชว์ผลอยู่ — หน้าจบต้องรอให้เล่าจบก่อน ไม่ใช่เด้งทับ</summary>
        bool _runFinishedWhilePresenting;

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
                runManager.CommandResolved += OnCommandResolved;
                runManager.EventResolved += OnEventResolved;
            }

            if (memberCards != null)
                foreach (var card in memberCards)
                    if (card != null) card.Selected += OnMemberSelected;

            if (commandButtons != null)
                foreach (var button in commandButtons)
                    if (button != null) button.Clicked += OnCommandClicked;

            if (eventDialog != null) eventDialog.Chosen += OnEventChosen;
            if (resultView != null) resultView.RestartRequested += OnRestart;
            if (outcomePopup != null) outcomePopup.ContinueRequested += OnOutcomeDismissed;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            if (runManager != null)
            {
                runManager.Changed -= Refresh;
                runManager.RunFinished -= OnRunFinished;
                runManager.CommandResolved -= OnCommandResolved;
                runManager.EventResolved -= OnEventResolved;
            }

            if (memberCards != null)
                foreach (var card in memberCards)
                    if (card != null) card.Selected -= OnMemberSelected;

            if (commandButtons != null)
                foreach (var button in commandButtons)
                    if (button != null) button.Clicked -= OnCommandClicked;

            if (eventDialog != null) eventDialog.Chosen -= OnEventChosen;
            if (resultView != null) resultView.RestartRequested -= OnRestart;
            if (outcomePopup != null) outcomePopup.ContinueRequested -= OnOutcomeDismissed;
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
            if (_presenting) return;

            _selected = index;
            Refresh();
        }

        void OnCommandClicked(CommandType type)
        {
            if (_presenting) return;

            var engine = runManager != null ? runManager.Engine : null;
            if (engine == null || engine.Phase != TurnPhase.AwaitingCommand) return;
            if (engine.PreviewCommand(_selected, type).Blocked) return;

            // ต้องยกธงก่อนสั่ง เพราะ ExecuteCommand เดินเทิร์นจนจบและยิง Changed กลับมาแบบซิงโครนัส
            // ถ้ายกทีหลัง Refresh จะเปิดกล่องเหตุการณ์ของเทิร์นถัดไปทับผลของเทิร์นนี้
            _presenting = true;
            engine.ExecuteCommand(_selected, type);
            PlayPendingOutcome();
        }

        void OnEventChosen(bool accept)
        {
            if (_presenting) return;

            var engine = runManager != null ? runManager.Engine : null;
            if (engine == null || engine.Phase != TurnPhase.AwaitingEventChoice) return;

            _presenting = true;
            if (eventDialog != null) eventDialog.Hide();

            engine.ChooseEvent(accept);
            PlayPendingOutcome();
        }

        // ------------------------------------------------------------------ ผลลัพธ์

        void OnCommandResolved(CommandResult result)
        {
            var engine = runManager != null ? runManager.Engine : null;
            if (engine == null) return;

            // ประกอบข้อความ **ตอนนี้** ตอนที่ RunState ยังเป็นของวินาทีที่ตัดสิน
            _pendingOutcome = OutcomePopupView.BuildCommand(result, engine.State);
            _pendingFlashHeadline = GameText.OutcomeHeadline(result.Outcome);
            _pendingFlashColor = OutcomeFlashView.ColorFor(result.Outcome);
            _pendingFlashPositive = result.Outcome == OutcomeKind.Success;

            var member = engine.State.Members[result.MemberIndex];
            _pendingFlashCaption = $"{member.Def.DisplayName}  ·  {GameText.CommandName(result.Preview.Type)}";

            _hasPendingOutcome = true;
        }

        void OnEventResolved(EventResult result)
        {
            _pendingOutcome = OutcomePopupView.BuildEvent(result);
            _pendingFlashHeadline = _pendingOutcome.Status;
            _pendingFlashColor = _pendingOutcome.StatusColor;
            _pendingFlashPositive = result.Accepted && result.Succeeded;
            _pendingFlashCaption = result.Preview.Def.Title;

            _hasPendingOutcome = true;
        }

        void PlayPendingOutcome()
        {
            if (!_hasPendingOutcome || outcomeFlash == null || outcomePopup == null)
            {
                // ไม่มีของให้โชว์ (หรือยังไม่ได้ต่อสาย) ก็อย่าค้างหน้าจอไว้เฉย ๆ
                _hasPendingOutcome = false;
                FinishPresentation();
                return;
            }

            _hasPendingOutcome = false;

            outcomeFlash.Play(_pendingFlashHeadline, _pendingFlashColor, _pendingFlashPositive,
                _pendingFlashCaption, () => outcomePopup.Show(_pendingOutcome));
        }

        void OnOutcomeDismissed() => FinishPresentation();

        void FinishPresentation()
        {
            _presenting = false;
            Refresh();

            if (!_runFinishedWhilePresenting) return;

            _runFinishedWhilePresenting = false;
            ShowResult();
        }

        void OnRunFinished(RunResult result)
        {
            if (_presenting)
            {
                _runFinishedWhilePresenting = true;
                return;
            }

            ShowResult();
        }

        void ShowResult()
        {
            var engine = runManager != null ? runManager.Engine : null;
            if (resultView != null && engine != null) resultView.Show(engine.State);
        }

        void OnRestart()
        {
            if (resultView != null) resultView.Hide();
            if (eventDialog != null) eventDialog.Hide();
            if (outcomeFlash != null) outcomeFlash.Cancel();
            if (outcomePopup != null) outcomePopup.HideImmediate();

            _presenting = false;
            _hasPendingOutcome = false;
            _runFinishedWhilePresenting = false;
            _selected = 0;

            if (runManager != null) runManager.StartNewRun();
            Refresh();
        }

        /// <summary>คีย์ลัด: 1-3 เลือกคน · Q W E R สั่ง · Y/N ตอบเหตุการณ์ · Space ปิดป๊อปอัป</summary>
        void Update()
        {
            if (_presenting)
            {
                if (outcomePopup != null && outcomePopup.IsOpen && ContinuePressed())
                    outcomePopup.RequestContinue();
                return;
            }

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

        static bool ContinuePressed()
            => Input.GetKeyDown(KeyCode.Space)
               || Input.GetKeyDown(KeyCode.Return)
               || Input.GetKeyDown(KeyCode.KeypadEnter)
               || Input.GetKeyDown(KeyCode.Escape);

        // ------------------------------------------------------------------ วาดใหม่

        void Refresh()
        {
            var engine = runManager != null ? runManager.Engine : null;
            if (engine == null) return;

            ClampSelection(engine);

            // ระหว่างเล่าผล ปุ่มทุกปุ่มต้องดูกดไม่ได้ ไม่ใช่แค่กดแล้วไม่ทำงาน
            bool awaitingCommand = engine.Phase == TurnPhase.AwaitingCommand && !_presenting;

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
                if (!_presenting && engine.Phase == TurnPhase.AwaitingEventChoice)
                    eventDialog.Show(engine.CurrentEvent);
                else if (engine.Phase != TurnPhase.AwaitingEventChoice)
                    eventDialog.Hide();
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
            CommandButtonView[] buttons, EventDialogView dialog, LogView log, ResultView result, TMP_Text hint,
            OutcomeFlashView flash, OutcomePopupView popup)
        {
            runManager = manager;
            hud = hudView;
            memberCards = cards;
            commandButtons = buttons;
            eventDialog = dialog;
            logView = log;
            resultView = result;
            selectionHintText = hint;
            outcomeFlash = flash;
            outcomePopup = popup;
        }
#endif
    }
}
