using System;
using DG.Tweening;
using LeaderShip.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeaderShip.UI
{
    /// <summary>
    /// การ์ดลูกทีมหนึ่งใบ
    /// เป็น **actor** ไม่ใช่ manager จึงห้ามเป็น singleton และห้ามถูกค้นด้วย FindObjectOfType
    /// ตัวควบคุมหน้าจอเป็นคนฉีดข้อมูลและรับ event กลับ (.agents/AGENTS.md)
    /// </summary>
    public sealed class MemberCardView : MonoBehaviour
    {
        [SerializeField] Image frame;
        [SerializeField] Button selectButton;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text nicknameText;
        [SerializeField] TMP_Text outputText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] BarView fatigueBar;
        [SerializeField] BarView moraleBar;
        [SerializeField] TMP_Text selectedTag;

        public event Action<int> Selected;

        int _index = -1;
        public int Index => _index;

        RectTransform _rect;
        Vector2 _restPosition;
        bool _wasSelected;
        bool _hasSelectionState;

        /// <summary>ใบที่ถูกเลือกยกตัวขึ้นเล็กน้อย — ความต่างเชิงตำแหน่งอ่านง่ายกว่าความต่างของสีขอบ</summary>
        const float SelectedLift = 10f;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect != null) _restPosition = _rect.anchoredPosition;

            if (selectButton == null) Debug.LogError($"[{nameof(MemberCardView)}] '{name}' ไม่ได้ใส่ selectButton", this);
            else selectButton.onClick.AddListener(() => Selected?.Invoke(_index));
        }

        public void Bind(int index) => _index = index;

        public void Refresh(TurnEngine engine, bool isSelected)
        {
            if (engine == null || _index < 0 || _index >= engine.State.Members.Count) return;

            var state = engine.State;
            var b = state.Balance;
            var member = state.Members[_index];

            if (nameText != null) nameText.text = member.Def.DisplayName;
            if (nicknameText != null) nicknameText.text = $"“{member.Def.Nickname}”";

            if (outputText != null)
                outputText.text = $"Output per turn {GameText.Signed(member.Output(b))}%";

            if (fatigueBar != null)
            {
                float t = member.Fatigue / 100f;
                fatigueBar.Set(t, $"Fatigue {GameText.Num(member.Fatigue, 0)}", Palette.FatigueColor(t));
            }

            if (moraleBar != null)
                moraleBar.Set(member.Morale / 100f, $"Morale {GameText.Num(member.Morale, 0)}", Palette.Morale);

            if (statusText != null)
            {
                if (member.IsBurnedOut)
                {
                    statusText.text = $"Burned out — no orders until rest brings Fatigue below {GameText.Num(b.BurnoutExit, 0)}";
                    statusText.color = Palette.Danger;
                }
                else if (member.Fatigue >= b.PushTiredThreshold)
                {
                    statusText.text = "Exhausted — pushing now costs credibility";
                    statusText.color = Palette.Warning;
                }
                else if (member.LastCommand.HasValue && member.ConsecutiveCount >= 2)
                {
                    statusText.text = $"Given “{GameText.CommandName(member.LastCommand.Value)}” {member.ConsecutiveCount}x in a row";
                    statusText.color = Palette.Warning;
                }
                else
                {
                    statusText.text = "";
                    statusText.color = Palette.TextMuted;
                }
            }

            ApplySelection(isSelected);
        }

        /// <summary>
        /// สถานะ "กำลังสั่งใบนี้" บอกด้วยสามอย่างพร้อมกัน: สีขอบ · ป้ายข้อความ · ตำแหน่งที่ยกขึ้น
        /// อย่างเดียวไม่พอ เพราะขอบสีเข้มบนพื้นเข้มมองข้ามได้ง่ายมาก
        /// </summary>
        void ApplySelection(bool isSelected)
        {
            if (frame != null)
                frame.color = isSelected ? Palette.PanelBorderActive : Palette.PanelBorder;

            if (selectedTag != null)
            {
                selectedTag.gameObject.SetActive(isSelected);
                selectedTag.color = Palette.PanelBorderActive;
            }

            bool changed = !_hasSelectionState || _wasSelected != isSelected;
            _wasSelected = isSelected;
            _hasSelectionState = true;

            if (_rect == null) return;

            Vector2 target = _restPosition + new Vector2(0f, isSelected ? SelectedLift : 0f);

            if (!changed)
            {
                _rect.anchoredPosition = target;
                return;
            }

            UiTween.Kill(_rect);
            _rect.DOAnchorPos(target, UiTween.Normal).SetEase(Ease.OutCubic)
                .SetUpdate(true).SetLink(gameObject);

            // killExisting: false — DOAnchorPos ข้างบนเพิ่งเริ่มบนอ็อบเจกต์เดียวกัน ห้ามไปฆ่ามันทิ้ง
            if (isSelected) UiTween.Punch(transform, 0.05f, 0.24f, killExisting: false);
        }

        void OnDisable()
        {
            UiTween.Kill(_rect);
            if (_rect != null) _rect.anchoredPosition = _restPosition;
            _hasSelectionState = false;
        }

#if UNITY_EDITOR
        public void EditorAssign(Image frameImage, Button button, TMP_Text nameLabel, TMP_Text nicknameLabel,
            TMP_Text outputLabel, TMP_Text status, BarView fatigue, BarView morale, TMP_Text selectedLabel)
        {
            frame = frameImage;
            selectButton = button;
            nameText = nameLabel;
            nicknameText = nicknameLabel;
            outputText = outputLabel;
            statusText = status;
            fatigueBar = fatigue;
            moraleBar = morale;
            selectedTag = selectedLabel;
        }
#endif
    }
}
