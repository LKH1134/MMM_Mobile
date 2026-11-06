using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace MoreMush.Mobile
{
    // AdMob 리워드 광고. UI를 모른다 — 화면은 Initialize/ShowRewarded만 부르고, Status로 진행 상황을 받아 쓴다.
    // 광고 단위 ID: 안드로이드는 모든 빌드(에디터·개발·출시)에서 앱으로 신청한 실제 ID를 쓴다.
    // 실제 광고를 개발 중에 반복해서 누르면 계정이 제한될 수 있다 → 테스트 기기는 AdMob 콘솔에 테스트 기기로 등록해 둔다.
    // 리워드 광고는 한 번 보여주면 못 쓰므로, 닫히거나 실패하면 버리고 다음 것을 미리 불러 둔다.
    public static class AdService
    {
#if UNITY_ANDROID
        const string RewardedUnitId = "ca-app-pub-3609467797467871/2013896497";   // 실제 광고 (구글 테스트 ID: ca-app-pub-3940256099942544/5224354917)
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

        // 1) 광고 동의(UMP): 동의가 필요한 지역(EEA·영국·스위스 등)이면 AdMob 콘솔 "개인 정보 보호 및 메시지"에 만든 동의 양식을 띄운다.
        //    지난 실행에서 이미 동의 정보가 있으면 양식을 기다리지 않고 바로 광고 SDK를 시작한다.
        //    양식이 없거나(콘솔에 안 만듦) 오류가 나도 광고 SDK는 시작한다 — 그 지역에선 광고가 제한될 수 있다.
        // 2) 광고 SDK 시작 → 리워드 광고 불러오기
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            MobileAdsEventExecutor.Initialize();    // OnMain이 쓰는 메인 스레드 실행기 (동의 콜백이 광고 SDK 시작보다 먼저 온다)

            Consent = ConsentStep.Checking;
            ConsentInformation.Update(new ConsentRequestParameters(), updateError => OnMain(() =>
            {
                if (updateError != null) Report($"광고 동의 정보 확인 실패: {updateError.Message}");
                // 아직 동의를 받지 않은 동의 지역 사용자(= 처음 실행)면 양식이 뜬다 → 답할 때까지 기다린다
                if (updateError == null && ConsentInformation.ConsentStatus == ConsentStatus.Required) Consent = ConsentStep.FormShowing;
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() =>
                {
                    if (formError != null) Report($"광고 동의 양식 실패: {formError.Message}");
                    Consent = ConsentStep.Done;
                    StartSdk();
                }));
            }));
            if (ConsentInformation.CanRequestAds()) StartSdk();     // 지난 실행의 동의로 바로 시작 (양식 결과를 기다리지 않음)
        }

        static bool sdkStarted;

        public enum ConsentStep { NotStarted, Checking, FormShowing, Done }
        // 시작 화면(StartLoading)이 본다: 양식이 떠 있는 동안은 다음 씬으로 넘어가지 않는다
        public static ConsentStep Consent { get; private set; }

        // 광고 콜백은 SDK(안드로이드 UI 스레드)에서 온다 → OnMain으로 Unity 메인 스레드에 넘겨 처리한다
        // (예전 RaiseAdEventsOnUnityMainThread는 SDK 10.7부터 폐기 예정 — CHANGELOG 권장 방식)
        static void StartSdk()
        {
            if (sdkStarted) return;
            sdkStarted = true;
            MobileAds.Initialize(_ => OnMain(() =>
            {
                sdkReady = true;
                Report("SDK 초기화 완료");
                LoadRewarded();
            }));
        }

        // 동의 지역 사용자가 동의를 바꿀 수 있는 입구가 필요한지 (옵션 화면의 "광고 개인정보 설정" 버튼을 이때만 보인다)
        public static bool PrivacyOptionsRequired =>
            initialized && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public static void ShowPrivacyOptions() =>
            ConsentForm.ShowPrivacyOptionsForm(error => OnMain(() =>
            {
                if (error != null) Report($"광고 개인정보 설정 실패: {error.Message}");
            }));

        static void OnMain(Action action) => MobileAdsEventExecutor.ExecuteInUpdate(action);

        // 광고가 화면을 덮고 있는 동안 true (Show ~ 닫힘·표시 실패). 광고 때문에 앱이 잠깐 뒤로 간 것을 앱 밖 시간으로 셈하지 않게 쓴다
        public static bool IsShowing { get; private set; }

        public static void LoadRewarded()
        {
            DropDeadAd();
            if (!sdkReady || loading || rewardedAd != null) return;
            if (Time.realtimeSinceStartup < nextLoadAt) return;     // 실패 뒤 기다리는 중
            loading = true;
            Report("광고 불러오는 중...");

            RewardedAd.Load(RewardedUnitId, new AdRequest(), (ad, error) => OnMain(() =>
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
            }));
        }

        // onReward: 끝까지 시청해 보상을 받을 자격이 생겼을 때 한 번만 불린다. 보상 지급은 여기서만 한다.
        // 준비가 안 됐으면 false를 돌려주고 다음 광고를 불러 둔다
        public static bool ShowRewarded(Action onReward) => ShowRewarded(AdPlacement.Other, onReward);

        // placement = 어느 배치의 광고인지 (끝까지 본 수를 배치별로 센다, AdStats)
        public static bool ShowRewarded(string placement, Action onReward)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD   // 개발자 모드: 출시 빌드에는 컴파일되지 않는다
            if (DevMode.SkipAds)
            {
                // 개발자 모드: 광고 없이 끝까지 본 것으로
                Rewarded(placement, onReward);
                return true;
            }
