# -*- coding: utf-8 -*-
"""verify: 임의의 비용·골드 안을 판정 항목 (a)~(e)로 계산한다.

안(plan) = 파이썬 파일(또는 'current' = port.py 현재 C#) 이 다음 이름을 내보내면 된다:
  NAME                         : 표시 이름 (없으면 파일 이름)
  cost(stat, lv) -> float      : 다음 레벨로 올리는 비용 (반올림·9e18 자르기 전 값이면 (d)가 정확)    [필수]
  enemy_gold(stage) -> float   : 일반 적 1마리 골드                                                [필수]
  boss_gold(stage) -> float    : 없으면 enemy_gold × 20
  expected_level(stat, stage)  : 없으면 현재 C# 기대 성장 곡선 (port.expected_level)
  enemy_count(stage, wave)     : 없으면 현재 C#
  STATS                        : 없으면 port.STATS

시간 모형(이론과 독립): 스테이지 s를 넘기려면 s→s+1 기대 성장분 need(s)를 사야 하고,
  판당 골드 G(s) = Σwave enemy_count × enemy_gold + boss_gold, 판 수 R(s) = max(1, need/G), 한 판 40초.
  누적 t(s) = Σ 40·R(k), k=1..s.

사용: python check.py [current] [plan.py ...] [--n 2000]
"""
import importlib.util
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import port  # noqa: E402

LONG_CAP = 9e18
RUN_SECONDS = 40.0
MILESTONES = (100, 300, 500, 1000, 2000)


class Plan:
    def __init__(self, name, cost, enemy_gold, boss_gold=None, expected_level=None, enemy_count=None, stats=None, defs=None):
        self.name = name
        self.defs = defs or port.DEFS
        self.cost = cost
        self.enemy_gold = enemy_gold
        self.boss_gold = boss_gold or (lambda s: enemy_gold(s) * 20)
        self.expected_level = expected_level or port.expected_level
        self.enemy_count = enemy_count or port.enemy_count
        self.stats = stats or port.STATS

    def need(self, s):
        n = 0.0
        for k in self.stats:
            for lv in range(self.expected_level(k, s), self.expected_level(k, s + 1)):
                n += self.cost(k, lv)
        return n

    def run_gold(self, s, mul=1.0):
        eg = self.enemy_gold(s) * mul
        return eg * sum(self.enemy_count(s, w) for w in range(1, port.WAVES_PER_STAGE + 1)) + self.boss_gold(s) * mul


CURRENT = Plan("current(C# 포팅)", port.cost_raw, port.enemy_gold, port.boss_gold)


def load(path):
    if path == "current":
        return CURRENT
    spec = importlib.util.spec_from_file_location("plan_" + os.path.basename(path)[:-3], path)
    m = importlib.util.module_from_spec(spec)
    sys.path.insert(0, os.path.dirname(os.path.abspath(path)))
    spec.loader.exec_module(m)
    return Plan(getattr(m, "NAME", os.path.basename(path)), m.cost, m.enemy_gold, getattr(m, "boss_gold", None),
                getattr(m, "expected_level", None), getattr(m, "enemy_count", None), getattr(m, "STATS", None),
                getattr(m, "DEFS", None))


def simulate(plan, n, gold_mul=1.0):
    t, rows = 0.0, []
    for s in range(1, n + 1):
        nd = plan.need(s)
        g = plan.run_gold(s, gold_mul)
        r = max(1.0, nd / g) if g > 0 else float("inf")
        t += RUN_SECONDS * r
        rows.append((s, nd, g, r, t))
    return rows


def fit_log(rows, s0):
    """s = a + b·ln t 최소제곱, R²."""
    pts = [(math.log(t), s) for s, _, _, _, t in rows if s >= s0]
    n = len(pts)
    mx = sum(x for x, _ in pts) / n
    my = sum(y for _, y in pts) / n
    sxx = sum((x - mx) ** 2 for x, _ in pts)
    sxy = sum((x - mx) * (y - my) for x, y in pts)
    b = sxy / sxx
    a = my - b * mx
    ss_res = sum((y - a - b * x) ** 2 for x, y in pts)
    ss_tot = sum((y - my) ** 2 for _, y in pts)
    return a, b, 1 - ss_res / ss_tot


def stages_per_doubling(rows, s):
    """t(s)에서 누적 시간이 2배가 될 때까지 오르는 스테이지 수 (로그형이면 거의 일정)."""
    t0 = rows[s - 1][4]
    for s2, *_, t in rows[s - 1:]:
        if t >= 2 * t0:
            return s2 - s
    return None


