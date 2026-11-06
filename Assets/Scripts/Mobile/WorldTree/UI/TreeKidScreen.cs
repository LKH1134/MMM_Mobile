using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-05] 세계수 꼬마 시트 (편성 화면과 같은 배치, 프리팹 KidPopup의 Sheet/Content/KidScreen):
    //   위 4칸 = 오늘의 부탁(홈 일일 의뢰 카드 모양) · 가운데 = 고른 꼬마 정보(호감도·특기·진화 조건 + 선물·진화 버튼) ·
    //   주간 상자 버튼 · 아래 = 보유 꼬마 칸(누르면 가운데 정보가 바뀐다, 레벨 옆 [선물 가능]·[진화 가능]).
    // 모양은 프리팹에서 고치고, 값과 버튼 동작만 여기서 넣는다. 이 화면이 켜져 있으면 예전 TreeKidPanel은 꺼 둔다
    public class TreeKidScreen : MonoBehaviour
    {
        [Header("오늘의 부탁 (왼쪽부터, 부탁보다 많은 칸은 숨긴다)")]
        [SerializeField] QuestCardView[] requestCells;

        [Header("고른 꼬마 정보")]
        [SerializeField] Image infoIcon;
        [SerializeField] TMP_Text infoLevel;
        [SerializeField] TMP_Text infoName;        // 이름 ★ (단계는 infoLevel에 "Lv.n [n단계]")
        [SerializeField] TMP_Text infoAffinity;    // 호감도 레벨
        [SerializeField] TMP_Text infoBonus;       // 특기
        [SerializeField] TMP_Text infoEvolve;      // 진화 조건
        [SerializeField] Button giftButton;
        [SerializeField] TMP_Text giftLabel;
        [SerializeField] Button evolveButton;
        [SerializeField] TMP_Text evolveLabel;

        [Header("주간 상자")]
        [SerializeField] Button weekButton;
        [SerializeField] TMP_Text weekLabel;

        [Header("보유 꼬마")]
        [SerializeField] RectTransform grid;
        [SerializeField] KidCellView cellTemplate;
        [SerializeField] TMP_Text emptyText;       // 꼬마가 없을 때

        readonly List<KidCellView> cells = new List<KidCellView>();
        string selected;
        bool dirty;
        float tick;

        void Awake()
        {
            if (cellTemplate != null) cellTemplate.gameObject.SetActive(false);
            for (int i = 0; i < requestCells.Length; i++)
            {
                if (requestCells[i] == null || requestCells[i].button == null) continue;
                int index = i;
                UiSkin.GreyWhenDisabled(requestCells[i].button);
                requestCells[i].button.onClick.AddListener(() => DoRequest(index));
            }
            if (giftButton != null) { UiSkin.GreyWhenDisabled(giftButton); giftButton.onClick.AddListener(Gift); }
            if (evolveButton != null) { UiSkin.GreyWhenDisabled(evolveButton); evolveButton.onClick.AddListener(Evolve); }
            if (weekButton != null) { UiSkin.GreyWhenDisabled(weekButton); weekButton.onClick.AddListener(ClaimWeek); }
        }

        void OnEnable()
        {
            WorldTree.Changed += MarkDirty;
            Formation.Changed += MarkDirty;
            Wallet.Changed += MarkDirty;
            Refresh();
        }

        void OnDisable()
        {
            WorldTree.Changed -= MarkDirty;
            Formation.Changed -= MarkDirty;
            Wallet.Changed -= MarkDirty;
        }

        void MarkDirty() => dirty = true;

        // 값이 바뀌면(Changed) 바로, 아니면 SafetyTick마다 다시 쓴다. 이 창은 세계수 씬에서만 열려 처치로 창고 수가 바뀌지 않는다 —
        // 1초마다 통째로 다시 쓰던 것을 줄였다 (주기는 자정 넘김 같은 놓친 변화만 잡는다)
        const float SafetyTick = 5f;

        void LateUpdate()
        {
            tick -= Time.unscaledDeltaTime;
            if (!dirty && tick > 0f) return;
            tick = SafetyTick;
            dirty = false;
            Refresh();
        }

        void Refresh()
        {
            RefreshRequests();
            RefreshWeek();
            RefreshCells();
            RefreshInfo();
        }

        // ── 부탁 ──
        void RefreshRequests()
        {
            var requests = WorldTree.Requests;
            for (int i = 0; i < requestCells.Length; i++)
            {
                var c = requestCells[i];
                if (c == null) continue;
                bool has = i < requests.Count;
                // 칸을 감싼 자리(Request_n)째 끈다 → 부탁이 4개보다 적으면 남은 칸이 가운데로 모인다
                var slot = c.transform.parent != null && c.transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>() == null ? c.transform.parent.gameObject : c.gameObject;
                slot.SetActive(has);
                c.gameObject.SetActive(has);
                if (!has) continue;
                var r = requests[i];
                long have = WorldTree.StoreCount(r.enemy);
                bool can = !r.done && WorldTree.CanDoRequest(i);
                if (c.title != null) c.title.text = Formation.Name(r.kid);
                if (c.icon != null) { var sp = WorldTree.EnemySprite(r.enemy); c.icon.sprite = sp; c.icon.enabled = sp != null; c.icon.preserveAspect = true; }
                if (c.desc != null) c.desc.text = r.done ? "고마워요!" : $"{WorldTree.EnemyName(r.enemy)} {r.count}개 주세요";
                if (c.reward != null) c.reward.text = $"{Wallet.Format(WorldTree.RequestGold)} 골드 · 호감도 +{GameBalance.ConstInt("requestAffinity")}";
                if (c.progress != null) c.progress.text = $"{Wallet.Format(System.Math.Min(have, r.count))} / {r.count}";
                if (c.barFill != null) c.barFill.anchorMax = new Vector2(r.count <= 0 ? 0f : Mathf.Clamp01(have / (float)r.count), c.barFill.anchorMax.y);
                if (c is KidRequestView kv && kv.kidIcon != null) { var ks = Formation.Icon(r.kid); kv.kidIcon.sprite = ks; kv.kidIcon.enabled = ks != null; kv.kidIcon.preserveAspect = true; }
                // 버튼은 늘 보인다: 줄 수 있으면 초록 "들어주기", 아니면 회색 "진행 중", 들어줬으면 회색 "완료"
                if (c.button != null) c.button.interactable = can;
                if (c.buttonText != null) c.buttonText.text = r.done ? "완료" : can ? "들어주기" : "진행 중";
            }
        }

        void DoRequest(int index)
        {
            var requests = WorldTree.Requests;
            if (index >= requests.Count) return;
            string kid = requests[index].kid;
            if (WorldTree.DoRequest(index)) Toast($"{Formation.Name(kid)}의 부탁을 들어줬어요!");
            Refresh();
        }

        // ── 주간 상자 ──
        void RefreshWeek()
        {
            if (weekButton != null) weekButton.interactable = WorldTree.CanClaimWeek;
            if (weekLabel != null)
                weekLabel.text = WorldTree.WeekClaimed ? "주간 상자 받음"
                    : $"주간 상자 {Mathf.Min(WorldTree.WeekDone, WorldTree.WeekGoal)}/{WorldTree.WeekGoal}";
        }

        void ClaimWeek()
        {
            if (WorldTree.ClaimWeek()) Toast($"균사석 {Wallet.Format(WorldTree.WeekMycelite)}을 받았어요!");
            Refresh();
        }

        // ── 보유 꼬마 ──
        void RefreshCells()
        {
            var owned = new List<string>(WorldTree.OwnedKids);
            if (emptyText != null) emptyText.gameObject.SetActive(owned.Count == 0);
            if (string.IsNullOrEmpty(selected) || !owned.Contains(selected)) selected = owned.Count > 0 ? owned[0] : null;
            while (cells.Count < owned.Count && cellTemplate != null && grid != null)
            {
                var c = Instantiate(cellTemplate, grid);
                c.gameObject.SetActive(true);
                if (c.button != null) c.button.onClick.AddListener(() => { selected = c.id; Refresh(); });
                cells.Add(c);
            }
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                bool on = i < owned.Count;
                c.gameObject.SetActive(on);
                if (!on) continue;
                string id = owned[i];
                c.id = id;
                c.name = "Kid_" + id;
                if (c.icon != null) { c.icon.sprite = Formation.Icon(id); c.icon.preserveAspect = true; }
                if (c.nameText != null) c.nameText.text = Formation.Name(id);
                if (c.levelText != null) c.levelText.text = "Lv." + Formation.Level(id);
                if (c.giftBadge != null) c.giftBadge.SetActive(CanGift(id));
                if (c.evolveBadge != null) c.evolveBadge.SetActive(WorldTree.CanKidEvolve(id) == WorldTree.KidFail.None);
                if (c.selection != null) c.selection.SetActive(id == selected);
            }
        }

        static bool CanGift(string id)
        {
            var (_, count) = WorldTree.GiftPick(id);
            return count > 0 && WorldTree.AffinityLevel(id) < WorldTree.MaxAffinityLevel;
        }

        // ── 고른 꼬마 정보 ──
        void RefreshInfo()
        {
            string id = selected;
            bool has = !string.IsNullOrEmpty(id);
            if (giftButton != null) giftButton.interactable = has && CanGift(id);
            if (evolveButton != null) evolveButton.interactable = has && WorldTree.CanKidEvolve(id) == WorldTree.KidFail.None;
            if (!has)
            {
                if (infoName != null) infoName.text = "꼬마가 없어요";
                if (infoIcon != null) infoIcon.enabled = false;
                if (infoLevel != null) infoLevel.text = "";
                if (infoAffinity != null) infoAffinity.text = "";
                if (infoBonus != null) infoBonus.text = "";
                if (infoEvolve != null) infoEvolve.text = "";
                return;
            }

            int star = WorldTree.Star(id);
            var row = WorldTree.KidRow(id);
            if (infoIcon != null) { infoIcon.enabled = true; infoIcon.sprite = Formation.Icon(id); infoIcon.preserveAspect = true; }
            if (infoLevel != null) infoLevel.text = $"Lv.{Formation.Level(id)} [{Mathf.Max(1, WorldTree.KidStep(id) + 1)}단계]";
            if (infoName != null) infoName.text = $"{Formation.Name(id)}  " + TreeUi.Colored(TreeUi.Stars(star, WorldTree.MaxStar), new Color(0.95f, 0.65f, 0.1f));

            int affLv = WorldTree.AffinityLevel(id);
            var (have, need) = WorldTree.AffinityProgress(id);
            if (infoAffinity != null) infoAffinity.text = need > 0 ? $"호감도 Lv {affLv}  ({have}/{need})" : $"호감도 Lv {affLv} (최고)";

            var sb = new StringBuilder();
            int step = WorldTree.KidStep(id);
            if (row != null) sb.Append("특기: ").Append(WorldTree.EffectText(row.bonusEffect, row.bonusValue * step)).Append(step == 0 ? " (진화하면)" : "");
            if (WorldTree.DecoSetComplete(id)) sb.Append(TreeUi.Colored("  장식 세트!", TreeUi.Good));
            if (infoBonus != null) infoBonus.text = sb.ToString();

            if (infoEvolve != null)
                infoEvolve.text = star >= WorldTree.MaxStar ? TreeUi.Colored("최고 등급", TreeUi.Good)
                    : "진화: " + TreeUi.Check(Formation.Level(id) >= WorldTree.EvoLevelNeeded(id), $"Lv {WorldTree.EvoLevelNeeded(id)}") + "  "
                      + TreeUi.Check(WorldTree.Stage >= WorldTree.EvoStageNeeded(id), $"세계수 {WorldTree.EvoStageNeeded(id)}단계") + "  "
                      + TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.EvoMyceliteCost(id)), "균사석 " + Wallet.Format(WorldTree.EvoMyceliteCost(id)));

            var (giftEnemy, giftCount) = WorldTree.GiftPick(id);
            if (giftLabel != null) giftLabel.text = CanGift(id) ? $"선물\n<size=70%>호감도 +{WorldTree.GiftValue(id, giftEnemy, giftCount)}</size>" : "선물";
            if (evolveLabel != null) evolveLabel.text = star >= WorldTree.MaxStar ? "진화" : $"진화 ★{star + 1}";
        }

        void Gift()
        {
            if (string.IsNullOrEmpty(selected)) return;
            int gain = WorldTree.Gift(selected);
            if (gain > 0) Toast($"{Formation.Name(selected)} 호감도 +{gain}");
            Refresh();
        }

        void Evolve()
        {
            if (string.IsNullOrEmpty(selected)) return;
            if (WorldTree.KidEvolve(selected)) Toast($"{Formation.Name(selected)}가 ★{WorldTree.Star(selected)}로 진화했어요!");
            Refresh();
        }

        static void Toast(string message)
        {
            var screen = FindAnyObjectByType<WorldTreeScreen>();
            if (screen != null) screen.ShowToast(message);
        }
    }
}
