using System.Collections.Generic;
using LeaderShip.Data;
using LeaderShip.Managers;
using LeaderShip.Model;
using LeaderShip.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LeaderShip.EditorTools
{
    /// <summary>
    /// ประกอบหน้าจอเกมทั้งหน้าด้วยโค้ด
    ///
    /// ทำไมไม่ลากประกอบในซีนเอา: หน้านี้มีของที่ต้องต่อสายกันหลายสิบชิ้น การประกอบด้วยมือ
    /// จะพังเงียบ ๆ ทุกครั้งที่เปลี่ยนโครงสร้าง และ diff ของไฟล์ .unity อ่านไม่ได้เลยเวลารีวิว
    /// สคริปต์นี้สร้างใหม่ได้เสมอ กดซ้ำได้ ไม่มีสถานะค้าง
    ///
    /// กฎที่ห้ามผิดในไฟล์นี้ (.agents/AGENTS.md):
    ///   - AddComponent&lt;RectTransform&gt;() **ก่อน** SetParent() เสมอ
    ///   - ห้ามใช้ ?? กับคอมโพเนนต์ของ Unity เพราะ fake null ไม่ทริกเกอร์
    /// </summary>
    public static class PrototypeBuilder
    {
        const string ArtRoot = "Assets/0Game/Art/UI/Double";
        const string RootName = "LeaderShipUI";
        const string ManagersName = "Managers";

        const float RefWidth = 1920f;
        const float RefHeight = 1080f;

        static TMP_FontAsset _font;
        static Sprite _panelSprite;      // ขอบ + พื้นทึบ ใช้เป็นชั้นสีพื้น
        static Sprite _borderSprite;     // เฉพาะขอบ ใช้ทับบนชั้นพื้นเพื่อให้ได้สองโทน
        static Sprite _buttonSprite;
        static Sprite _buttonBorderSprite;
        static Sprite _plainSprite;      // สี่เหลี่ยมเรียบ ใช้ทำแถบค่า
        static Sprite _dialogSprite;
        static Sprite _dialogBorderSprite;
        static Sprite _dividerSprite;

        [MenuItem("LeaderShip/สร้างหน้าจอเกม (Prototype Builder)", priority = 1)]
        public static void Build()
        {
            if (!EnsureGameplayScene()) return;
            if (!LoadArt()) return;
            LoadFont();

            var scene = EditorSceneManager.GetActiveScene();

            DestroyExisting(RootName);
            DestroyExisting(ManagersName);

            var content = AssetDatabase.LoadAssetAtPath<GameContentSO>("Assets/0Game/Data/GameContent.asset");
            if (content == null)
            {
                Debug.LogError("[PrototypeBuilder] ไม่พบ Assets/0Game/Data/GameContent.asset — " +
                               "สั่งเมนู LeaderShip/Content/สร้างไฟล์ข้อมูลตั้งต้น ก่อน");
                return;
            }

            var runManager = BuildManagers(content);
            var canvas = BuildCanvas();
            EnsureEventSystem();

            var canvasRect = canvas.GetComponent<RectTransform>();

            Background(canvasRect);

            var hud = BuildTopBar(canvasRect);
            var cards = BuildMemberCards(canvasRect, content);
            var commandBar = BuildCommandBar(canvasRect);
            var log = BuildLogPanel(canvasRect);

            // ลำดับพี่น้อง = ลำดับการวาด ของที่ต้องอยู่บนสุดต้องสร้างทีหลัง
            // กล่องเหตุการณ์ → ป๊อปอัปสรุป → แฟลชกลางจอ → หน้าจบรอบ
            var dialog = BuildEventDialog(canvasRect);
            var popup = BuildOutcomePopup(canvasRect);
            var flash = BuildOutcomeFlash(canvasRect);
            var result = BuildResultPanel(canvasRect);

            var screen = canvas.gameObject.AddComponent<GameplayScreen>();
            screen.EditorAssign(runManager, hud, cards, commandBar.Buttons, dialog, log, result,
                commandBar.HintText, flash, popup);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = canvas.gameObject;

            Debug.Log("[PrototypeBuilder] ประกอบหน้าจอเสร็จ — กด Play ได้เลย\n" +
                      "คีย์ลัด: 1-3 เลือกลูกทีม · Q เร่งงาน · W ทำงาน · E ทิศทาง · R ให้พัก · " +
                      "Y/N ตอบเหตุการณ์ · Space ปิดป๊อปอัปผลลัพธ์");
        }

        // ------------------------------------------------------------------ โหลดของ

        /// <summary>
        /// สคริปต์นี้ประกอบของลงใน "ซีนที่เปิดอยู่" การเผลอกดตอนเปิดซีนอื่นจะเทของลงผิดที่
        /// จึงเช็คก่อนเสมอ แล้วเสนอให้เปิดซีนที่ถูกต้องให้
        /// </summary>
        static bool EnsureGameplayScene()
        {
            const string ScenePath = "Assets/0Game/Scene/Gameplay.unity";

            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath) return true;

            if (System.IO.File.Exists(ScenePath))
            {
                bool open = EditorUtility.DisplayDialog(
                    "เปิดซีน Gameplay ก่อน",
                    $"ตอนนี้เปิดซีน '{(string.IsNullOrEmpty(active.path) ? active.name : active.path)}' อยู่\n" +
                    $"สคริปต์นี้จะประกอบของลงในซีนที่เปิดอยู่\n\nจะเปิด {ScenePath} ให้ไหม",
                    "เปิดแล้วประกอบต่อ", "ยกเลิก");

                if (!open) return false;

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return true;
            }

            return EditorUtility.DisplayDialog(
                "ไม่พบซีน Gameplay",
                $"ไม่พบ {ScenePath}\nจะประกอบลงในซีนที่เปิดอยู่ตอนนี้แทนไหม",
                "ประกอบเลย", "ยกเลิก");
        }

        static bool LoadArt()
        {
            _panelSprite = LoadSprite($"{ArtRoot}/Panel/panel-009.png");
            _borderSprite = LoadSprite($"{ArtRoot}/Border/panel-border-009.png");
            _buttonSprite = LoadSprite($"{ArtRoot}/Panel/panel-011.png");
            _buttonBorderSprite = LoadSprite($"{ArtRoot}/Border/panel-border-011.png");
            _plainSprite = LoadSprite($"{ArtRoot}/Panel/panel-015.png");
            _dialogSprite = LoadSprite($"{ArtRoot}/Panel/panel-014.png");
            _dialogBorderSprite = LoadSprite($"{ArtRoot}/Border/panel-border-014.png");
            _dividerSprite = LoadSprite($"{ArtRoot}/Divider/divider-000.png");

            if (_panelSprite == null || _plainSprite == null)
            {
                Debug.LogError($"[PrototypeBuilder] โหลดสไปรต์จาก {ArtRoot} ไม่ได้ — " +
                               "สั่งเมนู LeaderShip/Art/ตั้งค่าอิมพอร์ตสไปรต์ UI ทั้งชุด ก่อน");
                return false;
            }

            return true;
        }

        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[PrototypeBuilder] ไม่พบสไปรต์ {path}");
            return sprite;
        }

        static void LoadFont()
        {
            _font = null;

            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/0Game" });
            if (guids.Length > 0)
                _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));

            // ข้อความในเกมเป็นภาษาอังกฤษล้วน ฟอนต์ default ของ TMP (Liberation Sans) จึงพอแล้ว
            // ถ้าอยากเปลี่ยนหน้าตา วางไฟล์ .ttf ใน Assets/0Game/Art/Fonts แล้วสั่งเมนูสร้าง TMP Font Asset
            // ตัวที่เจอใน Assets/0Game จะถูกหยิบมาใช้แทนอัตโนมัติ
            if (_font == null)
            {
                _font = TMP_Settings.defaultFontAsset;
                if (_font == null)
                    Debug.LogError("[PrototypeBuilder] ไม่พบฟอนต์ TMP เลย — สั่ง Window/TextMeshPro/Import TMP Essential Resources ก่อน");
            }
        }

        static void DestroyExisting(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        // ------------------------------------------------------------------ โครงหลัก

        static RunManager BuildManagers(GameContentSO content)
        {
            var go = new GameObject(ManagersName);
            var manager = go.AddComponent<RunManager>();
            manager.EditorSetContent(content);
            return manager;
        }

        static Canvas BuildCanvas()
        {
            var go = new GameObject(RootName);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;

            // FindObjectOfType ถูกแบนใน runtime code แต่ที่นี่เป็น Editor tool ที่รันครั้งเดียวตอนประกอบซีน
            // ไม่ใช่โค้ดที่รันตอนเล่น จึงไม่เข้าข่ายกฎนั้น
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static void Background(RectTransform parent)
        {
            var rt = FullScreen("Background", parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = Palette.Background;
            image.raycastTarget = false;
        }

        // ------------------------------------------------------------------ แถบบน

        static HudView BuildTopBar(RectTransform parent)
        {
            var panel = Panel("TopBar", parent, 40, 24, 1840, 170, _panelSprite, _borderSprite,
                Palette.PanelFill, Palette.PanelBorder);

            var progress = Bar("ProgressBar", panel, 24, 16, 1150, 52, "Progress", 22);
            var credibility = Bar("CredibilityBar", panel, 24, 80, 1150, 40, "Credibility", 19);

            var warning = Text("WarningText", panel, 24, 126, 1150, 32, "", 19,
                Palette.Warning, TextAlignmentOptions.Left);

            var turn = Text("TurnText", panel, 1500, 22, 316, 36, "Turn 1", 22,
                Palette.TextMuted, TextAlignmentOptions.Right);

            var deadline = Text("DeadlineText", panel, 1500, 58, 316, 80, "18 turns left", 40,
                Palette.TextPrimary, TextAlignmentOptions.Right);

            var hud = panel.gameObject.AddComponent<HudView>();
            hud.EditorAssign(progress, credibility, turn, deadline, warning);
            return hud;
        }

        // ------------------------------------------------------------------ การ์ดลูกทีม

        static MemberCardView[] BuildMemberCards(RectTransform parent, GameContentSO contentAsset)
        {
            GameContent content = contentAsset.Build();
            int count = content.Members.Length;

            var cards = new List<MemberCardView>();
            const float cardWidth = 372f;
            const float gap = 18f;

            for (int i = 0; i < count; i++)
            {
                float x = 40f + i * (cardWidth + gap);
                var def = content.Members[i];

                var panel = Panel($"MemberCard_{def.Id}", parent, x, 214, cardWidth, 430,
                    _panelSprite, _borderSprite, Palette.PanelFillRaised, Palette.PanelBorder);

                var border = panel.Find("Border").GetComponent<Image>();

                var nameText = Text("Name", panel, 20, 14, 268, 42, def.DisplayName, 30,
                    Palette.TextPrimary, TextAlignmentOptions.Left);

                // ป้ายเลขคีย์ลัดติดกับการ์ดโดยตรง แทนที่จะเขียนรวมไว้บรรทัดเดียวที่อื่น
                // ผู้เล่นถึงจะรู้ว่าเลข 1 หมายถึงใบไหนโดยไม่ต้องนับเอง
                KeyBadge($"Key_{i + 1}", panel, 296, 16, (i + 1).ToString());

                var nickname = Text("Nickname", panel, 20, 58, 332, 62, $"“{def.Nickname}”", 17,
                    Palette.TextMuted, TextAlignmentOptions.TopLeft);
                nickname.fontStyle = FontStyles.Italic;

                var fatigue = Bar("FatigueBar", panel, 20, 126, 332, 42, "Fatigue", 18);
                var morale = Bar("MoraleBar", panel, 20, 176, 332, 42, "Morale", 18);

                var output = Text("Output", panel, 20, 230, 332, 32, "", 19,
                    Palette.Progress, TextAlignmentOptions.Left);

                var status = Text("Status", panel, 20, 270, 332, 108, "", 17,
                    Palette.TextMuted, TextAlignmentOptions.TopLeft);

                // ป้ายบอกว่ากำลังสั่งใบนี้ — สีขอบอย่างเดียวมองข้ามง่ายเกินไปบนพื้นเข้ม
                var selectedTag = Text("SelectedTag", panel, 20, 384, 332, 30, "— COMMANDING —", 17,
                    Palette.PanelBorderActive, TextAlignmentOptions.Center);
                selectedTag.gameObject.SetActive(false);

                // ทั้งใบกดได้ ไม่ใช่แค่ปุ่มเล็ก ๆ — เป้าคลิกใหญ่ อ่านง่าย และเดาได้ว่ากดตรงไหนก็ได้
                var button = panel.gameObject.AddComponent<Button>();
                button.targetGraphic = panel.GetComponent<Image>();
                button.transition = Selectable.Transition.ColorTint;
                button.colors = CardColors();

                var card = panel.gameObject.AddComponent<MemberCardView>();
                card.EditorAssign(border, button, nameText, nickname, output, status, fatigue, morale, selectedTag);
                cards.Add(card);
            }

            return cards.ToArray();
        }

        // ------------------------------------------------------------------ แถบคำสั่ง

        struct CommandBar
        {
            public CommandButtonView[] Buttons;
            public TMP_Text HintText;
        }

        static CommandBar BuildCommandBar(RectTransform parent)
        {
            var panel = Panel("CommandBar", parent, 40, 664, 1152, 340,
                _panelSprite, _borderSprite, Palette.PanelFill, Palette.PanelBorder);

            var hint = Text("SelectionHint", panel, 24, 14, 700, 38, "Commanding: —", 24,
                Palette.TextPrimary, TextAlignmentOptions.Left);

            // คีย์ลัดย้ายไปติดกับปุ่มของมันแล้ว บรรทัดนี้จึงเอาไว้ย้ำสัญญาข้อสำคัญของเกมแทน (ADR-0005)
            Text("Promise", panel, 700, 16, 428, 34, "The % you see is the % the game rolls", 17,
                Palette.TextMuted, TextAlignmentOptions.Right);

            var types = new[] { CommandType.Push, CommandType.Work, CommandType.Direct, CommandType.Rest };
            var buttons = new List<CommandButtonView>();

            const float buttonWidth = 273f;
            const float gap = 12f;

            for (int i = 0; i < types.Length; i++)
            {
                float x = 12f + i * (buttonWidth + gap);

                var buttonPanel = Panel($"Command_{types[i]}", panel, x, 62, buttonWidth, 250,
                    _buttonSprite, _buttonBorderSprite, Palette.PanelFillRaised, Palette.PanelBorder);

                var border = buttonPanel.Find("Border").GetComponent<Image>();

                var title = Text("Title", buttonPanel, 14, 12, 190, 42, GameText.CommandName(types[i]), 26,
                    Palette.TextPrimary, TextAlignmentOptions.Left);

                KeyBadge($"Key_{types[i]}", buttonPanel, 213, 14, ShortcutFor(types[i]));

                var chance = Text("Chance", buttonPanel, 14, 56, 245, 34, "", 21,
                    Palette.Good, TextAlignmentOptions.Left);

                var detail = Text("Detail", buttonPanel, 14, 94, 245, 92, "", 17,
                    Palette.TextMuted, TextAlignmentOptions.TopLeft);

                var warn = Text("Warn", buttonPanel, 14, 188, 245, 56, "", 16,
                    Palette.Warning, TextAlignmentOptions.TopLeft);

                var button = buttonPanel.gameObject.AddComponent<Button>();
                button.targetGraphic = buttonPanel.GetComponent<Image>();
                button.transition = Selectable.Transition.ColorTint;
                button.colors = ButtonColors();

                var view = buttonPanel.gameObject.AddComponent<CommandButtonView>();
                view.EditorAssign(types[i], button, border, title, chance, detail, warn);
                buttons.Add(view);
            }

            return new CommandBar { Buttons = buttons.ToArray(), HintText = hint };
        }

        // ------------------------------------------------------------------ บันทึกเหตุการณ์

        static LogView BuildLogPanel(RectTransform parent)
        {
            var panel = Panel("LogPanel", parent, 1216, 214, 664, 790,
                _panelSprite, _borderSprite, Palette.PanelFill, Palette.PanelBorder);

            Text("LogTitle", panel, 22, 14, 400, 36, "What Happened", 24,
                Palette.TextPrimary, TextAlignmentOptions.Left);

            var viewport = NewRect("Viewport", panel, 18, 56, 628, 716);
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = viewport;

            var contentRect = NewRect("Content", viewport, 0, 0, 628, 0);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0, contentRect.offsetMin.y);
            contentRect.offsetMax = new Vector2(0, contentRect.offsetMax.y);

            var layout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(8, 8, 4, 12);

            var fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRect;

            var logText = Text("LogText", contentRect, 0, 0, 612, 100, "", 18,
                Palette.TextPrimary, TextAlignmentOptions.TopLeft);
            logText.rectTransform.anchorMin = new Vector2(0, 1);
            logText.rectTransform.anchorMax = new Vector2(1, 1);
            logText.enableWordWrapping = true;

            // ต้องเป็น Overflow ไม่ใช่ Truncate ไม่งั้น ContentSizeFitter จะวัดความสูงได้แค่กรอบเดิม
            // แล้วบันทึกจะถูกตัดหายไปเงียบ ๆ ตอนยาวขึ้น
            logText.overflowMode = TextOverflowModes.Overflow;

            var view = panel.gameObject.AddComponent<LogView>();
            view.EditorAssign(logText, scroll);
            return view;
        }

        // ------------------------------------------------------------------ กล่องเหตุการณ์

        /// <summary>
        /// กล่องเหตุการณ์แบบ "การ์ดสองใบ ปุ่มอยู่ในใบของตัวเอง"
        ///
        /// ของเดิมเรียงข้อความกับปุ่มสลับกันลงมาเป็นแถวเดียว ผู้เล่นจึงอ่านไม่ออกว่า
        /// ข้อความไหนเป็นของปุ่มไหน และไม่รู้ว่าข้อความนั้นคือ "สิ่งที่จะเกิดถ้ากด" หรือ "สิ่งที่เกิดไปแล้ว"
        /// วางกรอบครอบทีละทางเลือก + หัวการ์ดขึ้นต้นด้วย "IF YOU ..." จึงตอบทั้งสองเรื่องพร้อมกัน
        /// </summary>
        static EventDialogView BuildEventDialog(RectTransform parent)
        {
            // ตัวคอมโพเนนต์ต้องอยู่บนอ็อบเจกต์ที่ **แอ็กทีฟตลอด** ส่วนที่ปิด/เปิดคือลูกของมัน
            // ถ้าเอาคอมโพเนนต์ไปไว้บนอ็อบเจกต์ที่ถูกปิดตั้งแต่ต้น Awake จะไม่เคยรัน
            // ปุ่มก็จะไม่เคยถูกต่อสาย แล้วกล่องจะกดไม่ได้แบบเงียบ ๆ
            var holder = FullScreen("EventDialog", parent);
            var root = FullScreen("Overlay", holder);

            var dimmer = root.gameObject.AddComponent<Image>();
            dimmer.color = Palette.WithAlpha(Color.black, 0.78f);
            var dimmerGroup = root.gameObject.AddComponent<CanvasGroup>();

            var panel = CenteredPanel("Dialog", root, 1000, 716,
                _dialogSprite, _dialogBorderSprite, Palette.PanelFill, Palette.PanelBorderActive);
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            Text("Kicker", panel, 30, 20, 940, 30, "SOMETHING CAME UP — PICK ONE", 17,
                Palette.TextMuted, TextAlignmentOptions.Left);

            var title = Text("Title", panel, 30, 52, 940, 52, "", 34,
                Palette.TextPrimary, TextAlignmentOptions.Left);

            var body = Text("Body", panel, 30, 108, 940, 60, "", 21,
                Palette.TextMuted, TextAlignmentOptions.TopLeft);

            // ---- การ์ด "ถ้ารับ" ------------------------------------------------
            var acceptCard = Panel("AcceptCard", panel, 30, 178, 940, 274,
                _buttonSprite, _buttonBorderSprite, Palette.PanelFillRaised, Palette.Good);
            var acceptGroup = acceptCard.gameObject.AddComponent<CanvasGroup>();

            var acceptHeader = Text("Header", acceptCard, 24, 14, 560, 36, "IF YOU ACCEPT", 22,
                Palette.Good, TextAlignmentOptions.Left);

            var acceptChance = Text("Chance", acceptCard, 600, 12, 316, 40, "", 26,
                Palette.Good, TextAlignmentOptions.Right);

            var acceptSuccess = Text("SuccessLine", acceptCard, 24, 58, 892, 62, "", 19,
                Palette.TextPrimary, TextAlignmentOptions.TopLeft);

            var acceptFail = Text("FailLine", acceptCard, 24, 122, 892, 62, "", 19,
                Palette.TextPrimary, TextAlignmentOptions.TopLeft);

            var accept = TextButton("AcceptButton", acceptCard, 24, 192, 892, 64, "Accept   (Y)", 26, Palette.Good);

            // ---- การ์ด "ถ้าไม่รับ" ---------------------------------------------
            var declineCard = Panel("DeclineCard", panel, 30, 468, 940, 212,
                _buttonSprite, _buttonBorderSprite, Palette.PanelFillRaised, Palette.Warning);
            var declineGroup = declineCard.gameObject.AddComponent<CanvasGroup>();

            var declineHeader = Text("Header", declineCard, 24, 14, 892, 36, "IF YOU DECLINE", 22,
                Palette.Warning, TextAlignmentOptions.Left);

            var decline = Text("DeclineLine", declineCard, 24, 56, 892, 62, "", 19,
                Palette.TextPrimary, TextAlignmentOptions.TopLeft);

            var declineButton = TextButton("DeclineButton", declineCard, 24, 130, 892, 64, "Decline   (N)", 26,
                Palette.Warning);

            var view = holder.gameObject.AddComponent<EventDialogView>();
            view.EditorAssign(root.gameObject, panel, panelGroup, dimmerGroup, title, body,
                acceptCard, acceptGroup, acceptHeader, acceptChance, acceptSuccess, acceptFail, accept,
                declineCard, declineGroup, declineHeader, decline, declineButton);

            root.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ ผลลัพธ์กลางจอ

        /// <summary>
        /// พาดหัว SUCCESS / FAILED กลางจอ
        /// มี blocker โปร่งใสกินทั้งจอ เพื่อไม่ให้เมาส์ไปโดนปุ่มข้างหลังระหว่างอนิเมชันกำลังเล่น
        /// </summary>
        static OutcomeFlashView BuildOutcomeFlash(RectTransform parent)
        {
            var holder = FullScreen("OutcomeFlash", parent);
            var root = FullScreen("Overlay", holder);

            // ฉากหลังหรี่เป็นชั้นแยก ไม่ใช่ CanvasGroup บน root
            // ถ้าใส่บน root ค่า alpha จะไปคูณกับ alpha ของตัวอักษรด้วย แล้วคุมจังหวะสองอย่างพร้อมกันไม่ได้
            var dimmerRect = FullScreen("Dimmer", root);
            var blocker = dimmerRect.gameObject.AddComponent<Image>();
            blocker.color = Palette.WithAlpha(Palette.Background, 0.86f);
            var dimmerGroup = dimmerRect.gameObject.AddComponent<CanvasGroup>();

            var panel = NewRect("Flash", root, 0, 0, 1200, 230);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(1200, 230);

            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            var headline = Text("Headline", panel, 0, 0, 1200, 150, "SUCCESS", 116,
                Palette.Good, TextAlignmentOptions.Center);
            headline.fontStyle = FontStyles.Bold;

            var caption = Text("Caption", panel, 0, 150, 1200, 60, "", 30,
                Palette.TextPrimary, TextAlignmentOptions.Center);

            var view = holder.gameObject.AddComponent<OutcomeFlashView>();
            view.EditorAssign(root.gameObject, panel, group, dimmerGroup, headline, caption);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>ป๊อปอัปสรุป "ได้อะไร เสียอะไร เพราะอะไร" — บรรทัดค่าถูกสร้างล่วงหน้าแล้วเปิด/ปิดเอา</summary>
        static OutcomePopupView BuildOutcomePopup(RectTransform parent)
        {
            const int MaxRows = 5;

            var holder = FullScreen("OutcomePopup", parent);
            var root = FullScreen("Overlay", holder);

            var dimmer = root.gameObject.AddComponent<Image>();
            dimmer.color = Palette.WithAlpha(Color.black, 0.74f);
            var dimmerGroup = root.gameObject.AddComponent<CanvasGroup>();

            var panel = CenteredPanel("Popup", root, 840, 646,
                _dialogSprite, _dialogBorderSprite, Palette.PanelFill, Palette.PanelBorderActive);
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            var title = Text("Title", panel, 30, 24, 780, 40, "", 24,
                Palette.TextMuted, TextAlignmentOptions.Left);

            var status = Text("Status", panel, 30, 62, 780, 76, "", 58,
                Palette.Good, TextAlignmentOptions.Left);
            status.fontStyle = FontStyles.Bold;

            var reason = Text("Reason", panel, 30, 146, 780, 96, "", 20,
                Palette.TextPrimary, TextAlignmentOptions.TopLeft);

            Divider("Divider", panel, 30, 248, 780, 20);

            Text("RowsTitle", panel, 30, 268, 780, 28, "WHAT IT COST AND WHAT IT BOUGHT", 16,
                Palette.TextMuted, TextAlignmentOptions.Left);

            var empty = Text("Empty", panel, 30, 302, 780, 46, "", 20,
                Palette.TextMuted, TextAlignmentOptions.TopLeft);
            empty.gameObject.SetActive(false);

            var rows = new OutcomePopupView.DeltaRow[MaxRows];
            for (int i = 0; i < MaxRows; i++)
            {
                var rowRect = NewRect($"Row_{i}", panel, 30, 302 + i * 46, 780, 42);
                var rowGroup = rowRect.gameObject.AddComponent<CanvasGroup>();

                var label = Text("Label", rowRect, 0, 0, 420, 42, "", 22,
                    Palette.TextMuted, TextAlignmentOptions.Left);

                var value = Text("Value", rowRect, 420, 0, 360, 42, "", 26,
                    Palette.TextPrimary, TextAlignmentOptions.Right);

                rowRect.gameObject.SetActive(false);

                rows[i] = new OutcomePopupView.DeltaRow
                {
                    Root = rowRect,
                    Group = rowGroup,
                    Label = label,
                    Value = value
                };
            }

            var continueButton = TextButton("ContinueButton", panel, 30, 546, 780, 72,
                "Continue   (Space)", 26, Palette.Progress);

            var view = holder.gameObject.AddComponent<OutcomePopupView>();
            view.EditorAssign(root.gameObject, panel, panelGroup, dimmerGroup, title, status, reason, empty,
                rows, continueButton);

            root.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ หน้าจบรอบ

        static ResultView BuildResultPanel(RectTransform parent)
        {
            var holder = FullScreen("ResultPanel", parent);
            var root = FullScreen("Overlay", holder);

            var dimmer = root.gameObject.AddComponent<Image>();
            dimmer.color = Palette.WithAlpha(Color.black, 0.82f);

            var panel = CenteredPanel("Result", root, 760, 520,
                _dialogSprite, _dialogBorderSprite, Palette.PanelFill, Palette.PanelBorderActive);
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            var title = Text("Title", panel, 30, 28, 700, 70, "", 46,
                Palette.TextPrimary, TextAlignmentOptions.Left);

            var headline = Text("Headline", panel, 30, 104, 700, 50, "", 24,
                Palette.TextMuted, TextAlignmentOptions.Left);

            Divider("Divider", panel, 30, 156, 700, 20);

            var style = Text("Style", panel, 30, 184, 700, 62, "", 34,
                Palette.Credibility, TextAlignmentOptions.Left);

            var breakdown = Text("Breakdown", panel, 30, 250, 700, 46, "", 20,
                Palette.TextMuted, TextAlignmentOptions.Left);

            var restart = TextButton("RestartButton", panel, 30, 330, 700, 88, "Play Again", 30, Palette.Progress);

            var view = holder.gameObject.AddComponent<ResultView>();
            view.EditorAssign(root.gameObject, panel, panelGroup, title, headline, style, breakdown, restart);
            root.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ helper

        /// <summary>
        /// สร้าง RectTransform ที่วางด้วยพิกัดจากมุมซ้ายบน (y นับลงล่าง)
        /// **AddComponent&lt;RectTransform&gt;() ต้องมาก่อน SetParent() เสมอ** ไม่งั้น Unity จะใส่ Transform ธรรมดาให้
        /// </summary>
        static RectTransform NewRect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>RectTransform ที่ยืดเต็มพ่อแม่ ใช้ทำพื้นหลังและชั้นทับหน้าจอ</summary>
        static RectTransform FullScreen(string name, Transform parent)
        {
            var rt = NewRect(name, parent, 0, 0, RefWidth, RefHeight);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// แผงสองชั้น: ชั้นล่างคือสไปรต์ที่มีพื้นทึบ (ทาสีพื้น) ชั้นบนคือสไปรต์ที่มีแต่ขอบ (ทาสีขอบ)
        /// อาร์ตชุดนี้เป็นสีขาวล้วน ถ้าใช้ภาพเดียวแล้ว tint จะได้บล็อกสีเดียวไม่มีเส้นขอบ
        /// </summary>
        static RectTransform Panel(string name, Transform parent, float x, float y, float w, float h,
            Sprite fillSprite, Sprite borderSprite, Color fillColor, Color borderColor)
        {
            var rt = NewRect(name, parent, x, y, w, h);

            var fill = rt.gameObject.AddComponent<Image>();
            fill.sprite = fillSprite;
            fill.type = Image.Type.Sliced;
            fill.color = fillColor;

            if (borderSprite != null)
            {
                var borderRect = NewRect("Border", rt, 0, 0, w, h);
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = Vector2.zero;
                borderRect.offsetMax = Vector2.zero;

                var border = borderRect.gameObject.AddComponent<Image>();
                border.sprite = borderSprite;
                border.type = Image.Type.Sliced;
                border.color = borderColor;
                border.raycastTarget = false;
            }

            return rt;
        }

        static RectTransform CenteredPanel(string name, Transform parent, float w, float h,
            Sprite fillSprite, Sprite borderSprite, Color fillColor, Color borderColor)
        {
            var rt = Panel(name, parent, 0, 0, w, h, fillSprite, borderSprite, fillColor, borderColor);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static TMP_Text Text(string name, Transform parent, float x, float y, float w, float h,
            string content, float size, Color color, TextAlignmentOptions alignment)
        {
            var rt = NewRect(name, parent, x, y, w, h);

            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        static BarView Bar(string name, Transform parent, float x, float y, float w, float h,
            string label, float fontSize)
        {
            var rt = NewRect(name, parent, x, y, w, h);

            var track = rt.gameObject.AddComponent<Image>();
            track.sprite = _plainSprite;
            track.type = Image.Type.Sliced;
            track.color = Palette.BarTrack;
            track.raycastTarget = false;

            // ช่องในสำหรับส่วนที่เติม เว้นขอบไว้ให้เห็นรางชัด ๆ
            var inner = NewRect("Inner", rt, 0, 0, w, h);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(5f, 5f);
            inner.offsetMax = new Vector2(-5f, -5f);

            var fillRect = NewRect("Fill", inner, 0, 0, w, h);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = _plainSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = Palette.Progress;
            fillImage.raycastTarget = false;

            var labelText = Text("Label", rt, 12, 0, w - 24, h, label, fontSize,
                Palette.TextPrimary, TextAlignmentOptions.Left);

            var view = rt.gameObject.AddComponent<BarView>();
            view.EditorAssign(fillRect, fillImage, labelText);
            return view;
        }

        static Button TextButton(string name, Transform parent, float x, float y, float w, float h,
            string label, float fontSize, Color accent)
        {
            var panel = Panel(name, parent, x, y, w, h,
                _buttonSprite, _buttonBorderSprite, Palette.PanelFillRaised, accent);

            Text("Label", panel, 0, 0, w, h, label, fontSize, accent, TextAlignmentOptions.Center);

            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColors();
            return button;
        }

        /// <summary>
        /// เส้นคั่นเป็นแถบบางสีเดียว ไม่ใช้สไปรต์ 9-slice ของชุดอาร์ต
        /// สไปรต์เส้นคั่นสูง 44px ถ้าย่อลงมาต่ำกว่าขอบ 9-slice ของมัน ลายมุมจะบิดจนเห็นเป็นรอยแปลก ๆ
        /// </summary>
        /// <summary>ป้ายคีย์ลัดสี่เหลี่ยมเล็ก ๆ ติดกับสิ่งที่มันสั่ง ไม่ใช่รวมไว้เป็นรายการที่อื่น</summary>
        static void KeyBadge(string name, Transform parent, float x, float y, string key)
        {
            var rt = NewRect(name, parent, x, y, 38, 34);

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = _plainSprite;
            image.type = Image.Type.Sliced;
            image.color = Palette.ButtonDisabled;
            image.raycastTarget = false;

            Text("Key", rt, 0, 0, 38, 34, key, 19, Palette.TextMuted, TextAlignmentOptions.Center);
        }

        static string ShortcutFor(CommandType type)
        {
            switch (type)
            {
                case CommandType.Push: return "Q";
                case CommandType.Work: return "W";
                case CommandType.Direct: return "E";
                default: return "R";
            }
        }

        static void Divider(string name, Transform parent, float x, float y, float w, float h)
        {
            var rt = NewRect(name, parent, x, y + Mathf.Max(0f, (h - 2f) * 0.5f), w, 2f);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = _plainSprite;
            image.type = Image.Type.Sliced;
            image.color = Palette.PanelBorder;
            image.raycastTarget = false;
        }

        static ColorBlock ButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            colors.fadeDuration = 0.08f;
            return colors;
        }

        static ColorBlock CardColors()
        {
            var colors = ButtonColors();
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.disabledColor = Color.white;
            return colors;
        }
    }
}
