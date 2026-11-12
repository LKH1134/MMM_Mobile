using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 일일·주간 의뢰 화면 (프리팹 QuestScreen, 와이어프레임 '06. 일일/주간 의뢰'·목업 '일일 의뢰'):
    //   제목 메모 · 초기화까지 남은 시간 · 2열 카드(이름 · 그림 · 설명 · 보상 · 달성도 막대 · 의뢰 완료 버튼) · 주간/일일 탭 버튼 · 일괄 수령 · 하단 띠 X.
    // 데이터는 DailyQuests (퀘스트 시트 period daily/weekly). 고른 탭은 초록 버튼·흰 글자, 다른 탭은 베이지·진한 글자.
    // 11-10: 세계수 의뢰를 합쳤다 — 보상 줄에 세계수 경험치(treeExp)도 보인다
    public class QuestScreenView : MonoBehaviour
    {
        [SerializeField] TMP_Text titleText;             // 제목 메모 글자 (일일 의뢰 / 주간 의뢰)
        [SerializeField] TMP_Text resetText;
        [SerializeField] RectTransform grid;
        [SerializeField] QuestCardView cardTemplate;     // 꺼 둔 원본
        [SerializeField] Button weeklyButton;
        [SerializeField] Button dailyButton;
        [SerializeField] Button claimAllButton;          // 일괄 수령 (일일·주간 모두, 받을 의뢰가 없으면 회색)

        [Header("그림 (Resources 경로)")]
        [SerializeField] string killIcon = "UI/Icon/sword";
        [SerializeField] string stageIcon = "UI/Icon/boss_mushroom";
        [SerializeField] string upgradeIcon = "UI/Icon/upgrade";
        [SerializeField] string gachaIcon = "UI/Icon/roulette";
        [SerializeField] string adIcon = "UI/Icon/advertisement";
        [SerializeField] string dungeonIcon = "UI/Icon/dungeon";
        [SerializeField] string dailyIcon = "UI/Icon/calendar";
        [SerializeField] string claimSprite = UiSkin.BtnGreen;      // 받을 수 있음
        [SerializeField] string doneSprite = UiSkin.BtnGrey;        // 받음
        [SerializeField] string barFillSprite = UiSkin.BarGreen;
        [SerializeField] string tabOnSprite = UiSkin.BtnGreen;      // 고른 탭
        [SerializeField] string tabOffSprite = "UI/UI_Btn_Beige";

        readonly List<QuestCardView> cards = new List<QuestCardView>();
        int shownSecond = -1;
        QuestPeriod period = QuestPeriod.Daily;

        void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (weeklyButton != null) { weeklyButton.interactable = true; weeklyButton.onClick.AddListener(() => Show(QuestPeriod.Weekly)); }
            if (dailyButton != null) { dailyButton.interactable = true; dailyButton.onClick.AddListener(() => Show(QuestPeriod.Daily)); }
            if (claimAllButton != null)
            {
                claimAllButton.onClick.AddListener(() => DailyQuests.ClaimAll());
                UiSkin.GreyWhenDisabled(claimAllButton);
            }
        }

        void OnEnable()
        {
            PopupLayer.Raise(gameObject, PopupLayer.Popup);
            // 예전 DailyQuestPopup과 같게 팝업으로 알린다: 열린 동안 광고 상자 시간이 멈추고, 뒤로가기·다른 팝업이 열리면 닫힌다
            PopupGate.Opened(this, () => gameObject.SetActive(false));
            DailyQuests.Changed += Refresh;
            Loc.Changed += OnLanguageChanged;
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed += CloseOnTab;
            // 일일에 받을 것이 없고 주간에만 있으면 주간 탭으로 연다
            period = DailyQuests.ClaimableCountOf(QuestPeriod.Daily) == 0 && DailyQuests.ClaimableCountOf(QuestPeriod.Weekly) > 0
                ? QuestPeriod.Weekly : QuestPeriod.Daily;
            shownSecond = -1;
            Refresh();
        }

        void OnDisable()
        {
            PopupGate.Closed(this);
            DailyQuests.Changed -= Refresh;
            Loc.Changed -= OnLanguageChanged;
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed -= CloseOnTab;
        }

        // 언어가 바뀌면 타이머와 카드 글자를 다시 쓴다
        void OnLanguageChanged()
        {
            shownSecond = -1;
            Refresh();
        }

        void CloseOnTab(int tab) => gameObject.SetActive(false);

        void Show(QuestPeriod p)
        {
            if (period == p) return;
            period = p;
            shownSecond = -1;
            Refresh();
        }

        void Update()
        {
            var t = DailyQuests.UntilReset(period);
            int sec = (int)t.TotalSeconds;
            if (sec == shownSecond || resetText == null) return;
            shownSecond = sec;
            resetText.text = t.TotalDays >= 1
                ? Loc.F("quest.reset_week", (int)t.TotalDays, t.Hours, t.Minutes, t.Seconds)
                : Loc.F("quest.reset_in", (int)t.TotalHours, t.Minutes, t.Seconds);
        }

        string IconOf(QuestKind k)
        {
            switch (k)
            {
                case QuestKind.Kill: return killIcon;
                case QuestKind.Stage: return stageIcon;
                case QuestKind.Upgrade: return upgradeIcon;
                case QuestKind.Gacha: return gachaIcon;
                case QuestKind.Dungeon: return dungeonIcon;
                case QuestKind.Daily: return dailyIcon;
                default: return adIcon;
            }
        }

        static string TitleOf(QuestKind k)
        {
            switch (k)
            {
                case QuestKind.Kill: return Loc.T("quest.title_kill");
                case QuestKind.Stage: return Loc.T("quest.title_stage");
                case QuestKind.Upgrade: return Loc.T("quest.title_upgrade");
                case QuestKind.Gacha: return Loc.T("quest.title_gacha");
                case QuestKind.Dungeon: return Loc.T("quest.title_dungeon");
                case QuestKind.Daily: return Loc.T("quest.title_daily");
                default: return Loc.T("common.watch_ad");
            }
        }

        void SetTab(Button b, bool on)
        {
            if (b == null) return;
            if (b.image != null) b.image.sprite = Resources.Load<Sprite>(on ? tabOnSprite : tabOffSprite);
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = on ? Color.white : UiSkin.Ink;
        }

        // 보상 줄: 0인 보상은 빼고, 세계수 경험치는 다음 줄에
        static string RewardLine(QuestPeriod p, int i)
        {
            long gold = DailyQuests.RewardGold(p, i), myc = DailyQuests.RewardMycelite(p, i), exp = DailyQuests.RewardTreeExp(p, i);
            string line = "";
            if (gold > 0) line = Loc.F("idle.gold", Wallet.Format(gold));
            if (myc > 0) line += (line.Length > 0 ? "   " : "") + Loc.F("idle.mycelite", Wallet.Format(myc));
            if (exp > 0) line += (line.Length > 0 ? "\n" : "") + Loc.F("quest.tree_exp", exp.ToString("N0"));
            return line;
        }

        void Refresh()
        {
            if (cardTemplate == null || grid == null) return;
            var p = period;
            int n = DailyQuests.Count(p);
            SetTab(dailyButton, p == QuestPeriod.Daily);
            SetTab(weeklyButton, p == QuestPeriod.Weekly);
            if (titleText != null) titleText.text = Loc.T(p == QuestPeriod.Weekly ? "quest.weekly" : "quest.daily");
            if (claimAllButton != null) claimAllButton.interactable = DailyQuests.ClaimableCount > 0;
            while (cards.Count < n)
            {
                var c = Instantiate(cardTemplate, grid);
                c.gameObject.SetActive(true);
                int index = cards.Count;
                c.button.onClick.AddListener(() => DailyQuests.Claim(period, index));
                cards.Add(c);
            }
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                c.gameObject.SetActive(i < n);
                if (i >= n) continue;
                var kind = DailyQuests.Kind(p, i);
                int target = DailyQuests.Target(p, i), done = Mathf.Min(DailyQuests.Progress(p, i), target);
                bool claimed = DailyQuests.IsClaimed(p, i), can = DailyQuests.CanClaim(p, i);
                c.name = "Quest" + i;
                if (c.title != null) c.title.text = TitleOf(kind);
                if (c.icon != null) c.icon.sprite = Resources.Load<Sprite>(IconOf(kind));
                if (c.desc != null) c.desc.text = DailyQuests.Describe(p, i);
                if (c.reward != null) c.reward.text = RewardLine(p, i);
                if (c.progress != null) c.progress.text = $"{done:N0} / {target:N0}";
                if (c.barFill != null)
                {
                    c.barFill.anchorMax = new Vector2(target <= 0 ? 0f : Mathf.Clamp01((float)done / target), 1f);
                    var fi = c.barFill.GetComponent<Image>();
                    if (fi != null && fi.sprite == null) fi.sprite = Resources.Load<Sprite>(barFillSprite);
                }
                // 받을 수 있으면 "의뢰 완료" 버튼, 받았으면 버튼 바탕 없이 "완료" 글자만 (11-05), 진행 중이면 버튼 대신 막대만
                if (c.button != null)
                {
                    c.button.gameObject.SetActive(can || claimed);
                    c.button.interactable = can;
                    c.button.image.sprite = Resources.Load<Sprite>(can ? claimSprite : doneSprite);
                    c.button.image.enabled = can;     // 받은 뒤에는 회색 바탕을 그리지 않는다
                }
                if (c.buttonText != null) c.buttonText.text = Loc.T(claimed ? "quest.done" : "quest.complete");
                DoneCover.Set(c.transform, claimed, c.button != null ? c.button.transform : null);   // 받은 의뢰는 회색 (11-10)
            }
        }
    }
}
