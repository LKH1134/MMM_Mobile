using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 버섯 도감 화면 (프리팹 DexScreen, 와이어프레임 '10. 도감'):
    //   [버섯 도감] 제목 / 식용 · 약용 · 독성 탭 / 황금 토글 · 정렬 / 버섯 칸 4열(이름 · 그림 · 성급) / 도감 · 세트 보너스 버튼 / 닫기(X)
    // 버섯 목록은 도감 시트(MushroomDex)의 무리(종류 × ★1~5)마다 count종, 그림은 무리 그림 시트의 조각이다.
    // 얻은 버섯은 그림이 보이고, 못 얻은 버섯은 검은 그림자 + "???"이다 (방치 전투 보스를 잡으면 얻는다).
    // 세트 보너스 버튼 = 고른 종류의 ★1~5 세트 진행·효과 목록(setPanel), 도감 버튼 = 다시 버섯 목록.
    // 황금 버섯 그림이 아직 없어서 황금 토글은 켜고 끄는 모양만 있다
    public class DexScreenView : MonoBehaviour
    {

        [Header("탭 (식용 · 약용 · 독성)")]
        [SerializeField] Button[] kindTabs = new Button[3];
        [SerializeField] Sprite tabOn, tabOff;

        [Header("목록")]
        [SerializeField] RectTransform grid;
        [SerializeField] FormationCellView cellTemplate;     // 이름 · 그림 · 성급(levelText) · 황금 표시(badge)

        [Header("위 버튼")]
        [SerializeField] Button sortButton;
        [SerializeField] TMP_Text sortLabel;
        [SerializeField] Toggle goldToggle;
        [SerializeField] RectTransform goldKnob;    // 토글 손잡이 (켜지면 오른쪽 끝으로)

        [Header("아래 버튼")]
        [SerializeField] Button dexButton;
        [SerializeField] Button setBonusButton;
        [SerializeField] Button closeButton;

        [Header("세트 보너스 목록 (세트 보너스 버튼으로 버섯 목록과 바꿔 보인다)")]
        [SerializeField] GameObject gridRoot;       // 버섯 목록 (Scroll)
        [SerializeField] GameObject setPanel;
        [SerializeField] TMP_Text setText;
        [SerializeField] Color lockedTint = new Color(0.08f, 0.08f, 0.1f, 0.85f);

        // 탭 순서 (kindTabs 0·1·2)
        static readonly MushroomKind[] Kinds = { MushroomKind.Edible, MushroomKind.Medicinal, MushroomKind.Toxic };
        static readonly string[] GradeKeys = { "name.8101", "name.8102", "name.8103", "grade.unique", "name.8104" };   // 일반·희귀·영웅·유일·전설 (★1~5)

        struct Entry { public DexRow group; public int no; }

        readonly List<Entry> shown = new List<Entry>();
        readonly List<FormationCellView> cells = new List<FormationCellView>();
        readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int kind;
        bool ascending;
        bool showSets;

        void Awake()
        {
            if (cellTemplate != null) cellTemplate.gameObject.SetActive(false);
            for (int i = 0; i < kindTabs.Length; i++)
            {
                if (kindTabs[i] == null) continue;
                int k = i;
                kindTabs[i].onClick.AddListener(() => { kind = k; Refresh(); });
            }
            if (sortButton != null) sortButton.onClick.AddListener(() => { ascending = !ascending; Refresh(); });
            if (goldToggle != null) goldToggle.onValueChanged.AddListener(_ => Refresh());
            if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            if (setBonusButton != null) setBonusButton.onClick.AddListener(() => { showSets = true; Refresh(); });
            if (dexButton != null) dexButton.onClick.AddListener(() => { showSets = false; Refresh(); });
        }

        void OnEnable()
        {
            PopupLayer.Raise(gameObject, PopupLayer.Popup);   // 팝업 층: 하단 공용 바·광고 상자보다 위
            PopupGate.Opened(this, () => gameObject.SetActive(false));   // 다른 팝업과 같게: 광고 상자 시간 멈춤 · 뒤로가기로 닫힘
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed += CloseOnTab;
            MushroomDex.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            PopupGate.Closed(this);
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed -= CloseOnTab;
            MushroomDex.Changed -= Refresh;
        }

        void CloseOnTab(int tab) => gameObject.SetActive(false);

        void Refresh()
        {
            for (int i = 0; i < kindTabs.Length; i++)
            {
                if (kindTabs[i] == null) continue;
                // 탭 그림은 프리팹의 tabOn/tabOff (11-05: 고른 탭 In_Color_Green · 나머지 In_Color_Grey). 글자 색·외곽선은 프리팹 그대로 둔다
                if (kindTabs[i].image != null && tabOn != null && tabOff != null) kindTabs[i].image.sprite = i == kind ? tabOn : tabOff;
            }
            if (sortLabel != null) sortLabel.text = Loc.T(ascending ? "dex.sort_asc" : "dex.sort_desc");
            if (goldKnob != null && goldToggle != null)
            {
                float x = goldToggle.isOn ? 1f : 0f;
                goldKnob.anchorMin = goldKnob.anchorMax = goldKnob.pivot = new Vector2(x, 0.5f);
                goldKnob.anchoredPosition = new Vector2(goldToggle.isOn ? -6f : 6f, 0f);
            }

            if (gridRoot != null) gridRoot.SetActive(!showSets || setPanel == null);
            if (setPanel != null) setPanel.SetActive(showSets);
            if (sortButton != null) sortButton.gameObject.SetActive(!showSets);
            if (showSets && setPanel != null) { RefreshSets(); return; }

            shown.Clear();
            foreach (var g in MushroomDex.Groups)
                if (g.kind == Kinds[kind])
                    for (int n = 0; n < g.count; n++) shown.Add(new Entry { group = g, no = n });
            shown.Sort((a, b) =>
            {
                int c = ascending ? a.group.star.CompareTo(b.group.star) : b.group.star.CompareTo(a.group.star);
                return c != 0 ? c : a.no.CompareTo(b.no);
            });
            while (cells.Count < shown.Count)
            {
                var c = Instantiate(cellTemplate, grid);
                c.gameObject.SetActive(true);
                cells.Add(c);
            }
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                bool on = i < shown.Count;
                c.gameObject.SetActive(on);
                if (!on) continue;
                var e = shown[i];
                bool have = MushroomDex.Has(e.group, e.no);
                int star = Mathf.Clamp(e.group.star, 1, MushroomDex.MaxStar);
                c.name = "Dex_" + e.group.key + "_" + e.no;
                c.icon.sprite = MushroomDex.Sprite(e.group, e.no);
                c.icon.color = have ? Color.white : lockedTint;   // 못 얻은 버섯 = 그림자
                if (c.nameText != null) c.nameText.text = have ? $"{MushroomDex.KindName(e.group.kind)} {Loc.T(GradeKeys[star - 1])} {e.no + 1}" : "???";   // 이름 표가 생기면 바꾼다
                if (c.levelText != null) c.levelText.text = new string('★', star);
                if (c.badge != null) c.badge.SetActive(false);   // 황금 버섯 획득 표시 — 기록이 생기면 켠다
            }
        }

        // 고른 종류의 ★1~5 세트: 모은 수 / 종 수 · 효과 (완성 = 초록, 미완성 = 회색, 아직 안 나오는 무리는 등장 스테이지) · 이 종류에서 켜진 효과 합
        void RefreshSets()
        {
            if (setText == null) return;
            var k = Kinds[kind];
            int have = 0, total = 0;
            foreach (var g in MushroomDex.Groups) if (g.kind == k) { have += MushroomDex.CollectedCount(g); total += g.count; }
            sb.Clear();
            sb.Append("<b>").Append(Loc.F("dex.set_header", MushroomDex.KindName(k), have, total)).Append("</b>\n\n");
            var sum = new double[System.Enum.GetValues(typeof(DexStat)).Length];
            foreach (var g in MushroomDex.Groups)
            {
                if (g.kind != k) continue;
                bool done = MushroomDex.IsComplete(g);
                sb.Append(done ? "<color=#3E8E2F>" : "<color=#8A8079>");
                sb.Append(new string('★', Mathf.Clamp(g.star, 1, MushroomDex.MaxStar))).Append("  ")
                  .Append(MushroomDex.CollectedCount(g)).Append('/').Append(g.count).Append("  ");
                var effects = MushroomDex.SetEffects(g);
                for (int i = 0; i < effects.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(Effect(effects[i].stat, effects[i].value));
                    if (done) sum[(int)effects[i].stat] += effects[i].value;
                }
                if (!done && IdleProgress.Stage < g.unlockStage)
                    sb.Append("  ").Append(Loc.F("dex.set_from", IdleBattle.HudStageNumber(g.unlockStage)));
                sb.Append("</color>\n");
            }
            sb.Append("\n<b>").Append(Loc.T("dex.set_active")).Append("</b>  ");
            bool any = false;
            for (int i = 0; i < sum.Length; i++)
            {
                if (sum[i] <= 0) continue;
                if (any) sb.Append(", ");
                sb.Append(Effect((DexStat)i, sum[i]));
                any = true;
            }
            if (!any) sb.Append(Loc.T("common.none"));
            setText.text = sb.ToString();
        }

        static string Effect(DexStat stat, double value) => $"{MushroomDex.StatName(stat)} +{value:0.#}%";
    }
}
