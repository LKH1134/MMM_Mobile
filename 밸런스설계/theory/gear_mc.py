# 장비 보유 효과 분산 (기준 8 등급 비 vs 천장). python gear_mc.py
import random, math, statistics
import formulas as F
import proposal as PR

def sim(pulls, ratio, bonus, top, pity=0, seed=0):
    rng = random.Random(seed)
    F.set_overrides({'ownBase': [2 * ratio ** g for g in range(5)], 'const': {'equipLevelBonus': bonus, 'equipTopGradeMul': top}})
    odds = F.T.gacha
    copies = [0] * 15; lvl = 1; exp = 0; since = 0
    for i in range(pulls):
        o = odds[min(lvl, len(odds)) - 1]['odds']
        r = rng.random() * 100; acc = 0; g = 4
        for k in range(5):
            acc += o[k]
            if r < acc: g = k; break
        since = 0 if g == 4 else since + 1
        if pity and since >= pity: g = 4; since = 0
        sub = 0 if rng.random() < 0.6 else (1 if rng.random() < 0.75 else 2)
        copies[g * 3 + sub] += 1
        exp += 1
        if lvl < len(odds) and exp >= odds[lvl - 1]['exp']: exp -= odds[lvl - 1]['exp']; lvl += 1
    tot = best = 0
    for i, n in enumerate(copies):
        if n:
            L = F.level_from_copies(n); v = F.base_own_percent(i // 3, i % 3) * (1 + bonus * (L - 1))
            tot += v; best = max(best, v)
    return tot + best * 3

for label, ratio, bonus, top, pity in [('v1 등급×10', 10, 0.01, 10, 0), ('등급×5', 5, 0.007, 5, 0), ('등급×10 + 천장 200', 10, 0.01, 10, 200), ('등급×5 + 천장 200', 5, 0.007, 5, 200)]:
    for pulls in (200, 600):
        xs = sorted(sim(pulls, ratio, bonus, top, pity, s) for s in range(400))
        p10, p50, p90 = xs[40], xs[200], xs[360]
        print(f'{label:16s} {pulls:4d}뽑  중앙 {p50:10.0f}%  p10~p90 ×{p90 / p10:5.2f}  (p10 {p10:.0f}, p90 {p90:.0f})')
