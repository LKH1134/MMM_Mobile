using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 의뢰 시트 (QuestPopup의 Content에 붙인다): 주간 의뢰 카드 3장 · 일일 의뢰 칸 · 초기화까지 남은 시간 (규칙은 WorldTree.Quests).
    // 다른 세계수 시트와 달리 프리팹에 놓인 칸을 이름으로 찾아 채운다:
    //   PageTabs/WeeklyTab·DailyTab(Toggle) · WeeklyTitle·DailyTitle · RemainTime · ClaimAllBtn(Button, 받을 의뢰가 없으면 회색)
    //   WeeklyPage/Quest_1~ (Name·Portrait·NpcName·Desc/Text·Progress·CompleteBtn/Label)
    //   DailyPage/Viewport/Content/Daily_1~ (Name·Icon·Desc(Button)/Text·Progress) — 일일은 Desc를 눌러 받는다. 의뢰보다 많은 칸은 숨긴다
    public class TreeQuestPanel : MonoBehaviour
    {
        class Card
        {
            public GameObject go;
            public TMP_Text name, npc, desc, progress, buttonLabel;
            public Image portrait;
            public Button button;
            public Image questIcon;         // [UI 개편 2026-11-05] 말풍선(Desc) 안 의뢰 종류 그림 — 있으면 채운다
            public RectTransform barFill;   // [UI 개편 2026-11-05] 달성도 막대(Bar/Fill) — 있으면 채운다
            public TMP_Text reward;         // [UI 개편 2026-11-05] 홈 일일 의뢰와 같은 카드: 보상 줄(Reward)이 있으면 경험치는 여기, 막대 글자는 n / m만
        }

        // [UI 개편 2026-11-05] 의뢰 종류별 말풍선 그림 (Resources 경로). 지정 버섯(species)은 그 적 그림을 쓴다
        static string QuestIconPath(string kind)
        {
            switch (kind)
            {
                case "kill": return "UI/Icon/sword";
                case "stage": return "UI/Icon/boss_mushroom";
                case "dungeon": return "UI/Icon/dungeon";
                case "daily": return "UI/Icon/calendar";
                default: return "UI/Icon/quest_scroll";
            }
        }

        Card[] weekly, daily;
        Toggle weeklyTab, dailyTab;
        GameObject weeklyPage, dailyPage, weeklyTitle, dailyTitle;
        TMP_Text remain;
        Button claimAll;
        float tick;
        bool dirty;

        void Awake()
        {
            weeklyTab = Find<Toggle>("PageTabs/WeeklyTab");
            dailyTab = Find<Toggle>("PageTabs/DailyTab");
            weeklyPage = transform.Find("WeeklyPage")?.gameObject;
            dailyPage = transform.Find("DailyPage")?.gameObject;
            weeklyTitle = transform.Find("WeeklyTitle")?.gameObject;
            dailyTitle = transform.Find("DailyTitle")?.gameObject;
            remain = Find<TMP_Text>("RemainTime");
            claimAll = Find<Button>("ClaimAllBtn");
            if (claimAll != null)
            {
                claimAll.onClick.AddListener(ClaimAll);
                UiSkin.GreyWhenDisabled(claimAll);
            }
            weekly = Cards(weeklyPage != null ? weeklyPage.transform : null, "Quest_", true);
            daily = Cards(dailyPage != null ? dailyPage.transform.Find("Viewport/Content") : null, "Daily_", false);
            if (weeklyTab != null) weeklyTab.onValueChanged.AddListener(on => { if (on) ShowPage(true); });
            if (dailyTab != null) dailyTab.onValueChanged.AddListener(on => { if (on) ShowPage(false); });
        }

        void OnEnable()
        {
            WorldTree.Changed += MarkDirty;
            Formation.Changed += MarkDirty;
            Loc.Changed += MarkDirty;
            ShowPage(weeklyTab == null || weeklyTab.isOn);
            Refresh();
        }

        void OnDisable()
        {
            WorldTree.Changed -= MarkDirty;
            Formation.Changed -= MarkDirty;
            Loc.Changed -= MarkDirty;
        }

        void MarkDirty() => dirty = true;

        // 남은 시간 글자는 1초마다, 카드 전체는 값이 바뀌면 바로·아니면 5초마다 (세계수 씬에서만 열려 처치로 진행이 바뀌지 않는다)
        const float CardsEvery = 5f;
        float cardsIn;

        void LateUpdate()
        {
            tick -= Time.unscaledDeltaTime;
            cardsIn -= Time.unscaledDeltaTime;
            if (!dirty && tick > 0f) return;
            tick = 1f;
            if (dirty || cardsIn <= 0f) { dirty = false; Refresh(); return; }
            RefreshRemain();
        }

        bool showingWeekly = true;

        void ShowPage(bool weeklyOn)
        {
            showingWeekly = weeklyOn;
            if (weeklyPage != null) weeklyPage.SetActive(weeklyOn);
            if (dailyPage != null) dailyPage.SetActive(!weeklyOn);
            if (weeklyTitle != null) weeklyTitle.SetActive(weeklyOn);
            if (dailyTitle != null) dailyTitle.SetActive(!weeklyOn);
            if (weeklyTab != null) weeklyTab.SetIsOnWithoutNotify(weeklyOn);
            if (dailyTab != null) dailyTab.SetIsOnWithoutNotify(!weeklyOn);
            Refresh();
        }

        void RefreshRemain()
        {
            if (remain == null) return;
            var left = showingWeekly ? WorldTree.UntilWeekReset : DungeonTickets.UntilReset;
            remain.text = left.TotalDays >= 1
                ? Loc.F("treequest.remain_days", (int)left.TotalDays, left.Hours, left.Minutes, left.Seconds)
                : Loc.F("treequest.remain", left.Hours, left.Minutes, left.Seconds);
        }

        void Refresh()
        {
            if (weekly == null) return;
            cardsIn = CardsEvery;
            RefreshRemain();
            Fill(weekly, WorldTree.WeeklyQuests, true);
            Fill(daily, WorldTree.DailyQuests, false);
            if (claimAll != null) claimAll.interactable = WorldTree.AnyQuestClaimable;
        }

        void Fill(Card[] cards, System.Collections.Generic.IReadOnlyList<WorldTree.Quest> quests, bool isWeekly)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                bool has = i < quests.Count && WorldTree.QuestRowOf(quests[i]) != null;
                c.go.SetActive(has);
                if (!has) continue;
                var q = quests[i];
                var row = WorldTree.QuestRowOf(q);
                bool done = WorldTree.QuestDone(q), claimable = WorldTree.QuestClaimable(q);
                if (c.name != null) c.name.text = row.Name;
                if (c.npc != null) c.npc.text = string.IsNullOrEmpty(q.kid) ? Loc.T("tab.worldtree") : Formation.Name(q.kid);
                if (c.portrait != null)
                {
                    // 일일 '지정 버섯' 의뢰는 그 적 그림, 나머지는 의뢰인 꼬마
                    var sprite = row.kind == "species" ? WorldTree.EnemySprite(q.arg)
                        : !string.IsNullOrEmpty(q.kid) ? Formation.Icon(q.kid) : WorldTree.TreeSprite(WorldTree.Stage);
                    c.portrait.sprite = sprite;
                    c.portrait.enabled = sprite != null;
                    c.portrait.preserveAspect = true;
                }
                if (c.questIcon != null)
                {
                    var icon = row.kind == "species" ? WorldTree.EnemySprite(q.arg) : Resources.Load<Sprite>(QuestIconPath(row.kind));
                    c.questIcon.sprite = icon;
                    c.questIcon.enabled = icon != null;
                }
                if (c.barFill != null)
                    c.barFill.anchorMax = new Vector2(row.target <= 0 ? 0f : Mathf.Clamp01((float)q.prog / row.target), 1f);
                string text = WorldTree.QuestText(q);
                bool descIsButton = c.buttonLabel == null;    // 완료 버튼이 없는 예전 일일 칸: 말풍선을 눌러 받는다
                if (descIsButton && claimable) text = Loc.F("treequest.claim_hint", WorldTree.Count(row.exp));
                else if (descIsButton && q.claimed) text = Loc.T("treequest.claimed_bubble");
                if (c.desc != null) c.desc.text = text;
                if (c.reward != null) c.reward.text = Loc.F("treequest.exp", WorldTree.Count(row.exp));
                if (c.progress != null) c.progress.text = $"{WorldTree.Count(Mathf.Min(q.prog, row.target))} / {WorldTree.Count(row.target)}" + (c.reward != null ? "" : "  · " + Loc.F("treequest.exp", WorldTree.Count(row.exp)));
                if (c.button != null) c.button.interactable = claimable;
                if (c.reward != null && c.button != null && !isWeekly)
                {
                    // 홈 일일 의뢰와 같게: 진행 중이면 버튼을 숨기고, 받았으면 바탕 없이 "완료" 글자만
                    c.button.gameObject.SetActive(claimable || q.claimed);
                    if (c.button.image != null) c.button.image.enabled = claimable;
                }
                if (c.buttonLabel != null) c.buttonLabel.text = q.claimed ? (c.reward != null ? Loc.T("tree.done") : Loc.T("treequest.done_full")) : done ? Loc.T("treequest.complete") : Loc.T("treequest.in_progress");
                DoneCover.Set(c.go.transform, q.claimed, c.button != null ? c.button.transform : null);   // 받은 의뢰는 회색 (11-10)
            }
        }

        void Claim(bool isWeekly, int i)
        {
            var list = isWeekly ? WorldTree.WeeklyQuests : WorldTree.DailyQuests;
            if (i >= list.Count) return;
            int exp = WorldTree.QuestRowOf(list[i])?.exp ?? 0;
            if (!WorldTree.ClaimQuest(isWeekly, i)) return;
            var screen = FindAnyObjectByType<WorldTreeScreen>();
            if (screen != null) screen.ShowToast(Loc.F("treequest.claim_toast", WorldTree.Count(exp)));
            Refresh();
        }

        // 일괄 수령: 주간·일일 모두 받을 수 있는 의뢰를 받는다 (보이는 쪽만이 아니라)
        void ClaimAll()
        {
            int count = WorldTree.ClaimAllQuests(out long exp);
            if (count == 0) { Refresh(); return; }
            var screen = FindAnyObjectByType<WorldTreeScreen>();
            if (screen != null)
                screen.ShowToast(count == 1 ? Loc.F("treequest.claim_toast", WorldTree.Count(exp))
                    : Loc.F("treequest.claim_all_toast", count, WorldTree.Count(exp)));
            Refresh();
        }

        Card[] Cards(Transform parent, string prefix, bool isWeekly)
        {
            var list = new System.Collections.Generic.List<Card>();
            if (parent == null) return list.ToArray();
            for (int n = 1; ; n++)
            {
                var t = parent.Find(prefix + n);
                if (t == null) break;
                var c = new Card
                {
                    go = t.gameObject,
                    name = (t.Find("Name") ?? t.Find("Title"))?.GetComponent<TMP_Text>(),
                    npc = t.Find("NpcName")?.GetComponent<TMP_Text>(),
                    // 홈 일일 의뢰 카드 이름도 받는다: Title · IconBox/Icon · Desc(글자) · Reward · Claim · Bar/Text
                    // [UI 개편 2026-11-05] 주간 카드의 Desc·Reward는 안쪽 칸 InfoBox 아래로 옮겼다 (InfoBox/… 도 찾는다)
                    desc = (t.Find("Desc/Text") ?? t.Find("Desc") ?? t.Find("InfoBox/Desc"))?.GetComponent<TMP_Text>(),
                    progress = (t.Find("Progress") ?? t.Find("Bar/Text"))?.GetComponent<TMP_Text>(),
                    reward = (t.Find("Reward") ?? t.Find("InfoBox/Reward"))?.GetComponent<TMP_Text>(),
                    portrait = (t.Find("Portrait") ?? t.Find("Icon") ?? t.Find("IconBox/Icon"))?.GetComponent<Image>(),
                    // [UI 개편 2026-11-05] 일일 칸도 CompleteBtn이 있으면 그 버튼으로 받는다 (없으면 예전처럼 Desc를 눌러 받기)
                    button = (t.Find("CompleteBtn") ?? t.Find("Claim") ?? (isWeekly ? null : t.Find("Desc")))?.GetComponent<Button>(),
                    buttonLabel = (t.Find("CompleteBtn/Label") ?? t.Find("Claim/Label"))?.GetComponent<TMP_Text>(),
                    questIcon = t.Find("Desc/QuestIcon")?.GetComponent<Image>(),
                    barFill = t.Find("Bar/Fill") as RectTransform,
                };
                int index = n - 1;
                if (c.button != null) c.button.onClick.AddListener(() => Claim(isWeekly, index));
                // [UI 개편 11-05] 주간 카드 버튼은 늘 보이고, 받을 수 없으면 회색 그림 (받을 수 있으면 견본 그림 = 초록)
                if (isWeekly && c.button != null) UiSkin.GreyWhenDisabled(c.button);
                list.Add(c);
            }
            return list.ToArray();
        }

        T Find<T>(string path) where T : Component
        {
            var t = transform.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }
    }
}
