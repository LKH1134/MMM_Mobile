# -*- coding: utf-8 -*-
"""verify 독립 포팅: 방치 전투 프레임 시뮬 (Idle/IdleBattle.cs). test/theory 코드는 보지 않았다.

규칙 출처 (Assets/Scripts/Mobile/Idle/IdleBattle.cs)
- 프레임 순서 Tick :307-326 — 꼬마 → 수확기 → 적 → 무리 체력 → 흐름
- 웨이브/보스 StartWave :330-346, 흐름 UpdateFlow :348-377 (웨이브 사이 waveDelay 0.6, 보스 실패·처치·패배 뒤 ×2 = 1.2초)
- 처치 OnEnemyKilled :379-401 (보스 처치 → 균사석, 스테이지+1, 체력 가득)
- 무리 체력 UpdateParty :405-440 (나온 지 0.4초 넘은 살아 있는 적만 공격, 재생 − 피해, 0 이하 → Defeat :446-456)
- 꼬마 UpdateGgomas :491-511 (처음 타이머 U(0,0.8) :150, 대상 없으면 0.1초 뒤, 던지면 timer += 1/속도)
- 대상 PickTarget :514-520 (나온 지 0.15초 넘고 hp-incoming>0 인 적 중 무작위, 없으면 살아 있는 아무나)
- 피해 Throw :522-543 (FinalAttack × 꼬마 배율 × 치명타 × 일반 적 피해량(보스 제외))
- 비행 0.45초 UpdateShots :552-571 (맞을 때 적이 이미 죽었으면 버려짐 Hit :628-642)
- 죽은 적은 0.25초 뒤 목록에서 빠짐 :665-676
FinalAttack = Attack × (1 + 무기%+펫%) × AdvancedAttack (IdleProgress.cs:175-179). 장비 없음 → 보유 % 0
"""
import random
import port as P

DT = 1 / 60
FLIGHT, WAVE_DELAY, ATK_DELAY, PICK_AGE, DEAD_FADE = 0.45, 0.6, 0.4, 0.15, 0.25


class Enemy:
    __slots__ = ('hp', 'max', 'inc', 'boss', 'dead', 'age', 'deadT', 'gold', 'gen')


