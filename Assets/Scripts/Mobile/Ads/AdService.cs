using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace MoreMush.Mobile
{
    // AdMob 리워드 광고. UI를 모른다 — 화면은 Initialize/ShowRewarded만 부르고, Status로 진행 상황을 받아 쓴다.
    // 광고 단위 ID는 구글 공식 테스트 ID다. 출시 빌드 전에 실제 ID로 바꿀 것 (테스트 ID가 아닌 걸로 개발 중에 눌러보면 계정이 정지될 수 있다).
    // 리워드 광고는 한 번 보여주면 못 쓰므로, 닫히거나 실패하면 버리고 다음 것을 미리 불러 둔다.
    public static class AdService
    {
#if UNITY_ANDROID
        const string RewardedUnitId = "ca-app-pub-3940256099942544/5224354917"   // 테스트 광고. 출시 때 실제 ID ca-app-pub-3609467797467871/2013896497 로 바꾼다;
#elif UNITY_IPHONE
        const string RewardedUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
        const string RewardedUnitId = "unused";
#endif

        // 구글 리워드 광고는 불러오고 약 1시간이 지나면 못 쓴다. 광고 상자는 한 시간 넘게 뒤에 뜨므로 오래된 광고는 버리고 새로 불러온다
        const float MaxAdAgeSeconds = 50f * 60f;

        // 로드가 실패하면(오프라인·광고 없음) 바로 다시 요청하지 않고 15초 → 30초 → … 최대 5분 기다린다 (요청을 쏟으면 계정이 제한될 수 있다)
        const float FirstRetrySeconds = 15f, MaxRetrySeconds = 300f;

        static bool initialized;
        static bool sdkReady;       // MobileAds.Initialize 콜백이 온 뒤에만 불러온다 (그 전 요청은 첫 실행에서 실패한다)
        static bool loading;
        static float retryDelay;
        static float nextLoadAt;    // Time.realtimeSinceStartup
        static RewardedAd rewardedAd;
        static float loadedAt;      // Time.realtimeSinceStartup

        // 진행 상황 문구. 화면에 보여 주거나 로그로 쓴다
        public static event Action<string> Status;

        public static bool IsReady => rewardedAd != null && !IsStale() && rewardedAd.CanShowAd();

        static bool IsStale() => Time.realtimeSinceStartup - loadedAt > MaxAdAgeSeconds;

        // 못 쓰게 된(만료·표시 불가) 광고를 버린다. 새로 불러오는 것은 LoadRewarded가 한다
        static void DropDeadAd()
        {
            if (rewardedAd == null || (!IsStale() && rewardedAd.CanShowAd())) return;
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        // 화면이 광고를 곧 쓸 때(광고 상자가 나올 때) 부른다: 죽은 광고는 버리고, 없으면 불러온다
        public static void Prepare()
        {
            DropDeadAd();
            LoadRewarded();
        }

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            // 광고 콜백을 메인 스레드로 받는다 (UI·데이터를 바로 만져도 안전)
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(_ =>
            {
                sdkReady = true;
                Report("SDK 초기화 완료");
                LoadRewarded();
            });
        }

        public static void LoadRewarded()
        {
            DropDeadAd();
            if (!sdkReady || loading || rewardedAd != null) return;
            if (Time.realtimeSinceStartup < nextLoadAt) return;     // 실패 뒤 기다리는 중
            loading = true;
            Report("광고 불러오는 중...");

            RewardedAd.Load(RewardedUnitId, new AdRequest(), (ad, error) =>
            {
                loading = false;
                if (error != null || ad == null)
                {
                    retryDelay = retryDelay <= 0f ? FirstRetrySeconds : Mathf.Min(retryDelay * 2f, MaxRetrySeconds);
                    nextLoadAt = Time.realtimeSinceStartup + retryDelay;
                    Report($"광고 로드 실패: {error?.GetMessage()}");
                    return;
                }
                retryDelay = 0f;
                rewardedAd = ad;
                loadedAt = Time.realtimeSinceStartup;
                Report("광고 준비됨");
            });
        }

        // onReward: 끝까지 시청해 보상을 받을 자격이 생겼을 때 한 번만 불린다. 보상 지급은 여기서만 한다.
        // 준비가 안 됐으면 false를 돌려주고 다음 광고를 불러 둔다
        public static bool ShowRewarded(Action onReward) => ShowRewarded(AdPlacement.Other, onReward);

        // placement = 어느 배치의 광고인지 (끝까지 본 수를 배치별로 센다, AdStats)
        public static bool ShowRewarded(string placement, Action onReward)
        {
            if (!IsReady)
            {
                Report("아직 광고가 준비되지 않았습니다");
                LoadRewarded();
                return false;
            }

            var ad = rewardedAd;
            rewardedAd = null;   // 한 번 쓰면 끝. 닫힌 뒤 새로 불러온다

            ad.OnAdFullScreenContentClosed += () => Release(ad, "광고 닫힘");
            ad.OnAdFullScreenContentFailed += error => Release(ad, $"광고 표시 실패: {error?.GetMessage()}");
            ad.Show(_ =>
            {
                // 광고를 끝까지 본 사람의 보상이 다른 처리의 예외에 막히지 않게 보상을 먼저, 나머지는 따로 지킨다
                try { onReward?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                try { DailyQuests.Add(QuestKind.Ad); }
                catch (Exception e) { Debug.LogException(e); }
                try { AdStats.Record(placement); }
                catch (Exception e) { Debug.LogException(e); }
                Report("보상 지급");
            });
            return true;
        }

        static void Release(RewardedAd ad, string message)
        {
            ad.Destroy();
            Report(message);
            LoadRewarded();
        }

        // 듣는 화면 하나가 예외를 던져도 다른 화면과 광고 흐름(다음 광고 불러오기 등)은 계속되게 하나씩 부른다
        static void Report(string message)
        {
            Debug.Log($"[AdService] {message}");
            var handlers = Status;
            if (handlers == null) return;
            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                try { handler(message); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

    // 광고 배치 이름 (AdStats 키·분석 이벤트 이름). 새 배치는 여기에 더한다
    public static class AdPlacement
    {
        public const string AdBox = "adbox";            // 홈 화면 광고 상자
        public const string OfflineDouble = "offline2x"; // 앱 밖 정산 2배
        public const string Gacha = "gacha";            // 광고 뽑기
        public const string Dungeon = "dungeon";        // 던전 광고 입장
        public const string Other = "other";
        public static readonly string[] All = { AdBox, OfflineDouble, Gacha, Dungeon, Other };
    }

    // 광고 시청 지표: 끝까지 본 광고 수를 배치별로 오늘(한국 날짜)·누적으로 센다. 밸런스(유저가 광고를 얼마나 보나)와 광고 BM 확인용.
    // 분석 SDK를 붙이면 Watched 이벤트에서 보낸다 (지금은 기기 안에만 남는다). PlayerPrefs("AdStats.Day", "AdStats.Today.<배치>", "AdStats.Total.<배치>", "AdStats.Days" = 광고를 하나라도 본 날 수)
    public static class AdStats
    {
        const string DayKey = "AdStats.Day";
        const string DaysKey = "AdStats.Days";
        static string TodayKey(string placement) => "AdStats.Today." + placement;
        static string TotalKey(string placement) => "AdStats.Total." + placement;

        // (배치, 그 배치의 오늘 시청 수)
        public static event Action<string, int> Watched;

        public static void Record(string placement)
        {
            if (string.IsNullOrEmpty(placement)) placement = AdPlacement.Other;
            RollDay(true);
            int today = PlayerPrefs.GetInt(TodayKey(placement), 0) + 1;
            PlayerPrefs.SetInt(TodayKey(placement), today);
            PlayerPrefs.SetInt(TotalKey(placement), PlayerPrefs.GetInt(TotalKey(placement), 0) + 1);
            SaveScheduler.MarkDirty();
            Watched?.Invoke(placement, today);
        }

        public static int Today(string placement)
        {
            RollDay(false);
            return PlayerPrefs.GetInt(TodayKey(placement), 0);
        }

        public static int TodayAll
        {
            get
            {
                int sum = 0;
                foreach (var p in AdPlacement.All) sum += Today(p);
                return sum;
            }
        }

        public static int Total(string placement) => PlayerPrefs.GetInt(TotalKey(placement), 0);

        // 광고를 본 날 하루 평균 시청 수 (본 날이 없으면 0)
        public static double PerWatchDay
        {
            get
            {
                int days = PlayerPrefs.GetInt(DaysKey, 0);
                if (days <= 0) return 0;
                long sum = 0;
                foreach (var p in AdPlacement.All) sum += Total(p);
                return (double)sum / days;
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("MoreMush/광고 시청 지표")]
        static void Print()
        {
            var sb = new System.Text.StringBuilder("[AdStats] 배치: 오늘 / 누적\n");
            foreach (var p in AdPlacement.All) sb.Append($"  {p}: {Today(p)} / {Total(p)}\n");
            sb.Append($"  광고 본 날 {PlayerPrefs.GetInt(DaysKey, 0)}일, 그날 평균 {PerWatchDay:0.#}회");
            Debug.Log(sb.ToString());
        }
#endif

        // 날이 바뀌었으면 오늘 수를 비운다. watching이면 오늘을 광고 본 날로 센다 (하루 한 번)
        static void RollDay(bool watching)
        {
            string stored = PlayerPrefs.GetString(DayKey, "");
            bool newDay = DungeonTickets.IsNewDay(stored);
            if (newDay)
            {
                if (!watching && stored.Length == 0) return;    // 아직 한 번도 안 봤다
                foreach (var p in AdPlacement.All) PlayerPrefs.DeleteKey(TodayKey(p));
                if (!watching) { PlayerPrefs.SetString(DayKey, ""); return; }
                PlayerPrefs.SetString(DayKey, DungeonTickets.Today);
                PlayerPrefs.SetInt(DaysKey, PlayerPrefs.GetInt(DaysKey, 0) + 1);
            }
        }
    }
}
