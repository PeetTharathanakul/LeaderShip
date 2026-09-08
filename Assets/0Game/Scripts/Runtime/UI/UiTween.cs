using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace LeaderShip.UI
{
    /// <summary>
    /// คำศัพท์อนิเมชันกลางของเกม — ทุก View เรียกจากที่นี่ ไม่เขียน DOTween กระจายเอง
    ///
    /// ทำไมต้องรวม: ถ้าปล่อยให้แต่ละ View ตั้งเวลาและ ease เอง หน้าจอจะขยับคนละจังหวะ
    /// แล้วผู้เล่นจะอ่านไม่ออกว่าอะไรสำคัญกว่ากัน จังหวะเวลาเป็นส่วนหนึ่งของการสื่อสาร ไม่ใช่ของตกแต่ง
    ///
    /// ทุกตัวใช้ <c>SetUpdate(true)</c> (unscaled time) เพราะถ้าวันไหนมีการหยุดเวลา
    /// ป๊อปอัปกับแฟลชต้องยังเล่นได้ ไม่งั้นเกมจะค้างแบบหาสาเหตุไม่เจอ
    /// และใช้ <c>SetLink</c> ทุกตัว เพื่อให้ tween ตายไปพร้อมอ็อบเจกต์ ไม่ค้างไปเขียนของที่ถูกทำลายแล้ว
    /// </summary>
    public static class UiTween
    {
        public const float Fast = 0.12f;
        public const float Normal = 0.22f;
        public const float Slow = 0.38f;

        /// <summary>ฆ่า tween ทั้งหมดของเป้าหมายก่อนเริ่มตัวใหม่ กัน tween ซ้อนกันแล้วค่าค้างกลางทาง</summary>
        public static void Kill(Component target, bool complete = false)
        {
            if (target == null) return;
            DOTween.Kill(target, complete);
            DOTween.Kill(target.transform, complete);
        }

        /// <summary>โผล่เข้ามาแบบเด้งเล็กน้อย ใช้กับแผงและป๊อปอัปทุกใบให้เหมือนกันหมด</summary>
        public static Sequence PopIn(RectTransform rect, CanvasGroup group, float from = 0.88f, float duration = Normal)
        {
            if (rect == null) return null;

            Kill(rect);
            rect.localScale = Vector3.one * from;

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(rect.gameObject);
            seq.Join(rect.DOScale(1f, duration).SetEase(Ease.OutBack, 1.4f));

            if (group != null)
            {
                group.alpha = 0f;
                seq.Join(group.DOFade(1f, duration * 0.8f).SetEase(Ease.OutQuad));
            }

            return seq;
        }

        /// <summary>หายไปแบบยุบลงนิดหนึ่ง — คู่กับ <see cref="PopIn"/></summary>
        public static Sequence PopOut(RectTransform rect, CanvasGroup group, Action onComplete = null,
            float duration = Fast)
        {
            if (rect == null)
            {
                onComplete?.Invoke();
                return null;
            }

            Kill(rect);

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(rect.gameObject);
            seq.Join(rect.DOScale(0.94f, duration).SetEase(Ease.InQuad));
            if (group != null) seq.Join(group.DOFade(0f, duration).SetEase(Ease.InQuad));
            WhenDone(seq, onComplete);

            return seq;
        }

        /// <summary>
        /// เรียก <paramref name="onDone"/> ครั้งเดียวเสมอ ไม่ว่าซีเควนซ์จะจบเองหรือถูกฆ่ากลางทาง
        ///
        /// จำเป็นเพราะ callback พวกนี้เป็นตัวปลดล็อกอินพุตของเกม
        /// ถ้ามันไม่ยิง ผู้เล่นจะกดอะไรไม่ได้อีกเลยและไม่มีทางรู้ว่าทำไม
        /// </summary>
        public static void WhenDone(Sequence sequence, Action onDone)
        {
            if (onDone == null) return;

            if (sequence == null)
            {
                onDone();
                return;
            }

            bool fired = false;
            void Fire()
            {
                if (fired) return;
                fired = true;
                onDone();
            }

            sequence.OnComplete(Fire);
            sequence.OnKill(Fire);
        }

        /// <summary>
        /// กระตุกเพื่อบอกว่า "ตรงนี้เพิ่งเปลี่ยน" ใช้กับปุ่มที่เพิ่งกด และการ์ดที่เพิ่งถูกเลือก
        ///
        /// <paramref name="killExisting"/> ต้องเป็น false เมื่อเป้าหมายกำลังมี tween ตัวอื่นทำงานอยู่
        /// และเราตั้งใจให้มันทำงานต่อ — <c>DOTween.Kill(target)</c> ฆ่า **ทุก** tween ของอ็อบเจกต์นั้น
        /// รวมถึงตัวที่ถูก nest อยู่ใน Sequence ซึ่งจะทำให้ทั้ง Sequence ตายกลางทางแบบเงียบ ๆ
        /// (เคยพังมาแล้ว: แฟลชกลางจอค้าง ป๊อปอัปไม่เคยขึ้น เพราะ Punch ไปฆ่า DOScale ในซีเควนซ์เดียวกัน)
        /// </summary>
        public static Tween Punch(Transform target, float strength = 0.14f, float duration = 0.3f,
            bool killExisting = true)
        {
            if (target == null) return null;

            if (killExisting)
            {
                DOTween.Kill(target);
                target.localScale = Vector3.one;
            }

            return target.DOPunchScale(Vector3.one * strength, duration, 8, 0.9f)
                .SetUpdate(true)
                .SetLink(target.gameObject);
        }

        /// <summary>เขย่าเพื่อบอกว่า "อันนี้แย่" ใช้ตอนคำสั่งล้มเหลวหรือถูกปฏิเสธ</summary>
        public static Tween Shake(RectTransform rect, float strength = 22f, float duration = 0.38f)
        {
            if (rect == null) return null;

            return rect.DOPunchAnchorPos(new Vector2(strength, 0f), duration, 12, 1f)
                .SetUpdate(true)
                .SetLink(rect.gameObject);
        }

        /// <summary>เลื่อนเข้ามาพร้อมจางเข้า ใช้ไล่ทีละบรรทัดในป๊อปอัปสรุปให้ตาไล่อ่านตามทัน</summary>
        public static Sequence SlideIn(RectTransform rect, CanvasGroup group, Vector2 offset,
            float delay, float duration = Normal)
        {
            if (rect == null) return null;

            Kill(rect);

            Vector2 target = rect.anchoredPosition;
            rect.anchoredPosition = target + offset;
            if (group != null) group.alpha = 0f;

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(rect.gameObject).SetDelay(delay);
            seq.Join(rect.DOAnchorPos(target, duration).SetEase(Ease.OutCubic));
            if (group != null) seq.Join(group.DOFade(1f, duration).SetEase(Ease.OutQuad));

            return seq;
        }

        /// <summary>
        /// วิ่งตัวเลขจากค่าเดิมไปค่าใหม่ แทนที่จะกระโดดทันที
        /// ผู้เล่นต้องเห็น "มันขยับไปเท่าไหร่" ไม่ใช่แค่ "ตอนนี้เป็นเท่าไหร่"
        /// </summary>
        public static Tween CountTo(TMP_Text label, float from, float to, Func<float, string> format,
            float duration = Slow)
        {
            if (label == null || format == null) return null;

            DOTween.Kill(label);

            if (Mathf.Approximately(from, to))
            {
                label.text = format(to);
                return null;
            }

            float value = from;
            return DOTween.To(() => value, v =>
                {
                    value = v;
                    label.text = format(v);
                }, to, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetTarget(label)
                .SetLink(label.gameObject);
        }

        /// <summary>ค่อย ๆ เติมแถบแทนการกระโดด ใช้กับ <see cref="BarView"/> ทุกแถบ</summary>
        public static Tween FillTo(RectTransform fill, float value01, float duration = Slow)
        {
            if (fill == null) return null;

            DOTween.Kill(fill);

            float from = fill.anchorMax.x;
            float to = Mathf.Clamp01(value01);

            if (Mathf.Approximately(from, to))
            {
                SetFill(fill, to);
                return null;
            }

            float value = from;
            return DOTween.To(() => value, v =>
                {
                    value = v;
                    SetFill(fill, v);
                }, to, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetTarget(fill)
                .SetLink(fill.gameObject);
        }

        static void SetFill(RectTransform fill, float value01)
        {
            var max = fill.anchorMax;
            max.x = value01;
            fill.anchorMax = max;

            // ค่าที่มากกว่า 0 แต่น้อยมากต้องยังเห็นเป็นเส้นบาง ๆ ไม่ใช่หายไปเฉย ๆ
            fill.gameObject.SetActive(value01 > 0.0001f);
        }

        /// <summary>สีของบรรทัด "ได้/เสีย" — แปลง Sentiment ของชั้นโมเดลเป็นสีของเกม</summary>
        public static Color ToneColor(Model.Sentiment tone)
        {
            switch (tone)
            {
                case Model.Sentiment.Good: return Palette.Good;
                case Model.Sentiment.Bad: return Palette.Danger;
                default: return Palette.TextMuted;
            }
        }
    }
}
