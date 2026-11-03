# -*- coding: utf-8 -*-
"""theory 수치안 v1(이론.md 10절)을 verify 포팅(port.py) 위에 직접 얹는다. theory/*.py는 쓰지 않고, 이론.md 10.1(코드)·10.2(표) 문장과
theory/stage_proposal.csv(표 값 그 자체)만 읽는다.

C1  적 체력 표 우선: EnemyHp = (표 안 ? row.enemyHp : 공식) × (1 + growth×(w−1)), BossHp = 표 안 ? row.bossHp : 공식
C2  valueMode Power: base × (1 + lv/valueDivisor)^valueExponent
C3  앱 밖 골드 × offlineGoldRate,  C4 광고 뽑기 adGachaPulls,  C5 퀘스트 stage = 보스 등장 횟수  (전투 판정엔 영향 없음)
"""
import csv
import math
import os
import port as P

HERE = os.path.dirname(__file__)
CSV = os.path.join(HERE, '..', 'theory', 'stage_proposal.csv')

POWER = {'Attack': (3, 10), 'Hp': (3, 10), 'HpRegen': (3, 10)}   # (valueExponent, valueDivisor)
UPG = {  # 10.2 업그레이드
    'Attack': dict(valueMode='Power', priceExponent=3.5, priceScale=1125, maxLevel=17000),
    'Hp': dict(valueMode='Power', priceExponent=3.5, priceScale=1125, maxLevel=17000),
    'HpRegen': dict(valueMode='Power', priceExponent=3.5, priceScale=675, maxLevel=11000),
    'Speed': dict(maxLevel=275),
    'CritChance': dict(priceScale=225), 'CritDamage': dict(priceScale=169),
    'AdvancedAttack': dict(priceScale=141), 'NormalEnemyDamage': dict(priceScale=70),
}
CONSTS = dict(equipLevelBonus=0.01, equipTopGradeMul=10, ggomaLevelBonus=0.002, offlineCapHours=12, offlineGoldRate=0.5,
              adGachaPulls=4, adBoxGoldKills=4000, adBoxMyceliteBosses=20, goldBase=6.75, goldStep=1.5, goldExp=3.5,
              expAttackMulPerStage=11.2, expHpMulPerStage=5.6, enemyAtkHpRate=0.0029, enemyAtkRegenRate=0.063, bossAtkHpRate=0.0105)
OWNBASE = {0: 2, 1: 20, 2: 200, 3: 2000, 4: 20000}
MASCOT = {'leaf': 0.75, 'dew': 0.75, 'wind': 0.9, 'fire': 1.08, 'rock': 0.9, 'star': 1.30, 'coin': 0.9, 'chest': 1.08, 'beak': 1.56}
QUEST_MYC = {'kill': 75, 'stage': 75, 'upgrade': 75, 'gacha': 75, 'ad': 50}
QUEST_TARGET = {'stage': 3, 'upgrade': 20}

_orig = {}


def apply():
    T = P.T
    for st, ch in UPG.items():
        T.upgrades[st].update(ch)
    T.consts.update({k: float(v) for k, v in CONSTS.items()})
    for g in T.grades:
        g['ownBase'] = OWNBASE[g['grade']]
    for m in T.mascots:
        m['attackMultiplier'] = MASCOT[m['key']]
    for q in T.quests:
        q['mycelite'] = QUEST_MYC[q['kind']]
        if q['kind'] in QUEST_TARGET:
            q['target'] = QUEST_TARGET[q['kind']]
    rows = list(csv.DictReader(open(CSV, encoding='utf-8')))
    assert len(rows) == len(T.stages) == 1000, (len(rows), len(T.stages))
    for r, old in zip(rows, T.stages):
        assert int(r['stage']) == old['stage']
        for k, v in r.items():
            if k == 'stage':
                continue
            old[k] = float(v) if k in ('enemyHp', 'bossHp', 'enemyAttack', 'bossAttack', 'bossTime') else int(float(v))
    _orig.update(value=P.value, enemy_hp=P.enemy_hp, boss_hp=P.boss_hp)

    def value(stat, lv):
        r = P.d(stat)
        if r.get('valueMode') == 'Power':
            lv = P.clamp(lv, 0, r['maxLevel'])
            e, dv = POWER[stat]
            return r['baseValue'] * math.pow(1 + lv / dv, e)
        return _orig['value'](stat, lv)

    def enemy_hp(stage, wave):
        g = 1 + P.C('enemyHpWaveGrowth') * (wave - 1)
        if P.in_table(stage):
            return P.stage_row(stage)['enemyHp'] * g
        return (P.expected_hit(stage) * value('NormalEnemyDamage', P.expected_level('NormalEnemyDamage', stage))
                * P.C('enemyHpHits') * g)

    def boss_hp(stage):
        if P.in_table(stage):
            return P.stage_row(stage)['bossHp']
        return P.expected_dps(stage) * P.C('bossKillSeconds')

    P.value, P.enemy_hp, P.boss_hp = value, enemy_hp, boss_hp


apply()

if __name__ == '__main__':
    print('편성', P.default_formation(), sum(P.default_formation()))
    for L in (0, 1, 10, 15, 100, 1000, 15000, 17000):
        print('공격력 Lv', L, '%.4g' % P.value('Attack', L), '비용 %.4g' % P.cost('Attack', L))
    print('Speed max', P.value('Speed', 10 ** 6))