#endif
            if (!IsReady)
            {
                Report("아직 광고가 준비되지 않았습니다");
                LoadRewarded();
                return false;
            }

            var ad = rewardedAd;
            rewardedAd = null;   // 한 번 쓰면 끝. 닫힌 뒤 새로 불러온다

            ad.OnAdFullScreenContentClosed += () => OnMain(() => Release(ad, "광고 닫힘"));
            ad.OnAdFullScreenContentFailed += error => OnMain(() => Release(ad, $"광고 표시 실패: {error?.GetMessage()}"));
            // 광고가 나오는 동안 게임 소리(BGM)를 멈춘다. 닫히거나 표시에 실패하면 Release가 다시 켠다
            AudioListener.pause = true;
            IsShowing = true;
            ad.Show(_ => OnMain(() => Rewarded(placement, onReward)));
            return true;
        }

        // 광고를 끝까지 본 사람의 보상이 다른 처리의 예외에 막히지 않게 보상을 먼저, 나머지는 따로 지킨다
        static void Rewarded(string placement, Action onReward)
        {
            try { onReward?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            try { DailyQuests.Add(QuestKind.Ad); }
            catch (Exception e) { Debug.LogException(e); }
            try { AdStats.Record(placement); }
            catch (Exception e) { Debug.LogException(e); }
            Report("보상 지급");
        }

        static void Release(RewardedAd ad, string message)
        {
            AudioListener.pause = false;
            IsShowing = false;
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

    // 광고를 끝까지 봤는데 그 순간 보상을 줄 수 없었을 때(광고 보는 사이 창이 닫힘·씬 로딩 중 등) 남기는 "광고 없이 한 번" 크레딧.
    // 다음에 같은 버튼을 누르면 광고 없이 바로 쓴다 — 본 광고의 보상이 사라지지 않게
    public static class AdCredits
    {
        static string Key(string what) => "AdCredit." + what;

        public static void Add(string what)
        {
            PlayerPrefs.SetInt(Key(what), PlayerPrefs.GetInt(Key(what), 0) + 1);
            PlayerPrefs.Save();
            Debug.Log($"[AdCredits] 광고 보상을 지금 줄 수 없어 다음 한 번으로 남겼다: {what}");
        }

        public static bool Has(string what) => PlayerPrefs.GetInt(Key(what), 0) > 0;

        public static bool TryUse(string what)
        {
            int n = PlayerPrefs.GetInt(Key(what), 0);
            if (n <= 0) return false;
            if (n == 1) PlayerPrefs.DeleteKey(Key(what)); else PlayerPrefs.SetInt(Key(what), n - 1);
            SaveScheduler.MarkDirty();
            return true;
        }
    }
}
