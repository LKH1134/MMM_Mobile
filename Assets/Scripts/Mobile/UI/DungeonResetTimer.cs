using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 던전 머리줄의 "남은 시간: n시간 m분" — 다음 한국 00시(입장 횟수 충전)까지. 1초마다 고치고, 00시를 넘기면 카드들을 다시 그리게 한다
    public class DungeonResetTimer : MonoBehaviour
    {
        [SerializeField] TMP_Text timeText;

        float nextTick;
        int shownMinute = -1;
        System.DateTime day;

        void OnEnable()
        {
            day = DungeonTickets.KstNow.Date;
            shownMinute = -1;
            Loc.Changed += OnLanguageChanged;
            Tick();
        }

        void OnDisable() => Loc.Changed -= OnLanguageChanged;

        // 분이 그대로여도 새 언어로 다시 쓴다
        void OnLanguageChanged()
        {
            shownMinute = -1;
            Tick();
        }

        void Update()
        {
            if (Time.unscaledTime < nextTick) return;
            Tick();
        }

        void Tick()
        {
            nextTick = Time.unscaledTime + 1f;
            var today = DungeonTickets.KstNow.Date;
            if (today != day)
            {
                day = today;
                DungeonTickets.NotifyNewDay();
            }
            var left = DungeonTickets.UntilReset;
            int minutes = (int)left.TotalMinutes;
            if (minutes == shownMinute || timeText == null) return;
            shownMinute = minutes;
            timeText.text = Loc.F("dungeon.time_left", minutes / 60, minutes % 60);
        }
    }
}
