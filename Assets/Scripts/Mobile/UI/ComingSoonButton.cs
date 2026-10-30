using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 아직 없는 기능의 버튼: 누르면 "아직 개발중인 기능입니다." 알림 창만 띄운다 (예: 하단바 세계수).
    // 알림 창은 ConfirmPopup 프리팹을 처음 누를 때 popupRoot(디자인 영역) 아래에 만든다
    [RequireComponent(typeof(Button))]
    public class ComingSoonButton : MonoBehaviour
    {
        [SerializeField] PopupSlot popup;           // ConfirmPopup 프리팹
        [SerializeField] RectTransform popupRoot;   // 비우면 이 버튼의 캔버스
        [SerializeField] string title = "알림";
        [SerializeField] string message = "아직 개발중인 기능입니다.";

        void Awake() => GetComponent<Button>().onClick.AddListener(Show);

        void Show()
        {
            Transform root = popupRoot != null ? popupRoot : GetComponentInParent<Canvas>().rootCanvas.transform;
            popup.Get(root).GetComponent<ConfirmPopup>().ShowNotice(title, message);
        }
    }
}
