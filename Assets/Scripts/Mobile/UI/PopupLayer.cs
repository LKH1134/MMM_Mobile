using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 팝업을 자기 캔버스로 Main 캔버스 위에 올린다. 형제 순서와 상관없이 위에 그려지고 입력도 먼저 받으므로
    // 팝업 배경(Dim·Panel)이 뒤의 버튼(하단바·재화·편성 칸)을 막는다.
    // 순서는 부모 캔버스 순서 + offset (Main 캔버스 순서가 10000이라 고정값이면 뒤로 숨는다)
    public static class PopupLayer
    {
        public const int Panel = 10;      // 편성 안 팝업 (꼬마 고르기·무기·방어구·펫)
        public const int Popup = 100;     // UiPopup·뽑기 결과
        public const int Toast = 200;     // 안내 문구: 어떤 팝업 위에도 보인다

        // raycast = false면 그리기 순서만 올리고 입력은 통과시킨다 (안내 문구)
        public static void Raise(GameObject go, int offset, bool raycast = true)
        {
            var parent = go.transform.parent != null ? go.transform.parent.GetComponentInParent<Canvas>() : null;
            if (!go.TryGetComponent<Canvas>(out var canvas)) canvas = go.AddComponent<Canvas>();
            if (raycast && !go.TryGetComponent<GraphicRaycaster>(out _)) go.AddComponent<GraphicRaycaster>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = (parent != null ? parent.rootCanvas.sortingOrder : 0) + offset;
        }
    }
}
