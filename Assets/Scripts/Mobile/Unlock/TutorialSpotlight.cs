using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 튜토리얼 스포트라이트: 화면을 어둡게 덮고 가리킬 버튼 자리만 구멍으로 남겨 둔다 (구멍은 그대로 눌린다). 손 그림이 있으면 거기를 가리킨다.
    // 버튼을 누르면 끝난다. 구멍 밖은 눌러도 아무 일도 없다 (그 버튼을 누르게 이끄는 안내라서).
    // UnlockAnnouncer 프리팹 안에 꺼 두고 쓴다. 어두운 판 4장(위·아래·왼·오른쪽)과 손은 프리팹에서 고친다
    public class TutorialSpotlight : MonoBehaviour
    {
        [SerializeField] RectTransform[] dims = new RectTransform[4];      // 위·아래·왼·오른쪽 어두운 판 (앵커 가운데, 피벗 왼쪽 아래)
        [SerializeField] RectTransform hand;                                // 손 그림 (피벗 = 손끝). 그림이 없으면 꺼 둔다
        [SerializeField] float pad = 8f;                                    // 구멍을 버튼보다 넓히는 만큼
        [SerializeField] float bounceHeight = 28f, bounceSpeed = 5f;

        RectTransform canvasRect;
        Canvas canvas;
        Button target;
        bool active;

        public bool IsActive => active;

        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            canvasRect = (RectTransform)transform;      // 이 오브젝트는 캔버스를 꽉 채운다 — 이 좌표계로 구멍을 낸다
            if (!active) gameObject.SetActive(false);
        }

        public void Begin(Button button)
        {
            End();
            if (button == null) return;
            target = button;
            target.onClick.AddListener(End);
            active = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (canvas == null) Awake();
            Place();
        }

        public void End()
        {
            if (target != null) target.onClick.RemoveListener(End);
            target = null;
            active = false;
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!active) return;
            if (target == null || !target.gameObject.activeInHierarchy) { End(); return; }
            Place();
        }

        void Place()
        {
            var hole = HoleRect();
            var cr = canvasRect.rect;
            SetRect(dims[0], Rect.MinMaxRect(cr.xMin, hole.yMax, cr.xMax, cr.yMax));          // 위
            SetRect(dims[1], Rect.MinMaxRect(cr.xMin, cr.yMin, cr.xMax, hole.yMin));          // 아래
            SetRect(dims[2], Rect.MinMaxRect(cr.xMin, hole.yMin, hole.xMin, hole.yMax));      // 왼쪽
            SetRect(dims[3], Rect.MinMaxRect(hole.xMax, hole.yMin, cr.xMax, hole.yMax));      // 오른쪽
            if (hand == null || !hand.gameObject.activeSelf) return;
            // 손끝이 구멍 가운데 살짝 아래를 콕콕 찍는다
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * bounceSpeed)) * bounceHeight;
            hand.anchoredPosition = new Vector2(hole.center.x, hole.center.y - hole.height * 0.12f - bounce);
            hand.SetAsLastSibling();
        }

        static void SetRect(RectTransform rt, Rect r)
        {
            if (rt == null) return;
            bool empty = r.width <= 0f || r.height <= 0f;
            rt.gameObject.SetActive(!empty);
            if (empty) return;
            rt.anchoredPosition = r.position;
            rt.sizeDelta = r.size;
        }

        // 가리킬 버튼의 화면 자리를 이 오브젝트 좌표로
        Rect HoleRect()
        {
            var rt = (RectTransform)target.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var src = target.GetComponentInParent<Canvas>().rootCanvas;
            Camera srcCam = src.renderMode == RenderMode.ScreenSpaceOverlay ? null : src.worldCamera;
            var mine = canvas.rootCanvas;
            Camera myCam = mine.renderMode == RenderMode.ScreenSpaceOverlay ? null : mine.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(srcCam, corners[0]), myCam, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(srcCam, corners[2]), myCam, out var b);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);
        }
    }
}
