using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MoreMush.Mobile;
using MoreMush.Mobile.EditorTools;

static class Program
{
    static int fails, checks;
    static double maxRel;

    static void Same(string what, double a, double b, double rel = 1e-9)
    {
        checks++;
        double d = Math.Abs(a - b), scale = Math.Max(Math.Abs(a), Math.Abs(b));
        double r = scale == 0 ? 0 : d / scale;
        if (r > maxRel) maxRel = r;
        if (!(d == 0 || r <= rel))
        {
            if (++fails <= 25) Console.WriteLine($"FAIL {what}: new={a:R} old={b:R}");
        }
    }

    static void SameStr(string what, string a, string b)
    {
        checks++;
        if (a != b && ++fails <= 25) Console.WriteLine($"FAIL {what}: new='{a}' old='{b}'");
    }

    static T Set<T>(string field, T value)
    {
        typeof(GameBalance).GetField(field, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
        return value;
    }

    static T[] Rows<T>(Dictionary<string, List<string[]>> sheets, string sheet, int min, int max, List<string> issues) where T : TableRow, new()
    {
        var table = new DataTable<T>(sheet, min, max);
        table.Load(sheets[sheet], issues);
        return table.All.ToArray();
    }

    static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : @"C:\Project\MMM_Mobile\Assets\Resources\Tables\GameBalance.bytes";
        byte[] bytes = File.ReadAllBytes(path);

        var vi = new List<string>();
        int vrows = GameBalanceValidator.Validate(bytes, vi, false);
        Console.WriteLine($"validator: rows={vrows} issues={vi.Count}");
        foreach (var i in vi.Take(20)) Console.WriteLine("  " + i);
        if (args.Length > 1 && args[1] == "validate-only") return vi.Count == 0 ? 0 : 1;

        var sheets = XlsxReader.Read(bytes);
        var issues = new List<string>();
        var consts = Rows<ConstRow>(sheets, "상수", 9001, 9999, issues);
        Set("consts", consts.ToDictionary(r => r.key, r => r.value));
        Set("grades", Rows<GradeRow>(sheets, "등급", 8101, 8199, issues).OrderBy(r => r.grade).ToArray());
        Set("gachaLevels", Rows<GachaLevelRow>(sheets, "뽑기", 8201, 8299, issues).OrderBy(r => r.level).ToArray());
        Set("upgrades", Rows<UpgradeRow>(sheets, "업그레이드", 4001, 4999, issues));
        Set("mascots", Rows<MascotRow>(sheets, "꼬마", 3001, 3999, issues));
        Set("stages", Rows<StageRow>(sheets, "스테이지", 5001, 6999, issues).OrderBy(r => r.stage).ToArray());
        Set("resources", Rows<ResourceRow>(sheets, "리소스", 7001, 7999, issues).ToDictionary(r => r.index, r => r.path));
        Set("dungeonUnlock", Rows<DungeonRow>(sheets, "던전", 9101, 9199, issues).ToDictionary(r => r.dungeon, r => r.unlockStage));
        Set("loaded", true);
        int real = issues.Count(i => !(i.Contains("기본값으로 둔다") && (i.Contains("'name'") || i.Contains("'desc'") || i.Contains("'icon'"))));
        Console.WriteLine($"loader issues (non-ignorable) = {real}");
        foreach (var i in issues.Where(i => !i.Contains("기본값으로 둔다")).Take(10)) Console.WriteLine("  " + i);

        // ── 강화 정의 ──
        for (int s = 0; s < IdleProgress.Defs.Length; s++)
        {
            var n = IdleProgress.Defs[s];
            var o = IdleProgressOld.Defs[s];
            SameStr($"def{s}.name", n.name, o.name);
            SameStr($"def{s}.icon", n.icon, o.icon);
            Same($"def{s}.base", n.baseValue, o.baseValue, 0); Same($"def{s}.per", n.perLevel, o.perLevel, 0);
            Same($"def{s}.max", n.maxLv, o.maxLv, 0); Same($"def{s}.costBase", n.costBase, o.costBase, 0);
            Same($"def{s}.costExp", n.costExp, o.costExp, 0); Same($"def{s}.costScale", n.costScale, o.costScale, 0);
            Same($"def{s}.unlockLv", n.unlockLv, o.unlockLv, 0); Same($"def{s}.capEvery", n.capEvery, o.capEvery, 0);
            Same($"def{s}.capStep", n.capStep, o.capStep, 0);
            if (o.unlockLv > 0) SameStr($"def{s}.unlockStat", n.unlockStat.ToString(), o.unlockStat.ToString());
            Same($"def{s}.multiply", n.multiply ? 1 : 0, o.stat == IdleStat.Speed ? 1 : 0, 0);
            Same($"def{s}.divisor", n.costDivisor, 10, 0);
        }

        // ── 강화 비용·값·단계 제한 ──
        foreach (IdleStat st in Enum.GetValues(typeof(IdleStat)))
        {
            int max = IdleProgress.Get(st).maxLv;
            var lvs = new SortedSet<int> { 0, 1, 2, 9, 10, max - 1, max };
            for (int lv = 0; lv <= max; lv += 997) lvs.Add(lv);
            foreach (int lv in lvs)
            {
                Same($"cost {st} {lv}", IdleProgress.Cost(st, lv), IdleProgressOld.Cost(st, lv), 0);
                Same($"value {st} {lv}", IdleProgress.Value(st, lv), IdleProgressOld.Value(st, lv), 0);
                Same($"cap {st} {lv}", IdleProgress.CapLevel(st, lv), IdleProgressOld.CapLevel(st, lv), 0);
            }
        }

        // ── 스테이지별 값 (표 안 1~1000, 표 밖 1001~1500) ──
        foreach (int stage in Enumerable.Range(-2, 1503).Concat(new[] { 5000, 100000 }))
        {
            Same($"waves {stage}", IdleProgress.WavesPerStage(stage), IdleProgressOld.WavesPerStage, 0);
            Same($"bossTime {stage}", IdleProgress.BossTime(stage), IdleProgressOld.BossTime, 0);
            for (int wave = 1; wave <= 6; wave++)
            {
                Same($"count {stage}/{wave}", IdleProgress.EnemyCount(stage, wave), IdleProgressOld.EnemyCount(stage, wave), 0);
                Same($"enemyHp {stage}/{wave}", IdleProgress.EnemyHp(stage, wave), IdleProgressOld.EnemyHp(stage, wave), 0);
            }
            Same($"bossHp {stage}", IdleProgress.BossHp(stage), IdleProgressOld.BossHp(stage), 0);
            Same($"enemyAttack {stage}", IdleProgress.EnemyAttack(stage), IdleProgressOld.EnemyAttack(stage));
            Same($"bossAttack {stage}", IdleProgress.BossAttack(stage), IdleProgressOld.BossAttack(stage));
            Same($"enemyGold {stage}", IdleProgress.EnemyGold(stage), IdleProgressOld.EnemyGold(stage), 0);
            Same($"bossGold {stage}", IdleProgress.BossGold(stage), IdleProgressOld.BossGold(stage), 0);
            Same($"mycelite {stage}", IdleProgress.BossMycelite(stage), IdleProgressOld.BossMycelite(stage), 0);
            foreach (IdleStat st in Enum.GetValues(typeof(IdleStat)))
                Same($"expLv {st} {stage}", IdleProgress.ExpectedLevel(st, stage), IdleProgressOld.ExpectedLevel(st, stage), 0);
        }

        // ── Formation 내부 표 (PlayerPrefs 없이) ──
        var tableType = typeof(Formation).GetNestedType("Table", BindingFlags.NonPublic);
        var t = Activator.CreateInstance(tableType, true);
        string[] Str(string f) => (string[])tableType.GetField(f).GetValue(t);
        int[] Ints(string f) => (int[])tableType.GetField(f).GetValue(t);
        SameStr("defaults", string.Join(",", Str("defaults")), "leaf,dew,fire,rock,coin");
        SameStr("startLevels", string.Join(",", Ints("startLevels")), "1,1,0,1,1,0,1,0,0");
        SameStr("levelKey0", Str("levelKeys")[0], "Formation.Level.leaf");
        SameStr("copiesKey8", Str("copiesKeys")[8], "Formation.Copies.beak");
        var icons = Str("iconPaths");
        for (int i = 0; i < icons.Length; i++)
            SameStr($"icon {i}", icons[i], i == 8 ? "Art/Dream/Characters/Critters/Bosses/boss1" : "Art/Dream/Characters/Critters/Ggomas/icon_" + Str("ids")[i]);

        // ── 던전 해금 ──
        int[] wantUnlock = { 5, 15, 30, 80, 150 };
        for (int d = 1; d <= 5; d++) Same($"unlock {d}", DungeonTickets.UnlockStage(d), wantUnlock[d - 1], 0);
        Same("unlock none", DungeonTickets.UnlockStage(6), -1, 0);
        Same("unlock zero", DungeonTickets.UnlockStage(0), -1, 0);

        // ── 오류 경로 ──
        try { GameBalance.Const("nope"); Console.WriteLine("FAIL missing const did not throw"); fails++; }
        catch (KeyNotFoundException) { checks++; }

        // ── 꼬마 ──
        string[] oldIds = { "leaf", "dew", "wind", "fire", "rock", "star", "coin", "chest", "beak" };
        int[] oldGrades = { 0, 0, 1, 2, 1, 3, 1, 2, 4 };
        double[] oldMuls = { 0.8, 0.8, 0.9, 1.0, 0.9, 1.1, 0.9, 1.0, 1.2 };
        SameStr("mascot ids", string.Join(",", Formation.Ids), string.Join(",", oldIds));
        for (int i = 0; i < oldIds.Length; i++)
        {
            Same($"grade {oldIds[i]}", Formation.Grade(oldIds[i]), oldGrades[i], 0);
            for (int lv = 1; lv <= 100; lv += 9)
                Same($"mul {oldIds[i]} {lv}", Formation.AttackMultiplierAt(oldIds[i], lv), oldMuls[i] * (1 + 0.05 * (lv - 1)), 1e-12);
        }
        Same("ggoma maxLevel", Formation.MaxLevel, 100, 0);
        SameStr("grade-2 ids", string.Join(",", Formation.IdsOfGrade(2)), "fire,chest");

        // ── 장비·뽑기·기타 ──
        double[] ownBase = { 2, 5, 15, 40, 100 };
        for (int i = 0; i < 15; i++)
        {
            int g = i / 3, sub = i % 3;
            double from = ownBase[g], to = g + 1 < 5 ? ownBase[g + 1] : from * 2.5;
            Same($"basePct {i}", Equipment.BaseOwnPercent(i), from * Math.Pow(to / from, sub / 3.0), 1e-12);
        }
        for (int lv = 1; lv < 100; lv++) Same($"copies {lv}", Equipment.CopiesToNext(lv), lv + 1, 0);
        Same("equip max", Equipment.MaxLevel, 100, 0); Same("equip bonus", Equipment.LevelBonus, 0.2, 0); Same("equip ratio", Equipment.EquipRatio, 3, 0);
        Same("gacha max", GachaProgress.MaxLevel, 10, 0);
        for (int g = 0; g < 5; g++) Same($"ownBase {g}", GachaProgress.OwnBase(g), ownBase[g], 0);
        for (int lv = 1; lv <= 10; lv++)
        {
            Same($"expToNext {lv}", GachaProgress.ExpToNext(lv), lv >= 10 ? 0 : 100 * lv, 0);
            int step = lv - 1;
            float legend = 0.5f + 0.5f * step, epic = 3f + 1.5f * step, rare = 12f + 2f * step, uncommon = 30f;
            float[] want = { 100f - legend - epic - rare - uncommon, uncommon, rare, epic, legend };
            var got = GachaProgress.Odds(lv);
            float sum = 0;
            for (int k = 0; k < 5; k++) { Same($"odds {lv}/{k}", got[k], want[k], 1e-6); sum += got[k]; }
            Same($"odds sum {lv}", sum, 100, 1e-6);
        }
        Same("odds clamp", GachaProgress.Odds(99)[4], 5.0, 1e-6);
        Same("dungeon", DungeonTickets.PerDay, 2, 0);
        Same("offline cap", IdleAway.OfflineCapHours, 8, 0); Same("offline min", IdleAway.OfflineMinSeconds, 60, 0);
        Same("power", CombatPower.OffenseSeconds, 10, 0); Same("power2", CombatPower.RegenSeconds, 30, 0);

        Console.WriteLine($"checks={checks} fails={fails} maxRelDiff={maxRel:E2}");
        return fails == 0 && vi.Count == 0 && real == 0 ? 0 : 1;
    }
}
