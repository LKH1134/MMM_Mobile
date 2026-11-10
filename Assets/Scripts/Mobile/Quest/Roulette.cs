using System;
using System.Globalization;
using UnityEngine;

namespace MoreMush.Mobile
{
    public enum RouletteKind { Gold, Mycelite }

    // 룰렛: 하루 PerDay번(한국 00시에 다시 채움), 한 번 돌린 뒤 CooldownSeconds(5분)가 지나야 다음 한 번.
    // 결과는 돌리는 순간 정해 "대기 보상"으로 저장하고, 화면의 회전이 끝나면 지급한다(ClaimPending).
    // 회전 중 앱이 꺼져도 다음에 룰렛 화면을 열 때(또는 창이 닫힐 때) 받는다 — 횟수만 쓰이고 보상이 사라지는 일이 없게.
    // 시각은 게임 시계(GameClock)로: 기기 시계를 되돌려 대기 시간을 건너뛰지 못한다.
    // 저장: PlayerPrefs "Roulette.Day"(마지막으로 돌린 한국 날짜) · "Roulette.Used"(그날 돌린 수) · "Roulette.Last"(마지막으로 돌린 시각, 틱) · "Roulette.Pending"(대기 보상 칸 번호, 없으면 -1)
    public static class Roulette
    {
        public const int PerDay = 3;
        public const double CooldownSeconds = 300.0;     // 5분
        public const int SliceCount = 6;

        // 칸의 보상. 골드는 "지금 스테이지 적 1마리 골드 × amount(마리)", 균사석은 amount개.
        // weight = 나올 비중(합 100). 칸은 원판에서 시계 방향으로 0번(맨 위)부터. 수치는 임시값 — 균사석은 뽑기 11회 = 100, 광고 상자 = 90 기준
        public readonly struct Slice
        {
            public readonly RouletteKind kind;
            public readonly long amount;
            public readonly int weight;
            public Slice(RouletteKind kind, long amount, int weight) { this.kind = kind; this.amount = amount; this.weight = weight; }
        }

        public static readonly Slice[] Slices =
        {
            new Slice(RouletteKind.Gold, 1000, 26),
            new Slice(RouletteKind.Mycelite, 30, 22),
            new Slice(RouletteKind.Gold, 2000, 20),
            new Slice(RouletteKind.Mycelite, 50, 16),
            new Slice(RouletteKind.Gold, 4000, 10),
            new Slice(RouletteKind.Mycelite, 100, 6),
        };

        const string DayKey = "Roulette.Day", UsedKey = "Roulette.Used", LastKey = "Roulette.Last", PendingKey = "Roulette.Pending";

        public static event Action Changed;

        // 오늘 돌린 수 (날짜가 바뀌었으면 0)
        public static int Used => DungeonTickets.IsNewDay(PlayerPrefs.GetString(DayKey, "")) ? 0 : Mathf.Clamp(PlayerPrefs.GetInt(UsedKey, 0), 0, PerDay);
        public static int Remaining => PerDay - Used;

        public static bool HasPending => PlayerPrefs.GetInt(PendingKey, -1) >= 0;

        // 다음 한 번까지 남은 대기 초 (0이면 지금 가능). 저장된 시각이 지금보다 앞서 있으면(망가진 값) 대기 시간 안으로 줄여 막히지 않게
        public static double CooldownLeft
        {
            get
            {
                if (!long.TryParse(PlayerPrefs.GetString(LastKey, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var last) || last <= 0) return 0;
                double elapsed = new TimeSpan(GameClock.NowTicks - last).TotalSeconds;
                return Math.Min(CooldownSeconds, Math.Max(0, CooldownSeconds - elapsed));
            }
        }

        public static bool CanSpin => !HasPending && Remaining > 0 && CooldownLeft <= 0;

        // 돌린다: 횟수·시각을 쓰고 나올 칸을 정해 대기 보상으로 저장. 못 돌리면 false
        public static bool TrySpin(out int slice)
        {
            slice = -1;
            if (!CanSpin) return false;
            int used = Used;
            if (DungeonTickets.IsNewDay(PlayerPrefs.GetString(DayKey, ""))) PlayerPrefs.SetString(DayKey, DungeonTickets.Today);   // 시계가 되돌아가 있으면 저장된(더 뒤) 날짜를 그대로 둔다
            PlayerPrefs.SetInt(UsedKey, used + 1);
            PlayerPrefs.SetString(LastKey, GameClock.NowTicks.ToString(CultureInfo.InvariantCulture));
            slice = Pick(UnityEngine.Random.value);
            PlayerPrefs.SetInt(PendingKey, slice);
            SaveScheduler.Flush();       // 드문 일 — 바로 저장 (강제 종료돼도 횟수와 대기 보상이 함께 남게)
            Changed?.Invoke();
            return true;
        }

        // 0~1 난수로 칸 고르기 (비중 순서대로 누적)
        public static int Pick(float r01)
        {
            int total = 0;
            foreach (var s in Slices) total += s.weight;
            float roll = Mathf.Clamp01(r01) * total;
            int acc = 0;
            for (int i = 0; i < Slices.Length; i++)
            {
                acc += Slices[i].weight;
                if (roll < acc) return i;
            }
            return Slices.Length - 1;
        }

        // 칸에 적을 실제 수량 (골드는 지금 스테이지 기준)
        public static long AmountOf(int slice)
        {
            var s = Slices[Mathf.Clamp(slice, 0, Slices.Length - 1)];
            if (s.kind == RouletteKind.Mycelite) return s.amount;
            return IdleProgress.ClampToLong((double)IdleProgress.EnemyGold(IdleProgress.Stage) * s.amount, 1);
        }

        // 대기 보상을 지급하고 (칸, 종류, 수량)을 돌려준다. 없으면 slice = -1
        public static (int slice, RouletteKind kind, long amount) ClaimPending()
        {
            int slice = PlayerPrefs.GetInt(PendingKey, -1);
            if (slice < 0 || slice >= Slices.Length)
            {
                if (slice != -1) PlayerPrefs.SetInt(PendingKey, -1);      // 알 수 없는 번호는 지운다
                return (-1, RouletteKind.Gold, 0);
            }
            PlayerPrefs.SetInt(PendingKey, -1);
            var kind = Slices[slice].kind;
            long amount = AmountOf(slice);
            Wallet.Add(kind == RouletteKind.Gold ? Currency.Gold : Currency.Mycelite, amount);
            SaveScheduler.Flush();
            Changed?.Invoke();
            return (slice, kind, amount);
        }
    }
}
