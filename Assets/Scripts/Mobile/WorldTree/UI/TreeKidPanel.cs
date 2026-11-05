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

            SetText(Use("Header"), null, "오늘의 부탁");
            var requests = WorldTree.Requests;
            if (requests.Count == 0) SetText(Use("Empty"), "Text", "꼬마가 생기면 부탁을 해요");
            for (int i = 0; i < requests.Count; i++)
            {
                var r = requests[i];
                int index = i;
                var card = Use("RequestCard");
                CardOff(card, r.done);
                SetIcon(card, "Icon", Formation.Icon(r.kid));
                long have = WorldTree.StoreCount(r.enemy);
                SetText(card, "Text", $"{Formation.Name(r.kid)}: {WorldTree.EnemyName(r.enemy)} {r.count}개 주세요");
                SetText(card, "Sub", r.done ? "고마워요!" : TreeUi.Check(have >= r.count, $"창고 {Wallet.Format(have)}/{r.count}") + $"  ·  보상 {Gold(WorldTree.RequestGold)} · 호감도 +{GameBalance.ConstInt("requestAffinity")}");
                SetButton(card, "Button", r.done ? "완료" : "들어주기", !r.done && WorldTree.CanDoRequest(index), () =>
                {
                    if (WorldTree.DoRequest(index)) Toast($"{Formation.Name(r.kid)}의 부탁을 들어줬어요!");
                });
            }
            var week = Use("WeekCard");
            SetText(week, "Title", $"주간 상자  {Mathf.Min(WorldTree.WeekDone, WorldTree.WeekGoal)}/{WorldTree.WeekGoal}");
            SetText(week, "Desc", $"이번 주 부탁 {WorldTree.WeekGoal}번 → {Mycelite(WorldTree.WeekMycelite)}");
            SetButton(week, "Button", WorldTree.WeekClaimed ? "받음" : "받기", WorldTree.CanClaimWeek, () =>
            {
                if (WorldTree.ClaimWeek()) Toast($"{Mycelite(WorldTree.WeekMycelite)}을 받았어요!");
            });

            SetText(Use("Header"), null, "꼬마");
            bool any = false;
            foreach (var id in WorldTree.OwnedKids) { any = true; KidCardFromTemplate(id); }
            if (!any) SetText(Use("Empty"), "Text", "뽑기에서 꼬마를 데려오세요");
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
            SetText(card, "Affinity", need > 0 ? $"호감도 Lv {affLv}  ({have}/{need})" : $"호감도 Lv {affLv} (최고)");
            SetBar(card, "Bar/Fill", need > 0 ? have / (float)need : 1f);
            var sb = new StringBuilder();
            int step = WorldTree.KidStep(id);
            if (row != null) sb.Append("특기: ").Append(WorldTree.EffectText(row.bonusEffect, row.bonusValue * step)).Append(step == 0 ? " (진화하면)" : "");
            if (WorldTree.DecoSetComplete(id)) sb.Append(TreeUi.Colored("  장식 세트!", TreeUi.Good));
            SetText(card, "Bonus", sb.ToString());
            SetText(card, "Evolve", star >= WorldTree.MaxStar ? TreeUi.Colored("최고 등급", TreeUi.Good)
                : "진화: " + TreeUi.Check(Formation.Level(id) >= WorldTree.EvoLevelNeeded(id), $"Lv {WorldTree.EvoLevelNeeded(id)}") + "  "
                  + TreeUi.Check(WorldTree.Stage >= WorldTree.EvoStageNeeded(id), $"세계수 {WorldTree.EvoStageNeeded(id)}단계") + "  "
                  + TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.EvoMyceliteCost(id)), Mycelite(WorldTree.EvoMyceliteCost(id))));
            var (giftEnemy, giftCount) = WorldTree.GiftPick(id);
            bool canGift = giftCount > 0 && affLv < WorldTree.MaxAffinityLevel;
            SetButton(card, "GiftButton", canGift ? $"선물\n+{WorldTree.GiftValue(id, giftEnemy, giftCount)}" : "선물", canGift, () =>
            {
                int gain = WorldTree.Gift(id);
                if (gain > 0) Toast($"{Formation.Name(id)} 호감도 +{gain}");
            });
            SetButton(card, "EvolveButton", star >= WorldTree.MaxStar ? "진화" : $"진화 ★{star + 1}", WorldTree.CanKidEvolve(id) == WorldTree.KidFail.None, () =>
            {
                if (WorldTree.KidEvolve(id)) Toast($"{Formation.Name(id)}가 ★{WorldTree.Star(id)}로 진화했어요!");
            });
        }

        // 견본이 없을 때: 예전 코드 화면
        void BuildByCode()
        {
            // 부탁
            TreeUi.Header(list, "오늘의 부탁", font);
            var requests = WorldTree.Requests;
            if (requests.Count == 0)
            {
                var empty = TreeUi.Card(list, 110f);
                TreeUi.Line(empty, "꼬마가 생기면 부탁을 해요", 30f, TreeUi.Faint, font, 32f, 24f, 0f, 1f);
            }
            for (int i = 0; i < requests.Count; i++)
            {
                var r = requests[i];
                int index = i;
                var card = TreeUi.Card(list, 150f, !r.done);
                TreeUi.Icon(card, Formation.Icon(r.kid), 110f);
                long have = WorldTree.StoreCount(r.enemy);
                TreeUi.Line(card, $"{Formation.Name(r.kid)}: {WorldTree.EnemyName(r.enemy)} {r.count}개 주세요", 32f, TreeUi.Ink, font, 150f, 260f, 0.5f, 0.92f);
                TreeUi.Line(card, r.done ? "고마워요!" : TreeUi.Check(have >= r.count, $"창고 {Wallet.Format(have)}/{r.count}") + $"  ·  보상 {Gold(WorldTree.RequestGold)} · 호감도 +{GameBalance.ConstInt("requestAffinity")}",
                    26f, TreeUi.Faint, font, 150f, 260f, 0.08f, 0.5f);
                TreeUi.Button(card, r.done ? "완료" : "들어주기", !r.done && WorldTree.CanDoRequest(index), () =>
                {
                    if (WorldTree.DoRequest(index)) Toast($"{Formation.Name(r.kid)}의 부탁을 들어줬어요!");
                }, font);
            }
            var week = TreeUi.Card(list, 130f);
            TreeUi.Line(week, $"주간 상자  {Mathf.Min(WorldTree.WeekDone, WorldTree.WeekGoal)}/{WorldTree.WeekGoal}", 34f, TreeUi.Ink, font, 32f, 260f, 0.5f, 0.92f).fontStyle = FontStyles.Bold;
            TreeUi.Line(week, $"이번 주 부탁 {WorldTree.WeekGoal}번 → {Mycelite(WorldTree.WeekMycelite)}", 26f, TreeUi.Faint, font, 32f, 260f, 0.1f, 0.5f);
            TreeUi.Button(week, WorldTree.WeekClaimed ? "받음" : "받기", WorldTree.CanClaimWeek, () =>
            {
                if (WorldTree.ClaimWeek()) Toast($"{Mycelite(WorldTree.WeekMycelite)}을 받았어요!");
            }, font, tint: TreeUi.ButtonAlt);

            // 꼬마
            TreeUi.Header(list, "꼬마", font);
            bool any = false;
            foreach (var id in WorldTree.OwnedKids)
            {
                any = true;
                KidCard(id);
            }
            if (!any)
            {
                var empty = TreeUi.Card(list, 110f);
                TreeUi.Line(empty, "뽑기에서 꼬마를 데려오세요", 30f, TreeUi.Faint, font, 32f, 24f, 0f, 1f);
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
            TreeUi.Line(card, need > 0 ? $"호감도 Lv {affLv}  ({have}/{need})" : $"호감도 Lv {affLv} (최고)", 30f, TreeUi.Ink, font, 196f, 290f, 0.62f, 0.78f);
            TreeUi.Bar(card, need > 0 ? have / (float)need : 1f, new Color(0.95f, 0.5f, 0.6f), new Vector2(196f, -14f), new Vector2(-290f, 0f), 0.6f);

            // 특기
            var sb = new StringBuilder();
            int step = WorldTree.KidStep(id);
            if (row != null)
                sb.Append("특기: ").Append(WorldTree.EffectText(row.bonusEffect, row.bonusValue * step)).Append(step == 0 ? " (진화하면)" : "").Append('\n');
            if (WorldTree.DecoSetComplete(id)) sb.Append(TreeUi.Colored("  장식 세트!", TreeUi.Good));
            TreeUi.Line(card, sb.ToString(), 26f, TreeUi.Faint, font, 196f, 290f, 0.3f, 0.56f);

            // 진화 조건
            string evo;
            if (star >= WorldTree.MaxStar) evo = TreeUi.Colored("최고 등급", TreeUi.Good);
            else
                evo = "진화: " + TreeUi.Check(Formation.Level(id) >= WorldTree.EvoLevelNeeded(id), $"Lv {WorldTree.EvoLevelNeeded(id)}") + "  "
                    + TreeUi.Check(WorldTree.Stage >= WorldTree.EvoStageNeeded(id), $"세계수 {WorldTree.EvoStageNeeded(id)}단계") + "  "
                    + TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.EvoMyceliteCost(id)), Mycelite(WorldTree.EvoMyceliteCost(id)));
            TreeUi.Line(card, evo, 26f, TreeUi.Ink, font, 32f, 290f, 0.04f, 0.28f);

            // 버튼: 위 선물, 아래 진화
            var (giftEnemy, giftCount) = WorldTree.GiftPick(id);
            bool canGift = giftCount > 0 && affLv < WorldTree.MaxAffinityLevel;
            string giftLabel = canGift ? $"선물\n+{WorldTree.GiftValue(id, giftEnemy, giftCount)}" : "선물";
            var gift = TreeUi.Button(card, giftLabel, canGift, () =>
            {
                int gain = WorldTree.Gift(id);
                if (gain > 0) Toast($"{Formation.Name(id)} 호감도 +{gain}");
            }, font, height: 120f, tint: new Color(0.95f, 0.55f, 0.65f));
            ((RectTransform)gift.transform).anchoredPosition = new Vector2(-24f, 70f);
            var evolve = TreeUi.Button(card, star >= WorldTree.MaxStar ? "진화" : $"진화 ★{star + 1}", WorldTree.CanKidEvolve(id) == WorldTree.KidFail.None, () =>
            {
                if (WorldTree.KidEvolve(id)) Toast($"{Formation.Name(id)}가 ★{WorldTree.Star(id)}로 진화했어요!");
            }, font, height: 120f, tint: TreeUi.ButtonAlt);
            ((RectTransform)evolve.transform).anchoredPosition = new Vector2(-24f, -70f);
        }
    }
}
