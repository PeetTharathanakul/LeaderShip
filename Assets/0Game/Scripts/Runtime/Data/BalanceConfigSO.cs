using LeaderShip.Model;
using UnityEngine;

namespace LeaderShip.Data
{
    /// <summary>
    /// ค่าบาลานซ์ทั้งหมดของเกม — ตาม .agents/AGENTS.md ต้องอยู่ใน ScriptableObject ห้าม hardcode
    /// แก้ไฟล์นี้แล้วรัน Balance Simulator ซ้ำได้ทันที ไม่ต้องคอมไพล์ใหม่
    ///
    /// **หนึ่งคลาสต่อหนึ่งไฟล์ และชื่อไฟล์ต้องตรงกับชื่อคลาส** — ห้ามรวมกลับไปไว้ไฟล์เดียวอีก
    /// Unity ผูก MonoScript ให้เฉพาะคลาสแรกของไฟล์ ที่เหลือจะถูกเซฟเป็น `m_Script: {fileID: 0}`
    /// ซึ่งใน Editor ยังพอทำงานได้ แต่ใน **build** อ็อบเจกต์จะโหลดไม่ขึ้นและกลายเป็น null เงียบ ๆ
    /// (เคยพังมาแล้ว: เกมที่บิลด์ออกมาเปิดแล้วไม่เล่น เพราะ GameContent เป็น null)
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
}
