using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 설정 창 "게임 종료" 버튼: 저장하고 앱을 끝낸다 (에디터에서는 플레이를 멈춘다. MainBackHandler의 종료와 같은 동작)
    [RequireComponent(typeof(Button))]
    public class QuitGameButton : MonoBehaviour
    {
        void Awake() => GetComponent<Button>().onClick.AddListener(Quit);

        static void Quit()
        {
            SaveScheduler.Flush();
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
