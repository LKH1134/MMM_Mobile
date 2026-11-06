using UnityEngine;

namespace MoreMush.Mobile
{
    // 부모를 꽉 채우되, 안의 내용을 폰 폭(designWidth) 기준 비율로 통째로 확대·축소한다.
    // 태블릿처럼 폭이 넓으면 칸·버튼·글자가 같은 비율로 커져 좌우를 채우고, 높이는 그만큼 덜 쓴다(부모 높이 / 배율).
    // 판·목록 묶음(Body·Scroll 등)에 붙인다. 플레이 중에만 고친다
    public class ScaleToWidth : MonoBehaviour
    {
        [SerializeField] float designWidth = 1080f;
        [SerializeField] float minScale = 0.7f;
        [SerializeField] float maxScale = 1.3f;   // 태블릿에서 너무 커져 한 화면에 보이는 줄이 줄지 않게

        RectTransform rect;
        Vector2 baseOffsetMin, baseOffsetMax;
        Vector2 shownParent = new Vector2(-1f, -1f);
        bool captured;

        void OnEnable() => shownParent = new Vector2(-1f, -1f);

        void LateUpdate()
        {
            if (rect == null) rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            if (!captured)
            {
                // 프리팹에 적은 여백(부모 기준 offset)을 기억해 두고 그대로 지킨다
                baseOffsetMin = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one ? rect.offsetMin : Vector2.zero;
                baseOffsetMax = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one ? rect.offsetMax : Vector2.zero;
                captured = true;
            }
            var size = parent.rect.size;
            if (size == shownParent || size.x <= 0f) return;
            shownParent = size;
            float k = Mathf.Clamp(size.x / designWidth, minScale, maxScale);
            float w = size.x - baseOffsetMin.x + baseOffsetMax.x;
            float h = size.y - baseOffsetMin.y + baseOffsetMax.y;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = new Vector3(k, k, 1f);
            rect.sizeDelta = new Vector2(w / k, h / k);
            rect.anchoredPosition = (baseOffsetMin + baseOffsetMax) * 0.5f - (parent.rect.center - parent.rect.center);
        }
    }
}
