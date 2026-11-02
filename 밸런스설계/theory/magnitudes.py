# 기준 7 (b)(c): 수치 크기와 long 9e18 넘침 범위. python magnitudes.py
import math
import formulas as F
import proposal as PR
import gen_stage as G

LONG = 9e18


def overflow_level(stat):
    lo, hi = 0, F.U(stat)['maxLv'] * 4
    if F.cost(stat, hi) < LONG:
        return None
    while hi - lo > 1:
        m = (lo + hi) // 2
        if F.cost(stat, m) >= LONG:
            hi = m
        else:
            lo = m
    return hi


def stage_of_level(stat, lv):
    for s in range(1, 20000):
        if F.expected_level(stat, s) >= lv:
            return s
    return None


def table(label):
    print(f'== {label}')
    print('s | 공격력(기대 Lv 값) | 일반 적 체력 w1 | 보스 체력 | 적 처치 골드 | 보스 골드 | 공격력 다음 비용 | 보유효과 기대(공격)')
    for s in [1, 10, 100, 1000]:
        lv = F.expected_level('Attack', s)
        print(s, f"{F.value('Attack', lv):.3g}", f"{F.enemy_hp(s, 1):.3g}", f"{F.boss_hp(s):.3g}",
              f"{F.enemy_gold(s):.3g}", f"{F.boss_gold(s):.3g}", f"{F.cost('Attack', lv):.3g}")
    print('비용이 9e18에 닿는 레벨 → 그 레벨의 기대 스테이지 (maxLv)')
    for st in F.STATS:
        L = overflow_level(st)
        print(' ', st, 'maxLv', F.U(st)['maxLv'], '→ 9e18 at Lv', L, 'stage', L and stage_of_level(st, L))


if __name__ == '__main__':
    PR.current()
    table('지금 (+공속 275)')
    PR.apply()
    table('수치안')
    # 보스 골드·앱 밖 하루 골드 (long 지갑)
    for s in [500, 1000]:
        bg = F.boss_gold(s)
        per_s = (F.run_units(s) * F.enemy_gold(s)) / 35
        print('수치안', s, 'boss gold', f'{bg:.3g}', '하루 골드(22.5h)', f'{per_s * 22.5 * 3600:.3g}')
