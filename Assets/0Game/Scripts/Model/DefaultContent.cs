namespace LeaderShip.Model
{
    /// <summary>
    /// ชุดคอนเทนต์ตั้งต้นที่ตรงกับ docs/balance.md เป๊ะ ๆ
    ///
    /// ที่นี่ **ไม่ใช่** แหล่งความจริงตอนรันเกม — ตอนรันจริงค่าทั้งหมดมาจาก ScriptableObject (ADR-0001)
    /// คลาสนี้มีไว้ 2 อย่างเท่านั้น:
    ///   1. ให้ Editor สร้างไฟล์ .asset ตั้งต้นได้ในคลิกเดียว
    ///   2. ให้ simulator รันได้แม้ยังไม่ได้สร้าง asset
    ///
    /// ข้อความในนี้เป็นภาษาอังกฤษ เพราะเป็นข้อความที่ผู้เล่นเห็น
    /// ตัวเลขทุกตัวตรงกับ docs/balance.md — แก้ตัวเลขเมื่อไหร่ต้องรัน Balance Simulator ใหม่
    /// </summary>
    public static class DefaultContent
    {
        public static GameContent Create()
        {
            var content = new GameContent
            {
                Balance = new BalanceData(),
                Members = CreateMembers(),
                Commands = CreateCommands(),
                Events = CreateEvents()
            };
            content.Validate();
            return content;
        }

        public static MemberDefinition[] CreateMembers() => new[]
        {
            new MemberDefinition
            {
                Id = "reid",
                DisplayName = "Reid",
                Nickname = "Don't explain. Just tell me what to do.",
                BaseOutput = 1.2f,
                PushProgressMult = 1.4f,
                PushMoraleLossMult = 1.0f,
                WorkProgressMult = 1.1f,
                DirectMoraleMult = 0.5f,
                FatigueGainMult = 1.3f,
                RepeatPenaltyMult = 1.0f
            },
            new MemberDefinition
            {
                Id = "ivy",
                DisplayName = "Ivy",
                Nickname = "I need direction, not pressure.",
                BaseOutput = 0.8f,
                PushProgressMult = 0.7f,
                PushMoraleLossMult = 1.6f,
                WorkProgressMult = 1.0f,
                DirectMoraleMult = 1.6f,
                FatigueGainMult = 1.0f,
                RepeatPenaltyMult = 1.0f
            },
            new MemberDefinition
            {
                Id = "kai",
                DisplayName = "Kai",
                Nickname = "I can read you. One trick on repeat and you lose me.",
                BaseOutput = 1.5f,
                PushProgressMult = 0.6f,
                PushMoraleLossMult = 1.0f,
                WorkProgressMult = 1.2f,
                DirectMoraleMult = 1.0f,
                FatigueGainMult = 0.9f,
                RepeatPenaltyMult = 2.0f
            }
        };

        public static CommandDefinition[] CreateCommands() => new[]
        {
            new CommandDefinition
            {
                Type = CommandType.Push,
                DisplayName = "Push Hard",
                Description = "The biggest gain when it lands, but the riskiest, and it costs both energy and morale. Failing sets the work back.",
                BaseSuccessChance = 0.45f,
                ProgressOnSuccess = 12f,
                FatigueDelta = 18f,
                MoraleDelta = -8f,
                ProgressOnFail = -5f,
                MoraleDeltaOnFail = -8f
            },
            new CommandDefinition
            {
                Type = CommandType.Work,
                DisplayName = "Assign Work",
                Description = "The middle road. Fair progress, fair fatigue. Failing just means nothing moves.",
                BaseSuccessChance = 0.70f,
                ProgressOnSuccess = 5f,
                FatigueDelta = 9f,
                MoraleDelta = 0f,
                ProgressOnFail = 0f,
                MoraleDeltaOnFail = -3f
            },
            new CommandDefinition
            {
                Type = CommandType.Direct,
                DisplayName = "Give Direction",
                Description = "No progress by itself, but it restores morale, which lifts the odds on everything that comes after.",
                BaseSuccessChance = 0.85f,
                ProgressOnSuccess = 0f,
                FatigueDelta = 2f,
                MoraleDelta = 16f,
                ProgressOnFail = 0f,
                MoraleDeltaOnFail = 4f
            },
            new CommandDefinition
            {
                Type = CommandType.Rest,
                DisplayName = "Let Rest",
                Description = "Always works and cuts fatigue sharply. The price is the turn itself, and it earns no credibility at all.",
                AlwaysSucceeds = true,
                BaseSuccessChance = 1f,
                ProgressOnSuccess = 0f,
                FatigueDelta = -32f,
                MoraleDelta = 4f,
                ProgressOnFail = 0f,
                MoraleDeltaOnFail = 0f
            }
        };

        public static EventDefinition[] CreateEvents() => new[]
        {
            new EventDefinition
            {
                Id = "client_scope_creep",
                Title = "The client wants one more feature, urgently",
                Body = "The client calls asking for one extra piece. They say it is not big.",
                BaseSuccessChance = 0.55f,
                OnAcceptSuccess = new EventOutcome { ProgressDelta = 15f, Text = "The team lands it in time and the client is delighted." },
                OnAcceptFail = new EventOutcome { ProgressDelta = -8f, CredibilityDelta = -10f, Text = "The scope blew up and finished work had to be torn out." },
                OnDecline = new EventOutcome { DeadlineDelta = -1, Text = "The client is unhappy and pulls the deadline in." }
            },
            new EventDefinition
            {
                Id = "lend_a_hand",
                Title = "The team next door wants to borrow someone for half a day",
                Body = "The project beside yours is drowning and they are asking for a hand.",
                BaseSuccessChance = 0.60f,
                OnAcceptSuccess = new EventOutcome { CredibilityDelta = 6f, TeamFatigueDelta = 6f, Text = "Helping out earns goodwill on both sides." },
                OnAcceptFail = new EventOutcome { ProgressDelta = -4f, CredibilityDelta = -8f, TeamFatigueDelta = 10f, Text = "You helped, your own work stalled, and the team started grumbling." },
                OnDecline = new EventOutcome { CredibilityDelta = -5f, TeamMoraleDelta = -4f, Text = "The team notices that you never help anyone." }
            },
            new EventDefinition
            {
                Id = "untested_shortcut",
                Title = "Someone proposes a shortcut nobody has tried",
                Body = "A team member suggests a new approach that could save a lot of time, but no one has used it before.",
                BaseSuccessChance = 0.45f,
                OnAcceptSuccess = new EventOutcome { ProgressDelta = 18f, TeamMoraleDelta = 6f, Text = "The new approach works and the person who proposed it is beaming." },
                OnAcceptFail = new EventOutcome { ProgressDelta = -10f, CredibilityDelta = -12f, Text = "It broke, and the time is gone for nothing." },
                OnDecline = new EventOutcome { TeamMoraleDelta = -8f, Text = "They went quiet. They probably will not propose anything next time." }
            },
            new EventDefinition
            {
                Id = "leave_on_time",
                Title = "The team asks to leave on time all week",
                Body = "Everyone is running on fumes and asks for one week of leaving on time.",
                BaseSuccessChance = 0.75f,
                OnAcceptSuccess = new EventOutcome { TeamFatigueDelta = -12f, TeamMoraleDelta = 10f, Text = "Everyone comes back noticeably sharper." },
                OnAcceptFail = new EventOutcome { ProgressDelta = -6f, TeamFatigueDelta = -6f, Text = "They did get the rest, but the work piled up while they were gone." },
                OnDecline = new EventOutcome { TeamMoraleDelta = -10f, TeamFatigueDelta = 5f, Text = "The team stays late, but the mood is not the same afterwards." }
            },
            new EventDefinition
            {
                Id = "status_report",
                Title = "Leadership calls you in for a progress report",
                Body = "They want you to walk them through the state of the project yourself.",
                BaseSuccessChance = 0.65f,
                OnAcceptSuccess = new EventOutcome { CredibilityDelta = 12f, Text = "You explained it clearly and leadership relaxed." },
                OnAcceptFail = new EventOutcome { CredibilityDelta = -12f, DeadlineDelta = -1, Text = "Your answers were vague, so they asked for delivery sooner." },
                OnDecline = new EventOutcome { CredibilityDelta = -8f, Text = "You sent someone else, and the team saw you dodge it." }
            },
            new EventDefinition
            {
                Id = "personal_talk",
                Title = "A team member wants to talk about something personal",
                Body = "Someone asks for a word. They say it is not about work directly.",
                BaseSuccessChance = 0.70f,
                OnAcceptSuccess = new EventOutcome { TeamMoraleDelta = 12f, TeamFatigueDelta = 4f, Text = "You talked it through and they left a great deal lighter." },
                OnAcceptFail = new EventOutcome { TeamMoraleDelta = -6f, CredibilityDelta = -8f, Text = "You said the wrong thing at the wrong moment and it spread through the whole team." },
                OnDecline = new EventOutcome { TeamMoraleDelta = -8f, Text = "You told them it could wait. They never came back." }
            },
            new EventDefinition
            {
                Id = "warn_client_chase",
                Title = "The client has started chasing",
                Body = "Two chasing emails back to back, and the tone has changed.",
                WarningLeadTurns = 2,
                WarningDeadlineCut = 2
            },
            new EventDefinition
            {
                Id = "warn_sales_promise",
                Title = "Sales already promised the client a date",
                Body = "Someone committed to a new delivery date without asking you.",
                WarningLeadTurns = 2,
                WarningDeadlineCut = 2
            }
        };
    }
}
