using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 절전 모드 (휴대폰 방치용). Main 씬에서 3분 동안 터치·키 입력이 없으면 화면을 검게 덮고
    // 카메라를 끄고 프레임을 낮춘다. 방치 전투는 그대로 돌아 골드가 쌓인다 — 덮개에 절전 중 번 골드·스테이지를 보여 준다.
    // 덮개를 누르면 해제된다 (덮개가 누름을 받으므로 뒤의 버튼은 눌리지 않는다).
    // 자기 오버레이 캔버스를 코드로 만들고 씬이 바뀌어도 남겨 둔다 (씬에 둘 것 없음)
    public class PowerSaveMode : MonoBehaviour, IPointerClickHandler
    {
        public const float IdleSeconds = 180f;
        const int SleepFrameRate = 10;
        const int SortingOrder = 31000;               // 방치 정산 팝업(30000) 위, 씬 로딩(32000) 아래
        const float RefreshEvery = 1f;                // 덮개 글자 갱신 간격

        static PowerSaveMode instance;

        GameObject cover;
        TMP_Text goldText;
        TMP_Text stageText;
        float lastInput;
        float nextRefresh;
        long startGold;
        int savedFrameRate;
        Camera[] disabledCameras = new Camera[0];

        public static bool Active => instance != null && instance.cover != null && instance.cover.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("PowerSaveMode", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;    // 카메라를 꺼도 그려진다
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;
            instance = go.AddComponent<PowerSaveMode>();
            instance.lastInput = Time.unscaledTime;
            SceneManager.sceneLoaded += (s, m) => instance.OnSceneLoaded();
        }

        void Update()
        {
            if (AnyInput()) lastInput = Time.unscaledTime;
            if (Active)
            {
                if (!Allowed()) Exit();
                else if (Time.unscaledTime >= nextRefresh) Refresh();
                return;
            }
            if (Allowed() && Time.unscaledTime - lastInput >= IdleSeconds) Enter();
        }

        // 방치 전투가 도는 Main에서만. 로딩 중·다른 씬에서는 쓰지 않는다
        static bool Allowed() => SceneManager.GetActiveScene().name == MainNavigation.MainScene && !SceneLoader.IsLoading;

        static bool AnyInput()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.scroll.ReadValue() != Vector2.zero)) return true;
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.isPressed;
        }

        void Enter()
        {
            if (cover == null) Build();
            startGold = Wallet.Gold;
            savedFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = SleepFrameRate;
            disabledCameras = Camera.allCameras;
            foreach (var c in disabledCameras) c.enabled = false;
            cover.SetActive(true);
            Refresh();
        }

        void Exit()
        {
            lastInput = Time.unscaledTime;
            if (cover == null || !cover.activeSelf) return;
            cover.SetActive(false);
            foreach (var c in disabledCameras) if (c != null) c.enabled = true;
            disabledCameras = new Camera[0];
            Application.targetFrameRate = savedFrameRate > 0 ? savedFrameRate : AppSettings.TargetFrameRate;
        }

        public void OnPointerClick(PointerEventData e) => Exit();

        // 씬이 바뀌면 해제하고, 글꼴은 새 씬 글자에서 다시 가져온다
        void OnSceneLoaded()
        {
            Exit();
            if (cover != null) ApplyFont();
        }

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + RefreshEvery;
            goldText.text = "+" + Wallet.Format(System.Math.Max(0, Wallet.Gold - startGold));
            stageText.text = $"스테이지 {IdleProgress.Stage}";
        }

        void Build()
        {
            cover = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            cover.transform.SetParent(transform, false);
            Stretch((RectTransform)cover.transform);
            cover.GetComponent<Image>().color = Color.black;   // 누름을 받는다 — 클릭은 부모인 이 컴포넌트의 OnPointerClick으로 올라온다

            NewText("Title", "절전 모드", 84f, new Color(1f, 1f, 1f, 0.9f), 260f);
            stageText = NewText("Stage", "", 52f, new Color(1f, 1f, 1f, 0.7f), 120f);
            goldText = NewText("Gold", "", 72f, new Color(1f, 0.85f, 0.4f, 1f), 0f);
            NewText("Hint", "절전 중에도 골드를 모읍니다", 40f, new Color(1f, 1f, 1f, 0.5f), -110f);
            NewText("Wake", "화면을 누르면 해제됩니다", 44f, new Color(1f, 1f, 1f, 0.6f), -700f);
            ApplyFont();
            cover.SetActive(false);
        }

        TMP_Text NewText(string name, string text, float size, Color color, float y)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(cover.transform, false);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(1000f, size * 1.5f);
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        // 한글 글꼴은 씬에 있는 글자에서 가져온다 (TMP 기본 글꼴은 한글이 없다)
        void ApplyFont()
        {
            var sample = FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Exclude);
            if (sample == null || sample.transform.IsChildOf(transform)) return;
            foreach (var t in cover.GetComponentsInChildren<TMP_Text>(true)) t.font = sample.font;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
