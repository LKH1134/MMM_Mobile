using System;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 던전 입장 횟수: 던전마다 하루 PerDay번. 한국 시각(UTC+9) 00시에 다시 채운다 (기기 시간대와 상관없이).
    // 해금: 던전 시트의 unlockStage에 진입해야 열린다 (IsUnlocked). 입장할 때 하나 쓴다. PlayerPrefs("Dungeon.<번호>.Day" = 마지막으로 쓴 한국 날짜, "Dungeon.<번호>.Used" = 그날 쓴 수)
    // 입장 횟수를 다 쓰면 광고를 보고 하루 AdPerDay번 더 들어간다 ("Dungeon.<번호>.AdDay"·"AdUsed")
    public static class DungeonTickets
    {
        public static int PerDay => GameBalance.ConstInt("dungeonPerDay");     // 상수 시트
        static readonly TimeSpan Kst = TimeSpan.FromHours(9);

        public static event Action Changed;

        public static DateTime KstNow => GameClock.UtcNow + Kst;   // 게임 시계: 기기 시계를 되돌려도 뒤로 가지 않는다
        public static string Today => KstNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);   // 기기 달력(불기·이슬람력)과 상관없이 서기

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

        // 이번 주(한국 월요일 00시 시작) 첫날 yyyyMMdd — 주간 퀘스트·세계수 주간 부탁
        public static string ThisWeek
        {
            get
            {
                var now = KstNow.Date;
                return now.AddDays(-(((int)now.DayOfWeek + 6) % 7)).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        // 저장된 주(월요일 yyyyMMdd)보다 이번 주가 뒤면 새 주. 저장값이 크게 앞서 있으면 틀린 시계였던 것으로 본다 (IsNewDay와 같은 규칙)
        public static bool IsNewWeek(string storedWeek)
        {
            if (!DateTime.TryParseExact(storedWeek, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var stored)) return true;
            var monday = DateTime.ParseExact(ThisWeek, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            return monday > stored || stored - KstNow.Date > IdleAway.MaxClockSkew;
        }

        // 다음 월요일 한국 00시까지 남은 시간
        public static TimeSpan UntilWeekReset
        {
            get
            {
                var now = KstNow;
                return now.Date.AddDays(7 - ((int)now.DayOfWeek + 6) % 7) - now;
            }
        }

        static string DayKey(int dungeon) => $"Dungeon.{dungeon}.Day";
        static string UsedKey(int dungeon) => $"Dungeon.{dungeon}.Used";

        // 오늘 남은 입장 횟수 (날짜가 바뀌었으면 다시 PerDay)
        public static int Remaining(int dungeon)
        {
            if (IsNewDay(PlayerPrefs.GetString(DayKey(dungeon), ""))) return PerDay;
            return Mathf.Clamp(PerDay - PlayerPrefs.GetInt(UsedKey(dungeon), 0), 0, PerDay);
        }

        // 해금에 진입해야 하는 스테이지 (던전 시트, 0 = 처음부터, 행이 없으면 -1)
        public static int UnlockStage(int dungeon) => GameBalance.DungeonUnlockStage(dungeon);

        // 지금 스테이지(싸우는 중)가 unlockStage 이상이면 열린다 — 그 스테이지에 진입하면 해금
        public static bool IsUnlocked(int dungeon)
        {
            int need = UnlockStage(dungeon);
            return need >= 0 && IdleProgress.Stage >= need;
        }

        public static bool CanEnter(int dungeon) => IsUnlocked(dungeon) && Remaining(dungeon) > 0;

        // 하나 쓴다. 남은 횟수가 없으면 false
        public static bool TryUse(int dungeon)
        {
            if (!IsUnlocked(dungeon)) return false;
            int left = Remaining(dungeon);
            if (left <= 0) return false;
            if (IsNewDay(PlayerPrefs.GetString(DayKey(dungeon), ""))) PlayerPrefs.SetString(DayKey(dungeon), Today);   // 시계가 되돌아가 있으면 저장된 (더 뒤) 날짜를 그대로 둔다
            PlayerPrefs.SetInt(UsedKey(dungeon), PerDay - left + 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        // ── 광고 입장: 입장 횟수를 다 쓴 뒤에만, 던전마다 하루 AdPerDay번 (같은 한국 날짜 기준) ──
        public static int AdPerDay => GameBalance.ConstInt("dungeonAdPerDay");     // 상수 시트
        static string AdDayKey(int dungeon) => $"Dungeon.{dungeon}.AdDay";
        static string AdUsedKey(int dungeon) => $"Dungeon.{dungeon}.AdUsed";

        public static int AdRemaining(int dungeon)
        {
            if (IsNewDay(PlayerPrefs.GetString(AdDayKey(dungeon), ""))) return AdPerDay;
            return Mathf.Clamp(AdPerDay - PlayerPrefs.GetInt(AdUsedKey(dungeon), 0), 0, AdPerDay);
        }

        // 광고를 보고 들어갈 수 있다: 해금됐고, 입장 횟수는 0이고, 광고 입장이 남았다
        public static bool CanEnterWithAd(int dungeon) => IsUnlocked(dungeon) && Remaining(dungeon) <= 0 && AdRemaining(dungeon) > 0;

        // 광고를 끝까지 본 뒤 하나 쓴다 (보상 콜백에서만 부른다)
        public static bool TryUseAd(int dungeon)
        {
            if (!CanEnterWithAd(dungeon)) return false;
            int left = AdRemaining(dungeon);
            if (IsNewDay(PlayerPrefs.GetString(AdDayKey(dungeon), ""))) PlayerPrefs.SetString(AdDayKey(dungeon), Today);
            PlayerPrefs.SetInt(AdUsedKey(dungeon), AdPerDay - left + 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        // 지금 이 던전의 레벨 (깨면 다음 레벨이 열린다).
        //   벽돌깨기 던전(1~3) = 그 던전의 수확 판 스테이지 (StageProgress, 던전마다 따로)
        //   보스 연속전(4) = "Dungeon.4.Level" (다 잡으면 +1)
        //   레벨업 보스(5) = 매번 레벨 1부터 시작하므로, 지금까지 잡은 가장 높은 레벨 (BestLevel)
        public static int Level(int dungeon)
        {
            switch (DungeonSession.Mode(dungeon))
            {
                case DungeonMode.BossRush: return Mathf.Max(1, PlayerPrefs.GetInt(LevelKey(dungeon), 1));
                case DungeonMode.BossLevel: return BestLevel(dungeon);
                default: return StageProgress.CurrentOf(dungeon);
            }
        }

        static string LevelKey(int dungeon) => $"Dungeon.{dungeon}.Level";
        static string BestKey(int dungeon) => $"Dungeon.{dungeon}.Best";

        // 보스 연속전을 다 깼다: 다음 레벨
        public static void LevelUp(int dungeon)
        {
            PlayerPrefs.SetInt(LevelKey(dungeon), Level(dungeon) + 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 레벨업 보스에서 잡은 가장 높은 레벨 (0 = 아직 없음)
        public static int BestLevel(int dungeon) => Mathf.Max(0, PlayerPrefs.GetInt(BestKey(dungeon), 0));

        public static void RecordBest(int dungeon, int killedLevel)
        {
            if (killedLevel <= BestLevel(dungeon)) return;
            PlayerPrefs.SetInt(BestKey(dungeon), killedLevel);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 화면이 켜진 채 00시를 넘기면 다시 그리게 한다 (DungeonResetTimer가 부른다)
        public static void NotifyNewDay() => Changed?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD   // 개발자 모드: 출시 빌드에는 컴파일되지 않는다
        // 개발자 모드(DevMode) 전용: 모든 던전의 오늘 입장·광고 입장 횟수를 되돌린다
        public static void DevResetToday()
        {
            for (int d = 1; d <= 5; d++)
            {
                PlayerPrefs.DeleteKey(UsedKey(d));
                PlayerPrefs.DeleteKey(AdUsedKey(d));
            }
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
#endif
    }
}
