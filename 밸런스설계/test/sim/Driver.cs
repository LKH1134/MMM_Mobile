using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MoreMush.Mobile;

// 시나리오 진행: 하루 일정(플레이 구간·앱 밖) · 일일 퀘스트 · 광고(뽑기·상자·정산 2배) · 앱 밖 보상 · 던전 훅
sealed class Params
{
    public string name = "A";
    public bool continuous = true;          // 24시간 연속
    public bool quests, ads, offline, dungeonOn;
    public double[] sessStartH = { 7, 13, 20 };
    public double sessLenH = 1;
    public bool killedEachAway = true;      // 앱 밖마다 프로세스가 죽어 저장된 직전 세션 평균 골드/초를 쓴다 (false = 평균이 처음부터 누적)
    public double homeFraction = 0.7;       // 플레이 시간 중 홈 화면을 보는 비율 (광고 상자 시간은 홈에서만 간다)
    public int fps = 60;
    public double dt = 0.05;
    public int seed = 1;
    public double maxDays = 400, stuckDays = 30;
    public int stageGoal = 1000;
    // 던전 훅 (manager#6): (던전 레벨, 방치 스테이지) → 보상. null이면 0. 하루 무료 2회 + 광고 2회, 레벨은 클리어마다 +1
    public Func<int, int, (double gold, long myc)> dungeonReward;
    public int dungeonFree = 2, dungeonAd = 2;
    public double dungeonAdRate = 0;                  // 던전 시트(새 열)가 있을 때: 던전마다 광고 입장 2회 중 이 비율을 본다
    public bool bossQuest = false;                    // C5: 스테이지 퀘스트를 보스 클리어가 아니라 보스 등장(도전) 때 센다
    public string seg = "";
    public bool dungeonSheet = false;                 // 던전 시트 새 열 사용
    public double chestRate = 1, doubleRate = 1;     // 광고 시청률 0~1: 광고 상자(나온 것 중)·정산 2배(앞 공백이 있는 세션 중). 번갈아(누적 올림 규칙: 0.5면 1·3·5번째를 봄)
    public int[] gachaKinds = { 0, 1, 2, 3 };         // 광고 뽑기를 보는 뽑기 종류 (0 무기 1 방어구 2 펫 3 꼬마)
    public double dungeonMycDailyFlat = 0;  // 민감도용: 매일 고정 균사석 추가
    public override string ToString() =>
        $"name={name} continuous={continuous} quests={quests} ads={ads} offline={offline} sess={(continuous ? "24h" : string.Join("/", sessStartH) + "h x " + sessLenH + "h")} killedEachAway={killedEachAway} home={homeFraction} fps={fps} dt={dt} seed={seed} maxDays={maxDays} stuckDays={stuckDays} dungeon={dungeonOn} dungeonFlatMyc/day={dungeonMycDailyFlat} 구간={seg} 보스도전퀘스트={bossQuest} 던전광고율={dungeonAdRate} 광고 시청률: 상자 {chestRate} 정산2배 {doubleRate} (번갈아, 누적 올림 규칙), 광고뽑기 종류 [{string.Join(",", gachaKinds)}], 던전광고 {dungeonAd}/일";
}

sealed class StageRec
{
    public double wallAtReach = -1, playAtReach = -1;
    public int attempts, failBoss, defNormal, defBoss;
    public int[] lv; public double dps, hp, atk;
    public double bossRatio, survRatio;     // 스테이지 진입 때: (실제 보스 dps ÷ 필요 dps), (보스전 버틸 시간 ÷ 필요 시간)
    public long goldEarned;
}

sealed class Scenario
{
    public Params prm;
    public Player P;
    public Battle B;
    public StageRec[] rec;
    public double wall, play;
    public string endReason = "";
    public double totOfflineGold, totQuestGold, totChestGold, totKillGold, totDungeonGold;
    public long totQuestMyc, totChestMyc, totBossMyc, totDungeonMyc;
    public int adViews, chestCount, chestSeen, dayCount, doubleSeen;
    public double sessGold, sessSec, savedRate;
    // 퀘스트
    QuestRow[] qrows = GameBalance.Quests.ToArray();
    int[] qprog; bool[] qdone; int qDay = -1;
    int adToday; bool[] adGachaUsed = new bool[4];
    double homeAcc;
    int dungeonLevel = 1;
    readonly Dictionary<int, int> dLevel = new Dictionary<int, int>();       // 던전 시트 모드: 던전별 레벨
    readonly Dictionary<int, int[]> dToday = new Dictionary<int, int[]>();    // 던전별 오늘 [무료 사용, 광고 슬롯 결정]
    int dungAdSeen;
    public double totDungeonMycGain;
    // D5·D6: 첫 7일 재화 출처 스냅샷, 첫 사용 시각(벽시계 초, -1=미사용), 첫 1시간(플레이) 강화 횟수
    public double[] snap7; public double firstDungeon = -1, firstPull = -1, firstQuest = -1; public int upg1h;
    public int dungeonRuns, dungeonFails;
    double lastStageUpPlay;

