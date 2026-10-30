using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 씬 상단 HUD. 제한 시간을 스스로 세고(일시정지 중엔 멈춤), 점수·콤보는 게임 쪽에서 SetScore/SetCombo로 넣는다.
    // 제한 시간은 스킬에 따라 달라서 HarvestGame이 SetDuration으로 넣는다.
    // startOnAwake를 끄면 게임 쪽에서 StartTimer를 부를 때(첫 투척)부터 센다.
    // 배속(1·2·4배)도 여기서 Time.timeScale로 건다 — 게임 전체(제한 시간 포함)가 같이 빨라진다.
    // 일시정지는 팝업 없이 Time.timeScale만 0으로 내린다. 옵션 팝업이 열려 있는 동안에도 멈춘다
    // (둘은 따로 센다 — 일시정지한 채로 옵션을 열었다 닫아도 계속 멈춰 있다).
    public class StageHud : MonoBehaviour
    {
        [SerializeField] float duration = 30f;        // 스테이지 제한 시간(초)
        [SerializeField] bool startOnAwake = true;    // 끄면 StartTimer를 부를 때까지 시간이 흐르지 않는다

        [SerializeField] TMP_Text earnedGoldText;     // 이번 판 점수 (골드는 판이 끝나고 전환율을 곱해 정한다)
        [SerializeField] TMP_Text timerText;
        [SerializeField] RectTransform timerFill;     // 진행 바 안쪽 채움. 앵커 x 최대값 = 남은 비율
        [SerializeField] GameObject comboRoot;        // 콤보가 2 미만이면 숨긴다
        [SerializeField] TMP_Text comboText;
        [SerializeField] TMP_Text stageText;          // 지금 스테이지 "스테이지 n" (보스 판이면 "· 보스"). 비워도 된다

        [SerializeField] Button optionButton;
        [SerializeField] PopupSlot optionPopup;   // 프리팹. 처음 열 때 이 캔버스 아래에 만든다
        [SerializeField] Button pauseButton;

        [SerializeField] Button speedButton;          // 누를 때마다 다음 배속으로 (끝에서 처음으로 돌아간다)
        [SerializeField] Image speedIcon;
        [SerializeField] TMP_Text speedText;
        [SerializeField] SpeedStep[] speeds =
        {
            new SpeedStep { scale = 1f }, new SpeedStep { scale = 2f }, new SpeedStep { scale = 4f },
        };

        [System.Serializable]
        public struct SpeedStep
        {
            public float scale;                       // Time.timeScale. 제한 시간도 같이 빨라진다
            public Sprite icon;
        }

        const string SpeedKey = "InGame.SpeedIndex";  // 다음 판에도 고른 배속을 이어 간다

        public float Remaining { get; private set; }
        public bool Paused { get; private set; }
        public bool TimerStarted { get; private set; }
        public float Speed => speeds.Length > 0 ? speeds[speedIndex].scale : 1f;
        public event System.Action TimeUp;

        long score;
        int combo;
        int shownSecond = -1;
        int speedIndex;

        void Awake()
        {
            optionPopup.Created += popup =>
            {
                popup.Opened += ApplyTimeScale;
                popup.Closed += ApplyTimeScale;
            };
            optionButton.onClick.AddListener(() => optionPopup.Get(transform).Toggle());
            pauseButton.onClick.AddListener(TogglePause);
            if (speedButton != null) speedButton.onClick.AddListener(NextSpeed);
            speedIndex = Mathf.Clamp(PlayerPrefs.GetInt(SpeedKey, 0), 0, Mathf.Max(0, speeds.Length - 1));
            ShowSpeed();
            ApplyTimeScale();
            Remaining = duration;
            TimerStarted = startOnAwake;
            SetScore(0);
            SetCombo(0);
            ShowTime();
        }

        // 뒤로가기(BackButton): 열린 팝업이 없으면 설정 팝업을 연다 (일시정지되고, 거기서 로비로 나갈 수 있다)
        void OnEnable() => BackButton.SceneHandler = OpenOption;

        void OnDisable()
        {
            if (BackButton.SceneHandler == (System.Action)OpenOption) BackButton.SceneHandler = null;
        }

        void OpenOption() => optionPopup.Get(transform).Open();

        void Update() => Tick(Time.deltaTime);

        // 측정기: 시간을 처음으로 되돌린다
        public void ResetTimer(float seconds)
        {
            TimerStarted = false;
            SetDuration(seconds);
        }

        // dt초 흐르게 (측정기도 직접 부른다)
        public void Tick(float dt)
        {
            if (!TimerStarted || Remaining <= 0f || dt <= 0f) return;
            Remaining = Mathf.Max(0f, Remaining - dt);
            ShowTime();
            if (Remaining <= 0f) TimeUp?.Invoke();
        }

        void OnDestroy()
        {
            // 씬을 떠날 때 멈춘 시간을 남기지 않는다
            Time.timeScale = 1f;
        }

        public void StartTimer() => TimerStarted = true;

        // 시간이 흐르기 전(첫 투척 전)에만 바꾼다
        public void SetDuration(float seconds)
        {
            if (TimerStarted) return;
            duration = Mathf.Max(1f, seconds);
            Remaining = duration;
            shownSecond = -1;
            ShowTime();
        }

        public void SetScore(long value)
        {
            score = value;
            earnedGoldText.text = score.ToString("N0");
        }

        public void SetStage(int stage, bool boss)
        {
            if (stageText != null) stageText.text = boss ? $"스테이지 {stage} · 보스" : $"스테이지 {stage}";
        }

        public void SetCombo(int value)
        {
            combo = value;
            comboRoot.SetActive(combo >= 2);
            comboText.text = combo.ToString();
        }

        public void TogglePause()
        {
            Paused = !Paused;
            ApplyTimeScale();
        }

        // 1 → 2 → 4 → 1 ... 순서로 돈다
        public void NextSpeed()
        {
            if (speeds.Length == 0) return;
            speedIndex = (speedIndex + 1) % speeds.Length;
            forcedSpeed = 0f;
            PlayerPrefs.SetInt(SpeedKey, speedIndex);
            SaveScheduler.MarkDirty();
            ShowSpeed();
            ApplyTimeScale();
        }

        // 벤치마크: 배속을 정해 걸고 아이콘·글자도 맞춘다. 저장하지 않는다 (다음 판엔 플레이어가 고른 배속).
        // 배속 목록에 없는 값이면 가장 가까운 단계를 보여 주고 시간 배율만 그 값으로
        public void ForceSpeed(float scale)
        {
            if (speeds.Length > 0)
            {
                int best = 0;
                for (int i = 1; i < speeds.Length; i++)
                    if (Mathf.Abs(speeds[i].scale - scale) < Mathf.Abs(speeds[best].scale - scale)) best = i;
                speedIndex = best;
                ShowSpeed();
            }
            forcedSpeed = scale;
            ApplyTimeScale();
        }

        float forcedSpeed;   // 0이 아니면 배속 단계 대신 이 값 (ForceSpeed)

        void ShowSpeed()
        {
            if (speeds.Length == 0) return;
            var step = speeds[speedIndex];
            if (speedText != null) speedText.text = step.scale.ToString("0.##") + "배속";
            if (speedIcon != null && step.icon != null) speedIcon.sprite = step.icon;
        }

        void ApplyTimeScale() => Time.timeScale = Paused || optionPopup.IsOpen ? 0f : forcedSpeed > 0f ? forcedSpeed : Speed;

        void ShowTime()
        {
            timerFill.anchorMax = new Vector2(duration > 0f ? Remaining / duration : 0f, timerFill.anchorMax.y);
            int sec = Mathf.CeilToInt(Remaining);
            if (sec == shownSecond) return;
            shownSecond = sec;
            timerText.text = sec + "초";
        }
    }
}
