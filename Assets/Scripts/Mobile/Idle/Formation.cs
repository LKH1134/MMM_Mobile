using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 꼬마 편성: 칸 SlotCount개에 꼬마 id 하나씩(빈 칸 = ""). 칸 i = Main 전투의 Ggoma(i+1) 자리.
    // 꼬마는 꼬마 뽑기(GachaKind.Character)로 얻는다. 기본 편성 5명(꼬마 시트 defaultSlot)은 처음부터 Lv.1로 가진다. 중복은 조각 → 합성으로 레벨업.
    // 등급은 없다. 꼬마(도감) 레벨에 따라 진화한다: Lv < ggomaEvoLevel2 = 1단계, < ggomaEvoLevel3 = 2단계, 그 이상 = 3단계.
    // 단계마다 그림(evoResourceIds 시트의 evoCell 칸)과 공격 배율(× ggomaEvoMul2·3)이 바뀐다.
    // 가진 꼬마만 편성할 수 있고, 저장된 칸의 꼬마를 안 가졌으면 빈 칸으로 본다.
    // PlayerPrefs("Formation.<칸>", "Formation.Owned.<id>")에 저장한다 (세이브 구조가 생기면 옮긴다)
    public static class Formation
    {
        public const int SlotCount = 5;

        // 꼬마 8종 (이름·1단계 공격 배율·시작 레벨·기본 편성·진화 그림)은 꼬마 시트에서 읽는다. 순서 = 시트 순서.
        // 공격 배율: 이 꼬마가 던지는 수확기 피해 = 공격력 × 배율. 1단계는 모두 0.9 (예전 기본 편성 평균 약 0.88)
        sealed class Table
        {
            public readonly string[] ids, names, iconPaths, defaults, levelKeys, copiesKeys;
            public readonly string[][] evoPaths;    // [꼬마][단계-1] 진화 그림 시트 경로
            public readonly int[] startLevels, evoCells;
            public readonly double[] attackMuls;
            public readonly int maxLevel = GameBalance.ConstInt("ggomaMaxLevel");
            public readonly double levelBonus = GameBalance.Const("ggomaLevelBonus");
            public readonly int evoLevel2 = GameBalance.ConstInt("ggomaEvoLevel2"), evoLevel3 = GameBalance.ConstInt("ggomaEvoLevel3");
            public readonly double evoMul2 = GameBalance.Const("ggomaEvoMul2"), evoMul3 = GameBalance.Const("ggomaEvoMul3");
            public readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();   // 경로 → 칸 번호 순 조각

            public Table()
            {
                var rows = GameBalance.Mascots;
                int n = rows.Count;
                ids = new string[n];
                names = new string[n];
                iconPaths = new string[n];
                evoPaths = new string[n][];
                evoCells = new int[n];
                startLevels = new int[n];
                attackMuls = new double[n];
                defaults = new string[SlotCount];
                for (int i = 0; i < SlotCount; i++) defaults[i] = "";
                for (int i = 0; i < n; i++)
                {
                    var r = rows[i];
                    ids[i] = r.key;
                    names[i] = r.name;
                    iconPaths[i] = GameBalance.ResourcePath(r.resourceId);
                    evoPaths[i] = new string[EvoStageCount];
                    for (int s = 0; s < EvoStageCount; s++)
                        evoPaths[i][s] = r.evoResourceIds != null && s < r.evoResourceIds.Length ? GameBalance.ResourcePath(r.evoResourceIds[s]) : null;
                    evoCells[i] = r.evoCell;
                    startLevels[i] = r.initialOwned;
                    attackMuls[i] = r.attackMultiplier;
                    if (r.defaultSlot >= 0 && r.defaultSlot < SlotCount) defaults[r.defaultSlot] = r.key;
                }
                levelKeys = BuildKeys(n, i => "Formation.Level." + ids[i]);
                copiesKeys = BuildKeys(n, i => "Formation.Copies." + ids[i]);
            }
        }

        static Table table;
        static Table T => table ?? (table = new Table());

        public static string[] Ids => T.ids;

        public static event System.Action Changed;

        // 칸·레벨·조각은 전투 중 던질 때마다 읽는다 → PlayerPrefs(안드로이드에서 느림)를 처음 한 번만 읽고 메모리에 둔다.
        // 쓸 때 둘 다 고친다. PlayerPrefs를 밖에서 지웠으면 Reload
        static readonly string[] SlotKeys = BuildKeys(SlotCount, i => "Formation." + i);
        static string[] slots;          // 저장된 칸 id (가졌는지는 Get이 본다). null = 아직 안 읽음
        static int[] levels, copies;    // Ids 순서

        static string[] BuildKeys(int n, System.Func<int, string> key)
        {
            var keys = new string[n];
            for (int i = 0; i < n; i++) keys[i] = key(i);
            return keys;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            slots = null;
            levels = copies = null;
            migrated = false;
            table = null;       // 표는 처음 쓸 때 다시 만든다 (이 훅은 리소스가 준비되기 전에 돈다)
        }

        static void EnsureLoaded()
        {
            if (slots != null) return;
            Migrate();
            var t = T;
            var lv = new int[t.ids.Length];
            var cp = new int[t.ids.Length];
            for (int i = 0; i < t.ids.Length; i++)
            {
                lv[i] = Mathf.Clamp(PlayerPrefs.GetInt(t.levelKeys[i], t.startLevels[i]), 0, MaxLevel);
                cp[i] = Mathf.Max(0, PlayerPrefs.GetInt(t.copiesKeys[i], 0));
            }
            var s = new string[SlotCount];
            for (int i = 0; i < SlotCount; i++) s[i] = PlayerPrefs.GetString(SlotKeys[i], t.defaults[i]);
            levels = lv;
            copies = cp;
            slots = s;
        }

        static void SetSlot(int slot, string id)
        {
            slots[slot] = id;
            PlayerPrefs.SetString(SlotKeys[slot], id);
        }

        static void SetLevel(int i, int lv)
        {
            levels[i] = Mathf.Clamp(lv, 0, MaxLevel);
            PlayerPrefs.SetInt(T.levelKeys[i], levels[i]);
        }

        static void SetCopies(int i, int n)
        {
            copies[i] = Mathf.Max(0, n);
            PlayerPrefs.SetInt(T.copiesKeys[i], copies[i]);
        }

        public static string Get(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return "";
            EnsureLoaded();
            string id = slots[slot];
            return IsOwned(id) ? id : "";
        }

        // ── 강화 (장비와 같은 방식): 처음 얻으면 Lv.1, 같은 꼬마를 또 얻으면 조각. 조각 CopiesToNext(Lv)개로 레벨 +1 (최대 MaxLevel).
        // 레벨마다 공격 배율 +LevelBonus (1레벨 값 기준). PlayerPrefs("Formation.Level.<id>", "Formation.Copies.<id>")
        public static int MaxLevel => T.maxLevel;
        public static double LevelBonus => T.levelBonus;      // 임시값: 배율이 밸런스 곡선(배율 1.0 기준)을 크게 흔들지 않게 장비(20%)보다 낮게. 상수 시트 ggomaLevelBonus

        // 꼬마 레벨 (0 = 아직 없음). 기본 편성 꼬마는 처음부터 Lv.1
        public static int Level(string id)
        {
            int i = System.Array.IndexOf(Ids, id);
            if (i < 0) return 0;
            EnsureLoaded();
            return levels[i];
        }

        public static bool IsOwned(string id) => Level(id) > 0;

        // 레벨업에 쓸 조각 (같은 꼬마를 또 얻은 수)
        public static int Copies(string id)
        {
            int i = System.Array.IndexOf(Ids, id);
            if (i < 0) return 0;
            EnsureLoaded();
            return copies[i];
        }

        // 레벨 lv → lv+1에 드는 조각 수 (장비와 같음: Lv.1→2는 2개, 레벨마다 1개씩 늘어난다)
        public static int CopiesToNext(int lv) => Equipment.CopiesToNext(lv);

        public static bool CanMerge(string id)
        {
            int lv = Level(id);
            return lv > 0 && lv < MaxLevel && Copies(id) >= CopiesToNext(lv);
        }

        // 조각으로 올릴 수 있는 만큼 레벨을 올린다. 오른 레벨 수
        public static int Merge(string id)
        {
            int up = LevelUp(id);
            if (up > 0) Save();
            return up;
        }

        public static int MergeAll()
        {
            int up = 0;
            foreach (var id in Ids) up += LevelUp(id);
            if (up > 0) Save();
            return up;
        }

        static int LevelUp(string id)
        {
            int lv = Level(id), left = Copies(id), up = 0;
            if (lv <= 0) return 0;
            while (lv < MaxLevel && left >= CopiesToNext(lv))
            {
                left -= CopiesToNext(lv);
                lv++;
                up++;
            }
            if (up == 0) return 0;
            int i = System.Array.IndexOf(Ids, id);
            SetLevel(i, lv);
            SetCopies(i, left);
            return up;
        }

        // 꼬마 뽑기 결과를 더한다 (처음 얻으면 Lv.1, 또 얻으면 조각)
        public static void AddOwned(System.Collections.Generic.IEnumerable<string> ids)
        {
            EnsureLoaded();
            foreach (var id in ids)
            {
                int i = System.Array.IndexOf(Ids, id);
                if (i < 0) continue;
                if (levels[i] == 0) SetLevel(i, 1);
                else SetCopies(i, copies[i] + 1);
            }
            Save();
        }

        static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 예전 세이브(가진 장수 "Formation.Owned.<id>")를 한 번 옮긴다: 1장은 Lv.1, 나머지는 조각 (처음 읽을 때 EnsureLoaded가 부른다)
        static bool migrated;

        static void Migrate()
        {
            if (migrated) return;
            migrated = true;
            if (PlayerPrefs.GetInt("Formation.MigratedLevel", 0) == 1) return;
            PlayerPrefs.SetInt("Formation.MigratedLevel", 1);
            for (int i = 0; i < Ids.Length; i++)
            {
                string ownedKey = "Formation.Owned." + Ids[i];
                if (!PlayerPrefs.HasKey(ownedKey)) continue;
                int owned = PlayerPrefs.GetInt(ownedKey, 0);
                if (owned > 0)
                {
                    PlayerPrefs.SetInt(T.levelKeys[i], 1);
                    PlayerPrefs.SetInt(T.copiesKeys[i], owned - 1);
                }
                PlayerPrefs.DeleteKey(ownedKey);
            }
            PlayerPrefs.Save();
        }

        // 꼬마 뽑기 1회: 모든 꼬마가 같은 확률 (등급 없음)
        public static string RandomId() => Ids.Length > 0 ? Ids[Random.Range(0, Ids.Length)] : "";

        // 이 꼬마가 놓인 칸 (없으면 -1)
        public static int SlotOf(string id)
        {
            for (int i = 0; i < SlotCount; i++) if (Get(i) == id) return i;
            return -1;
        }

        public static bool IsEmpty(int slot) => string.IsNullOrEmpty(Get(slot));

        // slot에 id를 놓는다. id가 다른 칸에 이미 있으면 두 칸을 맞바꾼다(빈 칸이었으면 그 칸이 빈다).
        // 이미 그 칸에 있는 꼬마를 다시 고르면 편성 해제
        public static void Place(int slot, string id)
        {
            if (slot < 0 || slot >= SlotCount || !IsOwned(id)) return;
            string old = Get(slot);
            if (old == id) { Clear(slot); return; }
            int from = SlotOf(id);
            if (from >= 0) SetSlot(from, old);
            SetSlot(slot, id);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Clear(int slot)
        {
            if (IsEmpty(slot)) return;
            SetSlot(slot, "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 자동 편성: 가진 꼬마 중 공격 배율이 높은 순(같으면 목록 순)으로 SlotCount명을 1번 칸부터 놓는다
        public static void AutoArrange()
        {
            EnsureLoaded();
            var order = new System.Collections.Generic.List<string>(Ids);
            order.RemoveAll(id => !IsOwned(id));
            order.Sort((a, b) =>
            {
                int c = AttackMultiplier(b).CompareTo(AttackMultiplier(a));
                return c != 0 ? c : System.Array.IndexOf(Ids, a).CompareTo(System.Array.IndexOf(Ids, b));
            });
            for (int i = 0; i < SlotCount; i++) SetSlot(i, i < order.Count ? order[i] : "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // ── 진화: 꼬마(도감) 레벨로 정해진다 (따로 저장하지 않는다) ──
        public const int EvoStageCount = 3;     // 1·2·3단계
        public const int EvoCellCount = 8;      // 진화 그림 시트 한 장의 칸 수 (4×2)

        // 레벨 lv의 진화 단계 (1~3). 0레벨(미보유)은 1단계로 본다
        public static int EvoStageAt(int lv)
        {
            lv = Mathf.Max(1, lv);
            return lv >= T.evoLevel3 ? 3 : lv >= T.evoLevel2 ? 2 : 1;
        }

        public static int EvoStage(string id) => EvoStageAt(Level(id));

        // 이 단계가 되는 레벨 (1단계 = 1)
        public static int EvoLevel(int stage) => stage >= 3 ? T.evoLevel3 : stage == 2 ? T.evoLevel2 : 1;

        // 단계 공격 배율 (1단계 = 1)
        public static double EvoMul(int stage) => stage >= 3 ? T.evoMul3 : stage == 2 ? T.evoMul2 : 1;

        // 공격 배율 = 1단계 배율 × 단계 배율 × (1 + LevelBonus × (Lv − 1)). 빈 칸·모르는 id는 0 (던지지 않는다), 안 가졌으면 1레벨 값
        public static double AttackMultiplier(string id) => AttackMultiplierAt(id, Mathf.Max(1, Level(id)));

        public static double AttackMultiplierAt(string id, int lv)
        {
            int i = System.Array.IndexOf(Ids, id);
            lv = Mathf.Max(1, lv);
            return i >= 0 ? T.attackMuls[i] * EvoMul(EvoStageAt(lv)) * (1 + LevelBonus * (lv - 1)) : 0;
        }

        // 지금 편성된 꼬마들의 공격 배율 합 (전투력 계산용)
        public static double TotalAttackMultiplier()
        {
            double sum = 0;
            for (int i = 0; i < SlotCount; i++) sum += AttackMultiplier(Get(i));
            return sum;
        }

        public static string Name(string id)
        {
            int i = System.Array.IndexOf(Ids, id);
            return i >= 0 ? GameBalance.Mascots[i].Name : id;   // 번역된 이름 (언어를 바꿔도 바로 따라간다)
        }

        // 지금 진화 단계의 그림
        public static Sprite Icon(string id) => Icon(id, EvoStage(id));

        // 그 단계의 그림: 진화 그림 시트의 evoCell 칸. 시트·칸을 못 찾으면 꼬마 시트 resourceId 그림
        public static Sprite Icon(string id, int stage)
        {
            if (string.IsNullOrEmpty(id)) return null;
            int i = System.Array.IndexOf(Ids, id);
            if (i < 0) return null;
            var t = T;
            string sheet = t.evoPaths[i][Mathf.Clamp(stage, 1, EvoStageCount) - 1];
            var cells = sheet != null ? Cells(sheet) : null;
            int cell = t.evoCells[i];
            if (cells != null && cell >= 0 && cell < cells.Length && cells[cell] != null) return cells[cell];
            return t.iconPaths[i] != null ? Resources.Load<Sprite>(t.iconPaths[i]) : null;
        }

        // 시트의 조각들을 칸 번호 순으로 (이름 끝 "_숫자"가 칸 번호. 아니면 위 줄→아래 줄, 왼→오른 순). 처음 한 번만 읽는다
        static Sprite[] Cells(string path)
        {
            var sheets = T.sheets;
            if (sheets.TryGetValue(path, out var cached)) return cached;
            var all = Resources.LoadAll<Sprite>(path);
            Sprite[] cells = null;
            if (all != null && all.Length > 0)
            {
                cells = new Sprite[all.Length];
                bool named = true;
                foreach (var sp in all)
                {
                    int us = sp.name.LastIndexOf('_');
                    if (us < 0 || !int.TryParse(sp.name.Substring(us + 1), out int n) || n < 0 || n >= cells.Length || cells[n] != null) { named = false; break; }
                    cells[n] = sp;
                }
                if (!named)
                {
                    cells = (Sprite[])all.Clone();
                    System.Array.Sort(cells, (a, b) =>
                    {
                        int c = b.rect.y.CompareTo(a.rect.y);     // 위 줄 먼저 (텍스처 y는 아래가 0)
                        return c != 0 ? c : a.rect.x.CompareTo(b.rect.x);
                    });
                }
            }
            sheets[path] = cells;
            return cells;
        }
    }
}
