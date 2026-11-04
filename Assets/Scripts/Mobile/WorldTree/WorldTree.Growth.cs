using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 세계수 성장: 단계(1~MaxStage)마다 나이테 Lv1~RingMax. 나이테는 골드로 올리고, 다 채우면 경험치(공물)·골드·균사석으로 진화한다.
    // 경험치는 창고 버섯(방치 전투 처치로 쌓임)을 공물로 바쳐 얻는다. 가호는 지금 단계까지의 것이 모두 영구 적용된다 (WorldTree.Bonus)
    public static partial class WorldTree
    {
        public enum GrowFail { None, Max, Ring, Exp, Gold, Mycelite }

        public static int Stage => D.stage;
        public static int Ring => D.ring;
        public static double Exp => D.exp;
        public static bool IsMaxStage => Stage >= MaxStage;
        public static bool IsMaxRing => Ring >= RingMax;

        // ── 나이테 ──
        public static long RingCost => RingCostAt(Stage, Ring);

        public static long RingCostAt(int stage, int ring) =>
            KillGold(Row(stage).ringKills * Math.Pow(GameBalance.Const("treeRingGrowth"), ring - 1));

        public static GrowFail CanRingUp()
        {
            if (IsMaxRing) return GrowFail.Max;
            return Wallet.CanAfford(Currency.Gold, RingCost) ? GrowFail.None : GrowFail.Gold;
        }

        public static bool RingUp()
        {
            if (CanRingUp() != GrowFail.None) return false;
            if (!Wallet.TrySpend(Currency.Gold, RingCost)) return false;
            D.ring++;
            Notify(true);
            return true;
        }

        // ── 진화 ──
        public static int EvoExp => CurrentRow.evoExp;
        public static long EvoGold => KillGold(CurrentRow.evoGoldKills);
        public static long EvoMycelite => CurrentRow.evoMycelite;

        public static GrowFail CanEvolve()
        {
            if (IsMaxStage) return GrowFail.Max;
            if (!IsMaxRing) return GrowFail.Ring;
            if (Exp < EvoExp) return GrowFail.Exp;
            if (!Wallet.CanAfford(Currency.Gold, EvoGold)) return GrowFail.Gold;
            if (!Wallet.CanAfford(Currency.Mycelite, EvoMycelite)) return GrowFail.Mycelite;
            return GrowFail.None;
        }

        public static bool Evolve()
        {
            if (CanEvolve() != GrowFail.None) return false;
            long gold = EvoGold, mycelite = EvoMycelite;
            if (!Wallet.CanAfford(Currency.Gold, gold) || !Wallet.CanAfford(Currency.Mycelite, mycelite)) return false;
            Wallet.TrySpend(Currency.Gold, gold);
            Wallet.TrySpend(Currency.Mycelite, mycelite);
            D.exp -= EvoExp;            // 남은 경험치는 이어 간다
            D.stage++;
            D.ring = 1;
            Notify(true);
            return true;
        }

        // ── 가호: 단계에 도달하면 영구 ──
        public static bool BlessActive(BlessRow b) => b.stage <= Stage;

        // ── 창고 버섯 ──
        static Dictionary<string, int> enemyBySprite;     // 적 그림 이름(ed0…, 군락 시트 이름) → 적.index

        public static EnemyRow Enemy(int index)
        {
            foreach (var e in GameBalance.Enemies) if (e.index == index) return e;
            return null;
        }

        public static string EnemyName(int index) => Enemy(index)?.name ?? index.ToString();

        public static Sprite EnemySprite(int index)
        {
            var e = Enemy(index);
            string path = e != null ? GameBalance.ResourcePath(e.resourceId) : null;
            return path != null ? Resources.Load<Sprite>(path) : null;
        }

        public static long StoreCount(int enemy)
        {
            foreach (var s in D.store) if (s.enemy == enemy) return s.count;
            return 0;
        }

        public static long StoreTotal
        {
            get
            {
                long n = 0;
                foreach (var s in D.store) n += s.count;
                return n;
            }
        }

        public static IReadOnlyList<StoreEntry> Store => D.store;

        static void AddStore(int enemy, long n)
        {
            foreach (var s in D.store)
                if (s.enemy == enemy) { s.count = Math.Max(0, s.count + n); return; }
            if (n > 0) D.store.Add(new StoreEntry { enemy = enemy, count = n });
        }

        // 방치 전투에서 일반 적을 잡았다 (IdleBattle). spriteName = 적 그림 이름. 버섯 획득량 효과는 기대값으로 (소수 부분은 확률)
        public static void AddHarvest(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return;
            if (enemyBySprite == null)
            {
                enemyBySprite = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var e in GameBalance.Enemies)
                {
                    string path = GameBalance.ResourcePath(e.resourceId);
                    if (path != null) enemyBySprite[path.Substring(path.LastIndexOf('/') + 1)] = e.index;
                }
            }
            // 군락 시트(독버섯·약용버섯)는 잘린 조각 이름이 "시트이름_번호"다 — 조각 이름으로 못 찾으면 시트 이름으로 찾는다
            if (!enemyBySprite.TryGetValue(spriteName, out int enemy))
            {
                int us = spriteName.LastIndexOf('_');
                if (us <= 0 || !int.TryParse(spriteName.Substring(us + 1), out _)
                    || !enemyBySprite.TryGetValue(spriteName.Substring(0, us), out enemy)) return;
            }
            QuestKill(enemy);
            double amount = GameBalance.Const("treeHarvestPerKill") * (1 + Bonus(TreeEffect.Harvest));
            long n = (long)Math.Floor(amount);
            if (UnityEngine.Random.value < amount - n) n++;
            if (n <= 0) return;
            AddStore(enemy, n);
            MarkDirty();            // 처치마다 오므로 화면 갱신(Changed)은 열려 있는 화면이 1초마다 스스로 한다
        }

        // ── 공물: 창고를 통째로 바쳐 경험치 ──
        public static bool Prefers(int enemy)
        {
            var ids = CurrentRow.preferIds;
            return ids != null && Array.IndexOf(ids, enemy) >= 0;
        }

        public static double TributeExpOf(int enemy, long count)
        {
            var e = Enemy(enemy);
            if (e == null) return 0;
            return count * e.value * (Prefers(enemy) ? GameBalance.Const("tributePreferMul") : 1);
        }

        public static double TributePreview
        {
            get
            {
                double exp = 0;
                foreach (var s in D.store) exp += TributeExpOf(s.enemy, s.count);
                return exp;
            }
        }

        public static bool CanTribute => !IsMaxStage && StoreTotal > 0;

        public static double Tribute()
        {
            if (!CanTribute) return 0;
            double exp = TributePreview;
            D.store.Clear();
            D.exp += exp;
            Notify(true);
            return exp;
        }
    }
}
