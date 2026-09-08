using LeaderShip.Model;
using UnityEngine;

namespace LeaderShip.Data
{
    /// <summary>ลูกทีมหนึ่งคน — ดูกฎ "หนึ่งคลาสต่อหนึ่งไฟล์" ที่ <see cref="BalanceConfigSO"/></summary>
    [CreateAssetMenu(menuName = "LeaderShip/Member", fileName = "Member")]
    public sealed class MemberDefinitionSO : ScriptableObject
    {
        [SerializeField] MemberDefinition definition = new MemberDefinition();
        public MemberDefinition Definition => definition;

#if UNITY_EDITOR
        public void EditorSetData(MemberDefinition value) => definition = value;
#endif
    }
}
