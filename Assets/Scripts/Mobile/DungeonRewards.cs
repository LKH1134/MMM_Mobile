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
            // 보스 러시: 레벨 RushEasyLevels까지는 기준 스테이지 그대로(쉽게 깬다), 그 뒤부터 레벨마다 stageStep씩 강해진다
            int steps = DungeonSession.Mode(dungeon) == DungeonMode.BossRush ? level - RushEasyLevels : level - 1;
            return row.baseStage + row.stageStep * Mathf.Max(0, steps);
        }

        const int RushEasyLevels = 5;

        // 보스 한 마리 레벨 보스: 기준 스테이지 방치 보스 × hpMul·atkMul, 제한 시간은 그 스테이지 보스 시간
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

        // 못 깼다: 그 레벨 보상 × 상수 dungeonFailMul (빈손으로 나가지 않게 — 하루 두 번 도는 보람)
        public static double FailMul => System.Math.Max(0, GameBalance.Const("dungeonFailMul"));
        public static (long gold, long mycelite) GrantFail(int dungeon, int level) => Grant(Reward(dungeon, level, FailMul));

        // ── 보스 연속전 (던전 4): 레벨 L의 보스 i(0부터, bossRushCount마리) ──
        // 기준 스테이지 sL에서 끝나는 방치 스테이지 보스들: i번째 = sL − (n − 1 − i). 체력·공격 × hpMul·atkMul, 시간은 그 스테이지 보스 시간.
        // 보스를 하나 잡을 때마다 레벨 보상의 1/n을 받는다 — 다 잡으면 레벨 보상 전부 + 다음 레벨
        public static int RushCount => Mathf.Max(1, GameBalance.ConstInt("bossRushCount"));
        public static int RushStage(int dungeon, int level, int i) => Mathf.Max(1, Stage(dungeon, level) - (RushCount - 1 - i));
        public static double RushHp(int dungeon, int level, int i) => IdleProgress.BossHp(RushStage(dungeon, level, i)) * Mul(dungeon, true);
        public static double RushAttack(int dungeon, int level, int i) => IdleProgress.BossAttack(RushStage(dungeon, level, i)) * Mul(dungeon, false);
        public static float RushTime(int dungeon, int level, int i) => IdleProgress.BossTime(RushStage(dungeon, level, i));
        // i번째 보스(0부터)를 잡았다: 레벨 보상을 n등분하되, 반올림으로 새지 않게 "i+1마리까지 몫 − i마리까지 몫"을 준다 (다 잡으면 정확히 레벨 보상 전부)
        public static (long gold, long mycelite) GrantRushBoss(int dungeon, int level, int i)
        {
            var total = Reward(dungeon, level);
            int n = RushCount;
            long Share(long all, int k) => IdleProgress.ClampToLong(System.Math.Round((double)all * k / n));
            return Grant((Share(total.gold, i + 1) - Share(total.gold, i), Share(total.mycelite, i + 1) - Share(total.mycelite, i)));
        }

        // ── 레벨업 보스 (던전 5): 레벨 n (1부터, 잡을 때마다 +1, 체력 100% 회복) ──
        // 기준 = 지금 방치 스테이지 보스 (플레이어 성장에 맞춰 늘 같은 체감). 레벨 1 = 방치 보스 × hpMul·atkMul,
        // 레벨마다 체력 × bossLvHpGrowth, 공격 × bossLvAtkGrowth, 공격 속도 × bossLvSpeedGrowth, 방어력 + bossLvDefPerLevel (상한 bossLvDefMax).
        // 공격 속도는 초당 공격 = 공격력 × 속도로 셈한다 (방치 전투의 적 공격은 초당 피해다)
        public static double LevelHp(int dungeon, int n) => IdleProgress.BossHp(IdleProgress.Stage) * Mul(dungeon, true) * Grow("bossLvHpGrowth", n);
        public static double LevelAttack(int dungeon, int n) => IdleProgress.BossAttack(IdleProgress.Stage) * Mul(dungeon, false) * Grow("bossLvAtkGrowth", n);
        public static double LevelSpeed(int n) => Grow("bossLvSpeedGrowth", n);
        const double DefenseCap = 0.9;     // 표가 1 이상이어도 피해가 0·음수(보스 회복)가 되지 않게
        public static double LevelDefense(int n) =>
            System.Math.Min(System.Math.Min(DefenseCap, System.Math.Max(0, GameBalance.Const("bossLvDefMax"))), System.Math.Max(0, GameBalance.Const("bossLvDefPerLevel")) * (n - 1));
        public static float LevelSeconds => Mathf.Max(5f, (float)GameBalance.Const("bossLvSeconds"));

        // 아주 높은 레벨에서 무한대가 되면 체력 막대가 NaN이 된다 — 상한을 둔다
        const double GrowCap = 1e200;
        static double Grow(string key, int n) => System.Math.Min(GrowCap, System.Math.Pow(System.Math.Max(1, GameBalance.Const(key)), System.Math.Max(0, n - 1)));

        // 레벨 n 보스를 잡은 보상: 골드 = 방치 적 처치 골드 × rewardKills × (1 + rewardGrowth × (n − 1)), 균사석 = rewardMyc × 같은 배율 (소수는 모아 두었다가 끝날 때 반올림)
        public static (double gold, double mycelite) LevelReward(int dungeon, int n)
        {
            var row = GameBalance.Dungeon(dungeon);
            if (row == null || n < 1) return (0, 0);
            double k = 1 + row.rewardGrowth * (n - 1);
            return ((double)IdleProgress.EnemyGold(IdleProgress.Stage) * row.rewardKills * k, row.rewardMyc * k);
        }

        public static (long gold, long mycelite) GrantRaw(double gold, double mycelite) =>
            Grant((IdleProgress.ClampToLong(System.Math.Round(gold)), IdleProgress.ClampToLong(System.Math.Round(mycelite))));

        static (long gold, long mycelite) Grant((long gold, long mycelite) r)
        {
            if (r.gold > 0) Wallet.Add(Currency.Gold, r.gold);
            if (r.mycelite > 0) Wallet.Add(Currency.Mycelite, r.mycelite);
            if (r.gold > 0 || r.mycelite > 0) SaveScheduler.Flush();   // 드문 보상 — 입장 횟수는 이미 저장됐으니 보상도 바로 (강제 종료 때 횟수만 쓰이지 않게)
            return r;
        }
    }
}
