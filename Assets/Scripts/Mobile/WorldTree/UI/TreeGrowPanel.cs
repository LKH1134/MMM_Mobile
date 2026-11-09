using System.Text;
using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 성장 시트: 지금 단계·나이테·경험치, 나이테 성장, 공물(창고 버섯 → 경험치), 진화, 가호 목록, 창고
    public class TreeGrowPanel : TreePanel
    {
        // [UI 개편 2026-11-05] 카드 바탕 In_Color_Beige1, 아이콘은 카드 왼쪽 (글자는 그 오른쪽부터)
        const string Beige = TreeUi.CardBeigeSprite;
        const string RingIcon = "UI/Icon/level_up";
        const string TributeIcon = "UI/Icon/gift";
        const float IconSize = 110f;
        const float TextLeft = 150f;

        // [UI 개편 2026-11-05] 카드 모양은 프리팹 GrowPopup의 Sheet/Content/Templates에서 고친다 (아래 이름을 지켜야 값이 들어간다).
        //   CurrentCard: Icon · Title · Ring · Max · Bar/Fill · Exp      RingCard: Icon · Title · Desc · Button/Text
        //   TributeCard: Icon · Title · Desc · Prefer · Button/Text      EvolveCard: Icon · Title · Conditions · Unlocks · Button/Text
        //   Header: (글자)   BlessingRow: Stage · Effect   StoreEmpty: Text   StoreRow: Icon · Name · Count
        // 견본이 하나라도 없으면 예전처럼 코드로 그린다 (BuildByCode)
        static readonly string[] TemplateNames = { "CurrentCard", "RingCard", "TributeCard", "EvolveCard", "Header", "BlessingRow", "StoreEmpty", "StoreRow" };
        protected override void Build()
        {
            if (!HasTemplates(TemplateNames)) { BuildByCode(); return; }
            var row = WorldTree.CurrentRow;
            bool max = WorldTree.IsMaxStage;

            // 지금 단계
            var card = Use("CurrentCard");
            SetIcon(card, "Icon", WorldTree.TreeSprite(WorldTree.Stage));
            SetText(card, "Title", Loc.F("grow.stage_name", WorldTree.Stage, row.Name));
            SetText(card, "Ring", Loc.F("grow.ring_lv", WorldTree.Ring, WorldTree.RingMax));
            Show(card, "Max", max);
            Show(card, "Bar", !max);
            Show(card, "Exp", !max);
            if (!max)
            {
                float exp = WorldTree.EvoExp > 0 ? (float)(WorldTree.Exp / WorldTree.EvoExp) : 1f;
                SetBar(card, "Bar/Fill", exp);
                SetText(card, "Exp", Loc.F("grow.exp_of", Wallet.Format((long)WorldTree.Exp), Wallet.Format(WorldTree.EvoExp)));
            }

            // 나이테
            card = Use("RingCard");
            if (WorldTree.IsMaxRing) SetButton(card, "Button", Loc.T("grow.max"), false, null);
            else SetButton(card, "Button", Loc.F("grow.ring_up", Gold(WorldTree.RingCost)), WorldTree.CanRingUp() == WorldTree.GrowFail.None, () =>
            {
                if (WorldTree.RingUp()) Toast(Loc.F("grow.ring_toast", WorldTree.Ring));
            });

            // 공물
            card = Use("TributeCard");
            SetText(card, "Desc", Loc.F("grow.tribute_desc", Wallet.Format(WorldTree.StoreTotal), Wallet.Format((long)WorldTree.TributePreview)));
            SetText(card, "Prefer", Loc.F("grow.tribute_prefer", GameBalance.Const("tributePreferMul").ToString("0.#"), PreferNames(row)));
            SetButton(card, "Button", null, WorldTree.CanTribute, () =>
            {
                double exp = WorldTree.Tribute();
                if (exp > 0) Toast(Loc.F("grow.exp_toast", Wallet.Format((long)exp)));
            });

            // 진화
            if (!max)
            {
                var next = GameBalance.TreeStages[WorldTree.Stage];
                card = Use("EvolveCard");
                SetIcon(card, "Icon", WorldTree.TreeSprite(next.stage));
                SetText(card, "Title", Loc.F("grow.evolve_title", next.stage, next.Name));
                SetText(card, "Conditions", EvolveConditions());
                SetText(card, "Unlocks", NextUnlocks(row, next));
                SetButton(card, "Button", null, WorldTree.CanEvolve() == WorldTree.GrowFail.None, () =>
                {
                    if (WorldTree.Evolve()) Toast(Loc.F("grow.evolve_toast", WorldTree.CurrentRow.Name));
                });
            }

            // 가호
            SetText(Use("Header"), null, Loc.T("grow.blessing_header"));
            foreach (var b in GameBalance.Blessings)
            {
                bool on = WorldTree.BlessActive(b);
                card = Use("BlessingRow");
                CardOff(card, !on);   // 꺼진 가호 = 회색
                var stage = SetText(card, "Stage", Loc.F("grow.stage_n", b.stage));
                if (stage != null) stage.color = on ? TreeUi.Good : TreeUi.Faint;
                var effect = SetText(card, "Effect", WorldTree.EffectText(b.effect, b.value));
                if (effect != null && !on) effect.color = TreeUi.Faint;
            }

            // 창고
            SetText(Use("Header"), null, Loc.F("grow.store_header", Wallet.Format(WorldTree.StoreTotal)));
            if (WorldTree.StoreTotal <= 0) Use("StoreEmpty");
            foreach (var e in GameBalance.Enemies)
            {
                long n = WorldTree.StoreCount(e.index);
                if (n <= 0) continue;
                card = Use("StoreRow");
                SetIcon(card, "Icon", WorldTree.EnemySprite(e.index));
                SetText(card, "Name", e.Name + (WorldTree.Prefers(e.index) ? TreeUi.Colored("  " + Loc.T("grow.favorite"), TreeUi.Good) : ""));
                SetText(card, "Count", "×" + Wallet.Format(n));
            }
        }

        static string EvolveConditions()
        {
            var sb = new StringBuilder();
            sb.Append(TreeUi.Check(WorldTree.IsMaxRing, Loc.F("grow.cond_ring", WorldTree.RingMax))).Append("   ");
            sb.Append(TreeUi.Check(WorldTree.Exp >= WorldTree.EvoExp, Loc.F("grow.cond_exp", Wallet.Format(WorldTree.EvoExp)))).Append('\n');
            sb.Append(TreeUi.Check(Wallet.CanAfford(Currency.Gold, WorldTree.EvoGold), Gold(WorldTree.EvoGold)));
            if (WorldTree.EvoMycelite > 0)
                sb.Append("   ").Append(TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.EvoMycelite), Mycelite(WorldTree.EvoMycelite)));
            return sb.ToString();
        }

        // 견본이 없을 때: 예전 코드 화면
        void BuildByCode()
        {
            var row = WorldTree.CurrentRow;
            bool max = WorldTree.IsMaxStage;

            // 지금 단계
            var card = TreeUi.Card(list, 230f, true, Beige);
            TreeUi.Icon(card, WorldTree.TreeSprite(WorldTree.Stage), 190f);
            TreeUi.Line(card, Loc.F("grow.stage_name", WorldTree.Stage, row.Name), 46f, TreeUi.Ink, font, 240f, 24f, 0.62f, 0.95f).fontStyle = FontStyles.Bold;
            TreeUi.Line(card, Loc.F("grow.ring_lv", WorldTree.Ring, WorldTree.RingMax), 34f, TreeUi.Ink, font, 240f, 24f, 0.38f, 0.62f);
            if (max)
                TreeUi.Line(card, Loc.T("grow.max_stage"), 34f, TreeUi.Good, font, 240f, 24f, 0.08f, 0.36f);
            else
            {
                float exp = WorldTree.EvoExp > 0 ? (float)(WorldTree.Exp / WorldTree.EvoExp) : 1f;
                TreeUi.Bar(card, exp, new Color(0.45f, 0.75f, 0.35f), new Vector2(240f, 22f), new Vector2(-24f, 52f), 0f);
                TreeUi.Line(card, Loc.F("grow.exp_of", Wallet.Format((long)WorldTree.Exp), Wallet.Format(WorldTree.EvoExp)), 28f, TreeUi.Ink, font, 250f, 30f, 0.1f, 0.24f);
            }

            // 나이테
            card = TreeUi.Card(list, 170f, true, Beige);
            TreeUi.Icon(card, Resources.Load<Sprite>(RingIcon), IconSize);
            TreeUi.Line(card, Loc.T("grow.ring_title"), 40f, TreeUi.Ink, font, TextLeft, 290f, 0.5f, 0.92f).fontStyle = FontStyles.Bold;
            TreeUi.Line(card, Loc.T("grow.ring_hint"), 28f, TreeUi.Faint, font, TextLeft, 290f, 0.1f, 0.5f);
            if (WorldTree.IsMaxRing)
                TreeUi.Button(card, Loc.T("grow.max"), false, null, font);
            else
            {
                var fail = WorldTree.CanRingUp();
                TreeUi.Button(card, Loc.F("grow.ring_up", Gold(WorldTree.RingCost)), fail == WorldTree.GrowFail.None, () =>
                {
                    if (WorldTree.RingUp()) Toast(Loc.F("grow.ring_toast", WorldTree.Ring));
                }, font, width: 250f);
            }

            // 공물
            card = TreeUi.Card(list, 210f, true, Beige);
            TreeUi.Icon(card, Resources.Load<Sprite>(TributeIcon), IconSize);
            TreeUi.Line(card, Loc.T("grow.tribute_title"), 40f, TreeUi.Ink, font, TextLeft, 290f, 0.62f, 0.94f).fontStyle = FontStyles.Bold;
            TreeUi.Line(card, Loc.F("grow.tribute_desc", Wallet.Format(WorldTree.StoreTotal), Wallet.Format((long)WorldTree.TributePreview)), 30f, TreeUi.Ink, font, TextLeft, 290f, 0.36f, 0.62f);
            TreeUi.Line(card, Loc.F("grow.tribute_prefer", GameBalance.Const("tributePreferMul").ToString("0.#"), PreferNames(row)), 26f, TreeUi.Faint, font, TextLeft, 290f, 0.06f, 0.36f);
            TreeUi.Button(card, Loc.T("grow.tribute_btn"), WorldTree.CanTribute, () =>
            {
                double exp = WorldTree.Tribute();
                if (exp > 0) Toast(Loc.F("grow.exp_toast", Wallet.Format((long)exp)));
            }, font);

            // 진화
            if (!max)
            {
                var next = GameBalance.TreeStages[WorldTree.Stage];
                card = TreeUi.Card(list, 260f, true, Beige);
                TreeUi.Icon(card, WorldTree.TreeSprite(next.stage), IconSize);   // 다음 단계 나무
                TreeUi.Line(card, Loc.F("grow.evolve_title", next.stage, next.Name), 40f, TreeUi.Ink, font, TextLeft, 290f, 0.72f, 0.95f).fontStyle = FontStyles.Bold;
                var sb = new StringBuilder();
                sb.Append(TreeUi.Check(WorldTree.IsMaxRing, Loc.F("grow.cond_ring", WorldTree.RingMax))).Append("   ");
                sb.Append(TreeUi.Check(WorldTree.Exp >= WorldTree.EvoExp, Loc.F("grow.cond_exp", Wallet.Format(WorldTree.EvoExp)))).Append('\n');
                sb.Append(TreeUi.Check(Wallet.CanAfford(Currency.Gold, WorldTree.EvoGold), Gold(WorldTree.EvoGold)));
                if (WorldTree.EvoMycelite > 0)
                    sb.Append("   ").Append(TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.EvoMycelite), Mycelite(WorldTree.EvoMycelite)));
                TreeUi.Line(card, sb.ToString(), 28f, TreeUi.Ink, font, TextLeft, 290f, 0.3f, 0.72f);
                TreeUi.Line(card, NextUnlocks(row, next), 26f, TreeUi.Faint, font, TextLeft, 290f, 0.03f, 0.3f);
                TreeUi.Button(card, Loc.T("grow.evolve_btn"), WorldTree.CanEvolve() == WorldTree.GrowFail.None, () =>
                {
                    if (WorldTree.Evolve()) Toast(Loc.F("grow.evolve_toast", WorldTree.CurrentRow.Name));
                }, font, tint: TreeUi.ButtonBlue);   // [UI 개편 11-05] 진화할 수 있으면 하늘색 (못 하면 회색)
            }

            // 가호
            TreeUi.Header(list, Loc.T("grow.blessing_header"), font);
            foreach (var b in GameBalance.Blessings)
            {
                bool on = WorldTree.BlessActive(b);
                card = TreeUi.Card(list, 100f, on, Beige);
                TreeUi.Line(card, Loc.F("grow.stage_n", b.stage), 32f, on ? TreeUi.Good : TreeUi.Faint, font, 32f, 0f, 0f, 1f).fontStyle = FontStyles.Bold;
                TreeUi.Line(card, WorldTree.EffectText(b.effect, b.value), 32f, on ? TreeUi.Ink : TreeUi.Faint, font, 180f, 24f, 0f, 1f);
            }

            // 창고
            TreeUi.Header(list, Loc.F("grow.store_header", Wallet.Format(WorldTree.StoreTotal)), font);
            if (WorldTree.StoreTotal <= 0)
            {
                card = TreeUi.Card(list, 110f, true, Beige);
                TreeUi.Line(card, Loc.T("grow.store_empty"), 30f, TreeUi.Faint, font, 32f, 24f, 0f, 1f);
            }
            foreach (var e in GameBalance.Enemies)
            {
                long n = WorldTree.StoreCount(e.index);
                if (n <= 0) continue;
                card = TreeUi.Card(list, 110f, true, Beige);
                TreeUi.Icon(card, WorldTree.EnemySprite(e.index), 90f);
                string prefer = WorldTree.Prefers(e.index) ? TreeUi.Colored("  " + Loc.T("grow.favorite"), TreeUi.Good) : "";
                TreeUi.Line(card, e.Name + prefer, 32f, TreeUi.Ink, font, 140f, 260f, 0f, 1f);
                TreeUi.Line(card, "×" + Wallet.Format(n), 34f, TreeUi.Ink, font, 140f, 32f, 0f, 1f).alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        static string PreferNames(TreeStageRow row)
        {
            if (row.preferIds == null || row.preferIds.Length == 0) return "-";
            var sb = new StringBuilder();
            foreach (int id in row.preferIds)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(WorldTree.EnemyName(id));
            }
            return sb.ToString();
        }

        // 진화하면 늘어나는 것
        static string NextUnlocks(TreeStageRow now, TreeStageRow next)
        {
            var sb = new StringBuilder();
            if (next.craftSlots > now.craftSlots) sb.Append(Loc.F("grow.unlock_slots", now.craftSlots, next.craftSlots) + "  ");
            int a = now.rootSlots + now.trunkSlots + now.branchSlots + now.crownSlots;
            int b = next.rootSlots + next.trunkSlots + next.branchSlots + next.crownSlots;
            if (b > a) sb.Append(Loc.F("grow.unlock_deco", a, b) + "  ");
            foreach (var bless in GameBalance.Blessings)
                if (bless.stage == next.stage) sb.Append(Loc.F("grow.unlock_bless", WorldTree.EffectText(bless.effect, bless.value)));
            return sb.ToString();
        }
    }
}
