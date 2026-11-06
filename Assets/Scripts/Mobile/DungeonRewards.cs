using UnityEngine;

namespace MoreMush.Mobile
{
    // 던전 레벨 보상 (던전 시트 baseStage·stageStep·rewardKills·rewardMyc·rewardGrowth, 밸런스설계/이론.md 10.1 C7·C8).
    // 레벨 L의 기준 스테이지 = baseStage + stageStep × (L − 1). 깨면 골드 = 그 스테이지 적 처치 골드 × rewardKills,
    // 균사석 = rewardMyc × (1 + rewardGrowth × (L − 1)). 던전 1(수확 판)은 판에서 번 골드에 이 보상을 더 준다.
    // 매 판 직접 플레이한다 (소탕 없음). 레벨 보상은 깼을 때만 — 못 깨면 판에서 번 골드만 받는다
    public static class DungeonRewards
    {
        // 레벨의 기준 스테이지. 시트에 없거나 기준이 없는 던전은 0
        public static int Stage(int dungeon, int level)
        {
            var row = GameBalance.Dungeon(dungeon);
            if (row == null || row.baseStage <= 0) return 0;
            return row.baseStage + row.stageStep * Mathf.Max(0, level - 1);
        }

        // 보스전 던전(2~5)의 레벨 보스: 기준 스테이지 방치 보스 × hpMul·atkMul, 제한 시간은 그 스테이지 보스 시간.
        // 던전 씬이 생기면 이 값을 쓴다
        public static double BossHp(int dungeon, int level) => IdleProgress.BossHp(Stage(dungeon, level)) * Mul(dungeon, true);
        public static double BossAttack(int dungeon, int level) => IdleProgress.BossAttack(Stage(dungeon, level)) * Mul(dungeon, false);
        public static float BossTime(int dungeon, int level) => IdleProgress.BossTime(Stage(dungeon, level));

        static double Mul(int dungeon, bool hp)
        {
            var row = GameBalance.Dungeon(dungeon);
            double m = row == null ? 1 : hp ? row.hpMul : row.atkMul;
            return m > 0 ? m : 1;
        }

        public static (long gold, long mycelite) Reward(int dungeon, int level, double mul = 1)
        {
            var row = GameBalance.Dungeon(dungeon);
            if (row == null || level < 1 || mul <= 0) return (0, 0);
            long gold = row.rewardKills > 0
                ? IdleProgress.ClampToLong((double)IdleProgress.EnemyGold(Stage(dungeon, level)) * row.rewardKills * mul)
                : 0;
            long mycelite = row.rewardMyc > 0
                ? IdleProgress.ClampToLong(System.Math.Round(row.rewardMyc * (1 + row.rewardGrowth * (level - 1)) * mul))
                : 0;
            return (gold, mycelite);
        }

        // 레벨을 깼다: 그 레벨 보상
        public static (long gold, long mycelite) GrantClear(int dungeon, int level) => Grant(Reward(dungeon, level));

        static (long gold, long mycelite) Grant((long gold, long mycelite) r)
        {
            if (r.gold > 0) Wallet.Add(Currency.Gold, r.gold);
            if (r.mycelite > 0) Wallet.Add(Currency.Mycelite, r.mycelite);
            return r;
        }
    }
}
