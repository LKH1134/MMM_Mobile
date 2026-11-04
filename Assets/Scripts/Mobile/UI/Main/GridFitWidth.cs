using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 스크롤 목록(Content)을 부모(Viewport) 폭에 맞춰 통째로 확대·축소한다 — 칸·그림·글자가 같은 비율로 커진다.
    // 프리팹은 designWidth 폭(폰) 기준으로 짜 두면, 넓은 태블릿에서 칸이 좌우를 꽉 채운다. Content에 붙인다 (위 가운데 기준으로 바꾼다)
    public class GridFitWidth : MonoBehaviour
    {
        [SerializeField] float designWidth = 1020f;  // 폰에서의 목록 폭
        [SerializeField] float maxScale = 1.6f;

        float shownWidth = -1f;

        void OnEnable() => shownWidth = -1f;

        void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            float w = parent.rect.width;
            if (w <= 0f || Mathf.Approximately(w, shownWidth)) return;
            shownWidth = w;
            float k = Mathf.Clamp(w / designWidth, 0.6f, maxScale);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.localScale = new Vector3(k, k, 1f);
            rt.sizeDelta = new Vector2(w / k, rt.sizeDelta.y);
            LayoutRebuilder.MarkLayoutForRebuild(rt);
        }
    }
}
