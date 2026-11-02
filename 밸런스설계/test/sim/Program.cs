using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using MoreMush.Mobile;

static class Program
{
    const string TablePath = @"C:\Project\MMM_Mobile\Assets\Resources\Tables\GameBalance.bytes";
    static string Out = @"C:\Project\MMM_Mobile\밸런스설계\test\out";

    static string Arg(string[] a, string k, string def) { foreach (var x in a) if (x.StartsWith(k + "=")) return x.Substring(k.Length + 1); return def; }

    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        string tbl = Arg(args, "table", TablePath);       // theory 수치안: 고친 xlsx(또는 bytes) 경로를 주입
        Tables.Load(tbl);
        foreach (var kv in Arg(args, "const", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))   // 상수 덮어쓰기 key:value,key:value
        {
            var q = kv.Split(':');
            var d = (Dictionary<string, double>)typeof(GameBalance).GetField("consts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            d[q[0]] = double.Parse(q[1]);
        }
        string cmd = args.Length > 0 ? args[0] : "smoke";
        if (cmd == "smoke") { Smoke(); return 0; }
        if (cmd == "nolevel") { NoLevel(args); return 0; }
        if (cmd == "check")
        {
            int sm = int.Parse(Arg(args, "speedMax", "0"));
            if (sm > 0) IdleProgress.Defs[(int)IdleStat.Speed].maxLv = sm;
            Console.WriteLine($"speed maxLv={IdleProgress.Defs[(int)IdleStat.Speed].maxLv} ExpectedLevel(Speed,1000)={IdleProgress.ExpectedLevel(IdleStat.Speed, 1000)} Value(Speed,that)={IdleProgress.Value(IdleStat.Speed, IdleProgress.ExpectedLevel(IdleStat.Speed, 1000)):F3} BossHp(1000)={IdleProgress.BossHp(1000):E4} BossHp(500)={IdleProgress.BossHp(500):E4} EnemyHp(1000,1)={IdleProgress.EnemyHp(1000, 1):E4}");
            return 0;
        }
        if (cmd == "minlv") { MinLv(args); return 0; }
        if (cmd == "run") { RunScenario(args); return 0; }
        Console.WriteLine("cmd: smoke | nolevel | run name=A|B|C|... [dt= seed= maxDays= goal= restart= hours= ...]");
        return 1;
    }

    static void Smoke()
    {
        Console.WriteLine($"fps cap source: AppSettings.cs TargetFrameRate; stages in table={GameBalance.StageCount}");
        for (int s = 1; s <= 12; s++)
        {
            var c = StageCache.Get(s);
            Console.WriteLine($"stage {s}: waves={c.waves} cnt={string.Join("/", c.cnt.Skip(1).Take(c.waves))} hp1={c.hp[1]:G5} bossHp={c.bossHp:G5} bossT={c.bossTime} eAtk={c.eAtk:G4} bAtk={c.bAtk:G4} eGold={c.eGold} bGold={c.bGold} bMyc={c.bMyc}");
        }
        var p = new Player(1);
        Console.WriteLine($"lv0: atk={p.finalAtk} hp={p.finalHp} regen={p.finalRegen} speed={p.speed} mulSum={p.mulSum:F3} slots={string.Join(",", p.slotMul.Select(x => x.ToString("F2")))}");
    }

