using System;
using System.Globalization;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 게임 시계: 지금까지 본 가장 늦은 기기 시각보다 뒤로 가지 않는다.
    // 기기 시계를 앞으로 돌려 일일 보상(던전·뽑기·퀘스트)·앱 밖 보상을 받고 되돌린 뒤 다시 받는 것을 막는다 —
    // 되돌린 동안 게임 시간은 멈춰 있고, 실제 시간이 그 시각을 따라잡아야 다음 날이 온다.
    // 앞으로만 돌리는 것(하루를 일찍 넘기기)은 서버 시간 없이는 막지 못한다.
    // 게임 규칙에 쓰는 "지금"은 DateTime.UtcNow 대신 이것을 쓴다
    public static class GameClock
    {
        const string Key = "Clock.MaxSeen";
        const long StoreEveryTicks = TimeSpan.TicksPerMinute;   // 저장은 1분 넘게 앞섰을 때만 (파일 쓰기는 SaveScheduler가 모아서)

        static long maxSeen = -1;   // -1 = 아직 안 읽음
        static long stored;

        public static long NowTicks
        {
            get
            {
                if (maxSeen < 0) Load();
                long now = DateTime.UtcNow.Ticks;
                if (now > maxSeen)
                {
                    maxSeen = now;
                    if (now - stored >= StoreEveryTicks) Store();
                }
                return maxSeen;
            }
        }

        public static DateTime UtcNow => new DateTime(NowTicks, DateTimeKind.Utc);

        static void Load()
        {
            maxSeen = long.TryParse(PlayerPrefs.GetString(Key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t)
                      && t > 0 && t <= DateTime.MaxValue.Ticks ? t : 0;
            stored = maxSeen;
        }

        static void Store()
        {
            stored = maxSeen;
            PlayerPrefs.SetString(Key, maxSeen.ToString(CultureInfo.InvariantCulture));
            SaveScheduler.MarkDirty();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            maxSeen = -1;
            stored = 0;
        }
    }
}
