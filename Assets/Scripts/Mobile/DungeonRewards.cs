using UnityEngine;

namespace MoreMush.Mobile
{
    // 던전 레벨 보상 (던전 시트 baseStage·stageStep·rewardKills·rewardMyc·rewardGrowth, 밸런스설계/이론.md 10.1 C7·C8).
    // 레벨 L의 기준 스테이지 = baseStage + stageStep × (L − 1). 깨면 골드 = 그 스테이지 적 처치 골드 × rewardKills,
    // 균사석 = rewardMyc × (1 + rewardGrowth × (L − 1)). 던전 1(수확 판)은 판에서 번 골드에 이 보상을 더 준다.
    // 상한: 기준 스테이지가 방치 스테이지보다 높으면 싸우지 않고 소탕 — 마지막으로 깬 레벨(L − 1) 보상 × dungeonSweepMul.
    // 도전했다가 못 깨도 같은 소탕 보상을 준다. 레벨 1부터 막혀 있으면(깬 레벨 없음) 입장을 막는다
    public static class DungeonRewards
    {
        public static double SweepMul => System.Math.Max(0, GameBalance.Const("dungeonSweepMul"));

        // 레벨의 기준 스테이지. 시트에 없거나 기준이 없는 던전은 0 (상한 없음)
        public static int Stage(int dungeon, int level)
        {
            var row = GameBalance.Dungeon(dungeon);
            if (row == null || row.baseStage <= 0) return 0;
            return row.baseStage + row.stageStep * Mathf.Max(0, level - 1);
        }

        // 지금 레벨이 방치 진행보다 앞서 있다 → 싸우지 않고 소탕
        public static bool IsCapped(int dungeon)
        {
            int s = Stage(dungeon, DungeonTickets.Level(dungeon));
            return s > 0 && s > IdleProgress.Stage;
        }

        // 보스전 던전(2~5)의 레벨 보스: 기준 스테이지 방치 보스 × hpMul·atkMul, 제한 시간은 그 스테이지 보스 시간.
        // 기준 스테이지는 방치 스테이지 이하라(넘으면 소탕) 배율 1이면 이미 이긴 보스와 같다. 던전 씬이 생기면 이 값을 쓴다
        public static double BossHp(int dungeon, int level) => IdleProgress.BossHp(Stage(dungeon, level)) * Mul(dungeon, true);
        public static double BossAttack(int dungeon, int level) => IdleProgress.BossAttack(Stage(dungeon, level)) * Mul(dungeon, false);
        public static float BossTime(int dungeon, int level) => IdleProgress.BossTime(Stage(dungeon, level));

        static double Mul(int dungeon, bool hp)
        {
            var row = GameBalance.Dungeon(dungeon);
            double m = row == null ? 1 : hp ? row.hpMul : row.atkMul;
            return m > 0 ? m : 1;
        }

        public static bool CanSweep(int dungeon) => IsCapped(dungeon) && DungeonTickets.Level(dungeon) > 1;

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

        // 소탕 (상한에 걸려 입장했거나, 도전했다가 못 깼다): 마지막으로 깬 레벨 보상 × SweepMul
        public static (long gold, long mycelite) GrantSweep(int dungeon, int level) => Grant(Reward(dungeon, level - 1, SweepMul));

        static (long gold, long mycelite) Grant((long gold, long mycelite) r)
        {
            if (r.gold > 0) Wallet.Add(Currency.Gold, r.gold);
            if (r.mycelite > 0) Wallet.Add(Currency.Mycelite, r.mycelite);
            return r;
        }

        // 입장 횟수(없으면 광고 입장)를 하나 쓰고 소탕한다. 광고면 끝까지 본 뒤 onDone. 광고가 준비되지 않았으면 false
        public static bool TrySweep(int dungeon, System.Action<long> onDone)
        {
            if (!CanSweep(dungeon)) return false;
            if (DungeonTickets.CanEnter(dungeon))
            {
                if (!DungeonTickets.TryUse(dungeon)) return false;
                WorldTree.QuestDungeon();
                onDone?.Invoke(GrantSweep(dungeon, DungeonTickets.Level(dungeon)).gold);
                return true;
            }
            if (!DungeonTickets.CanEnterWithAd(dungeon)) return false;
            return AdService.ShowRewarded(AdPlacement.Dungeon, () =>
            {
                if (!CanSweep(dungeon) || !DungeonTickets.TryUseAd(dungeon)) return;
                WorldTree.QuestDungeon();
                onDone?.Invoke(GrantSweep(dungeon, DungeonTickets.Level(dungeon)).gold);
            });
        }
    }
}
