using System;
using System.Collections.Generic;
using MoreMush.Mobile;

// 전투 규칙(Idle/IdleBattle.cs)을 틱(dt)으로 옮긴 것. 옮긴 곳(IdleBattle.cs 줄):
//   StartWave 330-345 · UpdateFlow 347-376 · OnEnemyKilled 378-398 · UpdateParty 400-428 · Defeat 433-445
//   UpdateGgomas 478-499(던지는 수 = 프레임당 1회 → 초당 min(공격속도, fps)) · PickTarget 502-509(아직 안 죽을 적 우선)
//   Throw 511-520(공격력×꼬마배율×치명타, 일반 적에만 일반 적 피해량) · Hit 568-581 · 투척 비행 flightTime 0.45
// 근사: dt 틱(기본 0.05s). 한 틱 안의 순서는 던짐→착탄→나이→파티→흐름(게임 Tick 순서 그대로).
//   보스는 던진 N발을 한 덩어리로 합산(치명타 = 이항분포, 32발 이상은 정규 근사). 프레임 지터·홈 밖 숨김은 무시.
sealed class StageCache
{
    public int stage, waves;
    public int[] cnt;
    public double[] hp;
    public double bossHp, bossTime, eAtk, bAtk;
    public long eGold, bGold, bMyc;

    static readonly Dictionary<int, StageCache> cache = new Dictionary<int, StageCache>();

    public static StageCache Get(int stage)
    {
        if (cache.TryGetValue(stage, out var c)) return c;
        c = new StageCache { stage = stage, waves = IdleProgress.WavesPerStage(stage) };
        c.cnt = new int[c.waves + 2];
        c.hp = new double[c.waves + 2];
        for (int w = 1; w <= c.waves; w++) { c.cnt[w] = IdleProgress.EnemyCount(stage, w); c.hp[w] = IdleProgress.EnemyHp(stage, w); }
        c.bossHp = IdleProgress.BossHp(stage);
        c.bossTime = IdleProgress.BossTime(stage);
        c.eAtk = IdleProgress.EnemyAttack(stage);
        c.bAtk = IdleProgress.BossAttack(stage);
        c.eGold = IdleProgress.EnemyGold(stage);
        c.bGold = IdleProgress.BossGold(stage);
        c.bMyc = IdleProgress.BossMycelite(stage);
        cache[stage] = c;
        return c;
    }
}

enum Fail { None, BossTimeout, DefeatWave, DefeatBoss }

sealed class Battle
{
    public const double FlightTime = 0.45, WaveDelay = 0.6, EnemyAttackDelay = 0.4, TargetAge = 0.15;
    public readonly Player P;
    public readonly double Dt;
    public readonly int Fps;
    Random rng => P.rng;

    public StageCache sc;
    public int wave;                    // 0 = 대기, 1..waves 일반, waves+1 보스
    double nextWaveIn, bossTimeLeft;
    public double now;
    public double partyHp, partyMax;

    // 적
    int n;
    readonly double[] hp = new double[160], inc = new double[160], age = new double[160];
    readonly bool[] alive = new bool[160];
    bool bossWave;
    int gen;
    readonly int[] uncl = new int[160], any = new int[160];

    // 투척 (비행 중)
    struct Shot { public double t, dmg; public int idx, gen; }
    readonly Queue<Shot> shots = new Queue<Shot>();
    readonly double[] accum = new double[5];

    // 이벤트
    public Action<long, bool> onKill;       // (골드, 보스인지)
    public Action<int> onStageCleared;      // 넘은 스테이지
    public Action<Fail> onFail;
    public Action onBossStart;               // 보스 등장 (수치안 퀘스트 C5: 스테이지 퀘스트 = 보스 도전 횟수)
    public long killCount;
    public int attemptsAtStage;

    public Battle(Player p, double dt, int fps)
    {
        P = p; Dt = dt; Fps = fps;
        sc = StageCache.Get(p.stage);
        P.Refresh();
        partyMax = partyHp = P.finalHp;
        for (int i = 0; i < 5; i++) accum[i] = rng.NextDouble() * 0.8;   // IdleBattle.cs:150 timer = U(0,0.8)
        StartWave(1);
    }

    void StartWave(int w)
    {
        wave = w;
        gen++;
        shots.Clear();
        bossWave = w > sc.waves;
        if (bossWave)
        {
            n = 1; hp[0] = sc.bossHp; inc[0] = 0; age[0] = 0; alive[0] = true;
            bossTimeLeft = sc.bossTime;
            onBossStart?.Invoke();
        }
        else
        {
            n = sc.cnt[w];
            for (int i = 0; i < n; i++) { hp[i] = sc.hp[w]; inc[i] = 0; age[i] = 0; alive[i] = true; }
        }
        aliveCount = n;
    }

    int aliveCount;

    public void Step()
    {
        double dt = Dt;
        now += dt;
        P.Refresh();
        UpdateGgomas(dt);
        UpdateShots();
        for (int i = 0; i < n; i++) if (alive[i]) age[i] += dt;
        UpdateParty(dt);
        UpdateFlow(dt);
    }

    static int Binomial(Random r, int k, double p)
    {
        if (p <= 0) return 0;
        if (p >= 1) return k;
        if (k <= 32) { int c = 0; for (int i = 0; i < k; i++) if (r.NextDouble() < p) c++; return c; }
        double mean = k * p, sd = Math.Sqrt(k * p * (1 - p));
        double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        return (int)Math.Clamp(Math.Round(mean + sd * z), 0, k);
    }

