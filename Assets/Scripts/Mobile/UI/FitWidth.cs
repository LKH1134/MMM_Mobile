using UnityEngine;

namespace MoreMush.Mobile
{
    // 칸 위치를 고정값으로 배치한 묶음(편성 3×2 칸, 꼬마 고르기 4열, 장비 창 등)을 부모 폭에 맞춰 통째로 줄인다.
    // 비율을 지킨 채 줄이므로 칸이 겹치거나 그림이 찌그러지지 않는다. 부모가 designWidth 이상이면 그대로(1배).
    // 플레이 중에만 고친다 — 편집 중에 바꾸면 프리팹에 축소 값이 저장된다
    [RequireComponent(typeof(RectTransform))]
    public class FitWidth : MonoBehaviour
    {
        [SerializeField] float designWidth = 1316f;   // 이 묶음이 들어가야 하는 폭 (자기 폭 + 양옆 여백)

        RectTransform rect;
        float appliedWidth = -1f;

        void OnEnable()
        {
            rect = (RectTransform)transform;
            appliedWidth = -1f;
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            float w = parent.rect.width;
            if (w <= 0f || Mathf.Approximately(w, appliedWidth)) return;
            appliedWidth = w;
            float s = Mathf.Min(1f, w / designWidth);
            rect.localScale = new Vector3(s, s, 1f);
        }
    }
}
