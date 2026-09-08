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
            var dialog = BuildEventDialog(canvasRect);
            var result = BuildResultPanel(canvasRect);

            var screen = canvas.gameObject.AddComponent<GameplayScreen>();
            screen.EditorAssign(runManager, hud, cards, commandBar.Buttons, dialog, log, result, commandBar.HintText);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = canvas.gameObject;

            Debug.Log("[PrototypeBuilder] ประกอบหน้าจอเสร็จ — กด Play ได้เลย\n" +
                      "คีย์ลัด: 1-3 เลือกลูกทีม · Q เร่งงาน · W ทำงาน · E ทิศทาง · R ให้พัก · Y/N ตอบเหตุการณ์");
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

                var nameText = Text("Name", panel, 20, 14, 332, 42, def.DisplayName, 30,
                    Palette.TextPrimary, TextAlignmentOptions.Left);

                var nickname = Text("Nickname", panel, 20, 58, 332, 62, $"“{def.Nickname}”", 17,
                    Palette.TextMuted, TextAlignmentOptions.TopLeft);
                nickname.fontStyle = FontStyles.Italic;

                var fatigue = Bar("FatigueBar", panel, 20, 126, 332, 42, "Fatigue", 18);
                var morale = Bar("MoraleBar", panel, 20, 176, 332, 42, "Morale", 18);

                var output = Text("Output", panel, 20, 230, 332, 32, "", 19,
                    Palette.Progress, TextAlignmentOptions.Left);

                var status = Text("Status", panel, 20, 270, 332, 142, "", 17,
                    Palette.TextMuted, TextAlignmentOptions.TopLeft);

                // ทั้งใบกดได้ ไม่ใช่แค่ปุ่มเล็ก ๆ — เป้าคลิกใหญ่ อ่านง่าย และเดาได้ว่ากดตรงไหนก็ได้
                var button = panel.gameObject.AddComponent<Button>();
                button.targetGraphic = panel.GetComponent<Image>();
                button.transition = Selectable.Transition.ColorTint;
                button.colors = CardColors();

                var card = panel.gameObject.AddComponent<MemberCardView>();
                card.EditorAssign(border, button, nameText, nickname, output, status, fatigue, morale);
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

            Text("Shortcuts", panel, 724, 16, 404, 34, "Q Push · W Work · E Direct · R Rest", 17,
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

                var title = Text("Title", buttonPanel, 14, 12, 245, 42, GameText.CommandName(types[i]), 26,
                    Palette.TextPrimary, TextAlignmentOptions.Left);

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

        static EventDialogView BuildEventDialog(RectTransform parent)
        {
            // ตัวคอมโพเนนต์ต้องอยู่บนอ็อบเจกต์ที่ **แอ็กทีฟตลอด** ส่วนที่ปิด/เปิดคือลูกของมัน
            // ถ้าเอาคอมโพเนนต์ไปไว้บนอ็อบเจกต์ที่ถูกปิดตั้งแต่ต้น Awake จะไม่เคยรัน
            // ปุ่มก็จะไม่เคยถูกต่อสาย แล้วกล่องจะกดไม่ได้แบบเงียบ ๆ
            var holder = FullScreen("EventDialog", parent);
            var root = FullScreen("Overlay", holder);

            var dimmer = root.gameObject.AddComponent<Image>();
            dimmer.color = Palette.WithAlpha(Color.black, 0.72f);

            var panel = CenteredPanel("Dialog", root, 900, 620,
                _dialogSprite, _dialogBorderSprite, Palette.PanelFill, Palette.PanelBorderActive);

            var title = Text("Title", panel, 30, 24, 840, 52, "", 34,
                Palette.TextPrimary, TextAlignmentOptions.Left);

            var body = Text("Body", panel, 30, 80, 840, 68, "", 21,
                Palette.TextMuted, TextAlignmentOptions.TopLeft);

            var chance = Text("Chance", panel, 30, 152, 840, 40, "", 24,
                Palette.Good, TextAlignmentOptions.Left);

            Divider("Divider", panel, 30, 196, 840, 20);

            var acceptDetail = Text("AcceptDetail", panel, 30, 220, 840, 120, "", 19,
                Palette.TextPrimary, TextAlignmentOptions.TopLeft);

            var accept = TextButton("AcceptButton", panel, 30, 344, 840, 76, "Accept", 28, Palette.Good);

            var declineDetail = Text("DeclineDetail", panel, 30, 432, 840, 80, "", 19,
                Palette.TextPrimary, TextAlignmentOptions.TopLeft);

            var decline = TextButton("DeclineButton", panel, 30, 516, 840, 76, "Decline", 28, Palette.Warning);

            var view = holder.gameObject.AddComponent<EventDialogView>();
            view.EditorAssign(root.gameObject, title, body, chance, acceptDetail, declineDetail, accept, decline);
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
            view.EditorAssign(root.gameObject, title, headline, style, breakdown, restart);
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

        static void Divider(string name, Transform parent, float x, float y, float w, float h)
        {
            if (_dividerSprite == null) return;

            var rt = NewRect(name, parent, x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = _dividerSprite;
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
