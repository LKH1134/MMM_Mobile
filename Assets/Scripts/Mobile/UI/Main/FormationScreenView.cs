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
        [SerializeField] MushroomTraitPanelView traitPanel;   // 버섯 특성 화면 (Equit 자식 프리팹). 버섯 특성 버튼이 안쪽을 이 화면으로 바꾼다

        [Header("꼬마 정보")]
        [SerializeField] Image infoIcon;
        [SerializeField] TMP_Text infoLevel;
        [SerializeField] TMP_Text infoName;
        [SerializeField] TMP_Text infoStat;
        [SerializeField] TMP_Text infoNext;
        [SerializeField] RectTransform infoBarFill;           // 조각 막대 (anchorMax.x = 비율)
        [SerializeField] TMP_Text infoBarText;
        [SerializeField] Button mergeButton;
        [SerializeField] Button growButton;                  // 관문(Lv.5·10)에서 합성 버튼 자리에 대신 나온다
        [SerializeField] TMP_Text growLabel;
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
            if (infoNext != null) infoNext.gameObject.SetActive(false);   // 초록색 "다음 레벨" 글자는 쓰지 않는다 (조각 막대를 가린다)
            BuildTraitButton();
            if (autoButton != null) autoButton.onClick.AddListener(OnAutoClicked);
            if (mergeButton != null) mergeButton.onClick.AddListener(MergeSelected);
            if (mergeAllButton != null) mergeAllButton.onClick.AddListener(OnMergeAllClicked);
            if (growButton != null) growButton.onClick.AddListener(GrowSelected);
            if (placeButton != null) placeButton.onClick.AddListener(PlaceSelected);
        }

        // 일괄 합성 · 자동 편성 줄을 [일괄 합성][자동 편성][버섯 특성] 세 칸으로 넓힌다.
        // 버섯 특성 버튼은 자동 편성 버튼을 복제해 모양을 맞춘다 (누르면 하는 일은 아직 없다).
        // 프리팹은 그대로 두고 코드로 만든다 — 줄을 못 찾으면 만들지 않는다
        void BuildTraitButton()
        {
            if (autoButton == null || mergeAllButton == null) return;
            var row = autoButton.transform.parent as RectTransform;
            if (row == null) return;

            var go = Instantiate(autoButton.gameObject, row);
            go.name = "Trait";
            var traitButton = go.GetComponent<Button>();
            traitButton.onClick.RemoveAllListeners();
            traitButton.onClick.AddListener(OnTraitClicked);
            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = Loc.T("formation.mushroom_trait", "버섯 특성");

            // 세 칸을 가운데 기준으로 나란히 (줄 폭에 맞춰 줄인다)
            const float gap = 10f, maxWidth = 277.2f;
            float rowWidth = row.rect.width > 100f ? row.rect.width - 60f : 960f;
            float w = Mathf.Min(maxWidth, (rowWidth - gap * 2f) / 3f);
            var buttons = new[] { mergeAllButton, autoButton, traitButton };
            rowLabels = new TMP_Text[buttons.Length];
            for (int i = 0; i < buttons.Length; i++) rowLabels[i] = buttons[i].GetComponentInChildren<TMP_Text>(true);
            traitButtonRef = traitButton;
            rowButtons = new RectTransform[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                var rt = (RectTransform)buttons[i].transform;
                rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
                rt.anchoredPosition = new Vector2((i - 1) * (w + gap), rt.anchoredPosition.y);
                rowButtons[i] = rt;
            }
            rowY = rowButtons[0].anchoredPosition.y;
            rowHeight = rowButtons[0].sizeDelta.y;
            rowLabelRects = new RectTransform[rowLabels.Length];
            rowLabelMinY = new float[rowLabels.Length];
            for (int i = 0; i < rowLabels.Length; i++)
                if (rowLabels[i] != null) { rowLabelRects[i] = rowLabels[i].rectTransform; rowLabelMinY[i] = rowLabelRects[i].anchorMin.y; }

            // 특성 화면에서 초기화·Lv업 버튼 아래에 드는 재화 표시 (그림 + 값)
            pillReset = MakePill(autoButton.transform, 0.08f, 0.92f, "mycelium_stone");
            pillMycelite = MakePill(traitButton.transform, 0.08f, 0.92f, "mycelium_stone");
        }

        // 버튼 안 아래쪽 재화 칸: 밝은 바탕 + 재화 그림 + 값. 모양은 위 줄 재화 표시와 같은 그림을 쓴다
        class CostPill
        {
            public GameObject root;
            public TMP_Text text;
        }

        CostPill pillReset, pillMycelite;
        float rowHeight;                         // 버튼 줄의 원래 높이 (특성 화면에서는 재화 칸이 들어가게 키운다)
        RectTransform[] rowLabelRects;
        float[] rowLabelMinY;
        const float TraitRowHeight = 150f;
        const float PillTop = 0.44f;             // 재화 칸 윗변 (버튼 높이 비율)
        static readonly Color PillText = new Color(0.36f, 0.24f, 0.14f, 1f);
        static readonly Color PillShort = new Color(0.85f, 0.25f, 0.25f, 1f);      // 모자라면 빨간색

        CostPill MakePill(Transform button, float xMin, float xMax, string iconName)
        {
            var font = rowLabels[0] != null ? rowLabels[0].font : null;
            var go = new GameObject("Cost", typeof(RectTransform));
            go.layer = button.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(button, false);
            rt.anchorMin = new Vector2(xMin, 0.08f);
            rt.anchorMax = new Vector2(xMax, PillTop);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var bg = go.AddComponent<Image>();
            bg.sprite = Resources.Load<Sprite>("UI/UI_PP_In_Color_Beige1");
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.layer = go.layer;
            var ir = (RectTransform)iconGo.transform;
            ir.SetParent(rt, false);
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = new Vector2(44f, 44f);
            ir.anchoredPosition = new Vector2(30f, 0f);
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = Resources.Load<Sprite>("UI/Icon/" + iconName);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.layer = go.layer;
            var tr = (RectTransform)textGo.transform;
            tr.SetParent(rt, false);
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(56f, 0f); tr.offsetMax = new Vector2(-8f, 0f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f; text.fontSizeMax = 36f;
            text.enableWordWrapping = false;
            text.color = PillText;
            text.raycastTarget = false;

            go.SetActive(false);
            return new CostPill { root = go, text = text };
        }

        static void SetPill(CostPill pill, bool show, long cost = 0, bool enough = true)
        {
            if (pill == null) return;
            pill.root.SetActive(show);
            if (!show) return;
            pill.text.text = Wallet.Format(cost);
            pill.text.color = enough ? PillText : PillShort;
        }

        // 특성 화면일 때만 버튼을 키우고 글자를 위로 올려 재화 칸 자리를 만든다
        void ApplyTraitRowLayout(bool on)
        {
            if (rowButtons == null) return;
            foreach (var b in rowButtons) b.sizeDelta = new Vector2(b.sizeDelta.x, on ? TraitRowHeight : rowHeight);
            for (int i = 1; i < rowLabelRects.Length; i++)      // 돌아가기(0번)는 재화가 없어 그대로 가운데
            {
                if (rowLabelRects[i] == null) continue;
                var min = rowLabelRects[i].anchorMin;
                rowLabelRects[i].anchorMin = new Vector2(min.x, on ? PillTop : rowLabelMinY[i]);
            }
        }

        // 편성 화면 안쪽을 버섯 특성 화면으로 바꾼다. 팝업이 아니다 —
        // 편성 칸·무기/방어구·꼬마 정보·보유 꼬마 목록은 가려지고, 버튼 줄(일괄 합성·자동 편성·버섯 특성)은 맨 아래(하단바 위)로 내려간다.
        // 버섯 특성 버튼을 한 번 더 누르면 원래대로 돌아온다
        static readonly string[] InnerNames = { "SlotRow", "SideButtons", "Info", "Line", "ListTitle", "Scroll" };
        const float RowBottomGap = 24f;         // 버튼 줄과 안쪽 아래 끝 사이
        const float TraitTopGap = 30f;          // 특성 화면 위 (탭과의 사이)
        const float TraitRowGap = 30f;          // 특성 화면 아래 (버튼 줄과의 사이)
        const float TraitSideGap = 20f;         // 특성 화면 양옆
        bool traitOpen;
        RectTransform[] rowButtons;
        float rowY;                              // 버튼 줄의 원래 높이 (anchoredPosition.y)

        Button traitButtonRef;
        TMP_Text[] rowLabels;                    // 버튼 줄 글자 (일괄 합성 · 자동 편성 · 버섯 특성)
        string[] rowLabelOriginal;

        // 버튼 줄은 특성 화면에서 [돌아가기][초기화][Lv업]이 된다
        void OnMergeAllClicked() { if (traitOpen) ShowTrait(false); else MergeAll(); }
        void OnAutoClicked() { if (traitOpen) traitPanel.ResetAll(); else Formation.AutoArrange(); }
        void OnTraitClicked() { if (!traitOpen) ShowTrait(true); else traitPanel.LevelUp(); }

        // 특성 화면에서 꼬마를 바꿨다: 돌아갔을 때도 같은 꼬마가 골라져 있게 한다
        void OnTraitGgoma(string id)
        {
            selectedId = id;
            Refresh();
        }

        void SetRowLabels(bool trait)
        {
            if (rowLabels == null) return;
            if (trait)
            {
                rowLabelOriginal = new string[rowLabels.Length];
                for (int i = 0; i < rowLabels.Length; i++) rowLabelOriginal[i] = rowLabels[i] != null ? rowLabels[i].text : null;
                string[] texts = { Loc.T("trait.back", "돌아가기"), Loc.T("trait.reset", "초기화"), Loc.T("trait.levelup", "Lv업") };
                for (int i = 0; i < rowLabels.Length; i++) if (rowLabels[i] != null) rowLabels[i].text = texts[i];
            }
            else if (rowLabelOriginal != null)
            {
                for (int i = 0; i < rowLabels.Length; i++) if (rowLabels[i] != null && rowLabelOriginal[i] != null) rowLabels[i].text = rowLabelOriginal[i];
                rowLabelOriginal = null;
            }
        }

        // 특성 화면 버튼: 초기화 = 올린 특성이 있고 균사석이 있을 때, Lv업 = 고른 칸을 올릴 수 있을 때
        void RefreshTraitButtons()
        {
            if (!traitOpen || traitPanel == null) return;
            if (mergeAllButton != null) mergeAllButton.interactable = true;
            if (autoButton != null) autoButton.interactable = traitPanel.CanReset;
            if (traitButtonRef != null) traitButtonRef.interactable = traitPanel.CanLevelUp;

            // 재화 표시: 초기화 · Lv업 모두 균사석 (Lv업은 칸을 안 골랐거나 최대치면 숨김)
            SetPill(pillReset, true, MushroomTraits.ResetMycelite, Wallet.CanAfford(Currency.Mycelite, MushroomTraits.ResetMycelite));
            bool priced = traitPanel.TryGetPickCost(out long myc);
            SetPill(pillMycelite, priced, myc, Wallet.CanAfford(Currency.Mycelite, myc));
        }

        void ShowTrait(bool on)
        {
            if (traitPanel == null || slotRow == null || rowButtons == null) return;
            var page = (RectTransform)slotRow.parent;
            float y = rowY;
            if (on) Canvas.ForceUpdateCanvases();      // 화면이 막 켜진 프레임에도 안쪽 크기가 맞게
            traitPanel.StateChanged -= RefreshTraitButtons;
            traitPanel.GgomaPicked -= OnTraitGgoma;
            ApplyTraitRowLayout(on);
            if (on) y = -(page.rect.height - rowButtons[0].rect.height - RowBottomGap);   // 아래 끝에 붙인다 (위 기준 anchor)
            foreach (var b in rowButtons) b.anchoredPosition = new Vector2(b.anchoredPosition.x, y);

            if (on)
            {
                // 자리 = 안쪽 위 · 양옆 · 버튼 줄 위. 각각 여백을 둔다 (안쪽 좌표 기준)
                var r = page.rect;
                float rowTop = r.yMax + y;                                         // 버튼 줄 윗변 (위 기준 anchor, y는 아래로 음수)
                var min = page.TransformPoint(new Vector3(r.xMin + TraitSideGap, rowTop + TraitRowGap, 0f));
                var max = page.TransformPoint(new Vector3(r.xMax - TraitSideGap, r.yMax - TraitTopGap, 0f));
                traitPanel.Open(selectedId, min, max);
            }
            else traitPanel.Close();
            traitOpen = on;
            SetRowLabels(on);
            if (on) { traitPanel.StateChanged += RefreshTraitButtons; traitPanel.GgomaPicked += OnTraitGgoma; RefreshTraitButtons(); }
            else
            {
                if (mergeAllButton != null) mergeAllButton.interactable = true;
                if (autoButton != null) autoButton.interactable = true;
                if (traitButtonRef != null) traitButtonRef.interactable = true;
                SetPill(pillReset, false); SetPill(pillMycelite, false);
            }
            foreach (var n in InnerNames)
            {
                var inner = page.Find(n);
                if (inner != null) inner.gameObject.SetActive(!on);
            }
        }

        void OnEnable()
        {
            Formation.Changed += Refresh;
            Wallet.Changed += RefreshTraitButtons;
            GuideQuests.Report(GuideKind.OpenFormation);
            if (string.IsNullOrEmpty(selectedId)) selectedId = FirstShown();
            ShowTrait(false);     // 다시 열면 늘 편성 화면부터
            Refresh();
        }

        void OnDisable()
        {
            Formation.Changed -= Refresh;
            Wallet.Changed -= RefreshTraitButtons;
        }

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
            if (to < 0) { if (message != null) message.text = Loc.T("formation.pick_slot"); return; }
            Formation.Place(to, selectedId);
            targetSlot = -1;
        }

        void MergeSelected()
        {
            int up = Formation.Merge(selectedId);
            if (message != null) message.text = up > 0 ? $"{Formation.Name(selectedId)} Lv.{Formation.Level(selectedId)} (+{up})" : "";
            // 화면 갱신은 Formation.Changed가 한다 (바뀐 게 없으면 다시 그릴 것도 없다)
        }

        void GrowSelected()
        {
            if (string.IsNullOrEmpty(selectedId) || !Formation.NeedsGrowth(selectedId)) return;
            string name = Formation.Name(selectedId);
            if (Formation.TryGrow(selectedId))
            {
                if (message != null) message.text = Loc.F("formation.grow_done", name, Formation.EvoStage(selectedId));
            }
            else if (message != null) message.text = Loc.T("gacha.no_funds");
        }

        void MergeAll()
        {
            int up = Formation.MergeAll();
            if (message != null) message.text = up > 0 ? Loc.F("formation.merge_all_done", up) : Loc.T("formation.nothing_to_merge");
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
                if (c.levelText != null) c.levelText.text = have ? "Lv." + lv : Loc.T("formation.not_owned");
                if (c.badge != null) c.badge.SetActive(Formation.SlotOf(c.id) >= 0);
                if (c.selection != null) c.selection.SetActive(c.id == selectedId);
                if (Formation.CanMerge(c.id)) anyMerge = true;
            }
            if (mergeAllButton != null) mergeAllButton.interactable = traitOpen || anyMerge;
            if (traitOpen && traitPanel != null) traitPanel.SetGgoma(selectedId);
            RefreshInfo();
        }

        void RefreshInfo()
        {
            string id = selectedId;
            if (string.IsNullOrEmpty(id)) return;
            int level = Formation.Level(id), copies = Formation.Copies(id), need = Formation.CopiesToNext(level);
            bool have = level > 0, max = level >= Formation.MaxLevel;
            bool grow = Formation.NeedsGrowth(id);
            int stage = Formation.EvoStage(id);
            if (infoIcon != null) infoIcon.sprite = Formation.Icon(id);
            if (infoLevel != null) infoLevel.text = have ? (max ? $"Lv.{level} {Loc.T("upgrade.max")}" : grow ? $"Lv.{level} {Loc.T("formation.grow_tag")}" : $"Lv.{level}") : Loc.T("formation.not_owned");
            if (infoName != null) infoName.text = Loc.F("formation.stage_name", stage, Formation.Name(id));
            int shown = Mathf.Max(1, level);
            if (infoStat != null) infoStat.text = Loc.F("formation.attack_mul", Formation.AttackMultiplierAt(id, shown));
            if (infoNext != null)
            {
                string evo = stage < Formation.EvoStageCount ? Loc.F("formation.evo_at", Formation.EvoLevel(stage + 1), stage + 1) : Loc.T("formation.evo_final");
                infoNext.text = have && !max && !grow ? Loc.F("formation.next_level", Formation.AttackMultiplierAt(id, level + 1), evo) : evo;
            }
            if (infoBarFill != null) infoBarFill.anchorMax = new Vector2(max ? 1f : have ? Mathf.Clamp01(copies / (float)need) : 0f, 1f);
            if (infoBarText != null) infoBarText.text = !have ? Loc.T("formation.get_by_summon") : max ? Loc.T("formation.max_level") : grow ? Loc.F("formation.need_grow", level) : Loc.F("formation.shards", copies, need);
            if (mergeButton != null)
            {
                mergeButton.gameObject.SetActive(!grow);
                mergeButton.interactable = Formation.CanMerge(id);
            }
            if (growButton != null)
            {
                growButton.gameObject.SetActive(grow);
                if (growLabel != null) growLabel.text = grow ? Loc.F("formation.grow", Formation.GrowCost(id)) : "";
            }
            int from = Formation.SlotOf(id);
            if (placeButton != null) placeButton.interactable = have;
            if (placeLabel != null) placeLabel.text = from >= 0 && (targetSlot < 0 || targetSlot == from) ? Loc.T("formation.remove") : Loc.T("formation.place");
        }
    }
}
