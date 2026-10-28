using UnityEngine;

namespace MoreMush.Mobile
{
    // Canvas 바로 아래에 두는 패널. Screen.safeArea(노치·상태바·하단바 제외 영역)에 맞춰 앵커를 잡는다.
    // 터치할 UI는 이 패널의 자식으로, 화면 끝까지 채울 배경은 Canvas 바로 아래(패널 밖)에 둔다.
    // 회전·폴더블 전환 등으로 값이 바뀌면 다시 맞춘다
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPanel : MonoBehaviour
    {
        RectTransform rect;
        Rect lastSafeArea;
        Vector2Int lastScreenSize;

        void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (lastSafeArea != Screen.safeArea
                || lastScreenSize.x != Screen.width
                || lastScreenSize.y != Screen.height)
                Apply();
        }

        void Apply()
        {
            Rect safe = Screen.safeArea;
            lastSafeArea = safe;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (lastScreenSize.x <= 0 || lastScreenSize.y <= 0) return;

            // 픽셀 좌표를 0~1 앵커로 바꾼다
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
