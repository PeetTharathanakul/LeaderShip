using System.Collections.Generic;
using LeaderShip.Model;
using UnityEngine;

namespace LeaderShip.Data
{
    /// <summary>
    /// ค่าบาลานซ์ทั้งหมดของเกม — ตาม .agents/AGENTS.md ต้องอยู่ใน ScriptableObject ห้าม hardcode
    /// แก้ไฟล์นี้แล้วรัน Balance Simulator ซ้ำได้ทันที ไม่ต้องคอมไพล์ใหม่
    /// </summary>
    [CreateAssetMenu(menuName = "LeaderShip/Balance Config", fileName = "BalanceConfig")]
    public sealed class BalanceConfigSO : ScriptableObject
    {
        [SerializeField] BalanceData data = new BalanceData();

        /// <summary>คืนสำเนาเสมอ เพื่อไม่ให้รอบเล่นเขียนทับ asset บนดิสก์ตอนรันใน Editor</summary>
        public BalanceData CreateData() => data.Clone();

#if UNITY_EDITOR
        public void EditorSetData(BalanceData value) => data = value;
#endif
    }

    [CreateAssetMenu(menuName = "LeaderShip/Member", fileName = "Member")]
    public sealed class MemberDefinitionSO : ScriptableObject
    {
        [SerializeField] MemberDefinition definition = new MemberDefinition();
        public MemberDefinition Definition => definition;

#if UNITY_EDITOR
        public void EditorSetData(MemberDefinition value) => definition = value;
#endif
    }

    [CreateAssetMenu(menuName = "LeaderShip/Command", fileName = "Command")]
    public sealed class CommandDefinitionSO : ScriptableObject
    {
        [SerializeField] CommandDefinition definition = new CommandDefinition();
        public CommandDefinition Definition => definition;

#if UNITY_EDITOR
        public void EditorSetData(CommandDefinition value) => definition = value;
#endif
    }

    [CreateAssetMenu(menuName = "LeaderShip/Event", fileName = "Event")]
    public sealed class EventDefinitionSO : ScriptableObject
    {
        [SerializeField] EventDefinition definition = new EventDefinition();
        public EventDefinition Definition => definition;

#if UNITY_EDITOR
        public void EditorSetData(EventDefinition value) => definition = value;
#endif
    }

    /// <summary>รวมทุกอย่างเข้าเป็น GameContent ก้อนเดียวให้ชั้นโมเดลใช้</summary>
    [CreateAssetMenu(menuName = "LeaderShip/Game Content", fileName = "GameContent")]
    public sealed class GameContentSO : ScriptableObject
    {
        [SerializeField] BalanceConfigSO balance;
        [SerializeField] MemberDefinitionSO[] members;
        [SerializeField] CommandDefinitionSO[] commands;
        [SerializeField] EventDefinitionSO[] events;

        public bool IsComplete => balance != null
                                  && members != null && members.Length > 0
                                  && commands != null && commands.Length >= 4;

        public GameContent Build()
        {
            if (balance == null)
            {
                Debug.LogError($"[{name}] ไม่ได้ใส่ BalanceConfig — จะใช้ค่า default แทน ซึ่งไม่ใช่สิ่งที่ควรเกิดตอนรันจริง", this);
            }

            var memberList = new List<MemberDefinition>();
            if (members != null)
            {
                foreach (var m in members)
                {
                    if (m == null) { Debug.LogError($"[{name}] มีช่องลูกทีมที่ว่างอยู่", this); continue; }
                    memberList.Add(m.Definition);
                }
            }

            var commandList = new List<CommandDefinition>();
            if (commands != null)
            {
                foreach (var c in commands)
                {
                    if (c == null) { Debug.LogError($"[{name}] มีช่องคำสั่งที่ว่างอยู่", this); continue; }
                    commandList.Add(c.Definition);
                }
            }

            var eventList = new List<EventDefinition>();
            if (events != null)
            {
                foreach (var e in events)
                {
                    if (e == null) { Debug.LogError($"[{name}] มีช่องเหตุการณ์ที่ว่างอยู่", this); continue; }
                    eventList.Add(e.Definition);
                }
            }

            var content = new GameContent
            {
                Balance = balance != null ? balance.CreateData() : new BalanceData(),
                Members = memberList.ToArray(),
                Commands = commandList.ToArray(),
                Events = eventList.ToArray()
            };

            content.Validate();
            return content;
        }

#if UNITY_EDITOR
        /// <summary>ใช้โดย Content Bootstrapper เท่านั้น</summary>
        public void EditorAssign(BalanceConfigSO b, MemberDefinitionSO[] m, CommandDefinitionSO[] c, EventDefinitionSO[] e)
        {
            balance = b;
            members = m;
            commands = c;
            events = e;
        }
#endif
    }
}
