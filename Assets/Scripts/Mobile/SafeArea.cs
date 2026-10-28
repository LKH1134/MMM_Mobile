using UnityEngine;

namespace MoreMush.Mobile
{
    // 노치·펀치홀·홈바를 피하는 패널. Canvas 바로 아래, 화면 전체로 늘린 빈 오브젝트에 붙인다.
    // 기기가 알려 주는 안전 영역(Screen.safeArea)에 맞춰 앵커를 바꾼다. 버튼·글자는 이 아래에, 배경은 이 바깥(Canvas 바로 아래)에 둔다.
    // 화면 크기·회전이 바뀌면 다시 맞춘다 (에디터의 Device Simulator에서도 동작)
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        RectTransform rect;
        Rect applied;
        Vector2Int screen;

        void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != applied || Screen.width != screen.x || Screen.height != screen.y) Apply();
        }

        void Apply()
        {
            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0) return;

            rect.anchorMin = new Vector2(applied.xMin / screen.x, applied.yMin / screen.y);
            rect.anchorMax = new Vector2(applied.xMax / screen.x, applied.yMax / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
