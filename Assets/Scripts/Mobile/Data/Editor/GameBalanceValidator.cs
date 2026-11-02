using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace MoreMush.Mobile.EditorTools
{
    // 통합 표의 편집 계약. 에디터 임포트 단계에서 구조·참조·수치 범위를 검사한다 (게임은 이 검사를 통과한 bytes만 읽는다, GameBalance).
    public static class GameBalanceValidator
    {
        public const string WorkbookName = "GameBalance";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Dictionary<string, string> Schemas = new Dictionary<string, string>
        {
            { "적", "index:int key:string name:string hpCoefficient:double resourceId:int value:int requestCount:int" },
            { "보스", "index:int key:string name:string hpCoefficient:double resourceId:int" },
            { "꼬마", "index:int key:string name:string grade:int attackMultiplier:double initialOwned:int defaultSlot:int resourceId:int favoriteIds:int[] bonusEffect:TreeEffect bonusValue:double evoStage:int" },
            { "업그레이드", "index:int key:string stat:IdleStat name:string baseValue:double perLevel:double valueMode:string price:long priceExponent:double priceDivisor:double priceScale:double maxLevel:int currency:Currency unlockStat:IdleStat unlockLevel:int resourceId:int capEvery:int capStep:int valueExponent:double valueDivisor:double" },
            { "스테이지", "index:int key:string stage:int stageCoefficient:double enemyIds:int[] bossId:int wavesPerStage:int bossTime:double enemyCountBase:int enemyCountPerWave:int enemyCountPerStage:int enemyCountMax:int enemyGold:long bossGold:long bossMycelite:long enemyAttack:double bossAttack:double enemyHp:double bossHp:double" },
            { "리소스", "index:int key:string type:string path:string" },
            { "상수", "index:int key:string value:double" },
            { "등급", "index:int key:string grade:int ownBase:double" },
            { "뽑기", "index:int key:string level:int expToNext:int oddsNormal:double oddsUncommon:double oddsRare:double oddsEpic:double oddsLegend:double" },
            { "던전", "index:int key:string dungeon:int unlockStage:int" },
            { "퀘스트", "index:int key:string kind:string target:int goldKills:int mycelite:int" },
            { "세계수", "index:int key:string name:string stage:int evoExp:int evoGoldKills:int evoMycelite:int ringKills:double bagKillsPerHour:double bagMycelitePerHour:double craftSlots:int rootSlots:int trunkSlots:int branchSlots:int crownSlots:int preferIds:int[] resourceId:int" },
            { "가호", "index:int key:string stage:int effect:TreeEffect value:double" },
            { "꼬마진화", "index:int key:string star:int mycelite:int needLevel:int bagMul:double" },
            { "호감도", "index:int key:string level:int need:int bagBonus:double" },
            { "장식", "index:int key:string name:string owner:string grade:int zone:TreeZone effect:TreeEffect value:double resourceId:int" },
            { "장식등급", "index:int key:string name:string grade:int goldKills:int mycelite:int hours:double" },
        };

        // 상수 시트의 필수 key. min 이상이어야 하고, integer면 정수만 허용한다 (게임이 정수로 읽는 값)
        static readonly (string key, double min, bool integer)[] RequiredConsts =
        {
            ("equipMaxLevel", 1, true), ("equipLevelBonus", 0, false), ("equipRatio", 0, false),
            ("equipSubWeight1", 0, false), ("equipSubWeight2", 0, false), ("equipSubWeight3", 0, false), ("equipTopGradeMul", 0.001, false),
            ("copiesBase", 0, true), ("copiesPerLevel", 0, true),
            ("ggomaMaxLevel", 1, true), ("ggomaLevelBonus", 0, false), ("dungeonPerDay", 0, true),
            ("offlineCapHours", 0, false), ("offlineMinSeconds", 0, false), ("powerOffenseSeconds", 0, false), ("powerRegenSeconds", 0, false),
            ("levelsPerStage", 0, true), ("expSpeedPerStage", 0, false), ("expRegenRatio", 0, false), ("expCritChanceRatio", 0, false),
            ("expCritDamageRatio", 0, false), ("expAdvancedAttackRatio", 0, false), ("expNormalDamageDivisor", 1, true),
            ("expAttackMulPerStage", 0, false), ("expHpMulPerStage", 0, false),
            ("enemyHpHits", 0, false), ("enemyHpWaveGrowth", 0, false), ("bossKillSeconds", 0, false),
            ("enemyAtkHpRate", 0, false), ("enemyAtkRegenRate", 0, false), ("bossAtkHpRate", 0, false),
            ("goldBase", 0, false), ("goldStep", 0.001, false), ("goldExp", 0, false), ("bossGoldMul", 0, true),
            ("mycelBase", 0, true), ("mycelStageDiv", 1, true),
            ("adBoxIntervalHours", 0.001, false), ("adBoxStaySeconds", 1, false), ("adBoxGoldKills", 0, true), ("adBoxMyceliteBosses", 0, true),
            ("treeRingMax", 1, true), ("treeRingGrowth", 1, false), ("treeRingBagBonus", 0, false), ("treeHarvestPerKill", 0, false),
            ("tributePreferMul", 0, false), ("giftFavoriteMul", 0, false), ("giftMaxCount", 1, true),
            ("requestPerDay", 0, true), ("requestAffinity", 0, true), ("requestGoldKills", 0, false), ("requestWeekGoal", 1, true), ("requestWeekMycelite", 0, true),
            ("kidBagShare", 0, false), ("decoSetBagMul", 0, false), ("decoFirstMycelite", 0, true), ("decoFirstAffinity", 0, true),
            ("craftRushSeconds", 1, false), ("craftRushPerDay", 0, true), ("bagCapHours", 0.001, false), ("kidEvoLevelGradeMul", 0.001, false),
            ("dungeonAdPerDay", 0, true), ("dungeonAutoNextSeconds", 1, false),
            ("offlineGoldRate", 0, false), ("adGachaPulls", 1, true),
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

            foreach (string name in new[] { "적", "보스", "꼬마", "업그레이드", "세계수", "장식" })
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
            for (int slot = 0; slot < Formation.SlotCount; slot++)
                if (!slots.Contains(slot)) issues.Add($"[꼬마] 기본 편성 칸 {slot}에 꼬마가 없다 (defaultSlot 0~{Formation.SlotCount - 1}을 모두 채운다)");

            var stats = new HashSet<string>();
            var upgrades = tables["업그레이드"];
            foreach (var r in upgrades)
            {
                if (!stats.Add(r.Text("stat"))) issues.Add(r.At("stat") + ": 강화 종류가 겹친다");
                foreach (string col in new[] { "price", "priceDivisor", "maxLevel" }) Positive(r, col, issues);
                foreach (string col in new[] { "baseValue", "perLevel", "priceExponent", "priceScale", "unlockLevel", "capEvery", "capStep" }) Between(r, col, 0, double.MaxValue, issues);
                if (r.Int("capEvery") > 0 && (r.Int("capStep") <= 0 || r.Int("unlockLevel") <= 0)) issues.Add(r.At("capEvery") + ": 단계 제한은 capStep과 unlockLevel이 0보다 커야 한다");
                string mode = r.Text("valueMode");
                if (mode != "Add" && mode != "Multiply" && mode != "Power") issues.Add(r.At("valueMode") + ": Add·Multiply·Power 중 하나여야 한다");
                if (mode == "Power" && (!(r.Number("valueExponent") > 0) || !(r.Number("valueDivisor") > 0))) issues.Add(r.At("valueExponent") + ": Power는 valueExponent·valueDivisor가 0보다 커야 한다");
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
                foreach (string col in new[] { "enemyCountPerWave", "enemyCountPerStage", "bossMycelite", "enemyAttack", "bossAttack", "enemyHp", "bossHp" }) Between(r, col, 0, double.MaxValue, issues);
                if (!stages.Add(r.Int("stage"))) issues.Add(r.At("stage") + ": 스테이지 번호가 겹친다");
                if (r.Int("enemyCountBase") > r.Int("enemyCountMax")) issues.Add(r.At("enemyCountMax") + ": 시작 적 수보다 작다");
                Reference(r, "bossId", bosses, issues);
                var ids = r.Ids("enemyIds").ToArray();
                if (ids.Length == 0 || ids.Distinct().Count() != ids.Length) issues.Add(r.At("enemyIds") + ": 비어 있거나 중복된 적 ID가 있다");
                foreach (int id in ids) if (!enemies.ContainsKey(id)) issues.Add(r.At("enemyIds") + $": 적 {id}가 없다");
            }
            if (stages.Count > 0 && (stages.Min() != 1 || stages.Max() != stages.Count)) issues.Add("[스테이지] stage는 1부터 빠짐없이 이어져야 한다");

            ValidateConsts(tables["상수"], issues);
            ValidateGrades(tables["등급"], issues);
            ValidateGachaLevels(tables["뽑기"], issues);
            ValidateDungeons(tables["던전"], issues);
            ValidateQuests(tables["퀘스트"], issues);
            ValidateWorldTree(tables, enemies, issues);
            return count;
        }

        // 세계수: 단계는 1부터 이어지고, 가호는 있는 단계에만, 버섯·꼬마·등급 참조가 있어야 한다
        static void ValidateWorldTree(Dictionary<string, List<Row>> tables, Dictionary<int, Row> enemies, List<string> issues)
        {
            foreach (var r in tables["적"]) { Between(r, "value", 0, int.MaxValue, issues); Between(r, "requestCount", 1, int.MaxValue, issues); }
            foreach (var r in tables["꼬마"])
            {
                Between(r, "evoStage", 1, int.MaxValue, issues);
                foreach (int id in r.Ids("favoriteIds")) if (!enemies.ContainsKey(id)) issues.Add(r.At("favoriteIds") + $": 적 {id}가 없다");
            }

            var stages = new HashSet<int>();
            foreach (var r in tables["세계수"])
            {
                if (!stages.Add(r.Int("stage"))) issues.Add(r.At("stage") + ": 세계수 단계가 겹친다");
                foreach (string col in new[] { "evoExp", "evoGoldKills", "evoMycelite", "ringKills", "bagKillsPerHour", "bagMycelitePerHour", "craftSlots", "rootSlots", "trunkSlots", "branchSlots", "crownSlots" })
                    Between(r, col, 0, double.MaxValue, issues);
                foreach (int id in r.Ids("preferIds")) if (!enemies.ContainsKey(id)) issues.Add(r.At("preferIds") + $": 적 {id}가 없다");
            }
            if (stages.Count == 0) issues.Add("[세계수] 행이 하나도 없다");
            else if (stages.Min() != 1 || stages.Max() != stages.Count) issues.Add("[세계수] stage는 1부터 빠짐없이 이어져야 한다");
            foreach (var r in tables["세계수"])
                if (r.Int("stage") < stages.Count && r.Int("evoExp") <= 0) issues.Add(r.At("evoExp") + ": 마지막 단계가 아니면 0보다 커야 한다");
            foreach (var r in tables["가호"])
                if (!stages.Contains(r.Int("stage"))) issues.Add(r.At("stage") + ": 세계수 시트에 없는 단계다");

            var stars = new HashSet<int>();
            foreach (var r in tables["꼬마진화"])
            {
                if (!stars.Add(r.Int("star"))) issues.Add(r.At("star") + ": ★ 단계가 겹친다");
                Between(r, "mycelite", 0, int.MaxValue, issues);
                Between(r, "needLevel", 1, int.MaxValue, issues);
                Positive(r, "bagMul", issues);
            }
            if (stars.Count == 0 || stars.Min() != 0 || stars.Max() != stars.Count - 1) issues.Add("[꼬마진화] star는 0부터 빠짐없이 이어져야 한다");

            var levels = new HashSet<int>();
            foreach (var r in tables["호감도"])
            {
                if (!levels.Add(r.Int("level"))) issues.Add(r.At("level") + ": 호감도 레벨이 겹친다");
                Between(r, "need", 0, int.MaxValue, issues);
            }
            if (levels.Count == 0 || levels.Min() != 1 || levels.Max() != levels.Count) issues.Add("[호감도] level은 1부터 빠짐없이 이어져야 한다");
            foreach (var r in tables["호감도"])
                if (r.Int("level") < levels.Count && r.Int("need") <= 0) issues.Add(r.At("need") + ": 최고 레벨이 아니면 0보다 커야 한다");

            var grades = new HashSet<int>();
            foreach (var r in tables["장식등급"])
            {
                if (!grades.Add(r.Int("grade"))) issues.Add(r.At("grade") + ": 장식 등급이 겹친다");
                Between(r, "goldKills", 0, int.MaxValue, issues);
                Between(r, "mycelite", 0, int.MaxValue, issues);
                Positive(r, "hours", issues);
            }
            var kids = new HashSet<string>(tables["꼬마"].Select(r => r.Text("key")));
            foreach (var r in tables["장식"])
            {
                if (!kids.Contains(r.Text("owner"))) issues.Add(r.At("owner") + ": 꼬마 " + r.Text("owner") + "가 없다");
                if (!grades.Contains(r.Int("grade"))) issues.Add(r.At("grade") + ": 장식등급 시트에 없는 등급이다");
            }
        }

        static void ValidateConsts(List<Row> rows, List<string> issues)
        {
            var byKey = rows.ToDictionary(r => r.Text("key"));
            var known = new HashSet<string>(RequiredConsts.Select(c => c.key));
            foreach (var r in rows)
                if (!known.Contains(r.Text("key"))) issues.Add(r.At("key") + $": 모르는 상수 '{r.Text("key")}' (게임이 읽지 않는다)");
            foreach (var (key, min, integer) in RequiredConsts)
            {
                if (!byKey.TryGetValue(key, out var r)) { issues.Add($"[상수] '{key}' 행이 없다"); continue; }
                double v = r.Number("value");
                if (!(v >= min)) issues.Add(r.At("value") + $": {key}는 {min} 이상이어야 한다");
                if (integer && v != Math.Floor(v)) issues.Add(r.At("value") + $": {key}는 정수여야 한다");
            }
            if (byKey.ContainsKey("equipSubWeight1") && byKey.ContainsKey("equipSubWeight2") && byKey.ContainsKey("equipSubWeight3"))
            {
                double sum = byKey["equipSubWeight1"].Number("value") + byKey["equipSubWeight2"].Number("value") + byKey["equipSubWeight3"].Number("value");
                if (Math.Abs(sum - 1) > 1e-6) issues.Add($"[상수] equipSubWeight1~3의 합은 1이어야 한다 (지금 {sum})");
            }
            if (byKey.ContainsKey("copiesBase") && byKey.ContainsKey("copiesPerLevel")
                && byKey["copiesBase"].Number("value") + byKey["copiesPerLevel"].Number("value") < 1)
                issues.Add("[상수] copiesBase + copiesPerLevel은 1 이상이어야 한다 (Lv.1→2 조각이 0개가 되지 않게)");
        }

        static void ValidateDungeons(List<Row> rows, List<string> issues)
        {
            var seen = new HashSet<int>();
            foreach (var r in rows)
            {
                Between(r, "dungeon", 1, int.MaxValue, issues);
                Between(r, "unlockStage", 0, int.MaxValue, issues);
                if (!seen.Add(r.Int("dungeon"))) issues.Add(r.At("dungeon") + ": 던전 번호가 겹친다");
            }
            if (seen.Count == 0) issues.Add("[던전] 행이 하나도 없다");
        }

        static void ValidateQuests(List<Row> rows, List<string> issues)
        {
            foreach (var r in rows)
            {
                if (!Enum.TryParse<QuestKind>(r.Text("kind"), true, out _))
                    issues.Add(r.At("kind") + $": 모르는 종류 '{r.Text("kind")}' ({string.Join("/", Enum.GetNames(typeof(QuestKind)))} 중 하나)");
                Between(r, "target", 1, int.MaxValue, issues);
                Between(r, "goldKills", 0, int.MaxValue, issues);
                Between(r, "mycelite", 0, int.MaxValue, issues);
            }
            if (rows.Count == 0) issues.Add("[퀘스트] 행이 하나도 없다");
        }

        static void ValidateGrades(List<Row> rows, List<string> issues)
        {
            var seen = new HashSet<int>();
            foreach (var r in rows)
            {
                Between(r, "grade", 0, GachaProgress.GradeCount - 1, issues);
                Positive(r, "ownBase", issues);
                if (!seen.Add(r.Int("grade"))) issues.Add(r.At("grade") + ": 등급 번호가 겹친다");
            }
            if (seen.Count != GachaProgress.GradeCount) issues.Add($"[등급] 0~{GachaProgress.GradeCount - 1}등급이 하나씩 있어야 한다 (지금 {seen.Count}행)");
        }

        static void ValidateGachaLevels(List<Row> rows, List<string> issues)
        {
            var levels = new HashSet<int>();
            string[] odds = { "oddsNormal", "oddsUncommon", "oddsRare", "oddsEpic", "oddsLegend" };
            foreach (var r in rows)
            {
                foreach (string col in odds) Between(r, col, 0, 100, issues);
                double sum = odds.Sum(r.Number);
                if (Math.Abs(sum - 100) > 0.001) issues.Add(r.At("oddsNormal") + $": 확률 합이 100이어야 한다 (지금 {sum})");
                Between(r, "expToNext", 0, int.MaxValue, issues);
                if (!levels.Add(r.Int("level"))) issues.Add(r.At("level") + ": 뽑기 레벨이 겹친다");
            }
            if (levels.Count == 0) return;
            if (levels.Min() != 1 || levels.Max() != levels.Count) issues.Add("[뽑기] level은 1부터 빠짐없이 이어져야 한다");
            foreach (var r in rows)
            {
                bool last = r.Int("level") == levels.Count;
                if (last && r.Int("expToNext") != 0) issues.Add(r.At("expToNext") + ": 최고 레벨은 0이어야 한다 (더 오르지 않는다)");
                if (!last && r.Int("expToNext") <= 0) issues.Add(r.At("expToNext") + ": 0보다 커야 한다 (최고 레벨만 0)");
            }
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
            if (type == "TreeEffect") return Enum.TryParse<TreeEffect>(raw, out var effect) && Enum.IsDefined(typeof(TreeEffect), effect);
            if (type == "TreeZone") return Enum.TryParse<TreeZone>(raw, out var zone) && Enum.IsDefined(typeof(TreeZone), zone);
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
