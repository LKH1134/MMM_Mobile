using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    public enum EquipStat { Attack, Hp }

    // 장비 (레전드 오브 슬라임식): 무기(수확기)·방어구. 종류·등급·단계는 장비 시트가 정한다 (등급마다 장비 수가 달라도 된다:
    //   무기·방어구 일반 4·희귀 3·영웅 2·전설 1). 저장 번호 = 시트 itemIndex
    //   - 뽑기: 등급을 뽑은 뒤 그 등급의 장비 중 하나를 같은 확률로 고른다 (PickItems)
    //   - 처음 얻으면 Lv.1, 같은 장비를 또 얻으면 조각(Copies)으로 쌓인다
    //   - 합성(도감 레벨업): 조각 CopiesToNext(Lv)개를 써서 그 장비의 레벨을 1 올린다 (다른 장비로 바뀌지 않는다, 최대 MaxLevel)
    //   - 보유 효과: 가진 장비마다 Stat OwnPercent(i)% = 1레벨 값 × (1 + LevelBonus × (Lv − 1))
    //   - 장착 효과: 장착한 장비 하나만 Stat EquipPercent(i)% (보유 효과 × EquipRatio, 레벨 반영)
    // 무기 = 공격력, 방어구 = 체력 (IdleProgress.AttackMultiplier / HpMultiplier). 이름·그림·수치는 임시값이다
    public class Equipment
    {
        // 예전 배치 (5등급 × 3단계 = 15칸, 고급 있음). 세이브를 새 배치로 옮길 때만 쓴다 (Migrate·RemapLayout)
        const int OldGradeCount = 5, OldSubPerGrade = 3, LayoutVersion = 3;   // 3 = 2026-11-04 방어구 10종

        // 수치는 상수 시트에서 읽는다 (처음 쓸 때). 이 클래스의 정적 필드는 리소스가 준비되기 전에도 만들어지므로 표를 바로 읽지 않는다
        sealed class Config
        {
            public readonly int maxLevel = GameBalance.ConstInt("equipMaxLevel");
            public readonly double levelBonus = GameBalance.Const("equipLevelBonus");     // 레벨마다 효과 +20% (1레벨 값 기준)
            public readonly double equipRatio = GameBalance.Const("equipRatio");          // 장착 효과 = 보유 효과 × 이 배수
            public readonly double topGradeMul = GameBalance.Const("equipTopGradeMul");   // 마지막 등급은 다음 등급이 없어 기본값 × 이 배수까지 잇는다
            public readonly int copiesBase = GameBalance.ConstInt("copiesBase");
            public readonly int copiesPerLevel = GameBalance.ConstInt("copiesPerLevel");
        }

        static Config config;
        static Config Cfg => config ?? (config = new Config());

        public static int MaxLevel => Cfg.maxLevel;
        public static double LevelBonus => Cfg.levelBonus;
        public static double EquipRatio => Cfg.equipRatio;
        // 이름·그림·등급·단계는 장비 시트(종류별 itemIndex 순서). 저장 키 앞부분은 여기
        public static readonly Equipment Weapon = new Equipment(GachaKind.Weapon, "Weapon", "무기", EquipStat.Attack);
        public static readonly Equipment Armor = new Equipment(GachaKind.Accessory, "Armor", "방어구", EquipStat.Hp);
        // 펫(Pet)은 2026-11-04 삭제. 저장 키 Pet.* 는 쓰지 않는다

        public static readonly Equipment[] All = { Weapon, Armor };

        public static Equipment Of(GachaKind kind)
        {
            foreach (var e in All) if (e.Kind == kind) return e;
            return null;
        }

        public readonly GachaKind Kind;
        public readonly string Title;               // 화면 이름 (무기·방어구)
        public readonly EquipStat Stat;
        string[] names, spritePaths;                // 장비 시트에서 처음 쓸 때 읽는다 (null = 아직)
        int[] grades, subs, gradeSizes;             // 장비별 등급·등급 안 단계, 등급별 장비 수
        List<int>[] byGrade;                        // 등급별 장비 번호 (뽑기용)
        readonly string keyPrefix;

        public event Action Changed;

        // 레벨·조각·장착은 전투 중 매 프레임(IdleProgress.FinalHp 등 → TotalPercent) 읽는다 →
        // PlayerPrefs(안드로이드에서 느림)를 처음 한 번만 읽고 메모리에 둔다. 쓸 때 둘 다 고친다. PlayerPrefs를 밖에서 지웠으면 ReloadAll
        string[] levelKeys, copiesKeys;             // 장비 수(시트)를 알아야 만들 수 있어 LoadRows에서 만든다
        int[] levels, copies;                       // null = 아직 안 읽음
        int equippedRaw;
        double totalPercent = double.NaN;           // NaN = 다시 셈

        Equipment(GachaKind kind, string keyPrefix, string title, EquipStat stat)
        {
            Kind = kind;
            this.keyPrefix = keyPrefix;
            Title = title;
            Stat = stat;
        }

        string LevelKey(int i) => $"{keyPrefix}.Level.{i}";
        string CopiesKey(int i) => $"{keyPrefix}.Copies.{i}";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ReloadAll()
        {
            config = null;
            foreach (var e in All) e?.Reload();
        }

        void Reload()
        {
            names = spritePaths = levelKeys = copiesKeys = null;
            grades = subs = gradeSizes = null;
            byGrade = null;
            levels = copies = null;
            totalPercent = double.NaN;
        }

        void EnsureLoaded()
        {
            if (levels != null) return;
            if (names == null) LoadRows();
            Migrate();
            RemapLayout();
            var lv = new int[Count];
            var cp = new int[Count];
            for (int i = 0; i < Count; i++)
            {
                lv[i] = Mathf.Clamp(PlayerPrefs.GetInt(levelKeys[i], 0), 0, MaxLevel);
                cp[i] = Mathf.Max(0, PlayerPrefs.GetInt(copiesKeys[i], 0));
            }
            equippedRaw = PlayerPrefs.GetInt(EquippedKey, -1);
            copies = cp;
            levels = lv;
            totalPercent = double.NaN;
        }

        void SetLevel(int i, int lv)
        {
            levels[i] = Mathf.Clamp(lv, 0, MaxLevel);
            PlayerPrefs.SetInt(levelKeys[i], levels[i]);
            totalPercent = double.NaN;
        }

        void SetCopies(int i, int n)
        {
            copies[i] = Mathf.Max(0, n);
            PlayerPrefs.SetInt(copiesKeys[i], copies[i]);
        }

        void SetEquipped(int i)
        {
            equippedRaw = i;
            PlayerPrefs.SetInt(EquippedKey, i);
            totalPercent = double.NaN;
        }

        public int Count
        {
            get
            {
                if (names == null) LoadRows();
                return names.Length;
            }
        }

        // 장비 이름 (장비 시트 name)
        public string[] Names
        {
            get
            {
                if (names == null) LoadRows();
                return names;
            }
        }

        // 장비 시트 → 이름·그림·등급·단계·저장 키. 장비 수 = 가장 큰 itemIndex + 1 (검증기가 빈 번호를 막는다)
        void LoadRows()
        {
            var rows = GameBalance.EquipmentOf(Kind);
            int count = 0;
            foreach (var r in rows) if (r.itemIndex >= 0) count = Mathf.Max(count, r.itemIndex + 1);
            var n = new string[count];
            var p = new string[count];
            var g = new int[count];
            var s = new int[count];
            foreach (var r in rows)
            {
                if (r.itemIndex < 0) continue;
                n[r.itemIndex] = r.name;
                p[r.itemIndex] = GameBalance.ResourcePath(r.resourceId);
                g[r.itemIndex] = Mathf.Clamp(r.grade, 0, GachaProgress.GradeCount - 1);
                s[r.itemIndex] = Mathf.Max(0, r.sub);
            }
            for (int i = 0; i < count; i++) if (n[i] == null) n[i] = Title + " " + (i + 1);   // 검증기가 막지만, 빠진 칸이 있어도 화면이 깨지지 않게

            var sizes = new int[GachaProgress.GradeCount];
            var lists = new List<int>[GachaProgress.GradeCount];
            for (int k = 0; k < lists.Length; k++) lists[k] = new List<int>();
            for (int i = 0; i < count; i++) { sizes[g[i]]++; lists[g[i]].Add(i); }

            var lk = new string[count];
            var ck = new string[count];
            for (int i = 0; i < count; i++) { lk[i] = LevelKey(i); ck[i] = CopiesKey(i); }

            levelKeys = lk;
            copiesKeys = ck;
            grades = g;
            subs = s;
            gradeSizes = sizes;
            byGrade = lists;
            spritePaths = p;
            names = n;
        }
        public string StatName => Stat == EquipStat.Attack ? "공격력" : "체력";

        string OwnedKey(int i) => $"{keyPrefix}.Owned.{i}";        // 예전 세이브 (얻은 장수)
        string EquippedKey => keyPrefix + ".Equipped";
        string MigratedKey => keyPrefix + ".Migrated";
        string MigratedLevelKey => keyPrefix + ".MigratedLevel";

        // 장비 i의 등급 (GachaProgress.GradeNames 번호)·등급 안 단계
        public int Grade(int i)
        {
            if (names == null) LoadRows();
            return i >= 0 && i < grades.Length ? grades[i] : 0;
        }

        public int Sub(int i)
        {
            if (names == null) LoadRows();
            return i >= 0 && i < subs.Length ? subs[i] : 0;
        }

        // 그 등급의 장비 수
        public int GradeSize(int grade)
        {
            if (names == null) LoadRows();
            return grade >= 0 && grade < gradeSizes.Length ? gradeSizes[grade] : 0;
        }

        // 장비 레벨 (0 = 아직 없음)
        public int Level(int i)
        {
            if (i < 0 || i >= Count) return 0;
            EnsureLoaded();
            return levels[i];
        }

        public bool IsOwned(int i) => Level(i) > 0;

        // 레벨업에 쓸 조각 (같은 장비를 또 얻은 수)
        public int Copies(int i)
        {
            if (i < 0 || i >= Count) return 0;
            EnsureLoaded();
            return copies[i];
        }

        // 레벨 lv → lv+1에 드는 조각 수 (임시: Lv.1→2는 2개, 레벨마다 1개씩 늘어난다). 상수 시트 copiesBase + copiesPerLevel × lv
        public static int CopiesToNext(int lv) => Cfg.copiesBase + Cfg.copiesPerLevel * lv;

        // 장착한 장비 번호 (-1 = 없음, 안 가진 장비면 없음으로 본다)
        public int Equipped
        {
            get
            {
                EnsureLoaded();
                return IsOwned(equippedRaw) ? equippedRaw : -1;
            }
        }

        // 1장 보유 효과(%): 등급 기본값(GachaProgress.OwnBase) 사이를 그 등급 장비 수로 나눠 단계마다 같은 비율로 잇는다
        public double BaseOwnPercent(int i)
        {
            int g = Grade(i);
            double from = GachaProgress.OwnBase(g);
            double to = g + 1 < GachaProgress.GradeCount ? GachaProgress.OwnBase(g + 1) : from * Cfg.topGradeMul;
            return from * Math.Pow(to / from, Sub(i) / (double)Math.Max(1, GradeSize(g)));
        }

        static double LevelMultiplier(int lv) => lv > 0 ? 1 + LevelBonus * (lv - 1) : 0;

        // 보유 효과 (지금 레벨, 없으면 0). lv를 주면 그 레벨 기준
        public double OwnPercent(int i) => BaseOwnPercent(i) * LevelMultiplier(Level(i));
        public double OwnPercentAt(int i, int lv) => BaseOwnPercent(i) * LevelMultiplier(lv);

        // 장착 효과 (지금 레벨, 없으면 1레벨 기준으로 보여 준다)
        public double EquipPercent(int i) => i >= 0 && i < Count ? BaseOwnPercent(i) * EquipRatio * LevelMultiplier(Math.Max(1, Level(i))) : 0;
        public double EquipPercentAt(int i, int lv) => BaseOwnPercent(i) * EquipRatio * LevelMultiplier(lv);

        public double TotalOwnPercent
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < Count; i++) sum += OwnPercent(i);
                return sum;
            }
        }

        // 스탯에 더해지는 % 전체 (보유 + 장착). 레벨·장착이 바뀔 때만 다시 셈한다
        public double TotalPercent
        {
            get
            {
                EnsureLoaded();
                if (double.IsNaN(totalPercent)) totalPercent = TotalOwnPercent + EquipPercent(Equipped);
                return totalPercent;
            }
        }

        public Sprite Sprite(int i)
        {
            if (i < 0 || i >= Count) return null;
            if (spritePaths == null) LoadRows();
            return spritePaths[i] != null ? Resources.Load<Sprite>(spritePaths[i]) : null;
        }

        // 뽑기 결과(등급별 개수) → 장비 번호들
        public List<int> PickItems(int[] gradeCounts)
        {
            EnsureLoaded();     // GachaProgress.Apply가 등급별 장수를 올리기 전에 옛 세이브를 옮겨 둔다 (두 번 세지 않게)
            var items = new List<int>();
            for (int g = 0; g < gradeCounts.Length && g < byGrade.Length; g++)
            {
                var pool = byGrade[g];
                if (pool.Count == 0) continue;     // 장비가 없는 등급 (검증기가 막는다)
                for (int n = 0; n < gradeCounts[g]; n++) items.Add(pool[UnityEngine.Random.Range(0, pool.Count)]);
            }
            return items;
        }

        public void AddItems(IEnumerable<int> items)
        {
            EnsureLoaded();
            int best = -1;
            foreach (int i in items)
            {
                if (i < 0 || i >= Count) continue;
                if (Level(i) == 0) SetLevel(i, 1);                 // 처음 얻음 → Lv.1
                else SetCopies(i, Copies(i) + 1);                  // 또 얻음 → 조각
                best = Mathf.Max(best, i);
            }
            if (Equipped < 0 && best >= 0) SetEquipped(best);      // 처음 얻은 장비는 바로 장착
            Save();
        }

        public void Equip(int i)
        {
            if (!IsOwned(i) || Equipped == i) return;
            SetEquipped(i);
            Save();
        }

        public bool CanMerge(int i)
        {
            int lv = Level(i);
            return lv > 0 && lv < MaxLevel && Copies(i) >= CopiesToNext(lv);
        }

        // i의 조각으로 올릴 수 있는 만큼 레벨을 올린다. 오른 레벨 수를 돌려준다
        public int Merge(int i)
        {
            int up = LevelUp(i);
            if (up > 0) Save();
            return up;
        }

        // 모든 장비를 올릴 수 있는 만큼 올린다. 오른 레벨 수 합을 돌려준다
        public int MergeAll()
        {
            int up = 0;
            for (int i = 0; i < Count; i++) up += LevelUp(i);
            if (up > 0) Save();
            return up;
        }

        int LevelUp(int i)
        {
            int lv = Level(i), left = Copies(i), up = 0;
            if (lv <= 0) return 0;
            while (lv < MaxLevel && left >= CopiesToNext(lv))
            {
                left -= CopiesToNext(lv);
                lv++;
                up++;
            }
            if (up == 0) return 0;
            SetLevel(i, lv);
            SetCopies(i, left);
            return up;
        }

        void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 예전 세이브를 옮긴다 (한 번만):
        //   1) 등급별 장수만 있던 때 → 각 등급 첫 단계 장비 장수로
        //   2) 장비별 장수(Owned)만 있던 때 → 1장은 Lv.1, 나머지는 조각으로
        void Migrate()
        {
            if (PlayerPrefs.GetInt(MigratedLevelKey, 0) == 1) return;
            PlayerPrefs.SetInt(MigratedLevelKey, 1);
            if (PlayerPrefs.GetInt(MigratedKey, 0) == 0)
            {
                PlayerPrefs.SetInt(MigratedKey, 1);
                for (int g = 0; g < OldGradeCount; g++)
                {
                    int c = GachaProgress.Owned(Kind, g);
                    int first = g * OldSubPerGrade;
                    if (c > 0) PlayerPrefs.SetInt(OwnedKey(first), PlayerPrefs.GetInt(OwnedKey(first), 0) + c);
                }
            }
            for (int i = 0; i < OldGradeCount * OldSubPerGrade; i++)
            {
                int owned = PlayerPrefs.GetInt(OwnedKey(i), 0);
                if (owned <= 0) continue;
                if (PlayerPrefs.GetInt(LevelKey(i), 0) <= 0) { PlayerPrefs.SetInt(LevelKey(i), 1); owned--; }
                PlayerPrefs.SetInt(CopiesKey(i), PlayerPrefs.GetInt(CopiesKey(i), 0) + owned);
                PlayerPrefs.DeleteKey(OwnedKey(i));
            }
            PlayerPrefs.Save();
        }

        // 배치가 바뀐 세이브를 지금 장비 시트 배치로 옮긴다 (한 번만, Migrate 다음). 예전 번호 i = 등급 × 3 + 단계
        //   Layout 0~1 → 3: 2026-11-04 고급 삭제 전 (5등급). 고급(1)은 버린다. 일반 0 → 0, 희귀 2 → 1, 영웅 3 → 2, 전설 4 → 3
        //   Layout 2 → 3: 방어구만 — 4등급 × 3 (12종)에서 10종(4/3/2/1)으로. 무기는 Layout 2 배치 그대로
        //   새 등급에서 같은 단계 장비로 (새 등급 장비가 적으면 마지막 장비로 모은다: 레벨은 큰 쪽, 조각은 더한다)
        void RemapLayout()
        {
            string versionKey = keyPrefix + ".Layout";
            int version = PlayerPrefs.GetInt(versionKey, 0);
            if (version >= LayoutVersion) return;
            if (version < 2) RemapFrom(OldGradeCount, g => g == 1 ? -1 : g == 0 ? 0 : g - 1);
            else if (Kind == GachaKind.Accessory) RemapFrom(GachaProgress.GradeCount, g => g);
            PlayerPrefs.SetInt(versionKey, LayoutVersion);
            PlayerPrefs.Save();
        }

        // 예전 배치 (oldGrades등급 × OldSubPerGrade단계) → 지금 배치. newGrade(예전 등급) = 새 등급 (-1 = 버림)
        void RemapFrom(int oldGrades, Func<int, int> newGrade)
        {
            int oldCount = oldGrades * OldSubPerGrade;
            var lv = new int[Count];
            var cp = new int[Count];
            int oldEquipped = PlayerPrefs.GetInt(EquippedKey, -1), equipped = -1;
            for (int i = 0; i < oldCount; i++)
            {
                int oldLv = PlayerPrefs.GetInt(LevelKey(i), 0), oldCp = PlayerPrefs.GetInt(CopiesKey(i), 0);
                PlayerPrefs.DeleteKey(LevelKey(i));
                PlayerPrefs.DeleteKey(CopiesKey(i));
                int ng = newGrade(i / OldSubPerGrade);
                if (ng < 0 || oldLv <= 0) continue;
                if (ng >= byGrade.Length || byGrade[ng].Count == 0) continue;
                var pool = byGrade[ng];
                int to = pool[Mathf.Min(i % OldSubPerGrade, pool.Count - 1)];
                lv[to] = Mathf.Max(lv[to], oldLv);
                cp[to] += Mathf.Max(0, oldCp);
                if (i == oldEquipped) equipped = to;
            }
            for (int i = 0; i < Count; i++)
            {
                if (lv[i] > 0) PlayerPrefs.SetInt(LevelKey(i), lv[i]);
                if (cp[i] > 0) PlayerPrefs.SetInt(CopiesKey(i), cp[i]);
            }
            if (equipped < 0) for (int i = Count - 1; i >= 0; i--) if (lv[i] > 0) { equipped = i; break; }   // 장착하던 게 버려졌으면 가진 것 중 마지막(가장 좋은) 것
            PlayerPrefs.SetInt(EquippedKey, equipped);
        }
    }
}
