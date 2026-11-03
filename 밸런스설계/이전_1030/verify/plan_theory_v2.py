# -*- coding: utf-8 -*-
"""verify 독립 구현: 이론.md 7절 적용안(1~5)을 C# 조각 그대로 port.py 위에 옮김 (theory/model.py를 쓰지 않음).
  Cost = (long)Min(9e18, Ceiling(costBase·(1+lv/10.0)^costExp · (costScale>0 ? Exp(lv/costScale) : 1)))
  EnemyGold = (long)Max(1, Round(30·Pow(1 + (stage-1)/1.5, 2.5)))   (Math.Round = 은행가)
  maxLv: Attack/Hp 45000, HpRegen 28000
"""
import copy
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import port  # noqa: E402

NAME = "theory 7절 적용안 (verify 독립 구현, maxLv 45000/28000)"
COST_SCALE = {"Attack": 2250, "Hp": 2250, "HpRegen": 1350, "Speed": 0, "CritChance": 450,
              "CritDamage": 338, "AdvancedAttack": 281, "NormalEnemyDamage": 141}
MAXLV = {"Attack": 45000, "Hp": 45000, "HpRegen": 28000}
GOLD_BASE, GOLD_STEP, GOLD_EXP = 30.0, 1.5, 2.5

DEFS = copy.deepcopy(port.DEFS)
for k, v in MAXLV.items():
    DEFS[k]["maxLv"] = v


def cost_raw(stat, lv):
    d = DEFS[stat]
    c = d["cb"] * math.pow(1 + lv / 10.0, d["ce"])
    if COST_SCALE[stat] > 0:
        c *= math.exp(lv / COST_SCALE[stat])
    return c


def cost(stat, lv):          # check.py용: 9e18 자르기 전 값
    return cost_raw(stat, lv)


def cost_cs(stat, lv):       # C#과 같은 long 결과
    return int(min(port.LONG_CAP, math.ceil(cost_raw(stat, lv))))


def enemy_gold(stage):
    return int(max(1, round(GOLD_BASE * math.pow(1 + (stage - 1) / GOLD_STEP, GOLD_EXP))))


def expected_level(stat, stage):
    """port.expected_level 을 바뀐 maxLv로 (port.DEFS 를 잠깐 바꿔 호출)."""
    saved = port.DEFS
    port.DEFS = DEFS
    try:
        return port.expected_level(stat, stage)
    finally:
        port.DEFS = saved
