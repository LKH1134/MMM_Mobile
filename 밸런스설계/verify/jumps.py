# -*- coding: utf-8 -*-
"""수치안 스테이지 표의 난이도 점프 탐지 (독립): 챕터 보스(10의 배수) 행을 빼고, 일반 스테이지끼리 s−1→s 비율을 본다.
적 체력(w1)·보스 체력·적 공격·보스 공격 중 하나라도 이웃 평균 비율보다 크게 튀면 '의도 밖 벽 후보'."""
import proposal_port  # noqa
import port as P
import statistics as S


def series():
    out = []
    for s in range(2, 1001):
        a, b = s - 1, s
        if b % 10 == 0:
            continue
        if a % 10 == 0:
            a = s - 2  # 챕터 보스 행은 건너뛰어 비교
        r = {k: f(b) / f(a) for k, f in (('eHp', lambda x: P.enemy_hp(x, 1)), ('bHp', P.boss_hp),
                                          ('eAtk', P.enemy_attack), ('bAtk', P.boss_attack))}
        if a == s - 2:
            r = {k: v ** 0.5 for k, v in r.items()}  # 두 칸 → 한 칸 기준
        out.append((s, r))
    return out


if __name__ == '__main__':
    ser = series()
    base = {k: S.median([r[k] for s, r in ser if s > 6]) for k in ('eHp', 'bHp', 'eAtk', 'bAtk')}
    print('일반 스테이지 한 칸 비율 중앙', {k: round(v, 4) for k, v in base.items()})
    flag = []
    for s, r in ser:
        if s <= 6:
            continue
        m = max(r['eHp'], r['bHp'])
        a = max(r['eAtk'], r['bAtk'])
        if m > 1.10 or a > 1.10:
            flag.append((s, round(m, 3), round(a, 3)))
    print('점프 >1.10 (스테이지, 체력 비, 공격 비):', flag)
    # 챕터 보스 배율
    ch = [round(P.boss_hp(s) / (P.boss_hp(s - 1) * base['bHp']), 2) for s in range(10, 1001, 10)]
    print('챕터 보스 / 앞 스테이지 기대 보스 체력: 최소', min(ch), '최대', max(ch))
    # 회귀 R1·R3
    eg = [P.enemy_gold(s) for s in range(1, 1201)]
    bg = [P.boss_gold(s) for s in range(1, 1201)]
    print('R1 처치 골드 감소 곳:', [s for s in range(2, 1201) if eg[s - 1] < eg[s - 2]], '보스 골드 감소:', [s for s in range(2, 1201) if bg[s - 1] < bg[s - 2] and s % 10 != 1][:20])
    bad = [s for s in range(1, 1201) for v in (P.enemy_hp(s, 1), P.boss_hp(s), P.enemy_attack(s), P.boss_attack(s), eg[s - 1], bg[s - 1], P.boss_mycelite(s)) if not (v == v) or v <= 0 or v == float('inf')]
    print('R3 NaN·0·inf 곳:', sorted(set(bad))[:20])
    print('표 끝 1000→1001: 적 체력', '%.3g→%.3g' % (P.enemy_hp(1000, 1), P.enemy_hp(1001, 1)), '보스', '%.3g→%.3g' % (P.boss_hp(999), P.boss_hp(1001)),
          '적 공격', '%.3g→%.3g' % (P.enemy_attack(1000), P.enemy_attack(1001)), '처치 골드', eg[999], '→', eg[1000], '보스 균사석', P.boss_mycelite(1000), '→', P.boss_mycelite(1001))
    print('보스 체력 9e18 넘는 첫 스테이지:', next((s for s in range(1, 3001) if P.boss_hp(s) > 9e18), None))
    print('s5→s6 적 체력 ×%.0f, 적 공격 ×%.0f, 보스 체력 ×%.0f' % (P.enemy_hp(6, 1) / P.enemy_hp(5, 1), P.enemy_attack(6) / P.enemy_attack(5), P.boss_hp(6) / P.boss_hp(5)))
