using UnityEngine;

namespace MoreMush.Mobile
{
    // 꼬마 편성: 칸 SlotCount개에 꼬마 id 하나씩(빈 칸 = ""). 칸 i = Main 전투의 Ggoma(i+1) 자리.
    // 꼬마는 꼬마 뽑기(GachaKind.Character)로 얻는다. 기본 편성 5명(Defaults)은 처음부터 1장씩 가진다.
    // 가진 꼬마만 편성할 수 있고, 저장된 칸의 꼬마를 안 가졌으면 빈 칸으로 본다.
    // PlayerPrefs("Formation.<칸>", "Formation.Owned.<id>")에 저장한다 (세이브 구조가 생기면 옮긴다)
    public static class Formation
    {
        public const int SlotCount = 5;
        public const string IconPath = "Art/Dream/Characters/Critters/Ggomas/icon_";   // 임시 그림

        // 꼬마 8종 (이름은 MoreMustMush 꼬마 이름) + 보스 그림을 쓰는 캐릭터
        public static readonly string[] Ids = { "leaf", "dew", "wind", "fire", "rock", "star", "coin", "chest", "beak" };
        static readonly string[] Names = { "새싹 꼬마", "이슬 꼬마", "바람 꼬마", "불씨 꼬마", "조약돌 꼬마", "별똥 꼬마", "동전 꼬마", "꿀밤 꼬마", "부리 버섯" };

        // 캐릭터 능력치 (임시값): 등급(GachaProgress.GradeNames 번호)과 공격 배율 — 이 꼬마가 던지는 수확기 피해 = 공격력 × 배율.
        // 가장 강한 5명의 평균이 약 1.0이 되게 잡았다 (밸런스 곡선이 배율 1.0 기준) — 기본 편성은 약 0.88
        static readonly int[] Grades = { 0, 0, 1, 2, 1, 3, 1, 2, 4 };
        static readonly double[] AttackMuls = { 0.8, 0.8, 0.9, 1.0, 0.9, 1.1, 0.9, 1.0, 1.2 };

        // IconPath + id가 아닌 곳에 그림이 있는 캐릭터 (Resources 경로)
        static readonly System.Collections.Generic.Dictionary<string, string> IconOverrides = new System.Collections.Generic.Dictionary<string, string>
        {
            { "beak", "Art/Dream/Characters/Critters/Bosses/boss1" },
        };
        static readonly string[] Defaults = { "leaf", "dew", "fire", "rock", "coin" };

        public static event System.Action Changed;

        public static string Get(int slot)
        {
            string id = PlayerPrefs.GetString("Formation." + slot, Defaults[slot]);
            return IsOwned(id) ? id : "";
        }

        // 가진 장수 (기본 편성 꼬마는 처음부터 1장)
        public static int OwnedCount(string id)
        {
            if (System.Array.IndexOf(Ids, id) < 0) return 0;
            return Mathf.Max(0, PlayerPrefs.GetInt("Formation.Owned." + id, System.Array.IndexOf(Defaults, id) >= 0 ? 1 : 0));
        }

        public static bool IsOwned(string id) => OwnedCount(id) > 0;

        // 꼬마 뽑기 결과를 더한다 (같은 꼬마를 또 뽑으면 장수만 는다)
        public static void AddOwned(System.Collections.Generic.IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                if (System.Array.IndexOf(Ids, id) < 0) continue;
                PlayerPrefs.SetInt("Formation.Owned." + id, OwnedCount(id) + 1);
            }
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 이 등급의 꼬마 id들 (꼬마 뽑기에서 등급을 정한 뒤 그 안에서 고른다)
        public static System.Collections.Generic.List<string> IdsOfGrade(int grade)
        {
            var list = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Ids.Length; i++) if (Grades[i] == grade) list.Add(Ids[i]);
            return list;
        }

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
            if (!IsOwned(id)) return;
            string old = Get(slot);
            if (old == id) { Clear(slot); return; }
            int from = SlotOf(id);
            if (from >= 0) PlayerPrefs.SetString("Formation." + from, old);
            PlayerPrefs.SetString("Formation." + slot, id);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Clear(int slot)
        {
            if (IsEmpty(slot)) return;
            PlayerPrefs.SetString("Formation." + slot, "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 자동 편성: 가진 꼬마 중 공격 배율이 높은 순(같으면 등급 높은 순, 그다음 목록 순)으로 SlotCount명을 1번 칸부터 놓는다
        public static void AutoArrange()
        {
            var order = new System.Collections.Generic.List<string>(Ids);
            order.RemoveAll(id => !IsOwned(id));
            order.Sort((a, b) =>
            {
                int c = AttackMultiplier(b).CompareTo(AttackMultiplier(a));
                if (c == 0) c = Grade(b).CompareTo(Grade(a));
                return c != 0 ? c : System.Array.IndexOf(Ids, a).CompareTo(System.Array.IndexOf(Ids, b));
            });
            for (int i = 0; i < SlotCount; i++) PlayerPrefs.SetString("Formation." + i, i < order.Count ? order[i] : "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 빈 칸·모르는 id는 0 (던지지 않는다)
        public static double AttackMultiplier(string id)
        {
            int i = System.Array.IndexOf(Ids, id);
            return i >= 0 ? AttackMuls[i] : 0;
        }

        public static int Grade(string id)
        {
            int i = System.Array.IndexOf(Ids, id);
            return i >= 0 ? Grades[i] : 0;
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
            return i >= 0 ? Names[i] : id;
        }

        public static Sprite Icon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Resources.Load<Sprite>(IconOverrides.TryGetValue(id, out var path) ? path : IconPath + id);
        }
    }
}
