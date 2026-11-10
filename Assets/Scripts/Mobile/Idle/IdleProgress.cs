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
        // 이 수만큼 웨이브를 넘기면 보스 / 보스 제한 시간(초, 넘기면 같은 스테이지 1웨이브부터 다시) — 스테이지 시트 값
        // 0이면 진행 막대가 NaN이 되고 보스가 바로 실패한다 — 표가 틀려도 최소값을 둔다 (검증기는 양수만 통과시킨다)
        public static int WavesPerStage(int stage) => Mathf.Max(1, GameBalance.Stage(stage).wavesPerStage);
        public static float BossTime(int stage) => Mathf.Max(1f, (float)GameBalance.Stage(stage).bossTime);

        public class Def
        {
            public IdleStat stat;
            public string name, icon;           // icon: Resources 경로 (임시 그림)
            public double baseValue, perLevel;  // 값 = baseValue + perLevel × Lv (공격 속도만 baseValue × (1 + perLevel × Lv))
            public double valueExponent, valueDivisor;   // power면 값 = baseValue × (1 + Lv / valueDivisor)^valueExponent (레벨당 오르는 양이 레벨과 함께 커진다)
            public int maxLv;
            public double costBase, costExp;    // 비용 = costBase × (1 + Lv/10)^costExp × e^(Lv/costScale)
            public double costScale;            // 0 = 지수 항 없음. = FarmScale × (기대 레벨/스테이지) → 기대 레벨에서 e^((s-1)/FarmScale)
            public double costDivisor;          // 비용 식의 (1 + Lv/costDivisor)
            public bool multiply;               // true면 값 = baseValue × (1 + perLevel × Lv) (공격 속도)
            public bool power;                  // true면 값 = baseValue × (1 + Lv / valueDivisor)^valueExponent
            public bool incremental;            // true면 초반 IncLinearLv까지 레벨당 perLevel씩, 그 뒤 증가량이 (1 + n / valueDivisor)²배로 커진다 (값은 정수로 반올림)
            public IdleStat unlockStat;         // 이 강화가 unlockLv 이상이어야 열린다 (unlockLv 0 = 처음부터)
            public int unlockLv;
            public int capEvery, capStep;       // 단계 제한: 열린 뒤 unlockStat이 capEvery 오를 때마다 capStep레벨씩 더 올릴 수 있다 (0 = 제한 없음)
        }

        // 강화 8종은 업그레이드 시트에서 읽는다 (IdleStat 순서대로). 값 설명은 엑셀 info 시트와 GameBalance.cs.
        // maxLevel은 비용이 long 한계(9e18)에 닿기 전으로 잡는다 (Power 3·가격 지수 3.5에서 공격력·체력 Lv 29973, 체력 재생 Lv 19473에 닿는다 → 29900 / 19400).
        // 공격 속도는 Lv 275(3.00/s)에서 봉인한다 (밸런스설계/README.md 기준 5)
        static Def[] defs;

        public static Def[] Defs => defs ?? (defs = BuildDefs());

        static Def[] BuildDefs()
        {
            var rows = GameBalance.Upgrades;
            var list = new Def[System.Enum.GetValues(typeof(IdleStat)).Length];
            foreach (var r in rows)
            {
                int i = (int)r.stat;
                if (i < 0 || i >= list.Length) continue;
                list[i] = new Def
                {
                    stat = r.stat, name = r.name, icon = GameBalance.ResourcePath(r.resourceId),
                    baseValue = r.baseValue, perLevel = r.perLevel, maxLv = r.maxLevel,
                    costBase = r.price, costExp = r.priceExponent, costScale = r.priceScale, costDivisor = r.priceDivisor,
                    multiply = r.valueMode == "Multiply",
                    power = r.valueMode == "Power" && r.valueDivisor > 0,
                    incremental = r.valueMode == "Incremental" && r.valueDivisor > 0,
                    valueExponent = r.valueExponent, valueDivisor = r.valueDivisor,
                    unlockStat = r.unlockStat, unlockLv = r.unlockLevel, capEvery = r.capEvery, capStep = r.capStep,
                };
            }
            for (int i = 0; i < list.Length; i++)
                if (list[i] == null) throw new System.InvalidOperationException($"[IdleProgress] 업그레이드 시트에 {(IdleStat)i} 행이 없다");
            return list;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTables() { defs = null; curve = null; costCacheLv = null; }

        public static event System.Action Changed;

        // 화면에 보이는 강화 이름: 문자열 표 upgrade.<종류> (없으면 업그레이드 시트 name)
        public static string DisplayName(IdleStat stat) => Loc.T("upgrade." + stat, Get(stat).name);

        // Defs는 IdleStat 순서대로 적혀 있다 — 번호로 바로 꺼내고, 순서가 어긋나면 찾아서 쓴다
        public static Def Get(IdleStat stat)
        {
            var all = Defs;
            int i = (int)stat;
            if (i >= 0 && i < all.Length && all[i].stat == stat) return all[i];
            foreach (var d in all) if (d.stat == stat) return d;
            return all[0];
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
        public static void ReloadLevels() { levels = null; stage = 0; }

        static int stage;   // 0 = 아직 안 읽음
        public static int Stage
        {
            get { if (stage <= 0) stage = Mathf.Max(1, PlayerPrefs.GetInt("Idle.Stage", 1)); return stage; }   // 처치마다 읽힌다 — 메모리에 둔다 (안드로이드 PlayerPrefs 읽기는 JNI)
            set { stage = Mathf.Max(1, value); PlayerPrefs.SetInt("Idle.Stage", stage); SaveScheduler.MarkDirty(); Changed?.Invoke(); }   // 초반엔 몇 초마다 오른다 — 파일 쓰기는 SaveScheduler가 모아서 (앱을 내릴 때도 저장)
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
            if (stat == IdleStat.Speed) return SpeedCap(gateLv, d.maxLv);
            if (d.capEvery <= 0) return d.maxLv;
            if (gateLv < d.unlockLv) return 0;
            long cap = ((long)(gateLv - d.unlockLv) / d.capEvery + 1) * d.capStep;
            return (int)System.Math.Min(d.maxLv, cap);
        }

        // 공격 속도 단계표: 공격력이 gate 이상이면 공격 속도를 cap레벨까지 올릴 수 있다 (gate는 5의 배수, 첫 gate = 해금 레벨).
        // 속도 1.5(Lv 87.5)까지는 30레벨씩, 2.0(Lv 150)까지 40, 그 뒤 3.0(Lv 275)까지 50. 마지막 단계(공격력 2500)에서 3.0/s 마스터
        static readonly (int gate, int cap)[] SpeedSteps =
        {
            (20, 30), (375, 60), (730, 90), (1085, 130), (1440, 170), (1795, 220), (2150, 270), (2500, 275),
        };

        static int SpeedCap(int gateLv, int maxLv)
        {
            int cap = 0;
            foreach (var step in SpeedSteps) if (gateLv >= step.gate) cap = step.cap;
            return System.Math.Min(maxLv, cap);
        }

        // 열렸지만 단계 제한에 막혀 더 못 올린다
        public static bool IsCapped(IdleStat stat) => Unlocked(stat) && !IsMax(stat) && Level(stat) >= CapLevel(stat);

        // 다음 단계가 열리는 unlockStat 레벨
        public static int NextCapGateLevel(IdleStat stat)
        {
            var d = Get(stat);
            if (stat == IdleStat.Speed)
            {
                foreach (var step in SpeedSteps) if (step.cap > Level(stat)) return step.gate;
                return SpeedSteps[SpeedSteps.Length - 1].gate;
            }
            return d.capStep <= 0 ? d.unlockLv : d.unlockLv + Level(stat) / d.capStep * d.capEvery;
        }

        // 다음 레벨로 올리는 값
        public static long Cost(IdleStat stat) => Cost(stat, Level(stat));

        // 마지막으로 계산한 레벨의 비용 (강화 버튼은 갱신마다 같은 레벨 비용을 묻는다 — 거듭제곱·지수를 다시 하지 않게)
        static int[] costCacheLv;
        static long[] costCacheValue;

        public static long Cost(IdleStat stat, int lv)
        {
            int si = (int)stat;
            if (costCacheLv == null)
            {
                int n = System.Enum.GetValues(typeof(IdleStat)).Length;
                costCacheLv = new int[n];
                costCacheValue = new long[n];
                for (int i = 0; i < n; i++) costCacheLv[i] = -1;
            }
            if (si >= 0 && si < costCacheLv.Length && costCacheLv[si] == lv) return costCacheValue[si];
            long cost = ComputeCost(stat, lv);
            if (si >= 0 && si < costCacheLv.Length) { costCacheLv[si] = lv; costCacheValue[si] = cost; }
            return cost;
        }

        static long ComputeCost(IdleStat stat, int lv)
        {
            var d = Get(stat);
            if (d.incremental) return IncrementalCost(d, lv);
            double c = d.costBase * System.Math.Pow(1 + lv / d.costDivisor, d.costExp);
            if (d.costScale > 0) c *= System.Math.Exp(lv / d.costScale);
            if (stat == IdleStat.Speed) c *= SpeedCostRamp(lv, d.maxLv);
            c *= GateCostFactor(stat, d, lv);
            return (long)System.Math.Max(1, System.Math.Min(9e18, System.Math.Ceiling(c)));
        }

        // 증분 강화(공격력 등)로 해금·단계가 열리는 강화의 비용 보정: 그 레벨이 열리는 기준 레벨에서
        // 기준 강화의 비용이 예전 곡선보다 줄어든 비율을 곱한다 → "해금 시점의 공격력 비용 대비 몇 배"가 예전 설계 그대로
        static double GateCostFactor(IdleStat stat, Def d, int lv)
        {
            if (d.unlockLv <= 0 && stat != IdleStat.Speed) return 1.0;
            var g = Get(d.unlockStat);
            if (!g.incremental) return 1.0;
            int gate = GateLevelFor(stat, d, lv);
            double old = g.costBase * System.Math.Pow(1 + gate / g.costDivisor, g.costExp);
            if (g.costScale > 0) old *= System.Math.Exp(gate / g.costScale);
            if (!(old > 0) || double.IsInfinity(old)) return 1.0;    // 지수가 넘치면 보정하지 않는다 (비용이 0이 되지 않게)
            return System.Math.Min(1.0, IncrementalCost(g, gate) / old);
        }

        // lv → lv+1 강화가 열리는 기준 강화 레벨 (단계 제한이 없으면 해금 레벨)
        static int GateLevelFor(IdleStat stat, Def d, int lv)
        {
            if (stat == IdleStat.Speed)
            {
                foreach (var step in SpeedSteps) if (step.cap > lv) return step.gate;
                return SpeedSteps[SpeedSteps.Length - 1].gate;
            }
            return d.capStep <= 0 ? d.unlockLv : d.unlockLv + lv / d.capStep * d.capEvery;
        }

        // 공격 속도 후반 비용 가중: 시작 레벨까지는 1배, 그 뒤로 마지막 강화(만렙 - 1)까지 1 → 1 + speedCostRamp배로 이어서 오른다 (한 레벨에서 튀지 않게)
        static double SpeedCostRamp(int lv, int maxLv)
        {
            int from = GameBalance.ConstInt("speedCostRampFromLv");
            if (lv <= from || maxLv - 1 <= from) return 1.0;
            return 1.0 + GameBalance.Const("speedCostRamp") * (System.Math.Min(lv, maxLv - 1) - from) / (maxLv - 1 - from);
        }

        // ── 증분(Incremental) 강화: 공격력·체력·체력 재생 ──
        // 초반은 숫자가 작고 고르게: 값은 레벨마다 perLevel씩(공격력 +10·체력 +10·재생 +1), 비용은 Lv 50까지 1골드씩·Lv 100까지 2골드씩 오른다.
        // Lv 100 뒤로 값 증가량은 perLevel × (1 + n/valueDivisor)²로 커지고, 비용에는 예전 곡선(n = Lv − 100 레벨)이 더해진다.
        // valueDivisor = √(perLevel × 1000 / (3 × baseValue))이면 후반 값이 예전 Power 곡선(× (1 + Lv/10)³)의 100레벨 뒤와 같아진다
        // (공격력 18.26·체력 5.77·재생 12.91) → 골드 대비 힘은 예전 그대로
        const int IncLinearLv = 100;       // 여기까지 값·비용이 일정하게 오른다
        const int IncCostStep1Lv = 50;     // 여기까지 비용 +1골드씩, 그 뒤 IncLinearLv까지 +2골드씩
        const int IncCostStep1 = 1, IncCostStep2 = 2;

        static double IncrementalValue(Def d, int lv)
        {
            int lin = System.Math.Min(lv, IncLinearLv);
            double n = System.Math.Max(0, lv - IncLinearLv), k = d.valueDivisor;
            // Σ_{j=1..n} (1 + j/k)² = n + n(n+1)/k + n(n+1)(2n+1)/(6k²)
            double grow = n + n * (n + 1) / k + n * (n + 1) * (2 * n + 1) / (6 * k * k);
            return System.Math.Round(d.baseValue + d.perLevel * (lin + grow), System.MidpointRounding.AwayFromZero);
        }

        static long IncrementalCost(Def d, int lv)
        {
            double c = d.costBase + IncCostStep1 * System.Math.Min(lv, IncCostStep1Lv)
                     + IncCostStep2 * System.Math.Max(0, lv - IncCostStep1Lv);
            int n = lv - IncLinearLv;
            if (n > 0)
            {
                double old = d.costBase * System.Math.Pow(1 + n / d.costDivisor, d.costExp);
                if (d.costScale > 0) old *= System.Math.Exp(n / d.costScale);
                c += old - d.costBase;
            }
            return (long)System.Math.Min(9e18, System.Math.Ceiling(c));
        }

        public static bool TryUpgrade(IdleStat stat)
        {
            if (!Unlocked(stat) || IsMax(stat) || IsCapped(stat)) return false;
            if (!Wallet.TrySpend(Currency.Gold, Cost(stat))) return false;
            int next = Level(stat) + 1;
            levels[(int)stat] = next;
            PlayerPrefs.SetInt(LevelKeys[(int)stat], next);
            SaveScheduler.MarkDirty();      // 꾹 누르면 초당 수십 번 — 파일 쓰기는 SaveScheduler가 모아서
            DailyQuests.Add(QuestKind.Upgrade);
            GuideQuests.ReportUpgrade(stat);
            Changed?.Invoke();
            return true;
        }

        // ── 능력치 (레벨 → 값) ──
        public static double Value(IdleStat stat) => Value(stat, Level(stat));

        public static double Value(IdleStat stat, int lv)
        {
            var d = Get(stat);
            lv = Mathf.Clamp(lv, 0, d.maxLv);
            if (d.incremental) return IncrementalValue(d, lv);
            if (d.power) return d.baseValue * System.Math.Pow(1 + lv / d.valueDivisor, d.valueExponent);
            return d.multiply ? d.baseValue * (1 + d.perLevel * lv) : d.baseValue + d.perLevel * lv;
        }

        // 보유 효과까지 더한 실제 값: 무기 → 공격력 %, 방어구 → 체력·체력 재생 %
        public static double AttackMultiplier => 1 + GachaProgress.OwnPercent(GachaKind.Weapon) / 100.0;
        public static double HpMultiplier => 1 + GachaProgress.OwnPercent(GachaKind.Accessory) / 100.0;
        // 세계수 효과(가호·꼬마 특기·장식)도 여기서 곱한다 (WorldTree.Bonus)
        public static double FinalAttack => Value(IdleStat.Attack) * AttackMultiplier * Value(IdleStat.AdvancedAttack) * WorldTree.Mul(TreeEffect.Attack);
        public static double FinalHp => Value(IdleStat.Hp) * HpMultiplier * WorldTree.Mul(TreeEffect.Hp);
        public static double FinalHpRegen => Value(IdleStat.HpRegen) * HpMultiplier * WorldTree.Mul(TreeEffect.Hp);
        public static double FinalSpeed => Value(IdleStat.Speed) * WorldTree.Mul(TreeEffect.Speed);
        public static double FinalCritChance => System.Math.Min(1, Value(IdleStat.CritChance) + WorldTree.Bonus(TreeEffect.CritChance));
        public static double FinalCritDamage => Value(IdleStat.CritDamage) + WorldTree.Bonus(TreeEffect.CritDamage);

        public static string FormatValue(IdleStat stat, double v)
        {
            switch (stat)
            {
                case IdleStat.Speed: return v.ToString("0.00") + Loc.T("unit.per_sec");
                case IdleStat.HpRegen: return FormatNumber(v) + Loc.T("unit.per_sec");
                case IdleStat.CritChance: return (v * 100).ToString("0.0") + "%";
                case IdleStat.CritDamage:
                case IdleStat.AdvancedAttack:
                case IdleStat.NormalEnemyDamage: return (v * 100).ToString("0.#") + "%";
            }
            return FormatNumber(v);
        }

        // ── 기대 성장 곡선: 스테이지 s에 도착한 플레이어가 가졌을 것으로 보는 강화 레벨·보유 효과 ──
        // 곡선의 수치는 상수 시트에 있다 (levelsPerStage, exp*, enemy*, boss* …). 수식은 여기에 둔다
        sealed class Curve
        {
            public readonly int levelsPerStage = GameBalance.ConstInt("levelsPerStage");   // 공격력·체력 기대 레벨 = 15 × (스테이지 - 1) (스테이지 1 = Lv 0, 스테이지 201 = Lv 3000)
            public readonly double speedPerStage = GameBalance.Const("expSpeedPerStage");
            public readonly double regenRatio = GameBalance.Const("expRegenRatio");
            public readonly double critChanceRatio = GameBalance.Const("expCritChanceRatio");
            public readonly double critDamageRatio = GameBalance.Const("expCritDamageRatio");
            public readonly double advancedAttackRatio = GameBalance.Const("expAdvancedAttackRatio");
            public readonly int normalDamageDivisor = GameBalance.ConstInt("expNormalDamageDivisor");
            public readonly double attackMulPerStage = GameBalance.Const("expAttackMulPerStage");   // 무기 보유 효과 (스테이지 1000 ≈ ×13)
            public readonly double hpMulPerStage = GameBalance.Const("expHpMulPerStage");           // 방어구 보유 효과 (스테이지 1000 ≈ ×8)
            public readonly double enemyHpHits = GameBalance.Const("enemyHpHits");
            public readonly double enemyHpWaveGrowth = GameBalance.Const("enemyHpWaveGrowth");
            public readonly double bossKillSeconds = GameBalance.Const("bossKillSeconds");
            public readonly double enemyAtkHpRate = GameBalance.Const("enemyAtkHpRate");
            public readonly double enemyAtkRegenRate = GameBalance.Const("enemyAtkRegenRate");
            public readonly double bossAtkHpRate = GameBalance.Const("bossAtkHpRate");
            public readonly double goldBase = GameBalance.Const("goldBase");
            public readonly double goldStep = GameBalance.Const("goldStep");
            public readonly double goldExp = GameBalance.Const("goldExp");
            public readonly long bossGoldMul = (long)GameBalance.Const("bossGoldMul");
            public readonly long mycelBase = (long)GameBalance.Const("mycelBase");
            public readonly int mycelStageDiv = GameBalance.ConstInt("mycelStageDiv");
            // 표 끝을 넘은 스테이지: 마지막 일반 스테이지 값 × 성장^(s − 그 스테이지) (밸런스설계/이론.md 10.1 C9)
            public readonly double tailEnemyHpGrowth = GameBalance.Const("tailEnemyHpGrowth");
            public readonly double tailBossHpGrowth = GameBalance.Const("tailBossHpGrowth");
            public readonly double tailAttackGrowth = GameBalance.Const("tailAttackGrowth");
            public readonly double tailGoldGrowth = GameBalance.Const("tailGoldGrowth");
            public readonly double tailWallMul = GameBalance.Const("tailWallMul");
            public readonly int wallBossEvery = GameBalance.ConstInt("wallBossEvery");
            public readonly int wallBossFrom = GameBalance.ConstInt("wallBossFrom");
            public readonly int wallBossLateFrom = GameBalance.ConstInt("wallBossLateFrom");
            public readonly double wallBossHpMul = GameBalance.Const("wallBossHpMul");
            public readonly double wallBossHpMulLate = GameBalance.Const("wallBossHpMulLate");
        }

        static Curve curve;
        static Curve K => curve ?? (curve = new Curve());

        // 해금 조건(unlockStat)을 따라 재귀한다. 표가 순환하면 스택이 넘쳐 앱이 죽으므로 깊이를 막는다 (검증기도 순환을 막는다)
        const int MaxUnlockDepth = 8;
        static int expectedDepth;   // 메인 스레드에서만 부른다

        public static int ExpectedLevel(IdleStat stat, int stage)
        {
            if (expectedDepth >= MaxUnlockDepth) return 0;
            expectedDepth++;
            try { return ExpectedLevelCore(stat, stage); }
            finally { expectedDepth--; }
        }

        static int ExpectedLevelCore(IdleStat stat, int stage)
        {
            int s = Mathf.Max(0, stage - 1);
            var d = Get(stat);
            var k = K;
            switch (stat)
            {
                case IdleStat.Speed: return Mathf.Min(d.maxLv, (int)(k.speedPerStage * s));
                case IdleStat.HpRegen: return Mathf.Min(d.maxLv, (int)(k.levelsPerStage * s * k.regenRatio));
                // 단계 제한이 있는 강화: 기대 기준 레벨에서 열린 한도의 일부만 찍었다고 본다
                case IdleStat.CritChance: return ExpectedCapped(stat, stage, k.critChanceRatio);
                case IdleStat.CritDamage: return ExpectedCapped(stat, stage, k.critDamageRatio);
                case IdleStat.AdvancedAttack: return ExpectedCapped(stat, stage, k.advancedAttackRatio);
                case IdleStat.NormalEnemyDamage: return Mathf.Clamp((ExpectedLevel(d.unlockStat, stage) - d.unlockLv) / Mathf.Max(1, k.normalDamageDivisor), 0, d.maxLv);
            }
            return Mathf.Min(d.maxLv, k.levelsPerStage * s);
        }

        // 단계 제한 한도 × ratio. 한도는 계단(25레벨씩)이 아니라 이어진 값으로 본다 — 계단이면 그 스테이지만 골드가 튄다
        static int ExpectedCapped(IdleStat stat, int stage, double ratio)
        {
            var d = Get(stat);
            int gate = ExpectedLevel(d.unlockStat, stage);
            if (gate < d.unlockLv) return 0;
            if (d.capEvery <= 0) return ratio > 0 ? d.maxLv : 0;    // 단계 제한 없음 = 한도 무한 (나누기 0으로 NaN이 되지 않게)
            double cap = ((double)(gate - d.unlockLv) / d.capEvery + 1) * d.capStep;
            return (int)System.Math.Min(d.maxLv, cap * ratio);
        }

        static double ExpectedAttackMultiplier(int s) => 1 + K.attackMulPerStage * s;
        static double ExpectedHpMultiplier(int s) => 1 + K.hpMulPerStage * s;

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
        // 웨이브당 적 수: 시작값에서 웨이브·스테이지가 오를수록 늘고 상한에서 멈춘다 (스테이지 시트의 enemyCount* 열, 표 끝을 넘으면 마지막 행 설정)
        public static int EnemyCount(int stage, int wave)
        {
            var r = GameBalance.Stage(stage);
            return Mathf.Clamp(r.enemyCountBase + r.enemyCountPerWave * (wave - 1) + r.enemyCountPerStage * (stage - 1), r.enemyCountBase, r.enemyCountMax);
        }

        // 기대 성장이면 일반 적은 투척 2번, 보스는 18초(제한 30초)에 잡힌다. 일반 적은 일반 적 피해량까지 반영한다.
        // 스테이지 시트에 체력(enemyHp·bossHp, 0보다 큰 값)이 있으면 그 값 — 초반을 곡선에서 떼거나 챕터 보스 벽을 넣을 때
        // 표 끝을 넘으면 마지막 일반 스테이지(표 끝이 챕터 보스면 그 앞) 값에서 일정한 비율로 늘린다 — 기대 곡선과 이어지지 않아도 체감이 끊기지 않게
        public static double EnemyHp(int stage, int wave)
        {
            double first;
            if (GameBalance.InStageTable(stage) && GameBalance.Stage(stage).enemyHp > 0) first = GameBalance.Stage(stage).enemyHp;
            else if (!GameBalance.InStageTable(stage) && TailRow.enemyHp > 0) first = TailRow.enemyHp * Tail(stage, K.tailEnemyHpGrowth);
            else first = ExpectedHit(stage) * Value(IdleStat.NormalEnemyDamage, ExpectedLevel(IdleStat.NormalEnemyDamage, stage)) * K.enemyHpHits;
            return first * (1 + K.enemyHpWaveGrowth * (wave - 1));
        }

        // 벽 보스(상수 wallBoss*): wallBossFrom부터 wallBossEvery 배수 스테이지 보스는 체력 × 벽 배율 — 방치·앱 밖·광고·던전으로 넘는 시간 장벽
        public static double BossHp(int stage) => BossHpNoWall(stage) * WallBossMul(stage);

        public static double WallBossMul(int stage)
        {
            var k = K;
            if (k.wallBossEvery <= 0 || stage < k.wallBossFrom || stage % k.wallBossEvery != 0) return 1.0;
            return stage >= k.wallBossLateFrom ? k.wallBossHpMulLate : k.wallBossHpMul;
        }

        // 벽 배율을 뺀 보스 체력: 던전 보스(보스 러시·토벌)는 이 값을 쓴다 — 벽에 막혔을 때 던전까지 막히지 않게
        public static double BossHpNoWall(int stage)
        {
            if (GameBalance.InStageTable(stage) && GameBalance.Stage(stage).bossHp > 0) return GameBalance.Stage(stage).bossHp;
            if (!GameBalance.InStageTable(stage) && TailRow.bossHp > 0)
                return TailRow.bossHp * Tail(stage, K.tailBossHpGrowth) * (stage % ChapterLength == 0 ? K.tailWallMul : 1);
            return ExpectedDps(stage) * K.bossKillSeconds;
        }

        const int ChapterLength = 10;   // 10의 배수 스테이지 = 챕터 보스 (표에서는 체력 ×1.4)
        static int TailStage => GameBalance.StageCount > 1 && GameBalance.StageCount % ChapterLength == 0 ? GameBalance.StageCount - 1 : GameBalance.StageCount;
        static StageRow TailRow => GameBalance.Stage(TailStage);
        static double Tail(int stage, double growth) => System.Math.Pow(growth, stage - TailStage);

        // 적 공격(초당 피해): 일반 적은 1마리씩, 보스는 혼자. 기대 성장이면 웨이브마다 체력이 조금씩 깎이고 보스에서 바닥 근처까지 간다.
        // 스테이지 시트에 있는 스테이지는 시트 값, 표 끝을 넘으면 기대 성장 곡선으로 계산한다 (골드·균사석도 같다)
        public static double EnemyAttack(int stage)
        {
            if (GameBalance.InStageTable(stage)) return GameBalance.Stage(stage).enemyAttack;
            if (TailRow.enemyAttack > 0) return TailRow.enemyAttack * Tail(stage, K.tailAttackGrowth);
            return K.enemyAtkHpRate * ExpectedHp(stage) + K.enemyAtkRegenRate * ExpectedRegen(stage);
        }

        public static double BossAttack(int stage)
        {
            if (GameBalance.InStageTable(stage)) return GameBalance.Stage(stage).bossAttack;
            if (TailRow.bossAttack > 0) return TailRow.bossAttack * Tail(stage, K.tailAttackGrowth);
            return ExpectedRegen(stage) + K.bossAtkHpRate * ExpectedHp(stage);
        }

        // 골드(생산량)는 스테이지의 거듭제곱으로 계속 오르고, 강화 비용(소모량)은 거듭제곱 × 지수(priceScale)라 더 빨리 오른다.
        // 그 차이 ≈ e^((s-1)/FarmScale)가 스테이지당 반복 판 수 → 누적 플레이 시간 대비 스테이지가 로그 곡선
        //   (밸런스설계/이론.md: 100스테이지 1.5시간 · 300 11시간 · 500 2.4일 · 1000 70일, 한 판 40초 가정). 전체 속도는 goldBase로 조절한다
        public const double FarmScale = 150;

        public static long EnemyGold(int stage)
        {
            if (GameBalance.InStageTable(stage)) return GameBalance.Stage(stage).enemyGold;
            var k = K;
            if (TailRow.enemyGold > 0) return ClampToLong(TailRow.enemyGold * Tail(stage, k.tailGoldGrowth), 1);
            return ClampToLong(System.Math.Round(k.goldBase * System.Math.Pow(1 + (stage - 1) / k.goldStep, k.goldExp)), 1);
        }

        public static long BossGold(int stage)
        {
            if (GameBalance.InStageTable(stage)) return GameBalance.Stage(stage).bossGold;
            long gold = EnemyGold(stage), mul = K.bossGoldMul;
            if (mul <= 0) return 0;
            return gold > (long)MaxAmount / mul ? (long)MaxAmount : gold * mul;     // 넘치면 9e18에서 멈춘다
        }

        // 보스 처치 균사석 (뽑기 재화). 보스를 잡아야만 다음 스테이지로 가므로 스테이지마다 한 번
        public static long BossMycelite(int stage)
        {
            if (GameBalance.InStageTable(stage)) return GameBalance.Stage(stage).bossMycelite;
            if (TailRow.bossMycelite > 0) return TailRow.bossMycelite;     // 표 끝 뒤에도 표의 마지막 값 그대로 (v3 = 2)
            return K.mycelBase + stage / System.Math.Max(1, K.mycelStageDiv);
        }

        // 아주 높은 스테이지에서 long을 넘으면 (long) 변환이 기기마다 음수·최솟값이 된다 — 9e18에서 멈춘다. NaN은 min으로
        public const double MaxAmount = 9e18;
        public static long ClampToLong(double v, long min = 0)
        {
            if (double.IsNaN(v) || v <= min) return min;
            return v >= MaxAmount ? (long)MaxAmount : (long)v;
        }

        // 넘치지 않는 덧셈 (음수는 더하지 않는다)
        public static long SaturatingAdd(long a, long b) => b <= 0 ? a : a > long.MaxValue - b ? long.MaxValue : a + b;

        // 피해·체력 표시: Wallet과 같은 단위(만/억/조/경)
        public static string FormatNumber(double v)
        {
            if (v < 1) return v.ToString("0.#");
            if (v < 100) return v.ToString("0.#");
            return Wallet.Format(v);
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
