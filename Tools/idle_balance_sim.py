"""방치형 밸런스 시뮬레이션 — 기대 성장 곡선 기반 (BALANCE_IDLE.md, IdleProgress.cs와 같은 공식).
실행: python Tools/idle_balance_sim.py  (스테이지 1000까지, 연속 플레이 시간 기준)
스테이지 s에서 '이만큼 키웠을 것'(기대 레벨·보유효과)을 정하고, 적 체력·공격력·골드를 그 곡선에서 계산한다.
플레이어는 골드를 가중치 기준 가장 싼 강화에 쓰고, 균사석은 11뽑(무기→장신구→펫 순)에 쓴다.
"""
import math, random, sys

SLOTS, WAVES, BOSS_TIME, WAVE_DELAY, FLIGHT = 5, 5, 30.0, 0.6, 0.45

# 강화: 값 = base + per*lv (속도만 base*(1+per*lv)), 비용 = cbase*(1+lv/10)^CEXP
#            base    per     max     cbase  weight unlock
STATS = {
    'atk':   (10.0,  2.0,    100000, 10,   1.0,  None),
    'hp':    (100.0, 20.0,   100000, 10,   1.0,  None),
    'regen': (2.0,   0.4,    100000, 15,   1.3,  ('hp', 20)),
    'speed': (0.8,   0.01,   150,    300,  1.0,  ('atk', 30)),     # 0.8*(1+1%*lv), 최대 +150%
    'cc':    (0.0,   0.0001, 10000,  30,   1.6,  ('speed', 10)),   # 0.01%/lv
    'cd':    (1.5,   0.001,  10000,  30,   1.6,  ('cc', 100)),     # +0.1%/lv
    'gold':  (1.0,   0.01,   2000,   50,   1.6,  ('atk', 50)),     # +1%/lv
}
CEXP = {'atk': 2.5, 'hp': 2.5, 'regen': 2.5, 'speed': 3.5, 'cc': 2.5, 'cd': 2.5, 'gold': 2.7}

def val(k, lv):
    b, per = STATS[k][0], STATS[k][1]
    return b * (1 + per * lv) if k == 'speed' else b + per * lv

def cost(k, lv):
    return math.ceil(STATS[k][3] * (1 + lv / 10.0) ** CEXP[k])

# ---- 기대 성장 곡선 (설계 의도) ----
LV_PER_STAGE = 10                                   # 핵심 능력치(공격력·체력) 기대 레벨 = 10 × (스테이지 - 1)
RATIO = {'atk': 1.0, 'hp': 1.0, 'regen': 0.6, 'cc': 0.6, 'cd': 0.5, 'gold': 0.2}
def exp_lv(k, s):
    s = max(0, s - 1)                               # 스테이지 1 = Lv 0
    if k == 'speed': return min(150, int(1.5 * s))
    return min(STATS[k][2], int(LV_PER_STAGE * s * RATIO[k]))
def exp_own_atk(s): return 1 + 0.012 * s            # 무기+펫 보유효과 기대 배수
def exp_own_hp(s):  return 1 + 0.007 * s            # 장신구 보유효과 기대 배수
def exp_hit(s):
    cc = min(1, val('cc', exp_lv('cc', s))); cd = val('cd', exp_lv('cd', s))
    return val('atk', exp_lv('atk', s)) * exp_own_atk(s) * (1 + cc * (cd - 1))
def exp_rate(s): return SLOTS * val('speed', exp_lv('speed', s))
def exp_dps(s):  return exp_hit(s) * exp_rate(s)
def exp_hp(s):   return val('hp', exp_lv('hp', s)) * exp_own_hp(s)
def exp_regen(s): return val('regen', exp_lv('regen', s)) * exp_own_hp(s)

# ---- 적 ----
HITS_PER_ENEMY = 2.0      # 기대 성장이면 일반 적은 투척 2번
BOSS_SECONDS = 18.0       # 기대 성장이면 보스는 18초 (제한 30초)
def enemy_count(s, w): return min(15, max(5, 5 + 2 * (w - 1) + (s - 1)))
def enemy_hp(s, w):  return exp_hit(s) * HITS_PER_ENEMY * (1 + 0.08 * (w - 1))
def boss_hp(s):      return exp_dps(s) * BOSS_SECONDS
def enemy_atk(s):    return 0.007 * exp_hp(s) + 0.15 * exp_regen(s)     # 적 1마리 초당 피해
def boss_atk(s):     return exp_regen(s) + 0.025 * exp_hp(s)

# ---- 골드: 기대 성장에 필요한 골드 / (처치 수 × 반복 계수) ----
def farm(s): return 1 + (s / 25.0) ** 1.5                     # 스테이지를 평균 몇 번 돌아야 다음 성장분이 모이는지
def stage_need(s):
    return sum(sum(cost(k, l) for l in range(exp_lv(k, s), exp_lv(k, s + 1))) for k in STATS)
def kill_units(s): return sum(enemy_count(s, w) for w in range(1, WAVES + 1)) + 20
def enemy_gold(s): return max(1, round(stage_need(s) / (kill_units(s) * farm(s))))
def boss_gold(s):  return enemy_gold(s) * 20
def boss_gem(s):   return 5 + s // 10

