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
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Dictionary<string, string> Schemas = new Dictionary<string, string>
        {
            { "적", "index:int key:string name:string resourceId:int value:int requestCount:int" },
            { "보스", "index:int key:string name:string resourceId:int cell:int" },
            { "꼬마", "index:int key:string name:string attackMultiplier:double initialOwned:int defaultSlot:int resourceId:int favoriteIds:int[] bonusEffect:TreeEffect bonusValue:double evoStage:int evoResourceIds:int[] evoCell:int" },
            { "업그레이드", "index:int key:string stat:IdleStat name:string baseValue:double perLevel:double valueMode:string price:long priceExponent:double priceDivisor:double priceScale:double maxLevel:int currency:Currency unlockStat:IdleStat unlockLevel:int resourceId:int capEvery:int capStep:int valueExponent:double valueDivisor:double" },
            { "스테이지", "index:int key:string stage:int enemyIds:int[] bossId:int wavesPerStage:int bossTime:double enemyCountBase:int enemyCountPerWave:int enemyCountPerStage:int enemyCountMax:int enemyGold:long bossGold:long bossMycelite:long enemyAttack:double bossAttack:double enemyHp:double bossHp:double" },
            { "리소스", "index:int key:string type:string path:string" },
            { "상수", "index:int key:string value:double" },
            { "등급", "index:int key:string grade:int ownBase:double name:string" },
            { "뽑기", "index:int key:string level:int expToNext:int oddsNormal:double oddsRare:double oddsEpic:double oddsLegend:double" },
            { "던전", "index:int key:string dungeon:int unlockStage:int mode:string name:string scene:string implemented:bool baseStage:int stageStep:int rewardKills:int rewardMyc:int rewardGrowth:double hpMul:double atkMul:double" },
            { "퀘스트", "index:int key:string kind:string target:int goldKills:int mycelite:int" },
            { "가이드", "index:int key:string kind:string stat:string target:int mycelite:int" },
            { "가이드반복", "index:int key:string kind:string baseTarget:double perStage:double perLevel:double step:int mycelite:int" },
            { "세계수", "index:int key:string name:string stage:int evoExp:int evoGoldKills:int evoMycelite:int ringKills:double craftSlots:int rootSlots:int trunkSlots:int branchSlots:int crownSlots:int preferIds:int[] resourceId:int" },
            { "가호", "index:int key:string stage:int effect:TreeEffect value:double" },
            { "꼬마진화", "index:int key:string star:int mycelite:int needLevel:int" },
            { "꼬마스킬", "index:int key:string owner:string stage:int effect:GgomaSkillEffect value:double name:string" },
            { "호감도", "index:int key:string level:int need:int" },
            { "장식", "index:int key:string name:string owner:string grade:int zone:TreeZone effect:TreeEffect value:double resourceId:int" },
            { "장식등급", "index:int key:string name:string grade:int goldKills:int mycelite:int hours:double" },
            { "의뢰", "index:int key:string name:string period:string kind:string target:int exp:int desc:string" },
            { "장비", "index:int key:string name:string kind:GachaKind stat:EquipStat itemIndex:int grade:int sub:int resourceId:int" },
            { "뽑기상품", "index:int key:string name:string kind:GachaKind pullCount:int price:long currency:Currency dailyLimit:int requiresAd:bool" },
            { "방치전투", "index:int key:string formationSlots:int stagesPerChapter:int flightTime:double waveDelay:double enemyAttackDelay:double" },
            { "수확전투", "index:int key:string dungeon:int scope:string field:string dataType:string value:double" },
        };

        // 상수 시트의 필수 key. min 이상이어야 하고, integer면 정수만 허용한다 (게임이 정수로 읽는 값)
        static readonly (string key, double min, bool integer)[] RequiredConsts =
        {
            ("equipMaxLevel", 1, true), ("equipLevelBonus", 0, false), ("equipRatio", 0, false),
            ("equipTopGradeMul", 0.001, false), ("weaponEffectMul", 0.001, false),
            ("copiesBase", 0, true), ("copiesPerLevel", 0, true), ("ggomaGrowMycelite2", 0, true), ("ggomaGrowMycelite3", 0, true), ("ggomaCopiesBase", 0, true), ("ggomaCopiesPerLevel", 0, true),
            ("ggomaMaxLevel", 1, true), ("ggomaLevelBonus", 0, false),
            ("ggomaSplashRadius", 0, false), ("enemyToxicRate", 0, false), ("enemyMedicinalRate", 0, false), ("ggomaEvoLevel2", 1, true), ("ggomaEvoLevel3", 1, true), ("ggomaEvoMul2", 0.001, false), ("ggomaEvoMul3", 0.001, false), ("dungeonPerDay", 0, true),
            ("offlineCapHours", 0, false), ("offlineMinSeconds", 0, false), ("powerOffenseSeconds", 0, false), ("powerRegenSeconds", 0, false),
            ("levelsPerStage", 0, true), ("expSpeedPerStage", 0, false), ("expRegenRatio", 0, false), ("expCritChanceRatio", 0, false),
            ("expCritDamageRatio", 0, false), ("expAdvancedAttackRatio", 0, false), ("expNormalDamageDivisor", 1, true),
            ("expAttackMulPerStage", 0, false), ("expHpMulPerStage", 0, false),
            ("enemyHpHits", 0, false), ("enemyHpWaveGrowth", 0, false), ("bossKillSeconds", 0, false),
            ("enemyAtkHpRate", 0, false), ("enemyAtkRegenRate", 0, false), ("bossAtkHpRate", 0, false),
            ("goldBase", 0, false), ("goldStep", 0.001, false), ("goldExp", 0, false), ("bossGoldMul", 0, true),
            ("mycelBase", 0, true), ("mycelStageDiv", 1, true),
            ("adBoxIntervalHours", 0.001, false), ("adBoxStaySeconds", 1, false), ("adBoxGoldKills", 0, true), ("adBoxMycelite", 0, true),
            ("treeRingMax", 1, true), ("treeRingGrowth", 1, false), ("treeHarvestPerKill", 0, false),
            ("tributePreferMul", 0, false), ("giftFavoriteMul", 0, false), ("giftMaxCount", 1, true),
            ("requestPerDay", 0, true), ("requestAffinity", 0, true), ("requestGoldKills", 0, false), ("requestWeekGoal", 1, true), ("requestWeekMycelite", 0, true),
            ("decoFirstMycelite", 0, true), ("decoFirstAffinity", 0, true),
            ("craftRushSeconds", 1, false), ("craftRushPerDay", 0, true), ("treeQuestDaily", 1, true),
            ("dungeonAdPerDay", 0, true), ("dungeonAutoNextSeconds", 1, false), ("dungeonFailMul", 0, false),
            ("bossRushCount", 1, true), ("bossLvSeconds", 5, false), ("bossLvHpGrowth", 1, false), ("bossLvAtkGrowth", 1, false),
            ("bossLvSpeedGrowth", 1, false), ("bossLvDefPerLevel", 0, false), ("bossLvDefMax", 0, false),
            ("offlineGoldRate", 0, false), ("speedCostRampFromLv", 0, true), ("speedCostRamp", 0, false),
            ("tailEnemyHpGrowth", 1, false), ("tailBossHpGrowth", 1, false), ("tailAttackGrowth", 1, false), ("tailGoldGrowth", 1, false), ("tailWallMul", 1, false),
            ("wallBossEvery", 0, true), ("wallBossFrom", 0, true), ("wallBossHpMul", 1, false), ("wallBossLateFrom", 0, true), ("wallBossHpMulLate", 1, false),
            ("harvestHpRateExp", 0, false),
            ("harvestFarmRounds", 1, true), ("harvestCycleLength", 1, true), ("offlineRateMinSeconds", 0.001, false),
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
                if (type != "Sprite" && type != "Prefab" && type != "AudioClip") issues.Add(r.At("type") + ": Sprite·Prefab·AudioClip 중 하나여야 한다");
                else if (checkAssets)
                {
                    UnityEngine.Object asset = type == "Sprite" ? Resources.Load<Sprite>(path)
                        : type == "AudioClip" ? (UnityEngine.Object)Resources.Load<AudioClip>(path) : Resources.Load<GameObject>(path);
                    if (asset == null) issues.Add(r.At("path") + $": {type} 리소스 '{path}'를 찾지 못했다");
                }
            }

            foreach (string name in new[] { "적", "보스", "꼬마", "업그레이드", "세계수", "장식", "장비" })
                foreach (var r in tables[name]) Reference(r, "resourceId", resources, issues);

            // 보스 그림 조각: resourceId 시트에 "시트이름_cell" 조각이 있어야 한다. 같은 조각을 두 보스가 쓰면 안 된다
            var bossCells = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in tables["보스"])
            {
                if (!resources.TryGetValue(r.Int("resourceId"), out var res)) continue;
                string path = res.Text("path");
                string cell = path.Substring(path.LastIndexOf('/') + 1) + "_" + r.Int("cell");
                if (!bossCells.Add(cell)) issues.Add(r.At("cell") + $": 조각 '{cell}'을 다른 보스도 쓴다");
                if (checkAssets && !Resources.LoadAll<Sprite>(path).Any(s => s.name == cell)) issues.Add(r.At("cell") + $": '{path}'에 조각 '{cell}'이 없다");
            }

            var slots = new HashSet<int>();
            foreach (var r in tables["꼬마"])
            {
                Positive(r, "attackMultiplier", issues);
                Between(r, "evoCell", 0, Formation.EvoCellCount - 1, issues);
                var evo = r.Ids("evoResourceIds").ToList();
                if (evo.Count != Formation.EvoStageCount) issues.Add(r.At("evoResourceIds") + $": 진화 그림 시트는 {Formation.EvoStageCount}개여야 한다");
                foreach (int id in evo) if (!resources.ContainsKey(id)) issues.Add(r.At("evoResourceIds") + $": 리소스 {id}가 없다");
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
                if (mode != "Add" && mode != "Multiply" && mode != "Power" && mode != "Incremental") issues.Add(r.At("valueMode") + ": Add·Multiply·Power·Incremental 중 하나여야 한다");
                if (mode == "Incremental" && (!(r.Number("perLevel") > 0) || !(r.Number("valueDivisor") > 0))) issues.Add(r.At("valueDivisor") + ": Incremental은 perLevel·valueDivisor가 0보다 커야 한다");
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
                foreach (string col in new[] { "stage", "wavesPerStage", "bossTime", "enemyCountBase", "enemyCountMax", "enemyGold", "bossGold" }) Positive(r, col, issues);
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
            ValidateDungeons(tables["던전"], issues, checkAssets);
            ValidateQuests(tables["퀘스트"], issues);
            ValidateGuides(tables["가이드"], tables["가이드반복"], issues);
            ValidateWorldTree(tables, enemies, issues);
            ValidateImplementedDefinitions(tables, issues);
            return count;
        }

        // 새 시트는 기존 코드/프리팹의 정의를 기록한다. 실제 전투·상품 로직의 데이터 전환은 별도 작업이다.
        static void ValidateImplementedDefinitions(Dictionary<string, List<Row>> tables, List<string> issues)
        {
            var equipment = tables["장비"];
            // 종류마다: itemIndex 0부터 빈 번호 없이, 등급은 itemIndex 순서로 오름차순, 모든 등급에 1개 이상,
            // sub는 등급 안에서 0부터 이어진다 (등급마다 장비 수는 달라도 된다: 무기 4/3/2/1)
            foreach (var kind in new[] { GachaKind.Weapon, GachaKind.Accessory })
            {
                var rows = equipment.Where(r => r.Text("kind") == kind.ToString()).OrderBy(r => r.Int("itemIndex")).ToList();
                var perGrade = new int[GachaProgress.GradeCount];
                int prevGrade = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    int g = r.Int("grade");
                    if (r.Int("itemIndex") != i) { issues.Add(r.At("itemIndex") + $": {kind} itemIndex는 0부터 빈 번호·겹침 없이 이어져야 한다 ({i}이어야 함)"); continue; }
                    if (g < 0 || g >= GachaProgress.GradeCount) continue;
                    if (g < prevGrade) issues.Add(r.At("grade") + ": itemIndex 순서로 등급이 낮아지면 안 된다");
                    if (r.Int("sub") != perGrade[g]) issues.Add(r.At("sub") + $": 등급 안 단계는 0부터 이어져야 한다 ({perGrade[g]}이어야 함)");
                    perGrade[g]++;
                    prevGrade = g;
                }
                for (int g = 0; g < GachaProgress.GradeCount; g++)
                    if (perGrade[g] == 0) issues.Add($"[장비] {kind}에 {g}등급 장비가 없다 (그 등급을 뽑으면 아무것도 안 나온다)");
            }
            foreach (var r in equipment)
            {
                string kind = r.Text("kind");
                if (kind == nameof(GachaKind.Character)) issues.Add(r.At("kind") + ": 꼬마는 꼬마 시트에 정의한다");
                string stat = kind == nameof(GachaKind.Accessory) ? nameof(EquipStat.Hp) : nameof(EquipStat.Attack);
                if (r.Text("stat") != stat) issues.Add(r.At("stat") + $": 현재 {kind}의 능력치는 {stat}이다");
                Between(r, "itemIndex", 0, int.MaxValue, issues);
                Between(r, "grade", 0, GachaProgress.GradeCount - 1, issues);
                Between(r, "sub", 0, int.MaxValue, issues);
            }

            var products = new HashSet<string>();
            foreach (var r in tables["뽑기상품"])
            {
                Positive(r, "pullCount", issues);
                Between(r, "price", 0, 9e18, issues);
                Between(r, "dailyLimit", 0, int.MaxValue, issues);
                bool ad = IsTrue(r.Text("requiresAd"));
                if (ad && (r.Number("price") != 0 || r.Int("dailyLimit") <= 0)) issues.Add(r.At("price") + ": 광고 상품은 가격 0과 양의 일일 제한이 필요하다");
                if (!ad && r.Number("price") <= 0) issues.Add(r.At("price") + ": 유료 상품은 가격이 0보다 커야 한다");
                if (!products.Add(r.Text("kind") + ":" + r.Text("pullCount") + ":" + ad)) issues.Add(r.At("kind") + ": 같은 종류/횟수/광고 여부의 상품이 겹친다");
            }
            foreach (GachaKind kind in Enum.GetValues(typeof(GachaKind)))
            {
                var rows = tables["뽑기상품"].Where(r => r.Text("kind") == kind.ToString()).ToList();
                int ads = rows.Count(r => IsTrue(r.Text("requiresAd"))), paid = rows.Count - ads;
                // GachaPanel 버튼: 광고 1개 + 재화 2개 (횟수 적은 것 → 첫 버튼)
                if (ads != 1) issues.Add($"[뽑기상품] {kind} 광고 상품은 1개여야 한다 (지금 {ads})");
                if (paid != 2) issues.Add($"[뽑기상품] {kind} 재화 상품은 2개여야 한다 (지금 {paid})");
            }

            if (tables["방치전투"].Count != 1) issues.Add("[방치전투] 행이 하나여야 한다 (지금 " + tables["방치전투"].Count + ")");
            foreach (var r in tables["방치전투"])
            {
                if (r.Int("formationSlots") != Formation.SlotCount) issues.Add(r.At("formationSlots") + $": 편성 칸은 코드(Formation.SlotCount)와 같은 {Formation.SlotCount}이어야 한다");
                Positive(r, "stagesPerChapter", issues);
                Positive(r, "flightTime", issues);
                Between(r, "waveDelay", 0, double.MaxValue, issues);
                Between(r, "enemyAttackDelay", 0, double.MaxValue, issues);
            }

            var dungeons = new HashSet<int>(tables["던전"].Where(r => IsTrue(r.Text("implemented"))).Select(r => r.Int("dungeon")));
            var settings = new HashSet<string>();
            foreach (var r in tables["수확전투"])
            {
                if (!dungeons.Contains(r.Int("dungeon"))) issues.Add(r.At("dungeon") + ": 구현된 던전을 참조해야 한다");
                string scope = r.Text("scope"), field = r.Text("field");
                Type owner = scope == "RoundBase" ? typeof(RoundBase) : scope == "HarvestField" ? typeof(HarvestField)
                    : scope == "Harvester" ? typeof(Harvester) : scope == "Cluster" || scope == "Single" ? typeof(Mushroom)
                    : scope == "FieldInput" ? typeof(FieldInput) : scope == "SkillEffects" ? typeof(SkillEffects)
                    : scope == "FieldDevices" ? typeof(FieldDevices) : null;
                var member = owner?.GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                string expected = member?.FieldType == typeof(int) ? "int" : member?.FieldType == typeof(long) ? "long" : member?.FieldType == typeof(float) ? "float" : member?.FieldType == typeof(bool) ? "bool" : null;
                if (scope == "Bar" && (field == "width" || field == "height")) expected = "float";
                if (scope == "BossGimmicks" && (field == "splitAt0" || field == "splitAt1")) expected = "float";
                if (scope == "StageHud" && (field == "speed0" || field == "speed1" || field == "speed2")) expected = "float";
                if (expected == null) issues.Add(r.At("field") + ": 기록할 수 있는 수확 전투 수치 필드가 아니다");
                else if (r.Text("dataType") != expected) issues.Add(r.At("dataType") + $": 실제 필드는 {expected}이다");
                Between(r, "value", 0, double.MaxValue, issues);
                if ((expected == "int" || expected == "long" || expected == "bool") && r.Number("value") != Math.Floor(r.Number("value"))) issues.Add(r.At("value") + ": 정수여야 한다");
                if (expected == "bool") Between(r, "value", 0, 1, issues);
                if (scope == "BossGimmicks") Between(r, "value", 0.001, 1, issues);
                if (scope == "StageHud" || scope == "FieldDevices" || (scope == "SkillEffects" && field != "flashTime")) Positive(r, "value", issues);
                if (!settings.Add(r.Int("dungeon") + ":" + scope + "." + field)) issues.Add(r.At("field") + ": 같은 던전의 설정이 겹친다");
            }
            // 같은 씬을 쓰는 던전은 가장 앞 던전의 설정이 기준이다 (HarvestTuning) — 기준 던전만 빠짐없이 검사하고, 다른 던전은 바꿀 필드만 둔다
            var sceneOf = tables["던전"].ToDictionary(r => r.Int("dungeon"), r => r.Text("scene"));
            bool HasSettings(int d) => tables["수확전투"].Any(r => r.Int("dungeon") == d);
            foreach (int dungeon in dungeons.Where(d => HasSettings(d) && !dungeons.Any(o => o < d && sceneOf[o] == sceneOf[d] && HasSettings(o))))
            {
                foreach (var field in typeof(RoundBase).GetFields().Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float)))
                    if (!settings.Contains(dungeon + ":RoundBase." + field.Name)) issues.Add($"[수확전투] 던전 {dungeon} RoundBase.{field.Name}가 없다");
                foreach (var owner in new[] { typeof(SkillEffects), typeof(FieldDevices) })
                    foreach (var field in owner.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        .Where(f => Attribute.IsDefined(f, typeof(SerializeField)) && (f.FieldType == typeof(float) || f.FieldType == typeof(int))))
                        if (!settings.Contains(dungeon + ":" + owner.Name + "." + field.Name)) issues.Add($"[수확전투] 던전 {dungeon} {owner.Name}.{field.Name}가 없다");
                foreach (string key in new[] { "BossGimmicks.splitAt0", "BossGimmicks.splitAt1", "StageHud.speed0", "StageHud.speed1", "StageHud.speed2" })
                    if (!settings.Contains(dungeon + ":" + key)) issues.Add($"[수확전투] 던전 {dungeon} {key}가 없다");
                var split = tables["수확전투"].Where(r => r.Int("dungeon") == dungeon && r.Text("scope") == "BossGimmicks")
                    .GroupBy(r => r.Text("field")).ToDictionary(g => g.Key, g => g.First().Number("value"));
                if (split.TryGetValue("splitAt0", out double first) && split.TryGetValue("splitAt1", out double second) && first <= second)
                    issues.Add($"[수확전투] 던전 {dungeon} 분신 체력 기준은 splitAt0 > splitAt1이어야 한다");
            }
        }

        static bool IsTrue(string value) => value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

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
                foreach (string col in new[] { "evoExp", "evoGoldKills", "evoMycelite", "ringKills", "craftSlots", "rootSlots", "trunkSlots", "branchSlots", "crownSlots" })
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
            }
            if (stars.Count == 0 || stars.Min() != 0 || stars.Max() != stars.Count - 1) issues.Add("[꼬마진화] star는 0부터 빠짐없이 이어져야 한다");

            // 꼬마스킬: 주인 꼬마가 있어야 하고, 단계는 진화 1~3
            var mascotKeys = new HashSet<string>(tables["꼬마"].Select(r => r.Text("key")));
            foreach (var r in tables["꼬마스킬"])
            {
                if (!mascotKeys.Contains(r.Text("owner"))) issues.Add(r.At("owner") + ": 꼬마 " + r.Text("owner") + "가 없다");
                Between(r, "stage", 1, Formation.EvoStageCount, issues);
                Positive(r, "value", issues);
                if (string.IsNullOrWhiteSpace(r.Text("name"))) issues.Add(r.At("name") + ": 스킬 이름이 비어 있다");
            }

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

            // 의뢰: period daily/weekly, kind는 WorldTree.Quests가 세는 것만. 일일 행은 하루에 뽑는 수(treeQuestDaily) 이상
            var kinds = new HashSet<string> { "stage", "kill", "species", "dungeon", "daily" };
            int dailyRows = 0;
            foreach (var r in tables["의뢰"])
            {
                string period = r.Text("period"), kind = r.Text("kind");
                if (period != "daily" && period != "weekly") issues.Add(r.At("period") + ": daily 또는 weekly");
                if (!kinds.Contains(kind)) issues.Add(r.At("kind") + ": stage·kill·species·dungeon·daily 중 하나");
                if (kind == "daily" && period != "weekly") issues.Add(r.At("kind") + ": daily(일일 의뢰 받은 수)는 주간 의뢰에만 쓴다");
                Between(r, "target", 1, int.MaxValue, issues);
                Between(r, "exp", 0, int.MaxValue, issues);
                if (string.IsNullOrEmpty(r.Text("desc"))) issues.Add(r.At("desc") + ": 설명 글이 비었다");
                if (period == "daily") dailyRows++;
            }
            int perDay = tables["상수"].Where(r => r.Text("key") == "treeQuestDaily").Select(r => r.Int("value")).FirstOrDefault();
            if (dailyRows < perDay) issues.Add($"[의뢰] 일일 행 {dailyRows}개 < 하루에 뽑는 수 treeQuestDaily {perDay}");
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
            if (byKey.ContainsKey("enemyToxicRate") && byKey.ContainsKey("enemyMedicinalRate")
                && byKey["enemyToxicRate"].Number("value") + byKey["enemyMedicinalRate"].Number("value") > 1)
                issues.Add("[상수] enemyToxicRate + enemyMedicinalRate는 1 이하여야 한다 (나머지가 식용 버섯)");
            if (byKey.ContainsKey("ggomaCopiesBase") && byKey.ContainsKey("ggomaCopiesPerLevel")
                && byKey["ggomaCopiesBase"].Number("value") + byKey["ggomaCopiesPerLevel"].Number("value") < 1)
                issues.Add("[상수] ggomaCopiesBase + ggomaCopiesPerLevel은 1 이상이어야 한다");
            if (byKey.ContainsKey("copiesBase") && byKey.ContainsKey("copiesPerLevel")
                && byKey["copiesBase"].Number("value") + byKey["copiesPerLevel"].Number("value") < 1)
                issues.Add("[상수] copiesBase + copiesPerLevel은 1 이상이어야 한다 (Lv.1→2 조각이 0개가 되지 않게)");
        }

        // checkAssets = false: Unity 밖(밸런스 시뮬)에서 부를 때. 빌드 설정은 에디터 안에서만 읽을 수 있어 씬 검사를 건너뛴다
        static void ValidateDungeons(List<Row> rows, List<string> issues, bool checkAssets)
        {
            var seen = new HashSet<int>();
            foreach (var r in rows)
            {
                Between(r, "dungeon", 1, int.MaxValue, issues);
                Between(r, "unlockStage", 0, int.MaxValue, issues);
                foreach (var col in new[] { "baseStage", "stageStep", "rewardKills", "rewardMyc", "rewardGrowth" }) Between(r, col, 0, int.MaxValue, issues);
                foreach (var col in new[] { "hpMul", "atkMul" }) Between(r, col, 0.01, 1000, issues);
                if (!seen.Add(r.Int("dungeon"))) issues.Add(r.At("dungeon") + ": 던전 번호가 겹친다");
                string scene = r.Text("scene");
                bool implemented = IsTrue(r.Text("implemented"));
                if (!Enum.TryParse<DungeonMode>(r.Text("mode"), true, out var mode)) { issues.Add(r.At("mode") + ": Harvest·BossRush·BossLevel 중 하나"); continue; }
                if (mode != DungeonMode.Harvest)
                {
                    if (!string.IsNullOrEmpty(scene)) issues.Add(r.At("scene") + ": 방치 화면 던전(BossRush·BossLevel)은 씬을 비워 둔다");
                    continue;
                }
                if (implemented && (string.IsNullOrEmpty(scene) || checkAssets && !UnityEditor.EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/" + scene + ".unity")))
                    issues.Add(r.At("scene") + ": 구현된 던전은 빌드 설정에 포함된 씬이 필요하다");
                if (!implemented && !string.IsNullOrEmpty(scene)) issues.Add(r.At("scene") + ": 미구현 던전은 연결 씬을 비워 둔다");
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

        static void ValidateGuides(List<Row> fixedRows, List<Row> loopRows, List<string> issues)
        {
            foreach (var r in fixedRows)
            {
                if (!Enum.TryParse<GuideKind>(r.Text("kind"), true, out var kind))
                    issues.Add(r.At("kind") + $": 모르는 종류 '{r.Text("kind")}' ({string.Join("/", Enum.GetNames(typeof(GuideKind)))} 중 하나)");
                else if (kind == GuideKind.UpgradeLevel && !Enum.TryParse<IdleStat>(r.Text("stat"), true, out _))
                    issues.Add(r.At("stat") + $": UpgradeLevel은 강화 종류(IdleStat 이름)가 필요하다 ('{r.Text("stat")}')");
                Between(r, "target", 1, int.MaxValue, issues);
                Between(r, "mycelite", 0, int.MaxValue, issues);
            }
            foreach (var r in loopRows)
            {
                if (!Enum.TryParse<GuideKind>(r.Text("kind"), true, out var kind))
                    issues.Add(r.At("kind") + $": 모르는 종류 '{r.Text("kind")}'");
                else if (kind == GuideKind.UpgradeLevel || kind == GuideKind.OpenFormation)
                    issues.Add(r.At("kind") + ": 반복 목표에는 쓸 수 없는 종류다 (UpgradeLevel·OpenFormation은 고정 순서 전용)");
                Between(r, "baseTarget", 1, int.MaxValue, issues);
                Between(r, "perStage", 0, 1000, issues);
                Between(r, "perLevel", 0, 1000, issues);
                Between(r, "step", 1, 1000000, issues);
                Between(r, "mycelite", 0, int.MaxValue, issues);
            }
            if (loopRows.Count == 0) issues.Add("[가이드반복] 행이 하나도 없다 (고정 순서가 끝난 뒤 이어질 목표가 필요하다)");
        }

        static void ValidateGrades(List<Row> rows, List<string> issues)
        {
            var seen = new HashSet<int>();
            foreach (var r in rows)
            {
                Between(r, "grade", 0, GachaProgress.GradeCount - 1, issues);
                if (string.IsNullOrWhiteSpace(r.Text("name"))) issues.Add(r.At("name") + ": 등급 이름이 비어 있다");
                Positive(r, "ownBase", issues);
                if (!seen.Add(r.Int("grade"))) issues.Add(r.At("grade") + ": 등급 번호가 겹친다");
            }
            if (seen.Count != GachaProgress.GradeCount) issues.Add($"[등급] 0~{GachaProgress.GradeCount - 1}등급이 하나씩 있어야 한다 (지금 {seen.Count}행)");
        }

        static void ValidateGachaLevels(List<Row> rows, List<string> issues)
        {
            var levels = new HashSet<int>();
            string[] odds = { "oddsNormal", "oddsRare", "oddsEpic", "oddsLegend" };
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
                    bool emptyScene = name == "던전" && col.Key == "scene" && raw.Length == 0;
                    if (!emptyScene && !Valid(raw, col.Value)) issues.Add(row.At(col.Key) + $": '{raw}'는 {col.Value} 값이 아니다");
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
            if (type == "GgomaSkillEffect") return Enum.TryParse<GgomaSkillEffect>(raw, out var skill) && Enum.IsDefined(typeof(GgomaSkillEffect), skill);
            if (type == "TreeZone") return Enum.TryParse<TreeZone>(raw, out var zone) && Enum.IsDefined(typeof(TreeZone), zone);
            if (type == "GachaKind") return Enum.TryParse<GachaKind>(raw, out var kind) && Enum.IsDefined(typeof(GachaKind), kind);
            if (type == "EquipStat") return Enum.TryParse<EquipStat>(raw, out var equip) && Enum.IsDefined(typeof(EquipStat), equip);
            if (type == "bool") return bool.TryParse(raw, out _) || raw == "0" || raw == "1";
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
