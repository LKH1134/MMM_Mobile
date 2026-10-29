using UnityEngine;

namespace MoreMush.Mobile
{
    // 노치·펀치홀·홈 바를 피해 붙은 RectTransform을 기기의 안전 영역(Screen.safeArea)에 맞춘다.
    // 캔버스 바로 밑 패널에 붙이고, 가려지면 안 되는 UI는 전부 이 패널 안에 둔다 — 배경처럼 화면을 꽉 채울 것은 밖에 둔다
    // 회전이나 Device Simulator 기종 변경으로 영역이 바뀌면 다시 맞춘다
    // 플레이 중에만 맞춘다 — 에디터에서는 꽉 찬 패널로 두고 편집한다
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        RectTransform rect;
        Rect applied;
        Vector2Int screen;

        void Awake()
        {
            rect = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != applied || Screen.width != screen.x || Screen.height != screen.y)
                Apply();
        }

        void Apply()
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            if (Screen.width <= 0 || Screen.height <= 0) return;

            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);

            // 안전 영역을 화면 비율(0~1)로 바꿔 앵커에 넣고, 오프셋은 0으로 둬서 앵커 사각형을 그대로 채운다
            rect.anchorMin = new Vector2(applied.xMin / screen.x, applied.yMin / screen.y);
            rect.anchorMax = new Vector2(applied.xMax / screen.x, applied.yMax / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
