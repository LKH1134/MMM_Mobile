using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // Weapon·Accessory(방어구) = 장비 (Equipment), Character = 꼬마 (Formation).
    // 2 = 펫 (2026-11-04 삭제). 씬·프리팹에 숫자로 저장돼 있어 Character는 3 그대로 둔다
    public enum GachaKind { Weapon = 0, Accessory = 1, Character = 3 }

    // 뽑기 종류별 레벨·경험치·보유 수와 무료 뽑기 사용 기록. PlayerPrefs에 저장한다 (세이브 구조가 생기면 옮긴다).
    // 경험치는 뽑은 개수만큼 오른다 (11뽑이면 +11). 레벨이 오를수록 높은 등급 확률이 커진다.
    // 결과 생성(Roll)과 저장(Apply)을 나눠 두었다 — 나중에 서버 검증으로 옮기기 쉽게
    // 등급 수·확률·경험치 곡선은 임시값이다 (기획 확정 전)
    public static class GachaProgress
    {
        public const int GradeCount = 4;   // 0 일반 · 1 희귀 · 2 영웅 · 3 전설 (2026-11-04 고급 삭제)

        // 최고 레벨 = 뽑기 시트의 행 수. 레벨별 경험치와 등급 확률도 그 시트에서 읽는다
        public static int MaxLevel => GameBalance.GachaLevels.Count;
        public static string[] GradeNames => GameBalance.GradeNames;

        public static event Action Changed;

        static string LevelKey(GachaKind k) => $"Gacha.{k}.Level";
        static string ExpKey(GachaKind k) => $"Gacha.{k}.Exp";
        static string OwnedKey(GachaKind k, int grade) => $"Gacha.{k}.Owned.{grade}";
        static string FreeDayKey(GachaKind k) => $"Gacha.{k}.AdDay";   // 예전 광고 뽑기 키 그대로 (오늘 이미 쓴 기록 유지)

        public static int Level(GachaKind k) => Mathf.Clamp(PlayerPrefs.GetInt(LevelKey(k), 1), 1, MaxLevel);
        public static int Exp(GachaKind k) => Mathf.Max(0, PlayerPrefs.GetInt(ExpKey(k), 0));
        public static int Owned(GachaKind k, int grade) => Mathf.Max(0, PlayerPrefs.GetInt(OwnedKey(k, grade), 0));

        // 보유 효과(%) 1레벨 기준값 (등급별, 등급 시트 ownBase). 장비마다 단계·레벨을 반영한 값은 Equipment가 셈한다.
        // 무기 = 공격력 %, 방어구 = 체력 % (IdleProgress가 쓴다)
        public static double OwnBase(int grade)
        {
            var grades = GameBalance.Grades;    // 시트 행이 등급 수보다 적어도 넘지 않게 (검증기가 막지만 런타임에서도)
            return grades[Mathf.Clamp(grade, 0, Mathf.Min(GradeCount, grades.Count) - 1)].ownBase;
        }

        public static double OwnPercent(GachaKind k)
        {
            var eq = Equipment.Of(k);
            return eq != null ? eq.TotalPercent : 0;     // 장비별 보유 + 장착 (Equipment)
        }

        // 다음 레벨까지 필요한 경험치. 최고 레벨이면 0
        public static int ExpToNext(int level) => level >= MaxLevel ? 0 : GameBalance.GachaLevels[Mathf.Max(level, 1) - 1].expToNext;

        // 레벨별 등급 확률(%). 합이 100. 레벨이 오를수록 일반이 줄고 높은 등급이 는다 (뽑기 시트)
        public static float[] Odds(int level)
        {
            var rows = GameBalance.GachaLevels;
            var r = rows[Mathf.Clamp(level, 1, rows.Count) - 1];
            return new[] { (float)r.oddsNormal, (float)r.oddsRare, (float)r.oddsEpic, (float)r.oddsLegend };
        }

        // 광고 뽑기: 종류마다 하루 limit번 (뽑기상품 시트 dailyLimit). 던전 입장 횟수와 같이 한국 시각(UTC+9) 00시에 다시 생긴다
        // (기기 시계를 되돌려 다시 받지 못하게 저장된 날보다 뒤의 날만 새날로 본다, DungeonTickets.IsNewDay).
        // 횟수 키가 없는 예전 세이브는 그날 1번 쓴 것으로 본다
        static string FreeCountKey(GachaKind k) => $"Gacha.{k}.AdCount";

        public static int FreeUsed(GachaKind k) =>
            DungeonTickets.IsNewDay(PlayerPrefs.GetString(FreeDayKey(k), "")) ? 0 : Mathf.Max(0, PlayerPrefs.GetInt(FreeCountKey(k), 1));

        public static int FreeLeft(GachaKind k, int limit) => limit <= 0 ? int.MaxValue : Mathf.Max(0, limit - FreeUsed(k));
        public static bool FreeAvailable(GachaKind k, int limit) => FreeLeft(k, limit) > 0;

        public static void MarkFreeUsed(GachaKind k)
        {
            int used = FreeUsed(k) + 1;
            PlayerPrefs.SetString(FreeDayKey(k), DungeonTickets.Today);
            PlayerPrefs.SetInt(FreeCountKey(k), used);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 꼬마 뽑기: 꼬마는 등급이 없다 — Roll 결과의 개수만 쓰고, 한 번마다 모든 꼬마 중 같은 확률로 고른다. 저장은 Formation.AddOwned
        public static List<string> PickCharacters(int[] result)
        {
            int count = 0;
            foreach (int n in result) count += n;
            var ids = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                string id = Formation.RandomId();
                if (id != "") ids.Add(id);
            }
            return ids;
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
