using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreMush.Mobile
{
    // 다른 씬(세계수 등)의 하단바에서 Main의 탭을 열 때: 열 탭 번호를 적고 Main을 로드한다.
    // Main의 하단바 TabGroup(readPendingMainTab)이 시작할 때 한 번 읽고 지운다
    public static class MainNavigation
    {
        public const string MainScene = "Main";

        // Main 하단 탭 번호 (0 편성, 1 던전, 2 뽑기). -1 = 홈 (패널 없음)
        public const int Home = -1;
        public static int PendingTab { get; private set; } = Home;

        public static void Open(int tab)
        {
            PendingTab = tab;
            if (Application.CanStreamedLevelBeLoaded(MainScene)) SceneManager.LoadScene(MainScene);
            else Debug.LogWarning($"[MainNavigation] 빌드 설정에 없는 씬: {MainScene}");
        }

        public static int TakePendingTab()
        {
            int tab = PendingTab;
            PendingTab = Home;
            return tab;
        }
    }
}
