using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 방치형 통합 표 GameBalance.xlsx(→ Resources/Tables/GameBalance.bytes)를 처음 쓸 때 한 번 읽는다.
    // 강화(IdleProgress)·꼬마(Formation)·장비(Equipment)·뽑기(GachaProgress)·던전 횟수·앱 밖 보상·전투력의 수치가 여기서 나온다.
    // 표가 없거나 필수 시트가 비면 숫자를 지어내지 않고 예외를 던진다 (임포터가 구조를 검증한 표만 bytes로 복사한다).
    // 읽는 시점은 처음 쓸 때다 — SubsystemRegistration에 도는 Reload 훅이 이 클래스를 건드려도 리소스를 읽지 않는다.
    // 적·보스의 hpCoefficient, 스테이지의 stageCoefficient·enemyIds·bossId는 아직 게임이 읽지 않는다 (체력은 IdleProgress 수식).
    public static class GameBalance
    {
        public const string ResourceName = "Tables/GameBalance";

        static bool loaded;
        static Dictionary<string, double> consts;
        static UpgradeRow[] upgrades;
        static MascotRow[] mascots;
        static StageRow[] stages;
        static GradeRow[] grades;
        static GachaLevelRow[] gachaLevels;
        static Dictionary<int, string> resources;
        static Dictionary<int, int> dungeonUnlock;
        static QuestRow[] quests;
        static EnemyRow[] enemies;
        static TreeStageRow[] treeStages;
        static BlessRow[] blessings;
        static KidStarRow[] kidStars;
        static AffinityRow[] affinityLevels;
        static DecoRow[] decos;
        static DecoGradeRow[] decoGrades;

        // 표 이름(= 시트 이름)과 index 범위
        const string UpgradeSheet = "업그레이드", MascotSheet = "꼬마", StageSheet = "스테이지", ResourceSheet = "리소스",
            ConstSheet = "상수", GradeSheet = "등급", GachaSheet = "뽑기", DungeonSheet = "던전", QuestSheet = "퀘스트",
            EnemySheet = "적", TreeSheet = "세계수", BlessSheet = "가호", KidStarSheet = "꼬마진화", AffinitySheet = "호감도",
            DecoSheet = "장식", DecoGradeSheet = "장식등급";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            loaded = false;
            loadError = null;
            consts = null;
            upgrades = null;
            mascots = null;
            stages = null;
            grades = null;
            gachaLevels = null;
            resources = null;
            dungeonUnlock = null;
            quests = null;
            enemies = null;
            treeStages = null;
            blessings = null;
            kidStars = null;
            affinityLevels = null;
            decos = null;
            decoGrades = null;
        }

        // ── 읽기 ──
        public static double Const(string key)
        {
            Ensure();
            if (consts.TryGetValue(key, out double v)) return v;
            throw new KeyNotFoundException($"[GameBalance] 상수 '{key}'가 상수 시트에 없다");
        }

        public static int ConstInt(string key) => (int)Math.Round(Const(key));

        public static IReadOnlyList<UpgradeRow> Upgrades { get { Ensure(); return upgrades; } }
        public static IReadOnlyList<MascotRow> Mascots { get { Ensure(); return mascots; } }
        public static IReadOnlyList<GradeRow> Grades { get { Ensure(); return grades; } }
        public static IReadOnlyList<GachaLevelRow> GachaLevels { get { Ensure(); return gachaLevels; } }
        public static int StageCount { get { Ensure(); return stages.Length; } }
        public static IReadOnlyList<QuestRow> Quests { get { Ensure(); return quests; } }
        public static IReadOnlyList<EnemyRow> Enemies { get { Ensure(); return enemies; } }
        public static IReadOnlyList<TreeStageRow> TreeStages { get { Ensure(); return treeStages; } }
        public static IReadOnlyList<BlessRow> Blessings { get { Ensure(); return blessings; } }
        public static IReadOnlyList<KidStarRow> KidStars { get { Ensure(); return kidStars; } }
        public static IReadOnlyList<AffinityRow> AffinityLevels { get { Ensure(); return affinityLevels; } }
        public static IReadOnlyList<DecoRow> Decos { get { Ensure(); return decos; } }
        public static IReadOnlyList<DecoGradeRow> DecoGrades { get { Ensure(); return decoGrades; } }

        // 스테이지 번호(1부터)의 행. 표 끝을 넘으면 마지막 행 (웨이브 수·적 수 같은 설정을 이어 쓴다). 표 안인지는 InTable로 본다
        public static StageRow Stage(int stage)
        {
            Ensure();
            return stages[Mathf.Clamp(stage, 1, stages.Length) - 1];
        }

        public static bool InStageTable(int stage)
        {
            Ensure();
            return stage >= 1 && stage <= stages.Length;
        }

        // 던전 번호 → 해금에 진입해야 하는 스테이지 (던전 시트). 행이 없으면 -1 (열리지 않는다)
        public static int DungeonUnlockStage(int dungeon)
        {
            Ensure();
            return dungeonUnlock.TryGetValue(dungeon, out int stage) ? stage : -1;
        }

        // 리소스.index → Resources 경로 (없으면 null)
        public static string ResourcePath(int id)
        {
            Ensure();
            return resources.TryGetValue(id, out var path) ? path : null;
        }

        // 읽기에 실패하면 그 오류를 기억해 둔다 — 매번 xlsx를 다시 풀면(매 프레임 호출) 기기가 멈춘 것처럼 된다. Reload로 다시 시도한다
        static Exception loadError;

        static void Ensure()
        {
            if (loaded) return;
            if (loadError != null) throw new InvalidOperationException("[GameBalance] 표를 읽지 못했다: " + loadError.Message, loadError);
            try { Load(); }
            catch (Exception e)
            {
                loadError = e;
                throw;
            }
        }

        // ── 읽는 곳 ──
        static void Load()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
                throw new InvalidOperationException($"[GameBalance] Resources/{ResourceName}.bytes가 없다 — 메뉴 MoreMush/Data/Reimport Tables로 만든다");
            byte[] bytes = asset.bytes;
            Resources.UnloadAsset(asset);

            Dictionary<string, List<string[]>> sheets;
            try { sheets = XlsxReader.Read(bytes); }
            catch (Exception e) { throw new InvalidOperationException("[GameBalance] xlsx를 읽지 못했다: " + e.Message, e); }

            var issues = new List<string>();
            var constRows = Read(new DataTable<ConstRow>(ConstSheet, 9001, 9999), sheets, issues);
            grades = ToSorted(Read(new DataTable<GradeRow>(GradeSheet, 8101, 8199), sheets, issues), (a, b) => a.grade.CompareTo(b.grade));
            gachaLevels = ToSorted(Read(new DataTable<GachaLevelRow>(GachaSheet, 8201, 8299), sheets, issues), (a, b) => a.level.CompareTo(b.level));
            upgrades = ToArray(Read(new DataTable<UpgradeRow>(UpgradeSheet, 4001, 4999), sheets, issues));
            mascots = ToArray(Read(new DataTable<MascotRow>(MascotSheet, 3001, 3999), sheets, issues));
            stages = ToSorted(Read(new DataTable<StageRow>(StageSheet, 5001, 6999), sheets, issues), (a, b) => a.stage.CompareTo(b.stage));
            var resourceRows = Read(new DataTable<ResourceRow>(ResourceSheet, 7001, 7999), sheets, issues);
            var dungeonRows = Read(new DataTable<DungeonRow>(DungeonSheet, 9101, 9199), sheets, issues);
            quests = ToArray(Read(new DataTable<QuestRow>(QuestSheet, 9201, 9299), sheets, issues));
            enemies = ToArray(Read(new DataTable<EnemyRow>(EnemySheet, 1001, 1999), sheets, issues));
            treeStages = ToSorted(Read(new DataTable<TreeStageRow>(TreeSheet, 9301, 9399), sheets, issues), (a, b) => a.stage.CompareTo(b.stage));
            blessings = ToSorted(Read(new DataTable<BlessRow>(BlessSheet, 9401, 9499), sheets, issues), (a, b) => a.stage.CompareTo(b.stage));
            kidStars = ToSorted(Read(new DataTable<KidStarRow>(KidStarSheet, 9501, 9599), sheets, issues), (a, b) => a.star.CompareTo(b.star));
            affinityLevels = ToSorted(Read(new DataTable<AffinityRow>(AffinitySheet, 9601, 9699), sheets, issues), (a, b) => a.level.CompareTo(b.level));
            decos = ToArray(Read(new DataTable<DecoRow>(DecoSheet, 9701, 9799), sheets, issues));
            decoGrades = ToSorted(Read(new DataTable<DecoGradeRow>(DecoGradeSheet, 9801, 9899), sheets, issues), (a, b) => a.grade.CompareTo(b.grade));

            foreach (var issue in issues)
                if (!Ignorable(issue)) Debug.LogError("[GameBalance] " + issue);

            consts = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var row in constRows) consts[row.key] = row.value;
            resources = new Dictionary<int, string>();
            foreach (var row in resourceRows) resources[row.index] = row.path;
            dungeonUnlock = new Dictionary<int, int>();
            foreach (var row in dungeonRows) dungeonUnlock[row.dungeon] = row.unlockStage;

            for (int i = 0; i < stages.Length; i++)
                if (stages[i].stage != i + 1)
                    throw new InvalidOperationException($"[GameBalance] 스테이지 시트가 1부터 이어지지 않는다 ({i + 1}번째 행 = stage {stages[i].stage})");
            if (upgrades.Length == 0 || mascots.Length == 0 || grades.Length == 0 || gachaLevels.Length == 0 || stages.Length == 0)
                throw new InvalidOperationException("[GameBalance] 업그레이드·꼬마·등급·뽑기·스테이지 시트 중 비어 있는 것이 있다");
            if (treeStages.Length == 0 || kidStars.Length == 0 || affinityLevels.Length == 0 || decoGrades.Length == 0)
                throw new InvalidOperationException("[GameBalance] 세계수·꼬마진화·호감도·장식등급 시트 중 비어 있는 것이 있다");
            loaded = true;
        }

        static IReadOnlyList<T> Read<T>(DataTable<T> table, Dictionary<string, List<string[]>> sheets, List<string> issues) where T : TableRow, new()
        {
            if (!sheets.TryGetValue(table.Name, out var sheet))
                throw new InvalidOperationException($"[GameBalance] '{table.Name}' 시트가 없다");
            table.Load(sheet, issues);
            return table.All;
        }

        static T[] ToArray<T>(IReadOnlyList<T> rows)
        {
            var array = new T[rows.Count];
            for (int i = 0; i < array.Length; i++) array[i] = rows[i];
            return array;
        }

        static T[] ToSorted<T>(IReadOnlyList<T> rows, Comparison<T> order)
        {
            var array = ToArray(rows);
            Array.Sort(array, order);
            return array;
        }

        // 이 표들은 TableRow의 name·desc·icon 열을 안 쓰는 시트도 있다 — 없다는 알림은 문제가 아니다
        static bool Ignorable(string issue) =>
            issue.Contains("기본값으로 둔다") && (issue.Contains("'name'") || issue.Contains("'desc'") || issue.Contains("'icon'"));
    }

    // GameBalance.xlsx 시트의 행 정의. 필드 이름 = 엑셀 1행의 열 이름이고 리플렉션으로 채우므로 link.xml에도 적는다.
    // 열을 바꾸면 GameBalanceValidator.Schemas와 엑셀 열도 같이 바꾼다.

    // 상수: 장비·합성·꼬마 강화·던전·앱 밖 보상·전투력과 기대 성장 곡선의 단일 수치. key로 찾는다
    public class ConstRow : TableRow
    {
        public double value;
    }

    // 등급: 장비 1레벨 보유 효과(%) 기본값
    public class GradeRow : TableRow
    {
        public int grade;          // 0 일반 ~ 4 전설 (GachaProgress.GradeNames 번호)
        public double ownBase;
    }

    // 뽑기 레벨: 다음 레벨까지 경험치와 등급별 확률(%)
    public class GachaLevelRow : TableRow
    {
        public int level;
        public int expToNext;      // 최고 레벨은 0
        public double oddsNormal, oddsUncommon, oddsRare, oddsEpic, oddsLegend;
    }

    // 업그레이드: 방치형 강화 한 종류 (IdleProgress.Def)
    public class UpgradeRow : TableRow
    {
        public IdleStat stat;
        public double baseValue, perLevel;
        public string valueMode;   // Add: base + perLevel × Lv / Multiply: base × (1 + perLevel × Lv)
        public long price;         // 비용 = price × (1 + Lv / priceDivisor)^priceExponent × e^(Lv / priceScale)
        public double priceExponent, priceDivisor, priceScale;
        public int maxLevel;
        public Currency currency;
        public IdleStat unlockStat;
        public int unlockLevel;
        public int resourceId;
        public int capEvery, capStep;
    }

    // 꼬마: Formation이 쓰는 꼬마 한 종
    public class MascotRow : TableRow
    {
        public int grade;
        public double attackMultiplier;
        public int initialOwned;   // 새 저장 데이터의 시작 레벨 (기본 편성 꼬마 = 1)
        public int defaultSlot;    // 0~4 = 기본 편성 칸, -1 = 미편성
        public int resourceId;
        public int[] favoriteIds;  // 좋아하는 버섯 (적.index): 선물 호감도 배수, 부탁 버섯
        public TreeEffect bonusEffect;   // 세계수 특기: 진화 ★(+장식 세트)마다 bonusValue씩
        public double bonusValue;
        public int evoStage;       // ★n 진화는 세계수 evoStage + n 단계부터
    }

    // 스테이지: 한 행 = 스테이지 하나 (1부터 이어진다)
    public class StageRow : TableRow
    {
        public int stage;
        public double stageCoefficient;     // 아직 게임이 읽지 않는다
        public int[] enemyIds;              // 아직 게임이 읽지 않는다
        public int bossId;                  // 아직 게임이 읽지 않는다
        public int wavesPerStage;
        public double bossTime;
        public int enemyCountBase, enemyCountPerWave, enemyCountPerStage, enemyCountMax;
        public long enemyGold, bossGold, bossMycelite;
        public double enemyAttack, bossAttack;
    }

    // 퀘스트: 일일 퀘스트 한 줄. kind = QuestKind 이름(대소문자 무시)
    public class QuestRow : TableRow
    {
        public string kind;
        public int target;         // 목표 횟수
        public int goldKills;      // 보상 골드 = 지금 스테이지 적 처치 골드 × 이 횟수
        public int mycelite;       // 보상 균사석 (고정)
    }

    // 던전: 던전 번호별 해금 조건
    public class DungeonRow : TableRow
    {
        public int dungeon;        // 던전 카드 번호 (DungeonEntry.dungeon)
        public int unlockStage;    // 이 스테이지에 진입해야 열린다 (0 = 처음부터)
    }

    // 리소스: 리소스.index → Resources 경로
    public class ResourceRow : TableRow
    {
        public string type;
        public string path;
    }

    // 적: 일반 적 버섯 한 종. 처치하면 세계수 창고에 쌓인다 (WorldTree.AddHarvest)
    public class EnemyRow : TableRow
    {
        public double hpCoefficient;   // 아직 게임이 읽지 않는다
        public int resourceId;
        public int value;          // 공물 경험치·선물 호감도 기본값 (1마리당)
        public int requestCount;   // 꼬마 부탁 1건에 요구하는 수
    }

    // 세계수: 한 행 = 단계 하나 (1부터). 진화 비용은 "이 단계 → 다음 단계"
    public class TreeStageRow : TableRow
    {
        public int stage;
        public int evoExp, evoGoldKills, evoMycelite;
        public double ringKills;           // 나이테 Lv1→2 골드 = 적 처치 골드 × 이 횟수 (Lv마다 treeRingGrowth배)
        public double bagKillsPerHour, bagMycelitePerHour;
        public int craftSlots;
        public int rootSlots, trunkSlots, branchSlots, crownSlots;
        public int[] preferIds;            // 공물로 좋아하는 버섯 (적.index)
        public int resourceId;
    }

    // 가호: 세계수 단계에 도달하면 영구 적용
    public class BlessRow : TableRow
    {
        public int stage;
        public TreeEffect effect;
        public double value;
    }

    // 꼬마진화: ★ 단계 (0 = 진화 전)
    public class KidStarRow : TableRow
    {
        public int star;
        public int mycelite;       // 이 ★로 진화하는 균사석
        public int needLevel;      // 필요한 꼬마(뽑기) 레벨
        public double bagMul;      // 보따리 기여 배율
    }

    // 호감도: 레벨 (1부터)
    public class AffinityRow : TableRow
    {
        public int level;
        public int need;           // 다음 레벨까지 (최고 레벨 0)
        public double bagBonus;
    }

    // 장식: 공방에서 만들어 꾸미기 자리에 놓는다
    public class DecoRow : TableRow
    {
        public string owner;       // 주인 꼬마 key
        public int grade;          // 장식등급.grade
        public TreeZone zone;
        public TreeEffect effect;
        public double value;
        public int resourceId;
    }

    // 장식등급: 제작 비용·시간
    public class DecoGradeRow : TableRow
    {
        public int grade;
        public int goldKills;
        public int mycelite;
        public double hours;
    }
}