# ---- 뽑기 ----
OWN_BASE = [2, 5, 15, 40, 100]; OWN_PER_COPY = 0.05; OWN_CAP = 101
def odds(level):
    st = min(max(level, 1), 10) - 1
    lg, ep, ra, un = 0.5 + 0.5 * st, 3 + 1.5 * st, 12 + 2 * st, 30
    return [100 - lg - ep - ra - un, un, ra, ep, lg]
class Gacha:
    def __init__(s): s.owned = [0] * 5; s.lv = 1; s.exp = 0
    def roll(s, n, rng):
        o = odds(s.lv)
        for _ in range(n):
            r = rng.random() * 100; acc = 0
            for g in range(5):
                acc += o[g]
                if r < acc: s.owned[g] += 1; break
            else: s.owned[4] += 1
        s.exp += n
        while s.lv < 10 and s.exp >= 100 * s.lv: s.exp -= 100 * s.lv; s.lv += 1
    def own(s):
        return sum(OWN_BASE[g] * (1 + OWN_PER_COPY * (min(c, OWN_CAP) - 1)) for g, c in enumerate(s.owned) if c > 0)

def run(max_stage=1000, seed=1, report=(1, 5, 10, 20, 30, 50, 100, 200, 300, 500, 700, 1000), ad_per_day=True):
    rng = random.Random(seed)
    lv = {k: 0 for k in STATS}; gold = 0.0; gem = 0
    G = {'w': Gacha(), 'a': Gacha(), 'p': Gacha()}; order = 'wap'; oi = 0
    t = 0.0; s = 1; rows = []; fails = {'wave': 0, 'boss_t': 0, 'boss_hp': 0}; next_ad = 0.0

    def unlocked(k):
        u = STATS[k][5]; return u is None or lv[u[0]] >= u[1]
    def spend():
        nonlocal gold
        while True:
            best = None
            for k, v in STATS.items():
                if lv[k] >= v[2] or not unlocked(k): continue
                c = cost(k, lv[k]) * v[4]
                if best is None or c < best[0]: best = (c, k)
            if best is None or cost(best[1], lv[best[1]]) > gold: return
            gold -= cost(best[1], lv[best[1]]); lv[best[1]] += 1

    while s <= max_stage and t < 3600 * 24 * 90:
        spend()
        if ad_per_day and t >= next_ad:            # 매일 광고 11뽑 3종
            for g in G.values(): g.roll(11, rng)
            next_ad += 86400
        while gem >= 100: gem -= 100; G[order[oi % 3]].roll(11, rng); oi += 1
        oa = 1 + (G['w'].own() + G['p'].own()) / 100; oh = 1 + G['a'].own() / 100
        cc = min(1, val('cc', lv['cc'])); cd = val('cd', lv['cd'])
        hit = val('atk', lv['atk']) * oa * (1 + cc * (cd - 1))
        rate = SLOTS * val('speed', lv['speed']); dps = hit * rate
        hpmax = val('hp', lv['hp']) * oh; regen = val('regen', lv['regen']) * oh
        gm = val('gold', lv['gold']); hp = hpmax; ok = True
        for w in range(1, WAVES + 1):
            n = enemy_count(s, w); eh = enemy_hp(s, w)
            per = max(1, math.ceil(eh / hit))
            tw = n * per / rate + FLIGHT + WAVE_DELAY
            hp = min(hpmax, hp + regen * tw - enemy_atk(s) * n * tw / 2)
            t += tw
            if hp <= 0: fails['wave'] += 1; ok = False; gold += n * enemy_gold(s) * gm * 0.5; break
            gold += n * enemy_gold(s) * gm
        if ok:
            tb = boss_hp(s) / dps
            if tb > BOSS_TIME: fails['boss_t'] += 1; ok = False; t += BOSS_TIME
            elif hp + (regen - boss_atk(s)) * tb <= 0: fails['boss_hp'] += 1; ok = False; t += tb
            else: t += tb + WAVE_DELAY * 2; gold += boss_gold(s) * gm; gem += boss_gem(s)
        if ok:
            if s in report:
                rows.append(dict(s=s, h=t / 3600, lv=dict(lv), dps=dps, hp=hpmax, regen=regen, oa=oa, oh=oh,
                                 acost=cost('atk', lv['atk']), gk=enemy_gold(s) * gm, gold=gold,
                                 leg=sum(g.owned[4] for g in G.values()), fails=dict(fails)))
                for k in fails: fails[k] = 0
            s += 1
    return rows

def fmt(x):
    for u, d in (('Q', 1e15), ('T', 1e12), ('B', 1e9), ('M', 1e6), ('K', 1e3)):
        if x >= d: return f'{x / d:.1f}{u}'
    return f'{x:.0f}'

if __name__ == '__main__':
    rows = run()
    print('stage   hours | atk    hp     regen spd  cc    cd    gold | DPS      HP       regen  | ownA  ownH | atkCost  gold/kill | leg | fails')
    for r in rows:
        L = r['lv']
        print(f"{r['s']:5d} {r['h']:7.1f} | {L['atk']:6d} {L['hp']:6d} {L['regen']:5d} {L['speed']:4d} {L['cc']:5d} {L['cd']:5d} {L['gold']:4d} | "
              f"{fmt(r['dps']):>8} {fmt(r['hp']):>8} {fmt(r['regen']):>6} | x{r['oa']:4.1f} x{r['oh']:4.1f} | {fmt(r['acost']):>8} {fmt(r['gk']):>8} | {r['leg']:3d} | {r['fails']}")
