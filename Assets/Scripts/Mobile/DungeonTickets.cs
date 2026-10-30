using System;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 던전 입장 횟수: 던전마다 하루 PerDay번. 한국 시각(UTC+9) 00시에 다시 채운다 (기기 시간대와 상관없이).
    // 입장할 때 하나 쓴다. PlayerPrefs("Dungeon.<번호>.Day" = 마지막으로 쓴 한국 날짜, "Dungeon.<번호>.Used" = 그날 쓴 수)
    public static class DungeonTickets
    {
        public const int PerDay = 2;
        static readonly TimeSpan Kst = TimeSpan.FromHours(9);

        public static event Action Changed;

        public static DateTime KstNow => DateTime.UtcNow + Kst;
        static string Today => KstNow.ToString("yyyyMMdd");

        // 다음 한국 00시까지 남은 시간
        public static TimeSpan UntilReset => KstNow.Date.AddDays(1) - KstNow;

        static string DayKey(int dungeon) => $"Dungeon.{dungeon}.Day";
        static string UsedKey(int dungeon) => $"Dungeon.{dungeon}.Used";

        // 오늘 남은 입장 횟수 (날짜가 바뀌었으면 다시 PerDay)
        public static int Remaining(int dungeon)
        {
            if (PlayerPrefs.GetString(DayKey(dungeon), "") != Today) return PerDay;
            return Mathf.Clamp(PerDay - PlayerPrefs.GetInt(UsedKey(dungeon), 0), 0, PerDay);
        }

        public static bool CanEnter(int dungeon) => Remaining(dungeon) > 0;

        // 하나 쓴다. 남은 횟수가 없으면 false
        public static bool TryUse(int dungeon)
        {
            int left = Remaining(dungeon);
            if (left <= 0) return false;
            PlayerPrefs.SetString(DayKey(dungeon), Today);
            PlayerPrefs.SetInt(UsedKey(dungeon), PerDay - left + 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        // 화면이 켜진 채 00시를 넘기면 다시 그리게 한다 (DungeonResetTimer가 부른다)
        public static void NotifyNewDay() => Changed?.Invoke();
    }
}
