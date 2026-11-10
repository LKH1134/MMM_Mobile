using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 되돌릴 수 없는 일(데이터 초기화) 확인 창: 경고 그림 · 경고문 · "확인 버튼을 N초 동안 누르세요" · 진행 막대 · 확인/취소.
    // 확인 버튼을 누르고 있는 동안 막대가 차고, holdSeconds를 다 채우면 할 일을 한다. 중간에 손을 떼거나 버튼 밖으로 나가면 막대가 처음으로 돌아간다.
    // 취소·바깥(Dim)을 누르면 아무것도 하지 않고 닫힌다. 시간은 일시정지(timeScale 0)와 상관없이 실제 시간으로 잰다.
    // 프리팹 Prefabs/Mobile/UI/Common/HoldConfirmPopup. 다른 팝업 위에 뜨도록 캔버스 바로 아래에 만든다 (PopupSlot)
    [RequireComponent(typeof(UiPopup))]
    public class HoldConfirmPopup : MonoBehaviour
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text messageText;
        [SerializeField] TMP_Text holdText;
        [SerializeField] RectTransform holdFill;     // 막대 안쪽 (anchorMax.x = 진행 비율)
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;
        [SerializeField] float holdSeconds = 5f;

        UiPopup popup;
        System.Action onConfirm;
        float held;
        bool holding, done;

        void Awake()
        {
            popup = GetComponent<UiPopup>();
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);
            if (confirmButton != null)
            {
                var press = confirmButton.gameObject.GetComponent<HoldPress>();
                if (press == null) press = confirmButton.gameObject.AddComponent<HoldPress>();
                press.Down = () => holding = !done;
                press.Up = () => { holding = false; if (!done) SetProgress(0f); };
            }
        }

        public float HoldSeconds => holdSeconds;

        public void Show(string title, string message, string hold, System.Action confirm)
        {
            // 꺼진 채로 만들어져 아직 Awake 전일 수 있다. Open이 켜면서 Awake가 돈다
            if (popup == null) popup = GetComponent<UiPopup>();
            if (titleText != null && !string.IsNullOrEmpty(title)) titleText.text = title;
            if (messageText != null) messageText.text = message;
            if (holdText != null) holdText.text = hold;
            onConfirm = confirm;
            holding = done = false;
            SetProgress(0f);
            popup.Open();
        }

        void Update()
        {
            if (!holding || done) return;
            held += Time.unscaledDeltaTime;
            SetProgress(held / Mathf.Max(0.01f, holdSeconds));
            if (held < holdSeconds) return;
            done = true;
            holding = false;
            var action = onConfirm;
            onConfirm = null;
            popup.Close();
            action?.Invoke();
        }

        void SetProgress(float k)
        {
            k = Mathf.Clamp01(k);
            if (k <= 0f) held = 0f;
            if (holdFill == null) return;
            holdFill.anchorMax = new Vector2(k, 1f);
            if (holdFill.gameObject.activeSelf != k > 0f) holdFill.gameObject.SetActive(k > 0f);
        }

        void OnDisable()
        {
            holding = false;
            SetProgress(0f);
        }

        void Close()
        {
            onConfirm = null;
            holding = false;
            popup.Close();
        }

        // 버튼을 누르고 있는지 알려 준다 (누름 · 뗌 · 버튼 밖으로 나감)
        class HoldPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public System.Action Down, Up;
            public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Down?.Invoke(); }
            public void OnPointerUp(PointerEventData e) => Up?.Invoke();
            public void OnPointerExit(PointerEventData e) => Up?.Invoke();
        }
    }
}
