using System;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 탭 버튼 = 페이지 하나. 버튼을 누르면 그 페이지만 켜고 나머지는 끈다.
    // allowNone이면 열린 탭을 다시 누를 때 닫힌다 (방치형 하단바처럼 아무 패널도 없는 상태가 기본).
    // closeButtons(닫기 X 등)는 모든 페이지를 닫는다. 이 오브젝트가 다시 켜지면 처음 상태로 돌아간다
    public class TabGroup : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public Button button;
            public GameObject page;
            public GameObject[] hide = new GameObject[0];   // 이 탭이 열린 동안 끄는 것 (예: 던전·뽑기 머리줄과 겹치는 재화 줄)
        }

        [SerializeField] Tab[] tabs = new Tab[0];
        [SerializeField] Button[] closeButtons = new Button[0];
        [SerializeField] Button homeButton;          // 모든 페이지를 닫는 탭 (홈·꼬마). 아무 페이지도 없을 때 선택된 색으로 보인다
        [SerializeField] GameObject[] homeOnly = new GameObject[0];   // 아무 페이지도 없을 때(홈)만 켜는 것 (예: 방치형 강화 목록 — 페이지 위를 덮지 않게)
        [SerializeField] bool allowNone = true;
        [SerializeField] int defaultIndex = -1;      // 처음 열 탭 (-1 = 없음, allowNone이 아니면 0으로 본다)
        [SerializeField] bool readPendingMainTab;    // Main 하단바: 다른 씬에서 요청한 탭(MainNavigation)으로 시작
        [SerializeField] bool pagesArePopups;        // 페이지가 팝업이다 (편성 안 무기·방어구·펫): 다른 팝업과 한 번에 하나만. 아니면 탭을 바꿀 때 열린 팝업을 닫는다
        [SerializeField] Color selectedColor = new Color(1f, 0.85f, 0.45f, 1f);

        Color[] normalColors;
        Color homeNormalColor = Color.white;

        public int Current { get; private set; } = -1;
        public static TabGroup MainBar { get; private set; }     // Main 하단바 (readPendingMainTab) — 다른 화면에서 탭을 열 때 쓴다
        public event Action<int> Changed;

        void Awake()
        {
            if (readPendingMainTab) MainBar = this;
            normalColors = new Color[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var b = tabs[i].button;
                if (b == null) continue;
                normalColors[i] = b.image != null ? b.image.color : Color.white;
                b.onClick.AddListener(() => OnTabClicked(index));
            }
            foreach (var b in closeButtons)
                if (b != null) b.onClick.AddListener(() => Select(-1));
            if (homeButton != null)
            {
                if (homeButton.image != null) homeNormalColor = homeButton.image.color;
                homeButton.onClick.AddListener(() => Select(-1));
            }
        }

        void OnDestroy()
        {
            if (MainBar == this) MainBar = null;
        }

        // 꺼질 때(OnDisable) 자식을 끄면 Unity가 막으므로, 다시 켜질 때 처음 상태로 돌린다
        void OnEnable()
        {
            int start = StartIndex;
            if (readPendingMainTab)
            {
                int pending = MainNavigation.TakePendingTab();
                if (pending != MainNavigation.Home) start = pending;
            }
            Select(start);
        }

        int StartIndex => allowNone ? defaultIndex : Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, tabs.Length - 1));

        // 어느 탭의 hide에든 든 것은 그 탭이 열렸을 때만 끄고, 나머지 때는 켠다
        void ApplyHide()
        {
            var hidden = Current >= 0 ? tabs[Current].hide : null;
            foreach (var tab in tabs)
            {
                if (tab.hide == null) continue;
                foreach (var go in tab.hide)
                {
                    if (go == null) continue;
                    bool on = hidden == null || Array.IndexOf(hidden, go) < 0;
                    if (go.activeSelf != on) go.SetActive(on);
                }
            }
        }

        void OnTabClicked(int index) => Select(index == Current && allowNone ? -1 : index);

        public void Select(int index)
        {
            if (index < 0 && !allowNone) index = 0;
            int next = index >= 0 && index < tabs.Length ? index : -1;
            if (pagesArePopups)
            {
                if (next >= 0) PopupGate.Opened(this, () => Select(-1));
                else PopupGate.Closed(this);
            }
            else if (next != Current) PopupGate.CloseAll();
            Current = next;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = i == Current;
                if (tabs[i].page != null && tabs[i].page.activeSelf != on) tabs[i].page.SetActive(on);
                // 팝업 페이지는 자기 캔버스로 위에 올린다 — 형제 순서상 뒤에 오는 재화 줄 등에 가려지거나 뒤 버튼이 눌리지 않게
                if (on && pagesArePopups && tabs[i].page != null) PopupLayer.Raise(tabs[i].page, PopupLayer.Panel);
                var img = tabs[i].button != null ? tabs[i].button.image : null;
                if (img != null && normalColors != null) img.color = on ? selectedColor : normalColors[i];
            }
            foreach (var go in homeOnly)
                if (go != null && go.activeSelf != (Current < 0)) go.SetActive(Current < 0);
            ApplyHide();
            if (homeButton != null && homeButton.image != null) homeButton.image.color = Current < 0 ? selectedColor : homeNormalColor;
            Changed?.Invoke(Current);
        }
    }
}
