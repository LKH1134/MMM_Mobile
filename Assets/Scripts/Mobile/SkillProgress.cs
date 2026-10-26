using UnityEngine;

namespace MoreMush.Mobile
{
    // 스킬 능력 키. Skill.xlsx의 ability 칸에 이 문자열을 쓴다
    public static class SkillAbility
    {
        // 수확기
        public const string HarvesterDamage = "harvester_damage";   // 공격력 +
        public const string HarvesterSpeed = "harvester_speed";     // 속도 +비율
        public const string HarvesterSize = "harvester_size";       // 크기 +비율
        public const string CritChance = "crit_chance";             // 치명타 확률
        public const string CritDamage = "crit_damage";             // 치명타 피해 (1 = 100%)
        // 바·콤보
        public const string BarLength = "bar_length";               // 바 길이 +비율
        public const string ComboRate = "combo_rate";               // 콤보 계수 +비율
        // 스테이지
        public const string RoundTime = "round_time";               // 제한 시간 +초
        public const string ColonyCount = "colony_count";           // 군락지 수 +
        public const string ColonySingles = "colony_single_mushroom"; // 군락지 주변 단일 버섯 +
        public const string ColonyRespawn = "colony_respawn";       // 군락지 리스폰 +초 (음수)
        public const string EpicSpawn = "epic_spawn";               // 에픽 버섯 스폰율 +
        public const string UniqueSpawn = "unique_spawn";           // 유니크 버섯 스폰율 +
        public const string GoldenSpawn = "golden_spawn";           // 황금 변종 스폰율 +
        // 획득
        public const string ScoreRate = "score_rate";               // 획득 점수 +비율
        public const string GoldConversion = "gold_conversion";     // 점수 골드 전환율 +비율
        public const string TripleScoreChance = "triple_score_chance"; // 점수 3배 확률
        public const string NormalYield = "normal_yield";           // 일반 버섯 수확량 +비율
        public const string EpicYield = "epic_yield";
        public const string UniqueYield = "unique_yield";
        public const string FinalRushBonus = "final_rush_bonus";    // 마지막 param초 점수·수확량 +비율
        // 바 버프 (바에 닿을 때마다 걸리고 시간이 새로 채워진다)
        public const string PowerBuff = "start_attack_buff";        // 공격력 + (param초)
        public const string HasteBuff = "buff_haste_duration";      // 가속 지속(초)
        public const string PierceBuff = "buff_pierce_duration";    // 관통 지속(초)
        public const string HomingBuff = "buff_homing_duration";    // 유도 지속(초)
        public const string FeverBuff = "buff_fever_duration";      // 피버 지속(초), param = 콤보 계수 +비율
        public const string WisdomBuff = "buff_wisdom_rate";        // 지혜: 스킬 발동률 +, param = 지속(초)
        public const string HealBuff = "buff_heal_duration";        // 약손: 포자 디버프 제거 + 보호 지속(초)
        public const string SplitBuff = "buff_split_duration";      // 분열 지속(초), param = 발동 확률
        public const string CloneBar = "clone_bar_duration";        // 분신 바 지속(초), param = 발동 확률
        public const string CloneBarCount = "clone_bar_count";      // 분신 바 최대 수 +
        public const string OpeningBuff = "opening_buff_chance";    // 시작 때 보유 버프 즉시 발동 확률
        // 수확기 수
        public const string HarvesterSlot = "harvester_slot";       // 함께 던지는 수확기 +
        public const string OrbitCount = "orbit_harvester_count";   // 회전 꼬마 수확기 +
        public const string OrbitDamage = "orbit_harvester_damage"; // 회전 꼬마 수확기 공격력 (공격력 대비 비율)
        // 확률 발동
        public const string PoisonStrike = "poison_strike_chance";  // 단일 버섯 캘 때 → 다음 타격 피해 +param
        public const string ChainLightning = "chain_lightning_chance"; // 군락지 다 캘 때 → 번개 (공격력 × param)
        public const string Whirlwind = "whirlwind_chance";         // 군락 충돌 → 지속 피해 (공격력 × param)
        public const string SporeBurst = "spore_burst_chance";      // 군락 충돌 → 전체 피해 (공격력 × param)
        // 필드
        public const string FieldRestoration = "field_restoration"; // 1 그루터기 / 2 꽃밭 / 3 도토리
        // 디버프·보스
        public const string DebuffCleanse = "debuff_cleanse_chance"; // 군락 충돌로 디버프를 얻을 때 바로 해제할 확률
        public const string BossDamage = "boss_damage";             // 보스 피해 +비율
        public const string LegendaryYield = "legendary_yield";     // 보스 전용 레전더리 버섯 수확량 +비율
    }

