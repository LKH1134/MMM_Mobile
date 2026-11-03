# GameBalance.xlsx를 IdleProgress.cs(새 공식)에 맞춘다: 업그레이드 시트, 리소스 시트, 스테이지 스냅샷, info 설명
import math
import openpyxl

PATH = 'Assets/Data/Tables/GameBalance.xlsx'

# ── IdleProgress.cs 포팅 ──
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
SLOTS = 5
WAVES = 5


def val(k, lv):
    d = D[k]
    lv = max(0, min(lv, d['mx']))
    return d['b'] * (1 + d['p'] * lv) if k == 'Speed' else d['b'] + d['p'] * lv


def cost(k, lv):
    d = D[k]
    return min(9e18, math.ceil(d['cb'] * (1 + lv / 10.0) ** d['ce']))


def expected_capped(k, stage, ratio):
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
    if k == 'CritChance': return expected_capped(k, stage, 0.8)
    if k == 'CritDamage': return expected_capped(k, stage, 0.6)
    if k == 'AdvancedAttack': return expected_capped(k, stage, 0.5)
    if k == 'NormalEnemyDamage': return max(0, min(d['mx'], int((exp_lv('AdvancedAttack', stage) - d['ul']) / 2)))
    return min(d['mx'], LPS * s)


def hit(s):
    cc = min(1, val('CritChance', exp_lv('CritChance', s)))
    cd = val('CritDamage', exp_lv('CritDamage', s))
    return val('Attack', exp_lv('Attack', s)) * (1 + 0.012 * s) * val('AdvancedAttack', exp_lv('AdvancedAttack', s)) * (1 + cc * (cd - 1))


def hp(s): return val('Hp', exp_lv('Hp', s)) * (1 + 0.007 * s)
def regen(s): return val('HpRegen', exp_lv('HpRegen', s)) * (1 + 0.007 * s)
def count(s, w): return max(5, min(15, 5 + 2 * (w - 1) + (s - 1)))
def farm(s): return math.exp((s - 1) / 150.0)


def gold(s):
    need = 0.0
    for k in D:
        for lv in range(exp_lv(k, s), exp_lv(k, s + 1)):
            need += cost(k, lv)
    units = 20 + sum(count(s, w) for w in range(1, WAVES + 1))
    return max(1, round(need / (units * farm(s))))


GROWTH = {'Attack': 1/1500, 'Hp': 1/1500, 'HpRegen': 1/900, 'Speed': 0, 'CritChance': 1/300, 'CritDamage': 1/225, 'AdvancedAttack': 1/190, 'NormalEnemyDamage': 1/95}
import sys
scale = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0
def cost(k, lv):
    d = D[k]
    return min(9e18, math.ceil(d['cb'] * (1 + lv / 10.0) ** d['ce'] * math.exp(GROWTH[k] * scale * lv)))

prev = 0; t = 0; dips = []
rows = {}
for s in range(1, 1001):
    g = gold(s)
    if g < prev: dips.append(s)
    prev = g
    t += 40 * farm(s)
    rows[s] = (g, farm(s), t)
print("dips:", dips[:10], len(dips))
for s in (1, 10, 50, 100, 200, 300, 400, 500, 600, 700, 800, 1000):
    g, f, tt = rows[s]
    print(f"{s:5} gold {g:12.4g}  반복 {f:7.1f}  누적 {tt/3600:8.1f}h   공격력 강화비 {cost('Attack', exp_lv('Attack', s)):.3g}")