def run(stage=1, levels=None, slots=None, own_atk_pct=0.0, own_hp_pct=0.0, seconds=600, seed=0,
        stop_stage=None, dt=DT, test_timer=False):
    """stage부터 seconds초 돌린다. stop_stage에 닿으면 멈춤. 강화 레벨은 고정(강화 안 함)."""
    rng = random.Random(seed)
    lv = {s: 0 for s in P.STATS}
    lv.update(levels or {})
    slots = slots if slots is not None else P.default_formation()
    v = {s: P.value(s, lv[s]) for s in P.STATS}
    atk = v['Attack'] * (1 + own_atk_pct / 100) * v['AdvancedAttack']
    max_hp = v['Hp'] * (1 + own_hp_pct / 100)
    regen = v['HpRegen'] * (1 + own_hp_pct / 100)
    rate = max(0.05, v['Speed'])
    cc, cd, ned = v['CritChance'], v['CritDamage'], v['NormalEnemyDamage']

    g_timer = [rng.uniform(0, 0.8) for _ in slots]
    if test_timer:  # test/sim/Battle.cs 재현용: 누적값 U(0,0.4)에서 시작, 대상 없으면 누적 ≤0.1로 깎음
        g_timer = [(1 - rng.uniform(0, 0.4)) / rate for _ in slots]
    enemies, shots = [], []
    st = dict(stage=stage, wave=0, next_in=0.0, boss=None, boss_left=0.0, hp=max_hp,
              gold=0, myc=0, kills=0, defeats=0, boss_fails=0, clears=[], max_wave=0, t=0.0)
    gen = [0]

    def spawn(boss):
        e = Enemy()
        e.boss, e.dead, e.age, e.deadT, e.inc = boss, False, 0.0, 0.0, 0.0
        s = st['stage']
        e.hp = e.max = P.boss_hp(s) if boss else P.enemy_hp(s, st['wave'])
        e.gold = P.boss_gold(s) if boss else P.enemy_gold(s)
        gen[0] += 1
        e.gen = gen[0]
        enemies.append(e)
        return e

    def start_wave(w):
        st['wave'] = w
        st['max_wave'] = max(st['max_wave'], w)
        if w > P.waves_per_stage(st['stage']):
            st['boss_left'] = P.boss_time(st['stage'])
            st['boss'] = spawn(True)
        else:
            for _ in range(P.enemy_count(st['stage'], w)):
                spawn(False)

    def kill(e, reward):
        e.dead, e.deadT, e.hp = True, 0.0, 0
        if not reward:
            return
        st['gold'] += e.gold
        st['kills'] += 1
        if e.boss:
            st['boss'] = None
            st['myc'] += P.boss_mycelite(st['stage'])
            st['hp'] = max_hp
            st['clears'].append((st['stage'], round(st['t'], 2)))
            st['stage'] += 1
            st['wave'] = 0
            st['next_in'] = WAVE_DELAY * 2

    def alive():
        return [e for e in enemies if not e.dead]

    start_wave(1)
    steps = int(seconds / dt)
    for _ in range(steps):
        st['t'] += dt
        # 꼬마
        if test_timer and not any((not e.dead and e.age > PICK_AGE) for e in enemies):
            g_timer = [max(t, 0.9 / rate) for t in g_timer]
        for i, m in enumerate(slots):
            if m <= 0:
                continue
            g_timer[i] -= dt
            if g_timer[i] > 0:
                continue
            pick = [e for e in enemies if not e.dead and e.age > PICK_AGE and e.hp - e.inc > 0]
            if not pick:
                pick = [e for e in enemies if not e.dead and e.age > PICK_AGE]
            if not pick:
                g_timer[i] = 0.1
                continue
            tgt = pick[rng.randrange(len(pick))]
            g_timer[i] += 1 / rate
            dmg = atk * m
            crit = rng.random() < cc
            if crit:
                dmg *= cd
            if not tgt.boss:
                dmg *= ned
            tgt.inc += dmg
            shots.append([tgt, tgt.gen, 0.0, dmg])
        # 수확기
        keep = []
        for s in shots:
            s[2] += dt / FLIGHT
            if s[2] < 1:
                keep.append(s)
                continue
            e = s[0]
            if e.gen == s[1]:
                e.inc = max(0, e.inc - s[3])
                if not e.dead:
                    e.hp -= s[3]
                    if e.hp <= 0:
                        kill(e, True)
        shots[:] = keep
        if stop_stage and st['stage'] >= stop_stage:
            break   # 목표 스테이지 보스를 잡은 순간 멈춤 (다음 스테이지 웨이브를 세지 않게)
        # 적
        for e in list(enemies):
            if e.dead:
                e.deadT += dt / DEAD_FADE
                if e.deadT >= 1:
                    enemies.remove(e)
                    e.gen = -1  # 풀로 돌아감 (다시 꺼내면 gen이 바뀐다)
                continue
            e.age += dt
        # 무리 체력
        s = st['stage']
        ea, ba = P.enemy_attack(s), P.boss_attack(s)
        dps = sum((ba if e.boss else ea) for e in enemies if not e.dead and e.age > ATK_DELAY)
        st['hp'] = min(max_hp, st['hp'] + regen * dt - dps * dt)
        if st['hp'] <= 0:
            for e in alive():
                kill(e, False)
            st['boss'] = None
            st['wave'] = 0
            st['next_in'] = WAVE_DELAY * 2
            st['hp'] = max_hp
            st['defeats'] += 1
        # 흐름
        b = st['boss']
        if b is not None and not b.dead:
            st['boss_left'] -= dt
            if st['boss_left'] <= 0:
                kill(b, False)
                st['boss'] = None
                st['next_in'] = WAVE_DELAY * 2
                st['wave'] = 0
                st['hp'] = max_hp
                st['boss_fails'] += 1
            continue
        if alive():
            continue
        if st['next_in'] > 0:
            st['next_in'] -= dt
            if st['next_in'] > 0:
                continue
            start_wave(st['wave'] + 1)
            continue
        st['next_in'] = WAVE_DELAY
        if stop_stage and st['stage'] >= stop_stage:
            break
    return st


if __name__ == '__main__':
    import sys
    for s in range(1, 6):
        r = run(stage=s, seconds=300, seed=1, stop_stage=s + 1)
        print(f"스테이지 {s} 무강화 300초: 클리어={r['clears']} 패배={r['defeats']} 보스실패={r['boss_fails']} 최대웨이브={r['max_wave']} 처치={r['kills']} 골드={r['gold']}")
