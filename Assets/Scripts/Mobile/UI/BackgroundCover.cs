using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-06] 홈 전투 배경: 그림 비율을 지킨 채(늘이지 않고) 화면 위 ~ 아래 기준(업그레이드 노트 위 끝) 영역을 꽉 덮는다.
    // 영역보다 그림이 남는 쪽은 넘쳐서 잘린다 — 폰처럼 세로가 길면 좌우가, iPad처럼 가로가 넓으면 위아래가 잘린다.
    // 이 오브젝트(Image)에 붙인다. 부모(캔버스)를 기준으로 크기·위치를 매 프레임 맞춘다 (화면 크기·노트 위치가 바뀌어도 따라감)
    [ExecuteAlways, RequireComponent(typeof(Image))]
    public class BackgroundCover : MonoBehaviour
    {
        [SerializeField] RectTransform bottomEdge;      // 이 오브젝트의 위 끝까지 덮는다 (비우면 부모 아래 끝까지)
        [SerializeField] float overlap = 60f;           // 아래 기준 밑으로 더 내려 덮는 길이 (노트 탭 둥근 곳에 틈이 안 보이게)

        RectTransform rt;
        Image image;
        readonly Vector3[] corners = new Vector3[4];
        float lastEdgeFromTop;
        bool hasEdge;

        void LateUpdate()
        {
            if (rt == null) rt = (RectTransform)transform;
            if (image == null) image = GetComponent<Image>();
            var parent = rt.parent as RectTransform;
            if (parent == null || image.sprite == null) return;

            var area = parent.rect;
            float top = area.yMax, bottom = area.yMin;
            if (bottomEdge != null && bottomEdge.gameObject.activeInHierarchy)
            {
                bottomEdge.GetWorldCorners(corners);
                float edge = parent.InverseTransformPoint(corners[1]).y;     // 노트 위 끝
                if (edge > bottom && edge < top) { lastEdgeFromTop = top - edge; hasEdge = true; }
            }
            // 다른 탭에서 노트가 꺼져 있어도 마지막 위치를 써서 배경 크기가 탭마다 바뀌지 않게
            if (hasEdge) bottom = Mathf.Max(area.yMin, top - lastEdgeFromTop - overlap);
            float regionW = area.width, regionH = top - bottom;
            if (regionW <= 0f || regionH <= 0f) return;

            var size = image.sprite.rect.size;
            float aspect = size.x / size.y;
            float w = Mathf.Max(regionW, regionH * aspect);     // 넓이·높이 둘 다 덮는 쪽으로 (비율 유지)
            float h = w / aspect;

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var center = new Vector2(area.center.x, (top + bottom) * 0.5f);
            var pos = center - area.center;     // 앵커가 부모 가운데라 부모 가운데 기준 위치
            if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
            var sd = new Vector2(w, h);
            if (rt.sizeDelta != sd) rt.sizeDelta = sd;
        }
    }
}
