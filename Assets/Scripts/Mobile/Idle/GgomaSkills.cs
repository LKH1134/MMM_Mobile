using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 꼬마 패시브 스킬 (꼬마스킬 시트). 꼬마마다 진화 1·2·3단계에 하나씩 해금된다 (Formation.EvoStage ≥ stage).
    // 해금 전에도 목록은 보인다 (CharacterDetail이 잠금으로 그린다). 같은 효과는 더한다.
    // 효과는 그 꼬마가 던지는 수확기에만 붙는다 — TeamAttack만 편성된 꼬마 전원에게 붙는다 (그 꼬마가 편성돼 있을 때).
    // 해금 상태는 저장하지 않는다 (레벨에서 계산) — 저장 데이터가 바뀌지 않는다
    public enum GgomaSkillEffect
    {
        Attack,             // 공격력 +비율
        Speed,              // 공격 속도 +비율
        Splash,             // 맞은 적 둘레(ggomaSplashRadius)의 다른 적에게 피해 × 비율
        BossDamage,         // 보스 피해 +비율
        EdibleDamage,       // 식용 버섯 피해 +비율
        ToxicDamage,        // 독성 버섯 피해 +비율
        MedicinalDamage,    // 약용 버섯 피해 +비율
        CritChance,         // 치명타 확률 +값 (0.1 = +10%p)
        TeamAttack,         // 편성된 꼬마 전원 공격력 +비율
    }

    // 방치 전투 적 버섯의 종류. 그림 이름으로 나눈다: "독…" = 독성, "약용…" = 약용, 나머지(ed0~9 등) = 식용
    public enum MushroomKind { Edible, Toxic, Medicinal }

    public static class GgomaSkills
    {
        static Dictionary<string, List<GgomaSkillRow>> byOwner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reload() => byOwner = null;

        // 이 꼬마의 스킬 (단계 순). 없으면 빈 목록
        public static IReadOnlyList<GgomaSkillRow> Of(string id)
        {
            if (byOwner == null)
            {
                var map = new Dictionary<string, List<GgomaSkillRow>>();
                foreach (var r in GameBalance.GgomaSkills)
                {
                    if (!map.TryGetValue(r.owner, out var list)) map[r.owner] = list = new List<GgomaSkillRow>();
                    list.Add(r);
                }
                foreach (var list in map.Values) list.Sort((a, b) => a.stage != b.stage ? a.stage.CompareTo(b.stage) : a.index.CompareTo(b.index));
                byOwner = map;
            }
            return id != null && byOwner.TryGetValue(id, out var rows) ? rows : (IReadOnlyList<GgomaSkillRow>)System.Array.Empty<GgomaSkillRow>();
        }

        public static bool IsUnlocked(GgomaSkillRow r) => r != null && Formation.IsOwned(r.owner) && Formation.EvoStage(r.owner) >= r.stage;

        // 이 꼬마의 해금된 스킬 중 effect 값의 합
        public static double Self(string id, GgomaSkillEffect effect)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            double sum = 0;
            foreach (var r in Of(id)) if (r.effect == effect && IsUnlocked(r)) sum += r.value;
            return sum;
        }

        // 편성된 꼬마들의 TeamAttack 합
        public static double TeamAttack()
        {
            double sum = 0;
            for (int i = 0; i < Formation.SlotCount; i++) sum += Self(Formation.Get(i), GgomaSkillEffect.TeamAttack);
            return sum;
        }

        // 공격력 배수 (자기 Attack + 팀 TeamAttack)
        public static double AttackMul(string id, double teamAttack) => 1 + Self(id, GgomaSkillEffect.Attack) + teamAttack;
        public static double SpeedMul(string id) => 1 + Self(id, GgomaSkillEffect.Speed);

        // 적 종류·보스에 따른 피해 배수
        public static double TargetMul(string id, bool boss, MushroomKind kind)
        {
            if (boss) return 1 + Self(id, GgomaSkillEffect.BossDamage);
            var effect = kind == MushroomKind.Toxic ? GgomaSkillEffect.ToxicDamage
                : kind == MushroomKind.Medicinal ? GgomaSkillEffect.MedicinalDamage : GgomaSkillEffect.EdibleDamage;
            return 1 + Self(id, effect);
        }

        public static MushroomKind KindOf(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return MushroomKind.Edible;
            if (spriteName.StartsWith("Mush_Toxic")) return MushroomKind.Toxic;
            if (spriteName.StartsWith("Mush_Medicinal")) return MushroomKind.Medicinal;
            return MushroomKind.Edible;
        }

        // 짧은 효과 글자 (칸에 들어가게)
        public static string EffectText(GgomaSkillRow r)
        {
            string pct = (r.value * 100).ToString("0.#");
            switch (r.effect)
            {
                case GgomaSkillEffect.Attack: return $"공격력 +{pct}%";
                case GgomaSkillEffect.Speed: return $"공격 속도 +{pct}%";
                case GgomaSkillEffect.Splash: return $"범위 피해 {pct}%";
                case GgomaSkillEffect.BossDamage: return $"보스 피해 +{pct}%";
                case GgomaSkillEffect.EdibleDamage: return $"식용 버섯 +{pct}%";
                case GgomaSkillEffect.ToxicDamage: return $"독성 버섯 +{pct}%";
                case GgomaSkillEffect.MedicinalDamage: return $"약용 버섯 +{pct}%";
                case GgomaSkillEffect.CritChance: return $"치명타 +{pct}%p";
                case GgomaSkillEffect.TeamAttack: return $"모두 공격력 +{pct}%";
                default: return r.effect.ToString();
            }
        }
    }
}
