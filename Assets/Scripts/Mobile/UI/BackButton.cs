using UnityEngine;
using UnityEngine.InputSystem;

namespace MoreMush.Mobile
{
    // 안드로이드 뒤로가기(버튼·제스처, 에디터에서는 Esc). 게임 시작 때 스스로 하나 만들어 씬이 바뀌어도 남는다.
    // 순서: 열린 팝업이 있으면 닫는다(PopupGate) → 없으면 지금 씬이 등록한 처리(SceneHandler) → 그것도 없으면 아무것도 안 한다.
    // 씬 처리: Main = 탭이 열려 있으면 홈, 홈이면 종료 확인(MainBackHandler) / InGame = 설정 팝업 열기(StageHud)
    public class BackButton : MonoBehaviour
    {
        // 지금 씬의 뒤로가기 처리. 그 씬의 컴포넌트가 켜질 때 넣고 꺼질 때 비운다
        public static System.Action SceneHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("BackButton");
            DontDestroyOnLoad(go);
            go.AddComponent<BackButton>();
        }

        void Update()
        {
            var kb = Keyboard.current;     // 안드로이드 뒤로가기는 입력 시스템에서 Esc 키로 들어온다
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || SceneLoader.IsLoading) return;
            if (PopupGate.HandleBack()) return;
            SceneHandler?.Invoke();
        }
    }
}
