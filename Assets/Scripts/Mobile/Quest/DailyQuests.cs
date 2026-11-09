using System;
using UnityEngine;

namespace MoreMush.Mobile
{
    public enum QuestKind { Kill, Stage, Upgrade, Gacha, Ad }

    // 일일 퀘스트: 퀘스트 시트의 행마다 하루 목표 하나. 게임 쪽에서 Add(종류)로 진행도를 올리고, 다 채우면 창에서 보상(골드·균사석)을 받는다.
    // 한국 시각(UTC+9) 00시에 진행도와 수령 상태가 처음으로 돌아간다 (DungeonTickets와 같은 날짜 규칙).
    // 저장: PlayerPrefs("Quest.Day" = 마지막으로 센 한국 날짜, "Quest.<i>.P" = 진행도, "Quest.<i>.C" = 수령 여부)
    // 보상 골드 = 지금 스테이지 적 처치 골드 × 시트의 goldKills. 앱 밖에서 센 처치·스테이지는 세지 않는다
    public static class DailyQuests
    {
        const string DayKey = "Quest.Day";

        public static event Action Changed;

        static QuestRow[] rows;
        static QuestKind[] kinds;
        static int[] progress;
        static bool[] claimed;
        static DateTime nextResetUtc;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            loaded = false;
            rows = null;
        }

        static string ProgressKey(int i) => $"Quest.{i}.P";
        static string ClaimedKey(int i) => $"Quest.{i}.C";

        static void Ensure()
        {
            if (loaded && GameClock.UtcNow < nextResetUtc) return;
            bool rollover = loaded;     // 화면을 켠 채 00시를 넘겼다
            Load();
            if (rollover) Changed?.Invoke();
        }

        static void Load()
        {
            var table = GameBalance.Quests;
            rows = new QuestRow[table.Count];
            kinds = new QuestKind[table.Count];
            progress = new int[table.Count];
            claimed = new bool[table.Count];
            bool newDay = DungeonTickets.IsNewDay(PlayerPrefs.GetString(DayKey, ""));
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = table[i];
                if (!Enum.TryParse(table[i].kind, true, out kinds[i])) Debug.LogError($"[DailyQuests] 모르는 퀘스트 종류 '{table[i].kind}'");
                if (newDay)
                {
                    PlayerPrefs.DeleteKey(ProgressKey(i));
                    PlayerPrefs.DeleteKey(ClaimedKey(i));
                    continue;
                }
                progress[i] = Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey(i), 0), 0, rows[i].target);
                claimed[i] = PlayerPrefs.GetInt(ClaimedKey(i), 0) != 0;
            }
            if (newDay)
            {
                PlayerPrefs.SetString(DayKey, DungeonTickets.Today);
                SaveScheduler.MarkDirty();
            }
            nextResetUtc = GameClock.UtcNow + DungeonTickets.UntilReset;
            loaded = true;
        }

        public static int Count { get { Ensure(); return rows.Length; } }
        public static QuestKind Kind(int i) { Ensure(); return kinds[i]; }
        public static int Target(int i) { Ensure(); return rows[i].target; }
        public static int Progress(int i) { Ensure(); return progress[i]; }
        public static bool IsDone(int i) { Ensure(); return progress[i] >= rows[i].target; }
        public static bool IsClaimed(int i) { Ensure(); return claimed[i]; }
        public static bool CanClaim(int i) => IsDone(i) && !IsClaimed(i);
        public static long RewardMycelite(int i) { Ensure(); return rows[i].mycelite; }

        // 지금 스테이지 기준 보상 골드 (받는 순간의 스테이지로 정한다)
        public static long RewardGold(int i)
        {
            Ensure();
            double gold = (double)IdleProgress.EnemyGold(IdleProgress.Stage) * rows[i].goldKills;
            return gold >= 9e18 ? 9000000000000000000L : (long)Math.Max(0, gold);
        }

        // 받을 수 있는 퀘스트 수 (버튼의 빨간 점)
        public static int ClaimableCount
        {
            get
            {
                Ensure();
                int n = 0;
                for (int i = 0; i < rows.Length; i++) if (CanClaim(i)) n++;
                return n;
            }
        }

        // 다음 한국 00시까지 남은 시간
        public static TimeSpan UntilReset => DungeonTickets.UntilReset;

        // 이 종류의 퀘스트 진행도를 올린다 (목표까지만). 이미 받은 퀘스트는 올리지 않는다
        public static void Add(QuestKind kind, int amount = 1)
        {
            if (amount <= 0) return;
            Ensure();
            bool changed = false;
            for (int i = 0; i < rows.Length; i++)
            {
                if (kinds[i] != kind || claimed[i] || progress[i] >= rows[i].target) continue;
                progress[i] = Mathf.Min(rows[i].target, progress[i] + amount);
                PlayerPrefs.SetInt(ProgressKey(i), progress[i]);
                changed = true;
            }
            if (!changed) return;
            SaveScheduler.MarkDirty();      // 처치마다 불린다 — 파일 쓰기는 SaveScheduler가 모아서
            Changed?.Invoke();
        }

        public static bool Claim(int i)
        {
            if (!CanClaim(i)) return false;
            claimed[i] = true;
            PlayerPrefs.SetInt(ClaimedKey(i), 1);
            Wallet.Add(Currency.Gold, RewardGold(i));
            Wallet.Add(Currency.Mycelite, RewardMycelite(i));
            SaveScheduler.MarkDirty();
            Changed?.Invoke();
            return true;
        }

        // 일괄 수령: 받을 수 있는 퀘스트를 모두 Claim으로 받는다 (개별 수령과 같은 지급·저장·알림). 받은 개수
        public static int ClaimAll()
        {
            Ensure();
            int n = 0;
            for (int i = 0; i < rows.Length; i++) if (Claim(i)) n++;
            return n;
        }

        public static string Describe(int i)
        {
            int n = Target(i);
            switch (Kind(i))
            {
                case QuestKind.Kill: return Loc.F("quest.desc_kill", n);
                case QuestKind.Stage: return Loc.F("quest.desc_stage", n);
                case QuestKind.Upgrade: return Loc.F("quest.desc_upgrade", n);
                case QuestKind.Gacha: return Loc.F("quest.desc_gacha", n);
                default: return Loc.F("quest.desc_ad", n);
            }
        }
    }
}
