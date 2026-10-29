using UnityEngine;

namespace MoreMush.Mobile
{
    // 방치형 강화 종류. 순서 = 강화 목록에 보이는 순서
    public enum IdleStat { Attack, Speed, CritChance, CritDamage, GoldBonus }

    // 방치형 진행: 스테이지와 강화 레벨. PlayerPrefs("Idle.Stage", "Idle.Lv.<종류>")에 저장한다 (세이브 구조가 생기면 옮긴다).
    // 수치는 전부 임시값이다 (밸런스 확정 전) — 공식은 이 파일 한 곳에만 둔다
    public static class IdleProgress
    {
        public const int WavesPerStage = 5;     // 이 수만큼 웨이브를 넘기면 보스
        public const float BossTime = 30f;      // 보스 제한 시간(초). 넘기면 같은 스테이지 1웨이브부터 다시

        public class Def
        {
            public IdleStat stat;
            public string name;
            public int maxLv;
            public double baseCost, costGrowth;
            public IdleStat unlockStat;         // 이 강화가 unlockLv 이상이어야 열린다 (unlockLv 0 = 처음부터)
            public int unlockLv;
        }

        public static readonly Def[] Defs =
        {
            new Def { stat = IdleStat.Attack,     name = "공격력",     maxLv = 9999, baseCost = 5,   costGrowth = 1.07 },
            new Def { stat = IdleStat.Speed,      name = "공격 속도",   maxLv = 100,  baseCost = 50,  costGrowth = 1.12, unlockStat = IdleStat.Attack, unlockLv = 20 },
            new Def { stat = IdleStat.CritChance, name = "치명타 확률", maxLv = 50,   baseCost = 100, costGrowth = 1.15, unlockStat = IdleStat.Speed, unlockLv = 10 },
            new Def { stat = IdleStat.CritDamage, name = "치명타 피해", maxLv = 200,  baseCost = 200, costGrowth = 1.10, unlockStat = IdleStat.CritChance, unlockLv = 5 },
            new Def { stat = IdleStat.GoldBonus,  name = "골드 획득량", maxLv = 200,  baseCost = 80,  costGrowth = 1.10, unlockStat = IdleStat.Attack, unlockLv = 40 },
        };

        public static event System.Action Changed;

        public static Def Get(IdleStat stat) => Defs[(int)stat];

        public static int Stage
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt("Idle.Stage", 1));
            set { PlayerPrefs.SetInt("Idle.Stage", Mathf.Max(1, value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static int Level(IdleStat stat) => PlayerPrefs.GetInt("Idle.Lv." + stat, 0);

        public static bool Unlocked(IdleStat stat)
        {
            var d = Get(stat);
            return d.unlockLv <= 0 || Level(d.unlockStat) >= d.unlockLv;
        }

        public static bool IsMax(IdleStat stat) => Level(stat) >= Get(stat).maxLv;

        // 다음 레벨로 올리는 값
        public static long Cost(IdleStat stat)
        {
            var d = Get(stat);
            return (long)System.Math.Min(9e18, System.Math.Ceiling(d.baseCost * System.Math.Pow(d.costGrowth, Level(stat))));
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
            switch (stat)
            {
                case IdleStat.Attack: return 10.0 * (1 + 0.25 * lv) * System.Math.Pow(1.01, lv);   // 투척 1회 피해
                case IdleStat.Speed: return 0.8 * (1 + 0.03 * lv);                                   // 꼬마 1명의 초당 투척 수
                case IdleStat.CritChance: return 0.01 * lv;                                          // 0~1
                case IdleStat.CritDamage: return 1.5 + 0.1 * lv;                                     // 치명타 배수
                case IdleStat.GoldBonus: return 1.0 + 0.05 * lv;                                     // 골드 배수
            }
            return 0;
        }

        public static string FormatValue(IdleStat stat, double v)
        {
            switch (stat)
            {
                case IdleStat.Speed: return v.ToString("0.00") + "/초";
                case IdleStat.CritChance: return (v * 100).ToString("0") + "%";
                case IdleStat.CritDamage:
                case IdleStat.GoldBonus: return (v * 100).ToString("0") + "%";
            }
            return FormatNumber(v);
        }

        // ── 적 (스테이지·웨이브 → 값) ──
        // 웨이브당 적 수: 5에서 시작해 웨이브·스테이지가 오를수록 늘고 15에서 멈춘다
        public static int EnemyCount(int stage, int wave) => Mathf.Clamp(5 + 2 * (wave - 1) + (stage - 1), 5, 15);

        public static double EnemyHp(int stage, int wave) => 30.0 * System.Math.Pow(1.18, stage - 1) * (1 + 0.08 * (wave - 1));

        public static double BossHp(int stage) => EnemyHp(stage, WavesPerStage) * 12;

        public static long EnemyGold(int stage) => (long)System.Math.Max(1, System.Math.Round(2 * System.Math.Pow(1.16, stage - 1) * Value(IdleStat.GoldBonus)));

        public static long BossGold(int stage) => EnemyGold(stage) * 15;

        // 피해·체력 표시: Wallet과 같은 단위(만/억/조/경)
        public static string FormatNumber(double v)
        {
            if (v < 1) return v.ToString("0.#");
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
