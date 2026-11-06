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
            ClearSaves();
            Time.timeScale = 1f;
            SceneLoader.Load(startScene);
        }

        // 저장된 게임 기록(PlayerPrefs)을 모두 지우고 메모리 캐시도 버린다. 에디터 메뉴 "MoreMush/게임 기록 전체 초기화"도 이것을 부른다
        public static void ClearSaves()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            LocalBackup.DeleteAll();       // 백업 파일도 (안 지우면 다음 실행에 옛 세계수가 되살아난다)
            GameClock.Reload();
            IdleProgress.ReloadLevels();   // 메모리에 둔 강화 레벨·잔액도 버린다
            Wallet.Reload();
            Formation.Reload();
            Equipment.ReloadAll();
            WorldTree.Reload();            // 세계수 JSON 캐시 (안 버리면 다음 저장 때 옛 값을 다시 쓴다)
            AdBox.ResetTimer();            // 광고 상자 쿨타임도 처음부터
            DailyQuests.Reload();          // 일일 퀘스트 진행도도 처음부터
        }
    }
}
