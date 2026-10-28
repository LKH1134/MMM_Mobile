using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 광고 연동 확인용 임시 버튼. 버튼 오브젝트에 붙이면 누를 때 리워드 광고를 띄운다.
    // 보상 지급은 아직 없다 — 받았다는 문구만 남긴다. 최종 UI로 옮길 때는 AdService 호출 한 줄만 옮기면 된다.
    [RequireComponent(typeof(Button))]
    public class AdmobTestButton : MonoBehaviour
    {
        [SerializeField] TMP_Text statusText;   // 진행 상황 표시용. 없어도 된다 (Console·logcat에는 항상 남는다)

        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
            AdService.Status += ShowStatus;
        }

        void Start() => AdService.Initialize();

        void OnDestroy() => AdService.Status -= ShowStatus;

        void OnClick() => AdService.ShowRewarded(() => ShowStatus("테스트 보상 지급됨"));

        void ShowStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
