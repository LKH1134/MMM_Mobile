using UnityEngine;

namespace MoreMush.Mobile
{
    // 스킬 능력 키. Skill.xlsx의 ability 칸에 이 문자열을 쓴다
    public static class SkillAbility
    {
        public const string HarvesterDamage = "harvester_damage";   // 수확기 공격력 + (abilityValue × 레벨)
    }

    // 배운 스킬 레벨. 지금은 PlayerPrefs("Skill.Level.<index>")에 저장한다 (세이브 구조가 생기면 옮긴다).
    // 능력 합계는 Total(ability)로 구한다 — 같은 능력을 가진 스킬들의 (배웠으면 abilityBase) + abilityValue × 레벨을 모두 더한 값
    public static class SkillProgress
    {
        const string KeyPrefix = "Skill.Level.";

        public static event System.Action<int> Changed;   // 레벨이 바뀐 스킬 index

        public static int MaxLevel(SkillData skill) => Mathf.Max(1, skill.maxLevel);

        public static int GetLevel(int index) => PlayerPrefs.GetInt(KeyPrefix + index, 0);
        public static bool IsMaxed(SkillData skill) => GetLevel(skill.index) >= MaxLevel(skill);

        // 선행 조건: 연결 노드(link)는 1레벨 이상, 이전 레벨 노드(requires)는 최대 레벨까지
        public static bool RequirementsMet(SkillData skill) => MissingRequirements(skill).Count == 0;

        static readonly System.Collections.Generic.List<SkillData> missing = new System.Collections.Generic.List<SkillData>();

        // 아직 못 채운 선행 노드들 (돌려준 목록은 다음 호출 때 바뀐다)
        public static System.Collections.Generic.IReadOnlyList<SkillData> MissingRequirements(SkillData skill)
        {
            missing.Clear();
            if (skill.link != 0)
            {
                var link = GameData.Skills.Get(skill.link);
                if (link != null && GetLevel(link.index) <= 0) missing.Add(link);
            }
            if (skill.requires != null)
                foreach (int index in skill.requires)
                {
                    var prev = GameData.Skills.Get(index);
                    if (prev != null && !IsMaxed(prev)) missing.Add(prev);
                }
            return missing;
        }

        public static bool CanLevelUp(SkillData skill) => skill != null && !IsMaxed(skill) && RequirementsMet(skill);

        // 임시: 가격은 아직 빼지 않는다 (지갑이 생기면 unlockPrice·currency로 차감)
        public static bool LevelUp(SkillData skill)
        {
            if (!CanLevelUp(skill)) return false;
            PlayerPrefs.SetInt(KeyPrefix + skill.index, GetLevel(skill.index) + 1);
            PlayerPrefs.Save();
            Changed?.Invoke(skill.index);
            return true;
        }

        public static float Total(string ability)
        {
            float sum = 0f;
            foreach (var skill in GameData.Skills.All)
            {
                if (skill.ability != ability) continue;
                int level = Mathf.Min(GetLevel(skill.index), MaxLevel(skill));
                if (level > 0) sum += skill.abilityBase + skill.abilityValue * level;
            }
            return sum;
        }

        // 개발용: 배운 스킬 전부 초기화
        public static void ResetAll()
        {
            foreach (var skill in GameData.Skills.All) PlayerPrefs.DeleteKey(KeyPrefix + skill.index);
            PlayerPrefs.Save();
            Changed?.Invoke(0);
        }
    }
}
