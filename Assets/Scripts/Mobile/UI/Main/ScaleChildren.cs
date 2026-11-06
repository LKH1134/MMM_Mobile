using UnityEngine;

namespace MoreMush.Mobile
{
    // 부모 폭이 폰(designWidth)보다 넓을 때, 지정한 자식들(배너 안 제목·설명 등)만 비율대로 키운다.
    // 배너처럼 위쪽에 붙은 고정 높이 판에서, 판 크기는 두고 안의 글자·그림만 키울 때 쓴다. 각 자식의 피벗 기준으로 커진다
    public class ScaleChildren : MonoBehaviour
    {
        [SerializeField] RectTransform[] targets;
        [SerializeField] float designWidth = 1080f;
        [SerializeField] float maxScale = 1.3f;

        float shownWidth = -1f;

        void OnEnable() => shownWidth = -1f;

        void LateUpdate()
        {
            float w = ((RectTransform)transform).rect.width;
            if (w <= 0f || Mathf.Approximately(w, shownWidth)) return;
            shownWidth = w;
            float k = Mathf.Clamp(w / designWidth, 1f, maxScale);
            foreach (var t in targets) if (t != null) t.localScale = new Vector3(k, k, 1f);
        }
    }
}
