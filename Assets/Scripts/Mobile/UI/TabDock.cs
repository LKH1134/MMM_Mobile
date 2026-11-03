using UnityEngine;

namespace MoreMush.Mobile
{
    // 탭에 따라 이 UI의 자리를 바꾼다 (예: 재화 줄 — 홈에선 옵션 버튼 옆, 던전·뽑기에선 머리줄 설명과 겹치지 않게 제목 줄 오른쪽).
    // docked에 든 탭이 열리면 dockedPosition으로, 나머지 때는 처음 자리로 돌아간다
    [RequireComponent(typeof(RectTransform))]
    public class TabDock : MonoBehaviour
    {
        [SerializeField] TabGroup tabs;
        [SerializeField] int[] docked = new int[0];      // TabGroup의 탭 번호
        [SerializeField] Vector2 dockedPosition;          // 그 탭들이 열린 동안의 anchoredPosition

        RectTransform rt;
        Vector2 home;

        void Awake()
        {
            rt = (RectTransform)transform;
            home = rt.anchoredPosition;
            if (tabs != null) tabs.Changed += Apply;
        }

        void Start()
        {
            if (tabs != null) Apply(tabs.Current);
        }

        void OnDestroy()
        {
            if (tabs != null) tabs.Changed -= Apply;
        }

        void Apply(int tab) => rt.anchoredPosition = System.Array.IndexOf(docked, tab) >= 0 ? dockedPosition : home;
    }
}
