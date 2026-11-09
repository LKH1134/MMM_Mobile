using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 광고 상자 (레전드 오브 슬라임식): 일정 시간(adBoxIntervalHours)마다 홈 화면 왼쪽에서 상자가 날아와 두 자리 사이를 돌아다니다(adBoxStaySeconds) 오른쪽으로 빠지고,
    // 누르면 "광고 보기" 창이 뜬다. 리워드 광고를 끝까지 보면 골드와 균사석을 준다 (스테이지가 오를수록 커진다).
    //   - 시간은 유저가 Main 홈 화면을 보고 있을 때만 간다: 절전 모드·앱을 내린 동안·다른 탭·팝업·다른 씬에서는 멈춘다.
    //     상자가 떠 있는 동안 홈을 떠나면 상자는 숨고 머무는 시간도 멈춘다.
    //   - 상자를 놓치거나(머무는 시간 끝) 광고를 보면 다음 상자까지 처음부터 센다 (쿨타임). 센 시간은 저장한다.
    // Main 씬에 배치한다: 상자(Chest)는 홈 전투 영역에서 HUD 바로 뒤, "광고 보기" 창(AdBoxPopup 프리팹)은 캔버스에 꺼 둔다.
    // 쿨타임·떠 있는 상자는 씬을 오가도 이어지게 정적 값으로 둔다. 수치는 상수 시트의 adBox* 다
    public class AdBox : MonoBehaviour
    {
        const string ElapsedKey = "AdBox.Elapsed";      // 쿨타임을 센 초 (홈 화면을 본 시간)
        const float MaxStep = 0.5f;                     // 한 프레임이 길어도 이만큼만 센다 (화면 멈춤 때문에 시간이 튀지 않게)
        const float SaveEvery = 1f;
        const float EnterSeconds = 0.8f, LeaveSeconds = 0.8f;
        // 상자가 돌아다니는 길: 전투 화면 위쪽의 빈 풀밭 두 자리(왼쪽 의뢰 버튼 옆 · 오른쪽 전투력 옆)를 오간다.
        // 캔버스 중심에서 폭·높이의 비율 — 두 자리는 중심 기준 ±WanderHalfSpan, 높이는 TargetY (화면 위에서 약 25%). [2026-11-09] 한 자리에 머물던 것을 두 자리 배회로 바꿈
        const float WanderHalfSpan = 0.25f, TargetY = 0.252f;
        const float WanderPeriod = 14f;                  // 왼쪽→오른쪽→왼쪽 한 바퀴 초 (느리게)
        const float BobHeight = 18f, BobSpeed = 2.4f;
        const float EditorIntervalSeconds = 2f;         // 유니티 에디터에서는 표의 간격 대신 바로 나온다 (테스트용)

        static AdBox instance;

        enum State { Waiting, Present }
        enum Reward { Gold, Mycelite }      // 상자 한 개는 보상을 한 가지만 준다: 동전 그림이면 골드, 균사석 그림이면 균사석

        [SerializeField] RectTransform chest;       // 상자 버튼 (홈 전투 영역, HUD 바로 뒤. 꺼 둔다)
        [SerializeField] Image chestImage;
        [SerializeField] Sprite goldChest, myceliteChest;   // 동전 그림 = 골드, 균사석 그림 = 균사석
        [SerializeField] AdBoxView popupView;        // "광고 보기" 창 (꺼 둔다)

        AdBoxPopup popup;
        // 씬을 오가도 이어지는 값 (상자가 떠 있는 채 다른 씬에 갔다 와도 그대로)
        static State state = State.Waiting;
        static Reward reward = Reward.Gold;    // 이번 상자의 보상 (나올 때 정한다)
        static float elapsed = -1f;     // 쿨타임을 센 초 (-1 = 아직 안 읽음)
        static float stayLeft;          // 상자가 머물 남은 초
        static float age;               // 상자가 나온 뒤 홈에서 보낸 초 (날아오는 연출·흔들림)
        static bool rewardGiven;
        float saveIn;

        // 설정은 처음 쓸 때 읽는다
        static float intervalSeconds, staySeconds;
        static long goldKills;
        static long AdMycelite => GameBalance.ConstInt("adBoxMycelite");     // 상자 광고 균사석: 스테이지와 상관없이 고정 (상수 시트)
        static bool configured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            state = State.Waiting; reward = Reward.Gold; elapsed = -1f; stayLeft = age = 0f; rewardGiven = false; configured = false;
            lastReward = Reward.Mycelite; sameInRow = 0;
        }

        void Awake()
        {
            instance = this;
            if (elapsed < 0f) elapsed = Mathf.Max(0f, PlayerPrefs.GetFloat(ElapsedKey, 0f));
            if (popupView != null) popup = new AdBoxPopup(popupView);
            if (chest != null)
            {
                chest.GetComponent<Button>().onClick.AddListener(OpenPopup);
                chest.gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            AdService.Status += OnAdStatus;
            AdService.Initialize();        // 상자를 누를 때 광고가 준비돼 있게 미리 불러 둔다
        }

        void OnDisable()
        {
            AdService.Status -= OnAdStatus;
            if (popup != null && popup.IsOpen) popup.Close();   // 창이 열린 채 씬이 바뀌면 PopupGate가 잡힌 채 남지 않게
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // 광고 진행 상황(불러오는 중·실패·닫힘)을 열려 있는 창에 보여 준다
        void OnAdStatus(string message)
        {
            if (popup == null || !popup.IsOpen) return;
            popup.SetStatus(message);
            popup.SetWatchEnabled(true);        // 광고가 닫히거나 실패하면 다시 누를 수 있다
        }

        // 데이터를 지웠을 때: 센 시간과 떠 있는 상자를 버린다
        public static void ResetTimer()
        {
            elapsed = 0f;
            state = State.Waiting;
            if (instance != null) instance.HideChest();
        }

        // 표 값이 없거나 틀려도 상자가 프레임마다 뜨고 사라지며 광고를 계속 요청하지 않게 기본값·최소값을 둔다
        const float DefaultIntervalSeconds = 3600f, MinIntervalSeconds = 60f, DefaultStaySeconds = 60f, MinStaySeconds = 5f;

        static void Configure()
        {
            configured = true;
            intervalSeconds = DefaultIntervalSeconds;
            staySeconds = DefaultStaySeconds;
            try
            {
                intervalSeconds = Mathf.Max(MinIntervalSeconds, (float)(GameBalance.Const("adBoxIntervalHours") * 3600.0));
                staySeconds = Mathf.Max(MinStaySeconds, (float)GameBalance.Const("adBoxStaySeconds"));
                goldKills = (long)GameBalance.Const("adBoxGoldKills");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
#if UNITY_EDITOR
            intervalSeconds = EditorIntervalSeconds;
#endif
        }

        // 유저가 Main 홈 화면을 보고 있다: 탭·팝업이 없고, 절전·로딩·백그라운드가 아니다
        bool AtHome()
        {
#if UNITY_EDITOR
            const bool focused = true;      // 에디터에서는 게임 창을 안 눌러도 센다
#else
            bool focused = Application.isFocused;
#endif
            if (SceneLoader.IsLoading || PowerSaveMode.Active || !focused) return false;
            if (PopupGate.IsOpen) return false;
            var bar = TabGroup.MainBar;
            return bar != null && bar.Current < 0;
        }

        void Update()
        {
            if (!configured) Configure();
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            bool home = AtHome();
            if (state == State.Waiting)
            {
                if (!home) return;
                elapsed += dt;
                SaveElapsed(dt);
                if (elapsed >= intervalSeconds) Spawn();
                return;
            }

            // 상자가 떠 있다: 홈을 떠나면 숨기고 시간도 멈춘다 (내 "광고 보기" 창이 열려 있는 동안은 그대로 둔다)
            bool popupOpen = popup != null && popup.IsOpen;
            if (home) ApplyChestIcon();
            if (chest != null && chest.gameObject.activeSelf != (home || popupOpen)) chest.gameObject.SetActive(home || popupOpen);
            if (!home) return;
            age += dt;
            stayLeft -= dt;
            if (stayLeft <= 0f) { Despawn(); return; }
            Place();
        }

        void Spawn()
        {
            reward = NextReward();
#if UNITY_EDITOR
            Debug.Log($"[AdBox] 상자 종류: {reward}");      // 에디터에서 뜨는 종류가 어떻게 나오는지 콘솔로 확인
#endif
            ApplyChestIcon();
            state = State.Present;
            stayLeft = staySeconds;
            age = 0f;
            rewardGiven = false;
            chest.gameObject.SetActive(true);
            Place();
            AdService.Prepare();        // 누를 때 쓸 광고를 미리 새로 준비한다 (오래된 것은 버린다)
        }

        // 상자 쿨타임을 처음부터 다시 센다 (놓쳤거나 광고를 봤다)
        void Despawn()
        {
            HideChest();
            state = State.Waiting;
            elapsed = 0f;
            PlayerPrefs.SetFloat(ElapsedKey, 0f);
            SaveScheduler.MarkDirty();
        }

        void HideChest()
        {
            if (chest != null) chest.gameObject.SetActive(false);
        }

        void SaveElapsed(float dt)
        {
            saveIn -= dt;
            if (saveIn > 0f) return;
            saveIn = SaveEvery;
            PlayerPrefs.SetFloat(ElapsedKey, elapsed);
            SaveScheduler.MarkDirty();
        }

        // 왼쪽 바깥에서 날아와 두 자리 사이를 천천히 오가며 흔들리고, 머무는 시간이 끝나면 오른쪽으로 빠진다
        void Place()
        {
            if (chest == null) return;
            var area = ((RectTransform)chest.parent).rect;      // 상자가 걸린 판(홈 화면 전투 영역) 기준
            float w = area.width, h = area.height;
            float size = chest.rect.width;
            float outLeft = -(w * 0.5f + size), outRight = w * 0.5f + size;
            float y = h * TargetY;
            float x;
            if (age < EnterSeconds) x = Mathf.Lerp(outLeft, WanderX(w, 0f), EaseOut(age / EnterSeconds));
            else if (stayLeft < LeaveSeconds) x = Mathf.Lerp(WanderX(w, staySeconds - LeaveSeconds - EnterSeconds), outRight, EaseIn(1f - stayLeft / LeaveSeconds));
            else x = WanderX(w, age - EnterSeconds);
            float bob = Mathf.Sin(age * BobSpeed) * BobHeight;
            chest.anchoredPosition = new Vector2(x, y + bob);
        }

        // 왼쪽 자리에서 시작해 두 자리 사이를 천천히 오간다 (t = 날아온 뒤 흐른 초)
        static float WanderX(float width, float t) => -Mathf.Cos(Mathf.Max(0f, t) * Mathf.PI * 2f / WanderPeriod) * width * WanderHalfSpan;

        static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        static float EaseIn(float t) => Mathf.Pow(Mathf.Clamp01(t), 3f);

        // 다음 상자의 종류: 50 대 50으로 뽑되, 같은 종류가 3번 연속 나오지 않게 한다 (운이 몰려 한쪽만 나오는 느낌을 막는다)
        const int MaxSameInRow = 2;
        static Reward lastReward = Reward.Mycelite;
        static int sameInRow;

        static Reward NextReward()
        {
            Reward pick = Random.value < 0.5f ? Reward.Gold : Reward.Mycelite;
            if (sameInRow >= MaxSameInRow && pick == lastReward) pick = lastReward == Reward.Gold ? Reward.Mycelite : Reward.Gold;
            sameInRow = pick == lastReward ? sameInRow + 1 : 1;
            lastReward = pick;
            return pick;
        }

        // 보상 종류에 맞는 그림: 골드는 동전, 균사석은 동전 자리에 균사석이 붙은 그림
        void ApplyChestIcon()
        {
            var sprite = reward == Reward.Mycelite ? myceliteChest : goldChest;
            if (chestImage != null && sprite != null && chestImage.sprite != sprite) chestImage.sprite = sprite;
        }

        void OpenPopup()
        {
            if (state != State.Present || popup == null || popup.IsOpen) return;
            popupView.transform.SetAsLastSibling();
            var stage = IdleProgress.Stage;
            // 이 상자의 종류만 준다 (다른 쪽은 0 → 창에서도 숨는다)
            long gold = reward == Reward.Gold ? ClampLong((double)IdleProgress.EnemyGold(stage) * goldKills) : 0L;
            long mycelite = reward == Reward.Mycelite ? AdMycelite : 0L;
            PopupGate.Opened(this, () => popup.Close());      // 뒤로가기·다른 팝업이 열리면 닫힌다
            popup.Open(gold, mycelite, () => Watch(gold, mycelite), () => PopupGate.Closed(this), Despawn);     // 취소 = 상자도 사라진다 (쿨타임 처음부터)
        }

        void Watch(long gold, long mycelite)
        {
            popup.SetWatchEnabled(false);       // 광고 창이 뜨기 전에 또 누르지 않게
            bool started = AdService.ShowRewarded(AdPlacement.AdBox, () =>
            {
                if (rewardGiven || state != State.Present) return;      // 보상은 한 번만 (데이터를 지워 상자가 사라졌으면 주지 않는다)
                rewardGiven = true;
                if (gold > 0) Wallet.Add(Currency.Gold, gold);
                if (mycelite > 0) Wallet.Add(Currency.Mycelite, mycelite);
                popup.Close();
                Despawn();
            });
            if (!started)
            {
                popup.SetStatus(Loc.T("ad.loading"));
                popup.SetWatchEnabled(true);
            }
        }

        static long ClampLong(double v) => v >= 9e18 ? 9000000000000000000L : (long)System.Math.Max(0, v);
    }
}
