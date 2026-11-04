using System;
using System.Collections.Generic;
using System.Linq;
using MoreMush.Mobile;

// 플레이어 상태(PlayerPrefs를 쓰는 것들을 시뮬 안에서 들고 있는다). 공식은 실제 코드의 순수 함수를 부른다:
//   IdleProgress.Value/Cost/CapLevel/ExpectedLevel, Formation.AttackMultiplierAt/IdsOfGrade/CopiesToNext,
//   Equipment(인스턴스).OwnPercentAt/EquipPercentAt/Grade, Equipment.CopiesToNext, GachaProgress.Odds/ExpToNext
// 장비 배치(종류별 장비 수·등급)는 장비 시트에서 읽는다 (2026-11-04: 무기 10종 4/3/2/1, 방어구·펫 12종, 4등급)
// 상태 옮긴 곳: 레벨 = IdleProgress.cs:81-108(levels), 지갑 = Wallet.cs, 조각·레벨·장착 = Equipment.cs:111-150, Formation.cs:70-100,
//   뽑기 레벨·경험치 = GachaProgress.cs:Apply(155-170)
sealed class Player
{
    public const int Stats = 8;
    public int stage = 1;
    public double gold;
    public long myc;
    public int[] lv = new int[Stats];

    // 장비 3종(0 무기, 1 방어구=Accessory, 2 펫 = Equipment.All 순서) × 종류별 장비 수
    static Equipment E(int k) => Equipment.All[k];
    public int[][] eqLv = Equipment.All.Select(e => new int[e.Count]).ToArray();
    public int[][] eqCp = Equipment.All.Select(e => new int[e.Count]).ToArray();
    public int[] eqOn = { -1, -1, -1 };
    public double[] eqPct = new double[3];

    // 꼬마 9종
    public string[] ids = Formation.Ids;
    public int[] mLv, mCp;
    public double[] slotMul = new double[5];

    // 뽑기 4종(무기·방어구·펫·꼬마) 레벨·경험치
    public int[] gLv = { 1, 1, 1, 1 }, gExp = new int[4];
    public int nextKind;

    // 파생 값
    public double finalAtk, finalHp, finalRegen, speed, cc, cd, adv, norm, mulSum;
    public bool dirty = true;

    public Random rng;
    public long pulls;

    // 이벤트 (퀘스트 집계)
    public Action<int> onUpgradeBought, onPulled;

    public Player(int seed)
    {
        rng = new Random(seed);
        var rows = GameBalance.Mascots;
        mLv = new int[ids.Length];
        mCp = new int[ids.Length];
        for (int i = 0; i < ids.Length; i++) mLv[i] = rows[i].initialOwned;
        Refresh();
    }

    public static IdleProgress.Def D(int s) => IdleProgress.Defs[s];

    public bool Unlocked(int s)
    {
        var d = D(s);
        return d.unlockLv <= 0 || lv[(int)d.unlockStat] >= d.unlockLv;
    }

    public int Cap(int s) => IdleProgress.CapLevel((IdleStat)s, lv[(int)D(s).unlockStat]);

    public bool CanBuy(int s) => Unlocked(s) && lv[s] < D(s).maxLv && lv[s] < Cap(s);

    public void Refresh()
    {
        if (!dirty) return;
        dirty = false;
        for (int k = 0; k < 3; k++)
        {
            double sum = 0;
            for (int i = 0; i < eqLv[k].Length; i++) sum += E(k).OwnPercentAt(i, eqLv[k][i]);
            if (eqOn[k] >= 0) sum += E(k).EquipPercentAt(eqOn[k], Math.Max(1, eqLv[k][eqOn[k]]));
            eqPct[k] = sum;
        }
        double atkMul = 1 + (eqPct[0] + eqPct[2]) / 100.0, hpMul = 1 + eqPct[1] / 100.0;
        adv = IdleProgress.Value(IdleStat.AdvancedAttack, lv[(int)IdleStat.AdvancedAttack]);
        norm = IdleProgress.Value(IdleStat.NormalEnemyDamage, lv[(int)IdleStat.NormalEnemyDamage]);
        finalAtk = IdleProgress.Value(IdleStat.Attack, lv[(int)IdleStat.Attack]) * atkMul * adv;
        finalHp = IdleProgress.Value(IdleStat.Hp, lv[(int)IdleStat.Hp]) * hpMul;
        finalRegen = IdleProgress.Value(IdleStat.HpRegen, lv[(int)IdleStat.HpRegen]) * hpMul;
        speed = IdleProgress.Value(IdleStat.Speed, lv[(int)IdleStat.Speed]);
        cc = IdleProgress.Value(IdleStat.CritChance, lv[(int)IdleStat.CritChance]);
        cd = IdleProgress.Value(IdleStat.CritDamage, lv[(int)IdleStat.CritDamage]);
        // 자동 편성: 가진 꼬마 중 공격 배율 높은 순 5명 (Formation.AutoArrange)
        var muls = new List<double>();
        for (int i = 0; i < ids.Length; i++) if (mLv[i] > 0) muls.Add(Formation.AttackMultiplierAt(ids[i], mLv[i]));
        muls.Sort((a, b) => b.CompareTo(a));
        mulSum = 0;
        for (int i = 0; i < 5; i++) { slotMul[i] = i < muls.Count ? muls[i] : 0; mulSum += slotMul[i]; }
    }