    void UpdateGgomas(double dt)
    {
        // 던질 수 있는 적이 있나 (age > 0.15, 살아 있음)
        int nu = 0, na = 0;
        for (int i = 0; i < n; i++)
        {
            if (!alive[i] || age[i] <= TargetAge) continue;
            any[na++] = i;
            if (hp[i] - inc[i] > 0) uncl[nu++] = i;
        }
        double rate = Math.Max(0.05, P.speed);      // 게임: timer += 1/Max(0.05, rate)
        int maxThrows = Math.Max(1, (int)Math.Ceiling(dt * Fps - 1e-9));    // 프레임당 1회 던짐 → 한 틱(dt)에 최대 dt×fps번
        for (int g = 0; g < 5; g++)
        {
            if (P.slotMul[g] <= 0) continue;
            // IdleBattle.cs:UpdateGgomas: timer -= dt; timer > 0이면 쉼; 표적 없으면 timer=0.1; 있으면 timer += 1/rate 하고 던진다
            accum[g] -= dt;
            int k = 0;
            while (accum[g] <= 0 && k < maxThrows)
            {
                if (na == 0) { accum[g] = 0.1; break; }
                accum[g] += 1.0 / rate;
                k++;
            }
            if (k <= 0) continue;
            double baseDmg = P.finalAtk * P.slotMul[g];
            if (bossWave)
            {
                int crits = Binomial(rng, k, Math.Min(1.0, P.cc));
                double dmg = baseDmg * (k + crits * (P.cd - 1));
                inc[0] += dmg;
                shots.Enqueue(new Shot { t = now + FlightTime, dmg = dmg, idx = 0, gen = gen });
                continue;
            }
            for (int s = 0; s < k; s++)
            {
                int idx;
                if (nu > 0) { int pos = rng.Next(nu); idx = uncl[pos]; }
                else { idx = any[rng.Next(na)]; }
                bool crit = rng.NextDouble() < P.cc;
                double dmg = baseDmg * (crit ? P.cd : 1.0) * P.norm;
                inc[idx] += dmg;
                shots.Enqueue(new Shot { t = now + FlightTime, dmg = dmg, idx = idx, gen = gen });
                if (nu > 0 && hp[idx] - inc[idx] <= 0)
                {
                    // uncl에서 제거 (뒤 원소로 채움)
                    for (int q = 0; q < nu; q++) if (uncl[q] == idx) { uncl[q] = uncl[--nu]; break; }
                }
            }
        }
    }

    void UpdateShots()
    {
        while (shots.Count > 0 && shots.Peek().t <= now)
        {
            var s = shots.Dequeue();
            if (s.gen != gen) continue;
            inc[s.idx] = Math.Max(0, inc[s.idx] - s.dmg);
            if (!alive[s.idx]) continue;
            hp[s.idx] -= s.dmg;
            if (hp[s.idx] <= 0) KillAt(s.idx, true);
        }
    }

    void KillAt(int i, bool reward)
    {
        alive[i] = false; hp[i] = 0; aliveCount--;
        if (!reward) return;
        bool isBoss = bossWave;
        long gold = isBoss ? sc.bGold : sc.eGold;
        P.gold += gold;
        killCount++;
        onKill?.Invoke(gold, isBoss);
        if (isBoss)
        {
            P.myc += sc.bMyc;
            int cleared = P.stage;
            partyHp = P.finalHp;
            P.stage++;
            sc = StageCache.Get(P.stage);
            wave = 0;
            nextWaveIn = WaveDelay * 2;
            attemptsAtStage = 0;
            onStageCleared?.Invoke(cleared);
        }
    }

    void UpdateParty(double dt)
    {
        double max = P.finalHp;
        if (max > partyMax) partyHp += max - partyMax;
        partyMax = max;
        double dps = 0;
        for (int i = 0; i < n; i++)
            if (alive[i] && age[i] > EnemyAttackDelay) dps += bossWave ? sc.bAtk : sc.eAtk;
        partyHp = Math.Min(partyMax, partyHp + P.finalRegen * dt - dps * dt);
        if (partyHp <= 0) Defeat();
    }

    void Defeat()
    {
        Fail kind = bossWave ? Fail.DefeatBoss : Fail.DefeatWave;
        for (int i = 0; i < n; i++) if (alive[i]) KillAt(i, false);
        wave = 0;
        nextWaveIn = WaveDelay * 2;
        partyHp = partyMax;
        attemptsAtStage++;
        onFail?.Invoke(kind);
    }

    void UpdateFlow(double dt)
    {
        if (wave == sc.waves + 1 && aliveCount > 0 && bossWave)
        {
            bossTimeLeft -= dt;
            if (bossTimeLeft <= 0)
            {
                KillAt(0, false);
                wave = 0;
                nextWaveIn = WaveDelay * 2;
                partyHp = P.finalHp; partyMax = P.finalHp;
                attemptsAtStage++;
                onFail?.Invoke(Fail.BossTimeout);
            }
            return;
        }
        if (aliveCount > 0) return;
        if (nextWaveIn > 0)
        {
            nextWaveIn -= dt;
            if (nextWaveIn > 0) return;
            StartWave(wave + 1);
            return;
        }
        nextWaveIn = WaveDelay;
    }
}
