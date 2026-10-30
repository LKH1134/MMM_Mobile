using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace MoreMush.Mobile.EditorTools
{
    // 통합 표의 편집 계약. 런타임 수치 전환 없이, 에디터 임포트 단계에서 구조/참조를 검사한다.
    public static class GameBalanceValidator
    {
        public const string WorkbookName = "GameBalance";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Dictionary<string, string> Schemas = new Dictionary<string, string>
        {
            { "적", "index:int key:string hpCoefficient:double resourceId:int" },
            { "보스", "index:int key:string name:string hpCoefficient:double resourceId:int" },
            { "꼬마", "index:int key:string name:string grade:int attackMultiplier:double initialOwned:int defaultSlot:int resourceId:int" },
            { "업그레이드", "index:int key:string stat:IdleStat name:string baseValue:double perLevel:double valueMode:string price:long priceExponent:double priceDivisor:double priceScale:double maxLevel:int currency:Currency unlockStat:IdleStat unlockLevel:int resourceId:int capEvery:int capStep:int" },
            { "스테이지", "index:int key:string stage:int stageCoefficient:double enemyIds:int[] bossId:int wavesPerStage:int bossTime:double enemyCountBase:int enemyCountPerWave:int enemyCountPerStage:int enemyCountMax:int enemyGold:long bossGold:long bossMycelite:long enemyAttack:double bossAttack:double" },
            { "리소스", "index:int key:string type:string path:string" },
        };

        sealed class Row
        {
            public string sheet;
            public int line;
            public readonly Dictionary<string, string> cells = new Dictionary<string, string>();
            public string Text(string col) => cells.TryGetValue(col, out var s) ? s : "";
            public double Number(string col) => double.TryParse(Text(col), NumberStyles.Float, Inv, out var d) ? d : double.NaN;
            public int Int(string col) => int.TryParse(Text(col), NumberStyles.Integer, Inv, out var n) ? n : (int)NumberOrZero(col);
            double NumberOrZero(string col) => double.IsNaN(Number(col)) ? 0 : Number(col);
            public IEnumerable<int> Ids(string col) => Text(col).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), NumberStyles.Integer, Inv, out var n) ? n : 0);
            public string At(string col) => $"[{sheet}] {line}행 {col}";
        }

        // 문제 있는 통합 표는 bytes를 덮어쓰지 않는다. 테스트에서도 파일/저장값 변경 없이 호출 가능.
        public static int Validate(byte[] bytes, List<string> issues, bool checkAssets = true)
        {
            Dictionary<string, List<string[]>> sheets;
            try { sheets = XlsxReader.Read(bytes); }
            catch (Exception e) { issues.Add("통합 xlsx를 읽지 못했다: " + e.Message); return 0; }

            var tables = new Dictionary<string, List<Row>>();
            foreach (var schema in Schemas)
                tables[schema.Key] = Read(schema.Key, schema.Value, sheets, issues);
            if (!sheets.TryGetValue("info", out var info) || info.Count < 4)
                issues.Add("[info] 사용법 시트가 없거나 비어 있다");
            foreach (var name in sheets.Keys)
                if (name != "info" && !Schemas.ContainsKey(name)) issues.Add($"모르는 시트 '{name}'");
            // 구조/자료형/ID가 깨졌으면 잘못된 인덱스로 인한 연쇄 참조 오류를 만들지 않는다.
            int count = tables.Values.Sum(t => t.Count);
            if (issues.Count > 0) return count;

            var resources = Index(tables["리소스"]);
            var enemies = Index(tables["적"]);
            var bosses = Index(tables["보스"]);
            var resourcePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in tables["리소스"])
            {
                string path = r.Text("path");
                if (!resourcePaths.Add(path)) issues.Add(r.At("path") + ": 같은 경로가 중복 등록되어 있다 (기존 resourceId를 공유한다)");
                if (path.StartsWith("/") || path.Contains("\\") || path.Contains("..") || path.Contains(":")
                    || path.StartsWith("Assets/") || path.StartsWith("Resources/") || System.IO.Path.HasExtension(path))
                    issues.Add(r.At("path") + ": Resources 상대 경로를 확장자 없이 적는다");
                string type = r.Text("type");
                if (type != "Sprite" && type != "Prefab") issues.Add(r.At("type") + ": Sprite 또는 Prefab이어야 한다");
                else if (checkAssets)
                {
                    UnityEngine.Object asset = type == "Sprite" ? (UnityEngine.Object)Resources.Load<Sprite>(path) : Resources.Load<GameObject>(path);
                    if (asset == null) issues.Add(r.At("path") + $": {type} 리소스 '{path}'를 찾지 못했다");
                }
            }

            foreach (string name in new[] { "적", "보스", "꼬마", "업그레이드" })
                foreach (var r in tables[name]) Reference(r, "resourceId", resources, issues);
            foreach (var r in tables["적"].Concat(tables["보스"])) Positive(r, "hpCoefficient", issues);

            var slots = new HashSet<int>();
            foreach (var r in tables["꼬마"])
            {
                Positive(r, "attackMultiplier", issues);
                Between(r, "grade", 0, GachaProgress.GradeCount - 1, issues);
                Between(r, "initialOwned", 0, int.MaxValue, issues);
                Between(r, "defaultSlot", -1, Formation.SlotCount - 1, issues);
                int slot = r.Int("defaultSlot");
                if (slot >= 0 && !slots.Add(slot)) issues.Add(r.At("defaultSlot") + ": 기본 편성 칸이 겹친다");
                if (slot >= 0 && r.Number("initialOwned") < 1) issues.Add(r.At("initialOwned") + ": 기본 편성 꼬마는 1장 이상 필요하다");
            }

            var stats = new HashSet<string>();
            var upgrades = tables["업그레이드"];
            foreach (var r in upgrades)
            {
                if (!stats.Add(r.Text("stat"))) issues.Add(r.At("stat") + ": 강화 종류가 겹친다");
                foreach (string col in new[] { "price", "priceDivisor", "maxLevel" }) Positive(r, col, issues);
                foreach (string col in new[] { "baseValue", "perLevel", "priceExponent", "priceScale", "unlockLevel", "capEvery", "capStep" }) Between(r, col, 0, double.MaxValue, issues);
                if (r.Int("capEvery") > 0 && (r.Int("capStep") <= 0 || r.Int("unlockLevel") <= 0)) issues.Add(r.At("capEvery") + ": 단계 제한은 capStep과 unlockLevel이 0보다 커야 한다");
                if (r.Text("valueMode") != "Add" && r.Text("valueMode") != "Multiply") issues.Add(r.At("valueMode") + ": Add 또는 Multiply이어야 한다");
                if (r.Text("currency") != "Gold") issues.Add(r.At("currency") + ": 현재 강화는 Gold를 사용한다");
            }
            foreach (IdleStat stat in Enum.GetValues(typeof(IdleStat)))
                if (!stats.Contains(stat.ToString())) issues.Add($"[업그레이드] {stat} 행이 없다");
            foreach (var r in upgrades)
            {
                if (r.Int("unlockLevel") <= 0) continue;
                var parent = upgrades.Find(x => x.Text("stat") == r.Text("unlockStat"));
                if (parent == null || r.Int("unlockLevel") > parent.Int("maxLevel")) issues.Add(r.At("unlockLevel") + ": 해금에 필요한 강화 레벨에 도달할 수 없다");
                var seen = new HashSet<string> { r.Text("stat") };
                var next = r;
                while (next != null && next.Int("unlockLevel") > 0)
                {
                    if (!seen.Add(next.Text("unlockStat"))) { issues.Add(r.At("unlockStat") + ": 해금 조건이 순환한다"); break; }
                    next = upgrades.Find(x => x.Text("stat") == next.Text("unlockStat"));
                }
            }

            var stages = new HashSet<int>();
            foreach (var r in tables["스테이지"])
            {
                foreach (string col in new[] { "stage", "stageCoefficient", "wavesPerStage", "bossTime", "enemyCountBase", "enemyCountMax", "enemyGold", "bossGold" }) Positive(r, col, issues);
                foreach (string col in new[] { "enemyCountPerWave", "enemyCountPerStage", "bossMycelite", "enemyAttack", "bossAttack" }) Between(r, col, 0, double.MaxValue, issues);
                if (!stages.Add(r.Int("stage"))) issues.Add(r.At("stage") + ": 스테이지 번호가 겹친다");
                if (r.Int("enemyCountBase") > r.Int("enemyCountMax")) issues.Add(r.At("enemyCountMax") + ": 시작 적 수보다 작다");
                Reference(r, "bossId", bosses, issues);
                var ids = r.Ids("enemyIds").ToArray();
                if (ids.Length == 0 || ids.Distinct().Count() != ids.Length) issues.Add(r.At("enemyIds") + ": 비어 있거나 중복된 적 ID가 있다");
                foreach (int id in ids) if (!enemies.ContainsKey(id)) issues.Add(r.At("enemyIds") + $": 적 {id}가 없다");
            }
            if (stages.Count > 0 && (stages.Min() != 1 || stages.Max() != stages.Count)) issues.Add("[스테이지] stage는 1부터 빠짐없이 이어져야 한다");
            return count;
        }

        static List<Row> Read(string name, string schema, Dictionary<string, List<string[]>> sheets, List<string> issues)
        {
            var rows = new List<Row>();
            if (!sheets.TryGetValue(name, out var sheet) || sheet.Count < 4) { issues.Add($"[{name}] 데이터 시트가 없거나 비어 있다"); return rows; }
            var expected = schema.Split(' ').Select(s => s.Split(':')).ToDictionary(s => s[0], s => s[1]);
            var columns = new Dictionary<string, int>();
            for (int c = 0; c < sheet[0].Length; c++)
            {
                string field = sheet[0][c].Trim();
                if (field.Length == 0 || field.StartsWith("#")) continue;
                if (columns.ContainsKey(field)) { issues.Add($"[{name}] 열 {field}가 겹친다"); continue; }
                columns.Add(field, c);
                if (!expected.TryGetValue(field, out var type)) issues.Add($"[{name}] 모르는 열 {field} (메모 열은 #으로 시작)");
                else
                {
                    if (Cell(sheet[1], c) != type) issues.Add($"[{name}] {field} 자료형은 {type}이어야 한다");
                    if (string.IsNullOrWhiteSpace(Cell(sheet[2], c))) issues.Add($"[{name}] {field}의 3행 설명이 없다");
                }
            }
            foreach (string field in expected.Keys)
                if (!columns.ContainsKey(field)) issues.Add($"[{name}] 필수 열 {field}가 없다");
            if (!columns.TryGetValue("index", out int indexCol)) return rows;
            var ids = new HashSet<int>();
            var keys = new HashSet<string>();
            for (int i = 3; i < sheet.Count; i++)
            {
                if (sheet[i].All(string.IsNullOrWhiteSpace)) continue;
                if (string.IsNullOrWhiteSpace(Cell(sheet[i], indexCol))) { issues.Add($"[{name}] {i + 1}행 index가 없다"); continue; }
                var row = new Row { sheet = name, line = i + 1 };
                foreach (var col in expected)
                {
                    string raw = columns.TryGetValue(col.Key, out int c) ? Cell(sheet[i], c) : "";
                    row.cells[col.Key] = raw;
                    if (!Valid(raw, col.Value)) issues.Add(row.At(col.Key) + $": '{raw}'는 {col.Value} 값이 아니다");
                }
                if (row.Int("index") <= 0 || !ids.Add(row.Int("index"))) issues.Add(row.At("index") + ": ID는 양의 정수이며 시트 내에서 고유해야 한다");
                if (!keys.Add(row.Text("key"))) issues.Add(row.At("key") + ": 식별자가 겹친다");
                rows.Add(row);
            }
            return rows;
        }

        static bool Valid(string raw, string type)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (type == "string") return true;
            if (type == "int[]") return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).All(s => int.TryParse(s.Trim(), out var n) && n > 0);
            if (type == "IdleStat") return Enum.TryParse<IdleStat>(raw, out var stat) && Enum.IsDefined(typeof(IdleStat), stat);
            if (type == "Currency") return Enum.TryParse<Currency>(raw, out var currency) && Enum.IsDefined(typeof(Currency), currency);
            if (!double.TryParse(raw, NumberStyles.Float, Inv, out double d) || double.IsNaN(d) || double.IsInfinity(d)) return false;
            if (type == "int") return d == Math.Floor(d) && d >= int.MinValue && d <= int.MaxValue;
            if (type == "long") return d == Math.Floor(d) && d >= -9e18 && d <= 9e18;
            return type == "double";
        }

        static string Cell(string[] row, int col) => col < row.Length ? row[col].Trim() : "";
        static Dictionary<int, Row> Index(List<Row> rows) => rows.GroupBy(r => r.Int("index")).ToDictionary(g => g.Key, g => g.First());
        static void Reference(Row row, string col, Dictionary<int, Row> target, List<string> issues)
        {
            if (!target.ContainsKey(row.Int(col))) issues.Add(row.At(col) + $": 참조한 ID {row.Text(col)}가 없다");
            else if (col == "resourceId" && target[row.Int(col)].Text("type") != "Sprite") issues.Add(row.At(col) + ": 현재 그림 컬럼은 Sprite 리소스를 참조해야 한다");
        }
        static void Positive(Row row, string col, List<string> issues)
        {
            if (!(row.Number(col) > 0)) issues.Add(row.At(col) + ": 0보다 커야 한다");
        }
        static void Between(Row row, string col, double min, double max, List<string> issues)
        {
            double n = row.Number(col);
            if (!(n >= min && n <= max)) issues.Add(row.At(col) + $": {min}~{max} 범위여야 한다");
        }
    }
}
