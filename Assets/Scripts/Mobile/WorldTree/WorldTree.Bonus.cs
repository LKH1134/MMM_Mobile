using System;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 세계수 효과 합계 = 가호(지금 단계까지) + 꼬마 특기(bonusValue × 특기 단계) + 놓은 장식.
    // 전투가 공격마다 읽으므로 합계를 캐시하고, 세계수·꼬마 보유가 바뀔 때만 다시 센다.
    public static partial class WorldTree
    {
        static readonly double[] bonus = new double[Enum.GetValues(typeof(TreeEffect)).Length];
        static bool bonusDirty = true;
        static bool formationHooked;

        public static double Bonus(TreeEffect effect)
        {
            if (bonusDirty) RecomputeBonus();
            return bonus[(int)effect];
        }

        // 전투 능력치에 곱할 배율 (1 + 비율)
        public static double Mul(TreeEffect effect) => 1 + Bonus(effect);

        static void RecomputeBonus()
        {
            if (!formationHooked)
            {
                formationHooked = true;
                Formation.Changed += () => bonusDirty = true;     // 뽑기로 꼬마가 생기면 특기가 바뀐다
            }
            bonusDirty = false;
            Array.Clear(bonus, 0, bonus.Length);
            try
            {
                foreach (var b in GameBalance.Blessings)
                    if (BlessActive(b)) bonus[(int)b.effect] += b.value;
                foreach (var id in OwnedKids)
                {
                    var row = KidRow(id);
                    if (row != null) bonus[(int)row.bonusEffect] += row.bonusValue * KidStep(id);
                }
                foreach (var key in D.decosPlaced)
                {
                    var d = Deco(key);
                    if (d != null) bonus[(int)d.effect] += d.value;
                }
            }
            catch (Exception e)
            {
                // 표를 못 읽으면 효과 없이 (전투는 계속 돈다)
                Debug.LogError("[WorldTree] 효과를 계산하지 못했다: " + e.Message);
                Array.Clear(bonus, 0, bonus.Length);
            }
        }
    }
}
