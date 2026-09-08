using LeaderShip.Model;
using UnityEngine;

namespace LeaderShip.Data
{
    /// <summary>เหตุการณ์หนึ่งใบ — ดูกฎ "หนึ่งคลาสต่อหนึ่งไฟล์" ที่ <see cref="BalanceConfigSO"/></summary>
    [CreateAssetMenu(menuName = "LeaderShip/Event", fileName = "Event")]
    public sealed class EventDefinitionSO : ScriptableObject
    {
        [SerializeField] EventDefinition definition = new EventDefinition();
        public EventDefinition Definition => definition;

#if UNITY_EDITOR
        public void EditorSetData(EventDefinition value) => definition = value;
#endif
    }
}
