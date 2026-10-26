using UnityEngine;

namespace MoreMush.Mobile
{
    // 지금 도전할 스테이지 번호와 보스 도전 상태. PlayerPrefs에 저장한다 (세이브 구조가 생기면 옮긴다).
    //   일반 스테이지: 판을 마치면 다음 스테이지로
    //   보스 스테이지: 처치하면 다음으로. 못 잡으면 스테이지는 그대로, 보스 도전이 꺼지고 FarmRounds판 동안
    //                 그 스테이지에서 일반 판(파밍)을 돈 뒤 다시 보스에 도전한다 (PC판 규칙: 지면 파밍하며 강해져 넘는다)
    //   마지막 스테이지를 넘으면 마지막 스테이지에 머문다.
    // 보스 보상 계산용으로 직전 일반 판의 점수·골드도 남긴다
    public static class StageProgress
    {
        const string Key = "Stage.Current";
        const string FarmKey = "Stage.FarmLeft";
        const string LastScoreKey = "Stage.LastNormalScore";
        const string LastGoldKey = "Stage.LastNormalGold";

        public const int FarmRounds = 2;

        public static int Current => Mathf.Clamp(PlayerPrefs.GetInt(Key, 1), 1, Mathf.Max(1, MaxStage));
        public static int FarmLeft => Mathf.Max(0, PlayerPrefs.GetInt(FarmKey, 0));
        public static long LastNormalScore => long.TryParse(PlayerPrefs.GetString(LastScoreKey, "0"), out var v) ? v : 0;
        public static long LastNormalGold => long.TryParse(PlayerPrefs.GetString(LastGoldKey, "0"), out var v) ? v : 0;

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

        // 판이 끝났을 때. bossRound = 보스 판이었는지, cleared = 보스를 처치했는지 (일반 판은 무시)
        public static void Complete(bool bossRound, bool cleared, long score, long gold)
        {
            var s = CurrentData;
            if (bossRound)
            {
                if (cleared) Advance();
                else PlayerPrefs.SetInt(FarmKey, FarmRounds);
            }
            else
            {
                PlayerPrefs.SetString(LastScoreKey, score.ToString());
                PlayerPrefs.SetString(LastGoldKey, gold.ToString());
                if (s != null && s.boss != 0) PlayerPrefs.SetInt(FarmKey, Mathf.Max(0, FarmLeft - 1));   // 보스 스테이지에서 파밍
                else Advance();
            }
            PlayerPrefs.Save();
        }

        static void Advance()
        {
            PlayerPrefs.SetInt(Key, Mathf.Min(Current + 1, Mathf.Max(1, MaxStage)));
            PlayerPrefs.SetInt(FarmKey, 0);
        }

        // 개발용
        public static void Set(int stage)
        {
            PlayerPrefs.SetInt(Key, Mathf.Max(1, stage));
            PlayerPrefs.SetInt(FarmKey, 0);
            PlayerPrefs.Save();
        }
    }
}