def report(plan, n):
    out = [f"## {plan.name} (1~{n})"]
    eg = [plan.enemy_gold(s) for s in range(1, n + 2)]
    dips = [s for s in range(2, n + 1) if eg[s - 1] < eg[s - 2]]
    dips1000 = [s for s in dips if s <= 1000]
    out.append(f"(a) EnemyGold 감소: 1~1000 {len(dips1000)}곳, 1~{n} {len(dips)}곳 · 처음 {dips[:12]}")
    peak = max(range(1, n + 1), key=lambda s: eg[s - 1])
    out.append(f"    EnemyGold 최대 s={peak} ({eg[peak - 1]:.4g}), s={n}: {eg[n - 1]:.4g}")

    rows = simulate(plan, n)
    for s0 in (1, 50, 200):
        a, b, r2 = fit_log(rows, s0)
        out.append(f"(b) s = {a:.1f} + {b:.1f}·ln t  (s≥{s0}) R²={r2:.4f}")
    ms = [m for m in MILESTONES if m <= n]
    out.append("    누적 시간: " + " · ".join(f"{m}: {rows[m - 1][4] / 3600:.1f}h" for m in ms))
    out.append("    판 수 R(s): " + " · ".join(f"{m}: {rows[m - 1][3]:.2f}" for m in (1, 50) + tuple(ms)))
    out.append("    t 2배당 스테이지: " + " · ".join(f"s{m}→+{stages_per_doubling(rows, m)}" for m in (50, 100, 200, 300, 500) if m < n))
    rdown = sum(1 for i in range(10, n) if rows[i][3] < rows[i - 10][3] - 1e-9)
    out.append(f"    R(s) 10스테이지 간격 감소 {rdown}곳 (0이어야 '성장 속도 계속 감소')")

    gr, cr = [], []
    for s in range(1, 51):
        gr.append(plan.enemy_gold(s) / port.enemy_gold(s))
        lv_p, lv_c = plan.expected_level("Attack", s), port.expected_level("Attack", s)
        cr.append(plan.cost("Attack", lv_p) / port.cost_raw("Attack", lv_c))
    out.append("(c) 1~50 처치당 골드 비(안/현재): " + " · ".join(f"s{s}={gr[s - 1]:.2f}" for s in (1, 5, 10, 20, 30, 50))
               + f" (범위 {min(gr):.2f}~{max(gr):.2f})")
    out.append("    공격력 강화 비용 비(안/현재): " + " · ".join(f"s{s}={cr[s - 1]:.2f}" for s in (1, 5, 10, 20, 30, 50))
               + f" (범위 {min(cr):.2f}~{max(cr):.2f})")

    for lim in (1000, 2000):
        if lim > n:
            continue
        mc = max((plan.cost(k, plan.expected_level(k, s)), k, s) for s in (lim,) for k in plan.stats)
        mg = max(max(plan.enemy_gold(s), plan.boss_gold(s), plan.run_gold(s)) for s in range(1, lim + 1))
        mn = max(r[1] for r in rows[:lim])
        ok = mc[0] < LONG_CAP and mg < LONG_CAP and mn < LONG_CAP
        out.append(f"(d) ≤{lim}: 최대 비용 {mc[0]:.3g}({mc[1]}@s{mc[2]}) · 최대 골드 {mg:.3g} · 최대 need {mn:.3g} → {'OK' if ok else '9e18 초과!'}")
    mx = [(plan.cost(k, plan.defs[k]["maxLv"] - 1), k) for k in plan.stats if k in plan.defs]
    over = [k for c, k in mx if c >= LONG_CAP]
    out.append(f"    maxLv 직전 비용 9e18 이상: {over or '없음'} · 최대 {max(mx)[0]:.3g}({max(mx)[1]})")

    base = rows[min(1000, n) - 1][4]
    sens = []
    for mul in (0.5, 2.0):
        t = simulate(plan, min(1000, n), mul)[-1][4]
        sens.append(f"골드×{mul}: {t / 3600:.1f}h (×{t / base:.2f})")
    out.append(f"(e) {min(1000, n)}스테이지 도달 {base / 3600:.1f}h → " + " · ".join(sens))
    return "\n".join(out), dict(dips1000=len(dips1000), dips=len(dips),
                                hours={m: rows[m - 1][4] / 3600 for m in ms})


def main():
    args = sys.argv[1:]
    n = 1000
    if "--n" in args:
        i = args.index("--n")
        n = int(args[i + 1])
        del args[i:i + 2]
    for p in args or ["current"]:
        print(report(load(p), n)[0])
        print()


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