    public Scenario(Params p)
    {
        prm = p;
        P = new Player(p.seed);
        rec = new StageRec[p.stageGoal + 5];
        for (int i = 0; i < rec.Length; i++) rec[i] = new StageRec();
        qprog = new int[qrows.Length]; qdone = new bool[qrows.Length];
    }

    void NewDay(int day)
    {
        qDay = day;
        Array.Clear(qprog, 0, qprog.Length); Array.Clear(qdone, 0, qdone.Length);
        adToday = 0; Array.Clear(adGachaUsed, 0, 4);
        dToday.Clear();
        dayCount++;
    }

    void QuestAdd(string kind, int amount)
    {
        if (!prm.quests) return;
        for (int i = 0; i < qrows.Length; i++)
        {
            if (!string.Equals(qrows[i].kind, kind, StringComparison.OrdinalIgnoreCase) || qdone[i]) continue;
            qprog[i] = Math.Min(qrows[i].target, qprog[i] + amount);
            if (qprog[i] >= qrows[i].target)
            {
                if (firstQuest < 0) firstQuest = wall;
                qdone[i] = true;      // 수령 (게임에서는 수동 수령, 접속 중이므로 바로 받는다고 가정)
                double g = (double)IdleProgress.EnemyGold(P.stage) * qrows[i].goldKills;
                P.gold += g; totQuestGold += g;
                P.myc += qrows[i].mycelite; totQuestMyc += qrows[i].mycelite;
            }
        }
    }

    // n번째 기회를 보는가: ceil(n×rate) > ceil((n−1)×rate)
    static bool Watch(int n, double rate) => Math.Ceiling(n * rate - 1e-9) > Math.Ceiling((n - 1) * rate - 1e-9);

    void AdView() { adViews++; adToday++; QuestAdd("ad", 1); }

    void Build()
    {
        P.onUpgradeBought = n => QuestAdd("upgrade", n);
        P.onPulled = n => QuestAdd("gacha", n);
        B = new Battle(P, prm.dt, prm.fps);
        if (prm.bossQuest) B.onBossStart += () => QuestAdd("stage", 1);
        B.onKill += (g, boss) => { sessGold += g; totKillGold += g; if (boss) totBossMyc += B.sc.bMyc; QuestAdd("kill", 1); };
        B.onStageCleared += s =>
        {
            if (!prm.bossQuest) QuestAdd("stage", 1);
            lastStageUpPlay = wall;
            Enter(P.stage);
        };
        B.onFail += f =>
        {
            var r = rec[Math.Min(P.stage, rec.Length - 1)];
            r.attempts++;
            if (f == Fail.BossTimeout) r.failBoss++; else if (f == Fail.DefeatWave) r.defNormal++; else r.defBoss++;
        };
    }

    void Enter(int stage)
    {
        if (stage >= rec.Length) return;
        var r = rec[stage];
        if (r.wallAtReach >= 0) return;
        r.wallAtReach = wall; r.playAtReach = play;
        P.Refresh();
        r.lv = (int[])P.lv.Clone();
        var sc = StageCache.Get(stage);
        double rate = Math.Min(P.speed, prm.fps);
        double critF = 1 + Math.Min(1, P.cc) * (P.cd - 1);
        double bossDps = P.finalAtk * P.mulSum * rate * critF;
        r.dps = bossDps; r.hp = P.finalHp; r.atk = P.finalAtk;
        r.bossRatio = bossDps * sc.bossTime / sc.bossHp;
        double net = sc.bAtk - P.finalRegen;
        double ttd = net <= 0 ? 1e9 : P.finalHp / net;
        double need = sc.bossHp / Math.Max(1e-9, bossDps);
        r.survRatio = ttd / Math.Max(1e-9, need);
    }