    // 균형 정책이 보는 기대 레벨 (스테이지마다 한 번 계산)
    int expStage = -1;
    int[] expLv = new int[Stats];
    void ExpFor(int st)
    {
        if (expStage == st) return;
        expStage = st;
        for (int s = 0; s < Stats; s++) expLv[s] = IdleProgress.ExpectedLevel((IdleStat)s, st);
    }

    // 균형 정책: 레벨/기대 레벨(현재 스테이지)이 가장 낮은 강화를 산다. 못 사면 기다린다 (아래 Buy가 처리)
    public static string Policy = "bal";   // bal = 기대 레벨 대비 균형 · cost = 가장 싼 것부터 · eff = (공격×생존) 로그 이득 ÷ 비용 최대
    public static int Fps = 60;

    double CritF(int ccLv, int cdLv) => 1 + Math.Min(1, IdleProgress.Value(IdleStat.CritChance, ccLv)) * (IdleProgress.Value(IdleStat.CritDamage, cdLv) - 1);

    // 강화 s를 한 레벨 올리면 로그 이득 (공격 = 공격력×고급×속도×치명타×일반피해^0.5, 생존 = 체력 + 재생×30 — CombatPower 정의)
    double Gain(int s)
    {
        int l = lv[s], n = l + 1;
        var st = (IdleStat)s;
        double V(IdleStat x, int k) => IdleProgress.Value(x, k);
        double surv(int hpLv, int rgLv) => V(IdleStat.Hp, hpLv) + 30 * V(IdleStat.HpRegen, rgLv);
        int hl = lv[(int)IdleStat.Hp], rl = lv[(int)IdleStat.HpRegen], cl = lv[(int)IdleStat.CritChance], dl = lv[(int)IdleStat.CritDamage];
        switch (st)
        {
            case IdleStat.Attack: case IdleStat.AdvancedAttack: return Math.Log(V(st, n) / V(st, l));
            case IdleStat.NormalEnemyDamage: return 0.5 * Math.Log(V(st, n) / V(st, l));
            case IdleStat.Speed: return Math.Log(Math.Min(V(st, n), Fps) / Math.Min(V(st, l), Fps));
            case IdleStat.CritChance: return Math.Log(CritF(n, dl) / CritF(cl, dl));
            case IdleStat.CritDamage: return Math.Log(CritF(cl, n) / CritF(cl, dl));
            case IdleStat.Hp: return Math.Log(surv(n, rl) / surv(hl, rl));
            default: return Math.Log(surv(hl, n) / surv(hl, rl));
        }
    }

    public int PickStat()
    {
        if (Policy != "bal")
        {
            int bi = -1; double bv = double.MinValue;
            for (int s = 0; s < Stats; s++)
            {
                if (!CanBuy(s)) continue;
                double c = IdleProgress.Cost((IdleStat)s, lv[s]);
                double v = Policy == "cost" ? -c : Gain(s) / c;
                if (v > bv) { bv = v; bi = s; }
            }
            return bi;
        }
        ExpFor(stage);
        int best = -1;
        double bestR = double.MaxValue;
        for (int s = 0; s < Stats; s++)
        {
            if (!CanBuy(s)) continue;
            double r = expLv[s] >= D(s).maxLv ? -1.0 : lv[s] / Math.Max(1.0, expLv[s]);   // 기대 레벨이 최대 레벨에 닿은 강화(공속 봉인 등)는 최대까지 먼저 산다
            if (r < bestR - 1e-12) { bestR = r; best = s; }
        }
        return best;
    }

    // 정책에 따라 살 수 있는 만큼 강화 구매. 산 횟수
    public int BuyUpgrades()
    {
        int n = 0;
        while (true)
        {
            int s = PickStat();
            if (s < 0) break;
            long c = IdleProgress.Cost((IdleStat)s, lv[s]);
            if (gold < c) break;
            gold -= c;
            lv[s]++;
            dirty = true;
            n++;
            onUpgradeBought?.Invoke(1);
        }
        return n;
    }

