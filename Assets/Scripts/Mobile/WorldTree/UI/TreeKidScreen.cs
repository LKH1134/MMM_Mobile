using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-05] 세계수 꼬마 시트 (편성 화면과 같은 배치, 프리팹 KidPopup의 Sheet/Content/KidScreen):
    //   위 4칸 = 오늘의 부탁(홈 일일 의뢰 카드 모양) · 가운데 = 고른 꼬마 정보(특기·진화 조건 + 진화 버튼) ·
    //   주간 상자 버튼 · 아래 = 보유 꼬마 칸(누르면 가운데 정보가 바뀐다, 레벨 옆 [진화 가능]).
    // 모양은 프리팹에서 고치고, 값과 버튼 동작만 여기서 넣는다. 이 화면이 켜져 있으면 예전 TreeKidPanel은 꺼 둔다
    public class TreeKidScreen : MonoBehaviour
    {
        [Header("오늘의 부탁 (왼쪽부터, 부탁보다 많은 칸은 숨긴다)")]
        [SerializeField] QuestCardView[] requestCells;

        [Header("고른 꼬마 정보")]
        [SerializeField] Image infoIcon;
        [SerializeField] TMP_Text infoLevel;
        [SerializeField] TMP_Text infoName;        // 이름 ★ (단계는 infoLevel에 "Lv.n [n단계]")
        [SerializeField] TMP_Text infoBonus;       // 특기
        [SerializeField] TMP_Text infoEvolve;      // 진화 조건
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
            if (evolveButton != null) { UiSkin.GreyWhenDisabled(evolveButton); evolveButton.onClick.AddListener(Evolve); }
            if (weekButton != null) { UiSkin.GreyWhenDisabled(weekButton); weekButton.onClick.AddListener(ClaimWeek); }
        }

        void OnEnable()
        {
            WorldTree.Changed += MarkDirty;
            Formation.Changed += MarkDirty;
            Wallet.Changed += MarkDirty;
            Loc.Changed += MarkDirty;
            Refresh();
        }

        void OnDisable()
        {
            WorldTree.Changed -= MarkDirty;
            Formation.Changed -= MarkDirty;
            Wallet.Changed -= MarkDirty;
            Loc.Changed -= MarkDirty;
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
                if (c.desc != null) c.desc.text = r.done ? Loc.T("kid.thanks") : Loc.F("kid.request_desc", WorldTree.EnemyName(r.enemy), r.count);
                if (c.reward != null) c.reward.text = Loc.F("kid.reward_line", Wallet.Format(WorldTree.RequestGold));
                if (c.progress != null) c.progress.text = $"{Wallet.Format(System.Math.Min(have, r.count))} / {r.count}";
                if (c.barFill != null) c.barFill.anchorMax = new Vector2(r.count <= 0 ? 0f : Mathf.Clamp01(have / (float)r.count), c.barFill.anchorMax.y);
                if (c is KidRequestView kv && kv.kidIcon != null) { var ks = Formation.Icon(r.kid); kv.kidIcon.sprite = ks; kv.kidIcon.enabled = ks != null; kv.kidIcon.preserveAspect = true; }
                // 버튼은 늘 보인다: 줄 수 있으면 초록 "들어주기", 아니면 회색 "진행 중", 들어줬으면 회색 "완료"
                if (c.button != null) c.button.interactable = can;
                if (c.buttonText != null) c.buttonText.text = r.done ? Loc.T("tree.done") : can ? Loc.T("kid.help") : Loc.T("treequest.in_progress");
            }
        }

        void DoRequest(int index)
        {
            var requests = WorldTree.Requests;
            if (index >= requests.Count) return;
            string kid = requests[index].kid;
            if (WorldTree.DoRequest(index)) Toast.Post(Loc.F("kid.help_toast", Formation.Name(kid)));
            Refresh();
        }

        // ── 주간 상자 ──
        void RefreshWeek()
        {
            if (weekButton != null) weekButton.interactable = WorldTree.CanClaimWeek;
            if (weekLabel != null)
                weekLabel.text = WorldTree.WeekClaimed ? Loc.T("kid.week_claimed_label")
                    : Loc.F("kid.week_label", Mathf.Min(WorldTree.WeekDone, WorldTree.WeekGoal), WorldTree.WeekGoal);
        }

        void ClaimWeek()
        {
            if (WorldTree.ClaimWeek()) Toast.Post(Loc.F("kid.claim_toast", Loc.F("tree.mycelite", Wallet.Format(WorldTree.WeekMycelite))));
            Refresh();
        }

        // ── 보유 꼬마 ──
        void RefreshCells()
        {
            var owned = new List<string>(WorldTree.OwnedKids);
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(owned.Count == 0);
                if (owned.Count == 0) emptyText.text = Loc.T("kid.no_kids");
            }
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
                if (c.evolveBadge != null) c.evolveBadge.SetActive(WorldTree.CanKidEvolve(id) == WorldTree.KidFail.None);
                if (c.selection != null) c.selection.SetActive(id == selected);
            }
        }

        // ── 고른 꼬마 정보 ──
        void RefreshInfo()
        {
            string id = selected;
            bool has = !string.IsNullOrEmpty(id);
            if (evolveButton != null) evolveButton.interactable = has && WorldTree.CanKidEvolve(id) == WorldTree.KidFail.None;
            if (!has)
            {
                if (infoName != null) infoName.text = Loc.T("kid.no_kid");
                if (infoIcon != null) infoIcon.enabled = false;
                if (infoLevel != null) infoLevel.text = "";
                if (infoBonus != null) infoBonus.text = "";
                if (infoEvolve != null) infoEvolve.text = "";
                return;
            }

            int star = WorldTree.Star(id);
            var row = WorldTree.KidRow(id);
            if (infoIcon != null) { infoIcon.enabled = true; infoIcon.sprite = Formation.Icon(id); infoIcon.preserveAspect = true; }
            if (infoLevel != null) infoLevel.text = Loc.F("kid.level_step", Formation.Level(id), Mathf.Max(1, WorldTree.KidStep(id) + 1));
            if (infoName != null) infoName.text = $"{Formation.Name(id)}  " + TreeUi.Colored(TreeUi.Stars(star, WorldTree.MaxStar), new Color(0.95f, 0.65f, 0.1f));

            var sb = new StringBuilder();
            int step = WorldTree.KidStep(id);
            if (row != null) sb.Append(Loc.F(step == 0 ? "kid.talent_locked" : "kid.talent", WorldTree.EffectText(row.bonusEffect, row.bonusValue * step)));
            if (WorldTree.DecoSetComplete(id)) sb.Append(TreeUi.Colored("  " + Loc.T("kid.deco_set"), TreeUi.Good));
            if (infoBonus != null) infoBonus.text = sb.ToString();

            if (infoEvolve != null)
                infoEvolve.text = star >= WorldTree.MaxStar ? TreeUi.Colored(Loc.T("kid.max_rank"), TreeUi.Good)
                    : Loc.F("kid.evolve_cond", TreeUi.Check(Formation.Level(id) >= WorldTree.EvoLevelNeeded(id), $"Lv {WorldTree.EvoLevelNeeded(id)}") + "  "
                      + TreeUi.Check(WorldTree.Stage >= WorldTree.EvoStageNeeded(id), Loc.F("kid.need_stage", WorldTree.EvoStageNeeded(id))) + "  "
                      + TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.EvoMyceliteCost(id)), Loc.F("tree.mycelite", Wallet.Format(WorldTree.EvoMyceliteCost(id)))));

            if (evolveLabel != null) evolveLabel.text = star >= WorldTree.MaxStar ? Loc.T("kid.evolve") : Loc.F("kid.evolve_star", star + 1);
        }

        void Evolve()
        {
            if (string.IsNullOrEmpty(selected)) return;
            if (WorldTree.KidEvolve(selected)) Toast.Post(Loc.F("kid.evolve_toast", Formation.Name(selected), WorldTree.Star(selected)));
            Refresh();
        }

    }
}
