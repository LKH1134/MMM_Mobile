using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 가이드 퀘스트 바 (프리팹 GuideBar): 홈 화면 전투력 바로 위 한 줄. 지금 안내 목표 · 진행도 · 보상(균사석)을 보여 준다.
    // 달성하면 막대가 노랗게 차고 "받기"로 바뀌어 누르면 균사석을 받고 바로 다음 목표가 나온다.
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

        const float PulseSpeed = 5f, PulseSize = 0.015f;

        Sprite normalSprite, doneSprite;
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
            Loc.Changed += OnLanguageChanged;
            textDirty = true;
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
            Loc.Changed -= OnLanguageChanged;
        }

        void OnDestroy()
        {
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed -= OnTabChanged;
        }

        bool visible = true;            // 홈 탭이라 보이는 중 (안 보이면 박동하지 않는다)

        // 처치마다 Refresh가 불린다 → 값이 바뀐 글자만 다시 만든다 (모바일: 문자열 할당·TMP 메시 재생성 줄이기)
        bool textDirty = true;          // 언어가 바뀌었거나 받은 양을 보여 준 뒤 — 전부 다시 쓴다
        GuideKind shownKind; IdleStat shownStat;
        int shownTarget = -1, shownProg = -1; bool shownDone; long shownReward = -1;
        float shownFill = -1f; bool shownFillDone;

        void OnLanguageChanged()
        {
            textDirty = true;
            Refresh();
        }

        void OnTabChanged(int tab)
        {
            bool home = tab < 0;        // 전투력 표시와 같이 홈 전용
            visible = home;
            group.alpha = home ? 1f : 0f;
            group.blocksRaycasts = home;
            group.interactable = home;
        }

        void OnClick()
        {
            if (!GuideQuests.IsDone) return;
            if (!GuideQuests.Claim()) return;
            // 받은 양 알림("균사석 +n 획득!") 없이 바로 다음 목표를 보여 준다
            textDirty = true;
            Refresh();
        }

        void Update()
        {
            // 달성해서 보이는 동안만 박동한다. 그 밖에는 크기가 1이 아닐 때 한 번만 되돌린다
            if (done && visible) rect.localScale = Vector3.one * (1f + PulseSize * Mathf.Sin(Time.unscaledTime * PulseSpeed));
            else if (rect.localScale != Vector3.one) rect.localScale = Vector3.one;
        }

        void Refresh()
        {
            done = GuideQuests.IsDone;
            var kind = GuideQuests.Kind;
            var stat = GuideQuests.Stat;
            int target = GuideQuests.Target, prog = GuideQuests.Progress;
            long reward = GuideQuests.Reward;
            bool goalChanged = textDirty || kind != shownKind || stat != shownStat || target != shownTarget;
            if (goalChanged && label != null) label.text = GuideQuests.Describe();
            if ((goalChanged || prog != shownProg || done != shownDone) && progressText != null)
                progressText.text = done ? Loc.T("guide.claim") : $"{prog:N0}/{target:N0}";
            if ((textDirty || reward != shownReward) && rewardText != null) rewardText.text = "+" + reward.ToString("N0");
            shownKind = kind; shownStat = stat; shownTarget = target; shownProg = prog; shownDone = done; shownReward = reward;
            textDirty = false;
            SetFill(target <= 0 ? 0f : Mathf.Clamp01(prog / (float)target), done);
        }

        void SetFill(float k, bool isDone)
        {
            if (fill == null || (k == shownFill && isDone == shownFillDone)) return;   // 같으면 레이아웃·메시를 건드리지 않는다
            shownFill = k; shownFillDone = isDone;
            fill.sprite = isDone && doneSprite != null ? doneSprite : normalSprite;
            fill.rectTransform.anchorMax = new Vector2(k, 1f);
            fill.gameObject.SetActive(k > 0f);
        }
    }
}
