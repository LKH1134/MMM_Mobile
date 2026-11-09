using System.Text;
using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 꼬마 시트: 오늘의 부탁(창고 버섯 → 골드·호감도)과 주간 상자, 가진 꼬마마다 호감도(선물)·등급(★)·진화·특기
    public class TreeKidPanel : TreePanel
    {
        // [UI 개편 2026-11-05] 카드 모양은 프리팹 KidPopup의 Sheet/Content/Templates에서 고친다 (자식 이름을 지켜야 값이 들어간다).
        //   Header: (글자)   Empty: Text   RequestCard: Icon · Text · Sub · Button   WeekCard: Title · Desc · Button
        //   KidCard: Icon · Title · Affinity · Bar/Fill · Bonus · Evolve · GiftButton · EvolveButton
        static readonly string[] TemplateNames = { "Header", "Empty", "RequestCard", "WeekCard", "KidCard" };

        protected override void Build()
        {
            if (!HasTemplates(TemplateNames)) { BuildByCode(); return; }

            SetText(Use("Header"), null, Loc.T("kid.today_request"));
            var requests = WorldTree.Requests;
            if (requests.Count == 0) SetText(Use("Empty"), "Text", Loc.T("kid.no_requests"));
            for (int i = 0; i < requests.Count; i++)
            {
                var r = requests[i];
                int index = i;
                var card = Use("RequestCard");
                CardOff(card, r.done);
                SetIcon(card, "Icon", Formation.Icon(r.kid));
                long have = WorldTree.StoreCount(r.enemy);
                SetText(card, "Text", Loc.F("kid.request_text", Formation.Name(r.kid), WorldTree.EnemyName(r.enemy), r.count));
                SetText(card, "Sub", r.done ? Loc.T("kid.thanks") : TreeUi.Check(have >= r.count, Loc.F("kid.storage", Wallet.Format(have), r.count)) + "  ·  " + Loc.F("kid.reward", Gold(WorldTree.RequestGold), GameBalance.ConstInt("requestAffinity")));
                SetButton(card, "Button", r.done ? Loc.T("tree.done") : Loc.T("kid.help"), !r.done && WorldTree.CanDoRequest(index), () =>
                {
                    if (WorldTree.DoRequest(index)) Toast(Loc.F("kid.help_toast", Formation.Name(r.kid)));
                });
            }
            var week = Use("WeekCard");
            SetText(week, "Title", Loc.F("kid.week_title", Mathf.Min(WorldTree.WeekDone, WorldTree.WeekGoal), WorldTree.WeekGoal));
            SetText(week, "Desc", Loc.F("kid.week_desc", WorldTree.WeekGoal, Mycelite(WorldTree.WeekMycelite)));
            SetButton(week, "Button", WorldTree.WeekClaimed ? Loc.T("tree.claimed") : Loc.T("tree.claim"), WorldTree.CanClaimWeek, () =>
            {
                if (WorldTree.ClaimWeek()) Toast(Loc.F("kid.claim_toast", Mycelite(WorldTree.WeekMycelite)));
            });

            SetText(Use("Header"), null, Loc.T("kid.title"));
            bool any = false;
            foreach (var id in WorldTree.OwnedKids) { any = true; KidCardFromTemplate(id); }
            if (!any) SetText(Use("Empty"), "Text", Loc.T("kid.no_kids"));
        }

        void KidCardFromTemplate(string id)
        {
            var row = WorldTree.KidRow(id);
            int star = WorldTree.Star(id);
            var card = Use("KidCard");
            SetIcon(card, "Icon", Formation.Icon(id));
            SetText(card, "Title", $"{Formation.Name(id)}  Lv {Formation.Level(id)}  " + TreeUi.Colored(TreeUi.Stars(star, WorldTree.MaxStar), new Color(0.95f, 0.65f, 0.1f)));
            int affLv = WorldTree.AffinityLevel(id);
            var (have, need) = WorldTree.AffinityProgress(id);
            SetText(card, "Affinity", need > 0 ? Loc.F("kid.affinity_lv", affLv, have, need) : Loc.F("kid.affinity_max", affLv));
            SetBar(card, "Bar/Fill", need > 0 ? have / (float)need : 1f);
            var sb = new StringBuilder();
            int step = WorldTree.KidStep(id);
            if (row != null) sb.Append(Loc.F(step == 0 ? "kid.talent_locked" : "kid.talent", WorldTree.EffectText(row.bonusEffect, row.bonusValue * step)));
            if (WorldTree.DecoSetComplete(id)) sb.Append(TreeUi.Colored("  " + Loc.T("kid.deco_set"), TreeUi.Good));
            SetText(card, "Bonus", sb.ToString());
            SetText(card, "Evolve", star >= WorldTree.MaxStar ? TreeUi.Colored(Loc.T("kid.max_rank"), TreeUi.Good)
                : Loc.F("kid.evolve_cond", TreeUi.Check(Formation.Level(id) >= WorldTree.EvoLevelNeeded(id), $"Lv {WorldTree.EvoLevelNeeded(id)}") + "  "
                  + TreeUi.Check(WorldTree.Stage >= WorldTree.EvoStageNeeded(id), Loc.F("kid.need_stage", WorldTree.EvoStageNeeded(id))) + "  "
                  + TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.EvoMyceliteCost(id)), Mycelite(WorldTree.EvoMyceliteCost(id)))));
            var (giftEnemy, giftCount) = WorldTree.GiftPick(id);
            bool canGift = giftCount > 0 && affLv < WorldTree.MaxAffinityLevel;
            SetButton(card, "GiftButton", canGift ? Loc.F("kid.gift_plus", WorldTree.GiftValue(id, giftEnemy, giftCount)) : Loc.T("kid.gift"), canGift, () =>
            {
                int gain = WorldTree.Gift(id);
                if (gain > 0) Toast(Loc.F("kid.gift_toast", Formation.Name(id), gain));
            });
            SetButton(card, "EvolveButton", star >= WorldTree.MaxStar ? Loc.T("kid.evolve") : Loc.F("kid.evolve_star", star + 1), WorldTree.CanKidEvolve(id) == WorldTree.KidFail.None, () =>
            {
                if (WorldTree.KidEvolve(id)) Toast(Loc.F("kid.evolve_toast", Formation.Name(id), WorldTree.Star(id)));
            });
        }

        // 견본이 없을 때: 예전 코드 화면
        void BuildByCode()
        {
            // 부탁
            TreeUi.Header(list, Loc.T("kid.today_request"), font);
            var requests = WorldTree.Requests;
            if (requests.Count == 0)
            {
                var empty = TreeUi.Card(list, 110f);
                TreeUi.Line(empty, Loc.T("kid.no_requests"), 30f, TreeUi.Faint, font, 32f, 24f, 0f, 1f);
            }
            for (int i = 0; i < requests.Count; i++)
            {
                var r = requests[i];
                int index = i;
                var card = TreeUi.Card(list, 150f, !r.done);
                TreeUi.Icon(card, Formation.Icon(r.kid), 110f);
                long have = WorldTree.StoreCount(r.enemy);
                TreeUi.Line(card, Loc.F("kid.request_text", Formation.Name(r.kid), WorldTree.EnemyName(r.enemy), r.count), 32f, TreeUi.Ink, font, 150f, 260f, 0.5f, 0.92f);
                TreeUi.Line(card, r.done ? Loc.T("kid.thanks") : TreeUi.Check(have >= r.count, Loc.F("kid.storage", Wallet.Format(have), r.count)) + "  ·  " + Loc.F("kid.reward", Gold(WorldTree.RequestGold), GameBalance.ConstInt("requestAffinity")),
                    26f, TreeUi.Faint, font, 150f, 260f, 0.08f, 0.5f);
                TreeUi.Button(card, r.done ? Loc.T("tree.done") : Loc.T("kid.help"), !r.done && WorldTree.CanDoRequest(index), () =>
                {
                    if (WorldTree.DoRequest(index)) Toast(Loc.F("kid.help_toast", Formation.Name(r.kid)));
                }, font);
            }
            var week = TreeUi.Card(list, 130f);
            TreeUi.Line(week, Loc.F("kid.week_title", Mathf.Min(WorldTree.WeekDone, WorldTree.WeekGoal), WorldTree.WeekGoal), 34f, TreeUi.Ink, font, 32f, 260f, 0.5f, 0.92f).fontStyle = FontStyles.Bold;
            TreeUi.Line(week, Loc.F("kid.week_desc", WorldTree.WeekGoal, Mycelite(WorldTree.WeekMycelite)), 26f, TreeUi.Faint, font, 32f, 260f, 0.1f, 0.5f);
            TreeUi.Button(week, WorldTree.WeekClaimed ? Loc.T("tree.claimed") : Loc.T("tree.claim"), WorldTree.CanClaimWeek, () =>
            {
                if (WorldTree.ClaimWeek()) Toast(Loc.F("kid.claim_toast", Mycelite(WorldTree.WeekMycelite)));
            }, font, tint: TreeUi.ButtonAlt);

            // 꼬마
            TreeUi.Header(list, Loc.T("kid.title"), font);
            bool any = false;
            foreach (var id in WorldTree.OwnedKids)
            {
                any = true;
                KidCard(id);
            }
            if (!any)
            {
                var empty = TreeUi.Card(list, 110f);
                TreeUi.Line(empty, Loc.T("kid.no_kids"), 30f, TreeUi.Faint, font, 32f, 24f, 0f, 1f);
            }
        }

        void KidCard(string id)
        {
            var row = WorldTree.KidRow(id);
            int star = WorldTree.Star(id);
            var card = TreeUi.Card(list, 330f);
            var icon = TreeUi.Icon(card, Formation.Icon(id), 150f);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 1f);
            icon.rectTransform.pivot = new Vector2(0f, 1f);
            icon.rectTransform.anchoredPosition = new Vector2(24f, -24f);

            var title = TreeUi.Line(card, $"{Formation.Name(id)}  Lv {Formation.Level(id)}  " + TreeUi.Colored(TreeUi.Stars(star, WorldTree.MaxStar), new Color(0.95f, 0.65f, 0.1f)),
                38f, TreeUi.Ink, font, 196f, 24f, 0.78f, 0.95f);
            title.fontStyle = FontStyles.Bold;

            // 호감도
            int affLv = WorldTree.AffinityLevel(id);
            var (have, need) = WorldTree.AffinityProgress(id);
            TreeUi.Line(card, need > 0 ? Loc.F("kid.affinity_lv", affLv, have, need) : Loc.F("kid.affinity_max", affLv), 30f, TreeUi.Ink, font, 196f, 290f, 0.62f, 0.78f);
            TreeUi.Bar(card, need > 0 ? have / (float)need : 1f, new Color(0.95f, 0.5f, 0.6f), new Vector2(196f, -14f), new Vector2(-290f, 0f), 0.6f);

            // 특기
            var sb = new StringBuilder();
            int step = WorldTree.KidStep(id);
            if (row != null)
                sb.Append(Loc.F(step == 0 ? "kid.talent_locked" : "kid.talent", WorldTree.EffectText(row.bonusEffect, row.bonusValue * step))).Append('\n');
            if (WorldTree.DecoSetComplete(id)) sb.Append(TreeUi.Colored("  " + Loc.T("kid.deco_set"), TreeUi.Good));
            TreeUi.Line(card, sb.ToString(), 26f, TreeUi.Faint, font, 196f, 290f, 0.3f, 0.56f);

            // 진화 조건
            string evo;
            if (star >= WorldTree.MaxStar) evo = TreeUi.Colored(Loc.T("kid.max_rank"), TreeUi.Good);
            else
                evo = Loc.F("kid.evolve_cond", TreeUi.Check(Formation.Level(id) >= WorldTree.EvoLevelNeeded(id), $"Lv {WorldTree.EvoLevelNeeded(id)}") + "  "
                    + TreeUi.Check(WorldTree.Stage >= WorldTree.EvoStageNeeded(id), Loc.F("kid.need_stage", WorldTree.EvoStageNeeded(id))) + "  "
                    + TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.EvoMyceliteCost(id)), Mycelite(WorldTree.EvoMyceliteCost(id))));
            TreeUi.Line(card, evo, 26f, TreeUi.Ink, font, 32f, 290f, 0.04f, 0.28f);

            // 버튼: 위 선물, 아래 진화
            var (giftEnemy, giftCount) = WorldTree.GiftPick(id);
            bool canGift = giftCount > 0 && affLv < WorldTree.MaxAffinityLevel;
            string giftLabel = canGift ? Loc.F("kid.gift_plus", WorldTree.GiftValue(id, giftEnemy, giftCount)) : Loc.T("kid.gift");
            var gift = TreeUi.Button(card, giftLabel, canGift, () =>
            {
                int gain = WorldTree.Gift(id);
                if (gain > 0) Toast(Loc.F("kid.gift_toast", Formation.Name(id), gain));
            }, font, height: 120f, tint: new Color(0.95f, 0.55f, 0.65f));
            ((RectTransform)gift.transform).anchoredPosition = new Vector2(-24f, 70f);
            var evolve = TreeUi.Button(card, star >= WorldTree.MaxStar ? Loc.T("kid.evolve") : Loc.F("kid.evolve_star", star + 1), WorldTree.CanKidEvolve(id) == WorldTree.KidFail.None, () =>
            {
                if (WorldTree.KidEvolve(id)) Toast(Loc.F("kid.evolve_toast", Formation.Name(id), WorldTree.Star(id)));
            }, font, height: 120f, tint: TreeUi.ButtonAlt);
            ((RectTransform)evolve.transform).anchoredPosition = new Vector2(-24f, -70f);
        }
    }
}
