using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 예/아니오 확인 팝업 (가운데 창). Show로 제목·본문과 "예"를 눌렀을 때 할 일을 넣는다.
    // 아니오나 바깥을 누르면 아무것도 하지 않고 닫힌다. 다른 팝업 위에 뜨도록 캔버스 바로 아래에 만든다
    [RequireComponent(typeof(UiPopup))]
    public class ConfirmPopup : MonoBehaviour
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text messageText;
        [SerializeField] Button yesButton;
        [SerializeField] Button noButton;

        UiPopup popup;
        System.Action onYes;

        void Awake()
        {
            popup = GetComponent<UiPopup>();
            yesButton.onClick.AddListener(Yes);
            noButton.onClick.AddListener(Close);
        }

        public void Show(string title, string message, System.Action yes)
        {
            // 꺼진 채로 만들어져 아직 Awake 전일 수 있다. Open이 켜면서 Awake가 돈다
            if (popup == null) popup = GetComponent<UiPopup>();
            if (titleText != null && !string.IsNullOrEmpty(title)) titleText.text = title;
            messageText.text = message;
            onYes = yes;
            popup.Open();
        }

        void Yes()
        {
            var action = onYes;
            Close();
            action?.Invoke();
        }

        void Close()
        {
            onYes = null;
            popup.Close();
        }
    }
}
