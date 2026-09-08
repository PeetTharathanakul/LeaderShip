using UnityEngine;

namespace LeaderShip.Core
{
    /// <summary>
    /// Singleton แบบต่อซีน ตามกฎใน .agents/AGENTS.md
    ///
    /// **MANAGER เป็น singleton ได้ ACTOR ห้าม** — manager คือของที่มีชิ้นเดียวโดยนิยาม
    /// (การคุมรอบเล่น การคุมเทิร์น) ส่วน actor เช่นลูกทีมหรือการ์ด UI ห้ามเป็น singleton เด็ดขาด
    /// แม้ตอนนี้ในซีนจะมีชิ้นเดียวก็ตาม
    ///
    /// จงใจ **ไม่** ใช้ DontDestroyOnLoad — การเริ่มรอบใหม่คือการโหลดซีนใหม่
    /// และ getter คืน null จริงเมื่ออ็อบเจกต์ถูกทำลายไปแล้ว
    /// </summary>
    public abstract class SceneSingleton<T> : MonoBehaviour where T : SceneSingleton<T>
    {
        static T _instance;

        public static T Instance => _instance == null ? null : _instance;
        public static bool Exists => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // รายงาน ไม่เงียบ ๆ ทำลายทิ้ง — ซีนที่มีสองตัวคือบั๊กที่ต้องเห็น
                Debug.LogError(
                    $"[{typeof(T).Name}] มีอยู่ในซีนมากกว่าหนึ่งตัว: '{name}' กับ '{_instance.name}' " +
                    "ตัวที่สองจะถูกเมิน แก้ซีนด้วย", this);
                return;
            }

            _instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
