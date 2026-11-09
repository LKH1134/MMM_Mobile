using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreMush.Mobile
{
    // 씬 전환 + 로딩 팝업. 화면을 어둡게 덮고 가운데에 나이테 아이콘을 돌리면서 다음 씬을 뒤에서 읽는다.
    // 프리팹 Prefabs/Mobile/UI/Common/SceneLoader (자기 오버레이 캔버스)를 모든 씬에 둔다. 먼저 생긴 하나가 씬이 바뀌어도 남고, 나중 것은 스스로 지운다.
    // 덮는 동안 터치는 막는다. 모양은 프리팹에서 고친다
    public class SceneLoader : MonoBehaviour
    {
        const float SpinSpeed = 300f;                 // 초당 도는 각도
        const float MinShow = 0.3f;                   // 로딩이 빨라도 이만큼은 보여 준다 (깜빡임 방지)
        const float Fade = 0.15f;

        static SceneLoader instance;

        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform spinner;
        bool loading;

        public static bool IsLoading => instance != null && instance.loading;

        // 빌드 설정에 없는 씬이면 경고만 남기고 false
        public static bool Load(string scene)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"[SceneLoader] 빌드 설정에 없는 씬: {scene}");
                return false;
            }
            if (instance == null) { Debug.LogWarning("[SceneLoader] 씬에 SceneLoader 프리팹이 없다 — 바로 넘어간다"); SceneManager.LoadScene(scene); return true; }
            if (instance.loading) return false;
            instance.gameObject.SetActive(true);      // 꺼진 오브젝트에선 코루틴을 시작할 수 없다
            instance.StartCoroutine(instance.Run(scene));
            return true;
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            group.alpha = 0f;
            gameObject.SetActive(false);
        }

        // 일시정지·배속과 상관없이 실제 시간으로 돈다
        void Update() => spinner.Rotate(0f, 0f, -SpinSpeed * Time.unscaledDeltaTime);

        IEnumerator Run(string scene)
        {
            loading = true;
            spinner.localRotation = Quaternion.identity;
            yield return FadeTo(1f);

            float shown = 0f;
            var op = SceneManager.LoadSceneAsync(scene);
            if (op == null)
            {
                // 읽기를 시작하지 못했다 — loading이 true로 남으면 다음 씬 전환이 영영 막힌다
                Debug.LogError($"[SceneLoader] 씬을 읽지 못함: {scene}");
                yield return FadeTo(0f);
                gameObject.SetActive(false);
                loading = false;
                yield break;
            }
            while (!op.isDone)
            {
                shown += Time.unscaledDeltaTime;
                yield return null;
            }
            for (; shown < MinShow; shown += Time.unscaledDeltaTime) yield return null;

            yield return FadeTo(0f);
            gameObject.SetActive(false);
            loading = false;
        }

        IEnumerator FadeTo(float target)
        {
            float from = group.alpha;
            for (float t = 0f; t < Fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, target, t / Fade);
                yield return null;
            }
            group.alpha = target;
        }
    }
}