    void Pay()
    {
        { int n = P.BuyUpgrades(); if (play < 3600) upg1h += n; }
        if (P.myc >= 100) P.SpendMycelite();
    }

    // 던전 시트(수치안 새 열) 모드: 레벨 L의 기준 스테이지 s_L = baseStage + stageStep×(L−1). 방치 스테이지가 s_L 이상일 때만 성공해 L+1,
    // 골드 던전 = EnemyGold(min(방치 스테이지, s_L)) × rewardKills, 균사석 = rewardMyc × (1 + rewardGrowth×(L−1)). 입장 = 무료 dungeonFree + 광고 슬롯 dungeonAd(시청률 dungeonAdRate, 하루 1번 결정)
    void DoDungeonSheet()
    {
        foreach (var ds in Tables.Dungeons)
        {
            if (P.stage < ds.unlockStage) continue;
            if (!dLevel.ContainsKey(ds.dungeon)) dLevel[ds.dungeon] = 1;
            if (!dToday.TryGetValue(ds.dungeon, out var t)) { t = new int[2]; dToday[ds.dungeon] = t; }
            int free = prm.dungeonFree - t[0];
            int adSlots = (t[1] == 0) ? prm.dungeonAd : 0;
            int runs = Math.Max(0, free);
            t[0] = prm.dungeonFree;
            int adRuns = 0;
            if (adSlots > 0)
            {
                t[1] = 1;
                for (int i = 0; i < adSlots; i++) { dungAdSeen++; if (prm.ads && Watch(dungAdSeen, prm.dungeonAdRate)) { adRuns++; } }
            }
            for (int i = 0; i < runs + adRuns; i++)
            {
                if (i >= runs) AdView();
                int L = dLevel[ds.dungeon];
                int sL = ds.baseStage + ds.stageStep * (L - 1);
                dungeonRuns++;
                if (sL > P.stage) { dungeonFails++; continue; }
                dLevel[ds.dungeon] = L + 1;
                if (ds.currency == "Gold" || ds.currency == "Both")
                { double g = (double)IdleProgress.EnemyGold(Math.Min(P.stage, sL)) * ds.rewardKills; P.gold += g; totDungeonGold += g; }
                if (ds.currency == "Mycelite" || ds.currency == "Both")
                { long m = (long)(ds.rewardMyc * (1 + ds.rewardGrowth * (L - 1))); P.myc += m; totDungeonMyc += m; }
            }
        }
    }

    void DoDungeon()
    {
        if ((prm.dungeonSheet || prm.dungeonOn) && firstDungeon < 0) firstDungeon = wall;
        if (prm.dungeonSheet) { DoDungeonSheet(); return; }
        if (!prm.dungeonOn) return;
        int runs = prm.dungeonFree + (prm.ads ? prm.dungeonAd : 0);
        for (int i = 0; i < runs; i++)
        {
            if (prm.dungeonReward != null)
            {
                var (g, m) = prm.dungeonReward(dungeonLevel, P.stage);
                P.gold += g; P.myc += m; totDungeonGold += g; totDungeonMyc += m;
            }
            dungeonLevel++;
            if (i >= prm.dungeonFree) AdView();
        }
        if (prm.dungeonMycDailyFlat > 0) { P.myc += (long)prm.dungeonMycDailyFlat; totDungeonMyc += (long)prm.dungeonMycDailyFlat; }
    }

    // 세션 시작: 앱 밖 보상(상한 8시간), 새날 처리, 광고 뽑기, 던전
    void StartSession(double awaySec)
    {
        int day = (int)(wall / 86400);
        if (day != qDay) NewDay(day);
        if (prm.offline && awaySec >= GameBalance.Const("offlineMinSeconds"))
        {
            double secs = Math.Min(awaySec, GameBalance.Const("offlineCapHours") * 3600);
            double rate = prm.killedEachAway ? savedRate : (sessSec >= 30 ? sessGold / sessSec : savedRate);
            double g = rate * secs * Tables.ConstOr("offlineGoldRate", 1.0);   // C3: 수치안 앱 밖 골드 비율 (상수 없으면 1)
            P.gold += g; totOfflineGold += g;
            doubleSeen++;
            if (prm.ads && Watch(doubleSeen, prm.doubleRate)) { P.gold += g; totOfflineGold += g; AdView(); }   // 정산 팝업의 "광고 보고 2배"
        }
        if (prm.killedEachAway) { if (sessSec >= 30) savedRate = sessGold / sessSec; sessGold = 0; sessSec = 0; }
        if (prm.ads)
            foreach (int k in prm.gachaKinds)
                if (!adGachaUsed[k]) { adGachaUsed[k] = true; AdView(); P.Pull(k, (int)Tables.ConstOr("adGachaPulls", 11)); }
        DoDungeon();
    }

