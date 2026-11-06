using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 설정 창의 "광고 개인정보 설정" 링크. 광고 동의가 필요한 지역(EEA·영국 등) 사용자에게만 보이고,
    // 누르면 동의 양식(UMP 개인정보 옵션)을 다시 띄워 동의를 바꿀 수 있게 한다 (Google 정책상 필요한 입구).
    // 글자 하나에 붙인다 (OptionPopup의 Notice 줄). 그 밖의 사용자에겐 글자를 비워 둔다
    [RequireComponent(typeof(TMP_Text))]
    public class AdPrivacyLink : MonoBehaviour
    {
        const string Label = "<u>광고 개인정보 설정</u>";

        TMP_Text text;
        Button button;

        void Awake()
        {
            text = GetComponent<TMP_Text>();
            if (!TryGetComponent(out button)) button = gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = text;
            button.onClick.AddListener(AdService.ShowPrivacyOptions);
        }

        void OnEnable()
        {
            bool show = AdService.PrivacyOptionsRequired;
            text.text = show ? Label : "";
            text.raycastTarget = show;
            button.enabled = show;
        }
    }
}
