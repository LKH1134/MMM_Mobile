using UnityEngine;

namespace MoreMush.Mobile
{
    // 꼬마 편성: 칸 SlotCount개에 꼬마 id 하나씩(빈 칸 = ""). 칸 i = Main 전투의 Ggoma(i+1) 자리.
    // PlayerPrefs("Formation.<칸>")에 저장한다 (세이브 구조가 생기면 옮긴다)
    public static class Formation
    {
        public const int SlotCount = 5;
        public const string IconPath = "Art/Dream/Characters/Critters/Ggomas/icon_";   // 임시 그림

        // 꼬마 8종 (이름은 MoreMustMush 꼬마 이름)
        public static readonly string[] Ids = { "leaf", "dew", "wind", "fire", "rock", "star", "coin", "chest" };
        static readonly string[] Names = { "새싹 꼬마", "이슬 꼬마", "바람 꼬마", "불씨 꼬마", "조약돌 꼬마", "별똥 꼬마", "동전 꼬마", "꿀밤 꼬마" };
        static readonly string[] Defaults = { "leaf", "dew", "fire", "rock", "coin" };

        public static event System.Action Changed;

        public static string Get(int slot) => PlayerPrefs.GetString("Formation." + slot, Defaults[slot]);

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

        public static string Name(string id)
        {
            int i = System.Array.IndexOf(Ids, id);
            return i >= 0 ? Names[i] : id;
        }

        public static Sprite Icon(string id) => string.IsNullOrEmpty(id) ? null : Resources.Load<Sprite>(IconPath + id);
    }
}
