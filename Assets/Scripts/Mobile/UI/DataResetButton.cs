using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 옵션 팝업의 데이터 초기화 버튼. 누르면 확인 팝업을 띄우고, "예"를 고르면 저장된 데이터(PlayerPrefs)를 모두 지우고
    // 처음 로딩(Start 씬)부터 다시 시작한다
    [RequireComponent(typeof(Button))]
    public class DataResetButton : MonoBehaviour
    {
        [SerializeField] PopupSlot confirmPopup;      // ConfirmPopup 프리팹. 처음 누를 때 캔버스 바로 아래에 만든다
        [SerializeField] string title = "데이터 초기화";
        [SerializeField, TextArea] string message = "초기화 하시겠습니까?\n초기화 후 메인 화면으로 이동됩니다.";
        [SerializeField] string startScene = "Start";

        void Awake() => GetComponent<Button>().onClick.AddListener(Press);

        void Press()
        {
            // 옵션 팝업보다 위에 뜨도록 맨 위 캔버스에 만든다
            var canvas = GetComponentInParent<Canvas>().rootCanvas.transform;
            confirmPopup.Get(canvas).GetComponent<ConfirmPopup>().Show(title, message, ResetAll);
        }

        void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            IdleProgress.ReloadLevels();   // 메모리에 둔 강화 레벨·잔액도 버린다
            Wallet.Reload();
            Formation.Reload();
            Equipment.ReloadAll();
            Time.timeScale = 1f;
            SceneLoader.Load(startScene);
        }
    }
}
