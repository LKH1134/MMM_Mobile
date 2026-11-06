using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 팝업을 자기 캔버스로 Main 캔버스 위에 올린다. 형제 순서와 상관없이 위에 그려지고 입력도 먼저 받으므로
    // 팝업 배경(Dim·Panel)이 뒤의 버튼(하단바·재화·편성 칸)을 막는다.
    // 순서는 부모 캔버스 순서 + offset (Main 캔버스 순서가 10000이라 고정값이면 뒤로 숨는다)
    public static class PopupLayer
    {
        // [UI 개편 2026-11-04] 층 순서: 화면 내용(0) < 광고 상자(AdBox 5) < 하단 공용 바(BottomBar 20) < 편성 안 팝업(30) < 팝업(100) < 안내(200)
        //   하단 공용 바는 화면 내용보다 늘 앞. 아래 X가 있는 팝업형 화면은 하단 바를 덮는다 (UI_REWORK_LOG.md)
        public const int AdBox = 5;       // 홈 광고 상자 버튼
        public const int BottomBar = 20;  // 하단 공용 바 (BottomBarLayer가 올린다)
        public const int Panel = 30;      // 편성 안 팝업 (꼬마 고르기·무기·방어구) — 하단 바 위
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
