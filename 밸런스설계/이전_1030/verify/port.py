# -*- coding: utf-8 -*-
"""verify: IdleProgress.cs 독립 포팅 (2026-10-30, 원본 Assets/Scripts/Mobile/Idle/IdleProgress.cs 직접 읽고 새로 작성).

C# 동작 맞춤:
  - int / int      → 0쪽 버림 (cs_div)          예: NormalEnemyDamage (gate - 150) / 2 가 음수일 때
  - (int)double    → 0쪽 버림 (int())
  - (long)Math.Min(9e18, Math.Ceiling(..))     → Cost
  - Math.Round     → 은행가 반올림(MidpointRounding.ToEven) = 파이썬 round(float)
  - Mathf.Clamp/Min/Max(int) → 정수 그대로
  - Formation.SlotCount = 5 (Formation.cs:11)
한계: Math.Pow 와 math.pow 는 둘 다 C 런타임 pow 지만 플랫폼에 따라 마지막 비트가 다를 수 있음 → Ceiling 경계에서 1 차이 가능.

사용: python port.py [최대스테이지=1000]  → verify/port_current.csv, 화면에 감소 지점·원인 분해
"""
import csv
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

# ── IdleProgress.cs:32-44 Defs ── (unlockStat 미지정 = enum 기본값 Attack)
STATS = ["Attack", "Hp", "HpRegen", "Speed", "CritChance", "CritDamage", "AdvancedAttack", "NormalEnemyDamage"]
DEFS = {
    "Attack":            dict(base=10,  per=2,     maxLv=100000, cb=10,  ce=2.5, us="Attack", ul=0,    ce_=0,   cs=0),
    "Hp":                dict(base=100, per=20,    maxLv=100000, cb=10,  ce=2.5, us="Attack", ul=0,    ce_=0,   cs=0),
    "HpRegen":           dict(base=2,   per=0.4,   maxLv=100000, cb=15,  ce=2.5, us="Hp",     ul=20,   ce_=0,   cs=0),
    "Speed":             dict(base=0.8, per=0.01,  maxLv=150,    cb=300, ce=3.5, us="Attack", ul=30,   ce_=0,   cs=0),
    "CritChance":        dict(base=0,   per=0.001, maxLv=1000,   cb=30,  ce=2.5, us="Attack", ul=100,  ce_=100, cs=25),
    "CritDamage":        dict(base=1.5, per=0.004, maxLv=2500,   cb=30,  ce=2.5, us="Attack", ul=300,  ce_=100, cs=25),
    "AdvancedAttack":    dict(base=1,   per=0.005, maxLv=2000,   cb=1e7, ce=1.5, us="Attack", ul=3000, ce_=100, cs=25),
    "NormalEnemyDamage": dict(base=1,   per=0.01,  maxLv=1000,   cb=2e7, ce=1.5, us="AdvancedAttack", ul=150, ce_=0, cs=0),
}
WAVES_PER_STAGE = 5        # :16
LEVELS_PER_STAGE = 15      # :143
FARM_SCALE = 150.0         # :204
SLOT_COUNT = 5             # Formation.cs:11
BOSS_UNITS = 20            # :215, BossGold = EnemyGold × 20 (:224)
LONG_CAP = 9e18
RUN_SECONDS = 40.0         # 한 판 40초 가정 (과제 조건)


def cs_div(a, b):
    """C# 정수 나눗셈 (0쪽 버림)."""
    q = abs(a) // abs(b)
    return q if (a >= 0) == (b > 0) else -q


def clamp(v, lo, hi):
    return lo if v < lo else hi if v > hi else v


# ── :95-99 Cost ──
def cost_raw(stat, lv):
    d = DEFS[stat]
    return d["cb"] * math.pow(1 + lv / 10.0, d["ce"])


def cost(stat, lv):
    return int(min(LONG_CAP, math.ceil(cost_raw(stat, lv))))


# ── :114-119 Value ──
def value(stat, lv):
    d = DEFS[stat]
    lv = clamp(lv, 0, d["maxLv"])
    return d["base"] * (1 + d["per"] * lv) if stat == "Speed" else d["base"] + d["per"] * lv


# ── :145-170 ExpectedLevel / ExpectedCapped ──
def expected_capped(stat, stage, ratio):
    d = DEFS[stat]
    gate = expected_level(d["us"], stage)
    if gate < d["ul"]:
        return 0
    cap = (float(gate - d["ul"]) / d["ce_"] + 1) * d["cs"]
    return int(min(d["maxLv"], cap * ratio))


def expected_level(stat, stage):
    s = max(0, stage - 1)
    d = DEFS[stat]
    if stat == "Speed":
        return min(d["maxLv"], int(1.5 * s))
    if stat == "HpRegen":
        return min(d["maxLv"], int(LEVELS_PER_STAGE * s * 0.6))
    if stat == "CritChance":
        return expected_capped(stat, stage, 0.8)
    if stat == "CritDamage":
        return expected_capped(stat, stage, 0.6)
    if stat == "AdvancedAttack":
        return expected_capped(stat, stage, 0.5)
    if stat == "NormalEnemyDamage":
        return clamp(cs_div(expected_level(d["us"], stage) - d["ul"], 2), 0, d["maxLv"])
    return min(d["maxLv"], LEVELS_PER_STAGE * s)


# ── :172-198 기대 전투력 (골드 판정엔 안 쓰지만 포팅 완결성용) ──
def expected_hit(s):
    cc = min(1, value("CritChance", expected_level("CritChance", s)))
    cd = value("CritDamage", expected_level("CritDamage", s))
    return (value("Attack", expected_level("Attack", s)) * (1 + 0.012 * s)
            * value("AdvancedAttack", expected_level("AdvancedAttack", s)) * (1 + cc * (cd - 1)))


