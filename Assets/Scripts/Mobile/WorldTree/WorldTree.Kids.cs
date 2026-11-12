using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 세계수 꼬마 = 뽑기 꼬마(Formation). 가진 꼬마만 다룬다.
    // 진화(★): 균사석 + 꼬마 레벨 + 세계수 단계. ★가 특기(bonusEffect × 단계)를 올린다.
    // 부탁: 하루(KST) requestPerDay건. 창고 버섯을 주면 골드, 이번 주 requestWeekGoal건이면 주간 상자(균사석)
    public static partial class WorldTree
    {
        public enum KidFail { None, NotOwned, Max, Level, Stage, Mycelite }

        public static IEnumerable<string> OwnedKids
        {
            get
            {
                foreach (var id in Formation.Ids) if (Formation.IsOwned(id)) yield return id;
            }
        }

        public static MascotRow KidRow(string id)
        {
            foreach (var m in GameBalance.Mascots) if (m.key == id) return m;
            return null;
        }

        static KidState Kid(string id)
        {
            foreach (var k in D.kids) if (k.id == id) return k;
            var s = new KidState { id = id };
            D.kids.Add(s);
            return s;
        }

        static KidState FindKid(string id)
        {
            foreach (var k in D.kids) if (k.id == id) return k;
            return null;
        }

        // ── 진화 ★ ──
        public static int MaxStar => GameBalance.KidStars.Count - 1;
        public static int Star(string id) => Mathf.Clamp(FindKid(id)?.star ?? 0, 0, MaxStar);
        static KidStarRow StarRow(int star) => GameBalance.KidStars[Mathf.Clamp(star, 0, MaxStar)];

        // 다음 ★에 필요한 세계수 단계
        public static int EvoStageNeeded(string id)
        {
            var row = KidRow(id);
            return Mathf.Min(MaxStage, (row != null ? row.evoStage : 1) + Star(id) + 1);
        }

        // 필요 레벨 = 꼬마진화 시트 needLevel (꼬마 등급이 없어져 모든 꼬마가 같다)
        public static int EvoLevelNeeded(string id) => Mathf.Max(1, StarRow(Star(id) + 1).needLevel);
        public static int EvoMyceliteCost(string id) => StarRow(Star(id) + 1).mycelite;

        public static KidFail CanKidEvolve(string id)
        {
            if (!Formation.IsOwned(id)) return KidFail.NotOwned;
            if (Star(id) >= MaxStar) return KidFail.Max;
            if (Formation.Level(id) < EvoLevelNeeded(id)) return KidFail.Level;
            if (Stage < EvoStageNeeded(id)) return KidFail.Stage;
            if (!Wallet.CanAfford(Currency.Mycelite, EvoMyceliteCost(id))) return KidFail.Mycelite;
            return KidFail.None;
        }

        public static bool KidEvolve(string id)
        {
            if (CanKidEvolve(id) != KidFail.None) return false;
            if (!Wallet.TrySpend(Currency.Mycelite, EvoMyceliteCost(id))) return false;
            Kid(id).star++;
            Notify(true);
            return true;
        }

        // 특기 단계 = ★ + (장식 세트면 1)
        public static int KidStep(string id) => Star(id) + (DecoSetComplete(id) ? 1 : 0);

        // ── 부탁 ──
        public static IReadOnlyList<Request> Requests
        {
            get
            {
                EnsureRequests();
                return D.requests;
            }
        }

        public static int WeekDone { get { EnsureRequests(); return D.weekDone; } }
        public static bool WeekClaimed { get { EnsureRequests(); return D.weekClaimed; } }
        public static int WeekGoal => GameBalance.ConstInt("requestWeekGoal");
        public static int WeekMycelite => GameBalance.ConstInt("requestWeekMycelite");
        public static long RequestGold => KillGold(GameBalance.Const("requestGoldKills"));

        // 날이 바뀌었거나 비어 있으면 새로 뽑는다. 주가 바뀌면 주간 상자를 처음으로
        static void EnsureRequests()
        {
            var d = D;
            string week = ThisWeek;
            bool changed = false;
            if (d.requestWeek != week)
            {
                d.requestWeek = week;
                d.weekDone = 0;
                d.weekClaimed = false;
                changed = true;
            }
            if (d.requestDay != Today || d.requests.Count == 0)
            {
                if (RollRequests()) { d.requestDay = Today; changed = true; }
            }
            if (changed) MarkDirty();
        }

        static bool RollRequests()
        {
            var kids = new List<string>(OwnedKids);
            if (kids.Count == 0 || GameBalance.Enemies.Count == 0) return false;
            D.requests.Clear();
            var pool = new List<string>();
            for (int i = 0; i < GameBalance.ConstInt("requestPerDay"); i++)
            {
                if (pool.Count == 0) pool.AddRange(kids);
                string kid = pool[UnityEngine.Random.Range(0, pool.Count)];
                pool.Remove(kid);
                var fav = KidRow(kid)?.favoriteIds;
                int enemy = fav != null && fav.Length > 0
                    ? fav[UnityEngine.Random.Range(0, fav.Length)]
                    : GameBalance.Enemies[UnityEngine.Random.Range(0, GameBalance.Enemies.Count)].index;
                D.requests.Add(new Request { kid = kid, enemy = enemy, count = Math.Max(1, Enemy(enemy)?.requestCount ?? 1) });
            }
            return true;
        }

        public static bool CanDoRequest(int i)
        {
            var list = Requests;
            if (i < 0 || i >= list.Count || list[i].done) return false;
            return StoreCount(list[i].enemy) >= list[i].count;
        }

        public static bool DoRequest(int i)
        {
            if (!CanDoRequest(i)) return false;
            var r = D.requests[i];
            AddStore(r.enemy, -r.count);
            r.done = true;
            Wallet.Add(Currency.Gold, RequestGold);
            D.weekDone++;
            Notify(true);
            return true;
        }

        public static bool CanClaimWeek => WeekDone >= WeekGoal && !WeekClaimed;

        public static bool ClaimWeek()
        {
            if (!CanClaimWeek) return false;
            D.weekClaimed = true;
            Wallet.Add(Currency.Mycelite, WeekMycelite);
            Notify(true);
            return true;
        }
    }
}
