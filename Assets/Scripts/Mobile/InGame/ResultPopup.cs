using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 결과 팝업. 시간이 다 되면 HarvestGame이 열어 이번 판에 번 골드만 보여 준다.
    // 버튼은 둘: 왼쪽(입장 횟수를 하나 쓰고 이 씬을 다시 연다) · "나가기"(Main). 닫기 버튼·배경 눌러 닫기는 없다.
    //   레벨을 깼으면 왼쪽은 "다음 레벨 (레벨 n)" — 씬이 다시 열리면 StageProgress.Current(= 던전 레벨)가 이미 다음이다. 못 깼으면 "다시 도전".
    //   입장 횟수를 다 썼으면 "광고 보고 도전 (n/2)" (광고 입장, DungeonTickets.AdPerDay), 그것도 없으면 막힌다.
    // "다음 레벨 자동 도전"(체크 상자, 기기에 저장): 켜져 있고, 레벨을 깼고, 입장 횟수가 남았으면 AutoNextSeconds초를 센 뒤 다음 레벨로 들어간다.
    //   광고 입장은 자동으로 하지 않는다. 버튼을 누르거나 체크를 끄면 카운트를 멈춘다. 체크 줄(AutoNextRow)은 프리팹에 배치되어 있다
    [RequireComponent(typeof(UiPopup))]
    public class ResultPopup : MonoBehaviour
    {
        [SerializeField] TMP_Text earnedText;
        [SerializeField] GameObject myceliteRow;      // 균사석 아이콘 + 글자 줄 (골드 줄 아래)
        [SerializeField] TMP_Text myceliteText;
        [SerializeField] Button retryButton;
        [SerializeField] Button mainButton;
        [SerializeField] string mainScene = MainNavigation.MainScene;
        [SerializeField] float countUpDuration = 0.8f;   // 번 골드가 0부터 올라가는 시간
        [Header("다음 레벨 자동 도전 줄")]
        [SerializeField] Button autoRow;                 // 줄 전체 (누르면 켜고 끈다)
        [SerializeField] Image check;                    // 체크 표시
        [SerializeField] TMP_Text autoLabel;
        // 몇 번 던전인지는 DungeonSession.Current (벽돌깨기 던전 1~3이 이 씬을 같이 쓴다)
        static int dungeon => DungeonSession.Current;

        const string AutoNextKey = "Dungeon.AutoNext";

        UiPopup popup;
        Coroutine countUp, autoNext;
        TMP_Text retryLabel;
        bool cleared;

        static float AutoNextSeconds => Mathf.Max(1f, (float)GameBalance.Const("dungeonAutoNextSeconds"));   // 상수 시트

        static bool AutoNext
        {
            get => PlayerPrefs.GetInt(AutoNextKey, 0) == 1;
            set { PlayerPrefs.SetInt(AutoNextKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void Awake()
        {
            popup = GetComponent<UiPopup>();
            retryButton.onClick.AddListener(Retry);
            mainButton.onClick.AddListener(GoMain);
            retryLabel = retryButton.GetComponentInChildren<TMP_Text>(true);
            if (retryLabel != null) DungeonEntry.FitToWidth(retryLabel);   // "다음 레벨 (레벨 123)"·"광고 보고 도전 (2/2)"
            if (autoRow != null) autoRow.onClick.AddListener(ToggleAutoNext);
            if (check != null) check.enabled = AutoNext;
            SetAutoLabel(-1);
        }

        public void Show(long earned) => Show(earned, 0, false);

        // earned = 받은 골드, mycelite = 받은 균사석 (0이면 줄을 숨긴다), cleared = 이번 판으로 레벨을 넘었는지 (HarvestGame.StageCleared)
        public void Show(long earned, long mycelite, bool cleared)
        {
            // 꺼진 채로 만들어져 아직 Awake 전일 수 있다. Open이 켜면서 Awake가 돈다
            if (popup == null) popup = GetComponent<UiPopup>();
            popup.Open();
            this.cleared = cleared;
            RefreshButtons();
            if (countUp != null) StopCoroutine(countUp);
            countUp = StartCoroutine(CountUp(earned));
            ShowMycelite(mycelite);
            RestartAutoNext();
        }

        // 받은 균사석 한 줄 (프리팹 MyceliteRow: 균사석 아이콘 + 글자, 골드 줄과 같은 모양). 0이면 줄을 숨긴다
        void ShowMycelite(long mycelite)
        {
            if (myceliteRow != null) myceliteRow.SetActive(mycelite > 0);
            if (myceliteText != null) myceliteText.text = Wallet.Format(mycelite);
        }

        // 시간이 멈춰 있어도 돌도록 unscaled로 센다
        IEnumerator CountUp(long earned)
        {
            for (float t = 0f; t < countUpDuration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / countUpDuration, 3f);
                earnedText.text = Wallet.Format((long)(earned * k));
                yield return null;
            }
            earnedText.text = Wallet.Format(earned);
            countUp = null;
        }

        void RefreshButtons()
        {
            bool ticket = DungeonTickets.CanEnter(dungeon);
            bool byAd = DungeonTickets.CanEnterWithAd(dungeon);
            retryButton.interactable = ticket || byAd;
            if (retryLabel == null) return;
            if (!ticket && byAd) retryLabel.text = Loc.F("result.ad_retry", DungeonTickets.AdRemaining(dungeon), DungeonTickets.AdPerDay);
            else retryLabel.text = cleared ? Loc.F("result.next_level", DungeonTickets.Level(dungeon)) : Loc.T("result.retry_challenge");
        }

        // ── 다음 레벨 자동 도전 ──
        bool CanAutoNext => AutoNext && cleared && DungeonTickets.CanEnter(dungeon);

        void RestartAutoNext()
        {
            StopAutoNext();
            if (CanAutoNext) autoNext = StartCoroutine(AutoNextCountdown());
            else SetAutoLabel(-1);
        }

        void StopAutoNext()
        {
            if (autoNext != null) StopCoroutine(autoNext);
            autoNext = null;
            SetAutoLabel(-1);
        }

        // 일시정지와 상관없이 실제 시간으로 센다. 남은 횟수가 그새 바뀌었으면 들어가지 않는다
        IEnumerator AutoNextCountdown()
        {
            for (float left = AutoNextSeconds; left > 0f; left -= Time.unscaledDeltaTime)
            {
                SetAutoLabel(Mathf.CeilToInt(left));
                yield return null;
            }
            autoNext = null;
            SetAutoLabel(-1);
            if (CanAutoNext) Retry();
        }

        void SetAutoLabel(int seconds)
        {
            if (autoLabel == null) return;
            autoLabel.text = seconds > 0 ? Loc.F("result.auto_next_countdown", seconds) : Loc.T("result.auto_next");
        }

        void ToggleAutoNext()
        {
            AutoNext = !AutoNext;
            if (check != null) check.enabled = AutoNext;
            RestartAutoNext();
        }

        // 왼쪽 버튼: 입장 횟수가 있으면 하나 쓰고 이 씬을 다시 연다 (레벨을 깼으면 그게 다음 레벨), 없으면 광고 입장.
        // SceneLoader로 넘어간다 (로딩 팝업·터치 막기, 두 번 눌러도 한 번만 — 입장 횟수도 넘어갈 때만 쓴다)
        void Retry()
        {
            StopAutoNext();
            if (SceneLoader.IsLoading) return;
            string scene = gameObject.scene.name;
            if (DungeonTickets.CanEnter(dungeon))
            {
                if (DungeonSession.LoadHarvest(dungeon, scene)) DungeonSession.MarkEntry(dungeon, false);   // 횟수는 판이 끝나 보상을 받을 때
            }
            else if (DungeonTickets.CanEnterWithAd(dungeon))
            {
                if (!DungeonEntry.EnterWithAd(dungeon, scene) && retryLabel != null) retryLabel.text = Loc.T("ad.preparing");
            }
            else retryButton.interactable = false;
        }

        void GoMain()
        {
            StopAutoNext();
            if (!SceneLoader.Load(mainScene)) Debug.LogWarning($"[ResultPopup] 씬을 열 수 없음: {mainScene}");
        }

        void OnDisable() => StopAutoNext();
    }
}
