using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 팝업형 화면의 하단 띠 (와이어프레임: 화면 아래 어두운 바 가운데 X). 프리팹 Prefabs/Mobile/UI/Common/PopupBottomBand (팝업 안에 배치해 쓴다).
    // 하단 공용 바 자리를 UI_PP_Under_Side03으로 덮고, 가운데 X를 누르면 팝업을 닫는다:
    //   Closing을 건 코드 팝업이면 그것, forwardTo가 있으면 그 버튼(기존 닫기), 부모에 UiPopup이 있으면 Close(), 아니면 closeTarget(비우면 부모)을 끈다.
    // 자리: 부모가 어디에 있든(가운데 창 안이어도) 하단 공용 바와 똑같이 —
    //   좌우 = 화면 끝, 아래 = 화면 맨 아래(홈바 자리까지), 위 = 안전 영역 아래 끝 + barHeight(Stage 단위). X는 안전 영역 쪽 가운데
    public class PopupBottomBand : MonoBehaviour
    {
        [SerializeField] Button closeButton;
        [SerializeField] GameObject closeTarget;
        [SerializeField] Button forwardTo;      // 기존 닫기 버튼이 있으면 그 버튼을 누른 것으로 친다 (기존 닫기 동작을 그대로 쓴다)
        [SerializeField] float barHeight = 200f;    // 하단 공용 바 높이 (Stage 단위)

        public Button CloseButton => closeButton;
        public event System.Action Closing;     // 코드로 만든 팝업이 자기 닫기 함수를 걸 때 쓴다

        RectTransform rect, stage, safe, screen;
        Vector3 lastSafe, lastScreen, lastSelf;
        readonly Vector3[] corners = new Vector3[4];

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        void OnEnable()
        {
            rect = (RectTransform)transform;
            var frame = GetComponentInParent<StageFrame>(true);
            var area = GetComponentInParent<SafeArea>(true);
            var canvas = GetComponentInParent<Canvas>();
            stage = frame != null ? (RectTransform)frame.transform : null;
            safe = area != null ? (RectTransform)area.transform : null;
            screen = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            lastSafe = lastScreen = lastSelf = new Vector3(float.NaN, 0f, 0f);
        }

        void LateUpdate()
        {
            if (stage == null || safe == null || screen == null) return;   // 화면 전체 캔버스에 만든 코드 팝업: 앵커(아래 끝, 높이 barHeight) 그대로
            safe.GetWorldCorners(corners);
            var s = corners[0];
            screen.GetWorldCorners(corners);
            var f = corners[0];
            var self = rect.parent != null ? rect.parent.position + rect.parent.lossyScale * 1000f : Vector3.zero;   // 부모가 움직이거나 커져도(FitInRegion) 다시 잡는다
            if (s == lastSafe && f == lastScreen && self == lastSelf) return;
            lastSafe = s; lastScreen = f; lastSelf = self;
            Place();
        }

        void Place()
        {
            var parent = (RectTransform)rect.parent;
            safe.GetWorldCorners(corners);
            float safeBottomStage = stage.InverseTransformPoint(corners[0]).y;
            var topWorld = stage.TransformPoint(new Vector3(0f, safeBottomStage + barHeight, 0f));
            screen.GetWorldCorners(corners);
            var lb = parent.InverseTransformPoint(corners[0]);
            var rt = parent.InverseTransformPoint(corners[2]);
            float top = parent.InverseTransformPoint(topWorld).y;
            var pc = parent.rect.center;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(lb.x - pc.x, lb.y - pc.y);
            rect.offsetMax = new Vector2(rt.x - pc.x, top - pc.y);
            if (closeButton != null)
            {
                // X: 안전 영역 쪽 띠의 가운데 (홈바 자리로 내려가지 않게)
                var x = (RectTransform)closeButton.transform;
                var mid = stage.TransformPoint(new Vector3(0f, safeBottomStage + barHeight * 0.5f, 0f));
                x.anchorMin = x.anchorMax = new Vector2(0.5f, 1f);
                x.pivot = new Vector2(0.5f, 0.5f);
                float fromTop = rect.InverseTransformPoint(mid).y - rect.rect.yMax;
                x.anchoredPosition = new Vector2(0f, fromTop);
            }
        }

        public void Close()
        {
            if (Closing != null) { Closing(); return; }
            if (forwardTo != null) { forwardTo.onClick.Invoke(); return; }
            var popup = GetComponentInParent<UiPopup>(true);
            if (popup != null) { popup.Close(); return; }
            var target = closeTarget != null ? closeTarget : transform.parent != null ? transform.parent.gameObject : gameObject;
            target.SetActive(false);
        }
    }
}
