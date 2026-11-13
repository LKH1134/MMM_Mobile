using UnityEngine;

namespace MoreMush.Mobile
{
    // 버섯 특성: 특성 나무 칸마다 올린 레벨(저장)과 올리는 값. 편성 화면 [버섯 특성] 화면(MushroomTraitPanelView)이 쓴다.
    // 레벨 올리기 = 균사석만 쓴다. 위쪽 칸(tier가 클수록)일수록 비싸고, 레벨이 오를수록 비싸다.
    // 초기화 = 균사석 ResetMycelite개를 내고 모든 칸을 0으로 (쓴 재화는 돌려주지 않는다).
    // 효과 적용(전투 수치에 더하기)은 아직 없다 — 특성 효과 표가 정해지면 여기서 합을 내준다.
    // 수치는 임시값이다 — 밸런스가 정해지면 상수 시트로 옮긴다
    public static class MushroomTraits
    {
        public const int ResetMycelite = 10;                 // 특성 초기화 비용 (균사석)
        const long MyceliteStep = 5;                         // 1단계(맨 아래) 칸의 첫 레벨 균사석

        const string KeyPrefix = "MushroomTrait.Level.";

        public static int Level(int node) => Mathf.Max(0, PlayerPrefs.GetInt(KeyPrefix + node, 0));

        // 이 칸을 level → level+1 로 올리는 값. tier = 1(맨 아래) … 6(맨 위)
        public static long MyceliteCost(int tier, int level) => MyceliteStep * Mathf.Max(1, tier) * (level + 1);

        public static void SetLevel(int node, int level)
        {
            PlayerPrefs.SetInt(KeyPrefix + node, Mathf.Max(0, level));
            PlayerPrefs.Save();
        }

        public static bool AnyLevel(int nodeCount)
        {
            for (int i = 0; i < nodeCount; i++) if (Level(i) > 0) return true;
            return false;
        }

        public static void ResetAll(int nodeCount)
        {
            for (int i = 0; i < nodeCount; i++) PlayerPrefs.DeleteKey(KeyPrefix + i);
            PlayerPrefs.Save();
        }
    }
}
