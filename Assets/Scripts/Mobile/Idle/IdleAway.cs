using System;
using System.Globalization;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 홈을 비운 동안의 방치형 수익 집계 + 앱 밖에 있던 시간(백그라운드·종료) 보상.
    //   - 다른 탭을 여는 동안에도 전투는 계속 돌고(IdleBattle), 그동안 번 것만 따로 센다 → 홈으로 오면 정산 팝업
    //   - 앱이 멈춰 있던 동안은 전투가 돌지 않으므로, 그동안 시간 × 최근 초당 골드로 셈해서 넣는다 (최대 OfflineCapHours)
    // 초당 골드는 이번 실행에서 실제로 번 골드 ÷ 진행 시간 (RateMinSeconds 이상 쌓이면 저장해 다음 실행에 쓴다)
    public static class IdleAway
    {
        public const double OfflineCapHours = 8;        // 앱 밖 보상 최대 시간 (임시값)
        public const double OfflineMinSeconds = 60;     // 이보다 짧게 비우면 앱 밖 보상 없음
        const double RateMinSeconds = 30;
        const string RateKey = "Idle.GoldPerSec";
        const string LastSeenKey = "Idle.LastSeen";
        public static readonly TimeSpan MaxClockSkew = TimeSpan.FromDays(1);   // 저장된 시각·날짜가 지금보다 이만큼 넘게 앞서면 틀린 값으로 본다

        public class Report
        {
            public long gold, mycelite, offlineGold;
            public int kills, stages;
            public double seconds, offlineSeconds;
            public bool IsEmpty => gold <= 0 && mycelite <= 0 && offlineGold <= 0;
        }

        static Report current;
        static double sessionGold, sessionSeconds;

        public static bool Away => current != null;

        // 홈을 떠날 때
        public static void Begin()
        {
            if (current == null) current = new Report();
        }

        // 홈으로 돌아올 때: 그동안의 집계를 돌려주고 비운다 (떠난 적이 없으면 null)
        public static Report End()
        {
            var r = current;
            current = null;
            return r;
        }

        // 전투 진행 시간 (홈이든 아니든). 비운 동안이면 그 시간도 센다
        public static void Tick(double dt)
        {
            sessionSeconds += dt;
            if (current != null) current.seconds += dt;
        }

        public static void AddGold(long gold)
        {
            if (gold <= 0) return;
            sessionGold += gold;
            if (current != null) current.gold += gold;
        }

        public static void AddMycelite(long amount) { if (current != null) current.mycelite += amount; }
        public static void AddKill() { if (current != null) current.kills++; }
        public static void AddStage() { if (current != null) current.stages++; }

        public static double GoldPerSecond
        {
            get
            {
                if (sessionSeconds >= RateMinSeconds) return sessionGold / sessionSeconds;
                // 기기 언어가 바뀌어도 같은 값으로 읽히게 InvariantCulture로 저장·읽기
                return double.TryParse(PlayerPrefs.GetString(RateKey, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    && !double.IsNaN(v) && !double.IsInfinity(v) && v > 0 ? v : 0;
            }
        }

        // 지금 시각(과 초당 골드)을 남긴다 (앱이 멈출 때·주기적으로). 다음에 앱으로 돌아오면 이 시각부터 셈한다.
        // 기기 시계를 되돌려도 남긴 시각은 뒤로 가지 않는다 — 되돌렸다 다시 맞추는 것으로 앱 밖 보상을 거듭 받지 못하게.
        // 다만 MaxClockSkew보다 앞선 값은 한때 시계가 크게 틀렸던 것으로 보고 버린다 (안 그러면 그 날짜까지 앱 밖 보상이 막힌다)
        public static void MarkSeen()
        {
            long now = DateTime.UtcNow.Ticks;
            if (TryLastSeen(out var seen) && seen > now && seen - now <= MaxClockSkew.Ticks) now = seen;
            PlayerPrefs.SetString(LastSeenKey, now.ToString(CultureInfo.InvariantCulture));
            if (sessionSeconds >= RateMinSeconds) PlayerPrefs.SetString(RateKey, GoldPerSecond.ToString("R", CultureInfo.InvariantCulture));
            SaveScheduler.MarkDirty();
        }

        static bool TryLastSeen(out long ticks) =>
            long.TryParse(PlayerPrefs.GetString(LastSeenKey, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)
            && ticks > 0 && ticks <= DateTime.MaxValue.Ticks;

        // 앱으로 돌아왔을 때(시작·재개): 비운 시간만큼 골드를 넣고 집계에 더한다. 넣은 골드를 돌려준다
        public static long ApplyOffline()
        {
            if (!TryLastSeen(out var ticks)) { MarkSeen(); return 0; }
            double seconds = (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
            MarkSeen();
            if (seconds < OfflineMinSeconds) return 0;      // 시계를 되돌린 경우(음수)도 여기서 걸러진다
            seconds = Math.Min(seconds, OfflineCapHours * 3600);
            double raw = GoldPerSecond * seconds;
            if (double.IsNaN(raw) || raw < 1) return 0;
            long gold = (long)Math.Min(raw, 9e18);
            if (gold <= 0) return 0;
            Wallet.Add(Currency.Gold, gold);
            Begin();
            current.offlineGold += gold;
            current.offlineSeconds += seconds;
            return gold;
        }
    }
}
