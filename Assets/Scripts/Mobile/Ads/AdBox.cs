using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 광고 상자 (레전드 오브 슬라임식): 일정 시간(adBoxIntervalHours)마다 홈 화면 왼쪽에서 상자가 날아와 머물고(adBoxStaySeconds),
    // 누르면 "광고 보기" 창이 뜬다. 리워드 광고를 끝까지 보면 골드와 균사석을 준다 (스테이지가 오를수록 커진다).
    //   - 시간은 유저가 Main 홈 화면을 보고 있을 때만 간다: 절전 모드·앱을 내린 동안·다른 탭·팝업·다른 씬에서는 멈춘다.
    //     상자가 떠 있는 동안 홈을 떠나면 상자는 숨고 머무는 시간도 멈춘다.
    //   - 상자를 놓치거나(머무는 시간 끝) 광고를 보면 다음 상자까지 처음부터 센다 (쿨타임). 센 시간은 저장한다.
    // 자기 오버레이 캔버스를 코드로 만들고 씬이 바뀌어도 남겨 둔다 (씬에 둘 것 없음). 수치는 상수 시트의 adBox* 다
    public class AdBox : MonoBehaviour
    {
        const int SortingOrder = 10050;                 // Main 캔버스(10000) 위, 팝업(10100) 아래
        const string ElapsedKey = "AdBox.Elapsed";      // 쿨타임을 센 초 (홈 화면을 본 시간)
        const float MaxStep = 0.5f;                     // 한 프레임이 길어도 이만큼만 센다 (화면 멈춤 때문에 시간이 튀지 않게)
        const float SaveEvery = 1f;
        const float EnterSeconds = 0.8f, LeaveSeconds = 0.8f;
        const float ChestSize = 230f;
        const float TargetX = 0.22f, TargetY = 0.13f;   // 상자가 머무는 자리: 캔버스 중심에서 폭·높이의 비율 (오른쪽 위쪽)
        const float BobHeight = 18f, BobSpeed = 2.4f;
        const string ChestIcon = "Art/Dream/Characters/Critters/Ggomas/icon_chest";   // 임시 그림
        const float EditorIntervalSeconds = 2f;         // 유니티 에디터에서는 표의 간격 대신 바로 나온다 (테스트용)

        static AdBox instance;

        enum State { Waiting, Present }

        RectTransform canvasRect;
        RectTransform chest;
        AdBoxPopup popup;
        State state = State.Waiting;
        float elapsed;          // 쿨타임을 센 초
        float stayLeft;         // 상자가 머물 남은 초
        float age;              // 상자가 나온 뒤 홈에서 보낸 초 (날아오는 연출·흔들림)
        float saveIn;
        bool inMain;
        bool rewardGiven;

        // 설정은 처음 쓸 때 읽는다 (이 객체가 만들어질 때는 표를 아직 안 읽었을 수 있다)
        float intervalSeconds, staySeconds;
        long goldKills, myceliteBosses;
        bool configured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("AdBox", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;
            instance = go.AddComponent<AdBox>();
            instance.canvasRect = (RectTransform)go.transform;
            instance.elapsed = Mathf.Max(0f, PlayerPrefs.GetFloat(ElapsedKey, 0f));
            instance.inMain = SceneManager.GetActiveScene().name == MainNavigation.MainScene;   // 첫 씬은 이미 읽힌 뒤라 sceneLoaded가 오지 않는다
            SceneManager.sceneLoaded += (s, m) => instance.OnSceneLoaded(s, m);
            AdService.Status += instance.OnAdStatus;
            if (instance.inMain) AdService.Initialize();
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
            if (instance == null) return;
            instance.elapsed = 0f;
            instance.state = State.Waiting;
            instance.HideChest();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            popup?.Close();     // 창이 열린 채 씬이 바뀌면 PopupGate가 잡힌 채 남아 뒤로가기·다른 창이 막힌다
            inMain = scene.name == MainNavigation.MainScene;
            if (inMain) AdService.Initialize();         // 상자를 누를 때 광고가 준비돼 있게 Main에 들어오면 미리 불러 둔다
            if (!inMain) HideChest();
        }

        // 표 값이 없거나 틀려도 상자가 프레임마다 뜨고 사라지며 광고를 계속 요청하지 않게 기본값·최소값을 둔다
        const float DefaultIntervalSeconds = 3600f, MinIntervalSeconds = 60f, DefaultStaySeconds = 60f, MinStaySeconds = 5f;

        void Configure()
        {
            configured = true;
            intervalSeconds = DefaultIntervalSeconds;
            staySeconds = DefaultStaySeconds;
            try
            {
                intervalSeconds = Mathf.Max(MinIntervalSeconds, (float)(GameBalance.Const("adBoxIntervalHours") * 3600.0));
                staySeconds = Mathf.Max(MinStaySeconds, (float)GameBalance.Const("adBoxStaySeconds"));
                goldKills = (long)GameBalance.Const("adBoxGoldKills");
                myceliteBosses = (long)GameBalance.Const("adBoxMyceliteBosses");
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
            if (!inMain || SceneLoader.IsLoading || PowerSaveMode.Active || !focused) return false;
            if (PopupGate.IsOpen) return false;
            var bar = TabGroup.MainBar;
            return bar != null && bar.Current < 0;
        }

        void Update()
        {
            if (!inMain) return;
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
            if (chest != null && chest.gameObject.activeSelf != (home || popupOpen)) chest.gameObject.SetActive(home || popupOpen);
            if (!home) return;
            age += dt;
            stayLeft -= dt;
            if (stayLeft <= 0f) { Despawn(); return; }
            Place();
        }

        void Spawn()
        {
            if (chest == null) BuildChest();
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

        // 왼쪽 바깥에서 날아와 자리에 머물며 흔들리고, 떠나기 전에 왼쪽으로 빠진다
        void Place()
        {
            float w = canvasRect.rect.width, h = canvasRect.rect.height;
            float outX = -(w * 0.5f + ChestSize);
            float targetX = w * TargetX, targetY = h * TargetY;
            float x = targetX;
            if (age < EnterSeconds) x = Mathf.Lerp(outX, targetX, EaseOut(age / EnterSeconds));
            else if (stayLeft < LeaveSeconds) x = Mathf.Lerp(outX, targetX, EaseOut(stayLeft / LeaveSeconds));
            float bob = Mathf.Sin(age * BobSpeed) * BobHeight;
            chest.anchoredPosition = new Vector2(x, targetY + bob);
        }

        static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        // ── 화면 ──
        void BuildChest()
        {
            var font = BorrowFont();
            var go = new GameObject("Chest", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            chest = (RectTransform)go.transform;
            chest.anchorMin = chest.anchorMax = new Vector2(0.5f, 0.5f);
            chest.sizeDelta = new Vector2(ChestSize, ChestSize);
            var image = go.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(ChestIcon);
            image.preserveAspect = true;
            go.GetComponent<Button>().onClick.AddListener(OpenPopup);
            popup = new AdBoxPopup(transform, image.sprite, font);
        }

        void OpenPopup()
        {
            if (state != State.Present || popup == null || popup.IsOpen) return;
            var stage = IdleProgress.Stage;
            long gold = ClampLong((double)IdleProgress.EnemyGold(stage) * goldKills);
            long mycelite = ClampLong((double)IdleProgress.BossMycelite(stage) * myceliteBosses);
            PopupGate.Opened(this, () => popup.Close());      // 뒤로가기·다른 팝업이 열리면 닫힌다
            popup.Open(gold, mycelite, () => Watch(gold, mycelite), () => PopupGate.Closed(this));
        }

        void Watch(long gold, long mycelite)
        {
            popup.SetWatchEnabled(false);       // 광고 창이 뜨기 전에 또 누르지 않게
            bool started = AdService.ShowRewarded(() =>
            {
                if (rewardGiven || state != State.Present) return;      // 보상은 한 번만 (데이터를 지워 상자가 사라졌으면 주지 않는다)
                rewardGiven = true;
                Wallet.Add(Currency.Gold, gold);
                Wallet.Add(Currency.Mycelite, mycelite);
                popup.Close();
                Despawn();
            });
            if (!started)
            {
                popup.SetStatus("광고를 불러오는 중입니다. 잠시 후 다시 눌러 주세요");
                popup.SetWatchEnabled(true);
            }
        }

        static long ClampLong(double v) => v >= 9e18 ? 9000000000000000000L : (long)System.Math.Max(0, v);

        // 한글 글꼴은 씬에 있는 글자에서 가져온다 (TMP 기본 글꼴은 한글이 없다. PowerSaveMode와 같은 방식)
        TMP_FontAsset BorrowFont()
        {
            foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!t.transform.IsChildOf(transform) && t.font != null) return t.font;
            return null;
        }
    }
}
