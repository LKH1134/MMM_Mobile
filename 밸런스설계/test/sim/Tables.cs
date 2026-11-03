using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MoreMush.Mobile;
using MoreMush.Mobile.EditorTools;

// 표 로더: Unity 밖에서는 Resources가 안 되므로 GameBalance의 private static 필드를 리플렉션으로 채운다
// (밸런스설계/test/ref_harness/Program.cs 방식 그대로 + 퀘스트 시트 quests)
sealed class DungeonSpec { public int dungeon, unlockStage, baseStage, stageStep; public string currency = ""; public double hpMul, rewardGrowth; public long rewardKills, rewardMyc; }

static class Tables
{
    public static List<DungeonSpec> Dungeons = new List<DungeonSpec>();   // 던전 시트의 새 열(수치안: currency, baseStage, stageStep, hpMul, rewardKills, rewardMyc, rewardGrowth). 없으면 빈 목록
    public static bool HasConst(string key)
    {
        var d = (Dictionary<string, double>)typeof(GameBalance).GetField("consts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        return d.ContainsKey(key);
    }
    public static double ConstOr(string key, double def) => HasConst(key) ? GameBalance.Const(key) : def;

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

    public static void Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var vi = new List<string>();
        GameBalanceValidator.Validate(bytes, vi, false);
        if (vi.Count > 0) { Console.WriteLine("validator issues=" + vi.Count); foreach (var i in vi.Take(10)) Console.WriteLine("  " + i); }
        var sheets = XlsxReader.Read(bytes);
        var issues = new List<string>();
        Set("consts", Rows<ConstRow>(sheets, "상수", 9001, 9999, issues).ToDictionary(r => r.key, r => r.value));
        Set("grades", Rows<GradeRow>(sheets, "등급", 8101, 8199, issues).OrderBy(r => r.grade).ToArray());
        Set("gachaLevels", Rows<GachaLevelRow>(sheets, "뽑기", 8201, 8299, issues).OrderBy(r => r.level).ToArray());
        Set("upgrades", Rows<UpgradeRow>(sheets, "업그레이드", 4001, 4999, issues));
        Set("mascots", Rows<MascotRow>(sheets, "꼬마", 3001, 3999, issues));
        Set("stages", Rows<StageRow>(sheets, "스테이지", 5001, 6999, issues).OrderBy(r => r.stage).ToArray());
        Set("resources", Rows<ResourceRow>(sheets, "리소스", 7001, 7999, issues).ToDictionary(r => r.index, r => r.path));
        Set("dungeonUnlock", Rows<DungeonRow>(sheets, "던전", 9101, 9199, issues).ToDictionary(r => r.dungeon, r => r.unlockStage));
        Set("quests", Rows<QuestRow>(sheets, "퀘스트", 9201, 9299, issues));
        Set("loaded", true);
        Dungeons.Clear();
        {
            var sh = sheets["던전"];
            var h = sh[0].ToList();
            int ci(string n) => h.IndexOf(n);
            if (ci("baseStage") >= 0)
                for (int r = 3; r < sh.Count; r++)
                {
                    var c = sh[r];
                    if (c.Length <= ci("baseStage") || string.IsNullOrWhiteSpace(c[0])) continue;
                    double D(string n) => double.TryParse(c[ci(n)], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
                    Dungeons.Add(new DungeonSpec { dungeon = (int)D("dungeon"), unlockStage = (int)D("unlockStage"), baseStage = (int)D("baseStage"), stageStep = (int)D("stageStep"), currency = c[ci("currency")], hpMul = D("hpMul"), rewardKills = (long)D("rewardKills"), rewardMyc = (long)D("rewardMyc"), rewardGrowth = D("rewardGrowth") });
                }
        }
        int real = issues.Count(i => !(i.Contains("기본값으로 둔다")));
        if (real > 0) { Console.WriteLine("loader issues=" + real); foreach (var i in issues.Where(i => !i.Contains("기본값으로 둔다")).Take(10)) Console.WriteLine("  " + i); }
    }
}
