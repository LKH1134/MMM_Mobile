# -*- coding: utf-8 -*-
"""기준 1 (스테이지 1~5 무강화 클리어) 판정 + 여유 탐색. battle.py(독립 포팅) 위에서만 돈다.
- 판정: 스테이지 s에서 시작, 모든 강화 Lv0·기본 편성·장비 없음, 600초 안에 보스 처치하면 '클리어' (시드 5개 모두)
- 여유: 적 공격(일반·보스)만 ×fa, 적 체력(일반·보스)만 ×fh로 줄였을 때 클리어되는 최대 배율 (이분 탐색)
- 비교: 강화를 하면 어디까지 필요한지 (공격력 Lv·체력 Lv 최소값, 다른 강화 0)
"""
import sys
import port as P
import battle as B

SEEDS = range(5)
orig = dict(ea=P.enemy_attack, ba=P.boss_attack, eh=P.enemy_hp, bh=P.boss_hp)


def patch(fa=1.0, fh=1.0):
    P.enemy_attack = lambda s: orig['ea'](s) * fa
    P.boss_attack = lambda s: orig['ba'](s) * fa
    P.enemy_hp = lambda s, w: orig['eh'](s, w) * fh
    P.boss_hp = lambda s: orig['bh'](s) * fh


def clears(stage, levels=None, seconds=600):
    out = []
    for sd in SEEDS:
        r = B.run(stage=stage, levels=levels, seconds=seconds, seed=sd, stop_stage=stage + 1)
        out.append((bool(r['clears']), r['clears'][0][1] if r['clears'] else None, r['defeats'], r['boss_fails'], r['max_wave']))
    return out


def bisect(stage, key):
    lo, hi = 0.0, 1.0
    for _ in range(14):
        mid = (lo + hi) / 2
        patch(**{key: mid})
        if all(c[0] for c in clears(stage)):
            lo = mid
        else:
            hi = mid
    patch()
    return lo


def min_level(stage, stat, other=None, top=400):
    lo, hi = 0, top
    base = dict(other or {})
    if not all(c[0] for c in clears(stage, {**base, stat: hi})):
        return None
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if all(c[0] for c in clears(stage, {**base, stat: mid})):
            hi = mid
        else:
            lo = mid
    return hi


if __name__ == '__main__':
    patch()
    for s in range(1, 6):
        c = clears(s)
        ok = all(x[0] for x in c)
        print(f"[s{s}] 무강화 클리어={ok} (시드별 클리어,시각,패배,보스실패,최대웨이브) {c}")
    for s in range(1, 6):
        fa, fh = bisect(s, 'fa'), bisect(s, 'fh')
        print(f"[s{s}] 무강화로 깨려면: 적 공격 ×{fa:.3f} 이하 (체력 그대로) | 적 체력 ×{fh:.3f} 이하 (공격 그대로)")
        sys.stdout.flush()
    for s in range(1, 6):
        a = min_level(s, 'Attack')
        h = min_level(s, 'Hp')
        print(f"[s{s}] 참고 — 공격력만 올리면 Lv {a}, 체력만 올리면 Lv {h} (나머지 0)")
        sys.stdout.flush()


def bisect_both(stage):
    lo, hi = 0.0, 1.0
    for _ in range(12):
        mid = (lo + hi) / 2
        patch(fa=mid, fh=mid)
        if all(c[0] for c in clears(stage)):
            lo = mid
        else:
            hi = mid
    patch()
    return lo


def min_level_both(stage, top=200):
    lo, hi = 0, top
    if not all(c[0] for c in clears(stage, {'Attack': hi, 'Hp': hi})):
        return None
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if all(c[0] for c in clears(stage, {'Attack': mid, 'Hp': mid})):
            hi = mid
        else:
            lo = mid
    return hi


def main2():
    patch()
    for s in range(1, 6):
        f = bisect_both(s)
        L = min_level_both(s)
        print(f"[s{s}] 적 공격·체력 같이 ×{f:.3f} 이하면 무강화 클리어 | 참고: 공격력=체력=Lv {L}면 클리어 (나머지 0)")
        sys.stdout.flush()
