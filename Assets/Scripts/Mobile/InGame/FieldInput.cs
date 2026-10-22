using UnityEngine;
using UnityEngine.EventSystems;

namespace MoreMush.Mobile
{
    // InGame 터치 입력. 필드 RectTransform(레이캐스트를 받는 투명 Image가 있는 곳)에 붙인다.
    // 던지기 전: 눌러 끌었다 놓으면 끈 반대 방향으로 수확기를 던진다. 끄는 동안 날아갈 방향을 화살표로 보여 준다.
    // 던진 뒤: 화면 어디를 눌러도 좌우로 끄는 만큼 바가 따라 움직인다. 한 손가락만 받는다
    public class FieldInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] HarvestField field;

        [Header("조준")]
        [SerializeField] RectTransform arrow;         // 화살표 루트. 피벗은 아래 가운데, 그림은 위쪽을 가리킨다
        [SerializeField] RectTransform arrowShaft;    // 길이에 맞춰 높이를 늘릴 몸통
        [SerializeField] float headLength = 70f;      // 화살촉 높이. 몸통 길이 = 화살표 길이 - 이것
        [SerializeField] float startOffset = 20f;     // 수확기 가장자리에서 띄울 거리
        [SerializeField] RectTransform readyRing;     // 던지기 전 수확기 둘레에 보이는 원. 비워도 된다
        [SerializeField] float minDrag = 40f;         // 이보다 짧게 끌고 놓으면 취소
        [SerializeField] float maxDrag = 450f;        // 화살표가 가장 길어지는 끌기 거리
        [SerializeField] float minArrow = 130f;
        [SerializeField] float maxArrow = 380f;

        [Header("바")]
        [SerializeField] float barSensitivity = 1f;   // 손가락이 움직인 거리 대비 바가 움직이는 비율

        enum Mode { None, Aim, Bar }

        Mode mode;
        int pointerId;
        Vector2 start;
        Vector2 current;

        public bool Interactable { get; set; } = true;

        void Awake() => ShowArrow(false);

        void Update()
        {
            // 조작 중에 일시정지·옵션·시간 종료가 되면 손을 뗀 것으로 본다
            if (mode != Mode.None && !Usable()) Release();

            if (readyRing == null) return;
            bool ready = Usable() && field.MainReady;
            if (readyRing.gameObject.activeSelf != ready) readyRing.gameObject.SetActive(ready);
            if (ready) readyRing.anchoredPosition = field.Main.Position;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (mode != Mode.None || !Usable()) return;
            if (!ToLocal(e, out start)) return;
            pointerId = e.pointerId;
            current = start;
            mode = field.MainReady ? Mode.Aim : Mode.Bar;
            if (mode == Mode.Aim) RefreshArrow();
        }

        public void OnDrag(PointerEventData e)
        {
            if (mode == Mode.None || e.pointerId != pointerId) return;
            if (!ToLocal(e, out var p)) return;

            if (mode == Mode.Bar && field.Bar != null) field.Bar.MoveBy((p.x - current.x) * barSensitivity, field.Rect);
            current = p;
            if (mode == Mode.Aim) RefreshArrow();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (mode == Mode.None || e.pointerId != pointerId) return;
            if (ToLocal(e, out var p)) current = p;
            Vector2 pull = start - current;
            bool launch = mode == Mode.Aim && pull.magnitude >= minDrag;
            Release();
            if (launch) field.LaunchMain(pull);
        }

        bool Usable() => Interactable && field.Running && Time.timeScale > 0f;

        void Release()
        {
            mode = Mode.None;
            ShowArrow(false);
        }

        void RefreshArrow()
        {
            Vector2 pull = start - current;
            if (pull.magnitude < minDrag)
            {
                ShowArrow(false);
                return;
            }

            ShowArrow(true);
            Vector2 dir = pull.normalized;
            float length = Mathf.Lerp(minArrow, maxArrow, Mathf.InverseLerp(minDrag, maxDrag, pull.magnitude));
            arrow.anchoredPosition = field.Main.Position + dir * (field.Main.Radius + startOffset);
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            if (arrowShaft != null) arrowShaft.sizeDelta = new Vector2(arrowShaft.sizeDelta.x, Mathf.Max(0f, length - headLength));
        }

        void ShowArrow(bool show)
        {
            if (arrow != null && arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
        }

        bool ToLocal(PointerEventData e, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(field.Rect, e.position, e.pressEventCamera, out local);
    }
}