    void EndSession()
    {
        if (sessSec >= 30) savedRate = sessGold / sessSec;
    }

    public void Run()
    {
        Build();
        Enter(1);
        double maxSec = prm.maxDays * 86400, stuckSec = prm.stuckDays * 86400;
        var sessions = new List<(double s, double e)>();
        int dayIdx = 0;
        double prevEnd = -1;
        bool first = true;
        while (wall < maxSec)
        {
            double sStart, sEnd;
            if (prm.continuous) { sStart = 0; sEnd = maxSec; }
            else
            {
                int si = sessions.Count % prm.sessStartH.Length;
                dayIdx = sessions.Count / prm.sessStartH.Length;
                sStart = dayIdx * 86400.0 + prm.sessStartH[si] * 3600;
                sEnd = sStart + prm.sessLenH * 3600;
                sessions.Add((sStart, sEnd));
            }
            double away = prevEnd < 0 ? 0 : sStart - prevEnd;
            wall = sStart;
            if (!prm.continuous) StartSession(first ? 0 : away);
            else if (first) { NewDay(0); }
            first = false;
            while (wall < sEnd)
            {
                B.Step();
                wall += prm.dt; play += prm.dt; sessSec += prm.dt;
                if (snap7 == null && wall >= 7 * 86400) snap7 = new[] { totKillGold, totOfflineGold, totQuestGold + totChestGold, totDungeonGold };
                if (firstPull < 0 && P.pulls > 0) firstPull = wall;
                homeAcc += prm.dt * prm.homeFraction;
                if (prm.ads && homeAcc >= GameBalance.Const("adBoxIntervalHours") * 3600)
                {
                    homeAcc = 0; chestSeen++;
                    if (Watch(chestSeen, prm.chestRate)) goto watch;
                    goto skip;
                watch:
                    chestCount++; AdView();
                    double g = (double)IdleProgress.EnemyGold(P.stage) * GameBalance.Const("adBoxGoldKills");
                    long m = (long)(IdleProgress.BossMycelite(P.stage) * GameBalance.Const("adBoxMyceliteBosses"));
                    P.gold += g; totChestGold += g; P.myc += m; totChestMyc += m;
                skip:;
                }
                Pay();
                if (P.stage >= prm.stageGoal) { endReason = "goal"; goto done; }
                if (wall - lastStageUpPlay > stuckSec) { endReason = $"stuck at stage {P.stage} ({prm.stuckDays}d no progress)"; goto done; }
            }
            EndSession();
            prevEnd = sEnd;
        }
        endReason = "maxDays";
    done:
        return;
    }

    // 스테이지별 CSV (stage, 도달 벽시계 초, 플레이 초, 시도·실패, 진입 시 능력 비율, 레벨)
    public void WriteCsv(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("stage,wallSec,playSec,attempts,failBoss,defWave,defBoss,bossRatio,survRatio,dps,hp,atk,lvAtk,lvHp,lvRegen,lvSpeed,lvCC,lvCD,lvAdv,lvNorm");
        for (int s = 1; s < rec.Length; s++)
        {
            var r = rec[s];
            if (r.wallAtReach < 0) break;
            sb.AppendLine(string.Join(",", new[] {
                s.ToString(), r.wallAtReach.ToString("F1", CultureInfo.InvariantCulture), r.playAtReach.ToString("F1", CultureInfo.InvariantCulture),
                r.attempts.ToString(), r.failBoss.ToString(), r.defNormal.ToString(), r.defBoss.ToString(),
                r.bossRatio.ToString("G6", CultureInfo.InvariantCulture), r.survRatio.ToString("G6", CultureInfo.InvariantCulture),
                r.dps.ToString("G6", CultureInfo.InvariantCulture), r.hp.ToString("G6", CultureInfo.InvariantCulture), r.atk.ToString("G6", CultureInfo.InvariantCulture) }
                .Concat(r.lv.Select(v => v.ToString()))));
        }
        File.WriteAllText(path, sb.ToString());
    }
}
