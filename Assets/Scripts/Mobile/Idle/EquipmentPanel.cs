using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 장비 화면 (Wepon·Amor 프리팹): 위 = 고른 장비 정보·장착·합성(도감 레벨업), 가운데 = 장비 칸(Equipment 순서), 아래 = 보유 효과·소환·일괄 합성.
    // 칸마다 그림·장수·장착 표시, 합성 막대 등 화면 조각은 모두 프리팹에 배치되어 있고 여기서는 값만 채운다.
    // slots는 화면 순서(위→아래, 왼→오른) = 장비 순서. 장비보다 많은 칸은 프리팹에서 꺼 둔다
    public class EquipmentPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Slot { public Button button; public Image bg, icon; public TMP_Text count, badge; [System.NonSerialized] public Sprite normal; }   // normal = 프리팹에 놓은 칸 그림

        [SerializeField] GachaKind kind = GachaKind.Weapon;     // Weapon · Accessory(방어구)
        [SerializeField] Slot[] slots;

        [Header("위: 고른 장비")]
        [SerializeField] Image detailIcon;
        [SerializeField] TMP_Text detailName, detailCount;
        [SerializeField] RectTransform mergeBarFill;
        [SerializeField] TMP_Text mergeBarText, equipStat, ownStat;
        [SerializeField] Button equipButton, mergeButton;
        [SerializeField] TMP_Text equipLabel;

        [Header("아래")]
        [SerializeField] TMP_Text ownTotal;
        [SerializeField] Button summonButton, mergeAllButton;

        int selected = -1;

        Equipment Eq => Equipment.Of(kind) ?? Equipment.Weapon;

        // 칸 바탕: 색은 그림으로 정한다 (물들이지 않는다)

        void Awake()
        {
            UiSkin.GreyWhenDisabled(equipButton);   // 누를 수 없으면 회색 버튼 그림
            UiSkin.GreyWhenDisabled(mergeButton);
            if (equipButton != null) equipButton.onClick.AddListener(EquipSelected);
            if (mergeButton != null) mergeButton.onClick.AddListener(MergeSelected);
            if (summonButton != null) summonButton.onClick.AddListener(OpenSummon);
            if (mergeAllButton != null) mergeAllButton.onClick.AddListener(MergeAll);
            if (slots == null) slots = new Slot[0];
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s.button == null) continue;
                int index = i;
                if (s.icon != null) s.icon.sprite = Eq.Sprite(i);
                s.normal = s.bg != null && s.bg.sprite != null ? s.bg.sprite : Resources.Load<Sprite>(UiSkin.CardBeige);
                s.button.onClick.RemoveAllListeners();
                s.button.onClick.AddListener(() => { selected = index; Refresh(); });
            }
        }

        void OnEnable()
        {
            Eq.Changed += Refresh;
            Loc.Changed += Refresh;
            if (selected < 0) selected = Eq.Equipped >= 0 ? Eq.Equipped : HighestOwned();
            Refresh();
        }

        void OnDisable()
        {
            Eq.Changed -= Refresh;
            Loc.Changed -= Refresh;
        }

        // ── 동작 ──

        void EquipSelected()
        {
            if (!Eq.IsOwned(selected)) { Notify(Loc.F("equip.not_owned", Eq.DisplayTitle)); return; }
            Eq.Equip(selected);
            Notify(Loc.F("equip.equipped_toast", Eq.Names[selected]));
        }

        void MergeSelected()
        {
            if (!Eq.CanMerge(selected))
            {
                int lv = Eq.Level(selected);
                Notify(lv <= 0 ? Loc.F("equip.not_owned", Eq.DisplayTitle)
                    : lv >= Equipment.MaxLevel ? Loc.T("equip.max_level_msg")
                    : Loc.F("equip.need_shards", Eq.DisplayTitle, Equipment.CopiesToNext(lv)));
                return;
            }
            int up = Eq.Merge(selected);
            Notify($"{Eq.Names[selected]} Lv.{Eq.Level(selected)} (+{up})");
        }

        void MergeAll()
        {
            int up = Eq.MergeAll();
            Notify(up > 0 ? Loc.F("equip.merge_all_result", Eq.DisplayTitle, up) : Loc.F("equip.nothing_to_merge", Eq.DisplayTitle));
        }

        // 소환 = 하단바 뽑기 탭
        void OpenSummon()
        {
            if (TabGroup.MainBar != null) TabGroup.MainBar.Select(MainNavigation.GachaTab);
            else MainNavigation.Open(MainNavigation.GachaTab);
        }

        void Notify(string message)
        {
            Toast.Post(message);
        }

        int HighestOwned()
        {
            for (int i = Eq.Count - 1; i >= 0; i--) if (Eq.IsOwned(i)) return i;
            return 0;
        }

        // ── 그리기 ──

        void Refresh()
        {
            int equipped = Eq.Equipped;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s.button == null) continue;
                int lv = Eq.Level(i);
                s.icon.color = Color.white;     // 못 얻은 것은 "미보유" 글자로 알린다
                s.count.text = lv <= 0 ? Loc.T("equip.not_owned_short")
                    : lv >= Equipment.MaxLevel ? $"Lv.{lv} MAX"
                    : $"Lv.{lv} <size=80%>({Eq.Copies(i)}/{Equipment.CopiesToNext(lv)})</size>";
                s.badge.text = i == equipped ? "<color=#3A8F2E>" + Loc.T("common.equip") + "</color>" : "";
                // [UI 개편 11-05] 보통 칸은 프리팹에 놓은 그림 그대로 (없을 때만 UiSkin.CardBeige). 고른 칸·장착 칸만 바꿔 낀다
                s.bg.sprite = i == selected ? Resources.Load<Sprite>(UiSkin.CardSelected) : i == equipped ? Resources.Load<Sprite>(UiSkin.CardGreen) : s.normal;
                s.bg.type = Image.Type.Sliced;
                s.bg.color = Color.white;
            }

            if (ownTotal != null)
                ownTotal.text = Loc.F("equip.total_effect", Eq.DisplayStatName, Eq.TotalOwnPercent, Eq.EquipPercent(equipped));

            if (selected < 0 || selected >= Eq.Count) return;
            int level = Eq.Level(selected), copies = Eq.Copies(selected), need = Equipment.CopiesToNext(level);
            bool have = level > 0, max = level >= Equipment.MaxLevel;
            if (detailIcon != null) { detailIcon.sprite = Eq.Sprite(selected); detailIcon.color = Color.white; }
            if (detailName != null) detailName.text = $"[{GachaProgress.GradeNames[Eq.Grade(selected)]}] {Eq.Names[selected]}";
            if (detailCount != null) detailCount.text = have ? $"Lv.{level}" : Loc.T("equip.not_owned_short");
            if (mergeBarFill != null) mergeBarFill.anchorMax = new Vector2(max ? 1f : have ? Mathf.Clamp01(copies / (float)need) : 0f, 1f);
            if (mergeBarText != null) mergeBarText.text = !have ? Loc.T("equip.obtain_summon") : max ? Loc.T("equip.max_level") : Loc.F("equip.next_shards", copies, need);
            // 다음 레벨 값도 같이 보여 준다
            string Next(double next) => have && !max ? $"  <size=80%><color=#3A8F2E>→ +{next:0.#}%</color></size>" : "";
            int shown = Mathf.Max(1, level);
            if (equipStat != null) equipStat.text = Loc.F("equip.effect_equip", Eq.DisplayStatName, Eq.EquipPercentAt(selected, shown)) + Next(Eq.EquipPercentAt(selected, level + 1));
            if (ownStat != null) ownStat.text = Loc.F("equip.effect_own", Eq.DisplayStatName, Eq.OwnPercentAt(selected, shown)) + Next(Eq.OwnPercentAt(selected, level + 1));

            bool isEquipped = selected == equipped;
            if (equipButton != null) equipButton.interactable = have && !isEquipped;
            if (equipLabel != null) equipLabel.text = Loc.T(isEquipped ? "equip.equipped_now" : "common.equip");
            if (mergeButton != null) mergeButton.interactable = Eq.CanMerge(selected);
        }
    }
}
