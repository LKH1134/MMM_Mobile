using UnityEngine;

namespace MoreMush.Mobile
{
    // 안드로이드 몰입 모드: 상단 상태 바와 하단 내비게이션 바(홈·뒤로가기)를 숨긴다.
    // 화면 가장자리에서 쓸어내리면 잠깐 나타났다가 다시 숨는다 (sticky).
    // 게임 시작 시 자동으로 켜지고, 광고·앱 전환 뒤 포커스가 돌아올 때마다 다시 적용한다 — 씬에 따로 둘 필요 없다.
    public class ImmersiveMode : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject(nameof(ImmersiveMode));
            DontDestroyOnLoad(go);
            go.AddComponent<ImmersiveMode>();
            Apply();
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) Apply();
        }

        public static void Apply()
        {
            Screen.fullScreen = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return;
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() => HideBars(activity)));
            }
            catch (System.Exception e) { Debug.LogWarning("[ImmersiveMode] " + e.Message); }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void HideBars(AndroidJavaObject activity)
        {
            try
            {
                using var window = activity.Call<AndroidJavaObject>("getWindow");
                int sdk;
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");

                if (sdk >= 30)
                {
                    using var controller = window.Call<AndroidJavaObject>("getInsetsController");
                    if (controller != null)
                    {
                        using var type = new AndroidJavaClass("android.view.WindowInsets$Type");
                        controller.Call("hide", type.CallStatic<int>("statusBars") | type.CallStatic<int>("navigationBars"));
                        controller.Call("setSystemBarsBehavior", 2);   // BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
                        return;
                    }
                }

                // API 29 이하: 옛 플래그 방식
                const int LayoutStable = 0x100, LayoutHideNavigation = 0x200, LayoutFullscreen = 0x400,
                    HideNavigation = 0x2, Fullscreen = 0x4, ImmersiveSticky = 0x1000;
                using var decor = window.Call<AndroidJavaObject>("getDecorView");
                decor.Call("setSystemUiVisibility",
                    LayoutStable | LayoutHideNavigation | LayoutFullscreen | HideNavigation | Fullscreen | ImmersiveSticky);
            }
            catch (System.Exception e) { Debug.LogWarning("[ImmersiveMode] " + e.Message); }
        }
#endif
    }
}
