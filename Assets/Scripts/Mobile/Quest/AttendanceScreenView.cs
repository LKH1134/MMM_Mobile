using System.Collections;
using TMPro;
using UnityEngine;

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

        [Header("그림 (Resources 경로)")]
        [SerializeField] string cellSprite = "UI/UI_PP_In_Color_Beige1";
        [SerializeField] string claimedSprite = "UI/UI_PP_In_Color_Beige2";
        [SerializeField] string todaySprite = "UI/UI_PP_In_Color_Green";
        [SerializeField] string finalSprite = "UI/UI_PP_In_Color_Yellow";     // 마지막 날 (전설 무기)
        [SerializeField] string goldIcon = "UI/Icon/gold";
        [SerializeField] string myceliteIcon = "UI/Icon/mycelium_stone";

        int justClaimed;        // 이번에 열 때 방금 받은 날 (0 = 그냥 열기)
        int shownSecond = -1;
        Coroutine pop;

        // 방금 받은 날을 알려 주며 연다 (0 = 그냥 열기)
        public void Open(int claimedDay = 0)
        {
            justClaimed = claimedDay;
            if (gameObject.activeSelf) Refresh();
            else gameObject.SetActive(true);
        }

        void OnEnable()
        {
            PopupLayer.Raise(gameObject, PopupLayer.Popup);
            PopupGate.Opened(this, () => gameObject.SetActive(false));     // 뒤로가기·다른 팝업이 열리면 닫힌다
            Attendance.Changed += Refresh;
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed += CloseOnTab;
            shownSecond = -1;
            Refresh();
        }

        void OnDisable()
        {
            PopupGate.Closed(this);
            Attendance.Changed -= Refresh;
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed -= CloseOnTab;
            if (pop != null) StopCoroutine(pop);
            pop = null;
            justClaimed = 0;
        }

        void CloseOnTab(int tab) => gameObject.SetActive(false);

        void Update()
        {
            if (timerText == null) return;
            if (Attendance.Finished || !Attendance.ClaimedToday)
            {
                if (shownSecond == -2) return;
                shownSecond = -2;
                timerText.text = Attendance.Finished ? $"{Attendance.Days}일 출석을 모두 마쳤어요!" : "";
                return;
            }
            int sec = Mathf.Max(0, Mathf.FloorToInt((float)Attendance.UntilNextDay.TotalSeconds));
            if (sec == shownSecond) return;
            shownSecond = sec;
            var t = System.TimeSpan.FromSeconds(sec);
            timerText.text = $"다음 출석까지 {(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }

        void Refresh()
        {
            if (cells == null) return;
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
                if (cell.dayText != null) cell.dayText.text = $"{day}일째";
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
                messageText.text = justClaimed > 0 ? $"{justClaimed}일째 출석 보상을 받았어요!" : $"{Attendance.Days}일째에는 전설 무기 <color=#E08A00>{Attendance.WeaponTitle}</color>!";
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
