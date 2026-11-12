using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 공방: 장식을 골드·균사석과 시간으로 만든다 (주인 꼬마를 가져야 제작 목록에 열린다). 슬롯 수는 세계수 단계(craftSlots).
    // 다 되면 받아야 장식이 생긴다. 가속은 남은 시간 craftRushSeconds마다 균사석 1, 하루 craftRushPerDay번.
    // 꾸미기: 가진 장식을 그 장식의 자리(뿌리·줄기·가지·수관)에 놓는다. 자리마다 칸 수는 세계수 단계. 놓은 장식만 효과.
    // 처음 놓으면 균사석. 한 꼬마의 장식을 모두 놓으면 세트 (그 꼬마 특기 단계 +)
    public static partial class WorldTree
    {
        public enum CraftFail { None, Recipe, Owned, Crafting, Slots, Gold, Mycelite }

        public static DecoRow Deco(string key)
        {
            foreach (var d in GameBalance.Decos) if (d.key == key) return d;
            return null;
        }

        static DecoGradeRow DecoGrade(int grade)
        {
            foreach (var g in GameBalance.DecoGrades) if (g.grade == grade) return g;
            return GameBalance.DecoGrades[0];
        }

        public static string DecoGradeName(int grade) => DecoGrade(grade).Name;

        public static Sprite DecoSprite(DecoRow d)
        {
            string path = d != null ? GameBalance.ResourcePath(d.resourceId) : null;
            return path != null ? Resources.Load<Sprite>(path) : null;
        }

        // ── 공방 ──
        public static int CraftSlots => CurrentRow.craftSlots;
        public static int MaxCraftSlots
        {
            get
            {
                int max = 0;
                foreach (var r in GameBalance.TreeStages) max = Math.Max(max, r.craftSlots);
                return max;
            }
        }

        // 이 슬롯 수가 열리는 세계수 단계 (없으면 0)
        public static int CraftSlotStage(int slotCount)
        {
            foreach (var r in GameBalance.TreeStages) if (r.craftSlots >= slotCount) return r.stage;
            return 0;
        }

        public static IReadOnlyList<Craft> Crafts => D.crafts;

        public static bool RecipeOpen(DecoRow d) => d != null && Formation.IsOwned(d.owner);
        public static bool OwnsDeco(string key) => D.decosOwned.Contains(key);

        public static bool IsCrafting(string key)
        {
            foreach (var c in D.crafts) if (c.deco == key) return true;
            return false;
        }

        public static long CraftGold(DecoRow d) => KillGold(DecoGrade(d.grade).goldKills);
        public static int CraftMycelite(DecoRow d) => DecoGrade(d.grade).mycelite;
        public static double CraftSeconds(DecoRow d) => DecoGrade(d.grade).hours * 3600.0 / (1 + Bonus(TreeEffect.CraftSpeed));

        public static CraftFail CanCraft(DecoRow d)
        {
            if (!RecipeOpen(d)) return CraftFail.Recipe;
            if (OwnsDeco(d.key)) return CraftFail.Owned;
            if (IsCrafting(d.key)) return CraftFail.Crafting;
            if (D.crafts.Count >= CraftSlots) return CraftFail.Slots;
            if (!Wallet.CanAfford(Currency.Gold, CraftGold(d))) return CraftFail.Gold;
            if (!Wallet.CanAfford(Currency.Mycelite, CraftMycelite(d))) return CraftFail.Mycelite;
            return CraftFail.None;
        }

        public static bool StartCraft(DecoRow d)
        {
            if (CanCraft(d) != CraftFail.None) return false;
            long gold = CraftGold(d);
            int mycelite = CraftMycelite(d);
            Wallet.TrySpend(Currency.Gold, gold);
            Wallet.TrySpend(Currency.Mycelite, mycelite);
            long now = NowTicks;
            D.crafts.Add(new Craft { deco = d.key, startTicks = now, endTicks = now + (long)(CraftSeconds(d) * TimeSpan.TicksPerSecond) });
            Notify(true);
            return true;
        }

        public static double CraftRemaining(Craft c) => Math.Max(0, (c.endTicks - NowTicks) / (double)TimeSpan.TicksPerSecond);
        public static bool CraftDone(Craft c) => NowTicks >= c.endTicks;

        public static bool ClaimCraft(int i)
        {
            if (i < 0 || i >= D.crafts.Count || !CraftDone(D.crafts[i])) return false;
            string key = D.crafts[i].deco;
            D.crafts.RemoveAt(i);
            if (!D.decosOwned.Contains(key)) D.decosOwned.Add(key);
            Notify(true);
            return true;
        }

        // ── 가속 ──
        public static int RushPerDay => GameBalance.ConstInt("craftRushPerDay");
        public static int RushLeft => D.rushDay == Today ? Math.Max(0, RushPerDay - D.rushUsed) : RushPerDay;
        public static int RushCost(Craft c) => Math.Max(1, (int)Math.Ceiling(CraftRemaining(c) / GameBalance.Const("craftRushSeconds")));

        public static bool CanRush(int i) =>
            i >= 0 && i < D.crafts.Count && !CraftDone(D.crafts[i]) && RushLeft > 0 && Wallet.CanAfford(Currency.Mycelite, RushCost(D.crafts[i]));

        public static bool Rush(int i)
        {
            if (!CanRush(i)) return false;
            if (!Wallet.TrySpend(Currency.Mycelite, RushCost(D.crafts[i]))) return false;
            if (D.rushDay != Today) { D.rushDay = Today; D.rushUsed = 0; }
            D.rushUsed++;
            D.crafts[i].endTicks = NowTicks;
            Notify(true);
            return true;
        }

        // ── 꾸미기 ──
        public static int ZoneSlots(TreeZone zone) => ZoneSlotsAt(CurrentRow, zone);

        static int ZoneSlotsAt(TreeStageRow r, TreeZone zone)
        {
            switch (zone)
            {
                case TreeZone.Root: return r.rootSlots;
                case TreeZone.Trunk: return r.trunkSlots;
                case TreeZone.Branch: return r.branchSlots;
                default: return r.crownSlots;
            }
        }

        // 이 자리가 처음 열리는 세계수 단계 (끝까지 안 열리면 0)
        public static int ZoneOpenStage(TreeZone zone)
        {
            foreach (var r in GameBalance.TreeStages) if (ZoneSlotsAt(r, zone) > 0) return r.stage;
            return 0;
        }

        public static string ZoneName(TreeZone zone)
        {
            switch (zone)
            {
                case TreeZone.Root: return Loc.T("zone.root");
                case TreeZone.Trunk: return Loc.T("zone.trunk");
                case TreeZone.Branch: return Loc.T("zone.branch");
                default: return Loc.T("zone.crown");
            }
        }

        public static bool IsPlaced(string key) => D.decosPlaced.Contains(key);

        public static List<DecoRow> PlacedIn(TreeZone zone)
        {
            var list = new List<DecoRow>();
            foreach (var key in D.decosPlaced)
            {
                var d = Deco(key);
                if (d != null && d.zone == zone) list.Add(d);
            }
            return list;
        }

        public static bool CanPlace(DecoRow d) =>
            d != null && OwnsDeco(d.key) && !IsPlaced(d.key) && PlacedIn(d.zone).Count < ZoneSlots(d.zone);

        public static bool Place(DecoRow d)
        {
            if (!CanPlace(d)) return false;
            D.decosPlaced.Add(d.key);
            if (!D.decosFirstPlaced.Contains(d.key))
            {
                D.decosFirstPlaced.Add(d.key);
                Wallet.Add(Currency.Mycelite, GameBalance.ConstInt("decoFirstMycelite"));
            }
            Notify(true);
            return true;
        }

        public static bool Remove(DecoRow d)
        {
            if (d == null || !IsPlaced(d.key)) return false;
            D.decosPlaced.Remove(d.key);
            Notify(true);
            return true;
        }

        public static bool FirstPlaceRewardLeft(DecoRow d) => d != null && !D.decosFirstPlaced.Contains(d.key);

        // 주인 꼬마의 장식을 모두 놓았다
        public static bool DecoSetComplete(string kid)
        {
            bool any = false;
            foreach (var d in GameBalance.Decos)
            {
                if (d.owner != kid) continue;
                any = true;
                if (!IsPlaced(d.key)) return false;
            }
            return any;
        }
    }
}
