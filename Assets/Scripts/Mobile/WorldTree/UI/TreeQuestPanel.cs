using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 의뢰 시트 (QuestPopup의 Content에 붙인다): 주간 의뢰 카드 3장 · 일일 의뢰 칸 · 초기화까지 남은 시간 (규칙은 WorldTree.Quests).
    // 다른 세계수 시트와 달리 프리팹에 놓인 칸을 이름으로 찾아 채운다:
    //   PageTabs/WeeklyTab·DailyTab(Toggle) · WeeklyTitle·DailyTitle · RemainTime
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
            weekly = Cards(weeklyPage != null ? weeklyPage.transform : null, "Quest_", true);
            daily = Cards(dailyPage != null ? dailyPage.transform.Find("Viewport/Content") : null, "Daily_", false);
            if (weeklyTab != null) weeklyTab.onValueChanged.AddListener(on => { if (on) ShowPage(true); });
            if (dailyTab != null) dailyTab.onValueChanged.AddListener(on => { if (on) ShowPage(false); });
        }

        void OnEnable()
        {
            WorldTree.Changed += MarkDirty;
            Formation.Changed += MarkDirty;
            ShowPage(weeklyTab == null || weeklyTab.isOn);
            Refresh();
        }

        void OnDisable()
        {
            WorldTree.Changed -= MarkDirty;
            Formation.Changed -= MarkDirty;
        }

        void MarkDirty() => dirty = true;

        // 진행은 처치마다 메모리만 바뀌므로(Changed 없음) 1초마다 다시 쓴다
        void LateUpdate()
        {
            tick -= Time.unscaledDeltaTime;
            if (!dirty && tick > 0f) return;
            tick = 1f;
            dirty = false;
            Refresh();
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

        void Refresh()
        {
            if (weekly == null) return;
            var left = showingWeekly ? WorldTree.UntilWeekReset : DungeonTickets.UntilReset;
            if (remain != null)
                remain.text = left.TotalDays >= 1
                    ? $"남은 시간 {(int)left.TotalDays}일 {left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}"
                    : $"남은 시간 {left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}";
            Fill(weekly, WorldTree.WeeklyQuests, true);
            Fill(daily, WorldTree.DailyQuests, false);
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
                if (c.name != null) c.name.text = row.name;
                if (c.npc != null) c.npc.text = string.IsNullOrEmpty(q.kid) ? "세계수" : Formation.Name(q.kid);
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
                if (descIsButton && claimable) text = $"완료! 눌러서 받기 · 경험치 +{WorldTree.Count(row.exp)}";
                else if (descIsButton && q.claimed) text = "받았어요";
                if (c.desc != null) c.desc.text = text;
                if (c.progress != null) c.progress.text = $"{WorldTree.Count(Mathf.Min(q.prog, row.target))} / {WorldTree.Count(row.target)}" + $"  · 경험치 +{WorldTree.Count(row.exp)}";
                if (c.button != null) c.button.interactable = claimable;
                if (c.buttonLabel != null) c.buttonLabel.text = q.claimed ? "완료함" : done ? "의뢰 완료" : "진행 중";
            }
        }

        void Claim(bool isWeekly, int i)
        {
            var list = isWeekly ? WorldTree.WeeklyQuests : WorldTree.DailyQuests;
            if (i >= list.Count) return;
            int exp = WorldTree.QuestRowOf(list[i])?.exp ?? 0;
            if (!WorldTree.ClaimQuest(isWeekly, i)) return;
            var screen = FindAnyObjectByType<WorldTreeScreen>();
            if (screen != null) screen.ShowToast($"의뢰 완료! 세계수 경험치 +{WorldTree.Count(exp)}");
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
                    name = t.Find("Name")?.GetComponent<TMP_Text>(),
                    npc = t.Find("NpcName")?.GetComponent<TMP_Text>(),
                    desc = t.Find("Desc/Text")?.GetComponent<TMP_Text>(),
                    progress = t.Find("Progress")?.GetComponent<TMP_Text>(),
                    portrait = (t.Find("Portrait") ?? t.Find("Icon"))?.GetComponent<Image>(),
                    // [UI 개편 2026-11-05] 일일 칸도 CompleteBtn이 있으면 그 버튼으로 받는다 (없으면 예전처럼 Desc를 눌러 받기)
                    button = (t.Find("CompleteBtn") ?? (isWeekly ? null : t.Find("Desc")))?.GetComponent<Button>(),
                    buttonLabel = t.Find("CompleteBtn/Label")?.GetComponent<TMP_Text>(),
                    questIcon = t.Find("Desc/QuestIcon")?.GetComponent<Image>(),
                    barFill = t.Find("Bar/Fill") as RectTransform,
                };
                int index = n - 1;
                if (c.button != null) c.button.onClick.AddListener(() => Claim(isWeekly, index));
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