    // 무강화: 모든 강화 Lv0·기본 편성·장비 없음. 스테이지마다 N번 진입해서 한 번에 클리어하는 비율
    static void NoLevel(string[] a)
    {
        int trials = int.Parse(Arg(a, "trials", "300")), maxStage = int.Parse(Arg(a, "stages", "15"));
        double dt = double.Parse(Arg(a, "dt", "0.05"));
        Console.WriteLine($"[nolevel] 조건: 모든 강화 Lv0, 기본 편성(leaf,dew,fire,rock,coin Lv1), 장비 없음, 스테이지마다 {trials}회 진입(1웨이브부터, 체력 가득), dt={dt}, fps=60, 최대 300초/회");
        Console.WriteLine("stage,clearRate,defWave,defBoss,bossTimeout,meanClearSec,meanGoldPerAttempt,meanFailSec,meanKillsAtFail");
        for (int s = 1; s <= maxStage; s++)
        {
            int ok = 0, dw = 0, db = 0, bt = 0; double sumT = 0, sumG = 0, sumFailT = 0, sumWave = 0; int nf = 0;
            for (int t = 0; t < trials; t++)
            {
                var p = new Player(1000 * s + t); p.stage = s;
                var b = new Battle(p, dt, 60);
                bool done = false; Fail f = Fail.None;
                b.onStageCleared += _ => done = true;
                b.onFail += x => { f = x; done = true; sumFailT += b.now; sumWave += b.wave; nf++; };
                int guard = (int)(300 / dt);
                while (!done && guard-- > 0) b.Step();
                sumG += p.gold;
                if (f == Fail.None && done) { ok++; sumT += b.now; }
                else if (f == Fail.DefeatWave) dw++; else if (f == Fail.DefeatBoss) db++; else if (f == Fail.BossTimeout) bt++;
            }
            Console.WriteLine($"{s},{(double)ok / trials:F3},{dw},{db},{bt},{(ok > 0 ? sumT / ok : 0):F1},{sumG / trials:F0},{(nf > 0 ? sumFailT / nf : 0):F1},{killsAtFail(sumG, s, trials):F1}");
        }
    }

