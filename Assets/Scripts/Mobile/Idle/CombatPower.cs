using System.Text;

namespace MoreMush.Mobile
{
    // 전투력: 지금 강화·보유 효과·편성으로 낼 수 있는 힘을 점수 하나로.
    //   공격 점수 = 초당 피해 × OffenseSeconds (꼬마들이 그 시간 동안 주는 피해, 치명타 기대값 포함)
    //   생존 점수 = 최대 체력 + 체력 재생 × RegenSeconds
    //   전투력 = 공격 점수 + 생존 점수
    // 기대 성장 곡선대로 키우고 자동 편성하면 스테이지 1 ≈ 580, 100 ≈ 52만, 1,000 ≈ 1.2억 정도 (BALANCE_IDLE.md)
    public static class CombatPower
    {
        public const double OffenseSeconds = 10;
        public const double RegenSeconds = 30;

        public static double CritChance => System.Math.Min(1, IdleProgress.Value(IdleStat.CritChance));
        public static double CritDamage => IdleProgress.Value(IdleStat.CritDamage);
        public static double CritFactor => 1 + CritChance * (CritDamage - 1);

        // 편성된 꼬마 전원의 초당 피해 (꼬마마다 공격력 × 공격 배율 × 초당 투척 수 × 치명타 기대값)
        public static double Dps => IdleProgress.FinalAttack * Formation.TotalAttackMultiplier() * IdleProgress.Value(IdleStat.Speed) * CritFactor;

        public static double Offense => Dps * OffenseSeconds;
        public static double Survival => IdleProgress.FinalHp + IdleProgress.FinalHpRegen * RegenSeconds;
        public static double Total => Offense + Survival;

        // 통계 창에 보이는 줄들
        public static string Describe()
        {
            int members = 0;
            for (int i = 0; i < Formation.SlotCount; i++) if (!Formation.IsEmpty(i)) members++;
            double ownAtk = (IdleProgress.AttackMultiplier - 1) * 100, ownHp = (IdleProgress.HpMultiplier - 1) * 100;
            var sb = new StringBuilder();
            sb.Append("전투력 ").Append(Format(Total)).Append('\n');
            sb.Append("  공격 ").Append(Format(Offense)).Append("  ·  생존 ").Append(Format(Survival)).Append('\n');
            sb.Append("공격력 ").Append(Format(IdleProgress.FinalAttack)).Append("  (보유 효과 +").Append(ownAtk.ToString("0.#")).Append("%)\n");
            foreach (var eq in Equipment.All)
            {
                int on = eq.Equipped;
                sb.Append(eq.Title).Append(' ').Append(on >= 0 ? eq.Names[on] : "없음")
                  .Append("  ·  ").Append(eq.StatName).Append(" +").Append(eq.TotalPercent.ToString("0.#")).Append("%\n");
            }
            sb.Append("편성 ").Append(members).Append('/').Append(Formation.SlotCount).Append("명  ·  배율 합 ×").Append(Formation.TotalAttackMultiplier().ToString("0.##")).Append('\n');
            sb.Append("공격 속도 ").Append(IdleProgress.Value(IdleStat.Speed).ToString("0.00")).Append("/초  ·  치명타 ")
              .Append((CritChance * 100).ToString("0.##")).Append("% × ").Append((CritDamage * 100).ToString("0.#")).Append("%\n");
            sb.Append("초당 피해 ").Append(Format(Dps)).Append('\n');
            sb.Append("체력 ").Append(Format(IdleProgress.FinalHp)).Append("  (보유 효과 +").Append(ownHp.ToString("0.#")).Append("%)  ·  재생 ")
              .Append(Format(IdleProgress.FinalHpRegen)).Append("/초");
            return sb.ToString();
        }

        public static string Format(double v) => IdleProgress.FormatNumber(v);
    }
}
