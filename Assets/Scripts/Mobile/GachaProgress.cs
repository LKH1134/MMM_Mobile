using System;
using UnityEngine;

namespace MoreMush.Mobile
{
    public enum GachaKind { Weapon, Accessory, Pet }

    // 뽑기 종류별 레벨·경험치·보유 수와 광고 뽑기 사용 기록. PlayerPrefs에 저장한다 (세이브 구조가 생기면 옮긴다).
    // 경험치는 뽑은 개수만큼 오른다 (11뽑이면 +11). 레벨이 오를수록 높은 등급 확률이 커진다.
    // 결과 생성(Roll)과 저장(Apply)을 나눠 두었다 — 나중에 서버 검증으로 옮기기 쉽게
    // 등급 수·확률·경험치 곡선은 임시값이다 (기획 확정 전)
    public static class GachaProgress
    {
        public const int MaxLevel = 10;
        public const int GradeCount = 5;
        public static readonly string[] GradeNames = { "일반", "고급", "희귀", "영웅", "전설" };

        public static event Action Changed;

        static string LevelKey(GachaKind k) => $"Gacha.{k}.Level";
        static string ExpKey(GachaKind k) => $"Gacha.{k}.Exp";
        static string OwnedKey(GachaKind k, int grade) => $"Gacha.{k}.Owned.{grade}";
        static string AdDayKey(GachaKind k) => $"Gacha.{k}.AdDay";

        public static int Level(GachaKind k) => Mathf.Clamp(PlayerPrefs.GetInt(LevelKey(k), 1), 1, MaxLevel);
        public static int Exp(GachaKind k) => Mathf.Max(0, PlayerPrefs.GetInt(ExpKey(k), 0));
        public static int Owned(GachaKind k, int grade) => Mathf.Max(0, PlayerPrefs.GetInt(OwnedKey(k, grade), 0));

        // 다음 레벨까지 필요한 경험치. 최고 레벨이면 0
        public static int ExpToNext(int level) => level >= MaxLevel ? 0 : 100 * level;

        // 레벨별 등급 확률(%). 합이 100. 레벨이 오를수록 일반이 줄고 높은 등급이 는다
        public static float[] Odds(int level)
        {
            int step = Mathf.Clamp(level, 1, MaxLevel) - 1;
            float legend = 0.5f + 0.5f * step, epic = 3f + 1.5f * step, rare = 12f + 2f * step, uncommon = 30f;
            return new[] { 100f - legend - epic - rare - uncommon, uncommon, rare, epic, legend };
        }

        // 광고 뽑기: 하루 한 번 (기기 시계 기준 날짜가 바뀌면 다시)
        static string Today => DateTime.Now.ToString("yyyyMMdd");
        public static bool AdAvailable(GachaKind k) => PlayerPrefs.GetString(AdDayKey(k), "") != Today;
        public static void MarkAdUsed(GachaKind k)
        {
            PlayerPrefs.SetString(AdDayKey(k), Today);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 결과 생성만 한다 (저장하지 않는다). 한 번에 뽑는 동안은 시작할 때의 레벨 확률을 쓴다
        public static int[] Roll(GachaKind k, int count)
        {
            var odds = Odds(Level(k));
            var result = new int[GradeCount];
            for (int i = 0; i < count; i++)
            {
                float r = UnityEngine.Random.value * 100f, acc = 0f;
                int grade = GradeCount - 1;
                for (int g = 0; g < GradeCount; g++)
                {
                    acc += odds[g];
                    if (r < acc) { grade = g; break; }
                }
                result[grade]++;
            }
            return result;
        }

        // 결과를 보유 수에 더하고 경험치를 올린다 (뽑은 개수만큼)
        public static void Apply(GachaKind k, int[] result)
        {
            int total = 0;
            for (int g = 0; g < GradeCount; g++)
            {
                if (result[g] <= 0) continue;
                PlayerPrefs.SetInt(OwnedKey(k, g), Owned(k, g) + result[g]);
                total += result[g];
            }
            int level = Level(k), exp = Exp(k) + total;
            while (level < MaxLevel && exp >= ExpToNext(level)) { exp -= ExpToNext(level); level++; }
            if (level >= MaxLevel) exp = 0;
            PlayerPrefs.SetInt(LevelKey(k), level);
            PlayerPrefs.SetInt(ExpKey(k), exp);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
