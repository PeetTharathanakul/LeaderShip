using LeaderShip.Model;
using UnityEngine;

namespace LeaderShip.Data
{
    /// <summary>คำสั่งหนึ่งคำสั่ง — ดูกฎ "หนึ่งคลาสต่อหนึ่งไฟล์" ที่ <see cref="BalanceConfigSO"/></summary>
    [CreateAssetMenu(menuName = "LeaderShip/Command", fileName = "Command")]
    public sealed class CommandDefinitionSO : ScriptableObject
    {
        [SerializeField] CommandDefinition definition = new CommandDefinition();
        public CommandDefinition Definition => definition;

#if UNITY_EDITOR
        public void EditorSetData(CommandDefinition value) => definition = value;
#endif
    }
}
