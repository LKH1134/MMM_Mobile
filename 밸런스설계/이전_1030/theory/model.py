# -*- coding: utf-8 -*-
"""이론 모델: IdleProgress.cs 포팅 + 비용·골드 구조안 비교.
실행: python 밸런스설계/theory/model.py [current|proto|proposal|all]  (결정적, 난수 없음)
한 판 = 웨이브 5개 + 보스 1회, 40초 고정 가정. R(s) = 그 스테이지 기대 성장분 비용 / 판당 골드.
"""
import math
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

# ── IdleProgress.cs Defs (2026-10-30 기준) ──
D = {
    'Attack':            dict(b=10,  p=2,     mx=100000, cb=10,  ce=2.5),
    'Hp':                dict(b=100, p=20,    mx=100000, cb=10,  ce=2.5),
    'HpRegen':           dict(b=2,   p=0.4,   mx=100000, cb=15,  ce=2.5, u='Hp', ul=20),
    'Speed':             dict(b=0.8, p=0.01,  mx=150,    cb=300, ce=3.5, u='Attack', ul=30),
    'CritChance':        dict(b=0,   p=0.001, mx=1000,   cb=30,  ce=2.5, u='Attack', ul=100, every=100, step=25),
    'CritDamage':        dict(b=1.5, p=0.004, mx=2500,   cb=30,  ce=2.5, u='Attack', ul=300, every=100, step=25),
    'AdvancedAttack':    dict(b=1,   p=0.005, mx=2000,   cb=1e7, ce=1.5, u='Attack', ul=3000, every=100, step=25),
    'NormalEnemyDamage': dict(b=1,   p=0.01,  mx=1000,   cb=2e7, ce=1.5, u='AdvancedAttack', ul=150),
}
LPS = 15
WAVES = 5
FARM_SCALE = 150.0
RUN_SEC = 40.0
LONG_MAX = 9e18

# 비용 지수 항 e^(Lv / costScale). 0 = 없음
SCALE_NONE = {k: 0 for k in D}
# 관리자 시안 (manager_proto.py)
SCALE_PROTO = {'Attack': 1500, 'Hp': 1500, 'HpRegen': 900, 'Speed': 0, 'CritChance': 300,
               'CritDamage': 225, 'AdvancedAttack': 190, 'NormalEnemyDamage': 95}
# 이론안: costScale = FarmScale × (기대 레벨이 스테이지당 오르는 양) → 기대 레벨에서 e^((s-1)/FarmScale)와 같은 속도
#   공격력·체력 15/스테이지, 재생 9, 치명타 확률 3(=15/100×25×0.8), 치명타 피해 2.25, 상급 공격력 1.875, 일반 적 피해량 0.9375
SCALE_PROPOSAL = {'Attack': 2250, 'Hp': 2250, 'HpRegen': 1350, 'Speed': 0, 'CritChance': 450,
                  'CritDamage': 338, 'AdvancedAttack': 281, 'NormalEnemyDamage': 141}

# 이론안 골드: EnemyGold(s) = round(GoldBase × (1 + (s-1)/GoldStep)^GoldExp)
#   스테이지 1 = 30 (지금과 같음). 큰 s에서 ≈ 4 × (1.5s)^2.5 = 공격력 비용 거듭제곱 부분과 같은 모양
#   (적합: 이론안 비용의 "R=FarmFactor가 되는 골드"를 s=10~1000에서 x^2.5로 나눈 비 3.6~5.5 → 4)
GOLD_BASE = 30.0
GOLD_STEP = 1.5
GOLD_EXP = 2.5


def val(k, lv):
    d = D[k]
    lv = max(0, min(lv, d['mx']))
    return d['b'] * (1 + d['p'] * lv) if k == 'Speed' else d['b'] + d['p'] * lv


def make_cost(scale):
    def cost(k, lv):
        d = D[k]
        g = math.exp(lv / scale[k]) if scale[k] else 1.0
        return min(LONG_MAX, math.ceil(d['cb'] * (1 + lv / 10.0) ** d['ce'] * g))
    return cost


def exp_capped(k, stage, ratio):
    d = D[k]
    gate = exp_lv(d['u'], stage)
    if gate < d['ul']:
        return 0
    cap = ((gate - d['ul']) / d['every'] + 1) * d['step']
    return int(min(d['mx'], cap * ratio))


def exp_lv(k, stage):
    s = max(0, stage - 1)
    d = D[k]
    if k == 'Speed': return min(d['mx'], int(1.5 * s))
    if k == 'HpRegen': return min(d['mx'], int(LPS * s * 0.6))
    if k == 'CritChance': return exp_capped(k, stage, 0.8)
    if k == 'CritDamage': return exp_capped(k, stage, 0.6)
    if k == 'AdvancedAttack': return exp_capped(k, stage, 0.5)
    if k == 'NormalEnemyDamage':
        return max(0, min(d['mx'], int((exp_lv('AdvancedAttack', stage) - d['ul']) / 2)))
    return min(d['mx'], LPS * s)


def count(s, w): return max(5, min(15, 5 + 2 * (w - 1) + (s - 1)))
def units(s): return 20 + sum(count(s, w) for w in range(1, WAVES + 1))   # 보스 = 일반 20마리분
def farm(s): return math.exp((s - 1) / FARM_SCALE)


def need(cost, s):
    """스테이지 s → s+1 기대 성장분 비용 합, 최대 단일 비용"""
    total, top = 0.0, 0.0
    for k in D:
        for lv in range(exp_lv(k, s), exp_lv(k, s + 1)):
            c = cost(k, lv)
            total += c
            top = max(top, c)
    return total, top


