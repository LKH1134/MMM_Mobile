# -*- coding: utf-8 -*-
"""수치안 s5→s6 턱: s1~5를 한 번씩 깨며 번 골드(+ 퀘스트 골드 선택)로 s6을 깰 수 있나.
방치 전투는 패배해도 같은 스테이지에서 다시 시작 → s6에서 한 마리도 못 잡으면 처치 골드 0."""
import itertools
import sys
import proposal_port  # noqa
import port as P
import battle as B


def earned_s1_5():
    g = 0
    for s in range(1, 6):
        g += sum(P.enemy_count(s, w) for w in range(1, 6)) * P.enemy_gold(s) + P.boss_gold(s)
    return g


def cum(stat, L):
    return sum(P.cost(stat, l) for l in range(L))


def max_level(stat, budget):
    L, c = 0, 0
    while c + P.cost(stat, L) <= budget:
        c += P.cost(stat, L)
        L += 1
    return L, c


def best_clear(budget, seeds=8):
    """공격력·체력·재생·공속에 예산을 나눠 s6 한 번 진입 클리어율이 가장 높은 조합"""
    best = (0, None)
    for fa, fr, fs in itertools.product((0.3, 0.45, 0.6, 0.75), (0, 0.1, 0.2), (0, 0.1)):
        if fa + fr + fs > 0.95:
            continue
        la, ca = max_level('Attack', budget * fa)
        ls, cs = (max_level('Speed', budget * fs) if la >= 30 else (0, 0))
        lh, ch = max_level('Hp', budget - ca - cs - budget * fr)
        lr, cr = (max_level('HpRegen', budget * fr) if lh >= 20 else (0, 0))
        lv = {'Attack': la, 'Hp': lh, 'HpRegen': lr, 'Speed': ls}
        ok = 0
        for sd in range(seeds):
            r = B.run(stage=6, levels=lv, seconds=120, seed=sd, stop_stage=7)
            ok += bool(r['clears']) and r['defeats'] == 0
        if ok / seeds > best[0] or best[1] is None:
            best = (ok / seeds, lv)
        if best[0] == 1:
            break
    return best


if __name__ == '__main__':
    g = earned_s1_5()
    q = (P.enemy_gold(6) * (200 + 300))  # 강화 20회 퀘스트 goldKills 200 + 보스 도전 3회 goldKills 300 (s6에서 받음)
    print('s1~5 한 번씩 번 골드', g, '| s6에서 받는 퀘스트 골드(강화·보스 도전)', q)
    for label, b in (('처치 골드만', g), ('+퀘스트 골드', g + q)):
        print(label, b, best_clear(b))
        sys.stdout.flush()
    # 필요한 예산 탐색
    for b in (1e6, 2e6, 4e6, 8e6, 16e6):
        print('예산', b, best_clear(b))
        sys.stdout.flush()
