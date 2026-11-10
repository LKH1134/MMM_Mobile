using System;
using UnityEngine;

namespace MoreMush.Mobile
{
    // Dungeon = 던전 한 판(입장 횟수를 쓴 판), Daily = 일일 퀘스트를 받은 수 (주간 퀘스트에만 쓴다)
    public enum QuestKind { Kill, Stage, Upgrade, Gacha, Ad, Dungeon, Daily }

    public enum QuestPeriod { Daily, Weekly }

    // 일일·주간 퀘스트: 퀘스트 시트의 행마다 목표 하나 (period daily/weekly). 게임 쪽에서 Add(종류)로 진행도를 올리고, 다 채우면 창에서 보상(골드·균사석·세계수 경험치)을 받는다.
    // 일일은 한국 시각(UTC+9) 00시, 주간은 월요일 한국 00시에 진행도와 수령 상태가 처음으로 돌아간다 (DungeonTickets와 같은 '뒤의 날만 새날' 규칙).
    // 저장: 일일 PlayerPrefs("Quest.Day" = 마지막으로 센 한국 날짜, "Quest.<일일 순번>.P" = 진행도, "Quest.<일일 순번>.C" = 수령 여부 — 주간이 생기기 전과 같은 키)
    //       주간 PlayerPrefs("QuestW.Week" = 이번 주 월요일 yyyyMMdd, "QuestW.<행 index>.P"·".C")
    // 보상 골드 = 받을 때 스테이지 적 처치 골드 × 시트의 goldKills. 앱 밖에서 센 처치·스테이지는 세지 않는다.
    // 11-10: 세계수 의뢰(WorldTree.Quests)를 여기로 합쳤다 — 세계수 경험치는 treeExp로 받는다
    public static class DailyQuests
    {
        sealed class Set
        {
            public readonly QuestPeriod period;
            public QuestRow[] rows;
            public QuestKind[] kinds;
            public int[] progress;
            public bool[] claimed;
            public DateTime nextResetUtc;
            public bool loaded;

            public Set(QuestPeriod period) { this.period = period; }

            bool Weekly => period == QuestPeriod.Weekly;
            public string StampKey => Weekly ? "QuestW.Week" : "Quest.Day";
            public string Stamp => Weekly ? DungeonTickets.ThisWeek : DungeonTickets.Today;
            public bool IsNew(string stored) => Weekly ? DungeonTickets.IsNewWeek(stored) : DungeonTickets.IsNewDay(stored);
            public TimeSpan UntilReset => Weekly ? DungeonTickets.UntilWeekReset : DungeonTickets.UntilReset;
            // 일일은 예전 세이브와 맞게 일일 행 순번, 주간은 행 index (행을 끼워 넣어도 다른 행 진행이 섞이지 않게)
            public string ProgressKey(int i) => Weekly ? $"QuestW.{rows[i].index}.P" : $"Quest.{i}.P";
            public string ClaimedKey(int i) => Weekly ? $"QuestW.{rows[i].index}.C" : $"Quest.{i}.C";
        }

        public static event Action Changed;

        static readonly Set daily = new Set(QuestPeriod.Daily), weekly = new Set(QuestPeriod.Weekly);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            foreach (var s in new[] { daily, weekly }) { s.loaded = false; s.rows = null; }
        }

        static Set Of(QuestPeriod p)
        {
            var s = p == QuestPeriod.Weekly ? weekly : daily;
            Ensure(s);
            return s;
        }

        static void Ensure(Set s)
        {
            if (s.loaded && GameClock.UtcNow < s.nextResetUtc) return;
            bool rollover = s.loaded;     // 화면을 켠 채 00시(주간은 월요일 00시)를 넘겼다
            Load(s);
            if (rollover) Changed?.Invoke();
        }

        static void Load(Set s)
        {
            string period = s.period == QuestPeriod.Weekly ? "weekly" : "daily";
            var list = new System.Collections.Generic.List<QuestRow>();
            foreach (var r in GameBalance.Quests)
                if (string.Equals(r.period, period, StringComparison.OrdinalIgnoreCase)) list.Add(r);
            int n = list.Count;
            s.rows = list.ToArray();
            s.kinds = new QuestKind[n];
            s.progress = new int[n];
            s.claimed = new bool[n];
            bool fresh = s.IsNew(PlayerPrefs.GetString(s.StampKey, ""));
            for (int i = 0; i < n; i++)
            {
                if (!Enum.TryParse(s.rows[i].kind, true, out s.kinds[i])) Debug.LogError($"[DailyQuests] 모르는 퀘스트 종류 '{s.rows[i].kind}'");
                if (fresh)
                {
                    PlayerPrefs.DeleteKey(s.ProgressKey(i));
                    PlayerPrefs.DeleteKey(s.ClaimedKey(i));
                    continue;
                }
                s.progress[i] = Mathf.Clamp(PlayerPrefs.GetInt(s.ProgressKey(i), 0), 0, s.rows[i].target);
                s.claimed[i] = PlayerPrefs.GetInt(s.ClaimedKey(i), 0) != 0;
            }
            if (fresh)
            {
                PlayerPrefs.SetString(s.StampKey, s.Stamp);
                SaveScheduler.MarkDirty();
            }
            s.nextResetUtc = GameClock.UtcNow + s.UntilReset;
            s.loaded = true;
        }

