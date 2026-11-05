using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 일일 의뢰 화면 (프리팹 QuestScreen, 와이어프레임 '06. 일일/주간 의뢰'·목업 '일일 의뢰'):
    //   제목 메모 · 초기화까지 남은 시간 · 2열 카드(이름 · 그림 · 설명 · 보상 · 달성도 막대 · 의뢰 완료 버튼) · 주간/일일 버튼 · 하단 띠 X.
    // 데이터는 예전 DailyQuestPopup과 같은 DailyQuests를 쓴다. 주간 의뢰는 아직 없어 버튼을 회색으로 둔다
    public class QuestScreenView : MonoBehaviour
    {
        [SerializeField] TMP_Text resetText;
        [SerializeField] RectTransform grid;
        [SerializeField] QuestCardView cardTemplate;     // 꺼 둔 원본
        [SerializeField] Button weeklyButton;
        [SerializeField] Button dailyButton;

        [Header("그림 (Resources 경로)")]
        [SerializeField] string killIcon = "UI/Icon/sword";
        [SerializeField] string stageIcon = "UI/Icon/boss_mushroom";
        [SerializeField] string upgradeIcon = "UI/Icon/upgrade";
        [SerializeField] string gachaIcon = "UI/Icon/roulette";
        [SerializeField] string adIcon = "UI/Icon/advertisement";
        [SerializeField] string claimSprite = "UI/UI_Btn_Green";      // 받을 수 있음
        [SerializeField] string doneSprite = "UI/UI_Btn_Grey";        // 받음
        [SerializeField] string barFillSprite = "UI/UI_PP_Bar_Gauge_Green";

        readonly List<QuestCardView> cards = new List<QuestCardView>();
        int shownSecond = -1;

        void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (weeklyButton != null) weeklyButton.interactable = false;     // 주간 의뢰는 아직 없다
            if (dailyButton != null) dailyButton.interactable = true;
        }

        void OnEnable()
        {
            PopupLayer.Raise(gameObject, PopupLayer.Popup);
            // 예전 DailyQuestPopup과 같게 팝업으로 알린다: 열린 동안 광고 상자 시간이 멈추고, 뒤로가기·다른 팝업이 열리면 닫힌다
            PopupGate.Opened(this, () => gameObject.SetActive(false));
            DailyQuests.Changed += Refresh;
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed += CloseOnTab;
            Refresh();
        }

        void OnDisable()
        {
            PopupGate.Closed(this);
            DailyQuests.Changed -= Refresh;
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed -= CloseOnTab;
        }

        void CloseOnTab(int tab) => gameObject.SetActive(false);

        void Update()
        {
            var t = DailyQuests.UntilReset;
            int sec = (int)t.TotalSeconds;
            if (sec == shownSecond || resetText == null) return;
            shownSecond = sec;
            resetText.text = $"초기화까지 {(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }

        string IconOf(QuestKind k)
        {
            switch (k)
            {
                case QuestKind.Kill: return killIcon;
                case QuestKind.Stage: return stageIcon;
                case QuestKind.Upgrade: return upgradeIcon;
                case QuestKind.Gacha: return gachaIcon;
                default: return adIcon;
            }
        }

        static string TitleOf(QuestKind k)
        {
            switch (k)
            {
                case QuestKind.Kill: return "버섯 퇴치";
                case QuestKind.Stage: return "보스 도전";
                case QuestKind.Upgrade: return "강화 연습";
                case QuestKind.Gacha: return "뽑기";
                default: return "광고 보기";
            }
        }

        void Refresh()
        {
            if (cardTemplate == null || grid == null) return;
            int n = DailyQuests.Count;
            while (cards.Count < n)
            {
                var c = Instantiate(cardTemplate, grid);
                c.gameObject.SetActive(true);
                int index = cards.Count;
                c.button.onClick.AddListener(() => DailyQuests.Claim(index));
                cards.Add(c);
            }
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                c.gameObject.SetActive(i < n);
                if (i >= n) continue;
                var kind = DailyQuests.Kind(i);
                int target = DailyQuests.Target(i), done = Mathf.Min(DailyQuests.Progress(i), target);
                bool claimed = DailyQuests.IsClaimed(i), can = DailyQuests.CanClaim(i);
                c.name = "Quest" + i;
                if (c.title != null) c.title.text = TitleOf(kind);
                if (c.icon != null) c.icon.sprite = Resources.Load<Sprite>(IconOf(kind));
                if (c.desc != null) c.desc.text = DailyQuests.Describe(i);
                if (c.reward != null) c.reward.text = $"골드 +{Wallet.Format(DailyQuests.RewardGold(i))}   균사석 +{Wallet.Format(DailyQuests.RewardMycelite(i))}";
                if (c.progress != null) c.progress.text = $"{done:N0} / {target:N0}";
                if (c.barFill != null)
                {
                    c.barFill.anchorMax = new Vector2(target <= 0 ? 0f : Mathf.Clamp01((float)done / target), 1f);
                    var fi = c.barFill.GetComponent<Image>();
                    if (fi != null && fi.sprite == null) fi.sprite = Resources.Load<Sprite>(barFillSprite);
                }
                // 받을 수 있으면 "의뢰 완료" 버튼, 받았으면 회색 "완료", 진행 중이면 버튼 대신 막대만
                if (c.button != null)
                {
                    c.button.gameObject.SetActive(can || claimed);
                    c.button.interactable = can;
                    c.button.image.sprite = Resources.Load<Sprite>(can ? claimSprite : doneSprite);
                }
                if (c.buttonText != null) c.buttonText.text = claimed ? "완료" : "의뢰 완료";
            }
        }
    }
}
