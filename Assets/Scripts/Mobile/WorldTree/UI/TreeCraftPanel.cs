using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 공방 시트: 제작 슬롯(진행·가속·받기, 잠긴 슬롯은 열리는 단계)과 제작 목록(장식). 목록은 만들 수 있는 것 → 재화 부족 → 잠김·보유 순
    public class TreeCraftPanel : TreePanel
    {
        int doneCount = -1;

        // [UI 개편 2026-11-05] 카드 모양은 프리팹 CraftPopup의 Sheet/Content/Templates에서 고친다 (자식 이름을 지켜야 값이 들어간다).
        //   Header: (글자)   SlotEmpty: Text   CraftCard: Icon · Title · State · Button   RecipeCard: Icon · Title · Effect · Cost · Button
        static readonly string[] TemplateNames = { "Header", "SlotEmpty", "CraftCard", "RecipeCard" };

        protected override void Build()
        {
            if (!HasTemplates(TemplateNames)) { Debug.LogError("[TreeCraftPanel] Templates 견본이 빠졌다", this); return; }
            doneCount = CountDone();
            var crafts = WorldTree.Crafts;
            SetText(Use("Header"), null, Loc.F("craft.slots_header", crafts.Count, WorldTree.CraftSlots, WorldTree.RushLeft, WorldTree.RushPerDay));
            for (int i = 0; i < WorldTree.MaxCraftSlots; i++)
            {
                if (i < crafts.Count) { CraftFromTemplate(i, crafts[i]); continue; }
                bool open = i < WorldTree.CraftSlots;
                var slot = Use("SlotEmpty");
                CardOff(slot, !open);
                SetText(slot, "Text", open ? Loc.T("craft.slot_empty") : Loc.F("craft.slot_locked", WorldTree.CraftSlotStage(i + 1)));
            }

            SetText(Use("Header"), null, Loc.T("craft.list"));
            var decos = new List<DecoRow>(GameBalance.Decos);
            decos.Sort((a, b) =>
            {
                int c = Order(a).CompareTo(Order(b));
                return c != 0 ? c : a.grade != b.grade ? a.grade.CompareTo(b.grade) : a.index.CompareTo(b.index);
            });
            foreach (var d in decos)
            {
                var dd = d;
                var fail = WorldTree.CanCraft(d);
                var card = Use("RecipeCard");
                CardOff(card, fail == WorldTree.CraftFail.Recipe || fail == WorldTree.CraftFail.Owned);
                SetIcon(card, "Icon", WorldTree.DecoSprite(d));
                SetText(card, "Title", $"{d.Name}  <size=26>{WorldTree.DecoGradeName(d.grade)} · {WorldTree.ZoneName(d.zone)}</size>");
                SetText(card, "Effect", WorldTree.EffectText(d.effect, d.value) + $"  ·  {Formation.Name(d.owner)}");
                SetText(card, "Cost", TreeUi.Check(Wallet.CanAfford(Currency.Gold, WorldTree.CraftGold(d)), Gold(WorldTree.CraftGold(d))) + "  "
                    + TreeUi.Check(Wallet.CanAfford(Currency.Mycelite, WorldTree.CraftMycelite(d)), Mycelite(WorldTree.CraftMycelite(d))) + "  "
                    + TreeUi.Duration(WorldTree.CraftSeconds(d)));
                SetButton(card, "Button", RecipeLabel(fail, d), fail == WorldTree.CraftFail.None, () =>
                {
                    if (WorldTree.StartCraft(dd)) Toast(Loc.F("craft.start_toast", dd.Name));
                });
            }
        }

        void CraftFromTemplate(int i, WorldTree.Craft c)
        {
            var d = WorldTree.Deco(c.deco);
            var card = Use("CraftCard");
            SetIcon(card, "Icon", WorldTree.DecoSprite(d));
            SetText(card, "Title", d != null ? d.Name : c.deco);
            if (WorldTree.CraftDone(c))
            {
                var state = SetText(card, "State", Loc.T("craft.done"));
                if (state != null) state.color = TreeUi.Good;
                SetButton(card, "Button", Loc.T("tree.claim"), true, () =>
                {
                    if (WorldTree.ClaimCraft(i)) Toast(Loc.F("craft.claim_toast", d?.Name));
                }, "UI/UI_Btn_Yellow");
                return;
            }
            var st = card.Find("State")?.GetComponent<TMP_Text>();
            if (st != null) Live(st, () => Loc.F("craft.remaining", TreeUi.Duration(WorldTree.CraftRemaining(c))));
            var rush = SetButton(card, "Button", null, WorldTree.CanRush(i), () => { if (WorldTree.Rush(i)) Toast(Loc.T("craft.rush_toast")); });
            if (rush != null) Live(rush.GetComponentInChildren<TMP_Text>(), () => Loc.F("craft.rush", Mycelite(WorldTree.RushCost(c))));
        }

        static string RecipeLabel(WorldTree.CraftFail fail, DecoRow d)
        {
            switch (fail)
            {
                case WorldTree.CraftFail.Owned: return Loc.T("craft.owned");
                case WorldTree.CraftFail.Crafting: return Loc.T("craft.crafting");
                case WorldTree.CraftFail.Recipe: return Loc.F("craft.need_kid", Formation.Name(d.owner));
                case WorldTree.CraftFail.Slots: return Loc.T("craft.slots_full");
                default: return Loc.T("craft.make");
            }
        }

        static int CountDone()
        {
            int n = 0;
            foreach (var c in WorldTree.Crafts) if (WorldTree.CraftDone(c)) n++;
            return n;
        }

        // 제작이 끝나면 버튼이 받기로 바뀐다
        protected override bool OnTick() => CountDone() != doneCount;

        static int Order(DecoRow d)
        {
            switch (WorldTree.CanCraft(d))
            {
                case WorldTree.CraftFail.None: return 0;
                case WorldTree.CraftFail.Gold:
                case WorldTree.CraftFail.Mycelite:
                case WorldTree.CraftFail.Slots: return 1;
                case WorldTree.CraftFail.Crafting: return 2;
                case WorldTree.CraftFail.Recipe: return 3;
                default: return 4;
            }
        }
    }
}
