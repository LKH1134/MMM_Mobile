using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 누르고 있으면 버튼 클릭을 되풀이한다 (강화 버튼 연타 대신). 처음 delay초 뒤부터 점점 빨라진다
    [RequireComponent(typeof(Button))]
    public class HoldRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] float delay = 0.4f;
        [SerializeField] float startInterval = 0.15f;
        [SerializeField] float minInterval = 0.03f;

        Button button;
        bool held;
        float wait, interval;

        void Awake() => button = GetComponent<Button>();

        public void OnPointerDown(PointerEventData e)
        {
            held = true;
            wait = delay;
            interval = startInterval;
        }

        public void OnPointerUp(PointerEventData e) => held = false;
        public void OnPointerExit(PointerEventData e) => held = false;
        void OnDisable() => held = false;

        void Update()
        {
            if (!held) return;
            wait -= Time.unscaledDeltaTime;
            if (wait > 0f) return;
            if (!button.IsInteractable()) { held = false; return; }
            button.onClick.Invoke();
            interval = Mathf.Max(minInterval, interval * 0.85f);
            wait = interval;
        }
    }
}
