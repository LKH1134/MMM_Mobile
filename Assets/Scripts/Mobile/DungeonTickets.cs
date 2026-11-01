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
        public static string Today => KstNow.ToString("yyyyMMdd");

        // 저장된 날짜(yyyyMMdd)보다 오늘이 뒤면 새날. 기기 시계를 되돌렸다 맞추는 것으로는 다시 채워지지 않게 '다른 날'이 아니라 '뒤의 날'만 본다.
        // 저장된 날짜가 IdleAway.MaxClockSkew보다 더 앞서 있으면 한때 시계가 크게 틀렸던 것으로 보고 새날로 친다 (그 날짜까지 막히지 않게)
        public static bool IsNewDay(string storedDay)
        {
            if (!DateTime.TryParseExact(storedDay, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var stored)) return true;
            var today = KstNow.Date;
            return today > stored || stored - today > IdleAway.MaxClockSkew;
        }

        // 다음 한국 00시까지 남은 시간
        public static TimeSpan UntilReset => KstNow.Date.AddDays(1) - KstNow;

        static string DayKey(int dungeon) => $"Dungeon.{dungeon}.Day";
        static string UsedKey(int dungeon) => $"Dungeon.{dungeon}.Used";

        // 오늘 남은 입장 횟수 (날짜가 바뀌었으면 다시 PerDay)
        public static int Remaining(int dungeon)
        {
            if (IsNewDay(PlayerPrefs.GetString(DayKey(dungeon), ""))) return PerDay;
            return Mathf.Clamp(PerDay - PlayerPrefs.GetInt(UsedKey(dungeon), 0), 0, PerDay);
        }

        public static bool CanEnter(int dungeon) => Remaining(dungeon) > 0;

        // 하나 쓴다. 남은 횟수가 없으면 false
        public static bool TryUse(int dungeon)
        {
            int left = Remaining(dungeon);
            if (left <= 0) return false;
            if (IsNewDay(PlayerPrefs.GetString(DayKey(dungeon), ""))) PlayerPrefs.SetString(DayKey(dungeon), Today);   // 시계가 되돌아가 있으면 저장된 (더 뒤) 날짜를 그대로 둔다
            PlayerPrefs.SetInt(UsedKey(dungeon), PerDay - left + 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        // 화면이 켜진 채 00시를 넘기면 다시 그리게 한다 (DungeonResetTimer가 부른다)
        public static void NotifyNewDay() => Changed?.Invoke();
    }
}
