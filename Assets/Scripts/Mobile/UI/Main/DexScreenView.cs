using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 버섯 도감 화면 (프리팹 DexScreen, 와이어프레임 '10. 도감'):
    //   [버섯 도감] 제목 / 식용 · 약용 · 독성 탭 / 황금 토글 · 정렬 / 버섯 칸 4열(이름 · 그림 · 성급) / 도감 · 세트 보너스 버튼 / 닫기(X)
    // 버섯 목록은 Resources/<sheetFolder>의 "Mush_<종류>_<등급>_Unit" 그림으로 만든다 (종류: Edible·Medicinal·Toxic, 등급: Common~Legend = ★1~5).
    // 아직 게임에 '얻은 버섯' 기록과 황금 버섯 그림이 없어서 모두 보이는 상태로 두고, 황금 토글은 켜고 끄는 모양만 있다
    public class DexScreenView : MonoBehaviour
    {
        [SerializeField] string sheetFolder = "Art/Dream/Index";

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

        static readonly string[] Kinds = { "Edible", "Medicinal", "Toxic" };
        static readonly string[] KindNames = { "식용", "약용", "독성" };
        static readonly string[] Grades = { "Common", "Rare", "Epic", "Unique", "Legend" };
        static readonly string[] GradeNames = { "일반", "희귀", "영웅", "유일", "전설" };

        class Entry { public Sprite sprite; public int kind, grade, no; }

        readonly List<Entry> entries = new List<Entry>();
        readonly List<FormationCellView> cells = new List<FormationCellView>();
        int kind;
        bool ascending;

        void Awake()
        {
            var sprites = Resources.LoadAll<Sprite>(sheetFolder);
            foreach (var sp in sprites)
            {
                var parts = sp.name.Split('_');   // Mush_Edible_Common_Unit_3 (시트 한 칸 = 버섯 한 종)
                if (parts.Length < 3) continue;
                int k = System.Array.IndexOf(Kinds, parts[1]), g = System.Array.IndexOf(Grades, parts[2]);
                if (k < 0 || g < 0) continue;
                int no = 0;
                if (parts.Length >= 5) int.TryParse(parts[4], out no);
                entries.Add(new Entry { sprite = sp, kind = k, grade = g, no = no });
            }
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
            if (setBonusButton != null) setBonusButton.interactable = false;   // 세트 보너스 화면은 아직 없다
        }

        void OnEnable()
        {
            PopupLayer.Raise(gameObject, PopupLayer.Popup);   // 팝업 층: 하단 공용 바·광고 상자보다 위
            PopupGate.Opened(this, () => gameObject.SetActive(false));   // 다른 팝업과 같게: 광고 상자 시간 멈춤 · 뒤로가기로 닫힘
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed += CloseOnTab;
            Refresh();
        }

        void OnDisable()
        {
            PopupGate.Closed(this);
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed -= CloseOnTab;
        }

        void CloseOnTab(int tab) => gameObject.SetActive(false);

        void Refresh()
        {
            for (int i = 0; i < kindTabs.Length; i++)
            {
                if (kindTabs[i] == null) continue;
                if (kindTabs[i].image != null) kindTabs[i].image.sprite = i == kind ? tabOn : tabOff;
                var label = kindTabs[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = i == kind ? Color.white : UiSkin.Ink;   // 초록 탭 = 흰 글자, 베이지 탭 = 진한 글자
            }
            if (sortLabel != null) sortLabel.text = ascending ? "레어도 ↑" : "레어도 ↓";
            if (goldKnob != null && goldToggle != null)
            {
                float x = goldToggle.isOn ? 1f : 0f;
                goldKnob.anchorMin = goldKnob.anchorMax = goldKnob.pivot = new Vector2(x, 0.5f);
                goldKnob.anchoredPosition = new Vector2(goldToggle.isOn ? -6f : 6f, 0f);
            }

            var shown = entries.FindAll(e => e.kind == kind);
            shown.Sort((a, b) =>
            {
                int c = ascending ? a.grade.CompareTo(b.grade) : b.grade.CompareTo(a.grade);
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
                c.name = "Dex_" + e.sprite.name;
                c.icon.sprite = e.sprite;
                if (c.nameText != null) c.nameText.text = $"{KindNames[e.kind]} {GradeNames[e.grade]} {e.no + 1}";   // 이름 표가 생기면 바꾼다
                if (c.levelText != null) c.levelText.text = new string('★', e.grade + 1);
                if (c.badge != null) c.badge.SetActive(false);   // 황금 버섯 획득 표시 — 기록이 생기면 켠다
            }
        }
    }
}
