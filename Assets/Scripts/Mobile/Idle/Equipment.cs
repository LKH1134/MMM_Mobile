using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    public enum EquipStat { Attack, Hp }

    // 장비 (레전드 오브 슬라임식): 무기(수확기)·방어구·펫. 종류마다 등급(GachaProgress.GradeNames) × SubPerGrade단계 = Names.Length개.
    //   - 뽑기: 등급을 뽑은 뒤 그 등급 안에서 단계를 SubWeights 확률로 고른다 (PickItems)
    //   - 처음 얻으면 Lv.1, 같은 장비를 또 얻으면 조각(Copies)으로 쌓인다
    //   - 합성(도감 레벨업): 조각 CopiesToNext(Lv)개를 써서 그 장비의 레벨을 1 올린다 (다른 장비로 바뀌지 않는다, 최대 MaxLevel)
    //   - 보유 효과: 가진 장비마다 Stat OwnPercent(i)% = 1레벨 값 × (1 + LevelBonus × (Lv − 1))
    //   - 장착 효과: 장착한 장비 하나만 Stat EquipPercent(i)% (보유 효과 × EquipRatio, 레벨 반영)
    // 무기·펫 = 공격력, 방어구 = 체력 (IdleProgress.AttackMultiplier / HpMultiplier). 이름·그림·수치는 임시값이다
    public class Equipment
    {
        public const int SubPerGrade = 3;
        public const int MaxLevel = 100;
        public const double LevelBonus = 0.2;       // 레벨마다 효과 +20% (1레벨 값 기준)
        public const double EquipRatio = 3;         // 장착 효과 = 보유 효과 × 이 배수
        static readonly float[] SubWeights = { 0.6f, 0.3f, 0.1f };
        static readonly string[] WeaponSprites = { "sam", "sam", "saw", "hs_gold", "hs_gold" };   // 등급별 수확기 그림

        public static readonly Equipment Weapon = new Equipment(GachaKind.Weapon, "Weapon", "무기", EquipStat.Attack,
            new[]
            {
                "나무 낫", "돌 낫", "구리 낫",
                "철 낫", "강철 낫", "은 낫",
                "회전 톱날", "강철 톱날", "은빛 톱날",
                "붉은 낫", "불꽃 낫", "용암 낫",
                "황금 낫", "별빛 낫", "태초의 낫",
            },
            i => "Art/Dream/Harvesters/" + WeaponSprites[i / SubPerGrade]);

        public static readonly Equipment Armor = new Equipment(GachaKind.Accessory, "Armor", "방어구", EquipStat.Hp,
            new[]
            {
                "풀잎 망토", "나뭇잎 조끼", "이끼 외투",
                "버섯 모자", "포자 갑옷", "균사 갑옷",
                "이슬 방패", "수정 갑옷", "달빛 갑옷",
                "용암 갑옷", "불사조 망토", "화산 갑옷",
                "황금 갑옷", "별빛 갑옷", "세계수 갑옷",
            },
            i => "Art/Dream/Icons/mushtree");

        public static readonly Equipment Pet = new Equipment(GachaKind.Pet, "Pet", "펫", EquipStat.Attack,
            new[]
            {
                "아기 송이", "아기 표고", "아기 팽이",
                "꼬마 송이", "꼬마 표고", "꼬마 팽이",
                "날쌘 버섯", "단단 버섯", "반짝 버섯",
                "불꽃 버섯", "번개 버섯", "얼음 버섯",
                "황금 버섯", "별빛 버섯", "태초 버섯",
            },
            i => "Art/Dream/Characters/Critters/Mushrooms/ed" + (i * 10 / 15));

        public static readonly Equipment[] All = { Weapon, Armor, Pet };

        public static Equipment Of(GachaKind kind)
        {
            foreach (var e in All) if (e.Kind == kind) return e;
            return null;
        }

        public readonly GachaKind Kind;
        public readonly string Title;               // 화면 이름 (무기·방어구·펫)
        public readonly EquipStat Stat;
        public readonly string[] Names;
        readonly Func<int, string> spritePath;
        readonly string keyPrefix;

        public event Action Changed;

        // 레벨·조각·장착은 전투 중 매 프레임(IdleProgress.FinalHp 등 → TotalPercent) 읽는다 →
        // PlayerPrefs(안드로이드에서 느림)를 처음 한 번만 읽고 메모리에 둔다. 쓸 때 둘 다 고친다. PlayerPrefs를 밖에서 지웠으면 ReloadAll
        readonly string[] levelKeys, copiesKeys;
        int[] levels, copies;                       // null = 아직 안 읽음
        int equippedRaw;
        double totalPercent = double.NaN;           // NaN = 다시 셈

        Equipment(GachaKind kind, string keyPrefix, string title, EquipStat stat, string[] names, Func<int, string> spritePath)
        {
            Kind = kind;
            this.keyPrefix = keyPrefix;
            Title = title;
            Stat = stat;
            Names = names;
            this.spritePath = spritePath;
            levelKeys = new string[names.Length];
            copiesKeys = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                levelKeys[i] = $"{keyPrefix}.Level.{i}";
                copiesKeys[i] = $"{keyPrefix}.Copies.{i}";
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ReloadAll()
        {
            foreach (var e in All) e?.Reload();
        }

        void Reload()
        {
            levels = copies = null;
            totalPercent = double.NaN;
        }

        void EnsureLoaded()
        {
            if (levels != null) return;
            Migrate();
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

        public int Count => Names.Length;
        public string StatName => Stat == EquipStat.Attack ? "공격력" : "체력";

        string OwnedKey(int i) => $"{keyPrefix}.Owned.{i}";        // 예전 세이브 (얻은 장수)
        string EquippedKey => keyPrefix + ".Equipped";
        string MigratedKey => keyPrefix + ".Migrated";
        string MigratedLevelKey => keyPrefix + ".MigratedLevel";

        public static int Grade(int i) => i / SubPerGrade;
        public static int Sub(int i) => i % SubPerGrade;

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

        // 레벨 lv → lv+1에 드는 조각 수 (임시: Lv.1→2는 2개, 레벨마다 1개씩 늘어난다)
        public static int CopiesToNext(int lv) => lv + 1;

        // 장착한 장비 번호 (-1 = 없음, 안 가진 장비면 없음으로 본다)
        public int Equipped
        {
            get
            {
                EnsureLoaded();
                return IsOwned(equippedRaw) ? equippedRaw : -1;
            }
        }

        // 1장 보유 효과(%): 등급 기본값(GachaProgress.OwnBase) 사이를 단계마다 같은 비율로 잇는다
        public static double BaseOwnPercent(int i)
        {
            int g = Grade(i);
            double from = GachaProgress.OwnBase[g];
            double to = g + 1 < GachaProgress.OwnBase.Length ? GachaProgress.OwnBase[g + 1] : from * 2.5;
            return from * Math.Pow(to / from, Sub(i) / (double)SubPerGrade);
        }

        static double LevelMultiplier(int lv) => lv > 0 ? 1 + LevelBonus * (lv - 1) : 0;

        // 보유 효과 (지금 레벨, 없으면 0). lv를 주면 그 레벨 기준
        public double OwnPercent(int i) => BaseOwnPercent(i) * LevelMultiplier(Level(i));
        public static double OwnPercentAt(int i, int lv) => BaseOwnPercent(i) * LevelMultiplier(lv);

        // 장착 효과 (지금 레벨, 없으면 1레벨 기준으로 보여 준다)
        public double EquipPercent(int i) => i >= 0 && i < Count ? BaseOwnPercent(i) * EquipRatio * LevelMultiplier(Math.Max(1, Level(i))) : 0;
        public static double EquipPercentAt(int i, int lv) => BaseOwnPercent(i) * EquipRatio * LevelMultiplier(lv);

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

        public Sprite Sprite(int i) => i >= 0 && i < Count ? Resources.Load<Sprite>(spritePath(i)) : null;

        // 뽑기 결과(등급별 개수) → 장비 번호들
        public List<int> PickItems(int[] gradeCounts)
        {
            EnsureLoaded();     // GachaProgress.Apply가 등급별 장수를 올리기 전에 옛 세이브를 옮겨 둔다 (두 번 세지 않게)
            var items = new List<int>();
            for (int g = 0; g < gradeCounts.Length && g * SubPerGrade < Count; g++)
                for (int n = 0; n < gradeCounts[g]; n++) items.Add(g * SubPerGrade + PickSub());
            return items;
        }

        static int PickSub()
        {
            float r = UnityEngine.Random.value, acc = 0f;
            for (int s = 0; s < SubWeights.Length; s++)
            {
                acc += SubWeights[s];
                if (r < acc) return s;
            }
            return SubWeights.Length - 1;
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
                for (int g = 0; g < GachaProgress.GradeCount && g * SubPerGrade < Count; g++)
                {
                    int c = GachaProgress.Owned(Kind, g);
                    int first = g * SubPerGrade;
                    if (c > 0) PlayerPrefs.SetInt(OwnedKey(first), PlayerPrefs.GetInt(OwnedKey(first), 0) + c);
                }
            }
            for (int i = 0; i < Count; i++)
            {
                int owned = PlayerPrefs.GetInt(OwnedKey(i), 0);
                if (owned <= 0) continue;
                if (PlayerPrefs.GetInt(levelKeys[i], 0) <= 0) { PlayerPrefs.SetInt(levelKeys[i], 1); owned--; }
                PlayerPrefs.SetInt(copiesKeys[i], PlayerPrefs.GetInt(copiesKeys[i], 0) + owned);
                PlayerPrefs.DeleteKey(OwnedKey(i));
            }
            PlayerPrefs.Save();
        }
    }
}
