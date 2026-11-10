using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 옵션 창의 "데이터 초기화" 버튼. 누르면 경고 창(HoldConfirmPopup)을 띄우고, 확인 버튼을 5초 동안 누르고 있으면
    // 저장된 기록(PlayerPrefs·백업 파일)을 모두 지우고 Main 홈 화면으로 처음부터 시작한다.
    // 경고 창은 처음 누를 때 맨 위 캔버스 아래에 만든다 — 팝업은 하나만 열리므로(PopupGate) 옵션 창 안에 두면 같이 닫혀 버린다
    [RequireComponent(typeof(Button))]
    public class DataResetButton : MonoBehaviour
    {
        [SerializeField] HoldConfirmPopup confirmPrefab;      // Prefabs/Mobile/UI/Common/HoldConfirmPopup

        HoldConfirmPopup confirm;

        void Awake() => GetComponent<Button>().onClick.AddListener(Press);

        void Press()
        {
            if (confirmPrefab == null) { Debug.LogError("[DataResetButton] confirmPrefab이 비어 있다 (옵션 창 프리팹에서 연결한다)"); return; }
            if (confirm == null)
            {
                var canvas = GetComponentInParent<Canvas>().rootCanvas.transform;
                confirm = Instantiate(confirmPrefab, canvas, false);
                confirm.gameObject.SetActive(false);
            }
            confirm.Show(Loc.T("data.reset_title"), Loc.T("data.reset_msg"), Loc.F("data.reset_hold", Mathf.RoundToInt(confirm.HoldSeconds)), ResetAll);
        }

        static void ResetAll()
        {
            // 전투가 꺼지면서(OnDisable) 모은 골드를 저장한다 — 지우기 전에 먼저 꺼서, 지운 뒤 옛 값이 다시 써지지 않게
            foreach (var battle in FindObjectsByType<IdleBattle>(FindObjectsSortMode.None)) battle.enabled = false;
            ClearSaves();
            Time.timeScale = 1f;
            MainNavigation.Open(MainNavigation.Home);
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
            FeatureUnlock.Reload();        // 기능 해금·안내 기록도 처음부터
            DailyQuests.Reload();          // 일일 퀘스트 진행도도 처음부터
            GuideQuests.Reload();          // 가이드 퀘스트 단계도 처음부터
            Attendance.Reload();           // 출석 기록도 처음부터
        }
    }
}
