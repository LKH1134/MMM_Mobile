using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 버튼 하나 = 씬 하나. 버튼에 붙이고 인스펙터에 씬 이름만 넣는다 (예: 세계수 버튼 → WorldTree).
    // 씬은 빌드 설정(File > Build Profiles)에 들어 있어야 한다
    [RequireComponent(typeof(Button))]
    public class SceneButton : MonoBehaviour
    {
        [SerializeField] string scene = "";

        void Awake() => GetComponent<Button>().onClick.AddListener(Go);

        public void Go()
        {
            if (DungeonSession.GuardLeave(() => SceneLoader.Load(scene))) return;   // 레이드 중이면 "현재 보상을 받고 나가시겠습니까?" 먼저
            SceneLoader.Load(scene);
        }
    }
}
