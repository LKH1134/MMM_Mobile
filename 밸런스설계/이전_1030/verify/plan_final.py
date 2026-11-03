# -*- coding: utf-8 -*-
"""현재 C# (2026-10-30 최종: costScale·GoldBase 30). SPEED_SCALE 환경변수로 공격 속도 상한 해제안 비교."""
import math, os, port
SPEED_SCALE = float(os.environ.get("SPEED_SCALE", "0"))   # 0 = 지금 (maxLv 150, 지수 항 없음)
NAME = "final" if SPEED_SCALE == 0 else f"speed-uncapped cs={SPEED_SCALE:g}"
SCALE = {"Attack": 2250, "Hp": 2250, "HpRegen": 1350, "Speed": SPEED_SCALE, "CritChance": 450, "CritDamage": 338, "AdvancedAttack": 281, "NormalEnemyDamage": 141}
SPEED_MAX = 150 if SPEED_SCALE == 0 else 10**9
def cost(stat, lv):
    d = port.DEFS[stat]
    c = d["cb"] * math.pow(1 + lv / 10.0, d["ce"])
    if SCALE[stat] > 0: c *= math.exp(lv / SCALE[stat])
    return c
def enemy_gold(s): return max(1, round(30 * math.pow(1 + (s - 1) / 1.5, 2.5)))
def expected_level(stat, stage):
    if stat == "Speed": return min(SPEED_MAX, int(1.5 * max(0, stage - 1)))
    return port.expected_level(stat, stage)
