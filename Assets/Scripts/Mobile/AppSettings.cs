using UnityEngine;

namespace MoreMush.Mobile
{
    // 앱 전체 실행 설정. 첫 씬을 읽기 전에 한 번 건다.
    // 모바일은 targetFrameRate를 정하지 않으면 30fps로 묶인다 — 60으로 올린다 (화면 주사율보다 높게는 안 나온다)
    // 방치형이라 화면이 꺼지지 않게 한다 — 오래 손대지 않으면 PowerSaveMode가 검은 덮개·낮은 fps로 전지를 아낀다
    public static class AppSettings
    {
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            Application.targetFrameRate = TargetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
