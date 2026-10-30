using UnityEngine;

namespace MoreMush.Mobile
{
    // 방치형 강화 종류. 순서 = 강화 목록에 보이는 순서 (저장 키는 이름이라 순서를 바꿔도 된다)
    // AdvancedAttack·NormalEnemyDamage = 후반 강화 (레전드 오브 슬라임 1.13.1의 상급 공격력·일반 적 피해량)
    public enum IdleStat { Attack, Hp, HpRegen, Speed, CritChance, CritDamage, AdvancedAttack, NormalEnemyDamage }

    // 방치형 진행: 스테이지와 강화 레벨. PlayerPrefs("Idle.Stage", "Idle.Lv.<종류>")에 저장한다 (세이브 구조가 생기면 옮긴다).
    // 밸런스 초안 (BALANCE_IDLE.md): 공식은 이 파일 한 곳에만 둔다.
    //   능력치는 레벨마다 같은 양씩 오르고(선형), 강화 비용은 (1 + Lv/10)^지수로 점점 크게 오른다.
    //   적은 "스테이지 s면 이만큼 키웠을 것"(기대 성장 곡선)에서 체력·공격력·골드를 계산한다 — 곡선만 고치면 적이 따라온다
    public static class IdleProgress
    {
        public const int WavesPerStage = 5;     // 이 수만큼 웨이브를 넘기면 보스
        public const float BossTime = 30f;      // 보스 제한 시간(초). 넘기면 같은 스테이지 1웨이브부터 다시

        public class Def
        {
            public IdleStat stat;
            public string name, icon;           // icon: Resources 경로 (임시 그림)
            public double baseValue, perLevel;  // 값 = baseValue + perLevel × Lv (공격 속도만 baseValue × (1 + perLevel × Lv))
            public int maxLv;
            public double costBase, costExp;    // 비용 = costBase × (1 + Lv/10)^costExp × e^(Lv/costScale)
            public double costScale;            // 0 = 지수 항 없음. = FarmScale × (기대 레벨/스테이지) → 기대 레벨에서 e^((s-1)/FarmScale)
            public IdleStat unlockStat;         // 이 강화가 unlockLv 이상이어야 열린다 (unlockLv 0 = 처음부터)
            public int unlockLv;
            public int capEvery, capStep;       // 단계 제한: 열린 뒤 unlockStat이 capEvery 오를 때마다 capStep레벨씩 더 올릴 수 있다 (0 = 제한 없음)
        }

        public static readonly Def[] Defs =
        {
            // maxLv 45000·28000·8500 = 비용이 long 한계(9e18)에 닿기 전 (공격력·체력 Lv 45623, 체력 재생 Lv 28423, 공격 속도 Lv 8578에서 닿는다).
            // 공격 속도는 최대치 없이 계속 오르고 지수 항으로만 늦춘다 (costScale 600 → 스테이지 1000 도달 70.4일, 밸런스설계/verify/plan_final.py)
            new Def { stat = IdleStat.Attack,     name = "공격력",      icon = "Art/Dream/Icons/hammer",   baseValue = 10,  perLevel = 2,      maxLv = 45000,  costBase = 10,  costExp = 2.5, costScale = 2250 },
            new Def { stat = IdleStat.Hp,         name = "체력",        icon = "Art/Dream/Icons/mushtree", baseValue = 100, perLevel = 20,     maxLv = 45000,  costBase = 10,  costExp = 2.5, costScale = 2250 },
            new Def { stat = IdleStat.HpRegen,    name = "체력 재생",    icon = "Art/Dream/Icons/t_forest", baseValue = 2,   perLevel = 0.4,    maxLv = 28000,  costBase = 15,  costExp = 2.5, costScale = 1350, unlockStat = IdleStat.Hp, unlockLv = 20 },
            new Def { stat = IdleStat.Speed,      name = "공격 속도",    icon = "Art/Dream/Icons/clock",    baseValue = 0.8, perLevel = 0.01,   maxLv = 8500,   costBase = 300, costExp = 3.5, costScale = 600, unlockStat = IdleStat.Attack, unlockLv = 30 },
            // 치명타: 공격력 Lv에 묶은 단계 제한 (공격력 100레벨마다 25레벨씩 — 레전드 오브 슬라임 치명타 피해 방식)
            new Def { stat = IdleStat.CritChance, name = "치명타 확률",  icon = "Art/Dream/Icons/gem",      baseValue = 0,   perLevel = 0.001,  maxLv = 1000,   costBase = 30,  costExp = 2.5, costScale = 450, unlockStat = IdleStat.Attack, unlockLv = 100, capEvery = 100, capStep = 25 },
            new Def { stat = IdleStat.CritDamage, name = "치명타 피해",  icon = "Art/Dream/Icons/f_beauty", baseValue = 1.5, perLevel = 0.004,  maxLv = 2500,   costBase = 30,  costExp = 2.5, costScale = 338, unlockStat = IdleStat.Attack, unlockLv = 300, capEvery = 100, capStep = 25 },
            // 후반 강화: 공격력 Lv 3000(먼작귀 포켓의 "공격력 3000" 조건)에 열리고, 일반 적 피해량은 상급 공격력 Lv 150에 열린다 (레전드 오브 슬라임과 같은 150)
            new Def { stat = IdleStat.AdvancedAttack,    name = "상급 공격력",   icon = "Art/Dream/Icons/book",  baseValue = 1, perLevel = 0.005, maxLv = 2000, costBase = 1e7, costExp = 1.5, costScale = 281, unlockStat = IdleStat.Attack, unlockLv = 3000, capEvery = 100, capStep = 25 },
            new Def { stat = IdleStat.NormalEnemyDamage, name = "일반 적 피해량", icon = "Art/Dream/Icons/quest", baseValue = 1, perLevel = 0.01,  maxLv = 1000, costBase = 2e7, costExp = 1.5, costScale = 141, unlockStat = IdleStat.AdvancedAttack, unlockLv = 150 },
        };

