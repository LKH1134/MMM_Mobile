using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 방치형 강화 종류. 순서 = 강화 목록에 보이는 순서 (저장 키는 이름이라 순서를 바꿔도 된다)
    public enum IdleStat { Attack, Hp, HpRegen, Speed, CritChance, CritDamage, GoldBonus }

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
            public double costBase, costExp;    // 비용 = costBase × (1 + Lv/10)^costExp
            public IdleStat unlockStat;         // 이 강화가 unlockLv 이상이어야 열린다 (unlockLv 0 = 처음부터)
            public int unlockLv;
        }

        public static readonly Def[] Defs =
        {
            new Def { stat = IdleStat.Attack,     name = "공격력",      icon = "Art/Dream/Icons/hammer",   baseValue = 10,  perLevel = 2,      maxLv = 100000, costBase = 10,  costExp = 2.5 },
            new Def { stat = IdleStat.Hp,         name = "체력",        icon = "Art/Dream/Icons/mushtree", baseValue = 100, perLevel = 20,     maxLv = 100000, costBase = 10,  costExp = 2.5 },
            new Def { stat = IdleStat.HpRegen,    name = "체력 재생",    icon = "Art/Dream/Icons/t_forest", baseValue = 2,   perLevel = 0.4,    maxLv = 100000, costBase = 15,  costExp = 2.5, unlockStat = IdleStat.Hp, unlockLv = 20 },
            new Def { stat = IdleStat.Speed,      name = "공격 속도",    icon = "Art/Dream/Icons/clock",    baseValue = 0.8, perLevel = 0.01,   maxLv = 150,    costBase = 300, costExp = 3.5, unlockStat = IdleStat.Attack, unlockLv = 30 },
            new Def { stat = IdleStat.CritChance, name = "치명타 확률",  icon = "Art/Dream/Icons/gem",      baseValue = 0,   perLevel = 0.0001, maxLv = 10000,  costBase = 30,  costExp = 2.5, unlockStat = IdleStat.Speed, unlockLv = 10 },
            new Def { stat = IdleStat.CritDamage, name = "치명타 피해",  icon = "Art/Dream/Icons/f_beauty", baseValue = 1.5, perLevel = 0.001,  maxLv = 10000,  costBase = 30,  costExp = 2.5, unlockStat = IdleStat.CritChance, unlockLv = 100 },
            new Def { stat = IdleStat.GoldBonus,  name = "골드 획득량",  icon = "Art/Dream/Icons/gold",     baseValue = 1,   perLevel = 0.01,   maxLv = 2000,   costBase = 50,  costExp = 2.7, unlockStat = IdleStat.Attack, unlockLv = 50 },
        };

        public static event System.Action Changed;

        public static Def Get(IdleStat stat)
        {
            foreach (var d in Defs) if (d.stat == stat) return d;
            return Defs[0];
        }

        public static int Stage
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt("Idle.Stage", 1));
            set { PlayerPrefs.SetInt("Idle.Stage", Mathf.Max(1, value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static int Level(IdleStat stat) => Mathf.Clamp(PlayerPrefs.GetInt("Idle.Lv." + stat, 0), 0, Get(stat).maxLv);

        public static bool Unlocked(IdleStat stat)
        {
            var d = Get(stat);
            return d.unlockLv <= 0 || Level(d.unlockStat) >= d.unlockLv;
        }

        public static bool IsMax(IdleStat stat) => Level(stat) >= Get(stat).maxLv;

        // 다음 레벨로 올리는 값
        public static long Cost(IdleStat stat) => Cost(stat, Level(stat));

        public static long Cost(IdleStat stat, int lv)
        {
            var d = Get(stat);
            return (long)System.Math.Min(9e18, System.Math.Ceiling(d.costBase * System.Math.Pow(1 + lv / 10.0, d.costExp)));
        }

        public static bool TryUpgrade(IdleStat stat)
        {
            if (!Unlocked(stat) || IsMax(stat)) return false;
            if (!Wallet.TrySpend(Currency.Gold, Cost(stat))) return false;
            PlayerPrefs.SetInt("Idle.Lv." + stat, Level(stat) + 1);
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
        public static double FinalAttack => Value(IdleStat.Attack) * AttackMultiplier;
        public static double FinalHp => Value(IdleStat.Hp) * HpMultiplier;
        public static double FinalHpRegen => Value(IdleStat.HpRegen) * HpMultiplier;

        public static string FormatValue(IdleStat stat, double v)
        {
            switch (stat)
            {
                case IdleStat.Speed: return v.ToString("0.00") + "/초";
                case IdleStat.HpRegen: return FormatNumber(v) + "/초";
                case IdleStat.CritChance: return (v * 100).ToString("0.00") + "%";
                case IdleStat.CritDamage:
                case IdleStat.GoldBonus: return (v * 100).ToString("0.#") + "%";
            }
            return FormatNumber(v);
        }

        // ── 기대 성장 곡선: 스테이지 s에 도착한 플레이어가 가졌을 것으로 보는 강화 레벨·보유 효과 ──
        public const int LevelsPerStage = 10;   // 공격력·체력 기대 레벨 = 10 × (스테이지 - 1) (스테이지 1 = Lv 0, 스테이지 1000 ≈ Lv 10000)

        public static int ExpectedLevel(IdleStat stat, int stage)
        {
            int s = Mathf.Max(0, stage - 1);
            double ratio;
            switch (stat)
            {
                case IdleStat.Speed: return Mathf.Min(Get(stat).maxLv, (int)(1.5 * s));
                case IdleStat.HpRegen: ratio = 0.6; break;
                case IdleStat.CritChance: ratio = 0.6; break;
                case IdleStat.CritDamage: ratio = 0.5; break;
                case IdleStat.GoldBonus: ratio = 0.2; break;
                default: ratio = 1.0; break;
            }
            return Mathf.Min(Get(stat).maxLv, (int)(LevelsPerStage * s * ratio));
        }

        static double ExpectedAttackMultiplier(int s) => 1 + 0.012 * s;   // 무기+펫 보유 효과 (스테이지 1000 ≈ ×13)
        static double ExpectedHpMultiplier(int s) => 1 + 0.007 * s;       // 장신구 보유 효과 (스테이지 1000 ≈ ×8)

        static double ExpectedHit(int s)
        {
            double cc = System.Math.Min(1, Value(IdleStat.CritChance, ExpectedLevel(IdleStat.CritChance, s)));
            double cd = Value(IdleStat.CritDamage, ExpectedLevel(IdleStat.CritDamage, s));
            return Value(IdleStat.Attack, ExpectedLevel(IdleStat.Attack, s)) * ExpectedAttackMultiplier(s) * (1 + cc * (cd - 1));
        }

        static double ExpectedDps(int s) => ExpectedHit(s) * Formation.SlotCount * Value(IdleStat.Speed, ExpectedLevel(IdleStat.Speed, s));
        static double ExpectedHp(int s) => Value(IdleStat.Hp, ExpectedLevel(IdleStat.Hp, s)) * ExpectedHpMultiplier(s);
        static double ExpectedRegen(int s) => Value(IdleStat.HpRegen, ExpectedLevel(IdleStat.HpRegen, s)) * ExpectedHpMultiplier(s);

        // ── 적 (스테이지·웨이브 → 값) ──
        // 웨이브당 적 수: 5에서 시작해 웨이브·스테이지가 오를수록 늘고 15에서 멈춘다
        public static int EnemyCount(int stage, int wave) => Mathf.Clamp(5 + 2 * (wave - 1) + (stage - 1), 5, 15);

        // 기대 성장이면 일반 적은 투척 2번, 보스는 18초(제한 30초)에 잡힌다
        public static double EnemyHp(int stage, int wave) => ExpectedHit(stage) * 2.0 * (1 + 0.08 * (wave - 1));
        public static double BossHp(int stage) => ExpectedDps(stage) * 18.0;

        // 적 공격(초당 피해): 일반 적은 1마리씩, 보스는 혼자. 기대 성장이면 웨이브마다 체력이 조금씩 깎이고 보스에서 바닥 근처까지 간다
        public static double EnemyAttack(int stage) => 0.007 * ExpectedHp(stage) + 0.15 * ExpectedRegen(stage);
        public static double BossAttack(int stage) => ExpectedRegen(stage) + 0.025 * ExpectedHp(stage);

        // 골드: 다음 스테이지까지의 기대 성장분(스테이지 s → s+1 레벨)을 사는 데 드는 골드를, 처치 수 × 반복 계수로 나눈다.
        // 반복 계수 = 다음 성장분을 모으려면 그 스테이지를 평균 몇 번 돌아야 하는지 (뒤로 갈수록 파밍이 길어진다)
        public static double FarmFactor(int stage) => 1 + System.Math.Pow(stage / 25.0, 1.5);

        static readonly Dictionary<int, long> baseGoldCache = new Dictionary<int, long>();

        static long BaseEnemyGold(int stage)
        {
            if (baseGoldCache.TryGetValue(stage, out var g)) return g;
            double need = 0;
            foreach (var d in Defs)
                for (int lv = ExpectedLevel(d.stat, stage); lv < ExpectedLevel(d.stat, stage + 1); lv++) need += Cost(d.stat, lv);
            int units = 20;   // 보스 = 일반 20마리분
            for (int w = 1; w <= WavesPerStage; w++) units += EnemyCount(stage, w);
            g = (long)System.Math.Max(1, System.Math.Round(need / (units * FarmFactor(stage))));
            baseGoldCache[stage] = g;
            return g;
        }

        public static long EnemyGold(int stage) => (long)System.Math.Max(1, System.Math.Round(BaseEnemyGold(stage) * Value(IdleStat.GoldBonus)));

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
            foreach (var d in Defs) PlayerPrefs.DeleteKey("Idle.Lv." + d.stat);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
#endif
    }
}
