using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 버튼 하나 = 씬 하나. 버튼에 붙이고 인스펙터에 씬 이름만 넣는다 (예: 스킬트리 버튼 → SkillTree).
    // 씬은 빌드 설정(File > Build Profiles)에 들어 있어야 한다
    [RequireComponent(typeof(Button))]
    public class SceneButton : MonoBehaviour
    {
        [SerializeField] string scene = "";

        void Awake() => GetComponent<Button>().onClick.AddListener(Go);

        public void Go()
        {
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else Debug.LogWarning($"[SceneButton] 빌드 설정에 없는 씬: {scene}");
        }
    }
}
