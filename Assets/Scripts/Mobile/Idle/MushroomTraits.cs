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

        // 특성은 꼬마(버섯)마다 따로 올린다: 저장 키 = "MushroomTrait.Level.<꼬마 id>.<칸 번호>"
        const string KeyPrefix = "MushroomTrait.Level.";

        static string Key(string ggoma, int node) => KeyPrefix + ggoma + "." + node;

        public static int Level(string ggoma, int node) =>
            string.IsNullOrEmpty(ggoma) ? 0 : Mathf.Max(0, PlayerPrefs.GetInt(Key(ggoma, node), 0));

        // 이 칸을 level → level+1 로 올리는 값. tier = 1(맨 아래) … 6(맨 위)
        public static long MyceliteCost(int tier, int level) => MyceliteStep * Mathf.Max(1, tier) * (level + 1);

        public static void SetLevel(string ggoma, int node, int level)
        {
            if (string.IsNullOrEmpty(ggoma)) return;
            PlayerPrefs.SetInt(Key(ggoma, node), Mathf.Max(0, level));
            PlayerPrefs.Save();
        }

        public static bool AnyLevel(string ggoma, int nodeCount)
        {
            for (int i = 0; i < nodeCount; i++) if (Level(ggoma, i) > 0) return true;
            return false;
        }

        // 이 꼬마의 특성만 0으로 (다른 꼬마는 그대로)
        public static void ResetAll(string ggoma, int nodeCount)
        {
            if (string.IsNullOrEmpty(ggoma)) return;
            for (int i = 0; i < nodeCount; i++) PlayerPrefs.DeleteKey(Key(ggoma, i));
            PlayerPrefs.Save();
        }
    }
}