    // 스테이지 s를 90% 이상 한 번에 깨는 데 필요한 최소 공격력=체력 레벨 L (나머지 강화 Lv0·기본 편성·장비 없음)과 그 누적 골드
    static void MinLv(string[] a)
    {
        int trials = int.Parse(Arg(a, "trials", "100")), maxStage = int.Parse(Arg(a, "stages", "10"));
        Console.WriteLine($"[minlv] 조건: 공격력=체력=L, 나머지 강화 Lv0, 기본 편성, 장비 없음, 클리어율 90% 이상 최소 L (이분 탐색, {trials}회), dt=0.05");
        Console.WriteLine("stage,minL,clearRateAtL,goldForAtkHp,enemyGoldPerKill");
        for (int s = 1; s <= maxStage; s++)
        {
            int lo = 0, hi = 4000; double rateHi = 0;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                double r = Rate(s, mid, trials);
                if (r >= 0.9) { hi = mid; rateHi = r; } else lo = mid + 1;
            }
            long gold = 0;
            for (int l = 0; l < lo; l++) gold += IdleProgress.Cost(IdleStat.Attack, l) + IdleProgress.Cost(IdleStat.Hp, l);
            Console.WriteLine($"{s},{lo},{Rate(s, lo, trials):F2},{gold},{StageCache.Get(s).eGold}");
        }
    }

    static double Rate(int s, int L, int trials)
    {
        int ok = 0;
        for (int t = 0; t < trials; t++)
        {
            var p = new Player(777 * s + t) { stage = s };
            p.lv[(int)IdleStat.Attack] = L; p.lv[(int)IdleStat.Hp] = L; p.dirty = true;
            var b = new Battle(p, 0.05, 60);
            bool done = false, win = false;
            b.onStageCleared += _ => { done = true; win = true; };
            b.onFail += _ => done = true;
            int guard = 6000;
            while (!done && guard-- > 0) b.Step();
            if (win) ok++;
        }
        return (double)ok / trials;
    }

    static double killsAtFail(double sumG, int s, int trials) => sumG / trials / Math.Max(1, StageCache.Get(s).eGold);

    static void RunScenario(string[] a)
    {
        var p = new Params();
        string name = Arg(a, "name", "A");
        p.name = name;
        p.dt = double.Parse(Arg(a, "dt", "0.05"));
        p.seed = int.Parse(Arg(a, "seed", "1"));
        p.maxDays = double.Parse(Arg(a, "maxDays", "400"));
        p.stuckDays = double.Parse(Arg(a, "stuck", "30"));
        p.stageGoal = int.Parse(Arg(a, "goal", "1000"));
        p.fps = int.Parse(Arg(a, "fps", "60")); Player.Fps = p.fps; Player.Policy = Arg(a, "policy", "bal"); Player.NoGacha = Arg(a, "gacha", "on") == "off"; Player.EquipGame = Arg(a, "equip", "best") == "game";
        p.killedEachAway = Arg(a, "restart", "killed") == "killed";
        // 시나리오: A 연속(24h) · B 하루 3시간(3회)+앱 밖+퀘스트 · C 하루 1시간(1회)+앱 밖+퀘스트 (앱 밖 상한 8시간 확인). 기준선은 모두 무광고. 이름 끝에 "ads"를 붙이면 광고 전부(상자·2배·광고 뽑기·광고 던전) 켠 비교용
        string kind = name.Substring(0, 1);
        bool adsAll = name.EndsWith("ads"), adsHalf = name.EndsWith("half"), adsOn = adsAll || adsHalf;
        double hours = double.Parse(Arg(a, "hours", kind == "C" ? "1" : "3")); int sess = int.Parse(Arg(a, "sessions", kind == "C" ? "1" : "3"));
        p.dungeonMycDailyFlat = double.Parse(Arg(a, "dungMyc", "0"));
        int speedMax = int.Parse(Arg(a, "speedMax", "0"));
        if (speedMax > 0) IdleProgress.Defs[(int)IdleStat.Speed].maxLv = speedMax;   // 표 Speed maxLevel을 낮추는 조건 (표가 바뀌기 전 리플렉션 주입)
        // 던전 레벨 보상 훅(manager#6): 보상 = 현재 방치 스테이지 적 골드 × dungKills × (1 + dungGrow×(던전 레벨−1)), 균사석 = dungMyc × (같은 배율). 하루 무료 2 + 광고 2(광고 켠 시나리오만)
        double dk = double.Parse(Arg(a, "dungKills", "0")), dm = double.Parse(Arg(a, "dungMycRun", "0")), dg = double.Parse(Arg(a, "dungGrow", "0"));
        if (dk > 0 || dm > 0)
        {
            p.dungeonOn = true;
            p.dungeonReward = (L, stg) => { double mul = 1 + dg * (L - 1); return ((double)IdleProgress.EnemyGold(stg) * dk * mul, (long)(dm * mul)); };
        }
        if (adsHalf) { p.chestRate = 0.5; p.doubleRate = 0.5; p.gachaKinds = new[] { 0, 2 }; p.dungeonAd = 1; }   // 광고 절반: 상자 50%·정산2배 세션 50%·광고 뽑기 2종(무기·펫)·던전 광고 1/일
        if (adsOn)   // 시청률·종류·던전 광고 덮어쓰기 (theory의 구간별 시청률용): chestRate= doubleRate= adKinds=0,2 dungAd=
        {
            p.chestRate = double.Parse(Arg(a, "chestRate", p.chestRate.ToString()));
            p.doubleRate = double.Parse(Arg(a, "doubleRate", p.doubleRate.ToString()));
            p.dungeonAd = int.Parse(Arg(a, "dungAd", p.dungeonAd.ToString()));
            string ak = Arg(a, "adKinds", ""); if (ak != "") p.gachaKinds = ak == "-" ? new int[0] : ak.Split(',').Select(int.Parse).ToArray();
        }
        string seg = Arg(a, "seg", "");
        if (seg != "")   // 광고 구간 (이론.md 9.1·9.2): none 0 · light 0.25 · half 0.5 · heavy 1.0 — 상자·정산2배·던전 광고 = 시청률, 광고 뽑기 = round(4r)종 (무기·펫·방어구·꼬마 순)
        {
            double r = seg == "none" ? 0 : seg == "light" ? 0.25 : seg == "half" ? 0.5 : 1.0;
            adsOn = r > 0; p.seg = seg;
            p.chestRate = r; p.doubleRate = r; p.dungeonAdRate = r;
            p.gachaKinds = new[] { 0, 2, 1, 3 }.Take((int)Math.Round(4 * r)).ToArray();
            p.dungeonAd = 2;
        }
        p.dungeonSheet = Tables.Dungeons.Count > 0 && Arg(a, "dungeon", "on") == "on";
        p.bossQuest = Arg(a, "bossquest", "off") == "on";   // C5(보스 도전 퀘스트)는 아직 코드에 없다: 수치안 측정에서만 bossquest=on
        if (p.dungeonSheet) p.dungeonOn = true;
        if (kind == "A") { p.continuous = true; p.ads = adsOn; }
        else
        {
            p.continuous = false; p.quests = true; p.offline = true;
            p.ads = adsOn;
            p.dungeonOn = p.dungeonOn || p.dungeonMycDailyFlat > 0;
            p.sessLenH = hours / sess;
            p.sessStartH = sess == 1 ? new[] { 20.0 } : sess == 2 ? new[] { 8.0, 20.0 } : new[] { 7.0, 13.0, 20.0 };
        }
        { var asm = typeof(GameBalance).Assembly.Location; Console.WriteLine($"[dll] {asm} · {File.GetLastWriteTime(asm):yyyy-MM-dd HH:mm:ss}"); }
        Console.WriteLine("[조건] " + p + " policy=" + Player.Policy + " gacha=" + (Player.NoGacha ? "off" : "on") + " equip=" + (Player.EquipGame ? "game(처음 묶음만 자동 장착)" : "best") + " table=" + System.IO.Path.GetFileName(Arg(a, "table", TablePath)) + " const=" + Arg(a, "const", "-") + " dungeonKills/myc/grow=" + dk + "/" + dm + "/" + dg + " speedMaxLv=" + IdleProgress.Defs[(int)IdleStat.Speed].maxLv);
        var sw = Stopwatch.StartNew();
        var sc = new Scenario(p);
        sc.Run();
        Directory.CreateDirectory(Out);
        string tag = Arg(a, "tag", name);
        sc.WriteCsv(Path.Combine(Out, "stage_" + tag + ".csv"));
        double[] marks = { 10, 50, 100, 200, 300, 400, 500, 1000 };
        Console.WriteLine($"[끝] {sc.endReason} · 최종 스테이지 {sc.P.stage} · 벽시계 {sc.wall / 86400:F2}일 · 플레이 {sc.play / 3600:F1}시간 · 실행 {sw.Elapsed.TotalSeconds:F1}s");
        Console.WriteLine("stage,wallHours,wallDays,playHours");
        foreach (int m in marks)
        {
            var r = sc.rec[Math.Min(m, sc.rec.Length - 1)];
            if (r.wallAtReach < 0) Console.WriteLine($"{m},미도달,,");
            else Console.WriteLine($"{m},{r.wallAtReach / 3600:F2},{r.wallAtReach / 86400:F2},{r.playAtReach / 3600:F2}");
        }
        Console.WriteLine($"첫7일 골드: 전투={(sc.snap7 == null ? -1 : sc.snap7[0]):E3} 앱밖={(sc.snap7 == null ? -1 : sc.snap7[1]):E3} 퀘스트상자={(sc.snap7 == null ? -1 : sc.snap7[2]):E3} 던전={(sc.snap7 == null ? -1 : sc.snap7[3]):E3}");
        Console.WriteLine($"첫사용(벽시계 분): 던전={(sc.firstDungeon < 0 ? -1 : sc.firstDungeon / 60):F1} 뽑기={(sc.firstPull < 0 ? -1 : sc.firstPull / 60):F1} 퀘스트={(sc.firstQuest < 0 ? -1 : sc.firstQuest / 60):F1} · 첫1시간 강화={sc.upg1h}");
        Console.WriteLine($"재화 합계: 처치골드={sc.totKillGold:E3} 앱밖={sc.totOfflineGold:E3} 퀘스트골드={sc.totQuestGold:E3} 상자골드={sc.totChestGold:E3} 던전골드={sc.totDungeonGold:E3} | 균사석 보스={sc.totBossMyc} 퀘스트={sc.totQuestMyc} 상자={sc.totChestMyc} 던전={sc.totDungeonMyc} | 광고 {sc.adViews}회(상자 {sc.chestCount}) 뽑기 {sc.P.pulls}회 · 일수 {sc.dayCount}");
        Console.WriteLine($"던전: 입장 {sc.dungeonRuns}회 (레벨 미달 실패 {sc.dungeonFails}회) · 퀘스트 균사석 합 {sc.totQuestMyc} · 하루 평균 {sc.totQuestMyc / Math.Max(1.0, sc.dayCount):F1}");
        Console.WriteLine($"최종 강화 Lv: {string.Join(",", sc.P.lv)} · 편성 {string.Join(",", sc.P.slotMul.Select(x => x.ToString("F2")))} · 장비% 무기={sc.P.eqPct[0]:F0} 방어구={sc.P.eqPct[1]:F0} 펫={sc.P.eqPct[2]:F0} · 뽑기 Lv {string.Join(",", sc.P.gLv)}");
    }
}
