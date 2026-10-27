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
        public float comboFactor = 0.05f;     // 콤보 1당 점수 +5%
        public int comboCap = 20;             // 점수 배율에 쓰는 콤보 상한 (HUD 콤보는 계속 오른다). 0이면 상한 없음
        public float critDamage = 0.5f;       // 치명 박살이 없을 때 치명타 피해 +50%
        public float goldenScore = 10f;       // 황금 변종 점수 배율
        public float goldenYield = 5f;        // 황금 변종 수확량 배율
        public float finalRushSeconds = 5f;   // 할아버지의 전설이 걸리는 마지막 시간 (표의 param이 있으면 그 값)

        [Header("2단계: 버프·소환 (표에 없는 값)")]
        public float hasteBoost = 0.3f;       // 가속 중 속도 +30%
        public int splitCount = 2;            // 분열 때 나오는 수확기 수
        public float splitSize = 0.7f;        // 분열체 크기 (본체 대비)
        public float stumpAttack = 0.5f;      // 그루터기 버프 공격력 +50%
        public float stumpDuration = 5f;
        public float flowerHaste = 2f;        // 꽃밭을 지나면 가속 몇 초
        public float acornScoreMul = 2f;      // 도토리를 모두 밝히면 점수 배율
        public float acornDuration = 5f;
        public float orbitDamageBase = 0.05f; // 회전 회오리가 없을 때 꼬마 수확기 공격력 (공격력 대비)
        public float lightningRadius = 110f;  // 번개가 맞히는 반지름

        [Header("3단계: 포자 디버프 (표에 없는 값)")]
        [Range(0f, 1f)] public float sporeChance = 0.2f;   // 군락(가운데 버섯)에 부딪힐 때 디버프를 얻을 확률
        public float sporeDuration = 3f;
        public float sporeSlow = 0.75f;       // 느림: 속도 배율
        public float sporeWeak = 0.5f;        // 약화: 공격력 배율

        [Header("보스 판 (보스별 값은 Boss.xlsx, 스테이지 계수는 Stage.xlsx)")]
        public float bossBaseHp = 100f;       // 기본 보스 체력. × 보스 hpRate × 스테이지 hpRate
        public float bossSize = 1.8f;         // 군락 버섯 대비 크기
        public Color bossTint = new Color(1f, 0.45f, 0.45f);   // 보스 color가 비었을 때
        public float bossHitShare = 0.35f;    // 기본 비율: 보스 체력을 다 깎는 만큼 때리면 직전 일반 판 점수의 이 비율 (덜 깎으면 비례해서 덜 받음)
        public float bossHitScore = 5f;       // 직전 일반 판 기록이 없을 때: 피해 1당 점수 (스테이지 hpRate로 나눔)
        public float bossRewardMul = 3f;      // 처치 보상 골드 = max(이번 판 골드, 직전 일반 판 골드) × 이 값 (PC판 BOSS_REWARD_MUL 2 → 패배 패널티를 없앤 대신 3)
        public float splitCloneSize = 0.75f;  // 환상 버섯 분신 크기 (본체 대비)

        [Header("일반 스테이지 목표 (목표 수치·버섯 체력 배율은 Stage.xlsx)")]
        public float goalRelief = 0.9f;       // 같은 스테이지를 연속으로 못 넘을 때마다 목표 × 이 값
        public float goalReliefMin = 0.7f;    // 줄여 주는 하한 (원래 목표 대비)
        public Color hardTint = new Color(0.8f, 0.8f, 0.8f);   // 단단한 버섯(mushHpMul > 1) 임시 구분 색 (등급 색에 곱한다)
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
        public int ComboCap;                  // 점수 배율에 쓰는 콤보 상한 (0 = 없음)
        public float ScoreMul, GoldMul, TripleChance;
        public float NormalYieldMul, EpicYieldMul, UniqueYieldMul;
        public float GoldenScore, GoldenYield;
        public float FinalRushBonus, FinalRushSeconds;
        // 2단계
        public RoundBase Base;
        public int PowerAttack; public float PowerDuration;
        public float HasteDuration, PierceDuration, HomingDuration, HealDuration;
        public float FeverDuration, FeverCombo;
        public float WisdomRate, WisdomDuration;
        public float SplitDuration, SplitChance;
        public float CloneBarDuration, CloneBarChance; public int CloneBarMax;
        public float OpeningChance;
        public int ExtraHarvesters, OrbitCount; public float OrbitDamage;
        public float PoisonChance, PoisonMul;
        public float LightningChance, LightningDamage; public bool LightningAnywhere;
        public float WhirlChance, WhirlDamage, WhirlDuration;
        public float BurstChance, BurstDamage;
        public int Restoration;
        // 3단계
        public float CleanseChance, BossDamageMul, LegendaryYieldMul;
        // 스테이지 (HarvestGame이 StageProgress로 넣는다)
        public StageData Stage;
        public BossData Boss;                 // 보스 스테이지가 아니면 null
        public bool BossRound => Boss != null;
        public float BossHp => Boss != null ? Base.bossBaseHp * Mathf.Max(0.0001f, Boss.hpRate) * HpRate : 0f;
        public float BossHpMul = 1f;          // 측정기 전용: 보스를 죽지 않게 해 판 전체 피해를 잴 때 (게임에선 1)
        public float HpRate => Stage != null && Stage.hpRate > 0f ? Stage.hpRate : 1f;
        public float RewardRate => Stage != null && Stage.rewardRate > 0f ? Stage.rewardRate : 1f;
        // 일반 버섯 체력 배율 (Stage.xlsx mushHpMul). 보스 체력(BossHp)엔 곱하지 않는다
        public float MushHpMul => Stage != null && Stage.mushHpMul > 0f ? Stage.mushHpMul : 1f;
        public int Goal;                      // 이번 판 수확 목표 (완화 반영). 0이면 목표 없음 (보스·파밍·목표 없는 스테이지)
        public bool HasGoal => Goal > 0;

        public const int TutorialStages = 3;  // 1~3 튜토리얼: 목표 3·4·5 고정 (연속 실패 완화 없음)

        // 스테이지 목표에 연속 실패 완화를 걸어 이번 판 목표를 정한다. 일반 판이 아니거나 목표가 없으면 0
        public static int GoalFor(StageData stage, int failStreak, RoundBase b)
        {
            if (stage == null || stage.boss != 0 || stage.goalType != StageGoal.Harvest || stage.goal <= 0) return 0;
            if (stage.stage <= TutorialStages) return stage.goal;
            float relief = Mathf.Max(b.goalReliefMin, Mathf.Pow(b.goalRelief, Mathf.Max(0, failStreak)));
            return Mathf.Max(1, Mathf.RoundToInt(stage.goal * relief));
        }

        public bool AnyBarBuff => PowerAttack > 0 || HasteDuration > 0f || PierceDuration > 0f || HomingDuration > 0f
                                  || FeverDuration > 0f || WisdomRate > 0f || HealDuration > 0f;
        // 분열로 한꺼번에 있을 수 있는 최대 수 (던지는 수확기마다 한 번씩) — 이만큼 미리 만들어 둔다
        public int MaxSplit => SplitDuration > 0f ? (1 + ExtraHarvesters) * Base.splitCount : 0;

        public static RoundStats FromSkills(RoundBase b)
        {
            float T(string ability) => SkillProgress.Total(ability);
            float P(string ability, float fallback) => SkillProgress.Param(ability, fallback);
            float critDamage = T(SkillAbility.CritDamage);
            float cloneDuration = T(SkillAbility.CloneBar);
            return new RoundStats
            {
                Base = b,
                PowerAttack = Mathf.RoundToInt(T(SkillAbility.PowerBuff)),
                PowerDuration = P(SkillAbility.PowerBuff, 3f),
                HasteDuration = T(SkillAbility.HasteBuff),
                PierceDuration = T(SkillAbility.PierceBuff),
                HomingDuration = T(SkillAbility.HomingBuff),
                HealDuration = T(SkillAbility.HealBuff),
                FeverDuration = T(SkillAbility.FeverBuff),
                FeverCombo = P(SkillAbility.FeverBuff, 0.5f),
                WisdomRate = T(SkillAbility.WisdomBuff),
                WisdomDuration = P(SkillAbility.WisdomBuff, 3f),
                SplitDuration = T(SkillAbility.SplitBuff),
                SplitChance = P(SkillAbility.SplitBuff, 0.1f),
                CloneBarDuration = cloneDuration,
                CloneBarChance = P(SkillAbility.CloneBar, 0.1f),
                CloneBarMax = cloneDuration > 0f ? 1 + Mathf.RoundToInt(T(SkillAbility.CloneBarCount)) : 0,
                OpeningChance = Mathf.Clamp01(T(SkillAbility.OpeningBuff)),
                ExtraHarvesters = Mathf.RoundToInt(T(SkillAbility.HarvesterSlot)),
                OrbitCount = Mathf.RoundToInt(T(SkillAbility.OrbitCount)),
                OrbitDamage = T(SkillAbility.OrbitDamage) > 0f ? T(SkillAbility.OrbitDamage) : b.orbitDamageBase,
                PoisonChance = T(SkillAbility.PoisonStrike),
                PoisonMul = 1f + P(SkillAbility.PoisonStrike, 2f),
                LightningChance = T(SkillAbility.ChainLightning),
                LightningDamage = P(SkillAbility.ChainLightning, 0.3f),
                LightningAnywhere = SkillProgress.SeriesLevel("chain_lightning") >= 3,
                WhirlChance = T(SkillAbility.Whirlwind),
                WhirlDamage = P(SkillAbility.Whirlwind, 0.05f),
                WhirlDuration = 2f + SkillProgress.SeriesLevel("whirlwind"),   // 3·4·5초
                BurstChance = T(SkillAbility.SporeBurst),
                BurstDamage = P(SkillAbility.SporeBurst, 0.1f),
                Restoration = Mathf.RoundToInt(T(SkillAbility.FieldRestoration)),
                CleanseChance = Mathf.Clamp01(T(SkillAbility.DebuffCleanse)),
                BossDamageMul = 1f + T(SkillAbility.BossDamage),
                LegendaryYieldMul = 1f + T(SkillAbility.LegendaryYield),

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
                ComboCap = b.comboCap,
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
                case MushroomTier.Legendary: return LegendaryYieldMul;
                default: return 1f;
            }
        }
    }
}