        public static int Count(QuestPeriod p) => Of(p).rows.Length;
        public static QuestKind Kind(QuestPeriod p, int i) => Of(p).kinds[i];
        public static int Target(QuestPeriod p, int i) => Of(p).rows[i].target;
        public static int Progress(QuestPeriod p, int i) => Of(p).progress[i];
        public static bool IsDone(QuestPeriod p, int i) { var s = Of(p); return s.progress[i] >= s.rows[i].target; }
        public static bool IsClaimed(QuestPeriod p, int i) => Of(p).claimed[i];
        public static bool CanClaim(QuestPeriod p, int i) => IsDone(p, i) && !IsClaimed(p, i);
        public static long RewardMycelite(QuestPeriod p, int i) => Of(p).rows[i].mycelite;
        public static long RewardTreeExp(QuestPeriod p, int i) => Of(p).rows[i].treeExp;

        // 지금 스테이지 기준 보상 골드 (받는 순간의 스테이지로 정한다)
        public static long RewardGold(QuestPeriod p, int i)
        {
            double gold = (double)IdleProgress.EnemyGold(IdleProgress.Stage) * Of(p).rows[i].goldKills;
            return gold >= 9e18 ? 9000000000000000000L : (long)Math.Max(0, gold);
        }

        public static int ClaimableCountOf(QuestPeriod p)
        {
            var s = Of(p);
            int n = 0;
            for (int i = 0; i < s.rows.Length; i++) if (!s.claimed[i] && s.progress[i] >= s.rows[i].target) n++;
            return n;
        }

        // 받을 수 있는 퀘스트 수, 일일 + 주간 (버튼의 빨간 점)
        public static int ClaimableCount => ClaimableCountOf(QuestPeriod.Daily) + ClaimableCountOf(QuestPeriod.Weekly);

        // 다음 초기화까지 남은 시간 (일일 = 다음 한국 00시, 주간 = 다음 월요일 한국 00시)
        public static TimeSpan UntilReset(QuestPeriod p) => p == QuestPeriod.Weekly ? DungeonTickets.UntilWeekReset : DungeonTickets.UntilReset;

        // 이 종류의 퀘스트 진행도를 올린다 (일일·주간 모두, 목표까지만). 이미 받은 퀘스트는 올리지 않는다
        public static void Add(QuestKind kind, int amount = 1)
        {
            if (amount <= 0) return;
            bool changed = AddTo(Of(QuestPeriod.Daily), kind, amount) | AddTo(Of(QuestPeriod.Weekly), kind, amount);
            if (!changed) return;
            SaveScheduler.MarkDirty();      // 처치마다 불린다 — 파일 쓰기는 SaveScheduler가 모아서
            Changed?.Invoke();
        }

        static bool AddTo(Set s, QuestKind kind, int amount)
        {
            bool changed = false;
            for (int i = 0; i < s.rows.Length; i++)
            {
                if (s.kinds[i] != kind || s.claimed[i] || s.progress[i] >= s.rows[i].target) continue;
                s.progress[i] = Mathf.Min(s.rows[i].target, s.progress[i] + amount);
                PlayerPrefs.SetInt(s.ProgressKey(i), s.progress[i]);
                changed = true;
            }
            return changed;
        }

        public static bool Claim(QuestPeriod p, int i)
        {
            if (!CanClaim(p, i)) return false;
            var s = Of(p);
            s.claimed[i] = true;
            PlayerPrefs.SetInt(s.ClaimedKey(i), 1);
            Wallet.Add(Currency.Gold, RewardGold(p, i));
            Wallet.Add(Currency.Mycelite, RewardMycelite(p, i));
            long exp = RewardTreeExp(p, i);
            if (exp > 0) WorldTree.AddQuestExp(exp);
            SaveScheduler.MarkDirty();
            if (p == QuestPeriod.Daily) Add(QuestKind.Daily);      // 주간 "일일 의뢰 n개 완료"
            Changed?.Invoke();
            return true;
        }

        // 일괄 수령: 받을 수 있는 퀘스트를 모두 Claim으로 받는다 (개별 수령과 같은 지급·저장·알림). 받은 개수.
        // 일일을 먼저 받는다 — 일일을 받으면 주간 "일일 의뢰 n개"가 채워져 같은 번에 받을 수 있다
        public static int ClaimAll()
        {
            int n = 0;
            foreach (var p in new[] { QuestPeriod.Daily, QuestPeriod.Weekly })
                for (int i = 0; i < Count(p); i++) if (Claim(p, i)) n++;
            return n;
        }

        public static string Describe(QuestPeriod p, int i)
        {
            int n = Target(p, i);
            switch (Kind(p, i))
            {
                case QuestKind.Kill: return Loc.F("quest.desc_kill", n);
                case QuestKind.Stage: return Loc.F("quest.desc_stage", n);
                case QuestKind.Upgrade: return Loc.F("quest.desc_upgrade", n);
                case QuestKind.Gacha: return Loc.F("quest.desc_gacha", n);
                case QuestKind.Dungeon: return Loc.F("quest.desc_dungeon", n);
                case QuestKind.Daily: return Loc.F("quest.desc_daily", n);
                default: return Loc.F("quest.desc_ad", n);
            }
        }
    }
}
