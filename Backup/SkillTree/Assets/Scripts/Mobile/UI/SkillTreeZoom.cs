using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 스킬 트리 확대·축소. 두 손가락을 벌리고 오므리면 그 가운데를 기준으로 커지고 작아지며, 두 손가락을 함께 움직이면 따라 움직인다.
    // 에디터·PC에서는 Ctrl + 마우스 휠로 같은 동작을 한다 (휠만 돌리면 원래대로 스크롤).
    // 두 손가락으로 조작하는 동안은 ScrollRect의 끌기를 멈추고, 손가락을 떼면 ScrollRect가 범위 안으로 되돌린다
    [RequireComponent(typeof(ScrollRect))]
    public class SkillTreeZoom : MonoBehaviour
    {
        [SerializeField] float minZoom = 0.35f;       // 트리 전체 폭이 화면에 들어가는 배율이 이보다 크면 그쪽을 쓴다
        [SerializeField] float maxZoom = 1.25f;
        [SerializeField] float wheelStep = 0.1f;      // Ctrl + 휠 한 칸에 바뀌는 비율

        ScrollRect scroll;
        RectTransform viewport;
        bool pinching;
        float startDistance, startZoom;
        Vector2 anchorPoint;                          // 두 손가락 가운데에 있던 트리 위의 점 (트리 좌표)

        public float Zoom => scroll.content.localScale.x;

        void Awake()
        {
            scroll = GetComponent<ScrollRect>();
            viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)transform;
        }

        void OnDisable()
        {
            if (pinching) EndPinch();
        }

        void Update()
        {
            var touch = Touchscreen.current;
            int pressed = 0;
            Vector2 a = default, b = default;
            if (touch != null)
                foreach (var t in touch.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (pressed == 0) a = t.position.ReadValue();
                    else if (pressed == 1) b = t.position.ReadValue();
                    pressed++;
                }

            if (pressed >= 2)
            {
                Vector2 mid = (a + b) * 0.5f;
                float distance = Mathf.Max(1f, Vector2.Distance(a, b));
                if (!pinching) BeginPinch(mid, distance);
                SetZoom(startZoom * distance / startDistance, mid, anchorPoint);
            }
            else if (pinching) EndPinch();

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (!pinching && mouse != null && keyboard != null && keyboard.ctrlKey.isPressed)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    Vector2 at = mouse.position.ReadValue();
                    SetZoom(Zoom * (1f + Mathf.Sign(wheel) * wheelStep), at, ContentPoint(at));
                }
            }
        }

        void BeginPinch(Vector2 mid, float distance)
        {
            pinching = true;
            startDistance = distance;
            startZoom = Zoom;
            anchorPoint = ContentPoint(mid);
            // 첫 손가락으로 시작한 끌기를 끝내고, 조작하는 동안 ScrollRect가 움직이지 않게 한다
            if (EventSystem.current != null) scroll.OnEndDrag(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            scroll.velocity = Vector2.zero;
            scroll.horizontal = false;
            scroll.vertical = false;
        }

        void EndPinch()
        {
            pinching = false;
            scroll.vertical = true;
            scroll.horizontal = scroll.content.rect.width * Zoom > viewport.rect.width;
            scroll.velocity = Vector2.zero;
        }

        // 화면 점 → 트리(content) 좌표
        Vector2 ContentPoint(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(scroll.content, screen, null, out var local);
            return local;
        }

        // 배율을 바꾸고, 트리 위의 point가 화면 screen 아래에 오게 옮긴다
        void SetZoom(float zoom, Vector2 screen, Vector2 point)
        {
            var content = scroll.content;
            float fit = viewport.rect.width / Mathf.Max(1f, content.rect.width);
            zoom = Mathf.Clamp(zoom, Mathf.Max(minZoom, Mathf.Min(fit, 1f)), maxZoom);
            content.localScale = new Vector3(zoom, zoom, 1f);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screen, null, out var inView);
            // content는 viewport 왼쪽 아래에 붙어 있고 pivot도 왼쪽 아래
            Vector2 fromCorner = inView - viewport.rect.min;
            content.anchoredPosition = fromCorner - (point - content.rect.min) * zoom;
            if (!pinching) scroll.horizontal = content.rect.width * zoom > viewport.rect.width;
        }
    }
}
