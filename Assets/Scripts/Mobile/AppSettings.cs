using UnityEngine;

namespace MoreMush.Mobile
{
    // 앱 전체 실행 설정. 첫 씬을 읽기 전에 한 번 건다.
    // 모바일은 targetFrameRate를 정하지 않으면 30fps로 묶인다 — 60으로 올린다 (화면 주사율보다 높게는 안 나온다)
    // 방치형이라 화면이 꺼지지 않게 한다 — 오래 손대지 않으면 PowerSaveMode가 검은 덮개·낮은 fps로 전지를 아낀다.
    // 숫자·날짜 문자열은 기기 언어와 상관없이 같게 만든다 (태국어 기기는 불기 연도로 날짜를 써서 하루 충전이 매번 다시 되고,
    // 독일어 기기는 소수점이 쉼표가 된다) — 문화권을 Invariant로 고정한다
    public static class AppSettings
    {
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void FixCulture()
        {
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = invariant;
            System.Globalization.CultureInfo.CurrentCulture = invariant;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            Application.targetFrameRate = TargetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= ResetTimeScale;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += ResetTimeScale;
        }

        // 던전의 일시정지·배속(Time.timeScale)이 남은 채 Main으로 오면 전투가 멈춘 것처럼 보인다 — Main은 늘 1배속
        static void ResetTimeScale(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single && scene.name == MainNavigation.MainScene) Time.timeScale = 1f;
        }
    }
}
