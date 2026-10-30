# -*- coding: utf-8 -*-
"""verify: theory 7절 적용안 세부 대조 (이론.md 5·6·9절 수치, maxLv 영향, 실수 기대 Lv R)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import port  # noqa: E402
import plan_theory_v2 as P  # noqa: E402
from check import Plan, simulate  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")
plan = Plan(P.NAME, P.cost, P.enemy_gold, expected_level=P.expected_level, defs=P.DEFS)
rows = simulate(plan, 1000)

print("[5절 표 대조] s: 골드 / 공격력 다음 비용(long) / R / 누적h")
for s in (1, 10, 50, 100, 200, 300, 500, 700, 1000):
    _, nd, g, r, t = rows[s - 1]
    print(f"  {s:5}: {P.enemy_gold(s):>12,} / {P.cost_cs('Attack', P.expected_level('Attack', s)):>16,} / {nd / g:8.2f} / {t / 3600:8.1f}h ({t / 86400:.1f}일)")

print("[maxLv 영향] 기대 Lv 이 원래 maxLv 와 다른 첫 스테이지:")
for k in ("Attack", "Hp", "HpRegen"):
    s = next(s for s in range(1, 10000) if P.expected_level(k, s) != port.expected_level(k, s))
    print(f"  {k}: s={s}")
diff = [s for s in range(1, 3001) for k in port.STATS if P.expected_level(k, s) != port.expected_level(k, s)]
print(f"  s≤3000 에서 모든 강화 기대 Lv 차이: {len(diff)}곳 (적 HP/공격/골드 이외 공식 불변)")
for k in port.STATS:
    m = P.DEFS[k]["maxLv"]
    print(f"  {k:18} maxLv {m:6} 마지막 구매 비용 Cost(maxLv-1)={P.cost_cs(k, m - 1):.3e}  여유 ×{9e18 / P.cost_raw(k, m - 1):.2f}")

# 정수 Lv R 인접 비, R/FarmFactor
R = [nd / g for _, nd, g, _, _ in rows]
adj = [R[i] / R[i - 1] for i in range(1, 1000)]
dec = [i + 1 for i in range(1, 1000) if R[i] < R[i - 1]]
rf = [R[s - 1] / port.farm_factor(s) for s in range(10, 1001)]
print(f"[정수 Lv] R 감소 {len(dec)}곳, 인접 비 {min(adj):.3f}~{max(adj):.3f}, R/FarmFactor(s≥10) {min(rf):.3f}~{max(rf):.3f}")


# 실수 기대 Lv: C# 식에서 (int) 버림만 뺀 연속 곡선, need = ∫ cost dLv (Simpson)
def lv_real(k, stage):
    s = max(0, stage - 1)
    d = P.DEFS[k]
    if k == "Speed":
        return min(d["maxLv"], 1.5 * s)
    if k == "HpRegen":
        return min(d["maxLv"], 15 * s * 0.6)
    if k in ("CritChance", "CritDamage", "AdvancedAttack"):
        ratio = {"CritChance": 0.8, "CritDamage": 0.6, "AdvancedAttack": 0.5}[k]
        gate = lv_real(d["us"], stage)
        if gate < d["ul"]:
            return 0.0
        return min(d["maxLv"], ((gate - d["ul"]) / d["ce_"] + 1) * d["cs"] * ratio)
    if k == "NormalEnemyDamage":
        return max(0.0, min(d["maxLv"], (lv_real(d["us"], stage) - d["ul"]) / 2))
    return min(d["maxLv"], 15.0 * s)


def integ(k, a, b, n=16):
    if b <= a:
        return 0.0
    h = (b - a) / n
    f = lambda x: P.cost_raw(k, x)
    return h / 3 * (f(a) + f(b) + 4 * sum(f(a + (2 * i - 1) * h) for i in range(1, n // 2 + 1))
                    + 2 * sum(f(a + 2 * i * h) for i in range(1, n // 2)))


Rr = []
for s in range(1, 1001):
    nd = sum(integ(k, lv_real(k, s), lv_real(k, s + 1)) for k in port.STATS)
    Rr.append(nd / plan.run_gold(s))
decr = [i + 1 for i in range(1, 1000) if Rr[i] < Rr[i - 1]]
adjr = [Rr[i] / Rr[i - 1] for i in range(1, 1000)]
print(f"[실수 Lv] R 감소 {len(decr)}곳 {decr}, 인접 비 {min(adjr):.3f}~{max(adjr):.3f}")
