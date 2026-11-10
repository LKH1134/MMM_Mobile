using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 가이드 퀘스트 바 (프리팹 GuideBar): 홈 화면 전투력 바로 위 한 줄. 지금 안내 목표 · 진행도 · 보상(균사석)을 보여 준다.
    // 달성하면 막대가 노랗게 차고 "받기"로 바뀌어 누르면 균사석을 받고, 잠깐 받은 양을 보여 준 뒤 다음 목표가 나온다.
    // 값은 GuideQuests에서 읽는다. 모양·자리는 프리팹에서 고친다. 홈이 아닌 탭(편성·던전·뽑기)에서는 숨긴다
    public class GuideBarView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image fill;                 // 진행 막대 (anchorMax.x = 비율)
        [SerializeField] TMP_Text label;             // 목표 문구
        [SerializeField] TMP_Text progressText;      // 3/5 (달성하면 "받기")
        [SerializeField] TMP_Text rewardText;        // +100
        [SerializeField] Image rewardIcon;

        [Header("그림 (Resources 경로)")]
        [SerializeField] string fillSprite = "UI/UI_PP_Bar_Gauge_Yellow";
        [SerializeField] string doneFillSprite = "UI/UI_PP_Bar_Gauge_Green";
        [SerializeField] string rewardIconSprite = "UI/Icon/mycelium_stone";

        const float FlashSeconds = 0.9f;     // 받은 뒤 보상 문구를 보여 주는 시간
        const float PulseSpeed = 5f, PulseSize = 0.015f;

        Sprite normalSprite, doneSprite;
        float flashLeft;
        bool done;
        CanvasGroup group;
        RectTransform rect;

        void Awake()
        {
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            normalSprite = Resources.Load<Sprite>(fillSprite);
            doneSprite = Resources.Load<Sprite>(doneFillSprite);
            if (rewardIcon != null) rewardIcon.sprite = Resources.Load<Sprite>(rewardIconSprite);
            foreach (var t in new[] { label, progressText, rewardText }) OutlineText.Apply(t);
            if (label != null) DungeonEntry.FitToWidth(label);          // 긴 문구는 줄여 한 줄에 맞춘다
            if (button != null) button.onClick.AddListener(OnClick);
        }

        void OnEnable()
        {
            GuideQuests.Changed += Refresh;
            Loc.Changed += Refresh;
            Refresh();
        }

        void Start()
        {
            if (TabGroup.MainBar == null) return;      // MainHudView처럼 Start에서 연결 (탭 묶음이 먼저 만들어진 뒤)
            TabGroup.MainBar.Changed += OnTabChanged;
            OnTabChanged(TabGroup.MainBar.Current);
        }

        void OnDisable()
        {
            GuideQuests.Changed -= Refresh;
            Loc.Changed -= Refresh;
        }

        void OnDestroy()
        {
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed -= OnTabChanged;
        }

        void OnTabChanged(int tab)
        {
            bool home = tab < 0;        // 전투력 표시와 같이 홈 전용
            group.alpha = home ? 1f : 0f;
            group.blocksRaycasts = home;
            group.interactable = home;
        }

        void OnClick()
        {
            if (flashLeft > 0f || !GuideQuests.IsDone) return;
            long reward = GuideQuests.Reward;
            if (!GuideQuests.Claim()) return;
            flashLeft = FlashSeconds;
            ShowClaimed(reward);
        }

        void ShowClaimed(long reward)
        {
            done = false;
            rect.localScale = Vector3.one;
            if (label != null) label.text = Loc.F("guide.claimed", reward);
            if (progressText != null) progressText.text = "";
            SetFill(1f, true);
        }

        void Update()
        {
            if (flashLeft > 0f)
            {
                flashLeft -= Time.unscaledDeltaTime;
                if (flashLeft <= 0f) Refresh();
            }
            rect.localScale = done ? Vector3.one * (1f + PulseSize * Mathf.Sin(Time.unscaledTime * PulseSpeed)) : Vector3.one;
        }

        void Refresh()
        {
            if (flashLeft > 0f) return;      // 받은 양을 보여 주는 동안은 그대로
            done = GuideQuests.IsDone;
            int target = GuideQuests.Target, prog = GuideQuests.Progress;
            if (label != null) label.text = GuideQuests.Describe();
            if (progressText != null) progressText.text = done ? Loc.T("guide.claim") : $"{prog:N0}/{target:N0}";
            if (rewardText != null) rewardText.text = "+" + GuideQuests.Reward.ToString("N0");
            SetFill(target <= 0 ? 0f : Mathf.Clamp01(prog / (float)target), done);
        }

        void SetFill(float k, bool isDone)
        {
            if (fill == null) return;
            fill.sprite = isDone && doneSprite != null ? doneSprite : normalSprite;
            fill.rectTransform.anchorMax = new Vector2(k, 1f);
            fill.gameObject.SetActive(k > 0f);
        }
    }
}
