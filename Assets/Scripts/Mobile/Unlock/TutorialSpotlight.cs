using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 튜토리얼 스포트라이트: 화면을 어둡게 덮고 가리킬 버튼 자리만 구멍으로 남겨 두어(구멍은 그대로 눌린다) 손가락이 거기를 가리킨다.
    // 버튼을 누르면 끝난다. 구멍 밖은 눌러도 아무 일도 없다 (그 버튼을 누르게 이끄는 안내라서)
    public class TutorialSpotlight : MonoBehaviour
    {
        const string HandSprite = "UI/Icon/hand_pointer";
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
        const float Pad = 8f, HandSize = 300f, BounceHeight = 28f, BounceSpeed = 5f;
        // 손 그림에서 손끝(검지 끝) 위치: 그림 가로·세로의 비율 (왼쪽 아래 기준)
        static readonly Vector2 FingerTip = new Vector2(0.31f, 0.88f);

        readonly RectTransform[] dims = new RectTransform[4];     // 위·아래·왼·오른쪽 어두운 판
        RectTransform hand, canvasRect;
        Canvas canvas;
        Button target;
        bool active;

        public bool IsActive => active;

        public static TutorialSpotlight Create(Transform parent)
        {
            var go = new GameObject("TutorialSpotlight", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var s = go.AddComponent<TutorialSpotlight>();
            s.canvas = parent.GetComponentInParent<Canvas>();
            s.canvasRect = (RectTransform)s.canvas.transform;
            s.Build();
            go.SetActive(false);
            return s;
        }

        void Build()
        {
            for (int i = 0; i < dims.Length; i++)
            {
                var g = new GameObject("Dim" + i, typeof(RectTransform), typeof(Image));
                g.transform.SetParent(transform, false);
                var img = g.GetComponent<Image>();
                img.color = Dim;
                img.raycastTarget = true;                // 구멍 밖은 눌림을 막는다
                dims[i] = (RectTransform)g.transform;
                dims[i].anchorMin = dims[i].anchorMax = new Vector2(0.5f, 0.5f);
                dims[i].pivot = Vector2.zero;
            }
            var h = new GameObject("Hand", typeof(RectTransform), typeof(Image));
            h.transform.SetParent(transform, false);
            var hi = h.GetComponent<Image>();
            hi.sprite = Resources.Load<Sprite>(HandSprite);
            hi.enabled = hi.sprite != null;          // 손 그림(Resources/UI/Icon/hand_pointer)이 없으면 손 없이 구멍만 보여 준다
            hi.preserveAspect = true;
            hi.raycastTarget = false;
            hand = (RectTransform)h.transform;
            hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
            hand.sizeDelta = new Vector2(HandSize, HandSize);
            hand.pivot = FingerTip;
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
            // 손끝이 구멍 가운데 살짝 아래를 콕콕 찍는다
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * BounceSpeed)) * BounceHeight;
            hand.anchoredPosition = new Vector2(hole.center.x, hole.center.y - hole.height * 0.12f - bounce);
            hand.SetAsLastSibling();
        }

        static void SetRect(RectTransform rt, Rect r)
        {
            bool empty = r.width <= 0f || r.height <= 0f;
            rt.gameObject.SetActive(!empty);
            if (empty) return;
            rt.anchoredPosition = r.position;
            rt.sizeDelta = r.size;
        }

        // 가리킬 버튼의 화면 자리를 이 캔버스 좌표로
        Rect HoleRect()
        {
            var rt = (RectTransform)target.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var src = target.GetComponentInParent<Canvas>().rootCanvas;
            Camera srcCam = src.renderMode == RenderMode.ScreenSpaceOverlay ? null : src.worldCamera;
            Camera myCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(srcCam, corners[0]), myCam, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(srcCam, corners[2]), myCam, out var b);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x) - Pad, Mathf.Min(a.y, b.y) - Pad, Mathf.Max(a.x, b.x) + Pad, Mathf.Max(a.y, b.y) + Pad);
        }
    }
}
