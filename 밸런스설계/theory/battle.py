# IdleBattle.cs 전투 규칙의 결정적 시간 단계 시뮬 (난수 대신: 첫 투척 시차 고정, 대상은 돌아가며, 치명타는 기대 배율)
# 같은 입력이면 같은 결과.
import formulas as F

DT = 0.02
FLIGHT = 0.45          # flightTime
WAVE_DELAY = 0.6       # waveDelay (보스 처치·실패·패배 뒤는 ×2)
ATTACK_DELAY = 0.4     # enemyAttackDelay
TARGET_AGE = 0.15      # PickTarget 나이 조건


class Player:
    """강화 레벨 + 장비 보유 효과(공격 %·체력 %) + 편성 배율"""

    def __init__(self, levels=None, atk_pct=0.0, hp_pct=0.0, formation=None):
        self.lv = dict.fromkeys(F.STATS, 0)
        if levels:
            self.lv.update(levels)
        self.atk_pct, self.hp_pct = atk_pct, hp_pct
        self.formation = list(formation or F.OV.get('formation') or F.DEFAULT_FORMATION)

    def v(self, stat):
        return F.value(stat, self.lv[stat])

    def final_attack(self):
        return self.v('Attack') * (1 + self.atk_pct / 100) * self.v('AdvancedAttack')

    def crit_factor(self):
        return 1 + min(1, self.v('CritChance')) * (self.v('CritDamage') - 1)

    def max_hp(self):
        return self.v('Hp') * (1 + self.hp_pct / 100)

    def regen(self):
        return self.v('HpRegen') * (1 + self.hp_pct / 100)

    def boss_dps(self):
        return self.final_attack() * sum(self.formation) * self.v('Speed') * self.crit_factor()

    def key(self):
        return (tuple(self.lv[s] for s in F.STATS), round(self.atk_pct, 3), round(self.hp_pct, 3), tuple(self.formation))


class Result:
    __slots__ = ('ok', 'time', 'gold', 'kills', 'reason', 'boss_used', 'min_hp', 'wave_reached')

    def __repr__(self):
        return (f"ok={self.ok} t={self.time:.1f}s gold={self.gold:.0f} reason={self.reason} "
                f"boss_used={self.boss_used:.1f} min_hp={self.min_hp:.2f} wave={self.wave_reached}")


def attempt(stage, p, max_time=400.0):
    """스테이지 한 번 도전: 1웨이브 → 보스. 성공/실패와 걸린 시간(다음 판 시작까지 대기 포함)·번 골드"""
    nw = F.waves(stage)
    rate = max(0.05, p.v('Speed'))
    crit = p.crit_factor()
    fa = p.final_attack()
    ned = p.v('NormalEnemyDamage')
    max_hp, regen = p.max_hp(), p.regen()
    e_atk, b_atk = F.enemy_attack(stage), F.boss_attack(stage)
    g_enemy, g_boss = F.enemy_gold(stage), F.boss_gold(stage)

    slots = [m for m in p.formation if m > 0]
    timers = [i * 0.16 for i in range(len(slots))]     # 게임: Random.Range(0, 0.8)
    rr = 0
    enemies = []   # [hp, incoming, age, boss, dead]
    shots = []     # [target, t, dmg]
    hp = max_hp
    min_hp = 1.0
    gold = kills = 0
    t = 0.0
    wave = 0
    next_in = 0.0
    boss = None
    boss_left = 0.0
    boss_used = 0.0

    def start(w):
        nonlocal wave, boss, boss_left
        wave = w
        if w > nw:
            boss_left = F.boss_time(stage)
            boss = [F.boss_hp(stage), 0.0, 0.0, True, False]
            enemies.append(boss)
        else:
            h = F.enemy_hp(stage, w)
            for _ in range(F.enemy_count(stage, w)):
                enemies.append([h, 0.0, 0.0, False, False])

    start(1)
    r = Result()
    while t < max_time:
        t += DT
        # 꼬마
        for i, m in enumerate(slots):
            timers[i] -= DT
            if timers[i] > 0:
                continue
            cand = [e for e in enemies if not e[4] and e[2] > TARGET_AGE and e[0] - e[1] > 0]
            if not cand:
                cand = [e for e in enemies if not e[4] and e[2] > TARGET_AGE]
            if not cand:
                timers[i] = 0.1
                continue
            tgt = cand[rr % len(cand)]
            rr += 1
            timers[i] += 1 / rate
            dmg = fa * m * crit * (1 if tgt[3] else ned)
            tgt[1] += dmg
            shots.append([tgt, 0.0, dmg])
        # 수확기
        keep = []
        for s in shots:
            s[1] += DT / FLIGHT
            if s[1] < 1:
                keep.append(s)
                continue
            e = s[0]
            e[1] = max(0.0, e[1] - s[2])
            if e[4]:
                continue
            e[0] -= s[2]
            if e[0] <= 0:
                e[4] = True
                kills += 1
                gold += g_boss if e[3] else g_enemy
                if e[3]:
                    r.ok, r.time, r.gold, r.kills, r.reason = True, t + WAVE_DELAY * 2, gold, kills, 'clear'
                    r.boss_used, r.min_hp, r.wave_reached = F.boss_time(stage) - boss_left, min_hp, wave
                    return r
        shots = keep
        enemies = [e for e in enemies if not e[4]]
        for e in enemies:
            e[2] += DT
        # 체력
        dps = sum((b_atk if e[3] else e_atk) for e in enemies if e[2] > ATTACK_DELAY)
        hp = min(max_hp, hp + regen * DT - dps * DT)
        min_hp = min(min_hp, hp / max_hp)
        if hp <= 0:
            r.ok, r.time, r.gold, r.kills, r.reason = False, t + WAVE_DELAY * 2, gold, kills, 'defeat'
            r.boss_used, r.min_hp, r.wave_reached = 0.0, 0.0, wave
            return r
        # 흐름
        if boss is not None:
            boss_left -= DT
            if boss_left <= 0:
                r.ok, r.time, r.gold, r.kills, r.reason = False, t + WAVE_DELAY * 2, gold, kills, 'boss_timeout'
                r.boss_used, r.min_hp, r.wave_reached = F.boss_time(stage), min_hp, wave
                r.boss_used = F.boss_time(stage)
                return r
            continue
        if enemies:
            continue
        if next_in > 0:
            next_in -= DT
            if next_in > 0:
                continue
            start(wave + 1)
            continue
        next_in = WAVE_DELAY
    r.ok, r.time, r.gold, r.kills, r.reason = False, t, gold, kills, 'stall'
    r.boss_used, r.min_hp, r.wave_reached = 0.0, min_hp, wave
    return r


if __name__ == '__main__':
    import sys
    for s in range(1, 8):
        print(s, attempt(s, Player()))
