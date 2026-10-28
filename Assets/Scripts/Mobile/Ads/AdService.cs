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
        const string RewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IPHONE
        const string RewardedUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
        const string RewardedUnitId = "unused";
#endif

        static bool initialized;
        static bool loading;
        static RewardedAd rewardedAd;

        // 진행 상황 문구. 화면에 보여 주거나 로그로 쓴다
        public static event Action<string> Status;

        public static bool IsReady => rewardedAd != null && rewardedAd.CanShowAd();

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            // 광고 콜백을 메인 스레드로 받는다 (UI·데이터를 바로 만져도 안전)
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(_ =>
            {
                Report("SDK 초기화 완료");
                LoadRewarded();
            });
        }

        public static void LoadRewarded()
        {
            if (!initialized || loading || rewardedAd != null) return;
            loading = true;
            Report("광고 불러오는 중...");

            RewardedAd.Load(RewardedUnitId, new AdRequest(), (ad, error) =>
            {
                loading = false;
                if (error != null || ad == null)
                {
                    Report($"광고 로드 실패: {error?.GetMessage()}");
                    return;
                }
                rewardedAd = ad;
                Report("광고 준비됨");
            });
        }

        // onReward: 끝까지 시청해 보상을 받을 자격이 생겼을 때 한 번만 불린다. 보상 지급은 여기서만 한다.
        // 준비가 안 됐으면 false를 돌려주고 다음 광고를 불러 둔다
        public static bool ShowRewarded(Action onReward)
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
                Report("보상 지급");
                onReward?.Invoke();
            });
            return true;
        }

        static void Release(RewardedAd ad, string message)
        {
            ad.Destroy();
            Report(message);
            LoadRewarded();
        }

        static void Report(string message)
        {
            Debug.Log($"[AdService] {message}");
            Status?.Invoke(message);
        }
    }
}
