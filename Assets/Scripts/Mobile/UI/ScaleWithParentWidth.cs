using UnityEngine;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-06] 부모 폭이 폰(designWidth)보다 넓으면 이 오브젝트를 통째로 키운다 (위치·앵커는 그대로, 피벗 기준으로 커짐).
    // 홈 전투의 꼬마 칸 묶음(GgomaArea)·적 영역(EnemyArea)에 붙여, 가로가 넓은 화면(폴드·태블릿)에서 전투가 작아 보이지 않게 한다.
    // 폰보다 좁으면 줄이지 않는다(minScale 1). 플레이 중에만 고친다
    public class ScaleWithParentWidth : MonoBehaviour
    {
        [SerializeField] float designWidth = 1080f;
        [SerializeField] float minScale = 1f;
        [SerializeField] float maxScale = 1.35f;

        float shownWidth = -1f;

        void OnEnable() => shownWidth = -1f;

        void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            float w = parent.rect.width;
            if (w <= 0f || Mathf.Approximately(w, shownWidth)) return;
            shownWidth = w;
            float k = Mathf.Clamp(w / designWidth, minScale, maxScale);
            transform.localScale = new Vector3(k, k, 1f);
        }
    }
}
