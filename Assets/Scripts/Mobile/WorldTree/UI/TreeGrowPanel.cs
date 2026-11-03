using System.Text;
using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 성장 시트: 지금 단계·나이테·경험치, 나이테 성장, 공물(창고 버섯 → 경험치), 진화, 가호 목록, 창고
    public class TreeGrowPanel : TreePanel
    {
        protected override void Build()
        {
            var row = WorldTree.CurrentRow;
            bool max = WorldTree.IsMaxStage;

            // 지금 단계
            var card = TreeUi.Card(list, 230f);
            TreeUi.Icon(card, WorldTree.TreeSprite(WorldTree.Stage), 190f);
            TreeUi.Line(card, $"{WorldTree.Stage}단계 · {row.name}", 46f, TreeUi.Ink, font, 240f, 24f, 0.62f, 0.95f).fontStyle = FontStyles.Bold;
            TreeUi.Line(card, $"나이테 Lv {WorldTree.Ring}/{WorldTree.RingMax}", 34f, TreeUi.Ink, font, 240f, 24f, 0.38f, 0.62f);
            if (max)
                TreeUi.Line(card, "최고 단계입니다", 34f, TreeUi.Good, font, 240f, 24f, 0.08f, 0.36f);
            else
            {
                float exp = WorldTree.EvoExp > 0 ? (float)(WorldTree.Exp / WorldTree.EvoExp) : 1f;
                TreeUi.Bar(card, exp, new Color(0.45f, 0.75f, 0.35f), new Vector2(240f, 22f), new Vector2(-24f, 52f), 0f);
                TreeUi.Line(card, $"경험치 {Wallet.Format((long)WorldTree.Exp)} / {Wallet.Format(WorldTree.EvoExp)}", 28f, TreeUi.Ink, font, 250f, 30f, 0.1f, 0.24f);
            }

            // 나이테
            card = TreeUi.Card(list, 170f);
            TreeUi.Line(card, "나이테 성장", 40f, TreeUi.Ink, font, 32f, 290f, 0.5f, 0.92f).fontStyle = FontStyles.Bold;
            TreeUi.Line(card, "최대 Lv가 되어야 진화", 28f, TreeUi.Faint, font, 32f, 290f, 0.1f, 0.5f);
            if (WorldTree.IsMaxRing)
                TreeUi.Button(card, "최대", false, null, font);
            else
            {
                var fail = WorldTree.CanRingUp();
                TreeUi.Button(card, "성장\n" + Gold(WorldTree.RingCost), fail == WorldTree.GrowFail.None, () =>
                {
                    if (WorldTree.RingUp()) Toast($"나이테 Lv {WorldTree.Ring}!");
                }, font, width: 250f);
            }

            // 공물
            card = TreeUi.Card(list, 210f);
            TreeUi.Line(card, "공물 바치기", 40f, TreeUi.Ink, font, 32f, 290f, 0.62f, 0.94f).fontStyle = FontStyles.Bold;
            TreeUi.Line(card, $"창고 버섯 {Wallet.Format(WorldTree.StoreTotal)}개 → 경험치 +{Wallet.Format((long)WorldTree.TributePreview)}", 30f, TreeUi.Ink, font, 32f, 290f, 0.36f, 0.62f);
            TreeUi.Line(card, "좋아하는 버섯(×" + GameBalance.Const("tributePreferMul").ToString("0.#") + "): " + PreferNames(row), 26f, TreeUi.Faint, font, 32f, 290f, 0.06f, 0.36f);
            TreeUi.Button(card, "바치기", WorldTree.CanTribute, () =>
            {
                double exp = WorldTree.Tribute();
                if (exp > 0) Toast($"경험치 +{Wallet.Format((long)exp)}");
            }, font);

            // 진화
            if (!max)
            {
                var next = GameBalance.TreeStages[WorldTree.Stage];
                card = TreeUi.Card(list, 260f);
                TreeUi.Line(card, $"진화 → {next.stage}단계 {next.name}", 40f, TreeUi.Ink, font, 32f, 290f, 0.72f, 0.95f).fontStyle = FontStyles.Bold;
                var sb = new StringBuilder();
                sb.Append(TreeUi.Check(WorldTree.IsMaxRing, $"나이테 Lv {WorldTree.RingMax}")).Append("   ");
                sb.Append(TreeUi.Check(WorldTree.Exp >= WorldTree.EvoExp, $"경험치 {Wallet.Format(WorldTree.EvoExp)}")).Append('\n');
                sb.Append(TreeUi.Check(Wallet.CanAfford(Currency.Gold, WorldTree.EvoGold), Gold(WorldTree.EvoGold)));
                if (WorldTree.EvoMycelite > 0)
                    sb.Append("   ").Append(TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.EvoMycelite), Mycelite(WorldTree.EvoMycelite)));
                TreeUi.Line(card, sb.ToString(), 28f, TreeUi.Ink, font, 32f, 290f, 0.3f, 0.72f);
                TreeUi.Line(card, NextUnlocks(row, next), 26f, TreeUi.Faint, font, 32f, 290f, 0.03f, 0.3f);
                TreeUi.Button(card, "진화!", WorldTree.CanEvolve() == WorldTree.GrowFail.None, () =>
                {
                    if (WorldTree.Evolve()) Toast($"세계수가 {WorldTree.CurrentRow.name}(으)로 자랐어요!");
                }, font, tint: TreeUi.ButtonAlt);
            }

            // 가호
            TreeUi.Header(list, "가호", font);
            foreach (var b in GameBalance.Blessings)
            {
                bool on = WorldTree.BlessActive(b);
                card = TreeUi.Card(list, 100f, on);
                TreeUi.Line(card, $"{b.stage}단계", 32f, on ? TreeUi.Good : TreeUi.Faint, font, 32f, 0f, 0f, 1f).fontStyle = FontStyles.Bold;
                TreeUi.Line(card, WorldTree.EffectText(b.effect, b.value), 32f, on ? TreeUi.Ink : TreeUi.Faint, font, 180f, 24f, 0f, 1f);
            }

            // 창고
            TreeUi.Header(list, $"창고 버섯 ({Wallet.Format(WorldTree.StoreTotal)})", font);
            if (WorldTree.StoreTotal <= 0)
            {
                card = TreeUi.Card(list, 110f);
                TreeUi.Line(card, "방치 전투에서 버섯을 잡으면 창고에 쌓여요", 30f, TreeUi.Faint, font, 32f, 24f, 0f, 1f);
            }
            foreach (var e in GameBalance.Enemies)
            {
                long n = WorldTree.StoreCount(e.index);
                if (n <= 0) continue;
                card = TreeUi.Card(list, 110f);
                TreeUi.Icon(card, WorldTree.EnemySprite(e.index), 90f);
                string prefer = WorldTree.Prefers(e.index) ? TreeUi.Colored("  좋아함", TreeUi.Good) : "";
                TreeUi.Line(card, e.name + prefer, 32f, TreeUi.Ink, font, 140f, 260f, 0f, 1f);
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
            if (next.craftSlots > now.craftSlots) sb.Append($"제작 슬롯 {now.craftSlots}→{next.craftSlots}  ");
            int a = now.rootSlots + now.trunkSlots + now.branchSlots + now.crownSlots;
            int b = next.rootSlots + next.trunkSlots + next.branchSlots + next.crownSlots;
            if (b > a) sb.Append($"장식 칸 {a}→{b}  ");
            foreach (var bless in GameBalance.Blessings)
                if (bless.stage == next.stage) sb.Append("가호: ").Append(WorldTree.EffectText(bless.effect, bless.value));
            return sb.ToString();
        }
    }
}
