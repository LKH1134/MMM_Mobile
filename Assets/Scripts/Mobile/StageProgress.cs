using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 지금 도전할 스테이지 번호와 보스 도전 상태. PlayerPrefs에 저장한다 (세이브 구조가 생기면 옮긴다).
    //   일반 스테이지: 목표가 없다 — 판을 마치면 다음 스테이지로
    //   보스 스테이지: 처치하면 다음으로. 못 잡으면 그대로 — 결과 팝업에서 바로 재도전하거나 ChooseFarm으로
    //                 FarmRounds판 그 스테이지 일반 판(파밍)을 돈 뒤 재도전한다 (강제 파밍 없음)
    //   스테이지는 끝이 없다. 표(Stage.xlsx)의 마지막 스테이지를 넘으면 마지막 CycleLength개 묶음을 되풀이해 만든다 (Generate):
    //                 체력·보상 계수는 한 바퀴마다 표의 마지막 묶음이 커진 만큼 (마지막 스테이지 ÷ CycleLength 앞 스테이지) 곱해 끊김 없이 잇고,
    //                 CycleLength번째마다 보스 (표의 보스를 차례로 다시 쓴다)
    // 첫 클리어 기록(Cleared = 넘은 가장 높은 스테이지): 보스·★ 균사석은 처음 넘을 때만 준다 (FirstClearGem).
    // 보스 보상 계산용으로 직전 일반 판의 점수·골드도 남긴다
    public static class StageProgress
    {
        const string Key = "Stage.Current";
        const string FarmKey = "Stage.FarmLeft";
        const string LastScoreKey = "Stage.LastNormalScore";
        const string LastGoldKey = "Stage.LastNormalGold";
        const string ClearedKey = "Stage.Cleared";

        public const int FarmRounds = 1;   // 보스에 지고 "파밍"을 고르면 이만큼 일반 판을 돈 뒤 재도전
        public const int CycleLength = 10;   // 표 뒤 스테이지: 이만큼씩 되풀이, 마지막 스테이지가 보스

        // 옛 세이브는 표의 마지막 스테이지(그때의 끝)를 넘고도 거기 머물러 있었다 — 넘은 스테이지 다음으로 본다
        public static int Current => Mathf.Max(1, Mathf.Max(PlayerPrefs.GetInt(Key, 1), Cleared + 1));
        public static int FarmLeft => Mathf.Max(0, PlayerPrefs.GetInt(FarmKey, 0));
        public static long LastNormalScore => long.TryParse(PlayerPrefs.GetString(LastScoreKey, "0"), out var v) ? v : 0;
        public static long LastNormalGold => long.TryParse(PlayerPrefs.GetString(LastGoldKey, "0"), out var v) ? v : 0;

        // 넘은(일반 판을 마침·보스 처치) 가장 높은 스테이지. 키가 없는 옛 세이브는 처음 읽을 때 옮겨 적는다
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
            int current = Mathf.Max(1, PlayerPrefs.GetInt(Key, 1)), max = TableMaxStage;
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
        // cleared = 일반 판을 마침·보스 처치 (파밍 판은 false), clearedBefore = 판 전의 Cleared. 측정기도 같은 규칙을 쓴다
        public static int FirstClearGem(StageData stage, BossData boss, bool cleared, int clearedBefore)
        {
            if (stage == null || !cleared || stage.stage <= clearedBefore) return 0;
            return Mathf.Max(0, stage.gem) + (boss != null ? Mathf.Max(0, boss.gem) : 0);
        }

        // 표(Stage.xlsx)에 있는 마지막 스테이지. 그 뒤는 Generate로 만든다
        public static int TableMaxStage
        {
            get
            {
                int max = 0;
                foreach (var s in GameData.Stages.All) max = Mathf.Max(max, s.stage);
                return max;
            }
        }

        // 스테이지 번호의 행. 표 뒤 스테이지는 만들어 준다 (표가 비었거나 1 미만이면 null)
        public static StageData Get(int stage)
        {
            foreach (var s in GameData.Stages.All) if (s.stage == stage) return s;
            int max = TableMaxStage;
            if (stage <= max || max <= 0) return null;
            if (generated.TryGetValue(stage, out var made) && made.source == GameData.Stages.Get(made.data.index)) return made.data;
            var data = Generate(stage, max);
            if (data != null) generated[stage] = (data, GameData.Stages.Get(data.index));
            return data;
        }

        // 표를 다시 읽으면 원본 행이 바뀌므로 그때 다시 만든다
        static readonly Dictionary<int, (StageData data, StageData source)> generated = new Dictionary<int, (StageData, StageData)>();

        // 표 뒤 스테이지: 표의 마지막 CycleLength개 중 같은 자리 행을 본떠, 넘은 바퀴 수만큼 체력·보상을 키운다.
        // 보스 자리(CycleLength번째)는 표의 보스를 차례로 다시 쓰고, 마지막 보스만큼 버티게 체력을 맞춘다 (BossHpMul)
        static StageData Generate(int stage, int max)
        {
            int cycle = (stage - max - 1) / CycleLength + 1;                 // 표 뒤 몇 바퀴째인지 (1부터)
            int templateStage = stage - cycle * CycleLength;                // 표 안의 같은 자리
            StageData t = null, last = null, prev = null;
            foreach (var s in GameData.Stages.All)
            {
                if (s.stage == templateStage) t = s;
                if (s.stage == max) last = s;
                if (s.stage == max - CycleLength) prev = s;
            }
            if (t == null || last == null) return null;
            float Growth(float lastValue, float prevValue) => prev != null && prevValue > 0f && lastValue > 0f ? lastValue / prevValue : 1f;
            var data = new StageData
            {
                index = t.index, key = t.key, stage = stage, theme = last.theme,
                hpRate = t.hpRate * Mathf.Pow(Growth(last.hpRate, prev != null ? prev.hpRate : 0f), cycle),
                rewardRate = t.rewardRate * Mathf.Pow(Growth(last.rewardRate, prev != null ? prev.rewardRate : 0f), cycle),
                mushHpMul = t.mushHpMul, gem = t.gem,
            };
            if (stage % CycleLength == 0)
            {
                var bosses = new List<BossData>();
                foreach (var s in GameData.Stages.All)
                    if (s.boss != 0 && GameData.Bosses.Get(s.boss) is BossData b && !bosses.Contains(b)) bosses.Add(b);
                var lastBoss = last.boss != 0 ? GameData.Bosses.Get(last.boss) : null;
                if (bosses.Count > 0)
                {
                    var boss = bosses[(stage / CycleLength - 1) % bosses.Count];
                    data.boss = boss.index;
                    if (lastBoss != null && boss.hpRate > 0f) data.BossHpMul = Mathf.Max(1f, lastBoss.hpRate / boss.hpRate);
                }
            }
            return data;
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

        // 판이 끝났을 때. bossRound = 보스 판이었는지, cleared = 보스를 처치했는지 / 일반 판이면 넘었는지 (파밍 판은 무시)
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
            PlayerPrefs.SetInt(Key, current + 1);
            PlayerPrefs.SetInt(FarmKey, 0);
        }

        // 개발용. 앞 스테이지는 다 넘은 것으로 친다 (건너뛴 보스·★ 균사석은 주지 않고, 되돌아가면 다시 받을 수 있다)
        public static void Set(int stage)
        {
            stage = Mathf.Max(1, stage);
            PlayerPrefs.SetInt(Key, stage);
            PlayerPrefs.SetInt(ClearedKey, stage - 1);
            PlayerPrefs.SetInt(FarmKey, 0);
            PlayerPrefs.Save();
        }
    }
}
