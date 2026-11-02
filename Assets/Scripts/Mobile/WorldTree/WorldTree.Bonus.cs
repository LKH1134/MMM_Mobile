using System;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 세계수 효과 합계 = 가호(지금 단계까지) + 꼬마 특기(bonusValue × 특기 단계) + 놓은 장식.
    // 전투가 공격마다 읽으므로 합계를 캐시하고, 세계수·꼬마 보유가 바뀔 때만 다시 센다.
    // 수확 보따리: 시간당 골드(지금 스테이지 적 처치 골드 × bagKillsPerHour)·균사석이 보관 시간까지 쌓인다.
    // 배율 = 나이테 + 꼬마 기여(kidBagShare × ★ 배율 × 호감도 × 장식 세트) + 보따리 효과
    public static partial class WorldTree
    {
        static readonly double[] bonus = new double[Enum.GetValues(typeof(TreeEffect)).Length];
        static bool bonusDirty = true;
        static bool formationHooked;

        public static double Bonus(TreeEffect effect)
        {
            if (bonusDirty) RecomputeBonus();
            return bonus[(int)effect];
        }

        // 전투 능력치에 곱할 배율 (1 + 비율)
        public static double Mul(TreeEffect effect) => 1 + Bonus(effect);

        static void RecomputeBonus()
        {
            if (!formationHooked)
            {
                formationHooked = true;
                Formation.Changed += () => bonusDirty = true;     // 뽑기로 꼬마가 생기면 특기·보따리 기여가 바뀐다
            }
            bonusDirty = false;
            Array.Clear(bonus, 0, bonus.Length);
            try
            {
                foreach (var b in GameBalance.Blessings)
                    if (BlessActive(b)) bonus[(int)b.effect] += b.value;
                foreach (var id in OwnedKids)
                {
                    var row = KidRow(id);
                    if (row != null) bonus[(int)row.bonusEffect] += row.bonusValue * KidStep(id);
                }
                foreach (var key in D.decosPlaced)
                {
                    var d = Deco(key);
                    if (d != null) bonus[(int)d.effect] += d.value;
                }
            }
            catch (Exception e)
            {
                // 표를 못 읽으면 효과 없이 (전투는 계속 돈다)
                Debug.LogError("[WorldTree] 효과를 계산하지 못했다: " + e.Message);
                Array.Clear(bonus, 0, bonus.Length);
            }
        }

        // ── 수확 보따리 ──
        public static double KidBagShare(string id)
        {
            if (!Formation.IsOwned(id)) return 0;
            double share = GameBalance.Const("kidBagShare") * StarRow(Star(id)).bagMul
                * (1 + GameBalance.AffinityLevels[AffinityLevel(id) - 1].bagBonus);
            if (DecoSetComplete(id)) share *= 1 + GameBalance.Const("decoSetBagMul");
            return share;
        }

        public static double BagMultiplier
        {
            get
            {
                double kids = 0;
                foreach (var id in OwnedKids) kids += KidBagShare(id);
                return (1 + (Ring - 1) * GameBalance.Const("treeRingBagBonus")) * (1 + kids);
            }
        }

        public static double BagGoldPerHour => (double)IdleProgress.EnemyGold(IdleProgress.Stage) * CurrentRow.bagKillsPerHour * BagMultiplier * Mul(TreeEffect.BagGold);
        public static double BagMycelitePerHour => CurrentRow.bagMycelitePerHour * BagMultiplier * Mul(TreeEffect.BagMycelite);
        public static double BagCapSeconds => (GameBalance.Const("bagCapHours") + Bonus(TreeEffect.BagHours)) * 3600.0;

        public static long BagGold { get { SettleBag(); return (long)Math.Floor(D.bagGold); } }
        public static long BagMycelite { get { SettleBag(); return (long)Math.Floor(D.bagMycelite); } }
        public static double BagSeconds { get { SettleBag(); return D.bagSeconds; } }
        public static bool BagFull => BagSeconds >= BagCapSeconds - 0.5;
        public static bool CanClaimBag => BagGold >= 1 || BagMycelite >= 1;

        // 지난 정산부터 지금까지 쌓는다 (보관 시간까지). 비율이 바뀌는 행동 전에 먼저 부른다
        public static void SettleBag()
        {
            var d = D;
            long now = NowTicks;
            if (d.bagTicks > now) d.bagTicks = now;      // 기기 시계를 되돌렸다
            double dt = (now - d.bagTicks) / (double)TimeSpan.TicksPerSecond;
            if (dt < 1) return;
            d.bagTicks = now;
            double use = Math.Min(dt, Math.Max(0, BagCapSeconds - d.bagSeconds));
            if (use <= 0) return;
            double gold = BagGoldPerHour, mycelite = BagMycelitePerHour;
            if (gold <= 0 && mycelite <= 0) return;      // 쌓을 게 없으면 보관 시간도 쓰지 않는다
            d.bagGold += gold * use / 3600.0;
            d.bagMycelite += mycelite * use / 3600.0;
            d.bagSeconds += use;
            MarkDirty();
        }

        // 받은 (골드, 균사석). 소수 부분은 보따리에 남는다
        public static (long gold, long mycelite) ClaimBag()
        {
            SettleBag();
            var d = D;
            long gold = (long)Math.Floor(d.bagGold), mycelite = (long)Math.Floor(d.bagMycelite);
            if (gold < 1 && mycelite < 1) return (0, 0);
            d.bagGold -= gold;
            d.bagMycelite -= mycelite;
            d.bagSeconds = 0;
            if (gold > 0) Wallet.Add(Currency.Gold, gold);
            if (mycelite > 0) Wallet.Add(Currency.Mycelite, mycelite);
            Notify(true);
            return (gold, mycelite);
        }
    }
}
