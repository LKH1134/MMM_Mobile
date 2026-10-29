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
        }

        [SerializeField] Tab[] tabs = new Tab[0];
        [SerializeField] Button[] closeButtons = new Button[0];
        [SerializeField] Button homeButton;          // 모든 페이지를 닫는 탭 (홈·꼬마). 아무 페이지도 없을 때 선택된 색으로 보인다
        [SerializeField] bool allowNone = true;
        [SerializeField] int defaultIndex = -1;      // 처음 열 탭 (-1 = 없음, allowNone이 아니면 0으로 본다)
        [SerializeField] bool readPendingMainTab;    // Main 하단바: 다른 씬에서 요청한 탭(MainNavigation)으로 시작
        [SerializeField] Color selectedColor = new Color(1f, 0.85f, 0.45f, 1f);

        Color[] normalColors;
        Color homeNormalColor = Color.white;

        public int Current { get; private set; } = -1;
        public event Action<int> Changed;

        void Awake()
        {
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

        void OnTabClicked(int index) => Select(index == Current && allowNone ? -1 : index);

        public void Select(int index)
        {
            if (index < 0 && !allowNone) index = 0;
            Current = index >= 0 && index < tabs.Length ? index : -1;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = i == Current;
                if (tabs[i].page != null && tabs[i].page.activeSelf != on) tabs[i].page.SetActive(on);
                var img = tabs[i].button != null ? tabs[i].button.image : null;
                if (img != null && normalColors != null) img.color = on ? selectedColor : normalColors[i];
            }
            if (homeButton != null && homeButton.image != null) homeButton.image.color = Current < 0 ? selectedColor : homeNormalColor;
            Changed?.Invoke(Current);
        }
    }
}
