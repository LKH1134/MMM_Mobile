using UnityEngine;

namespace MoreMush.Mobile
{
    // Main 씬의 뒤로가기 (BackButton이 열린 팝업을 먼저 닫은 뒤): 하단 탭이 열려 있으면 홈으로, 홈이면 "게임을 종료할까요?" 확인.
    // 확인 창은 ConfirmPopup 프리팹을 처음 쓸 때 popupRoot(디자인 영역) 아래에 만든다
    public class MainBackHandler : MonoBehaviour
    {
        [SerializeField] PopupSlot confirmPopup;    // ConfirmPopup 프리팹
        [SerializeField] RectTransform popupRoot;

        void OnEnable()
        {
            BackButton.SceneHandler = HandleBack;
            DungeonSession.Confirm = ShowConfirm;
        }

        void ShowConfirm(string title, string message, System.Action yes)
        {
            Transform root = popupRoot != null ? popupRoot : transform;
            confirmPopup.Get(root).GetComponent<ConfirmPopup>().Show(title, message, yes);
        }

        void OnDisable()
        {
            if (BackButton.SceneHandler == (System.Action)HandleBack) BackButton.SceneHandler = null;
            if (DungeonSession.Confirm == (System.Action<string, string, System.Action>)ShowConfirm) DungeonSession.Confirm = null;
        }

        void HandleBack()
        {
            var bar = TabGroup.MainBar;
            if (bar != null && bar.Current >= 0) { bar.Select(MainNavigation.Home); return; }
            if (DungeonSession.GuardLeave(null)) return;      // 레이드 중: 종료 대신 "현재 보상을 받고 나가시겠습니까?"
            ShowConfirm("게임 종료", "게임을 종료할까요?", Quit);
        }

        static void Quit()
        {
            SaveScheduler.Flush();      // 메모리에만 있는 값(세계수 JSON 등)까지 저장
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
