using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 의뢰 (일일·주간): PC MoreMustMush Game.Quests를 방치형에 맞게 옮겼다. 완료하고 받으면 세계수 경험치.
    // 일일 = 의뢰 시트 daily 행에서 하루(KST) treeQuestDaily개를 뽑는다, 주간 = weekly 행 전부 (월요일 시작). 날짜는 '뒤의 날'만 새로 뽑는다 (DungeonTickets.IsNewDay).
    // 종류(kind): stage 보스 도전(보스가 나올 때 — 벽에서 막혀도 센다, 메인 일일 퀘스트 Stage와 같음) · kill 일반 적 처치 · species 지정한 적 처치 · dungeon 던전 1 판 수 · daily 일일 의뢰 받은 수.
    // 진행은 라이브 플레이만 센다 (앱 밖 정산분 제외, 창고 버섯과 같음). 의뢰마다 의뢰인 꼬마(가진 꼬마 중) 하나를 정해 화면에 보여 준다
    public static partial class WorldTree
    {
        [Serializable] public class Quest { public int row; public int arg; public string kid = ""; public int prog; public bool claimed; }

        public static int DailyQuestCount => GameBalance.ConstInt("treeQuestDaily");

        public static IReadOnlyList<Quest> DailyQuests { get { EnsureQuests(); return D.questDaily; } }
        public static IReadOnlyList<Quest> WeeklyQuests { get { EnsureQuests(); return D.questWeekly; } }

        // 이번 주가 끝나는 때 (다음 월요일 한국 00시)까지
        public static TimeSpan UntilWeekReset
        {
            get
            {
                var now = DungeonTickets.KstNow;
                int days = 7 - ((int)now.DayOfWeek + 6) % 7;
                return now.Date.AddDays(days) - now;
            }
        }

        public static TreeQuestRow QuestRowOf(Quest q)
        {
            foreach (var r in GameBalance.TreeQuests) if (r.index == q.row) return r;
            return null;
        }

        public static bool QuestDone(Quest q) => QuestRowOf(q) is TreeQuestRow r && q.prog >= r.target;
        public static bool QuestClaimable(Quest q) => !q.claimed && QuestDone(q);

        // 설명 글: 표 desc의 {n} = 목표, {m} = 지정한 적 이름
        public static string QuestText(Quest q)
        {
            var r = QuestRowOf(q);
            if (r == null) return "";
            return (r.desc ?? "").Replace("{n}", Count(r.target)).Replace("{m}", r.kind == "species" ? EnemyName(q.arg) : "");
        }

        // 의뢰 숫자 (목표·진행·경험치): 1,500처럼 쉼표 (Wallet.Format은 1.5a 같은 재화 단위)
        public static string Count(long v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        // 날짜·주가 바뀌었으면 새로 뽑는다. 화면을 열 때·진행을 셀 때 부른다
        static void EnsureQuests()
        {
            var d = D;
            bool changed = false;
            if (DungeonTickets.IsNewDay(d.questDay) || d.questDaily.Count == 0)
            {
                if (RollDaily()) { d.questDay = Today; changed = true; }
            }
            if (IsNewWeek(d.questWeek) || d.questWeekly.Count == 0)
            {
                d.questWeek = ThisWeek;
                d.questWeekly.Clear();
                foreach (var r in GameBalance.TreeQuests)
                    if (r.period == "weekly") d.questWeekly.Add(new Quest { row = r.index, kid = PickKid() });
                changed = true;
            }
            if (changed) MarkDirty();
        }

        // 저장된 주(월요일 yyyyMMdd)보다 이번 주가 뒤면 새 주. 저장값이 크게 앞서 있으면 틀린 시계였던 것으로 본다 (DungeonTickets.IsNewDay와 같은 규칙)
        static bool IsNewWeek(string storedWeek)
        {
            if (!DateTime.TryParseExact(storedWeek, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var stored)) return true;
            var monday = DateTime.ParseExact(ThisWeek, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            return monday > stored || stored - DungeonTickets.KstNow.Date > IdleAway.MaxClockSkew;
        }

        static bool RollDaily()
        {
            var pool = new List<TreeQuestRow>();
            foreach (var r in GameBalance.TreeQuests)
                if (r.period == "daily" && (r.kind != "species" || GameBalance.Enemies.Count > 0)) pool.Add(r);
            if (pool.Count == 0) return false;
            D.questDaily.Clear();
            while (D.questDaily.Count < DailyQuestCount && pool.Count > 0)
            {
                var r = pool[UnityEngine.Random.Range(0, pool.Count)];
                pool.Remove(r);
                int arg = r.kind == "species" ? GameBalance.Enemies[UnityEngine.Random.Range(0, GameBalance.Enemies.Count)].index : 0;
                D.questDaily.Add(new Quest { row = r.index, arg = arg, kid = PickKid() });
            }
            return true;
        }

        static string PickKid()
        {
            var kids = new List<string>(OwnedKids);
            return kids.Count > 0 ? kids[UnityEngine.Random.Range(0, kids.Count)] : "";
        }

        // ── 진행 ──
        static long nextEnsureTicks;

        // 처치마다 오므로 날짜 확인(EnsureQuests)은 1초에 한 번만, 목록 배열도 만들지 않는다
        static void AddQuest(string kind, int arg, int amount)
        {
            if (amount <= 0) return;
            if (NowTicks >= nextEnsureTicks)
            {
                EnsureQuests();
                nextEnsureTicks = NowTicks + TimeSpan.TicksPerSecond;
            }
            bool changed = AddTo(D.questDaily, kind, arg, amount) | AddTo(D.questWeekly, kind, arg, amount);
            if (changed) MarkDirty();      // 처치마다 오므로 화면은 1초마다 스스로 갱신한다
        }

        static bool AddTo(List<Quest> list, string kind, int arg, int amount)
        {
            bool changed = false;
            foreach (var q in list)
            {
                var r = QuestRowOf(q);
                if (r == null || q.claimed || r.kind != kind || q.prog >= r.target) continue;
                if (kind == "species" && q.arg != arg) continue;
                q.prog = (int)Math.Min(r.target, (long)q.prog + amount);
                changed = true;
            }
            return changed;
        }

        static void QuestKill(int enemy)
        {
            AddQuest("kill", 0, 1);
            AddQuest("species", enemy, 1);
        }

        // 방치 전투에서 보스에게 도전했다 (보스가 나올 때, IdleBattle.StartWave)
        public static void QuestStage() => AddQuest("stage", 0, 1);

        // 던전 1(수확 판)을 한 판 했다 (HarvestGame) — 입장 횟수를 쓴 것만.
        // PC는 '한 판 콤보'였으나 모바일 던전 1은 레벨 5부터 한 판에 1~3개만 캐서 콤보가 닿지 않는다 (11-03 실측)
        public static void QuestDungeon() => AddQuest("dungeon", 0, 1);

        // ── 받기 ──
        public static bool ClaimQuest(bool weekly, int i)
        {
            EnsureQuests();
            var list = weekly ? D.questWeekly : D.questDaily;
            if (i < 0 || i >= list.Count || !QuestClaimable(list[i])) return false;
            var q = list[i];
            q.claimed = true;
            D.exp += QuestRowOf(q).exp;
            if (!weekly) AddDailyDone();
            Notify(true);
            return true;
        }

        // 일일 의뢰를 받았다 → 주간 "일일 의뢰 n개" 진행
        static void AddDailyDone()
        {
            foreach (var q in D.questWeekly)
            {
                var r = QuestRowOf(q);
                if (r != null && !q.claimed && r.kind == "daily") q.prog = Math.Min(r.target, q.prog + 1);
            }
        }
    }
}
