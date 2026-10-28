using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 옵션 팝업의 벤치마크 버튼 (개발용). 개발 빌드·에디터에서만 보이고, 누르면 Benchmark를 시작한다
    [RequireComponent(typeof(Button))]
    public class BenchmarkButton : MonoBehaviour
    {
        void Awake()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                gameObject.SetActive(false);
                return;
            }
            GetComponent<Button>().onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                Benchmark.Start();
            });
        }
    }
}
