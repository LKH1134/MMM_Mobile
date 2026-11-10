using System;
using UnityEngine;

namespace MoreMush.Mobile
{
    public enum AttendanceRewardKind { Gold, Mycelite, Weapon }

    // 출석 체크 이벤트: 7일. 하루 = 한국 시간 00:00:00 기준 (DungeonTickets.Today, 게임 시계라 기기 시계를 되돌려도 다시 받지 못한다).
    // 날짜가 바뀐 뒤 처음 확인할 때(Main에 들어올 때, 접속 중이면 00시가 지나는 순간) 다음 날 보상을 바로 주고 출석 창을 띄운다 (AttendanceEntry).
    // 빠진 날은 건너뛰지 않는다 — 출석한 날 수로 1일째 → 7일째까지 차례로 받는다.
    // 보상은 표 출석 시트 (한 행 = 하루, 행 수 = 일수). 지금: 균사석 500·500·500·1,000·1,500·2,000 (합계 6,000), 7일째 = 전설 무기 날다람쥐.
    // 골드 보상도 넣을 수 있다 (받는 순간 스테이지 적 처치 골드 × kills, DailyQuests와 같은 방식)
    public static class Attendance
    {
        public readonly struct Reward
        {
            public readonly AttendanceRewardKind kind;
            public readonly long amount;      // 골드는 적 처치 수(kills), 균사석은 개수, 무기는 1
            public readonly int equipId;      // 무기: 장비.index

            public Reward(AttendanceRewardKind kind, long amount, int equipId = 0)
            {
                this.kind = kind;
                this.amount = amount;
                this.equipId = equipId;
            }
        }

        // 출석 일수 = 출석 시트 행 수 (출석 창 칸 수와 같아야 한다)
        public static int Days => GameBalance.Attendance.Count;

        static Reward ToReward(AttendanceRow row) => new Reward(row.kind, row.amount, row.equipId);

        const string CountKey = "Attendance.Count";     // 받은 날 수 (0~Days)
        const string DayKey = "Attendance.Day";         // 마지막으로 받은 한국 날짜 (yyyyMMdd)

        static int count = -1;      // -1 = 아직 안 읽음
        static string lastDay;
        static long nextCheckTicks;  // 이 게임 시각(UTC ticks)이 되기 전에는 날짜를 다시 보지 않는다 (매 초 날짜 문자열을 만들지 않게)

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            count = -1;
            lastDay = null;
            nextCheckTicks = 0;
        }

        static void Ensure()
        {
            if (count >= 0) return;
            count = Mathf.Clamp(PlayerPrefs.GetInt(CountKey, 0), 0, Days);
            lastDay = PlayerPrefs.GetString(DayKey, "");
        }

        public static int Claimed { get { Ensure(); return count; } }     // 받은 날 수
        public static bool Finished => Claimed >= Days;
        public static bool ClaimedToday { get { Ensure(); return lastDay == DungeonTickets.Today; } }
        public static bool CanClaim => !Finished && !ClaimedToday;
        public static TimeSpan UntilNextDay => DungeonTickets.UntilReset;

        public static Reward RewardOf(int day) => ToReward(GameBalance.Attendance[Mathf.Clamp(day, 1, Days) - 1]);

        // 날짜가 바뀌었는지 가볍게 본다 (매 프레임 불러도 된다). 받을 수 있으면 true
        public static bool CheckDay()
        {
            long now = GameClock.NowTicks;
            if (now < nextCheckTicks) return false;
            bool can = CanClaim;
            nextCheckTicks = can ? 0 : now + UntilNextDay.Ticks;
            return can;
        }

        // 오늘 출석 보상을 준다. 받은 날(1~Days)을 돌려준다 (0 = 받을 게 없다)
        public static int Claim()
        {
            if (!CanClaim) return 0;
            int day = count + 1;
            Grant(RewardOf(day));
            count = day;
            lastDay = DungeonTickets.Today;
            nextCheckTicks = 0;
            PlayerPrefs.SetInt(CountKey, count);
            PlayerPrefs.SetString(DayKey, lastDay);
            SaveScheduler.Flush();      // 드문 보상 — 바로 저장 (강제 종료돼도 받은 날과 보상이 함께 남게)
            Changed?.Invoke();
            return day;
        }

        static void Grant(Reward r)
        {
            switch (r.kind)
            {
                case AttendanceRewardKind.Gold: Wallet.Add(Currency.Gold, GoldOf(r)); break;
                case AttendanceRewardKind.Mycelite: Wallet.Add(Currency.Mycelite, r.amount); break;
                case AttendanceRewardKind.Weapon:
                    int item = WeaponItemOf(r);
                    if (item >= 0) Equipment.Weapon.AddItems(new[] { item });
                    break;
            }
        }

        // 지금 스테이지 기준 골드 (받는 순간의 스테이지로 정한다)
        public static long GoldOf(Reward r)
        {
            double gold = (double)IdleProgress.EnemyGold(IdleProgress.Stage) * r.amount;
            return gold >= 9e18 ? 9000000000000000000L : (long)Math.Max(0, gold);
        }

        // 무기 보상의 장비 번호(저장 순서): 출석 시트 equipId = 장비.index인 무기. 없으면 가장 높은 등급(같으면 뒤 번호)
        public static int WeaponItemOf(Reward r)
        {
            var rows = GameBalance.EquipmentOf(GachaKind.Weapon);
            for (int i = 0; i < rows.Length; i++) if (rows[i].index == r.equipId) return rows[i].itemIndex;
            var w = Equipment.Weapon;
            int best = -1;
            for (int i = 0; i < w.Count; i++) if (best < 0 || w.Grade(i) >= w.Grade(best)) best = i;
            return best;
        }

        // 출석 창이 보여 주는 무기: 표에서 가장 마지막 무기 보상 (보통 마지막 날)
        public static int WeaponItem
        {
            get
            {
                var rows = GameBalance.Attendance;
                for (int i = rows.Count - 1; i >= 0; i--)
                    if (rows[i].kind == AttendanceRewardKind.Weapon) return WeaponItemOf(ToReward(rows[i]));
                return -1;
            }
        }

        public static string WeaponTitle
        {
            get
            {
                int i = WeaponItem;
                return i >= 0 ? Equipment.Weapon.Names[i] : "";
            }
        }

        // 개발용: 오늘 받은 기록을 지워 다시 받게 한다
        public static void DevResetToday()
        {
            Ensure();
            lastDay = "";
            nextCheckTicks = 0;
            PlayerPrefs.SetString(DayKey, "");
            Changed?.Invoke();
        }
    }
}