def expected_dps(s):
    return expected_hit(s) * SLOT_COUNT * value("Speed", expected_level("Speed", s))


def enemy_hp(stage, wave):
    return expected_hit(stage) * value("NormalEnemyDamage", expected_level("NormalEnemyDamage", stage)) * 2.0 * (1 + 0.08 * (wave - 1))


def boss_hp(stage):
    return expected_dps(stage) * 18.0


# ── :189 EnemyCount ──
def enemy_count(stage, wave):
    return clamp(5 + 2 * (wave - 1) + (stage - 1), 5, 15)


def units(stage):
    return BOSS_UNITS + sum(enemy_count(stage, w) for w in range(1, WAVES_PER_STAGE + 1))


# ── :204-224 골드 ──
def farm_factor(stage):
    return math.exp((stage - 1) / FARM_SCALE)


def need_by_stat(stage, cost_fn=cost, lvl_fn=expected_level):
    """스테이지 s → s+1 기대 성장분 비용 (종류별)."""
    out = {}
    for k in STATS:
        a, b = lvl_fn(k, stage), lvl_fn(k, stage + 1)
        out[k] = float(sum(cost_fn(k, lv) for lv in range(a, b)))
    return out


def need(stage):
    n = 0.0
    for k in STATS:          # C#과 같은 순서로 double 누적
        for lv in range(expected_level(k, stage), expected_level(k, stage + 1)):
            n += cost(k, lv)
    return n


def enemy_gold(stage):
    return int(max(1, round(need(stage) / (units(stage) * farm_factor(stage)))))


def boss_gold(stage):
    return enemy_gold(stage) * 20


def run_gold(stage, eg=None, bg=None):
    """한 판 골드 = 웨이브 적 처치 합 + 보스."""
    eg = enemy_gold(stage) if eg is None else eg
    bg = eg * 20 if bg is None else bg
    return eg * sum(enemy_count(stage, w) for w in range(1, WAVES_PER_STAGE + 1)) + bg


def table(max_stage=1000):
    rows, t = [], 0.0
    for s in range(1, max_stage + 1):
        eg = enemy_gold(s)
        bg = eg * 20
        nd = need(s)
        rg = run_gold(s, eg, bg)
        r = nd / rg
        t += RUN_SECONDS * max(1.0, r)
        rows.append(dict(stage=s, enemy_gold=eg, boss_gold=bg, attack_lv=expected_level("Attack", s),
                         attack_cost=cost("Attack", expected_level("Attack", s)), need=nd, run_gold=rg,
                         R=r, farm_factor=farm_factor(s), cum_hours=t / 3600))
    return rows


def dips(rows):
    return [r["stage"] for i, r in enumerate(rows) if i > 0 and r["enemy_gold"] < rows[i - 1]["enemy_gold"]]


def explain_dip(s):
    """s-1 → s 골드 감소를 종류별 need 변화 + 처치 수 + 반복 계수로 분해."""
    a, b = need_by_stat(s - 1), need_by_stat(s)
    dl = {k: (expected_level(k, s) - expected_level(k, s - 1), expected_level(k, s + 1) - expected_level(k, s)) for k in STATS}
    drops = {k: b[k] - a[k] for k in STATS if b[k] < a[k]}
    ratio = sum(b.values()) / sum(a.values())
    if ratio < 1:
        main = min(drops, key=drops.get)          # 기대 Lv 증분이 줄어 성장분 비용 자체가 감소
    elif units(s) != units(s - 1):
        main = "EnemyCount(처치 수 증가)"
    else:
        main = "FarmFactor(need 증가율 < e^(1/150)=+0.67%/스테이지)"
    return dict(stage=s, main=main, drops=drops, dlv={k: dl[k] for k in drops},
                need_ratio=ratio, units=(units(s - 1), units(s)))


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
    rows = table(n)
    path = os.path.join(HERE, "port_current.csv")
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    d = dips(rows)
    print(f"CSV: {path}")
    print(f"EnemyGold 감소 {len(d)}곳 (1~{n}): {d[:20]}{' …' if len(d) > 20 else ''}")
    causes = {}
    for s in d:
        e = explain_dip(s)
        causes.setdefault(e["main"], []).append(s)
    for k, v in causes.items():
        print(f"  원인 {k}: {len(v)}곳 (첫 {v[:8]})")
    for s in d[:5]:
        e = explain_dip(s)
        parts = ", ".join("%s: dneed %.3g, 기대Lv 증분(s-1→s, s→s+1)=%s" % (k, v, e["dlv"][k]) for k, v in e["drops"].items())
        print(f"  예 {s}: need비 {e['need_ratio']:.3f}, 감소 종류 {{{parts}}}")
    pk = max(rows, key=lambda r: r["enemy_gold"])
    print(f"  EnemyGold 최대: s={pk['stage']} gold {pk['enemy_gold']} — 이후 추세 감소 (1000: {rows[min(n,1000)-1]['enemy_gold']})")
    for s in (1, 10, 50, 100, 200, 300, 500, 1000, 2000):
        if s <= n:
            r = rows[s - 1]
            print(f"  s={s:5} gold {r['enemy_gold']:>12} 공격력비용 {r['attack_cost']:>12} R {r['R']:8.2f} 누적 {r['cum_hours']:8.2f}h")


if __name__ == "__main__":
    main()
