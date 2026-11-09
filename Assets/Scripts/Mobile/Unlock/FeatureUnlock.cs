using UnityEngine;

namespace MoreMush.Mobile
{
    // 해금되는 기능 (하단 탭). 순서 = 안내 팝업이 뜨는 순서
    public enum Feature { Team, Gacha, Dungeon, WorldTree }

    // 기능 해금: 지금 싸우는 스테이지가 해금 스테이지 이상이 되면 한 번 열리고, 그 뒤로는 스테이지가 내려가도 다시 잠기지 않는다 (PlayerPrefs "Unlock.<기능>").
    //   편성 = 1-2를 깨면(= 1-3 진입) · 소환 = 편성 화면에 처음 들어가면 · 던전 = 던전 시트에서 가장 먼저 열리는 던전의 unlockStage · 세계수 = WorldTreeStage
    // 열린 뒤에는 한 번 "새로운 콘텐츠!" 안내(Seen)를 보여 주고 그 탭을 가리킨다. 그 탭을 처음 열 때까지(Opened) 탭이 반짝인다 (UnlockAnnouncer·BottomBarLocks)
    public static class FeatureUnlock
    {
        // 해금 스테이지 (스테이지 번호 1부터. 1-2를 깨고 1-3에 들어가면 스테이지 3 — HUD 표기는 IdleBattle.HudStageNumber)
        public const int TeamStage = 3, WorldTreeStage = 7;
        const int FallbackDungeonStage = 5;

        public static readonly Feature[] All = { Feature.Team, Feature.Gacha, Feature.Dungeon, Feature.WorldTree };

        public static event System.Action Changed;

        static bool[] unlocked, seen, opened;

        // 스테이지로 열리는 기능의 해금 스테이지. 소환은 스테이지가 아니라 편성 화면을 열어야 열려서 0
        public static int UnlockStage(Feature feature)
        {
            switch (feature)
            {
                case Feature.Team: return TeamStage;
                case Feature.Gacha: return 0;
                case Feature.WorldTree: return WorldTreeStage;
                default: return FirstDungeonStage();
            }
        }

        // 조건을 채웠는가: 소환 = 편성 탭을 열어 봤다, 나머지 = 스테이지 도달
        static bool Reached(int i)
        {
            var feature = (Feature)i;
            if (feature == Feature.Gacha) return opened[(int)Feature.Team];
            return IdleProgress.Stage >= UnlockStage(feature);
        }

        // 던전 시트에서 가장 먼저 열리는 던전의 해금 스테이지 (1 이상). 표를 못 읽으면 기본값
        static int FirstDungeonStage()
        {
            int best = int.MaxValue;
            try
            {
                foreach (var row in GameBalance.Dungeons)
                    if (row.implemented && row.unlockStage >= 0) best = Mathf.Min(best, row.unlockStage);
            }
            catch (System.Exception e) { Debug.LogException(e); }
            return best == int.MaxValue ? FallbackDungeonStage : Mathf.Max(1, best);
        }

        public static bool IsUnlocked(Feature feature)
        {
            Load();
            int i = (int)feature;
            if (unlocked[i]) return true;
            if (!Reached(i)) return false;
            Unlock(i);
            return true;
        }

        // 스테이지가 올랐을 때 새로 열린 기능이 있는지 본다 (있으면 Changed)
        public static void Check()
        {
            Load();
            bool any = false;
            for (int i = 0; i < unlocked.Length; i++)
            {
                if (unlocked[i] || !Reached(i)) continue;
                unlocked[i] = true;
                PlayerPrefs.SetInt(Key("Unlock.", i), 1);
                any = true;
            }
            if (any) { SaveScheduler.MarkDirty(); Changed?.Invoke(); }
        }

        static void Unlock(int i)
        {
            unlocked[i] = true;
            PlayerPrefs.SetInt(Key("Unlock.", i), 1);
            SaveScheduler.MarkDirty();
            Changed?.Invoke();
        }

        public static bool IsAnnounced(Feature feature) { Load(); return seen[(int)feature]; }
        public static bool IsOpened(Feature feature) { Load(); return opened[(int)feature]; }

        public static void MarkAnnounced(Feature feature) => Mark(seen, "Unlock.Seen.", (int)feature);

        public static void MarkOpened(Feature feature)
        {
            Load();
            if (opened[(int)feature]) return;
            Mark(opened, "Unlock.Opened.", (int)feature);
            Check();                // 편성을 열었으면 소환이 열린다
            Changed?.Invoke();
        }

        static void Mark(bool[] flags, string prefix, int i)
        {
            Load();
            if (flags[i]) return;
            flags[i] = true;
            PlayerPrefs.SetInt(Key(prefix, i), 1);
            SaveScheduler.MarkDirty();
        }

        static string Key(string prefix, int i) => prefix + (Feature)i;

        static void Load()
        {
            if (unlocked != null) return;
            int n = System.Enum.GetValues(typeof(Feature)).Length;
            unlocked = new bool[n]; seen = new bool[n]; opened = new bool[n];
            for (int i = 0; i < n; i++)
            {
                unlocked[i] = PlayerPrefs.GetInt(Key("Unlock.", i), 0) == 1;
                seen[i] = PlayerPrefs.GetInt(Key("Unlock.Seen.", i), 0) == 1;
                opened[i] = PlayerPrefs.GetInt(Key("Unlock.Opened.", i), 0) == 1;
            }
        }

        // 저장 기록을 지웠을 때 (DataResetButton.ClearSaves) — 메모리 값도 버린다
        public static void Reload()
        {
            unlocked = seen = opened = null;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            unlocked = seen = opened = null;
            Changed = null;
        }
    }
}