    // 구매 목표 비용 (기다리는 강화의 가격). 없으면 큰 수
    public double NextCost()
    {
        int s = PickStat();
        return s < 0 ? double.MaxValue : IdleProgress.Cost((IdleStat)s, lv[s]);
    }

    // ── 뽑기 ──
    // 등급별 장비 번호 (Equipment.PickItems: 뽑힌 등급 안에서 같은 확률)
    static List<int> Pool(int kind, int g) => Enumerable.Range(0, E(kind).Count).Where(i => E(kind).Grade(i) == g).ToList();

    // 11회(비용 100)·35회(비용 300). kind 0 무기, 1 방어구, 2 펫, 3 꼬마. 결과를 반영한다 (GachaPanel.DoRoll 순서)
    public void Pull(int kind, int count)
    {
        var odds = GachaProgress.Odds(gLv[kind]);
        int grades = GachaProgress.GradeCount;
        var res = new int[grades];
        for (int i = 0; i < count; i++)
        {
            float r = (float)rng.NextDouble() * 100f, acc = 0f;
            int g = grades - 1;
            for (int k = 0; k < grades; k++) { acc += odds[k]; if (r < acc) { g = k; break; } }
            res[g]++;
        }
        // GachaProgress.Apply: 경험치
        int level = gLv[kind], exp = gExp[kind] + count;
        while (level < GachaProgress.MaxLevel && exp >= GachaProgress.ExpToNext(level)) { exp -= GachaProgress.ExpToNext(level); level++; }
        if (level >= GachaProgress.MaxLevel) exp = 0;
        gLv[kind] = level; gExp[kind] = exp;
        pulls += count;

        if (kind == 3)
        {
            for (int g = 0; g < grades; g++)
            {
                var pool = Formation.IdsOfGrade(g);
                if (pool.Count == 0) continue;
                for (int n = 0; n < res[g]; n++)
                {
                    int idx = Array.IndexOf(ids, pool[rng.Next(pool.Count)]);
                    if (mLv[idx] == 0) mLv[idx] = 1; else mCp[idx]++;
                }
            }
            // 합성(Formation.MergeAll)
            for (int i = 0; i < ids.Length; i++)
                while (mLv[i] > 0 && mLv[i] < Formation.MaxLevel && mCp[i] >= Formation.CopiesToNext(mLv[i])) { mCp[i] -= Formation.CopiesToNext(mLv[i]); mLv[i]++; }
        }
        else
        {
            int e = kind, batchBest = -1;
            for (int g = 0; g < grades; g++)
            {
                var pool = res[g] > 0 ? Pool(e, g) : null;
                if (pool == null || pool.Count == 0) continue;
                for (int n = 0; n < res[g]; n++)
                {
                    int it = pool[rng.Next(pool.Count)];
                    batchBest = Math.Max(batchBest, it);
                    if (eqLv[e][it] == 0) eqLv[e][it] = 1; else eqCp[e][it]++;
                }
            }
            // 합성(Equipment.MergeAll)
            for (int i = 0; i < eqLv[e].Length; i++)
                while (eqLv[e][i] > 0 && eqLv[e][i] < Equipment.MaxLevel && eqCp[e][i] >= Equipment.CopiesToNext(eqLv[e][i])) { eqCp[e][i] -= Equipment.CopiesToNext(eqLv[e][i]); eqLv[e][i]++; }
            if (EquipGame)
            {
                // 게임(Equipment.cs:AddItems 284): 아직 장착이 없을 때만 이번 묶음에서 가장 높은 번호를 자동 장착, 이후 바꾸지 않는다
                if (eqOn[e] < 0 && batchBest >= 0) eqOn[e] = batchBest;
            }
            else
            {
                // 장착: 장착 효과가 가장 큰 보유 장비 (플레이어가 최선으로 바꿔 낀다고 가정)
                int bestI = -1; double bestV = -1;
                for (int i = 0; i < eqLv[e].Length; i++)
                    if (eqLv[e][i] > 0) { double v = E(e).EquipPercentAt(i, eqLv[e][i]); if (v > bestV) { bestV = v; bestI = i; } }
                eqOn[e] = bestI;
            }
        }
        dirty = true;
        onPulled?.Invoke(count);
    }

    // 균사석 쓰기: 300 이상이면 35회, 100 이상이면 11회, 4종 돌려가며
    public static bool NoGacha;
    public static bool EquipGame;     // true = 게임처럼 처음 얻은 묶음에서만 자동 장착 (민감도), false = 늘 최선으로 바꿔 낌 (기본)
    public void SpendMycelite()
    {
        if (NoGacha) return;
        while (myc >= 100)
        {
            int count = myc >= 300 ? 35 : 11;
            myc -= count == 35 ? 300 : 100;
            Pull(nextKind, count);
            nextKind = (nextKind + 1) % 4;
        }
    }
}
