using UnityEngine;

namespace MoreMush.Mobile
{
    // 하단 탭바 버튼: 지금 보고 있는 화면의 탭이면 아이콘을 살짝 들어 올린다 (선반 위 아이콘 중 고른 것만 들린 모양).
    // Main에서는 하단바 TabGroup(MainBar)의 현재 탭으로 정하고, Main 밖 씬(세계수 등)에서는 currentScene으로 정한다
    public class TabIconLift : MonoBehaviour
    {
        [SerializeField] RectTransform icon;
        [SerializeField] int tab = -1;              // Main 하단바 탭 번호 (-1 = 홈). 어느 탭도 아닌 버튼(세계수 이동)은 -99
        [SerializeField] bool currentScene;         // Main 밖 씬에서 이 버튼이 지금 화면이면 켠다
        [SerializeField] float lift = 22f;
        [SerializeField] float liftScale = 1.08f;

        Vector2 basePos;
        TabGroup bar;

        void Start()
        {
            if (icon == null) { enabled = false; return; }
            basePos = icon.anchoredPosition;
            bar = TabGroup.MainBar;
            if (bar != null) bar.Changed += Apply;
            Apply(bar != null ? bar.Current : int.MinValue);
        }

        void OnDestroy()
        {
            if (bar != null) bar.Changed -= Apply;
        }

        void Apply(int current)
        {
            bool on = bar != null ? current == tab : currentScene;
            icon.anchoredPosition = basePos + (on ? Vector2.up * lift : Vector2.zero);
            icon.localScale = Vector3.one * (on ? liftScale : 1f);
        }
    }
}