def gold_derived(cost):
    return lambda s: max(1, round(need(cost, s)[0] / (units(s) * farm(s))))


def gold_closed(s):
    return max(1, round(GOLD_BASE * (1 + (s - 1) / GOLD_STEP) ** GOLD_EXP))


PLANS = {
    'current':  ("현재 (비용 거듭제곱, 골드=필요비용 역산)", make_cost(SCALE_NONE), None),
    'proto':    ("관리자 시안 (비용×e^(Lv/스케일), 골드 역산)", make_cost(SCALE_PROTO), None),
    'proposal': ("이론안 (비용×e^(Lv/스케일), 골드 독립 함수)", make_cost(SCALE_PROPOSAL), gold_closed),
}
TABLE_STAGES = (1, 10, 50, 100, 200, 300, 400, 500, 700, 1000, 1500, 2000)


def run(name, last=2000):
    title, cost, gold = PLANS[name]
    if gold is None:
        gold = gold_derived(cost)
    prev, t, dips, rows = 0, 0.0, [], {}
    max_cost, max_gold, max_need = 0.0, 0, 0.0
    for s in range(1, last + 1):
        g = gold(s)
        if g < prev:
            dips.append(s)
        prev = g
        n, top = need(cost, s)
        r = n / (g * units(s))           # 이 스테이지에서 다음 성장분을 모으는 판 수
        t += RUN_SEC * max(1.0, r)        # 최소 1판(보스를 잡아야 넘어감)
        rows[s] = (g, cost('Attack', exp_lv('Attack', s)), r, t)
        if s <= 1000:
            max_cost, max_gold, max_need = max(max_cost, top), max(max_gold, g), max(max_need, n)
    print(f"\n== {name}: {title}")
    d1000 = [x for x in dips if x <= 1000]
    print(f"골드 감소 지점: 1~1000 {len(d1000)}개 {d1000[:8]}  / 1~{last} {len(dips)}개")
    print(f"1~1000 최대: 단일 강화 비용 {max_cost:.3g}, 처치당 골드 {max_gold:.3g}, 보스 골드 {max_gold*20:.3g}, 스테이지 성장분 합 {max_need:.3g}")
    print(f"{'s':>5} {'처치당골드':>12} {'공격력다음비용':>14} {'R(판)':>9} {'누적':>14}")
    for s in TABLE_STAGES:
        if s > last: continue
        g, c, r, tt = rows[s]
        h = tt / 3600
        ts = f"{h:8.1f}h" + (f" ({h/24:.1f}일)" if h >= 24 else "")
        print(f"{s:5} {g:12.4g} {c:14.4g} {r:9.2f} {ts:>14}")
    return rows, dips


if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 else 'all'
    for n in (PLANS if which == 'all' else [which]):
        run(n)


# ── 검증 요청 verify#2: 실수 기대 Lv로 need를 계산 (int 버림 제거, 비용 연속 적분) ──
def exp_lv_real(k, stage):
    s = max(0.0, stage - 1.0)
    d = D[k]
    if k == 'Speed': return min(d['mx'], 1.5 * s)
    if k == 'HpRegen': return min(d['mx'], LPS * s * 0.6)
    if k in ('CritChance', 'CritDamage', 'AdvancedAttack'):
        ratio = {'CritChance': 0.8, 'CritDamage': 0.6, 'AdvancedAttack': 0.5}[k]
        gate = exp_lv_real(d['u'], stage)
        if gate < d['ul']: return 0.0
        return min(d['mx'], ((gate - d['ul']) / d['every'] + 1) * d['step'] * ratio)
    if k == 'NormalEnemyDamage':
        return max(0.0, min(d['mx'], (exp_lv_real('AdvancedAttack', stage) - d['ul']) / 2))
    return min(d['mx'], LPS * s)


def need_real(scale, s, steps=16):
    """∫ 연속 비용 dLv over [L(s), L(s+1)] (심프슨). 정수 레벨 합과 평균적으로 같다"""
    total = 0.0
    for k in D:
        a, b = exp_lv_real(k, s), exp_lv_real(k, s + 1)
        if b <= a: continue
        d = D[k]
        f = lambda x: d['cb'] * (1 + x / 10.0) ** d['ce'] * (math.exp(x / scale[k]) if scale[k] else 1.0)
        h = (b - a) / steps
        total += h / 3 * (f(a) + f(b) + sum((4 if i % 2 else 2) * f(a + i * h) for i in range(1, steps)))
    return total


def r_report(last=1000):
    """R(s) 국소 감소: 정수 기대 Lv(게임 코드 그대로) vs 실수 기대 Lv"""
    cost = make_cost(SCALE_PROPOSAL)
    ri = [need(cost, s)[0] / (gold_closed(s) * units(s)) for s in range(1, last + 1)]
    rr = [need_real(SCALE_PROPOSAL, s) / (gold_closed(s) * units(s)) for s in range(1, last + 1)]
    for name, r in (("정수 Lv", ri), ("실수 Lv", rr)):
        dec = [s for s in range(2, last + 1) if r[s - 1] < r[s - 2]]
        worst = min(r[s - 1] / r[s - 2] for s in range(2, last + 1))
        jump = max(r[s - 1] / r[s - 2] for s in range(2, last + 1))
        rf = [r[s - 1] / farm(s) for s in range(10, last + 1)]
        print(f"{name}: R 감소 {len(dec)}곳 {dec[:10]}, 인접 비 최소 {worst:.3f} 최대 {jump:.3f}, R/FarmFactor(s≥10) {min(rf):.2f}~{max(rf):.2f}")
    return ri, rr
