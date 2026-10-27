using UnityEngine;

namespace MoreMush.Mobile
{
    // 지금 도전할 스테이지 번호와 보스 도전 상태. PlayerPrefs에 저장한다 (세이브 구조가 생기면 옮긴다).
    //   일반 스테이지: 목표(Stage.xlsx goal)를 채우면 다음 스테이지로. 못 채우면 같은 스테이지를 다시 (골드는 다 받음),
    //                 연속 실패 수(FailStreak)만큼 목표를 줄여 준다 (RoundStats.GoalFor)
    //   보스 스테이지: 처치하면 다음으로. 못 잡으면 그대로 — 결과 팝업에서 바로 재도전하거나 ChooseFarm으로
    //                 FarmRounds판 그 스테이지 일반 판(파밍)을 돈 뒤 재도전한다 (강제 파밍 없음)
    //   마지막 스테이지를 넘으면 마지막 스테이지에 머문다.
    // 첫 클리어 기록(Cleared = 넘은 가장 높은 스테이지): 보스·★ 균사석은 처음 넘을 때만 준다 (FirstClearGem).
    // 보스 보상 계산용으로 직전 일반 판의 점수·골드도 남긴다
    public static class StageProgress
    {
        const string Key = "Stage.Current";
        const string FarmKey = "Stage.FarmLeft";
        const string LastScoreKey = "Stage.LastNormalScore";
        const string LastGoldKey = "Stage.LastNormalGold";
        const string ClearedKey = "Stage.Cleared";
        const string FailKey = "Stage.FailStreak";

        public const int FarmRounds = 1;   // 보스에 지고 "파밍"을 고르면 이만큼 일반 판을 돈 뒤 재도전

        public static int Current => Mathf.Clamp(PlayerPrefs.GetInt(Key, 1), 1, Mathf.Max(1, MaxStage));
        public static int FarmLeft => Mathf.Max(0, PlayerPrefs.GetInt(FarmKey, 0));
        public static long LastNormalScore => long.TryParse(PlayerPrefs.GetString(LastScoreKey, "0"), out var v) ? v : 0;
        public static long LastNormalGold => long.TryParse(PlayerPrefs.GetString(LastGoldKey, "0"), out var v) ? v : 0;
        public static int FailStreak => Mathf.Max(0, PlayerPrefs.GetInt(FailKey, 0));

        // 넘은(일반 목표 달성·보스 처치) 가장 높은 스테이지. 키가 없는 옛 세이브는 처음 읽을 때 옮겨 적는다
        public static int Cleared
        {
            get
            {
                if (!PlayerPrefs.HasKey(ClearedKey)) MigrateCleared();
                return PlayerPrefs.GetInt(ClearedKey, 0);
            }
        }

        // 옛 세이브: 스테이지는 순서대로만 넘어가므로 Current − 1까지 넘은 것 (그 보스 균사석은 이미 받았다).
        // 마지막 보스를 잡은 세이브도 Current가 마지막에 머물러 구분이 안 되는데, 옛 규칙은 균사석을 보스 처치에서만 줬으므로
        // 지금까지 받은 균사석(지갑 + 균사석 노드에 쓴 몫)이 마지막 보스 전 보스들의 합을 넘으면 마지막 보스까지 잡은 것으로 본다
        static void MigrateCleared()
        {
            int current = Current, max = MaxStage;
            int cleared = current - 1;
            if (current == max && max > 0)
            {
                long received = Wallet.Mycelite + SkillProgress.Spent(Currency.Mycelite);
                long beforeLast = 0;
                foreach (var s in GameData.Stages.All)
                    if (s.stage < max && s.boss != 0 && GameData.Bosses.Get(s.boss) is BossData b) beforeLast += b.gem;
                if (received > beforeLast) cleared = max;
            }
            PlayerPrefs.SetInt(ClearedKey, Mathf.Max(0, cleared));
            PlayerPrefs.Save();
        }

        // 이번 판으로 처음 넘은 스테이지면 첫 클리어 균사석 (★ 스테이지 gem + 보스 gem), 아니면 0.
        // cleared = 목표 달성·보스 처치 (파밍 판은 false), clearedBefore = 판 전의 Cleared. 측정기도 같은 규칙을 쓴다
        public static int FirstClearGem(StageData stage, BossData boss, bool cleared, int clearedBefore)
        {
            if (stage == null || !cleared || stage.stage <= clearedBefore) return 0;
            return Mathf.Max(0, stage.gem) + (boss != null ? Mathf.Max(0, boss.gem) : 0);
        }

        public static int MaxStage
        {
            get
            {
                int max = 0;
                foreach (var s in GameData.Stages.All) max = Mathf.Max(max, s.stage);
                return max;
            }
        }

        // 스테이지 번호의 행. 표에 없으면 null
        public static StageData Get(int stage)
        {
            foreach (var s in GameData.Stages.All) if (s.stage == stage) return s;
            return null;
        }

        public static StageData CurrentData => Get(Current);

        // 이번 판이 보스 판인지 (보스 스테이지이고 파밍 중이 아님)
        public static bool BossActive
        {
            get
            {
                var s = CurrentData;
                return s != null && s.boss != 0 && FarmLeft == 0;
            }
        }

        // 판이 끝났을 때. bossRound = 보스 판이었는지, cleared = 보스를 처치했는지 / 일반 판이면 목표를 채웠는지 (파밍 판은 무시)
        public static void Complete(bool bossRound, bool cleared, long score, long gold)
        {
            var s = CurrentData;
            if (bossRound)
            {
                if (cleared) Advance();   // 못 잡으면 그대로 (결과 팝업에서 재도전·파밍을 고른다)
            }
            else
            {
                PlayerPrefs.SetString(LastScoreKey, score.ToString());
                PlayerPrefs.SetString(LastGoldKey, gold.ToString());
                if (s != null && s.boss != 0) PlayerPrefs.SetInt(FarmKey, Mathf.Max(0, FarmLeft - 1));   // 보스 스테이지에서 파밍
                else if (cleared) Advance();
                else PlayerPrefs.SetInt(FailKey, FailStreak + 1);   // 같은 스테이지를 다시, 다음 목표는 조금 낮게
            }
            PlayerPrefs.Save();
        }

        // 보스에 진 뒤 결과 팝업에서 "파밍"을 고르면: FarmRounds판 일반 판을 돈 뒤 재도전
        public static void ChooseFarm()
        {
            PlayerPrefs.SetInt(FarmKey, FarmRounds);
            PlayerPrefs.Save();
        }

        static void Advance()
        {
            int current = Current;
            PlayerPrefs.SetInt(ClearedKey, Mathf.Max(Cleared, current));
            PlayerPrefs.SetInt(Key, Mathf.Min(current + 1, Mathf.Max(1, MaxStage)));
            PlayerPrefs.SetInt(FarmKey, 0);
            PlayerPrefs.SetInt(FailKey, 0);
        }

        // 개발용. 앞 스테이지는 다 넘은 것으로 친다 (건너뛴 보스·★ 균사석은 주지 않고, 되돌아가면 다시 받을 수 있다)
        public static void Set(int stage)
        {
            stage = Mathf.Max(1, stage);
            PlayerPrefs.SetInt(Key, stage);
            PlayerPrefs.SetInt(ClearedKey, stage - 1);
            PlayerPrefs.SetInt(FarmKey, 0);
            PlayerPrefs.SetInt(FailKey, 0);
            PlayerPrefs.Save();
        }
    }
}
