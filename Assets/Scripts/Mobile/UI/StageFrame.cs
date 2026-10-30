using UnityEngine;

namespace MoreMush.Mobile
{
    // 화면 비율이 달라도 UI가 잘리거나 늘어나지 않게 하는 디자인 영역. SafeArea 바로 아래, 모든 UI의 부모로 둔다.
    // 높이는 늘 designHeight(2340), 폭은 화면에 맞춰 minWidth~maxWidth(1080~1316) 사이에서 변한다 → 안의 UI는 이 폭 범위에서 앵커로 맞춘다.
    // 화면이 이보다 길거나 넓으면 남는 쪽은 비워 둔다(카메라 배경 = 검정). 노치·홈바로 공간이 모자라면 통째로 줄인다(scale < 1)
    // 편집 중에도 돌아 게임 뷰 비율대로 미리 볼 수 있다 (씬 오브젝트에만 붙이므로 프리팹에 값이 남지 않는다)
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class StageFrame : MonoBehaviour
    {
        public const float DesignHeight = 2340f;
        public const float MinWidth = 1080f;
        public const float MaxWidth = 1316f;

        RectTransform rect;
        Vector2 appliedParent = new Vector2(-1f, -1f);

        void OnEnable()
        {
            rect = (RectTransform)transform;
            appliedParent = new Vector2(-1f, -1f);
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            var avail = parent.rect.size;
            if (avail.x <= 0f || avail.y <= 0f || avail == appliedParent) return;
            appliedParent = avail;

            float scale = Mathf.Min(1f, avail.x / MinWidth, avail.y / DesignHeight);
            float width = Mathf.Clamp(avail.x / scale, MinWidth, MaxWidth);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, DesignHeight);
            rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
