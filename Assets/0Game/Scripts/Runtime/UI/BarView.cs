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

        float _value;
        bool _hasValue;

        void Awake()
        {
            if (fill == null) Debug.LogError($"[{nameof(BarView)}] '{name}' ไม่ได้ใส่ fill", this);
            if (fillImage == null) Debug.LogError($"[{nameof(BarView)}] '{name}' ไม่ได้ใส่ fillImage", this);
        }

        /// <summary>
        /// ตั้งค่าแถบ โดยค่อย ๆ วิ่งไปหาค่าใหม่แทนการกระโดด
        /// ผู้เล่นต้องเห็นว่า "มันขยับไปเท่าไหร่" ไม่ใช่แค่ "ตอนนี้เป็นเท่าไหร่"
        /// ครั้งแรกที่วาด (<see cref="_hasValue"/> ยังเป็น false) จะเซ็ตทันที ไม่ให้ทุกแถบวิ่งจากศูนย์ตอนเปิดเกม
        /// </summary>
        public void Set(float value01, string text, Color color)
        {
            value01 = Mathf.Clamp01(value01);

            if (fill != null)
            {
                if (_hasValue && !Mathf.Approximately(_value, value01)) UiTween.FillTo(fill, value01);
                else SetImmediate(value01);
            }

            _value = value01;
            _hasValue = true;

            if (fillImage != null) fillImage.color = color;
            if (label != null) label.text = text;
        }

        void SetImmediate(float value01)
        {
            UiTween.Kill(fill);

            var max = fill.anchorMax;
            max.x = value01;
            fill.anchorMax = max;

            // ค่าที่มากกว่า 0 แต่น้อยมากต้องยังเห็นเป็นเส้นบาง ๆ ไม่ใช่หายไปเฉย ๆ
            fill.gameObject.SetActive(value01 > 0.0001f);
        }

        void OnDisable()
        {
            UiTween.Kill(fill);
            _hasValue = false;
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
