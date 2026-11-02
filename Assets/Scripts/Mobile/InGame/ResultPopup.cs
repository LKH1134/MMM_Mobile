using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 결과 팝업. 시간이 다 되면 HarvestGame이 열어 이번 판에 번 골드만 보여 준다.
    // 버튼은 둘: 왼쪽(입장 횟수를 하나 쓰고 이 씬을 다시 연다) · "나가기"(Main). 닫기 버튼·배경 눌러 닫기는 없다.
    //   레벨을 깼으면 왼쪽은 "다음 레벨 (레벨 n)" — 씬이 다시 열리면 StageProgress.Current(= 던전 레벨)가 이미 다음이다. 못 깼으면 "다시 도전".
    //   입장 횟수를 다 썼으면 "광고 보고 도전 (n/2)" (광고 입장, DungeonTickets.AdPerDay), 그것도 없으면 막힌다.
    // "다음 레벨 자동 도전"(체크 상자, 기기에 저장): 켜져 있고, 레벨을 깼고, 입장 횟수가 남았으면 AutoNextSeconds초를 센 뒤 다음 레벨로 들어간다.
    //   광고 입장은 자동으로 하지 않는다. 버튼을 누르거나 체크를 끄면 카운트를 멈춘다. 체크 상자는 프리팹을 고치지 않게 처음 열 때 만든다
    [RequireComponent(typeof(UiPopup))]
    public class ResultPopup : MonoBehaviour
    {
        [SerializeField] TMP_Text earnedText;
        [SerializeField] Button retryButton;
        [SerializeField] Button mainButton;
        [SerializeField] string mainScene = MainNavigation.MainScene;
        [SerializeField] float countUpDuration = 0.8f;   // 번 골드가 0부터 올라가는 시간
        [SerializeField] int dungeon = 1;                // 이 씬이 몇 번 던전인지 (입장 횟수, DungeonTickets)

        const string AutoNextKey = "Dungeon.AutoNext";
        static readonly Vector2 AutoRowPos = new Vector2(0f, 250f);     // 창 아래 가운데 기준 (버튼 바로 위)
        static readonly Vector2 AutoRowSize = new Vector2(760f, 64f);
        const float BoxSize = 52f;
        static readonly Color BoxColor = new Color(0.12f, 0.1f, 0.16f, 0.9f);
        static readonly Color CheckColor = new Color(0.55f, 0.9f, 0.45f);

        UiPopup popup;
        Coroutine countUp, autoNext;
        TMP_Text retryLabel, autoLabel;
        Image check;
        bool cleared;

        static float AutoNextSeconds => Mathf.Max(1f, (float)GameBalance.Const("dungeonAutoNextSeconds"));   // 상수 시트

        static bool AutoNext
        {
            get => PlayerPrefs.GetInt(AutoNextKey, 0) == 1;
            set { PlayerPrefs.SetInt(AutoNextKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void Awake()
        {
            popup = GetComponent<UiPopup>();
            retryButton.onClick.AddListener(Retry);
            mainButton.onClick.AddListener(GoMain);
            retryLabel = retryButton.GetComponentInChildren<TMP_Text>(true);
            if (retryLabel != null) DungeonEntry.FitToWidth(retryLabel);   // "다음 레벨 (레벨 123)"·"광고 보고 도전 (2/2)"
            BuildAutoRow();
        }

        public void Show(long earned) => Show(earned, false);

        // cleared = 이번 판으로 레벨을 넘었는지 (HarvestGame.StageCleared)
        public void Show(long earned, bool cleared)
        {
            // 꺼진 채로 만들어져 아직 Awake 전일 수 있다. Open이 켜면서 Awake가 돈다
            if (popup == null) popup = GetComponent<UiPopup>();
            popup.Open();
            this.cleared = cleared;
            RefreshButtons();
            if (countUp != null) StopCoroutine(countUp);
            countUp = StartCoroutine(CountUp(earned));
            RestartAutoNext();
        }

        // 시간이 멈춰 있어도 돌도록 unscaled로 센다
        IEnumerator CountUp(long earned)
        {
            for (float t = 0f; t < countUpDuration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / countUpDuration, 3f);
                earnedText.text = Wallet.Format((long)(earned * k));
                yield return null;
            }
            earnedText.text = Wallet.Format(earned);
            countUp = null;
        }

        void RefreshButtons()
        {
            bool ticket = DungeonTickets.CanEnter(dungeon);
            bool byAd = DungeonTickets.CanEnterWithAd(dungeon);
            retryButton.interactable = ticket || byAd;
            if (retryLabel == null) return;
            if (!ticket && byAd) retryLabel.text = $"광고 보고 도전 ({DungeonTickets.AdRemaining(dungeon)}/{DungeonTickets.AdPerDay})";
            else retryLabel.text = cleared ? $"다음 레벨 (레벨 {DungeonTickets.Level(dungeon)})" : "다시 도전";
        }

        // ── 다음 레벨 자동 도전 ──
        bool CanAutoNext => AutoNext && cleared && DungeonTickets.CanEnter(dungeon);

        void RestartAutoNext()
        {
            StopAutoNext();
            if (CanAutoNext) autoNext = StartCoroutine(AutoNextCountdown());
            else SetAutoLabel(-1);
        }

        void StopAutoNext()
        {
            if (autoNext != null) StopCoroutine(autoNext);
            autoNext = null;
            SetAutoLabel(-1);
        }

        // 일시정지와 상관없이 실제 시간으로 센다. 남은 횟수가 그새 바뀌었으면 들어가지 않는다
        IEnumerator AutoNextCountdown()
        {
            for (float left = AutoNextSeconds; left > 0f; left -= Time.unscaledDeltaTime)
            {
                SetAutoLabel(Mathf.CeilToInt(left));
                yield return null;
            }
            autoNext = null;
            SetAutoLabel(-1);
            if (CanAutoNext) Retry();
        }

        void SetAutoLabel(int seconds)
        {
            if (autoLabel == null) return;
            autoLabel.text = seconds > 0 ? $"다음 레벨 자동 도전  <color=#FFD95A>{seconds}초 후 도전</color>" : "다음 레벨 자동 도전";
        }

        void ToggleAutoNext()
        {
            AutoNext = !AutoNext;
            if (check != null) check.enabled = AutoNext;
            RestartAutoNext();
        }

        // 버튼 바로 위 한 줄: [체크 상자] 다음 레벨 자동 도전 — 줄 전체를 눌러도 켜고 끈다
        void BuildAutoRow()
        {
            var window = retryButton.transform.parent as RectTransform;
            if (window == null || retryLabel == null) return;

            var row = new GameObject("AutoNextRow", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)row.transform;
            rt.SetParent(window, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = AutoRowPos;
            rt.sizeDelta = AutoRowSize;
            var hit = row.GetComponent<Image>();
            hit.color = Color.clear;        // 눌리는 자리만 (보이지 않음)
            row.GetComponent<Button>().onClick.AddListener(ToggleAutoNext);

            var box = NewImage("Box", rt, BoxColor, new Vector2(BoxSize, BoxSize));
            ((RectTransform)box.transform).anchoredPosition = new Vector2(-AutoRowSize.x / 2f + BoxSize / 2f, 0f);
            box.raycastTarget = false;
            check = NewImage("Check", (RectTransform)box.transform, CheckColor, new Vector2(BoxSize - 18f, BoxSize - 18f));
            check.raycastTarget = false;
            check.enabled = AutoNext;

            var labelGo = Instantiate(retryLabel.gameObject, rt, false);
            labelGo.name = "Label";
            foreach (Transform child in labelGo.transform) Destroy(child.gameObject);
            autoLabel = labelGo.GetComponent<TMP_Text>();
            autoLabel.raycastTarget = false;
            autoLabel.alignment = TextAlignmentOptions.MidlineLeft;
            autoLabel.enableAutoSizing = false;
            autoLabel.fontSize = 40f;
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(BoxSize + 20f, 0f);
            lrt.offsetMax = Vector2.zero;
            SetAutoLabel(-1);
        }

        static Image NewImage(string name, RectTransform parent, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        // 왼쪽 버튼: 입장 횟수가 있으면 하나 쓰고 이 씬을 다시 연다 (레벨을 깼으면 그게 다음 레벨), 없으면 광고 입장.
        // SceneLoader로 넘어간다 (로딩 팝업·터치 막기, 두 번 눌러도 한 번만 — 입장 횟수도 넘어갈 때만 쓴다)
        void Retry()
        {
            StopAutoNext();
            if (SceneLoader.IsLoading) return;
            string scene = gameObject.scene.name;
            if (DungeonTickets.CanEnter(dungeon))
            {
                if (SceneLoader.Load(scene)) DungeonTickets.TryUse(dungeon);
            }
            else if (DungeonTickets.CanEnterWithAd(dungeon))
            {
                if (!DungeonEntry.EnterWithAd(dungeon, scene) && retryLabel != null) retryLabel.text = "광고 준비 중…";
            }
            else retryButton.interactable = false;
        }

        void GoMain()
        {
            StopAutoNext();
            if (!SceneLoader.Load(mainScene)) Debug.LogWarning($"[ResultPopup] 씬을 열 수 없음: {mainScene}");
        }

        void OnDisable() => StopAutoNext();
    }
}