    // 배운 스킬 레벨. 지금은 PlayerPrefs("Skill.Level.<index>")에 저장한다 (세이브 구조가 생기면 옮긴다).
    // 능력 합계는 Total(ability)로 구한다 — 같은 능력을 가진 스킬들의 (배웠으면 abilityBase) + abilityValue × 레벨을 모두 더한 값
    public static class SkillProgress
    {
        const string KeyPrefix = "Skill.Level.";

        public static event System.Action<int> Changed;   // 레벨이 바뀐 스킬 index

        // 밸런스 측정기(BalanceBot)용: 넣으면 PlayerPrefs 대신 이 사전의 레벨을 쓴다 (실제 저장값은 건드리지 않음)
        public static System.Collections.Generic.Dictionary<int, int> SimLevels;

        public static int MaxLevel(SkillData skill) => Mathf.Max(1, skill.maxLevel);

        public static int GetLevel(int index) =>
            SimLevels != null ? (SimLevels.TryGetValue(index, out int l) ? l : 0) : PlayerPrefs.GetInt(KeyPrefix + index, 0);
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

        // level(1부터) 레벨을 배우는 가격과 재화. 1레벨 = unlockPrice·currency, 2레벨부터는 levelPrice가 있으면
        // levelPrice × priceGrowth^(level-2)·levelCurrency, 없으면 unlockPrice × priceGrowth^(level-1)·currency
        public static long Price(SkillData skill, int level, out Currency currency)
        {
            float growth = skill.priceGrowth > 0f ? skill.priceGrowth : 1f;
            if (level <= 1)
            {
                currency = skill.currency;
                return skill.unlockPrice;
            }
            if (skill.levelPrice > 0)
            {
                currency = skill.levelCurrency;
                return (long)System.Math.Round(skill.levelPrice * System.Math.Pow(growth, level - 2));
            }
            currency = skill.currency;
            return (long)System.Math.Round(skill.unlockPrice * System.Math.Pow(growth, level - 1));
        }

        // 다음 레벨 가격
        public static long NextPrice(SkillData skill, out Currency currency) => Price(skill, GetLevel(skill.index) + 1, out currency);

        public static bool CanAfford(SkillData skill)
        {
            long price = NextPrice(skill, out var currency);
            return Wallet.CanAfford(currency, price);
        }

        // 선행 조건을 채웠고 돈이 있으면 지갑에서 빼고 레벨을 올린다
        public static bool LevelUp(SkillData skill)
        {
            if (!CanLevelUp(skill)) return false;
            long price = NextPrice(skill, out var currency);
            if (!Wallet.TrySpend(currency, price)) return false;
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

        // 계열(group)에서 지금까지 오른 레벨 (Lv.4 노드를 1레벨 배웠으면 4). 안 배웠으면 0
        public static int SeriesLevel(string group)
        {
            int best = 0;
            foreach (var skill in GameData.Skills.All)
            {
                if (skill.group != group) continue;
                int level = Mathf.Min(GetLevel(skill.index), MaxLevel(skill));
                if (level > 0) best = Mathf.Max(best, Mathf.Max(1, skill.startLevel) + level - 1);
            }
            return best;
        }

        // 배운 스킬 중 이 능력을 가진 것의 고정 수치(param). 여럿이면 가장 큰 값, 없으면 fallback
        public static float Param(string ability, float fallback = 0f)
        {
            float best = float.NegativeInfinity;
            foreach (var skill in GameData.Skills.All)
                if (skill.ability == ability && GetLevel(skill.index) > 0) best = Mathf.Max(best, skill.param);
            return float.IsNegativeInfinity(best) ? fallback : best;
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
