using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MoreMush.Mobile
{
    // 절전 모드 (휴대폰 방치용). Main 씬에서 3분 동안 터치·키 입력이 없으면 화면을 검게 덮고
    // 카메라를 끄고 프레임을 낮춘다. 방치 전투는 그대로 돌아 골드가 쌓인다 — 덮개에 절전 중 번 골드·스테이지를 보여 준다.
    // 덮개를 누르면 해제된다 (덮개가 누름을 받으므로 뒤의 버튼은 눌리지 않는다).
    // Main 씬에 배치한다 (자기 오버레이 캔버스 — 카메라를 꺼도 그려진다). 덮개 모양은 씬에서 고친다
    public class PowerSaveMode : MonoBehaviour, IPointerClickHandler
    {
        public const float IdleSeconds = 180f;
        const int SleepFrameRate = 10;
        const float RefreshEvery = 1f;                // 덮개 글자 갱신 간격

        static PowerSaveMode instance;

        [SerializeField] GameObject cover;          // 검은 덮개 (꺼 둔다. 누르면 해제)
        [SerializeField] TMP_Text goldText;         // 절전 중 번 골드
        [SerializeField] TMP_Text stageText;
        float lastInput;
        float nextRefresh;
        long startGold;
        int savedFrameRate;
        Camera[] disabledCameras = new Camera[0];

        public static bool Active => instance != null && instance.cover != null && instance.cover.activeSelf;

        void Awake()
        {
            instance = this;
            lastInput = Time.unscaledTime;
            if (cover != null) cover.SetActive(false);
        }

        void OnDestroy()
        {
            Exit();
            if (instance == this) instance = null;
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
            if (Time.unscaledTime - lastInput >= IdleSeconds && Allowed()) Enter();
        }

        // 로딩 중에는 쓰지 않는다 (Main 씬에만 있다)
        static bool Allowed() => !SceneLoader.IsLoading;

        // 앱으로 돌아오면 덮개를 걷고 다시 센다 — 뒤에 있던 시간 때문에 바로 덮이거나 방치 정산 팝업을 가리지 않게
        void OnApplicationPause(bool paused) { if (!paused) Exit(); }

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
            if (cover == null) return;
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

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + RefreshEvery;
            goldText.text = "+" + Wallet.Format(System.Math.Max(0, Wallet.Gold - startGold));
            stageText.text = IdleBattle.HudStageLabel(IdleProgress.Stage);
        }
    }
}
