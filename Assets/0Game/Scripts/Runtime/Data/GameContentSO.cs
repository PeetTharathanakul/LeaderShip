using System.Collections.Generic;
using LeaderShip.Model;
using UnityEngine;

namespace LeaderShip.Data
{
    /// <summary>
    /// รวมทุกอย่างเข้าเป็น GameContent ก้อนเดียวให้ชั้นโมเดลใช้
    /// ดูกฎ "หนึ่งคลาสต่อหนึ่งไฟล์" ที่ <see cref="BalanceConfigSO"/>
    /// </summary>
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
