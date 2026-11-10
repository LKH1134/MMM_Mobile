using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 도감 세트가 올리는 능력치
    public enum DexStat { Attack, Hp, HpRegen, Speed }

    // 버섯 도감 (수집): 도감 시트의 무리(종류 × ★1~5)마다 count종의 버섯이 있다.
    //   - 얻기: 방치 전투 보스를 잡을 때마다(던전 제외) unlockStage가 지난 무리마다 dropChance로 아직 없는 종 1개 (RollBossDrop).
    //     별이 높은 무리일수록 unlockStage가 늦어 초반엔 ★가 낮은 버섯, 후반엔 높은 버섯이 나온다
    //   - 세트: 한 무리의 count종을 다 모으면 도감세트 시트 효과가 켜진다. 식용 = 공격력, 약용 = 체력·체력 재생, 독성 = 공격 속도
    //   - 능력치: 공격력·체력·체력 재생 %는 장비 보유 효과 %에 더하고, 공격 속도 %는 최종 공격 속도에 곱한다 (IdleProgress)
    // 저장: PlayerPrefs "Dex.<도감.index>" = 모은 종 비트 (번호 i = 그림 조각 "시트이름_i"). 전투 중 매 프레임 읽혀서 메모리에 둔다
    public static class MushroomDex
    {
        public const int MaxStar = 5;
        public const int MaxPerGroup = 31;   // int 비트 하나에 담는다 (검증기가 count를 막는다)

        static DexRow[] rows;
        static int[] owned;                  // 무리마다 모은 종 비트 (null = 아직 안 읽음)
        static double[] percent;             // DexStat별 켜진 세트 합(%) (null = 다시 셈)
        static Sprite[][] sprites;           // 무리마다 조각 그림 (처음 쓸 때)

        // 새 버섯을 얻거나 세트가 바뀌면
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            rows = null;
            owned = null;
            percent = null;
            sprites = null;
            Changed = null;
        }

        static string Key(DexRow row) => "Dex." + row.index;

        static void Ensure()
        {
            if (owned != null) return;
            var list = GameBalance.DexRows;
            var r = new DexRow[list.Count];
            var o = new int[list.Count];
            for (int i = 0; i < r.Length; i++)
            {
                r[i] = list[i];
                o[i] = PlayerPrefs.GetInt(Key(r[i]), 0) & FullMask(r[i]);
            }
            rows = r;
            owned = o;
            percent = null;
        }

        static int FullMask(DexRow row)
        {
            int n = Mathf.Clamp(row.count, 0, MaxPerGroup);
            return n >= 31 ? int.MaxValue : (1 << n) - 1;
        }

        static int PopCount(int v)
        {
            int c = 0;
            for (; v != 0; v &= v - 1) c++;
            return c;
        }

        // 무리 목록 (종류·별 순서)
        public static IReadOnlyList<DexRow> Groups { get { Ensure(); return rows; } }

        static int Slot(DexRow row)
        {
            Ensure();
            return Array.IndexOf(rows, row);
        }

        public static DexRow Group(MushroomKind kind, int star)
        {
            Ensure();
            foreach (var r in rows) if (r.kind == kind && r.star == star) return r;
            return null;
        }

        public static bool Has(DexRow row, int no)
        {
            int s = Slot(row);
            return s >= 0 && no >= 0 && no < row.count && (owned[s] & (1 << no)) != 0;
        }

        public static int CollectedCount(DexRow row)
        {
            int s = Slot(row);
            return s >= 0 ? PopCount(owned[s]) : 0;
        }

        public static bool IsComplete(DexRow row) => row != null && row.count > 0 && CollectedCount(row) >= row.count;

        public static int TotalCollected
        {
            get
            {
                Ensure();
                int n = 0;
                foreach (int o in owned) n += PopCount(o);
                return n;
            }
        }

        // 켜진 세트의 그 능력치 % 합
        public static double Percent(DexStat stat)
        {
            Ensure();
            if (percent == null)
            {
                var p = new double[Enum.GetValues(typeof(DexStat)).Length];
                foreach (var set in GameBalance.DexSets)
                    if ((int)set.stat >= 0 && (int)set.stat < p.Length && IsComplete(Group(set.kind, set.star))) p[(int)set.stat] += set.value;
                percent = p;
            }
            return percent[(int)stat];
        }

        // 그 무리의 세트 효과 행들
        public static List<DexSetRow> SetEffects(DexRow row)
        {
            var list = new List<DexSetRow>();
            if (row == null) return list;
            foreach (var set in GameBalance.DexSets) if (set.kind == row.kind && set.star == row.star) list.Add(set);
            return list;
        }

        // 도감 칸 그림 (시트 조각 "시트이름_no")
        public static Sprite Sprite(DexRow row, int no)
        {
            int s = Slot(row);
            if (s < 0 || no < 0 || no >= row.count) return null;
            if (sprites == null) sprites = new Sprite[rows.Length][];
            if (sprites[s] == null)
            {
                var cells = new Sprite[row.count];
                string path = GameBalance.ResourcePath(row.resourceId);
                if (path != null)
                {
                    string sheet = path.Substring(path.LastIndexOf('/') + 1) + "_";
                    foreach (var sp in Resources.LoadAll<Sprite>(path))
                        if (sp.name.StartsWith(sheet, StringComparison.Ordinal) && int.TryParse(sp.name.Substring(sheet.Length), out int i) && i >= 0 && i < cells.Length) cells[i] = sp;
                }
                sprites[s] = cells;
            }
            return sprites[s][no];
        }

        public struct Drop
        {
            public DexRow group;
            public int no;
            public bool completedSet;   // 이 버섯으로 무리를 다 모았다
        }

        static readonly List<Drop> drops = new List<Drop>();
        static readonly List<int> missing = new List<int>();

        // 방치 전투 보스(stage)를 잡았다: 열린 무리마다 굴려 새 버섯을 얻는다. 얻은 것 목록 (재사용 리스트 — 다음 호출 전까지만 쓴다)
        public static List<Drop> RollBossDrop(int stage)
        {
            Ensure();
            drops.Clear();
            for (int s = 0; s < rows.Length; s++)
            {
                var row = rows[s];
                if (stage < row.unlockStage || UnityEngine.Random.value >= row.dropChance) continue;
                missing.Clear();
                for (int i = 0; i < row.count && i < MaxPerGroup; i++) if ((owned[s] & (1 << i)) == 0) missing.Add(i);
                if (missing.Count == 0) continue;
                int no = missing[UnityEngine.Random.Range(0, missing.Count)];
                Set(s, owned[s] | (1 << no));
                drops.Add(new Drop { group = row, no = no, completedSet = missing.Count == 1 });
            }
            if (drops.Count > 0) Changed?.Invoke();
            return drops;
        }

        static void Set(int slot, int bits)
        {
            owned[slot] = bits & FullMask(rows[slot]);
            PlayerPrefs.SetInt(Key(rows[slot]), owned[slot]);
            SaveScheduler.MarkDirty();
            percent = null;
        }

        // 개발용: 무리 하나를 다 채우거나 비운다
        public static void DevSetGroup(DexRow row, bool full)
        {
            int s = Slot(row);
            if (s < 0) return;
            Set(s, full ? FullMask(row) : 0);
            Changed?.Invoke();
        }

        // 화면 표시: "식용 ★3"
        public static string GroupLabel(DexRow row) => row == null ? "" : $"{KindName(row.kind)} {new string('★', Mathf.Clamp(row.star, 0, MaxStar))}";

        public static string KindName(MushroomKind kind) => Loc.T(kind == MushroomKind.Edible ? "dex.kind_edible" : kind == MushroomKind.Medicinal ? "dex.kind_medicinal" : "dex.kind_toxic");

        public static string StatName(DexStat stat) => Loc.T("upgrade." + stat);
    }
}
