using UnityEngine;

namespace MoreMush.Mobile
{
    // 고정 크기로 짠 팝업 창을 "위 재화 줄 아래 ~ 하단 띠 위" 영역에 비율 그대로 맞춘다 (가운데, 균일 배율).
    // 폰(1080×2340)에서는 거의 그대로, 태블릿에서는 커지고, 짧은 화면에서는 줄어든다. 하단 띠(PopupBottomBand)에 가리지 않는다.
    // 위·아래 여백은 Stage 단위. 플레이 중에만 고친다
    public class FitInRegion : MonoBehaviour
    {
        [SerializeField] float top = 260f;          // 화면(안전 영역) 위에서 비울 높이 — 재화 줄·제목 메모 자리
        [SerializeField] float bottom = 230f;       // 화면(안전 영역) 아래에서 비울 높이 — 하단 띠(200) + 여백
        [SerializeField] float side = 24f;          // 좌우 여백
        [SerializeField] float maxScale = 1.5f;

        RectTransform rect, stage;
        Vector2 baseSize;
        Vector2 shownStage = new Vector2(-1f, -1f);

        void Awake()
        {
            rect = (RectTransform)transform;
            baseSize = rect.rect.size;
            var frame = GetComponentInParent<StageFrame>(true);
            stage = frame != null ? (RectTransform)frame.transform : null;
        }

        void OnEnable() => shownStage = new Vector2(-1f, -1f);

        void LateUpdate()
        {
            if (stage == null || baseSize.x <= 0f || baseSize.y <= 0f) return;
            var size = stage.rect.size;
            if (size == shownStage) return;
            shownStage = size;
            float availW = size.x - side * 2f, availH = size.y - top - bottom;
            float k = Mathf.Min(maxScale, availW / baseSize.x, availH / baseSize.y);
            rect.localScale = new Vector3(k, k, 1f);
            // 영역 가운데 (Stage 좌표) → 부모 좌표
            var centerWorld = stage.TransformPoint(new Vector3(stage.rect.center.x, stage.rect.yMin + bottom + availH * 0.5f, 0f));
            var parent = (RectTransform)rect.parent;
            var local = (Vector2)parent.InverseTransformPoint(centerWorld);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            var p = rect.pivot;
            // 피벗이 가운데가 아니어도 창 가운데가 영역 가운데에 오게
            rect.anchoredPosition = local - parent.rect.center + new Vector2((p.x - 0.5f) * baseSize.x * k, (p.y - 0.5f) * baseSize.y * k);
        }
    }
}
