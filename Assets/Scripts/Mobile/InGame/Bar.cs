using UnityEngine;

namespace MoreMush.Mobile
{
    // 바(통나무). 필드 아래쪽의 얇은 막대. 플레이어가 좌우로 끌어 움직이고, 위에서 내려온 수확기가 윗면에 닿으면 반사각으로 튕겨 나간다.
    // 옆·아래로 오는 수확기는 뚫고 지나간다 (판정은 HarvestField). 충돌 모양은 자기 RectTransform 사각형의 윗변이다 (크기·처음 위치는 씬에서 맞춘다). 필드의 직계 자식이고 앵커는 가운데여야 한다
    [RequireComponent(typeof(RectTransform))]
    public class Bar : MonoBehaviour
    {
        static readonly Vector3[] corners = new Vector3[4];

        RectTransform self;

        RectTransform Self => self != null ? self : self = (RectTransform)transform;

        // field 로컬 좌표로 본 사각형
        public Rect GetRect(RectTransform field)
        {
            Self.GetWorldCorners(corners);
            Vector2 min = field.InverseTransformPoint(corners[0]);
            Vector2 max = field.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // 가로로 dx만큼 옮기되 필드 밖으로는 나가지 않는다
        public void MoveBy(float dx, RectTransform field)
        {
            Rect f = field.rect;
            float half = Self.rect.width * 0.5f;
            Vector2 p = Self.anchoredPosition;
            p.x = Mathf.Clamp(p.x + dx, f.xMin + half, f.xMax - half);
            Self.anchoredPosition = p;
        }
    }
}