        public static event System.Action Changed;

        // Defs는 IdleStat 순서대로 적혀 있다 — 번호로 바로 꺼내고, 순서가 어긋나면 찾아서 쓴다
        public static Def Get(IdleStat stat)
        {
            int i = (int)stat;
            if (i >= 0 && i < Defs.Length && Defs[i].stat == stat) return Defs[i];
            foreach (var d in Defs) if (d.stat == stat) return d;
            return Defs[0];
        }

        // 강화 레벨은 전투 중 매 프레임 여러 번 읽는다 → PlayerPrefs(안드로이드에서 느림)를 처음 한 번만 읽고 메모리에 둔다.
        // 쓰기는 TryUpgrade만 한다. PlayerPrefs를 밖에서 지웠으면 ReloadLevels로 다시 읽게 한다
        static int[] levels;
        static readonly string[] LevelKeys = BuildLevelKeys();

        static string[] BuildLevelKeys()
        {
            var keys = new string[System.Enum.GetValues(typeof(IdleStat)).Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = "Idle.Lv." + (IdleStat)i;
            return keys;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ReloadLevels() => levels = null;

        public static int Stage
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt("Idle.Stage", 1));
            set { PlayerPrefs.SetInt("Idle.Stage", Mathf.Max(1, value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static int Level(IdleStat stat)
        {
            if (levels == null)
            {
                levels = new int[LevelKeys.Length];
                for (int i = 0; i < levels.Length; i++) levels[i] = PlayerPrefs.GetInt(LevelKeys[i], 0);
            }
            return Mathf.Clamp(levels[(int)stat], 0, Get(stat).maxLv);
        }

        public static bool Unlocked(IdleStat stat)
        {
            var d = Get(stat);
            return d.unlockLv <= 0 || Level(d.unlockStat) >= d.unlockLv;
        }

        public static bool IsMax(IdleStat stat) => Level(stat) >= Get(stat).maxLv;

        // 단계 제한까지 올릴 수 있는 최대 레벨 (잠겨 있으면 0, 제한 없으면 maxLv)
        public static int CapLevel(IdleStat stat) => Unlocked(stat) ? CapLevel(stat, Level(Get(stat).unlockStat)) : 0;

        public static int CapLevel(IdleStat stat, int gateLv)
        {
            var d = Get(stat);
            if (d.capEvery <= 0) return d.maxLv;
            if (gateLv < d.unlockLv) return 0;
            long cap = ((long)(gateLv - d.unlockLv) / d.capEvery + 1) * d.capStep;
            return (int)System.Math.Min(d.maxLv, cap);
        }

        // 열렸지만 단계 제한에 막혀 더 못 올린다
        public static bool IsCapped(IdleStat stat) => Unlocked(stat) && !IsMax(stat) && Level(stat) >= CapLevel(stat);

        // 다음 단계가 열리는 unlockStat 레벨
        public static int NextCapGateLevel(IdleStat stat)
        {
            var d = Get(stat);
            return d.capStep <= 0 ? d.unlockLv : d.unlockLv + Level(stat) / d.capStep * d.capEvery;
        }

        // 다음 레벨로 올리는 값
        public static long Cost(IdleStat stat) => Cost(stat, Level(stat));

        public static long Cost(IdleStat stat, int lv)
        {
            var d = Get(stat);
            double c = d.costBase * System.Math.Pow(1 + lv / 10.0, d.costExp);
            if (d.costScale > 0) c *= System.Math.Exp(lv / d.costScale);
            return (long)System.Math.Min(9e18, System.Math.Ceiling(c));
        }

        public static bool TryUpgrade(IdleStat stat)
        {
            if (!Unlocked(stat) || IsMax(stat) || IsCapped(stat)) return false;
            if (!Wallet.TrySpend(Currency.Gold, Cost(stat))) return false;
            int next = Level(stat) + 1;
            levels[(int)stat] = next;
            PlayerPrefs.SetInt(LevelKeys[(int)stat], next);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        // ── 능력치 (레벨 → 값) ──
        public static double Value(IdleStat stat) => Value(stat, Level(stat));

        public static double Value(IdleStat stat, int lv)
        {
            var d = Get(stat);
            lv = Mathf.Clamp(lv, 0, d.maxLv);
            return stat == IdleStat.Speed ? d.baseValue * (1 + d.perLevel * lv) : d.baseValue + d.perLevel * lv;
        }

        // 보유 효과까지 더한 실제 값: 무기·펫 → 공격력 %, 장신구 → 체력·체력 재생 %
        public static double AttackMultiplier => 1 + (GachaProgress.OwnPercent(GachaKind.Weapon) + GachaProgress.OwnPercent(GachaKind.Pet)) / 100.0;
        public static double HpMultiplier => 1 + GachaProgress.OwnPercent(GachaKind.Accessory) / 100.0;
        public static double FinalAttack => Value(IdleStat.Attack) * AttackMultiplier * Value(IdleStat.AdvancedAttack);
        public static double FinalHp => Value(IdleStat.Hp) * HpMultiplier;
        public static double FinalHpRegen => Value(IdleStat.HpRegen) * HpMultiplier;

        public static string FormatValue(IdleStat stat, double v)
        {
            switch (stat)
            {
                case IdleStat.Speed: return v.ToString("0.00") + "/초";
                case IdleStat.HpRegen: return FormatNumber(v) + "/초";
                case IdleStat.CritChance: return (v * 100).ToString("0.0") + "%";
                case IdleStat.CritDamage:
                case IdleStat.AdvancedAttack:
                case IdleStat.NormalEnemyDamage: return (v * 100).ToString("0.#") + "%";
            }
            return FormatNumber(v);
        }

        // ── 기대 성장 곡선: 스테이지 s에 도착한 플레이어가 가졌을 것으로 보는 강화 레벨·보유 효과 ──
        public const int LevelsPerStage = 15;   // 공격력·체력 기대 레벨 = 15 × (스테이지 - 1) (스테이지 1 = Lv 0, 스테이지 201 = Lv 3000). 예전 10에서 1.5배

        public static int ExpectedLevel(IdleStat stat, int stage)
        {
            int s = Mathf.Max(0, stage - 1);
            var d = Get(stat);
            switch (stat)
            {
                case IdleStat.Speed: return Mathf.Min(d.maxLv, (int)(1.5 * s));
                case IdleStat.HpRegen: return Mathf.Min(d.maxLv, (int)(LevelsPerStage * s * 0.6));
                // 단계 제한이 있는 강화: 기대 기준 레벨에서 열린 한도의 일부만 찍었다고 본다
                case IdleStat.CritChance: return ExpectedCapped(stat, stage, 0.8);
                case IdleStat.CritDamage: return ExpectedCapped(stat, stage, 0.6);
                case IdleStat.AdvancedAttack: return ExpectedCapped(stat, stage, 0.5);
                case IdleStat.NormalEnemyDamage: return Mathf.Clamp((ExpectedLevel(d.unlockStat, stage) - d.unlockLv) / 2, 0, d.maxLv);
            }
            return Mathf.Min(d.maxLv, LevelsPerStage * s);
        }

        // 단계 제한 한도 × ratio. 한도는 계단(25레벨씩)이 아니라 이어진 값으로 본다 — 계단이면 그 스테이지만 골드가 튄다
        static int ExpectedCapped(IdleStat stat, int stage, double ratio)
        {
            var d = Get(stat);
            int gate = ExpectedLevel(d.unlockStat, stage);
            if (gate < d.unlockLv) return 0;
            double cap = ((double)(gate - d.unlockLv) / d.capEvery + 1) * d.capStep;
            return (int)System.Math.Min(d.maxLv, cap * ratio);
        }

        static double ExpectedAttackMultiplier(int s) => 1 + 0.012 * s;   // 무기+펫 보유 효과 (스테이지 1000 ≈ ×13)
        static double ExpectedHpMultiplier(int s) => 1 + 0.007 * s;       // 장신구 보유 효과 (스테이지 1000 ≈ ×8)

        static double ExpectedHit(int s)
        {
            double cc = System.Math.Min(1, Value(IdleStat.CritChance, ExpectedLevel(IdleStat.CritChance, s)));
            double cd = Value(IdleStat.CritDamage, ExpectedLevel(IdleStat.CritDamage, s));
            return Value(IdleStat.Attack, ExpectedLevel(IdleStat.Attack, s)) * ExpectedAttackMultiplier(s)
                * Value(IdleStat.AdvancedAttack, ExpectedLevel(IdleStat.AdvancedAttack, s)) * (1 + cc * (cd - 1));
        }

        static double ExpectedDps(int s) => ExpectedHit(s) * Formation.SlotCount * Value(IdleStat.Speed, ExpectedLevel(IdleStat.Speed, s));
        static double ExpectedHp(int s) => Value(IdleStat.Hp, ExpectedLevel(IdleStat.Hp, s)) * ExpectedHpMultiplier(s);
        static double ExpectedRegen(int s) => Value(IdleStat.HpRegen, ExpectedLevel(IdleStat.HpRegen, s)) * ExpectedHpMultiplier(s);

        // ── 적 (스테이지·웨이브 → 값) ──
        // 웨이브당 적 수: 5에서 시작해 웨이브·스테이지가 오를수록 늘고 15에서 멈춘다
        public static int EnemyCount(int stage, int wave) => Mathf.Clamp(5 + 2 * (wave - 1) + (stage - 1), 5, 15);

        // 기대 성장이면 일반 적은 투척 2번, 보스는 18초(제한 30초)에 잡힌다. 일반 적은 일반 적 피해량까지 반영한다
        public static double EnemyHp(int stage, int wave) => ExpectedHit(stage) * Value(IdleStat.NormalEnemyDamage, ExpectedLevel(IdleStat.NormalEnemyDamage, stage))
            * 2.0 * (1 + 0.08 * (wave - 1));
        public static double BossHp(int stage) => ExpectedDps(stage) * 18.0;

        // 적 공격(초당 피해): 일반 적은 1마리씩, 보스는 혼자. 기대 성장이면 웨이브마다 체력이 조금씩 깎이고 보스에서 바닥 근처까지 간다
        public static double EnemyAttack(int stage) => 0.007 * ExpectedHp(stage) + 0.15 * ExpectedRegen(stage);
        public static double BossAttack(int stage) => ExpectedRegen(stage) + 0.025 * ExpectedHp(stage);

        // 골드(생산량)는 스테이지의 거듭제곱으로 계속 오르고, 강화 비용(소모량)은 거듭제곱 × 지수(costScale)라 더 빨리 오른다.
        // 그 차이 ≈ FarmFactor(s) = e^((s-1)/FarmScale)가 스테이지당 반복 판 수 → 누적 플레이 시간 대비 스테이지가 로그 곡선
        //   (밸런스설계/이론.md: 100스테이지 1.5시간 · 300 11시간 · 500 2.4일 · 1000 70일, 한 판 40초 가정). 전체 속도는 GoldBase로 조절한다
        public const double FarmScale = 150;
        public static double FarmFactor(int stage) => System.Math.Exp((stage - 1) / FarmScale);

        public const double GoldBase = 30, GoldStep = 1.5, GoldExp = 2.5;

        public static long EnemyGold(int stage) =>
            (long)System.Math.Max(1, System.Math.Round(GoldBase * System.Math.Pow(1 + (stage - 1) / GoldStep, GoldExp)));

        public static long BossGold(int stage) => EnemyGold(stage) * 20;

        // 보스 처치 균사석 (뽑기 재화). 보스를 잡아야만 다음 스테이지로 가므로 스테이지마다 한 번
        public static long BossMycelite(int stage) => 5 + stage / 10;

        // 피해·체력 표시: Wallet과 같은 단위(만/억/조/경)
        public static string FormatNumber(double v)
        {
            if (v < 1) return v.ToString("0.#");
            if (v < 100) return v.ToString("0.#");
            return Wallet.Format((long)System.Math.Min(v, 9e18));
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("MoreMush/방치형 진행 초기화")]
        static void ResetAll()
        {
            PlayerPrefs.DeleteKey("Idle.Stage");
            foreach (var key in LevelKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            ReloadLevels();
            Changed?.Invoke();
        }
#endif
    }
}
