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
        public static double OffenseSeconds => GameBalance.Const("powerOffenseSeconds");   // 상수 시트
        public static double RegenSeconds => GameBalance.Const("powerRegenSeconds");

        public static double CritChance => IdleProgress.FinalCritChance;
        public static double CritDamage => IdleProgress.FinalCritDamage;

        // 편성된 꼬마 전원의 초당 피해 (꼬마마다 공격력 × 공격 배율 × 초당 투척 수 × 치명타 기대값)
        // 꼬마 스킬 중 늘 붙는 공격력·공격 속도·치명타 확률도 꼬마마다 곱한다 (보스·버섯 종류·범위 피해는 상황에 따라 달라 뺀다)
        public static double Dps => IdleProgress.FinalAttack * IdleProgress.FinalSpeed * SkilledSum();

        // 편성 꼬마마다 공격 배율 × 스킬 공격력 × 스킬 공격 속도 × 치명타 기대값의 합
        static double SkilledSum()
        {
            double team = GgomaSkills.TeamAttack(), sum = 0;
            for (int i = 0; i < Formation.SlotCount; i++)
            {
                string id = Formation.Get(i);
                if (string.IsNullOrEmpty(id)) continue;
                double crit = System.Math.Min(1, CritChance + GgomaSkills.Self(id, GgomaSkillEffect.CritChance));
                sum += Formation.AttackMultiplier(id) * GgomaSkills.AttackMul(id, team) * GgomaSkills.SpeedMul(id) * (1 + crit * (CritDamage - 1));
            }
            return sum;
        }

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
            sb.Append(Loc.F("stats.power", Format(Total))).Append('\n');
            sb.Append("  ").Append(Loc.F("stats.offense_survival", Format(Offense), Format(Survival))).Append('\n');
            sb.Append(Loc.F("stats.attack", Format(IdleProgress.FinalAttack), ownAtk.ToString("0.#"))).Append('\n');
            foreach (var eq in Equipment.All)
            {
                int on = eq.Equipped;
                sb.Append(Loc.F("stats.equip", eq.DisplayTitle, on >= 0 ? eq.Names[on] : Loc.T("common.none"),
                    eq.DisplayStatName, eq.TotalPercent.ToString("0.#"))).Append('\n');
            }
            sb.Append(Loc.F("stats.formation", members, Formation.SlotCount, Formation.TotalAttackMultiplier().ToString("0.##"))).Append('\n');
            sb.Append(Loc.F("stats.speed_crit", IdleProgress.FinalSpeed.ToString("0.00"), (CritChance * 100).ToString("0.##"), (CritDamage * 100).ToString("0.#"))).Append('\n');
            sb.Append(Loc.F("stats.dps", Format(Dps))).Append('\n');
            sb.Append(Loc.F("stats.hp", Format(IdleProgress.FinalHp), ownHp.ToString("0.#"), Format(IdleProgress.FinalHpRegen)));
            return sb.ToString();
        }

        public static string Format(double v) => IdleProgress.FormatNumber(v);
    }
}
