using UnityEngine;

namespace MoreMush.Mobile
{
    public enum MushroomTier { Normal, Epic, Unique, Legendary }

    // 판 하나의 기본 수치. 스킬이 없을 때의 값이고, Skill 표 설명의 시작값(→ 왼쪽)을 따른다. HarvestGame 인스펙터에서 고친다
    [System.Serializable]
    public class RoundBase
    {
        public float duration = 15f;          // 제한 시간(초)
        public int colonies = 3;              // 군락지 수
        public int singlesPerColony = 3;      // 군락지 주변 단일 버섯 수
        public float colonyRespawn = 7f;      // 군락지를 다 캔 뒤 새로 돋기까지(초)
        public float minRespawn = 0.5f;
        [Range(0f, 1f)] public float epicChance = 0.05f;     // 군락지가 에픽으로 돋을 확률
        [Range(0f, 1f)] public float uniqueChance = 0.01f;   // 유니크로 돋을 확률
        public float comboFactor = 0.1f;      // 콤보 1당 점수 +10%
        public float critDamage = 0.5f;       // 치명 박살이 없을 때 치명타 피해 +50%
        public float goldenScore = 10f;       // 황금 변종 점수 배율
        public float goldenYield = 5f;        // 황금 변종 수확량 배율
        public float finalRushSeconds = 5f;   // 할아버지의 전설이 걸리는 마지막 시간 (표의 param이 있으면 그 값)
    }

    // 판 시작 때 배운 스킬로 정해지는 수치 모음 (판 중에는 바뀌지 않는다)
    public class RoundStats
    {
        public float Duration;
        public int Colonies;
        public int SinglesPerColony;
        public float ColonyRespawn;
        public float EpicChance, UniqueChance, GoldenChance;
        public int BonusDamage;
        public float SpeedMul, SizeMul, BarMul;
        public float CritChance, CritMul;
        public float ComboFactor;
        public float ScoreMul, GoldMul, TripleChance;
        public float NormalYieldMul, EpicYieldMul, UniqueYieldMul;
        public float GoldenScore, GoldenYield;
        public float FinalRushBonus, FinalRushSeconds;

        public static RoundStats FromSkills(RoundBase b)
        {
            float T(string ability) => SkillProgress.Total(ability);
            float critDamage = T(SkillAbility.CritDamage);
            return new RoundStats
            {
                Duration = Mathf.Max(1f, b.duration + T(SkillAbility.RoundTime)),
                Colonies = Mathf.Max(1, b.colonies + Mathf.RoundToInt(T(SkillAbility.ColonyCount))),
                SinglesPerColony = Mathf.Max(0, b.singlesPerColony + Mathf.RoundToInt(T(SkillAbility.ColonySingles))),
                ColonyRespawn = Mathf.Max(b.minRespawn, b.colonyRespawn + T(SkillAbility.ColonyRespawn)),
                EpicChance = Mathf.Clamp01(b.epicChance + T(SkillAbility.EpicSpawn)),
                UniqueChance = Mathf.Clamp01(b.uniqueChance + T(SkillAbility.UniqueSpawn)),
                GoldenChance = Mathf.Clamp01(T(SkillAbility.GoldenSpawn)),
                BonusDamage = Mathf.RoundToInt(T(SkillAbility.HarvesterDamage)),
                SpeedMul = 1f + T(SkillAbility.HarvesterSpeed),
                SizeMul = 1f + T(SkillAbility.HarvesterSize),
                BarMul = 1f + T(SkillAbility.BarLength),
                CritChance = Mathf.Clamp01(T(SkillAbility.CritChance)),
                CritMul = 1f + (critDamage > 0f ? critDamage : b.critDamage),
                ComboFactor = b.comboFactor * (1f + T(SkillAbility.ComboRate)),
                ScoreMul = 1f + T(SkillAbility.ScoreRate),
                GoldMul = 1f + T(SkillAbility.GoldConversion),
                TripleChance = Mathf.Clamp01(T(SkillAbility.TripleScoreChance)),
                NormalYieldMul = 1f + T(SkillAbility.NormalYield),
                EpicYieldMul = 1f + T(SkillAbility.EpicYield),
                UniqueYieldMul = 1f + T(SkillAbility.UniqueYield),
                GoldenScore = b.goldenScore,
                GoldenYield = b.goldenYield,
                FinalRushBonus = T(SkillAbility.FinalRushBonus),
                FinalRushSeconds = SkillProgress.Param(SkillAbility.FinalRushBonus, b.finalRushSeconds),
            };
        }

        public float YieldMul(MushroomTier tier)
        {
            switch (tier)
            {
                case MushroomTier.Epic: return EpicYieldMul;
                case MushroomTier.Unique: return UniqueYieldMul;
                case MushroomTier.Normal: return NormalYieldMul;
                default: return 1f;
            }
        }
    }
}
