using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 출석 체크 화면 (프리팹 AttendanceScreen, Main 씬 Stage 아래 — QuestScreen과 같은 나무 창). 꺼 두고 Open으로 켠다.
    // 칸 = "N일째" · 보상 그림 · 개수. 받은 날은 체크, 오늘 받은 칸은 초록 칸으로 튀어 오르고, 다음 날 칸에 NEXT, 마지막 날은 노란 칸.
    // 아래에 다음 출석(한국 00시)까지 남은 시간. 모양·자리는 프리팹에서 고친다
    public class AttendanceScreenView : MonoBehaviour
    {
        [SerializeField] TMP_Text messageText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] AttendanceCellView[] cells;     // 1일째부터 차례로 (Attendance.Days개)
        [SerializeField] Button claimButton;             // "받기" (오늘 받을 게 남았을 때만 보인다. 타이머 글자 자리)
        [SerializeField] Button closeButton;             // 창 오른쪽 위 X

        [Header("그림 (Resources 경로)")]
        [SerializeField] string cellSprite = UiSkin.CardBeige;
        [SerializeField] string claimedSprite = "UI/UI_PP_In_Color_Beige2";
        [SerializeField] string todaySprite = UiSkin.CardGreen;
        [SerializeField] string finalSprite = "UI/UI_PP_In_Color_Yellow";     // 마지막 날 (전설 무기)
        [SerializeField] string goldIcon = UiSkin.IconGold;
        [SerializeField] string myceliteIcon = UiSkin.IconMycelite;

        [SerializeField] float closeDelay = 1f;          // 받기를 누른 뒤 창이 닫힐 때까지 (초)

        int justClaimed;        // 이번에 열 때 방금 받은 날 (0 = 아직 안 받았거나 그냥 열기)
        int shownSecond = -1;
        Coroutine pop, autoClose;

        // 연다. 오늘 받을 보상이 남았으면 "받기" 버튼이 보이고, 누르지 않고 닫아도 닫을 때 받는다.
        // 받지 못한 채 닫혔다면(앱 강제 종료 등) 보상이 그대로 남아 다음 접속 때 다시 뜬다
        public void Open(int claimedDay = 0)
        {
            justClaimed = claimedDay;
            if (gameObject.activeSelf) Refresh();
            else gameObject.SetActive(true);
        }

        // 받기 버튼: 오늘 보상을 받고 받은 칸을 보여 준 뒤 closeDelay초 후 창을 닫는다
        void Claim()
        {
            int day = Attendance.Claimed + 1;
            justClaimed = day;                  // Claim이 부르는 Changed → Refresh에서 받은 칸을 알 수 있게 먼저 정한다
            if (Attendance.Claim() == 0) { justClaimed = 0; Refresh(); return; }
            if (autoClose != null) StopCoroutine(autoClose);
            autoClose = StartCoroutine(CloseLater());
        }

        IEnumerator CloseLater()
        {
            yield return new WaitForSecondsRealtime(closeDelay);
            autoClose = null;
            gameObject.SetActive(false);
        }

        void Awake()
        {
            if (claimButton != null) claimButton.onClick.AddListener(Claim);
            if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        void OnEnable()
        {
            PopupLayer.Raise(gameObject, PopupLayer.Popup);
            PopupGate.Opened(this, () => gameObject.SetActive(false));     // 뒤로가기·다른 팝업이 열리면 닫힌다
            Attendance.Changed += Refresh;
            Loc.Changed += OnLanguageChanged;
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed += CloseOnTab;
            shownSecond = -1;
            Refresh();
        }

        void OnDisable()
        {
            PopupGate.Closed(this);
            Attendance.Changed -= Refresh;
            Loc.Changed -= OnLanguageChanged;
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed -= CloseOnTab;
            if (pop != null) StopCoroutine(pop);
            pop = null;
            autoClose = null;       // 꺼지면 코루틴도 멈춘다
            justClaimed = 0;
            // 받기를 누르지 않고 닫았으면 닫을 때 받는다 (씬이 사라지는 중에는 건드리지 않는다)
            if (gameObject.scene.isLoaded && Attendance.CanClaim) Attendance.Claim();
        }

        // 언어가 바뀌면 타이머 글자도 다시 쓴다
        void OnLanguageChanged()
        {
            shownSecond = -1;
            Refresh();
        }

        void CloseOnTab(int tab) => gameObject.SetActive(false);

        void Update()
        {
            if (timerText == null) return;
            if (Attendance.Finished || !Attendance.ClaimedToday)
            {
                if (shownSecond == -2) return;
                shownSecond = -2;
                timerText.text = Attendance.Finished ? Loc.F("attendance.all_done", Attendance.Days) : "";
                return;
            }
            int sec = Mathf.Max(0, Mathf.FloorToInt((float)Attendance.UntilNextDay.TotalSeconds));
            if (sec == shownSecond) return;
            shownSecond = sec;
            var t = System.TimeSpan.FromSeconds(sec);
            timerText.text = Loc.F("attendance.next_in", (int)t.TotalHours, t.Minutes, t.Seconds);
        }

        void Refresh()
        {
            if (cells == null) return;
            bool toClaim = justClaimed == 0 && Attendance.CanClaim;       // 오늘 보상이 남았다 → 받기 버튼
            if (claimButton != null) claimButton.gameObject.SetActive(toClaim);
            int claimed = Attendance.Claimed;
            int nextDay = claimed < Attendance.Days ? claimed + 1 : 0;
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;
                int day = i + 1;
                bool show = day <= Attendance.Days;
                cell.gameObject.SetActive(show);
                if (!show) continue;
                var reward = Attendance.RewardOf(day);
                bool got = day <= claimed;
                if (cell.background != null)
                    cell.background.sprite = Resources.Load<Sprite>(day == justClaimed ? todaySprite : got ? claimedSprite : day == Attendance.Days ? finalSprite : cellSprite);
                if (cell.dayText != null) cell.dayText.text = Loc.F("attendance.day", day);
                if (cell.icon != null)
                {
                    cell.icon.sprite = IconOf(reward);
                    cell.icon.color = got ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                }
                if (cell.check != null) cell.check.gameObject.SetActive(got);
                if (cell.amount != null) cell.amount.text = AmountOf(reward);
                if (cell.next != null) cell.next.SetActive(day == nextDay);
                cell.transform.localScale = Vector3.one;
            }
            if (messageText != null)
                messageText.text = justClaimed > 0 ? Loc.F("attendance.claimed_msg", justClaimed)
                    : toClaim ? Loc.F("attendance.claim_msg", claimed + 1)
                    : Loc.F("attendance.final_msg", Attendance.Days, Attendance.WeaponTitle);
            if (justClaimed > 0 && justClaimed <= cells.Length && cells[justClaimed - 1] != null && isActiveAndEnabled)
            {
                if (pop != null) StopCoroutine(pop);
                pop = StartCoroutine(Pop(cells[justClaimed - 1].transform));
            }
        }

        Sprite IconOf(Attendance.Reward r)
        {
            switch (r.kind)
            {
                case AttendanceRewardKind.Gold: return Resources.Load<Sprite>(goldIcon);
                case AttendanceRewardKind.Mycelite: return Resources.Load<Sprite>(myceliteIcon);
                default: return Equipment.Weapon.Sprite(Attendance.WeaponItem);
            }
        }

        static string AmountOf(Attendance.Reward r)
        {
            switch (r.kind)
            {
                case AttendanceRewardKind.Gold: return Wallet.Format(Attendance.GoldOf(r));
                case AttendanceRewardKind.Mycelite: return "x" + r.amount.ToString("N0");
                default: return Attendance.WeaponTitle;
            }
        }

        // 오늘 받은 칸이 도장 찍히듯 커졌다 돌아온다
        IEnumerator Pop(Transform t)
        {
            const float Duration = 0.35f;
            for (float time = 0f; time < Duration; time += Time.unscaledDeltaTime)
            {
                t.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(time / Duration * Mathf.PI));
                yield return null;
            }
            t.localScale = Vector3.one;
            pop = null;
        }
    }
}
