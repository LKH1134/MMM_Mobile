using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 씬 전환 + 로딩 팝업. 화면을 어둡게 덮고 가운데에 나이테 아이콘을 돌리면서 다음 씬을 뒤에서 읽는다.
    // 자기 오버레이 캔버스를 코드로 만들고 씬이 바뀌어도 남겨 둔다 (씬에 둘 것 없음). 덮는 동안 터치는 막는다
    public class SceneLoader : MonoBehaviour
    {
        const int SortingOrder = 32000;               // 방치 정산 팝업(30000)보다 위
        const string SpinnerSprite = "Art/Dream/UI/Window/ic_ring";
        const float SpinSpeed = 300f;                 // 초당 도는 각도
        const float MinShow = 0.3f;                   // 로딩이 빨라도 이만큼은 보여 준다 (깜빡임 방지)
        const float Fade = 0.15f;

        static SceneLoader instance;

        CanvasGroup group;
        RectTransform spinner;
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
            if (instance == null) instance = Create();
            if (instance.loading) return false;
            instance.gameObject.SetActive(true);      // 꺼진 오브젝트에선 코루틴을 시작할 수 없다
            instance.StartCoroutine(instance.Run(scene));
            return true;
        }

        static SceneLoader Create()
        {
            var go = new GameObject("SceneLoader", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;
            var loader = go.AddComponent<SceneLoader>();
            loader.Build();
            go.SetActive(false);
            return loader;
        }

        void Build()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(transform, false);
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true;                       // 로딩 중에는 뒤를 누르지 못하게 막는다
            var rt = dim.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var icon = new GameObject("Spinner", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(transform, false);
            icon.sprite = Resources.Load<Sprite>(SpinnerSprite);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            spinner = icon.rectTransform;
            spinner.sizeDelta = new Vector2(220f, 220f);
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
