# 던전 레벨 수치안 (기준 4). 던전 레벨 L의 적 강도·보상을 방치 스테이지 척도에 묶는다.
#   기준 스테이지 s_L = baseStage + stageStep × (L − 1)  (던전 시트 새 열)
#   클리어 = 방치 전투력으로 s_L의 보스(× hpMul)를 제한 시간 안에 잡는다 (LoS 보스 러시처럼 자동 전투).
#     던전 1(InGame 수확 판)은 손 플레이라 모델에서는 같은 전투력 판정으로 근사한다.
#   보상 (클리어 때): 골드 = EnemyGold(s_L) × rewardKills,  균사석 = rewardMyc × (1 + rewardGrowth × (L − 1))
#   실패: 보상 없음, 입장 횟수는 쓴다(레벨은 그대로). 자동 다음 레벨: 깨면 L+1로 다음 입장.
import formulas as F
import battle as B

# 던전 시트 수치안 (dungeon: currency, baseStage, stageStep, hpMul, rewardKills, rewardMyc, rewardGrowth)
DUNGEONS = {
    1: dict(name='수확(골드)', currency='Gold', baseStage=5, stageStep=3, hpMul=1.0, rewardKills=12000, rewardMyc=0, rewardGrowth=0.0),
    2: dict(name='보스 러시(균사석)', currency='Mycelite', baseStage=15, stageStep=3, hpMul=1.0, rewardKills=0, rewardMyc=5, rewardGrowth=0.01),
    3: dict(name='골드 러시(골드)', currency='Gold', baseStage=30, stageStep=3, hpMul=1.0, rewardKills=15000, rewardMyc=0, rewardGrowth=0.0),
    4: dict(name='보스 러시 II(균사석)', currency='Mycelite', baseStage=80, stageStep=3, hpMul=1.0, rewardKills=0, rewardMyc=6, rewardGrowth=0.01),
    5: dict(name='심층(골드+균사석)', currency='Both', baseStage=150, stageStep=3, hpMul=1.0, rewardKills=15000, rewardMyc=4, rewardGrowth=0.01),
}
KINDS = set(DUNGEONS)


def ref_stage(dn, lv):
    d = DUNGEONS[dn]
    return int(d['baseStage'] + d['stageStep'] * (lv - 1))


def boss_test(s, p, hp_mul):
    """보스만: 방치 보스 체력 × hpMul을 보스 시간 안에 (보스 공격 버티며) 잡는가 — 간단히 초당 피해와 생존으로"""
    t = F.boss_hp(s) * hp_mul / max(1e-9, p.boss_dps())
    if t > F.boss_time(s):
        return False
    hurt = (F.boss_attack(s) - p.regen()) * t
    return hurt < p.max_hp()


# V4 (v3): 던전 레벨 상한 = 방치 진행. s_L이 지금 방치 스테이지보다 높으면 도전하지 않고
#   마지막으로 깬 레벨(L−1)의 보상을 소탕으로 받는다 (레벨은 그대로). False면 v2처럼 무조건 도전.
CAP_TO_IDLE = True
SWEEP_MUL = 0.5      # D8: 소탕 = 깬 레벨 보상 × 0.5 (장기 던전 골드 비중 ≤ 50%)


def reward(dn, lv):
    d = DUNGEONS[dn]
    s = ref_stage(dn, lv)
    return F.enemy_gold(s) * d['rewardKills'], d['rewardMyc'] * (1 + d['rewardGrowth'] * (lv - 1))


def run(dn, lv, sim):
    d = DUNGEONS[dn]
    s = ref_stage(dn, lv)
    cnt = sim.__dict__.setdefault('dcount', {'try': 0, 'fail': 0, 'sweep': 0})
    if CAP_TO_IDLE and s > sim.stage:
        cnt['sweep'] += 1
        if lv <= 1:
            return 0.0, 0.0, False
        g, m = reward(dn, lv - 1)
        return g * SWEEP_MUL, m * SWEEP_MUL, False
    cnt['try'] += 1
    if not boss_test(s, sim.p, d['hpMul']):
        cnt['fail'] += 1
        if CAP_TO_IDLE and lv > 1:      # 실패해도 소탕 보상 (헛입장 없음)
            g, m = reward(dn, lv - 1)
            return g * SWEEP_MUL, m * SWEEP_MUL, False
        return 0.0, 0.0, False
    g, m = reward(dn, lv)
    return g, m, True
