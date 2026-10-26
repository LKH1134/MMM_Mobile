using UnityEngine;

namespace MoreMush.Mobile
{
    // 지금 도전할 스테이지 번호. PlayerPrefs("Stage.Current")에 저장한다 (세이브 구조가 생기면 옮긴다).
    // 일반 스테이지는 판을 마치면 다음으로, 보스 스테이지는 보스를 처치해야 다음으로 간다 (못 잡으면 같은 스테이지에 다시 도전).
    // 마지막 스테이지를 넘으면 마지막 스테이지에 머문다
    public static class StageProgress
    {
        const string Key = "Stage.Current";

        public static int Current => Mathf.Clamp(PlayerPrefs.GetInt(Key, 1), 1, Mathf.Max(1, MaxStage));

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

        // 판이 끝났을 때. cleared = 일반 스테이지면 항상 true, 보스 스테이지면 처치했는지
        public static void Complete(bool cleared)
        {
            if (!cleared) return;
            PlayerPrefs.SetInt(Key, Mathf.Min(Current + 1, Mathf.Max(1, MaxStage)));
            PlayerPrefs.Save();
        }

        // 개발용
        public static void Set(int stage)
        {
            PlayerPrefs.SetInt(Key, Mathf.Max(1, stage));
            PlayerPrefs.Save();
        }
    }
}
