using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeaderShip.UI
{
    /// <summary>
    /// แถบค่าหนึ่งแถบ (ความคืบหน้า / ความเหนื่อยล้า / กำลังใจ / ความน่าเชื่อถือ)
    ///
    /// ใช้การขยับ anchor แทน Image.Type.Filled เพราะสไปรต์ที่ได้มาเป็น 9-slice
    /// ซึ่ง Filled ใช้ร่วมกับ Sliced ไม่ได้ — ขอบจะยืดเพี้ยน
    /// </summary>
    public sealed class BarView : MonoBehaviour
    {
        [SerializeField] RectTransform fill;
        [SerializeField] Image fillImage;
        [SerializeField] TMP_Text label;

        void Awake()
        {
            if (fill == null) Debug.LogError($"[{nameof(BarView)}] '{name}' ไม่ได้ใส่ fill", this);
            if (fillImage == null) Debug.LogError($"[{nameof(BarView)}] '{name}' ไม่ได้ใส่ fillImage", this);
        }

        public void Set(float value01, string text, Color color)
        {
            value01 = Mathf.Clamp01(value01);

            if (fill != null)
            {
                var max = fill.anchorMax;
                max.x = value01;
                fill.anchorMax = max;

                // ค่าที่มากกว่า 0 แต่น้อยมากต้องยังเห็นเป็นเส้นบาง ๆ ไม่ใช่หายไปเฉย ๆ
                fill.gameObject.SetActive(value01 > 0.0001f);
            }

            if (fillImage != null) fillImage.color = color;
            if (label != null) label.text = text;
        }

#if UNITY_EDITOR
        public void EditorAssign(RectTransform fillRect, Image image, TMP_Text text)
        {
            fill = fillRect;
            fillImage = image;
            label = text;
        }
#endif
    }
}
