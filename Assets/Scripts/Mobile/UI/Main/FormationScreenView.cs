using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 편성 화면 (프리팹 FormationScreen, 와이어프레임 '수확기 공방' 배치):
    //   위: 편성 칸 줄(Formation.SlotCount칸) + 옆 버튼(펫 · 무기 · 방어구 · 자동 편성)
    //   가운데: 고른 꼬마 정보 — 그림·레벨 / 이름·공격 배율(다음 레벨 값은 초록) / 조각 막대 / 합성 · 편성 버튼
    //   아래: 꼬마 목록 (세로 스크롤)
    // 칸을 누르면 그 칸을 "놓을 자리"로 고르고(그 칸 꼬마가 있으면 정보도 띄운다), 목록에서 꼬마를 누르면 정보를 띄운다.
    // "편성" = 고른 자리(없으면 첫 빈 칸)에 놓기, 이미 편성 중이면 "편성 해제". 펫·무기·방어구는 Equit의 기존 창(TabGroup 0·1·2)을 연다
    public class FormationScreenView : MonoBehaviour
    {
        [Header("편성 칸 줄")]
        [SerializeField] RectTransform slotRow;
        [SerializeField] FormationCellView slotTemplate;     // 꺼 둔 원본

        [Header("옆 버튼")]
        // 펫·무기·방어구 버튼은 Equit의 TabGroup 탭 버튼으로 씬에서 연결한다 (누르면 TabGroup이 그 창을 연다)
        [SerializeField] Button autoButton;

        [Header("꼬마 정보")]
        [SerializeField] Image infoIcon;
        [SerializeField] TMP_Text infoLevel;
        [SerializeField] TMP_Text infoName;
        [SerializeField] TMP_Text infoStat;
        [SerializeField] TMP_Text infoNext;
        [SerializeField] RectTransform infoBarFill;           // 조각 막대 (anchorMax.x = 비율)
        [SerializeField] TMP_Text infoBarText;
        [SerializeField] Button mergeButton;
        [SerializeField] Button placeButton;
        [SerializeField] TMP_Text placeLabel;
        [SerializeField] Button mergeAllButton;
        [SerializeField] TMP_Text message;

        [Header("꼬마 목록")]
        [SerializeField] RectTransform grid;
        [SerializeField] FormationCellView cellTemplate;     // 꺼 둔 원본

        readonly List<FormationCellView> slots = new List<FormationCellView>();
        readonly List<FormationCellView> cells = new List<FormationCellView>();
        string selectedId;
        int targetSlot = -1;

        void Awake()
        {
            if (slotTemplate != null)
            {
                slotTemplate.gameObject.SetActive(false);
                for (int i = 0; i < Formation.SlotCount; i++)
                {
                    var s = Instantiate(slotTemplate, slotRow);
                    s.name = "Slot" + (i + 1);
                    s.slot = i;
                    s.gameObject.SetActive(true);
                    int slot = i;
                    s.button.onClick.AddListener(() => PickSlot(slot));
                    slots.Add(s);
                }
            }
            if (cellTemplate != null)
            {
                cellTemplate.gameObject.SetActive(false);
                foreach (var id in Formation.Ids)
                {
                    var c = Instantiate(cellTemplate, grid);
                    c.name = "Cell_" + id;
                    c.id = id;
                    c.gameObject.SetActive(true);
                    string pick = id;
                    c.button.onClick.AddListener(() => Select(pick));
                    cells.Add(c);
                }
            }
            if (autoButton != null) autoButton.onClick.AddListener(Formation.AutoArrange);
            if (mergeButton != null) mergeButton.onClick.AddListener(MergeSelected);
            if (mergeAllButton != null) mergeAllButton.onClick.AddListener(MergeAll);
            if (placeButton != null) placeButton.onClick.AddListener(PlaceSelected);
        }

        void OnEnable()
        {
            Formation.Changed += Refresh;
            if (string.IsNullOrEmpty(selectedId)) selectedId = FirstShown();
            Refresh();
        }

        void OnDisable() => Formation.Changed -= Refresh;

        string FirstShown()
        {
            for (int i = 0; i < Formation.SlotCount; i++) if (!Formation.IsEmpty(i)) return Formation.Get(i);
            return Formation.Ids.Length > 0 ? Formation.Ids[0] : null;
        }

        void PickSlot(int slot)
        {
            targetSlot = targetSlot == slot ? -1 : slot;
            if (!Formation.IsEmpty(slot)) selectedId = Formation.Get(slot);
            if (message != null) message.text = "";
            Refresh();
        }

        void Select(string id)
        {
            selectedId = id;
            if (message != null) message.text = "";
            Refresh();
        }

        void PlaceSelected()
        {
            if (string.IsNullOrEmpty(selectedId) || !Formation.IsOwned(selectedId)) return;
            int from = Formation.SlotOf(selectedId);
            if (from >= 0 && (targetSlot < 0 || targetSlot == from)) { Formation.Clear(from); return; }   // 편성 해제
            int to = targetSlot;
            if (to < 0) for (int i = 0; i < Formation.SlotCount; i++) if (Formation.IsEmpty(i)) { to = i; break; }
            if (to < 0) { if (message != null) message.text = "놓을 칸을 먼저 고르세요"; return; }
            Formation.Place(to, selectedId);
            targetSlot = -1;
        }

        void MergeSelected()
        {
            int up = Formation.Merge(selectedId);
            if (message != null) message.text = up > 0 ? $"{Formation.Name(selectedId)} Lv.{Formation.Level(selectedId)} (+{up})" : "";
            Refresh();
        }

        void MergeAll()
        {
            int up = Formation.MergeAll();
            if (message != null) message.text = up > 0 ? $"일괄 합성: 꼬마 레벨 +{up}" : "합성할 꼬마가 없습니다";
            Refresh();
        }

        void Refresh()
        {
            foreach (var s in slots)
            {
                string id = Formation.Get(s.slot);
                bool has = !string.IsNullOrEmpty(id);
                s.icon.sprite = has ? Formation.Icon(id) : null;
                s.icon.enabled = has;
                if (s.levelText != null) s.levelText.text = has ? "Lv." + Formation.Level(id) : "";
                if (s.selection != null) s.selection.SetActive(s.slot == targetSlot);
            }
            bool anyMerge = false;
            foreach (var c in cells)
            {
                int lv = Formation.Level(c.id);
                bool have = lv > 0;
                c.icon.sprite = Formation.Icon(c.id);
                if (c.lockMark != null) c.lockMark.SetActive(!have);   // 못 얻은 꼬마: 색을 바꾸지 않고 자물쇠 그림을 얹는다
                if (c.nameText != null) c.nameText.text = Formation.Name(c.id);
                if (c.levelText != null) c.levelText.text = have ? "Lv." + lv : "미보유";
                if (c.badge != null) c.badge.SetActive(Formation.SlotOf(c.id) >= 0);
                if (c.selection != null) c.selection.SetActive(c.id == selectedId);
                if (Formation.CanMerge(c.id)) anyMerge = true;
            }
            if (mergeAllButton != null) mergeAllButton.interactable = anyMerge;
            RefreshInfo();
        }

        void RefreshInfo()
        {
            string id = selectedId;
            if (string.IsNullOrEmpty(id)) return;
            int level = Formation.Level(id), copies = Formation.Copies(id), need = Formation.CopiesToNext(level);
            bool have = level > 0, max = level >= Formation.MaxLevel;
            int stage = Formation.EvoStage(id);
            if (infoIcon != null) infoIcon.sprite = Formation.Icon(id);
            if (infoLevel != null) infoLevel.text = have ? (max ? $"Lv.{level} MAX" : $"Lv.{level}") : "미보유";
            if (infoName != null) infoName.text = $"[{stage}단계] {Formation.Name(id)}";
            int shown = Mathf.Max(1, level);
            if (infoStat != null) infoStat.text = $"공격 배율 ×{Formation.AttackMultiplierAt(id, shown):0.00}";
            if (infoNext != null)
            {
                string evo = stage < Formation.EvoStageCount ? $"Lv.{Formation.EvoLevel(stage + 1)} {stage + 1}단계 진화" : "최종 진화";
                infoNext.text = have && !max ? $"다음 레벨 <color=#3A8F2E>×{Formation.AttackMultiplierAt(id, level + 1):0.00}</color>  ·  {evo}" : evo;
            }
            if (infoBarFill != null) infoBarFill.anchorMax = new Vector2(max ? 1f : have ? Mathf.Clamp01(copies / (float)need) : 0f, 1f);
            if (infoBarText != null) infoBarText.text = !have ? "꼬마 뽑기로 얻을 수 있습니다" : max ? "최고 레벨" : $"조각 {copies}/{need}";
            if (mergeButton != null) mergeButton.interactable = Formation.CanMerge(id);
            int from = Formation.SlotOf(id);
            if (placeButton != null) placeButton.interactable = have;
            if (placeLabel != null) placeLabel.text = from >= 0 && (targetSlot < 0 || targetSlot == from) ? "편성 해제" : "편성";
        }
    }
}
