# -*- coding: utf-8 -*-
"""test/sim 대조: 게임 규칙(battle.py) vs test 꼬마 타이머 규칙(test_timer=True) — 한 번 진입 클리어율.
test minlv 정의: 공격력=체력=L, 한 번 진입(1웨이브부터, 체력 가득)으로 보스 처치 비율 ≥ 90%."""
import sys
import battle as B

N = 40


def rate(stage, L, tt):
    ok = 0
    for sd in range(N):
        r = B.run(stage=stage, levels={'Attack': L, 'Hp': L}, seconds=400, seed=1000 + sd, stop_stage=stage + 1, test_timer=tt)
        ok += bool(r['clears']) and r['defeats'] == 0 and r['boss_fails'] == 0
    return ok / N


def min_l(stage, tt, top=260):
    lo, hi = 0, top
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if rate(stage, mid, tt) >= 0.9:
            hi = mid
        else:
            lo = mid
    return hi


if __name__ == '__main__':
    for s in range(1, 6):
        g, t = min_l(s, False), min_l(s, True)
        print(f"s{s} 최소 L(≥90% 한 번에): 게임 규칙 {g} · test 타이머 규칙 {t}")
        sys.stdout.flush()
