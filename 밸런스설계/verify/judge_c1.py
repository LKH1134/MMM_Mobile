# -*- coding: utf-8 -*-
"""수치안 기준 1: 무강화(Lv0·기본 편성 4.38·장비 없음)로 s1~5를 한 번 진입에 깨는지 + 최저 체력·보스 시간 + 여유.
그리고 s5를 깨고 s6에 들어갈 때 가진 골드로 s6을 깰 수 있는지 (방치형은 패배해도 같은 스테이지 → s5로 돌아가 벌 수 없다)."""
import sys
import proposal_port  # noqa: F401 (수치안 적용)
import port as P
import battle as B

N = 40


def one(stage, seed, levels=None, fa=1.0, fh=1.0):
    ea, ba, eh, bh = P.enemy_attack, P.boss_attack, P.enemy_hp, P.boss_hp
    if fa != 1 or fh != 1:
        P.enemy_attack = lambda s: ea(s) * fa
        P.boss_attack = lambda s: ba(s) * fa
        P.enemy_hp = lambda s, w: eh(s, w) * fh
        P.boss_hp = lambda s: bh(s) * fh
    try:
        return B.run(stage=stage, levels=levels, seconds=300, seed=seed, stop_stage=stage + 1)
    finally:
        P.enemy_attack, P.boss_attack, P.enemy_hp, P.boss_hp = ea, ba, eh, bh


def first_try_rate(stage, **kw):
    ok, t = 0, []
    for sd in range(N):
        r = one(stage, 500 + sd, **kw)
        good = bool(r['clears']) and r['defeats'] == 0 and r['boss_fails'] == 0
        ok += good
        if good:
            t.append(r['clears'][0][1])
    return ok / N, (min(t), max(t)) if t else None


if __name__ == '__main__':
    for s in range(1, 6):
        rt, tr = first_try_rate(s)
        # 여유: 적 공격·체력을 같이 키워서 한 번 진입 ≥90% 유지되는 최대 배율
        lo, hi = 1.0, 4.0
        for _ in range(10):
            mid = (lo + hi) / 2
            if first_try_rate(s, fa=mid, fh=mid)[0] >= 0.9:
                lo = mid
            else:
                hi = mid
        print(f"s{s}: 무강화 한 번 진입 클리어 {rt:.0%} (클리어 시각 {tr}) · 적 공격·체력 같이 ×{lo:.2f}까지 ≥90%")
        sys.stdout.flush()
